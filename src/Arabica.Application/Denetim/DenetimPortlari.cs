namespace Arabica.Application.Denetim;

/// <summary>Supplies the current actor + client IP for audit entries (NFR-S7). Impl in the API (HttpContext).</summary>
public interface IDenetimBaglami
{
    string Aktor { get; }
    string IpAdresi { get; }
}

/// <summary>
/// Writes an immutable audit entry (who/IP/when/what) to <c>hist.denetim_log</c>. Commits independently of
/// any business transaction so failed attempts are still recorded. Impl in Infrastructure.
/// </summary>
public interface IDenetimYazici
{
    Task YazAsync(string eylem, string detay, CancellationToken ct);
}
