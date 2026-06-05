using Arabica.Domain.Subeler;
using FluentAssertions;
using Xunit;

namespace Arabica.Domain.Tests;

/// <summary>Occupancy calculation and level-threshold tests for <see cref="Sube"/>.</summary>
public sealed class SubeTests
{
    [Theory]
    [InlineData(50, 200, 25.00)]
    [InlineData(0, 200, 0.00)]
    [InlineData(200, 200, 100.00)]
    [InlineData(130, 100, 130.00)] // kapasite aşımı senaryosu (%130)
    [InlineData(1, 3, 33.33)]      // yuvarlama (2 ondalık)
    public void DolulukOraniHesapla_dogru_yuzde_uretir(int musteri, int kapasite, decimal beklenen)
    {
        var sube = new Sube(1, "Isparta Merkez", kapasite, musteri);

        sube.DolulukOraniHesapla().Should().Be(beklenen);
    }

    [Theory]
    [InlineData(30, DolulukSeviyesi.Yesil)]   // <= 60
    [InlineData(60, DolulukSeviyesi.Yesil)]   // sınır
    [InlineData(75, DolulukSeviyesi.Sari)]    // 60 < x <= 85
    [InlineData(85, DolulukSeviyesi.Sari)]    // sınır
    [InlineData(95, DolulukSeviyesi.Kirmizi)] // > 85
    [InlineData(130, DolulukSeviyesi.Kirmizi)]
    public void SeviyeHesapla_varsayilan_esiklerle_dogru_seviye_verir(int musteri, DolulukSeviyesi beklenen)
    {
        var sube = new Sube(1, "S.D.Ü. Kampüs", maksimumKapasite: 100, anlikMusteriSayisi: musteri);

        sube.SeviyeHesapla(DolulukEsikleri.Varsayilan).Should().Be(beklenen);
    }

    [Fact]
    public void Sifir_veya_negatif_kapasite_reddedilir()
    {
        var eylem = () => new Sube(1, "Test", maksimumKapasite: 0);

        eylem.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void Bos_sube_adi_reddedilir()
    {
        var eylem = () => new Sube(1, "  ", maksimumKapasite: 100);

        eylem.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void MusteriSayisiniGuncelle_orani_degistirir()
    {
        var sube = new Sube(1, "Meydan", maksimumKapasite: 100, anlikMusteriSayisi: 10);

        sube.MusteriSayisiniGuncelle(90);

        sube.DolulukOraniHesapla().Should().Be(90.00m);
    }
}
