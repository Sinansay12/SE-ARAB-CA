namespace Arabica.Infrastructure.Mesajlasma;

/// <summary>Kafka configuration, bound from <c>appsettings.json</c> ("Kafka" section) via <c>IOptions</c>.</summary>
public sealed class KafkaSecenekleri
{
    public const string Bolum = "Kafka";

    public string BootstrapSunuculari { get; set; } = "localhost:9092";
    public string TuketiciGrubu { get; set; } = "arabica-ingest";

    public string PosTopic { get; set; } = Topicler.Pos;
    public string PdksTopic { get; set; } = Topicler.Pdks;
}

/// <summary>
/// Canonical raw-Kafka topic names for the high-volume POS/PDKS ingest stream (key = SubeId for per-branch
/// ordering; retention ≥ 7 days). Transfer integration events flow over the ESB (MassTransit), NOT here.
/// </summary>
public static class Topicler
{
    public const string Pos = "arabica.pos.olaylari";
    public const string Pdks = "arabica.pdks.olaylari";
}
