using Arabica.Infrastructure.Esb;
using Confluent.Kafka;
using Confluent.Kafka.Admin;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Arabica.Infrastructure.Mesajlasma;

/// <summary>
/// Idempotently pre-creates all Kafka topics (raw ingest + ESB integration events) at startup, BEFORE the
/// MassTransit Kafka-rider bus and the ingest consumer subscribe. Registered ahead of AddMassTransit so it
/// starts first (hosted services start sequentially), avoiding the "Unknown topic or partition" race.
/// </summary>
public sealed class KafkaTopicOlusturucu(IOptions<KafkaSecenekleri> secenekler, ILogger<KafkaTopicOlusturucu> log)
    : IHostedService
{
    private static readonly string[] Topikler =
    [
        Topicler.Pos, Topicler.Pdks,
        KafkaEsbTopics.Onerildi, KafkaEsbTopics.Onaylandi, KafkaEsbTopics.Reddedildi, KafkaEsbTopics.Tamamlandi
    ];

    public async Task StartAsync(CancellationToken ct)
    {
        using var admin = new AdminClientBuilder(
            new AdminClientConfig { BootstrapServers = secenekler.Value.BootstrapSunuculari }).Build();

        var specler = Topikler
            .Select(t => new TopicSpecification { Name = t, NumPartitions = 1, ReplicationFactor = 1 })
            .ToList();

        for (var deneme = 1; deneme <= 5; deneme++)
        {
            try
            {
                await admin.CreateTopicsAsync(specler);
                log.LogInformation("Kafka topic'leri oluşturuldu: {Topikler}", string.Join(", ", Topikler));
                return;
            }
            catch (CreateTopicsException ex)
            {
                foreach (var r in ex.Results.Where(r => r.Error.IsError && r.Error.Code != ErrorCode.TopicAlreadyExists))
                    log.LogWarning("Topic oluşturulamadı {Topic}: {Hata}", r.Topic, r.Error.Reason);
                log.LogInformation("Kafka topic'leri hazır (mevcut olanlar atlandı).");
                return;
            }
            catch (Exception ex) when (deneme < 5)
            {
                log.LogWarning("Kafka'ya erişilemedi (deneme {Deneme}/5): {Hata}", deneme, ex.Message);
                await Task.Delay(TimeSpan.FromSeconds(2), ct);
            }
        }
    }

    public Task StopAsync(CancellationToken ct) => Task.CompletedTask;
}
