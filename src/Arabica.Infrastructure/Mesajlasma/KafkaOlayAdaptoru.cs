using System.Text.Json;
using Arabica.Contracts.Olaylar;
using Microsoft.Extensions.Options;

namespace Arabica.Infrastructure.Mesajlasma;

/// <summary>Adapts a raw Kafka (topic, value) pair into a typed ingest event.</summary>
public interface IKafkaOlayAdaptoru
{
    /// <summary>Returns a <see cref="PosOlayi"/> or <see cref="PdksOlayi"/>, or null for an unknown topic.</summary>
    object? Coz(string topic, string deger);
}

/// <summary>
/// ADAPTER (structural): converts the external Kafka wire format (JSON string per topic) into the internal
/// strongly-typed event objects the ingest consumer understands. Keeps deserialization concerns out of the
/// background service.
/// </summary>
public sealed class KafkaOlayAdaptoru(IOptions<KafkaSecenekleri> secenekler) : IKafkaOlayAdaptoru
{
    private static readonly JsonSerializerOptions Secenekler = new(JsonSerializerDefaults.Web);
    private readonly KafkaSecenekleri _ayar = secenekler.Value;

    public object? Coz(string topic, string deger)
    {
        if (topic == _ayar.PosTopic) return JsonSerializer.Deserialize<PosOlayi>(deger, Secenekler);
        if (topic == _ayar.PdksTopic) return JsonSerializer.Deserialize<PdksOlayi>(deger, Secenekler);
        return null;
    }
}
