using Arabica.Application.Cikti;
using Arabica.Application.Mesajlasma;
using Arabica.Application.Ortak;
using Arabica.Application.Transferler;
using Arabica.Contracts.Api;
using Arabica.Domain.Subeler;
using Arabica.Domain.Transferler;
using Arabica.Infrastructure.Veri;
using Microsoft.EntityFrameworkCore;

namespace Arabica.Infrastructure.Transferler;

/// <summary>
/// Approves + completes a transfer and moves staff ATOMICALLY.
/// • Relational (PostgreSQL): everything runs in ONE transaction on the hist connection. Staff deltas are a
///   parameterized cross-schema UPDATE on hot.sube; the INSUFFICIENT-STAFF guard is the conditional UPDATE's
///   rows-affected (atomic, no TOCTOU). Hot/Hist stay in separate schemas — only the transaction is shared.
/// • EF InMemory (tests): raw SQL isn't supported, so it uses the domain methods on tracked entities.
/// Either way: synchronous, exactly-once (state machine blocks terminal re-approve), reflected immediately
/// (SignalR DolulukGuncellendi pushed after commit).
/// </summary>
public sealed class TransferTamamlamaServisi(
    HistoryDbContext hist,
    HotDbContext hot,
    IOutbox outbox,
    IZamanSaglayici zaman,
    IDashboardNotifier notifier,
    DolulukEsikleri esikler) : ITransferTamamlayici
{
    public async Task<TransferTamamlamaSonucu> OnaylaAsync(long transferId, CancellationToken ct)
    {
        var sonuc = hist.Database.IsRelational()
            ? await RelationalAsync(transferId, ct)
            : await BellekIciAsync(transferId, ct);

        if (sonuc is TransferTamamlamaSonucu.Tamamlandi)
            await DolulukYayinlaAsync(ct); // immediate dashboard refresh
        return sonuc;
    }

    // ---- PostgreSQL: single shared transaction on the hist connection ----
    private async Task<TransferTamamlamaSonucu> RelationalAsync(long transferId, CancellationToken ct)
    {
        var emir = await hist.TransferEmirleri.FirstOrDefaultAsync(e => e.EmirId == transferId, ct);
        if (emir is null) return new TransferTamamlamaSonucu.Bulunamadi(transferId);
        if (emir.Durum != TransferDurumu.Bekliyor)
            throw new InvalidOperationException($"Geçersiz durum geçişi: {emir.Durum} → Onaylandi."); // → 409

        var personel = emir.Tip == KaynakTipi.Personel;

        await using var tx = await hist.Database.BeginTransactionAsync(ct);

        if (personel)
        {
            // Atomic guard + decrement: affects 1 row only if the source is active AND has >= Adet staff.
            var etkilenen = await hist.Database.ExecuteSqlRawAsync(
                "UPDATE hot.sube SET aktif_personel_sayisi = aktif_personel_sayisi - {0} " +
                "WHERE sube_id = {1} AND aktif = true AND aktif_personel_sayisi >= {0}",
                [emir.Adet, emir.KaynakSubeId], ct);

            if (etkilenen == 0)
            {
                await tx.RollbackAsync(ct);
                var mevcut = await KaynakPersonelOkuAsync(emir.KaynakSubeId, ct);
                return new TransferTamamlamaSonucu.YetersizPersonel(emir.Adet, mevcut);
            }

            await hist.Database.ExecuteSqlRawAsync(
                "UPDATE hot.sube SET aktif_personel_sayisi = aktif_personel_sayisi + {0} WHERE sube_id = {1}",
                [emir.Adet, emir.HedefSubeId], ct);
        }

        emir.DurumGuncelle("ONAYLANDI");
        emir.DurumGuncelle("TAMAMLANDI");
        outbox.Ekle(TransferOlayFabrikasi.Olustur(emir, zaman.Simdi),
            emir.KaynakSubeId.ToString(System.Globalization.CultureInfo.InvariantCulture), zaman.Simdi);
        emir.OlaylariTemizle();

        await hist.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);

        return new TransferTamamlamaSonucu.Tamamlandi(emir.EmirId, emir.KaynakSubeId, emir.HedefSubeId, emir.Tip.ToString(), emir.Adet, personel);
    }

    private async Task<int> KaynakPersonelOkuAsync(int subeId, CancellationToken ct)
    {
        var liste = await hist.Database.SqlQueryRaw<int>(
            "SELECT aktif_personel_sayisi AS \"Value\" FROM hot.sube WHERE sube_id = {0}", subeId).ToListAsync(ct);
        return liste.Count > 0 ? liste[0] : 0;
    }

    // ---- EF InMemory (tests): domain methods on tracked entities ----
    private async Task<TransferTamamlamaSonucu> BellekIciAsync(long transferId, CancellationToken ct)
    {
        var emir = await hist.TransferEmirleri.FirstOrDefaultAsync(e => e.EmirId == transferId, ct);
        if (emir is null) return new TransferTamamlamaSonucu.Bulunamadi(transferId);
        if (emir.Durum != TransferDurumu.Bekliyor)
            throw new InvalidOperationException($"Geçersiz durum geçişi: {emir.Durum} → Onaylandi.");

        var personel = emir.Tip == KaynakTipi.Personel;
        Sube? kaynak = null, hedef = null;
        if (personel)
        {
            kaynak = await hot.Subeler.FirstOrDefaultAsync(s => s.SubeId == emir.KaynakSubeId, ct);
            hedef = await hot.Subeler.FirstOrDefaultAsync(s => s.SubeId == emir.HedefSubeId, ct);
            if (kaynak is null || !kaynak.PersonelCikarabilirMi(emir.Adet))
                return new TransferTamamlamaSonucu.YetersizPersonel(emir.Adet, kaynak?.AktifPersonelSayisi ?? 0);
        }

        emir.DurumGuncelle("ONAYLANDI");
        emir.DurumGuncelle("TAMAMLANDI");
        if (personel)
        {
            kaynak!.PersonelCikar(emir.Adet);
            hedef?.PersonelEkle(emir.Adet);
        }

        outbox.Ekle(TransferOlayFabrikasi.Olustur(emir, zaman.Simdi),
            emir.KaynakSubeId.ToString(System.Globalization.CultureInfo.InvariantCulture), zaman.Simdi);
        emir.OlaylariTemizle();

        await hist.SaveChangesAsync(ct);
        await hot.SaveChangesAsync(ct);

        return new TransferTamamlamaSonucu.Tamamlandi(emir.EmirId, emir.KaynakSubeId, emir.HedefSubeId, emir.Tip.ToString(), emir.Adet, personel);
    }

    private async Task DolulukYayinlaAsync(CancellationToken ct)
    {
        var subeler = await hot.Subeler.AsNoTracking().Where(s => s.Aktif).OrderBy(s => s.SubeId).ToListAsync(ct);
        var yanit = subeler.Select(s => new SubeDolulukYaniti(
            s.SubeId, s.Ad, s.DolulukOraniHesapla(), s.MaksimumKapasite, s.AktifPersonelSayisi, s.SeviyeHesapla(esikler).ToString())).ToList();
        await notifier.DolulukYayinlaAsync(yanit, ct);
    }
}
