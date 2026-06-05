using Arabica.Application.Ortak;
using MassTransit;
using Microsoft.Extensions.DependencyInjection;

namespace Arabica.Infrastructure.Esb;

/// <summary>ESB topic names for the broker-backed (Kafka rider) transport.</summary>
public static class KafkaEsbTopics
{
    public const string Onerildi = "arabica.transfer.onerildi";
    public const string Onaylandi = "arabica.transfer.onaylandi";
    public const string Reddedildi = "arabica.transfer.reddedildi";
    public const string Tamamlandi = "arabica.transfer.tamamlandi";
}

/// <summary>
/// ESB publish adapter for the MassTransit Kafka rider. Resolves the strongly-typed
/// <c>ITopicProducer&lt;T&gt;</c> for the event's runtime type and produces it to its Kafka topic.
/// (In-memory transport uses <see cref="MassTransitYayinci"/> instead — see InfrastructureKurulum.)
/// </summary>
public sealed class KafkaRiderYayinci(IServiceProvider sp) : IEntegrasyonYayinci
{
    public async Task YayinlaAsync(object olay, Type olayTipi, CancellationToken ct)
    {
        var producerType = typeof(ITopicProducer<>).MakeGenericType(olayTipi);
        dynamic producer = sp.GetRequiredService(producerType);
        await producer.Produce((dynamic)olay, ct);
    }
}
