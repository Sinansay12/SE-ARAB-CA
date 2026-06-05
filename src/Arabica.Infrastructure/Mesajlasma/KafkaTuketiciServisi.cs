using Arabica.Application.Gozlem;
using Arabica.Application.Olaylar;
using Arabica.Application.Ortak;
using Arabica.Contracts.Olaylar;
using Arabica.Domain.Subeler;
using Confluent.Kafka;
using MediatR;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Arabica.Infrastructure.Mesajlasma;

/// <summary>
/// Background consumer of the high-volume POS/PDKS ingest stream (consumer group <c>arabica-ingest</c>).
/// Manual offset commit AFTER successful processing ⇒ at-least-once + lossless, ordered replay. Each
/// message: decode (via <see cref="IKafkaOlayAdaptoru"/>) → update branch state → publish the
/// <see cref="SubeDurumuDegistiNotification"/> (OBSERVER source: MediatR fans it out to the dashboard and
/// the optimization trigger). This is the real-time stream — distinct from the MassTransit ESB.
/// </summary>
public sealed class KafkaTuketiciServisi(
    IServiceScopeFactory scopeFactory,
    IOptions<KafkaSecenekleri> secenekler,
    ILogger<KafkaTuketiciServisi> log) : BackgroundService
{
    private readonly KafkaSecenekleri _ayar = secenekler.Value;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // Yield so StartAsync returns immediately — otherwise the synchronous, blocking consumer.Consume()
        // below would run inside StartAsync and stall host startup until the first message/timeout.
        await Task.Yield();

        var config = new ConsumerConfig
        {
            BootstrapServers = _ayar.BootstrapSunuculari,
            GroupId = _ayar.TuketiciGrubu,
            AutoOffsetReset = AutoOffsetReset.Earliest,
            EnableAutoCommit = false
        };

        using var consumer = new ConsumerBuilder<string, string>(config).Build();
        consumer.Subscribe([_ayar.PosTopic, _ayar.PdksTopic]);
        log.LogInformation("Kafka tüketicisi başladı: {Topicler}", string.Join(", ", _ayar.PosTopic, _ayar.PdksTopic));

        try
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                ConsumeResult<string, string>? sonuc;
                try
                {
                    sonuc = consumer.Consume(stoppingToken);
                }
                catch (ConsumeException ex)
                {
                    log.LogError(ex, "Kafka tüketim hatası");
                    continue;
                }

                if (sonuc?.Message is null) continue;

                try
                {
                    await MesajiIsleAsync(sonuc.Topic, sonuc.Message.Value, stoppingToken);
                    consumer.Commit(sonuc); // yalnızca başarılı işleme sonrası offset ilerlet
                }
                catch (OperationCanceledException)
                {
                    throw;
                }
                catch (Exception ex)
                {
                    log.LogError(ex, "Mesaj işlenemedi (offset commit edilmedi): {Topic}", sonuc.Topic);
                }
            }
        }
        catch (OperationCanceledException)
        {
            // graceful shutdown
        }
        finally
        {
            consumer.Close();
        }
    }

    private async Task MesajiIsleAsync(string topic, string deger, CancellationToken ct)
    {
        using var scope = scopeFactory.CreateScope();
        var sp = scope.ServiceProvider;
        var adaptor = sp.GetRequiredService<IKafkaOlayAdaptoru>();
        var repo = sp.GetRequiredService<ISubeRepository>();

        var olay = adaptor.Coz(topic, deger);
        Sube? sube = olay switch
        {
            PosOlayi pos => await UygulaAsync(repo, pos.SubeId, s => SubeGuncelleyici.PosUygula(s, pos), ct),
            PdksOlayi pdks => await UygulaAsync(repo, pdks.SubeId, s => SubeGuncelleyici.PdksUygula(s, pdks), ct),
            _ => null
        };

        if (sube is null) return;

        // NFR-P1 (≤2 s): record end-to-end latency from the edge production timestamp to processing.
        var uretimZamani = olay switch { PosOlayi p => p.UretimZamani, PdksOlayi p => p.UretimZamani, _ => (DateTimeOffset?)null };
        if (uretimZamani is { } uz)
        {
            var simdi = sp.GetRequiredService<IZamanSaglayici>().Simdi;
            sp.GetRequiredService<ILatencyKaydedici>().Kaydet(simdi - uz);
        }

        // OBSERVER: tek kaynak → MediatR ile birden çok aboneye dağıt (dashboard + optimizasyon tetikleyici).
        var esikler = sp.GetRequiredService<DolulukEsikleri>();
        var publisher = sp.GetRequiredService<IPublisher>();
        await publisher.Publish(new SubeDurumuDegistiNotification(
            sube.SubeId, sube.Ad, sube.DolulukOraniHesapla(), sube.MaksimumKapasite,
            sube.AktifPersonelSayisi, sube.SeviyeHesapla(esikler).ToString()), ct);
    }

    private static async Task<Sube?> UygulaAsync(ISubeRepository repo, int subeId, Action<Sube> uygula, CancellationToken ct)
    {
        var sube = await repo.GetirAsync(subeId, ct);
        if (sube is null) return null;
        uygula(sube);
        await repo.KaydetAsync(ct);
        return sube;
    }
}
