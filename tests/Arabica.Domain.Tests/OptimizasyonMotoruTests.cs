using Arabica.Domain.IsHukuku;
using Arabica.Domain.Optimizasyon;
using Arabica.Domain.Subeler;
using Arabica.Domain.Transferler;
using FluentAssertions;
using Xunit;

namespace Arabica.Domain.Tests;

/// <summary>
/// Engine + strategy + factory + guard integration (all pure domain, so still unit tests).
/// Covers seasonal Strategy behavior and the İş Kanunu veto in the recommendation flow.
/// </summary>
public sealed class OptimizasyonMotoruTests
{
    private static readonly DateTimeOffset An = new(2026, 6, 5, 12, 0, 0, TimeSpan.FromHours(3));

    private static OptimizasyonMotoru Motor(IOptimizasyonServisi strateji, IsKanunuLimitleri? limitler = null) =>
        new(strateji, new TransferEmriFactory(), IsKanunuDegerlendirici.Varsayilan(limitler));

    [Fact]
    public void VizeFinal_stratejisi_yaz_dan_daha_dusuk_doluluk_ta_darbogaz_ilan_eder()
    {
        // %80 doluluk: vize/final (Sarı üst 75) ⇒ Kırmızı/darboğaz; yaz (Sarı üst 95) ⇒ Sarı/değil.
        var sube = new Sube(2, "S.D.Ü. Kampüs", maksimumKapasite: 100, anlikMusteriSayisi: 80);

        var vizeFinal = new VizeFinalSezonStratejisi().DarbogazHesapla(sube);
        var yaz = new YazDonemiStratejisi().DarbogazHesapla(sube);

        vizeFinal.DarbogazMi.Should().BeTrue();
        vizeFinal.Seviye.Should().Be(DolulukSeviyesi.Kirmizi);
        yaz.DarbogazMi.Should().BeFalse();
    }

    [Fact]
    public void DarbogazTespitiYap_tum_subeleri_siniflandirir()
    {
        var motor = Motor(new VizeFinalSezonStratejisi());
        var subeler = new[]
        {
            new Sube(1, "Merkez", 100, anlikMusteriSayisi: 30),   // Yeşil / atıl
            new Sube(2, "Kampüs", 100, anlikMusteriSayisi: 90)    // Kırmızı / darboğaz
        };

        var sonuc = motor.DarbogazTespitiYap(subeler);

        sonuc.Should().HaveCount(2);
        sonuc.Single(s => s.Sube.SubeId == 1).AtilMi.Should().BeTrue();
        sonuc.Single(s => s.Sube.SubeId == 2).DarbogazMi.Should().BeTrue();
    }

    [Fact]
    public void TransferOnerisiUret_uygun_aday_varsa_Bekliyor_emri_dondurur()
    {
        var motor = Motor(new VizeFinalSezonStratejisi());
        var kaynak = new Sube(1, "Merkez", 100, anlikMusteriSayisi: 20, aktifPersonelSayisi: 4);
        var hedef = new Sube(2, "Kampüs", 100, anlikMusteriSayisi: 95);
        var adaylar = new[]
        {
            new BaristaMesaiBaglami(11, TimeSpan.FromHours(3), TimeSpan.FromHours(15), TimeSpan.FromHours(1))
        };

        var sonuc = motor.TransferOnerisiUret(kaynak, hedef, adaylar, seyahatSuresi: TimeSpan.FromMinutes(20), An);

        var basarili = sonuc.Should().BeOfType<OneriSonucu.Basarili>().Subject;
        basarili.Emri.Durum.Should().Be(TransferDurumu.Bekliyor);
        basarili.Emri.Tip.Should().Be(KaynakTipi.Personel);
        basarili.Emri.KaynakSubeId.Should().Be(1);
        basarili.Emri.HedefSubeId.Should().Be(2);
    }

    [Fact]
    public void TransferOnerisiUret_tum_adaylar_is_kanunu_na_takilirsa_reddeder()
    {
        var motor = Motor(new VizeFinalSezonStratejisi());
        var kaynak = new Sube(1, "Merkez", 100, anlikMusteriSayisi: 20, aktifPersonelSayisi: 4);
        var hedef = new Sube(2, "Kampüs", 100, anlikMusteriSayisi: 95);
        var adaylar = new[]
        {
            // 10.5s çalışmış; 1s yol ile 11.5s > 11s ⇒ günlük azami ihlali.
            new BaristaMesaiBaglami(11, TimeSpan.FromHours(10.5), TimeSpan.FromHours(40), TimeSpan.FromHours(2))
        };

        var sonuc = motor.TransferOnerisiUret(kaynak, hedef, adaylar, seyahatSuresi: TimeSpan.FromHours(1), An);

        var reddedildi = sonuc.Should().BeOfType<OneriSonucu.Reddedildi>().Subject;
        reddedildi.Gerekce.Should().Contain("4857");
    }
}
