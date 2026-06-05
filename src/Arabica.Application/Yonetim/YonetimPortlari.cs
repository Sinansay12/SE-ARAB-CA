using Arabica.Contracts.Api;

namespace Arabica.Application.Yonetim;

/// <summary>
/// Anonymized personnel record (KVKK): a non-identifying handle + numeric IDs only. Plain POCO (no EF
/// attributes) so it lives in Application; EF maps it in Infrastructure (hot.personel).
/// </summary>
public sealed class PersonelKaydi
{
    public long PersonelId { get; set; }
    public int SubeId { get; set; }
    public string TakmaAd { get; set; } = string.Empty;
    public string Tip { get; set; } = string.Empty;
    public bool Aktif { get; set; } = true;
}

/// <summary>Personnel persistence (hot schema). Add does not commit — caller saves via the repository.</summary>
public interface IPersonelDeposu
{
    Task EkleAsync(PersonelKaydi kayit, CancellationToken ct);
    Task<IReadOnlyList<PersonelKaydi>> SubeyeGoreGetirAsync(int subeId, CancellationToken ct);
    Task<int> KaydetAsync(CancellationToken ct);
}

/// <summary>Read-only paginated access to the audit log (hist.denetim_log), newest first.</summary>
public interface IDenetimDeposu
{
    Task<IReadOnlyList<DenetimKaydiYaniti>> SayfaGetirAsync(int sayfa, int boyut, CancellationToken ct);
}

/// <summary>
/// Holds the runtime optimization-strategy override (admin can switch Vize-Final ↔ Yaz at runtime). When no
/// override is set, the calendar-based default applies. Singleton, thread-safe.
/// </summary>
public interface IStratejiSecimi
{
    string? GecerliSecim { get; }
    void Ayarla(string? sezonAnahtari);
}
