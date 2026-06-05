using Arabica.Application.Mesajlasma;
using Arabica.Application.Transferler;
using Arabica.Contracts.Api;
using Arabica.Domain.Subeler;
using Arabica.Domain.Transferler;
using Arabica.Infrastructure.Transferler;
using Arabica.Infrastructure.Veri;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Arabica.Integration.Tests;

/// <summary>
/// Real-Postgres tests (Testcontainers). Proves the HOTFIX atomic completion: approving a Personel transfer
/// moves staff across the hot/hist schema boundary in ONE transaction (source −N, target +N) together with
/// the transfer completion + outbox row; insufficient staff rejects with NO change; and the schemas stay
/// isolated.
/// </summary>
public sealed class PostgresEntegrasyonTests(PostgresFixture fixture) : IClassFixture<PostgresFixture>
{
    private static readonly DateTimeOffset An = new(2026, 6, 5, 9, 0, 0, TimeSpan.Zero);

    private sealed class SessizNotifier : IDashboardNotifier
    {
        public Task DolulukYayinlaAsync(IReadOnlyList<SubeDolulukYaniti> a, CancellationToken ct) => Task.CompletedTask;
        public Task TransferBildirimiYayinlaAsync(TransferBildirimGorunumu b, CancellationToken ct) => Task.CompletedTask;
    }

    private TransferTamamlamaServisi Servis(HistoryDbContext hist, HotDbContext hot)
        => new(hist, hot, new Outbox(hist), new SabitZaman(), new SessizNotifier(), DolulukEsikleri.Varsayilan);

    [SkippableFact]
    public async Task Onayla_personel_transferi_atomik_olarak_personel_tasir()
    {
        Skip.IfNot(fixture.Kullanilabilir, fixture.AtlamaSebebi);
        await SubeleriTohumlaAsync(kaynakId: 201, kaynakPersonel: 5, hedefId: 202, hedefPersonel: 2);
        var emirId = await PersonelTransferiTohumlaAsync(201, 202, adet: 2);

        await using (var hist = fixture.HistoryContext())
        await using (var hot = fixture.HotContext())
        {
            var sonuc = await Servis(hist, hot).OnaylaAsync(emirId, CancellationToken.None);
            sonuc.Should().BeOfType<TransferTamamlamaSonucu.Tamamlandi>();
        }

        await using var d = fixture.HotContext();
        (await d.Subeler.AsNoTracking().FirstAsync(s => s.SubeId == 201)).AktifPersonelSayisi.Should().Be(3); // 5 − 2
        (await d.Subeler.AsNoTracking().FirstAsync(s => s.SubeId == 202)).AktifPersonelSayisi.Should().Be(4); // 2 + 2
        await using var h = fixture.HistoryContext();
        (await h.TransferEmirleri.AsNoTracking().FirstAsync(e => e.EmirId == emirId)).Durum.Should().Be(TransferDurumu.Tamamlandi);
        (await h.Outbox.AsNoTracking().CountAsync(o => o.OlayTipi == "TransferTamamlandi")).Should().Be(1);
    }

    [SkippableFact]
    public async Task Onayla_yetersiz_personel_atomik_olarak_hicbir_sey_degistirmez()
    {
        Skip.IfNot(fixture.Kullanilabilir, fixture.AtlamaSebebi);
        await SubeleriTohumlaAsync(kaynakId: 211, kaynakPersonel: 1, hedefId: 212, hedefPersonel: 0);
        var emirId = await PersonelTransferiTohumlaAsync(211, 212, adet: 5); // 5 > 1

        await using (var hist = fixture.HistoryContext())
        await using (var hot = fixture.HotContext())
        {
            var sonuc = await Servis(hist, hot).OnaylaAsync(emirId, CancellationToken.None);
            sonuc.Should().BeOfType<TransferTamamlamaSonucu.YetersizPersonel>()
                .Which.Mevcut.Should().Be(1);
        }

        await using var d = fixture.HotContext();
        (await d.Subeler.AsNoTracking().FirstAsync(s => s.SubeId == 211)).AktifPersonelSayisi.Should().Be(1); // değişmedi
        (await d.Subeler.AsNoTracking().FirstAsync(s => s.SubeId == 212)).AktifPersonelSayisi.Should().Be(0);
        await using var h = fixture.HistoryContext();
        (await h.TransferEmirleri.AsNoTracking().FirstAsync(e => e.EmirId == emirId)).Durum.Should().Be(TransferDurumu.Bekliyor);
        (await h.Outbox.AsNoTracking().CountAsync(o => o.Anahtar == "211")).Should().Be(0);
    }

    [SkippableFact]
    public async Task Hot_ve_hist_semalari_ayni_veritabaninda_izole_calisir()
    {
        Skip.IfNot(fixture.Kullanilabilir, fixture.AtlamaSebebi);

        await using var hot = fixture.HotContext();
        hot.Subeler.Add(new Sube(101, "Isparta Merkez", maksimumKapasite: 120, anlikMusteriSayisi: 90));
        await hot.SaveChangesAsync();

        await using var hist = fixture.HistoryContext();
        await PersonelTransferiTohumlaAsync(1, 2, 1);

        (await fixture.HotContext().Subeler.AsNoTracking().AnyAsync(s => s.SubeId == 101)).Should().BeTrue();
        (await fixture.HistoryContext().TransferEmirleri.AsNoTracking().AnyAsync()).Should().BeTrue();
    }

    private async Task SubeleriTohumlaAsync(int kaynakId, int kaynakPersonel, int hedefId, int hedefPersonel)
    {
        await using var hot = fixture.HotContext();
        hot.Subeler.Add(new Sube(kaynakId, $"Kaynak {kaynakId}", 100, anlikMusteriSayisi: 40, aktifPersonelSayisi: kaynakPersonel));
        hot.Subeler.Add(new Sube(hedefId, $"Hedef {hedefId}", 100, anlikMusteriSayisi: 80, aktifPersonelSayisi: hedefPersonel));
        await hot.SaveChangesAsync();
    }

    private async Task<long> PersonelTransferiTohumlaAsync(int kaynak, int hedef, int adet)
    {
        await using var hist = fixture.HistoryContext();
        var emir = new TransferEmriFactory().PersonelTransferiOlustur(kaynak, hedef, adet, An);
        await hist.TransferEmirleri.AddAsync(emir);
        await hist.SaveChangesAsync();
        return emir.EmirId;
    }
}
