using Arabica.Application.Cikti;
using Arabica.Application.Denetim;
using Arabica.Application.Kimlik;
using Arabica.Application.Mesajlasma;
using Arabica.Application.Ortak;
using Arabica.Application.Tohumlama;
using Arabica.Domain.Optimizasyon;
using Arabica.Infrastructure.Cikti;
using Arabica.Infrastructure.Denetim;
using Arabica.Application.Gozlem;
using Arabica.Application.Transferler;
using Arabica.Application.Yonetim;
using Arabica.Contracts.Entegrasyon;
using Arabica.Infrastructure.Esb;
using Arabica.Infrastructure.Transferler;
using Arabica.Infrastructure.Gozlem;
using Arabica.Infrastructure.Kimlik;
using Arabica.Infrastructure.Mesajlasma;
using Arabica.Infrastructure.Optimizasyon;
using Arabica.Infrastructure.Ortak;
using Arabica.Infrastructure.RealTime;
using Arabica.Infrastructure.Tohumlama;
using Arabica.Infrastructure.Veri;
using MassTransit;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Arabica.Infrastructure.Kurulum;

/// <summary>
/// DI composition for the infrastructure layer: EF Core (Hot/History on separate schemas, NFR-P4),
/// repositories + outbox, the seasonal Strategy (keyed DI) + resolver, the ESB (MassTransit) with its two
/// independent consumers, the Kafka adapter, and the real-time notifier. EF is ORM-only — Liquibase owns
/// the schema, so no Migrate()/EnsureCreated() here.
/// </summary>
public static class InfrastructureKurulum
{
    public static IServiceCollection ArabicaInfrastructureEkle(this IServiceCollection services, IConfiguration config)
    {
        var hotConn = config.GetConnectionString("HotDb")
                      ?? throw new InvalidOperationException("Bağlantı dizesi eksik: ConnectionStrings:HotDb");
        var histConn = config.GetConnectionString("HistoryDb")
                       ?? throw new InvalidOperationException("Bağlantı dizesi eksik: ConnectionStrings:HistoryDb");
        var kimlikConn = config.GetConnectionString("KimlikDb")
                         ?? throw new InvalidOperationException("Bağlantı dizesi eksik: ConnectionStrings:KimlikDb");

        services.AddDbContext<HotDbContext>(o => o.UseNpgsql(hotConn));
        services.AddDbContext<HistoryDbContext>(o => o.UseNpgsql(histConn));
        services.AddDbContext<KimlikDbContext>(o => o.UseNpgsql(kimlikConn));

        services.AddScoped<ISubeRepository, SubeRepository>();
        services.AddScoped<IPersonelDeposu, PersonelDeposu>();
        services.AddScoped<IDenetimDeposu, DenetimDeposu>();
        services.AddScoped<ITransferEmriRepository, TransferEmriRepository>();
        services.AddScoped<IBirimIsi, BirimIsi>();
        services.AddScoped<IOutbox, Outbox>();
        services.AddScoped<IOutboxDeposu, OutboxDeposu>();
        services.AddScoped<OutboxGonderici>();
        services.AddScoped<ITransferTamamlayici, TransferTamamlamaServisi>();

        services.AddSingleton<IZamanSaglayici, SistemZamanSaglayici>();
        services.AddSingleton<IStratejiSecimi, StratejiSecimi>();
        services.AddSingleton<IKafkaUreticisi, KafkaUreticisi>();
        services.AddSingleton<IKafkaOlayAdaptoru, KafkaOlayAdaptoru>();

        // STRATEGY (behavioural) — keyed seasonal strategies + the runtime resolver.
        services.AddKeyedSingleton<IOptimizasyonServisi, VizeFinalSezonStratejisi>("vize-final");
        services.AddKeyedSingleton<IOptimizasyonServisi, YazDonemiStratejisi>("yaz");
        services.AddSingleton<ITakvimAnomaliSaglayici, TakvimAnomaliSaglayici>();
        services.AddSingleton<IOptimizasyonStratejiResolver, OptimizasyonStratejiResolver>();

        // Real-time notifier fallback (the API overrides it with the SignalR adapter) + latency recorder.
        services.AddSingleton<IDashboardNotifier, LogDashboardNotifier>();
        services.AddSingleton<ILatencyKaydedici, LatencyKaydedici>();

        services.Configure<KafkaSecenekleri>(config.GetSection(KafkaSecenekleri.Bolum));

        // Identity / security (JWT issuance, MFA) + audit writer + startup seeder.
        services.Configure<JwtSecenekleri>(config.GetSection(JwtSecenekleri.Bolum));
        services.AddScoped<IKimlikDogrulamaServisi, KimlikDogrulamaServisi>();
        services.AddScoped<IMfaDogrulayici, MfaDogrulayici>();
        services.AddScoped<IDenetimYazici, DenetimYazici>();
        services.AddScoped<IDemoVeriTohumlayici, DemoVeriTohumlayici>();
        services.AddHostedService<VeriTohumlayici>();

        // ESB = MassTransit with TWO independent consumers (notification + audit). Transport selected by
        // config: "Kafka" (broker-backed rider — compose/production) or "Bellek" (in-memory — fast tests).
        var transport = config.GetValue("Esb:Transport", "Bellek") ?? "Bellek";
        if (transport.Equals("Kafka", StringComparison.OrdinalIgnoreCase))
            EsbKafkaRiderEkle(services, config);
        else
            EsbBellekEkle(services);

        return services;
    }

