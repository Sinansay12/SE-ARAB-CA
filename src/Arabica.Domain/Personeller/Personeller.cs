using Arabica.Domain.Transferler;

namespace Arabica.Domain.Personeller;

/// <summary>
/// Personnel base type.
/// KVKK (NFR-L1): this type and its subclasses carry NO personal data. Only an anonymized numeric
/// identity (<see cref="PersonelId"/>) and a non-identifying handle (<see cref="TakmaAd"/>) exist.
/// Real name/phone/TC live in a separate, access-controlled identity store (later slice) and never
/// flow through Kafka or the analytics tables.
/// Shift methods take the instant as a parameter (no ambient clock) to keep the domain deterministic
/// and unit-testable — the idiomatic .NET adaptation of the Java <c>mesaiBaslat()</c> signature.
/// </summary>
public abstract class Personel(int personelId, string takmaAd)
{
    public int PersonelId { get; } =
        personelId > 0 ? personelId : throw new ArgumentOutOfRangeException(nameof(personelId), "Personel kimliği pozitif olmalıdır.");

    public string TakmaAd { get; } =
        string.IsNullOrWhiteSpace(takmaAd) ? throw new ArgumentException("Takma ad boş olamaz.", nameof(takmaAd)) : takmaAd;

    public DateTimeOffset? MesaiBaslangici { get; private set; }
    public DateTimeOffset? MesaiBitisi { get; private set; }

    public virtual void MesaiBaslat(DateTimeOffset an)
    {
        MesaiBaslangici = an;
        MesaiBitisi = null;
    }

    public virtual void MesaiBitir(DateTimeOffset an)
    {
        if (MesaiBaslangici is null)
            throw new InvalidOperationException("Mesai başlatılmadan bitirilemez.");
        if (an < MesaiBaslangici)
            throw new ArgumentException("Mesai bitişi başlangıçtan önce olamaz.", nameof(an));
        MesaiBitisi = an;
    }
}

/// <summary>A barista — the transferable front-line staff member.</summary>
public sealed class Barista(int personelId, string takmaAd) : Personel(personelId, takmaAd);

/// <summary>
/// Branch manager. Can act on transfer orders, but only for their own branch (kaynak or hedef).
/// Authorization across HTTP is also enforced by RBAC policies (later slice); this domain guard is
/// defense-in-depth.
/// </summary>
public sealed class SubeMuduru(int personelId, string takmaAd, int sorumluSubeId) : Personel(personelId, takmaAd)
{
    public int SorumluSubeId { get; } =
        sorumluSubeId > 0 ? sorumluSubeId : throw new ArgumentOutOfRangeException(nameof(sorumluSubeId));

    public void TransferOnayla(TransferEmri emri)
    {
        IlgiliMiDogrula(emri);
        emri.DurumGuncelle("ONAYLANDI");
    }

    public void TransferReddet(TransferEmri emri, string gerekce)
    {
        IlgiliMiDogrula(emri);
        if (string.IsNullOrWhiteSpace(gerekce))
            throw new ArgumentException("Red gerekçesi zorunludur.", nameof(gerekce));
        emri.DurumGuncelle("REDDEDILDI", gerekce);
    }

    private void IlgiliMiDogrula(TransferEmri emri)
    {
        ArgumentNullException.ThrowIfNull(emri);
        if (emri.KaynakSubeId != SorumluSubeId && emri.HedefSubeId != SorumluSubeId)
            throw new UnauthorizedAccessException(
                $"Şube müdürü (şube {SorumluSubeId}) yalnızca kendi şubesini ilgilendiren transferlere müdahale edebilir.");
    }
}
