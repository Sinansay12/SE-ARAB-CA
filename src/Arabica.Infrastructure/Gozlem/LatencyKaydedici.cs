using Arabica.Application.Gozlem;

namespace Arabica.Infrastructure.Gozlem;

/// <summary>
/// Thread-safe in-memory latency recorder for the ≤2 s budget (NFR-P1). Keeps a bounded rolling window of
/// samples and computes count/avg/max/p95 plus how many exceeded 2 s. Singleton.
/// </summary>
public sealed class LatencyKaydedici : ILatencyKaydedici
{
    private const int Esik = 2000; // ms (NFR-P1)
    private const int EnFazlaOrnek = 5000;
    private readonly object _kilit = new();
    private readonly List<double> _ornekler = [];

    public void Kaydet(TimeSpan gecikme)
    {
        lock (_kilit)
        {
            if (_ornekler.Count >= EnFazlaOrnek)
                _ornekler.RemoveAt(0);
            _ornekler.Add(gecikme.TotalMilliseconds);
        }
    }

    public LatencyOzeti Ozet()
    {
        lock (_kilit)
        {
            if (_ornekler.Count == 0)
                return new LatencyOzeti(0, 0, 0, 0, 0);

            var sirali = _ornekler.OrderBy(x => x).ToList();
            var p95Index = (int)Math.Ceiling(sirali.Count * 0.95) - 1;
            return new LatencyOzeti(
                Adet: sirali.Count,
                OrtalamaMs: Math.Round(sirali.Average(), 1),
                EnYuksekMs: Math.Round(sirali[^1], 1),
                P95Ms: Math.Round(sirali[Math.Clamp(p95Index, 0, sirali.Count - 1)], 1),
                EsikAsanSayisi: sirali.Count(x => x > Esik));
        }
    }
}
