namespace Arabica.Application.Gozlem;

/// <summary>End-to-end latency summary (ms) for the ingest→processing path (NFR-P1 / M4).</summary>
public sealed record LatencyOzeti(int Adet, double OrtalamaMs, double EnYuksekMs, double P95Ms, double EsikAsanSayisi);

/// <summary>Records observed end-to-end latencies and exposes a summary. Impl is an in-memory recorder.</summary>
public interface ILatencyKaydedici
{
    void Kaydet(TimeSpan gecikme);
    LatencyOzeti Ozet();
}
