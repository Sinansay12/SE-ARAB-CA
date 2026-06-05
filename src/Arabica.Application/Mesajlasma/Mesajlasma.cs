using Arabica.Contracts.Api;

namespace Arabica.Application.Mesajlasma;

/// <summary>Kafka producer port. Implemented in Infrastructure with Confluent.Kafka.</summary>
public interface IKafkaUreticisi
{
    /// <summary>Serializes <paramref name="mesaj"/> to JSON and publishes it.</summary>
    Task YayinlaAsync<T>(string topic, string anahtar, T mesaj, CancellationToken ct);

    /// <summary>Publishes an already-serialized payload as-is (used by the outbox dispatcher).</summary>
    Task YayinlaHamAsync(string topic, string anahtar, string ham, CancellationToken ct);
}

/// <summary>
/// Real-time push port (Observer subscriber + ESB notification sink). Implemented by the SignalR adapter
/// (Api). Carries live occupancy snapshots and transfer notifications to connected dashboards.
/// </summary>
public interface IDashboardNotifier
{
    Task DolulukYayinlaAsync(IReadOnlyList<SubeDolulukYaniti> anlikDoluluk, CancellationToken ct);
    Task TransferBildirimiYayinlaAsync(TransferBildirimGorunumu bildirim, CancellationToken ct);
}
