using Arabica.Application.Ortak;
using Arabica.Application.Yonetim;
using Arabica.Domain.Optimizasyon;
using Microsoft.Extensions.DependencyInjection;

namespace Arabica.Infrastructure.Optimizasyon;

/// <summary>Thread-safe holder for the runtime strategy override (admin switch).</summary>
public sealed class StratejiSecimi : IStratejiSecimi
{
    private volatile string? _secim;
    public string? GecerliSecim => _secim;
    public void Ayarla(string? sezonAnahtari) => _secim = string.IsNullOrWhiteSpace(sezonAnahtari) ? null : sezonAnahtari;
}

/// <summary>
/// Maps the current instant to the active season key. Simple month-based heuristic (placeholder — a richer
/// academic-calendar/Ramazan provider is roadmap, blueprint §9 G-takvim).
/// </summary>
public sealed class TakvimAnomaliSaglayici : ITakvimAnomaliSaglayici
{
    public string AktifSezon(DateTimeOffset an) => an.Month switch
    {
        1 or 4 or 5 or 6 => "vize-final", // vize/final yoğunluğu (yaklaşık)
        7 or 8 or 9 => "yaz",
        _ => "vize-final"
    };
}

/// <summary>
/// STRATEGY resolver — selects the seasonal <see cref="IOptimizasyonServisi"/> at runtime via .NET keyed DI.
/// Honors the admin runtime override (<see cref="IStratejiSecimi"/>) first, else the calendar default.
/// Adding a season = register a new keyed strategy + a calendar mapping; no caller changes.
/// </summary>
public sealed class OptimizasyonStratejiResolver(IServiceProvider sp, ITakvimAnomaliSaglayici takvim, IStratejiSecimi secim)
    : IOptimizasyonStratejiResolver
{
    public IOptimizasyonServisi Sec(DateTimeOffset an)
        => sp.GetRequiredKeyedService<IOptimizasyonServisi>(secim.GecerliSecim ?? takvim.AktifSezon(an));
}
