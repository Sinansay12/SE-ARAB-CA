using Arabica.Application.Ortak;

namespace Arabica.Infrastructure.Ortak;

/// <summary>System clock. Always returns UTC so values are safe to store in <c>timestamptz</c> via Npgsql.</summary>
public sealed class SistemZamanSaglayici : IZamanSaglayici
{
    public DateTimeOffset Simdi => DateTimeOffset.UtcNow;
}
