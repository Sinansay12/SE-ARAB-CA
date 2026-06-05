using Arabica.Application.Fasad;
using Arabica.Application.Kurulum;
using Arabica.Application.Mesajlasma;
using Arabica.Application.Olaylar;
using Arabica.Contracts.Api;
using Arabica.Domain.Optimizasyon;
// (TransferBildirimGorunumu lives in Arabica.Contracts.Api — already imported above)
using FluentAssertions;
using MediatR;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Arabica.Application.Tests;

/// <summary>
/// OBSERVER via MediatR: publishing one <see cref="SubeDurumuDegistiNotification"/> fans out to BOTH
/// registered handlers (dashboard push + optimization trigger) with no coupling between them.
/// </summary>
public sealed class ObserverTests
{
    private sealed class SahteNotifier : IDashboardNotifier
    {
        public int Cagri { get; private set; }
        public Task DolulukYayinlaAsync(IReadOnlyList<SubeDolulukYaniti> anlik, CancellationToken ct)
        {
            Cagri++;
            return Task.CompletedTask;
        }
        public Task TransferBildirimiYayinlaAsync(TransferBildirimGorunumu bildirim, CancellationToken ct)
            => Task.CompletedTask;
    }

    private sealed class SahteFasad : IKaynakYonetimFasadi
    {
        public int Cagri { get; private set; }
        public Task<IReadOnlyList<DarbogazSonucu>> DarbogazlariDegerlendirAsync(CancellationToken ct)
        {
            Cagri++;
            return Task.FromResult<IReadOnlyList<DarbogazSonucu>>([]);
        }
    }

    [Fact]
    public async Task Bildirim_iki_aboneye_birden_dagitilir()
    {
        var notifier = new SahteNotifier();
        var fasad = new SahteFasad();

        var sp = new ServiceCollection()
            .AddSingleton(typeof(ILogger<>), typeof(NullLogger<>))
            .AddMediatR(cfg => cfg.RegisterServicesFromAssembly(typeof(ApplicationKurulum).Assembly))
            .AddSingleton<IDashboardNotifier>(notifier)
            .AddSingleton<IKaynakYonetimFasadi>(fasad)
            .BuildServiceProvider();

        var publisher = sp.GetRequiredService<IPublisher>();
        await publisher.Publish(new SubeDurumuDegistiNotification(1, "Merkez", 80m, 100, 5, "Kirmizi"));

        notifier.Cagri.Should().Be(1);  // abone #1: dashboard
        fasad.Cagri.Should().Be(1);     // abone #2: optimizasyon tetikleyici
    }
}
