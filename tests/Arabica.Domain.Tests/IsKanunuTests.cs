using Arabica.Domain.IsHukuku;
using FluentAssertions;
using Xunit;

namespace Arabica.Domain.Tests;

/// <summary>İş Kanunu (4857) guard tests — NFR-L2 (daily/weekly max, due break, travel-time-as-work).</summary>
public sealed class IsKanunuTests
{
    private static readonly IsKanunuLimitleri Limitler = IsKanunuLimitleri.Varsayilan; // 11s / 45s / 5s

    private static IsKanunuBaglami Baglam(
        double bugun = 0, double hafta = 0, double sonMola = 0, double seyahat = 0) =>
        new(
            PersonelId: 7,
            BugunCalisilanSure: TimeSpan.FromHours(bugun),
            HaftaCalisilanSure: TimeSpan.FromHours(hafta),
            SonMoladanBuyanaSure: TimeSpan.FromHours(sonMola),
            SeyahatSuresi: TimeSpan.FromHours(seyahat));

    [Fact]
    public void Gunluk_azami_asilirsa_yol_dahil_reddedilir()
    {
        // 10s çalışma + 1.5s yol = 11.5s > 11s ⇒ red (yol mesaiye dahil — NFR-L2)
        var kural = new GunlukAzamiMesaiKurali();

        var sonuc = kural.Degerlendir(Baglam(bugun: 10, seyahat: 1.5), Limitler);

        sonuc.Uygun.Should().BeFalse();
        sonuc.Gerekce.Should().Contain("günlük");
    }

    [Fact]
    public void Yol_suresi_dahil_sinir_asilmiyorsa_uygundur()
    {
        // 10s + 0.5s yol = 10.5s <= 11s ⇒ uygun
        var kural = new GunlukAzamiMesaiKurali();

        var sonuc = kural.Degerlendir(Baglam(bugun: 10, seyahat: 0.5), Limitler);

        sonuc.Uygun.Should().BeTrue();
    }

    [Fact]
    public void Yol_suresinin_mesaiye_dahil_oldugu_ispatlanir()
    {
        // Yol olmasa uygun (10s), yol eklenince ihlal (10 + 2 = 12s) ⇒ farkı yalnızca yol yaratır.
        var kural = new GunlukAzamiMesaiKurali();

        kural.Degerlendir(Baglam(bugun: 10, seyahat: 0), Limitler).Uygun.Should().BeTrue();
        kural.Degerlendir(Baglam(bugun: 10, seyahat: 2), Limitler).Uygun.Should().BeFalse();
    }

    [Fact]
    public void Haftalik_azami_asilirsa_reddedilir()
    {
        var kural = new HaftalikAzamiMesaiKurali();

        var sonuc = kural.Degerlendir(Baglam(hafta: 44.5, seyahat: 1), Limitler); // 45.5 > 45

        sonuc.Uygun.Should().BeFalse();
        sonuc.Gerekce.Should().Contain("haftalık");
    }

    [Fact]
    public void Mola_hak_edilmisse_reddedilir()
    {
        var kural = new ZorunluMolaKurali();

        var sonuc = kural.Degerlendir(Baglam(sonMola: 5), Limitler); // >= 5s kesintisiz

        sonuc.Uygun.Should().BeFalse();
        sonuc.Gerekce.Should().Contain("mola");
    }

    [Fact]
    public void Tum_kurallar_gecince_degerlendirici_uygun_doner()
    {
        var degerlendirici = IsKanunuDegerlendirici.Varsayilan();

        var sonuc = degerlendirici.Degerlendir(Baglam(bugun: 4, hafta: 20, sonMola: 1, seyahat: 0.5));

        sonuc.Uygun.Should().BeTrue();
        sonuc.Gerekce.Should().BeNull();
    }

    [Fact]
    public void Degerlendirici_ilk_ihlali_gerekcesiyle_dondurur()
    {
        var degerlendirici = IsKanunuDegerlendirici.Varsayilan();

        // Hem günlük azami hem mola ihlali var; ilk kural (günlük azami) tetiklenir.
        var sonuc = degerlendirici.Degerlendir(Baglam(bugun: 10.5, sonMola: 6, seyahat: 1));

        sonuc.Uygun.Should().BeFalse();
        sonuc.Gerekce.Should().NotBeNullOrWhiteSpace();
    }
}
