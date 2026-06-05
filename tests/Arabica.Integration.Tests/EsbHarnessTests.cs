using Arabica.Application.Mesajlasma;
using Arabica.Contracts.Entegrasyon;
using Arabica.Infrastructure.Esb;
using Arabica.Infrastructure.RealTime;
using Arabica.Infrastructure.Veri;
using FluentAssertions;
using MassTransit;
using MassTransit.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Arabica.Integration.Tests;

/// <summary>
/// ESB (#8) two-consumer fan-out, Docker-free via MassTransit's in-memory test harness + EF InMemory.
/// One published <see cref="TransferOnaylandi"/> is consumed by BOTH independent consumers — the
/// notification consumer and the audit consumer (which writes a denetim_log row).
/// </summary>
public sealed class EsbHarnessTests
{
    private static readonly DateTimeOffset An = new(2026, 6, 5, 9, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task Transfer_olayi_iki_bagimsiz_consumer_tarafindan_tuketilir()
    {
        await using var provider = new ServiceCollection()
            .AddLogging()
            .AddDbContext<HistoryDbContext>(o => o.UseInMemoryDatabase("esb-harness-test"))
            .AddSingleton<IDashboardNotifier, LogDashboardNotifier>()
            .AddMassTransitTestHarness(x =>
            {
                x.AddConsumer<TransferBildirimConsumer>();
                x.AddConsumer<DenetimConsumer>();
            })
            .BuildServiceProvider(true);

        var harness = provider.GetRequiredService<ITestHarness>();
        await harness.Start();
        try
        {
            await harness.Bus.Publish(new TransferOnaylandi(1045, 1, 2, "Personel", 1, An));

            // Event reached the bus and was consumed.
            (await harness.Consumed.Any<TransferOnaylandi>()).Should().BeTrue();

            // BOTH consumers handled it (independent fan-out).
            var bildirim = harness.GetConsumerHarness<TransferBildirimConsumer>();
            var denetim = harness.GetConsumerHarness<DenetimConsumer>();
            (await bildirim.Consumed.Any<TransferOnaylandi>()).Should().BeTrue();
            (await denetim.Consumed.Any<TransferOnaylandi>()).Should().BeTrue();

            // Audit consumer persisted a denetim_log row (independent side effect).
            using var scope = provider.CreateScope();
            var ctx = scope.ServiceProvider.GetRequiredService<HistoryDbContext>();
            (await ctx.DenetimKayitlari.CountAsync(d => d.Eylem == "ESB:TransferOnaylandi")).Should().Be(1);
        }
        finally
        {
            await harness.Stop();
        }
    }
}
