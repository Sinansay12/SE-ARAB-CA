using Arabica.Infrastructure.Cikti;
using Arabica.Infrastructure.Kimlik;
using Arabica.Infrastructure.Mesajlasma;
using Arabica.Infrastructure.Veri;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Arabica.Api.Tests;

/// <summary>
/// Boots the real Api in-process with: background services OFF (no Kafka/outbox loops), the three EF
/// contexts swapped to per-instance EF InMemory stores (isolated per test), and test JWT config. The
/// startup seeder still runs, so demo users + branches + a pending transfer exist. MassTransit uses its
/// in-memory transport (no broker needed). This exercises the real controllers/auth/RBAC/MFA/pipeline.
/// </summary>
public sealed class ApiFabrika : WebApplicationFactory<Program>
{
    private readonly string _ek = Guid.NewGuid().ToString("N");

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");

        builder.ConfigureAppConfiguration((_, cfg) =>
        {
            // NOTE: only runtime IConfiguration/IOptions see these overrides; values Program reads at
            // BUILD time (e.g. Jwt:Imza in AddJwtBearer, connection strings in AddDbContext) still come from
            // appsettings.json. So we DON'T override Jwt here — the token issuer (IOptions) and the bearer
            // validator (build-time) must use the SAME key, which is the appsettings dev key. DB is swapped
            // in ConfigureServices and background services are removed, so those config values don't matter.
            cfg.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:HotDb"] = "Host=dummy",
                ["ConnectionStrings:HistoryDb"] = "Host=dummy",
                ["ConnectionStrings:KimlikDb"] = "Host=dummy",
                ["Kafka:BootstrapSunuculari"] = "dummy:9092",
                ["ArkaPlanServisleri:Etkin"] = "false"
            });
        });

        builder.ConfigureServices(services =>
        {
            BellekIciYap<HotDbContext>(services, $"hot-{_ek}");
            BellekIciYap<HistoryDbContext>(services, $"hist-{_ek}");
            BellekIciYap<KimlikDbContext>(services, $"kimlik-{_ek}");

            // No real Kafka in tests — remove the broker-bound background services (the in-memory MassTransit
            // bus and the data seeder stay). Belt-and-suspenders vs. the ArkaPlanServisleri flag.
            ArkaPlanKaldir<KafkaTuketiciServisi>(services);
            ArkaPlanKaldir<OutboxDispatcherServisi>(services);
        });
    }

    private static void ArkaPlanKaldir<T>(IServiceCollection services) where T : IHostedService
    {
        foreach (var d in services.Where(x => x.ServiceType == typeof(IHostedService) && x.ImplementationType == typeof(T)).ToList())
            services.Remove(d);
    }

    private static void BellekIciYap<T>(IServiceCollection services, string dbAdi) where T : DbContext
    {
        // Remove the Npgsql-bound options/registration for T (incl. EF8's IDbContextOptionsConfiguration<T>),
        // then re-register the context against an isolated in-memory store.
        var kaldirilacaklar = services.Where(d =>
            d.ServiceType == typeof(DbContextOptions<T>) ||
            d.ServiceType == typeof(T) ||
            (d.ServiceType.IsGenericType &&
             d.ServiceType.GetGenericTypeDefinition().Name.StartsWith("IDbContextOptionsConfiguration", StringComparison.Ordinal) &&
             d.ServiceType.GetGenericArguments()[0] == typeof(T))).ToList();

        foreach (var d in kaldirilacaklar)
            services.Remove(d);

        services.AddDbContext<T>(o => o.UseInMemoryDatabase(dbAdi));
    }
}