    /// <summary>In-memory ESB transport — in-process pub/sub to both consumers. Used by fast tests.</summary>
    private static void EsbBellekEkle(IServiceCollection services)
    {
        services.AddScoped<IEntegrasyonYayinci, MassTransitYayinci>();
        services.AddMassTransit(x =>
        {
            x.AddConsumer<TransferBildirimConsumer>();
            x.AddConsumer<DenetimConsumer>();
            x.UsingInMemory((ctx, cfg) => cfg.ConfigureEndpoints(ctx));
        });
    }

    /// <summary>
    /// Broker-backed ESB — MassTransit Kafka rider on the existing Kafka. One topic per integration event;
    /// each TopicEndpoint binds BOTH consumers (independent fan-out). Topics auto-created if missing.
    /// </summary>
    private static void EsbKafkaRiderEkle(IServiceCollection services, IConfiguration config)
    {
        var bootstrap = config.GetSection(KafkaSecenekleri.Bolum).Get<KafkaSecenekleri>()?.BootstrapSunuculari
                        ?? "localhost:9092";

        // Pre-create topics BEFORE the bus/consumers subscribe (registered first ⇒ starts first).
        services.AddHostedService<KafkaTopicOlusturucu>();
        services.AddScoped<IEntegrasyonYayinci, KafkaRiderYayinci>();
        services.AddMassTransit(x =>
        {
            x.UsingInMemory(); // control bus (no endpoints); the work happens on the Kafka rider
            x.AddRider(rider =>
            {
                rider.AddConsumer<TransferBildirimConsumer>();
                rider.AddConsumer<DenetimConsumer>();
                rider.AddProducer<TransferOnerildi>(KafkaEsbTopics.Onerildi);
                rider.AddProducer<TransferOnaylandi>(KafkaEsbTopics.Onaylandi);
                rider.AddProducer<TransferReddedildi>(KafkaEsbTopics.Reddedildi);
                rider.AddProducer<TransferTamamlandi>(KafkaEsbTopics.Tamamlandi);

                rider.UsingKafka((ctx, k) =>
                {
                    k.Host(bootstrap);
                    k.TopicEndpoint<TransferOnerildi>(KafkaEsbTopics.Onerildi, "arabica-esb", e =>
                    {
                        e.CreateIfMissing(t => { t.NumPartitions = 1; t.ReplicationFactor = 1; });
                        e.ConfigureConsumer<TransferBildirimConsumer>(ctx);
                        e.ConfigureConsumer<DenetimConsumer>(ctx);
                    });
                    k.TopicEndpoint<TransferOnaylandi>(KafkaEsbTopics.Onaylandi, "arabica-esb", e =>
                    {
                        e.CreateIfMissing(t => { t.NumPartitions = 1; t.ReplicationFactor = 1; });
                        e.ConfigureConsumer<TransferBildirimConsumer>(ctx);
                        e.ConfigureConsumer<DenetimConsumer>(ctx);
                    });
                    k.TopicEndpoint<TransferReddedildi>(KafkaEsbTopics.Reddedildi, "arabica-esb", e =>
                    {
                        e.CreateIfMissing(t => { t.NumPartitions = 1; t.ReplicationFactor = 1; });
                        e.ConfigureConsumer<TransferBildirimConsumer>(ctx);
                        e.ConfigureConsumer<DenetimConsumer>(ctx);
                    });
                    k.TopicEndpoint<TransferTamamlandi>(KafkaEsbTopics.Tamamlandi, "arabica-esb", e =>
                    {
                        e.CreateIfMissing(t => { t.NumPartitions = 1; t.ReplicationFactor = 1; });
                        e.ConfigureConsumer<TransferBildirimConsumer>(ctx);
                        e.ConfigureConsumer<DenetimConsumer>(ctx);
                    });
                });
            });
        });
    }

    /// <summary>
    /// Long-running background services (Kafka ingest consumer + outbox dispatcher). Separate so tests can
    /// compose the infrastructure without auto-starting hosted loops. The MassTransit bus host is started
    /// automatically by AddMassTransit.
    /// </summary>
    public static IServiceCollection ArabicaArkaPlanServisleriEkle(this IServiceCollection services)
    {
        services.AddHostedService<KafkaTuketiciServisi>();
        services.AddHostedService<OutboxDispatcherServisi>();
        return services;
    }
}
