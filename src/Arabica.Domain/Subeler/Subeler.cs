namespace Arabica.Domain.Subeler;

/// <summary>Branch occupancy level shown on the dashboard (green/yellow/red).</summary>
public enum DolulukSeviyesi
{
    Yesil,
    Sari,
    Kirmizi
}

/// <summary>
/// Occupancy-level thresholds (as occupancy %). Defaults per blueprint §9 G5 and are overridable
/// from configuration (appsettings). A value object: immutable and equality-by-value.
/// </summary>
public sealed record DolulukEsikleri(decimal YesilUstSinir = 60m, decimal SariUstSinir = 85m)
{
    public static DolulukEsikleri Varsayilan { get; } = new();

    /// <summary>Maps an occupancy percentage to a level. May exceed 100% (capacity overflow → Kırmızı).</summary>
    public DolulukSeviyesi Seviye(decimal dolulukOrani) => dolulukOrani switch
    {
        var o when o <= YesilUstSinir => DolulukSeviyesi.Yesil,
        var o when o <= SariUstSinir => DolulukSeviyesi.Sari,
        _ => DolulukSeviyesi.Kirmizi
    };
}

/// <summary>
/// An Arabica Cafe branch. Real-time fields (<see cref="AnlikMusteriSayisi"/>,
/// <see cref="AktifPersonelSayisi"/>) are fed from Kafka in a later slice via the update methods.
/// NOTE (KVKK): <see cref="Ad"/> is a branch name (e.g. "Isparta Merkez"), not personal data.
/// </summary>
public sealed class Sube
{
    public int SubeId { get; }
    public string Ad { get; private set; }
    public int MaksimumKapasite { get; private set; }
    public int AnlikMusteriSayisi { get; private set; }
    public int AktifPersonelSayisi { get; private set; }

    /// <summary>Soft-deactivation flag. Inactive branches are excluded from occupancy/optimization but kept for history.</summary>
    public bool Aktif { get; private set; } = true;

    public Sube(int subeId, string ad, int maksimumKapasite, int anlikMusteriSayisi = 0, int aktifPersonelSayisi = 0)
    {
        if (subeId <= 0) throw new ArgumentOutOfRangeException(nameof(subeId), "Şube kimliği pozitif olmalıdır.");
        if (string.IsNullOrWhiteSpace(ad)) throw new ArgumentException("Şube adı boş olamaz.", nameof(ad));
        if (maksimumKapasite <= 0) throw new ArgumentOutOfRangeException(nameof(maksimumKapasite), "Maksimum kapasite pozitif olmalıdır.");
        if (anlikMusteriSayisi < 0) throw new ArgumentOutOfRangeException(nameof(anlikMusteriSayisi));
        if (aktifPersonelSayisi < 0) throw new ArgumentOutOfRangeException(nameof(aktifPersonelSayisi));

        SubeId = subeId;
        Ad = ad;
        MaksimumKapasite = maksimumKapasite;
        AnlikMusteriSayisi = anlikMusteriSayisi;
        AktifPersonelSayisi = aktifPersonelSayisi;
    }

    /// <summary>
    /// Current occupancy as a percentage (rounded to 2 decimals). Can exceed 100% on capacity overflow,
    /// e.g. the %130 scenario in the SRS.
    /// </summary>
    public decimal DolulukOraniHesapla()
        => MaksimumKapasite <= 0 ? 0m : Math.Round((decimal)AnlikMusteriSayisi / MaksimumKapasite * 100m, 2);

    public DolulukSeviyesi SeviyeHesapla(DolulukEsikleri esikler)
        => esikler.Seviye(DolulukOraniHesapla());

    public void MusteriSayisiniGuncelle(int yeniSayi)
    {
        if (yeniSayi < 0) throw new ArgumentOutOfRangeException(nameof(yeniSayi));
        AnlikMusteriSayisi = yeniSayi;
    }

    public void AktifPersoneliGuncelle(int sayi)
    {
        if (sayi < 0) throw new ArgumentOutOfRangeException(nameof(sayi));
        AktifPersonelSayisi = sayi;
    }

    public void KapasiteyiGuncelle(int yeniKapasite)
    {
        if (yeniKapasite <= 0) throw new ArgumentOutOfRangeException(nameof(yeniKapasite));
        MaksimumKapasite = yeniKapasite;
    }

    /// <summary>Admin update of mutable branch fields (name, capacity, active-staff count).</summary>
    public void Guncelle(string ad, int maksimumKapasite, int aktifPersonelSayisi)
    {
        if (string.IsNullOrWhiteSpace(ad)) throw new ArgumentException("Şube adı boş olamaz.", nameof(ad));
        if (maksimumKapasite <= 0) throw new ArgumentOutOfRangeException(nameof(maksimumKapasite));
        if (aktifPersonelSayisi < 0) throw new ArgumentOutOfRangeException(nameof(aktifPersonelSayisi));
        Ad = ad;
        MaksimumKapasite = maksimumKapasite;
        AktifPersonelSayisi = aktifPersonelSayisi;
    }

    /// <summary>Soft-deactivate (never hard-deleted — preserves transfer-history references).</summary>
    public void Pasiflestir() => Aktif = false;

    public void Aktiflestir() => Aktif = true;

    /// <summary>True if the branch is active and has at least <paramref name="adet"/> staff to move out.</summary>
    public bool PersonelCikarabilirMi(int adet) => Aktif && AktifPersonelSayisi >= adet;

    /// <summary>Move staff OUT (transfer source). Throws if insufficient.</summary>
    public void PersonelCikar(int adet)
    {
        if (adet < 0) throw new ArgumentOutOfRangeException(nameof(adet));
        if (AktifPersonelSayisi < adet) throw new InvalidOperationException("Kaynak şubede yeterli aktif personel yok.");
        AktifPersonelSayisi -= adet;
    }

    /// <summary>Move staff IN (transfer target).</summary>
    public void PersonelEkle(int adet)
    {
        if (adet < 0) throw new ArgumentOutOfRangeException(nameof(adet));
        AktifPersonelSayisi += adet;
    }
}
