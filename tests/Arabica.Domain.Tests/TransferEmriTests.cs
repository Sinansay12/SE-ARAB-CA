using Arabica.Domain.Transferler;
using FluentAssertions;
using Xunit;

namespace Arabica.Domain.Tests;

/// <summary>State-machine and creation-rule tests for <see cref="TransferEmri"/> (FR-9/FR-10).</summary>
public sealed class TransferEmriTests
{
    private static readonly DateTimeOffset An = new(2026, 6, 5, 10, 0, 0, TimeSpan.FromHours(3));
    private static readonly ITransferEmriFactory Fabrika = new TransferEmriFactory();

    private static TransferEmri YeniEmir() =>
        Fabrika.PersonelTransferiOlustur(kaynakSubeId: 1, hedefSubeId: 2, baristaAdedi: 1, An, emirId: 1045);

    [Fact]
    public void Yeni_emir_Bekliyor_durumunda_baslar()
    {
        var emir = YeniEmir();

        emir.Durum.Should().Be(TransferDurumu.Bekliyor);
        emir.Tip.Should().Be(KaynakTipi.Personel);
        emir.Olaylar.Should().BeEmpty();
    }

    [Theory]
    [InlineData("ONAYLANDI", TransferDurumu.Onaylandi)]
    [InlineData("onaylandi", TransferDurumu.Onaylandi)] // case-insensitive
    public void Bekliyor_durumundan_Onaylandi_gecisi_gecerli(string yeniDurum, TransferDurumu beklenen)
    {
        var emir = YeniEmir();

        emir.DurumGuncelle(yeniDurum);

        emir.Durum.Should().Be(beklenen);
        emir.Olaylar.Should().ContainSingle()
            .Which.Should().BeOfType<TransferDurumuDegistiOlayi>()
            .Which.YeniDurum.Should().Be(TransferDurumu.Onaylandi);
    }

    [Fact]
    public void Bekliyor_dan_Reddedildi_gerekce_ile_gecerli()
    {
        var emir = YeniEmir();

        emir.DurumGuncelle("REDDEDILDI", "Operasyonel uygun değil.");

        emir.Durum.Should().Be(TransferDurumu.Reddedildi);
        emir.RedGerekcesi.Should().Be("Operasyonel uygun değil.");
    }

    [Fact]
    public void Reddetme_gerekce_olmadan_ArgumentException_firlatir()
    {
        var emir = YeniEmir();

        var eylem = () => emir.DurumGuncelle("REDDEDILDI");

        eylem.Should().Throw<ArgumentException>();
        emir.Durum.Should().Be(TransferDurumu.Bekliyor); // değişmedi
        emir.Olaylar.Should().BeEmpty();                 // outbox'a hiçbir şey gitmez
    }

    [Fact]
    public void Onaylandi_dan_Tamamlandi_gecisi_gecerli()
    {
        var emir = YeniEmir();
        emir.DurumGuncelle("ONAYLANDI");

        emir.DurumGuncelle("TAMAMLANDI");

        emir.Durum.Should().Be(TransferDurumu.Tamamlandi);
        emir.Olaylar.Should().HaveCount(2);
    }

    [Theory]
    [InlineData("TAMAMLANDI")] // Bekliyor → Tamamlandi atlanamaz
    public void Bekliyor_dan_gecersiz_gecis_InvalidOperationException_firlatir(string yeniDurum)
    {
        var emir = YeniEmir();

        var eylem = () => emir.DurumGuncelle(yeniDurum);

        eylem.Should().Throw<InvalidOperationException>();
        emir.Durum.Should().Be(TransferDurumu.Bekliyor);
        emir.Olaylar.Should().BeEmpty();
    }

    [Fact]
    public void Terminal_Reddedildi_durumundan_gecis_yapilamaz()
    {
        var emir = YeniEmir();
        emir.DurumGuncelle("REDDEDILDI", "gerekçe");

        var eylem = () => emir.DurumGuncelle("ONAYLANDI");

        eylem.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void Bilinmeyen_durum_ArgumentException_firlatir()
    {
        // SRS Test Durumu 2: "BILINMEYEN_DURUM" → IllegalArgumentException (C#: ArgumentException)
        var emir = YeniEmir();

        var eylem = () => emir.DurumGuncelle("BILINMEYEN_DURUM");

        eylem.Should().Throw<ArgumentException>();
        emir.Durum.Should().Be(TransferDurumu.Bekliyor);
        emir.Olaylar.Should().BeEmpty();
    }

    [Fact]
    public void Numerik_durum_degeri_de_reddedilir()
    {
        var emir = YeniEmir();

        var eylem = () => emir.DurumGuncelle("1");

        eylem.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Fabrika_kaynak_ve_hedef_ayni_ise_hata_verir()
    {
        var eylem = () => Fabrika.PersonelTransferiOlustur(1, 1, 1, An);

        eylem.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Fabrika_ekipman_transferi_uretebilir()
    {
        var emir = Fabrika.EkipmanTransferiOlustur(2, 1, 1, An);

        emir.Tip.Should().Be(KaynakTipi.Ekipman);
        emir.Durum.Should().Be(TransferDurumu.Bekliyor);
    }
}
