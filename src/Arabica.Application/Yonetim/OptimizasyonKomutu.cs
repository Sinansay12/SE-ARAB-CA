using Arabica.Application.Cikti;
using Arabica.Application.Denetim;
using Arabica.Application.Ortak;
using Arabica.Contracts.Api;
using Arabica.Contracts.Entegrasyon;
using Arabica.Domain.IsHukuku;
using Arabica.Domain.Optimizasyon;
using Arabica.Domain.Transferler;
using MediatR;

namespace Arabica.Application.Yonetim;

/// <summary>
/// Runs the optimization engine LIVE: detects bottleneck/idle branches under the active seasonal Strategy,
/// applies the İş Kanunu guards, and persists BEKLIYOR recommendations (Factory + outbox → ESB). Plain
/// IRequest (own unit of work — needs the DB id before the outbox event).
/// </summary>
public sealed record OptimizasyonTetikleCommand : IRequest<IReadOnlyList<TransferOneriYaniti>>;

public sealed class OptimizasyonTetikleCommandHandler(
    ISubeRepository subeRepo,
    IOptimizasyonStratejiResolver resolver,
    ITransferEmriFactory fabrika,
    IsKanunuDegerlendirici isKanunu,
    ITransferEmriRepository transferRepo,
    IOutbox outbox,
    IBirimIsi birimIsi,
    IZamanSaglayici zaman,
    IDenetimYazici denetim) : IRequestHandler<OptimizasyonTetikleCommand, IReadOnlyList<TransferOneriYaniti>>
{
    public async Task<IReadOnlyList<TransferOneriYaniti>> Handle(OptimizasyonTetikleCommand k, CancellationToken ct)
    {
        var subeler = await subeRepo.AktifleriGetirAsync(ct);
        var strateji = resolver.Sec(zaman.Simdi);
        var motor = new OptimizasyonMotoru(strateji, fabrika, isKanunu);

        var sonuclar = motor.DarbogazTespitiYap(subeler);
        var darbogazlar = sonuclar.Where(s => s.DarbogazMi).Select(s => s.Sube).ToList();
        var atillar = sonuclar.Where(s => s.AtilMi).Select(s => s.Sube).ToList();

        // İş Kanunu (4857) muhafızlarını geçen örnek aday (yol süresi mesaiye dahil).
        var adaylar = new[] { new BaristaMesaiBaglami(0, TimeSpan.FromHours(4), TimeSpan.FromHours(20), TimeSpan.FromHours(1)) };
        var seyahat = TimeSpan.FromMinutes(30);

        var uretilenler = new List<TransferOneriYaniti>();
        foreach (var darbogaz in darbogazlar)
        {
            var kaynak = atillar.FirstOrDefault(a => a.SubeId != darbogaz.SubeId);
            if (kaynak is null) continue;

            var oneri = motor.TransferOnerisiUret(kaynak, darbogaz, adaylar, seyahat, zaman.Simdi);
            if (oneri is not OneriSonucu.Basarili b) continue;

            await transferRepo.EkleAsync(b.Emri, ct);
            await birimIsi.KaydetAsync(ct);
            outbox.Ekle(new TransferOnerildi(b.Emri.EmirId, b.Emri.KaynakSubeId, b.Emri.HedefSubeId, b.Emri.Tip.ToString(), b.Emri.Adet, zaman.Simdi),
                b.Emri.KaynakSubeId.ToString(System.Globalization.CultureInfo.InvariantCulture), zaman.Simdi);
            await birimIsi.KaydetAsync(ct);

            uretilenler.Add(new TransferOneriYaniti(b.Emri.EmirId, b.Emri.KaynakSubeId, b.Emri.HedefSubeId, b.Emri.Tip.ToString(), b.Emri.Adet, b.Emri.Durum.ToString(), 0));
        }

        await denetim.YazAsync("ADMIN:OptimizasyonTetikle", $"strateji={strateji.SezonAnahtari}, üretilen öneri={uretilenler.Count}", ct);
        return uretilenler;
    }
}

/// <summary>Read the active optimization strategy (override or calendar default).</summary>
public sealed record StratejiQuery : ISorgu<StratejiYaniti>;

public sealed class StratejiQueryHandler(IStratejiSecimi secim, ITakvimAnomaliSaglayici takvim, IZamanSaglayici zaman)
    : IRequestHandler<StratejiQuery, StratejiYaniti>
{
    public Task<StratejiYaniti> Handle(StratejiQuery q, CancellationToken ct)
    {
        var aktif = secim.GecerliSecim ?? takvim.AktifSezon(zaman.Simdi);
        var aciklama = aktif == "yaz" ? "Yaz dönemi — yüksek tolerans" : "Vize/final sezonu — düşük tolerans";
        var ek = secim.GecerliSecim is null ? " (takvim varsayılanı)" : " (manuel geçersiz kılma)";
        return Task.FromResult(new StratejiYaniti(aktif, aciklama + ek));
    }
}

/// <summary>Override the active strategy at runtime (Strategy pattern, live). null/empty → reset to calendar.</summary>
public sealed record StratejiAyarlaCommand(string? Ad) : IRequest<StratejiYaniti>;

public sealed class StratejiAyarlaCommandHandler(IStratejiSecimi secim, ITakvimAnomaliSaglayici takvim, IZamanSaglayici zaman, IDenetimYazici denetim)
    : IRequestHandler<StratejiAyarlaCommand, StratejiYaniti>
{
    private static readonly string[] Gecerli = ["vize-final", "yaz"];

    public async Task<StratejiYaniti> Handle(StratejiAyarlaCommand k, CancellationToken ct)
    {
        var ad = string.IsNullOrWhiteSpace(k.Ad) ? null : k.Ad.Trim().ToLowerInvariant();
        if (ad is not null && !Gecerli.Contains(ad))
            throw new ArgumentException($"Geçersiz strateji: '{k.Ad}'. (vize-final | yaz)");

        secim.Ayarla(ad);
        await denetim.YazAsync("ADMIN:StratejiAyarla", $"strateji = {ad ?? "takvim-varsayılanı"}", ct);

        var aktif = ad ?? takvim.AktifSezon(zaman.Simdi);
        return new StratejiYaniti(aktif, ad is null ? "Takvim varsayılanına dönüldü" : "Manuel geçersiz kılma uygulandı");
    }
}
