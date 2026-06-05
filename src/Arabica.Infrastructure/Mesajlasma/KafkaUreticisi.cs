using System.Text.Json;
using Arabica.Application.Mesajlasma;
using Confluent.Kafka;
using Microsoft.Extensions.Options;

namespace Arabica.Infrastructure.Mesajlasma;

/// <summary>
/// Confluent.Kafka producer. Singleton — the underlying <see cref="IProducer{TKey,TValue}"/> is thread-safe.
/// Configured for durability + ordering: Acks=All, idempotent, zstd compression.
/// </summary>
public sealed class KafkaUreticisi : IKafkaUreticisi, IDisposable
{
    private static readonly JsonSerializerOptions Secenekler = new(JsonSerializerDefaults.Web);
    private readonly IProducer<string, string> _producer;

    public KafkaUreticisi(IOptions<KafkaSecenekleri> secenekler)
    {
        var config = new ProducerConfig
        {
            BootstrapServers = secenekler.Value.BootstrapSunuculari,
            Acks = Acks.All,
            EnableIdempotence = true,
            MessageSendMaxRetries = 5,
            CompressionType = CompressionType.Zstd,
            LingerMs = 5
        };
        _producer = new ProducerBuilder<string, string>(config).Build();
    }

    public Task YayinlaAsync<T>(string topic, string anahtar, T mesaj, CancellationToken ct)
        => YayinlaHamAsync(topic, anahtar, JsonSerializer.Serialize(mesaj, Secenekler), ct);

    public async Task YayinlaHamAsync(string topic, string anahtar, string ham, CancellationToken ct)
    {
        var mesaj = new Message<string, string> { Key = anahtar, Value = ham };
        await _producer.ProduceAsync(topic, mesaj, ct);
    }

    public void Dispose()
    {
        _producer.Flush(TimeSpan.FromSeconds(5));
        _producer.Dispose();
    }
}
