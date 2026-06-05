using Arabica.Application.Cikti;
using Arabica.Application.Raporlama;
using Arabica.Contracts.Entegrasyon;
using Arabica.Domain.Subeler;
using Arabica.Domain.Transferler;
using FluentAssertions;
using Xunit;

namespace Arabica.Application.Tests;

/// <summary>Factory + Builder (creational) behaviour checks.</summary>
public sealed class PatternTests
{
    private static readonly DateTimeOffset An = new(2026, 6, 5, 9, 0, 0, TimeSpan.Zero);

    [Fact]
    public void TransferOlayFabrikasi_duruma_gore_dogru_olayi_uretir()
    {
        var emir = new TransferEmriFactory().PersonelTransferiOlustur(1, 2, 2, An, emirId: 10);
        emir.DurumGuncelle("ONAYLANDI");

        TransferOlayFabrikasi.Olustur(emir, An).Should().BeOfType<TransferOnaylandi>()
            .Which.TransferId.Should().Be(10);
    }

    [Fact]
    public void TransferOlayFabrikasi_reddetmede_gerekceyi_tasir()
    {
        var emir = new TransferEmriFactory().PersonelTransferiOlustur(1, 2, 1, An, emirId: 11);
        emir.DurumGuncelle("REDDEDILDI", "yetersiz personel");

        TransferOlayFabrikasi.Olustur(emir, An).Should().BeOfType<TransferReddedildi>()
            .Which.Gerekce.Should().Be("yetersiz personel");
    }

    [Fact]
    public void KapasiteRaporuBuilder_agregalari_dogru_hesaplar()
    {
        var rapor = new KapasiteRaporuBuilder(DolulukEsikleri.Varsayilan)
            .ZamanDamgasi(An)
            .SubeEkle(new Sube(1, "Merkez", 100, anlikMusteriSayisi: 30))   // %30 → Yeşil (atıl)
            .SubeEkle(new Sube(2, "Kampüs", 100, anlikMusteriSayisi: 95))   // %95 → Kırmızı (darboğaz)
            .SubeEkle(new Sube(3, "Çarşı", 100, anlikMusteriSayisi: 70))    // %70 → Sarı
            .Insaa();

        rapor.ToplamSube.Should().Be(3);
        rapor.DarbogazSube.Should().Be(1);
        rapor.AtilSube.Should().Be(1);
        rapor.OrtalamaDoluluk.Should().Be(65.00m); // (30+95+70)/3
        rapor.Satirlar.Should().HaveCount(3);
    }
}
