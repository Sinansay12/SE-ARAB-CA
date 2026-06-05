using Arabica.Application.Fasad;
using Arabica.Application.Mesajlasma;
using Arabica.Contracts.Api;
using Arabica.Domain.Subeler;
using MediatR;
using Microsoft.Extensions.Logging;

namespace Arabica.Application.Olaylar;

/// <summary>
/// OBSERVER (behavioural pattern), realized as a MediatR notification. Published once by the Kafka ingest
/// consumer when a branch's state changes; fanned out to MULTIPLE independent handlers below with no
/// coupling between producer and subscribers. Adding a subscriber = new handler class, no producer change.
/// </summary>
public sealed record SubeDurumuDegistiNotification(
    int SubeId,
    string Ad,
    decimal DolulukOrani,
    int MaksimumKapasite,
    int AktifPersonelSayisi,
    string Seviye) : INotification;

/// <summary>Subscriber #1 — pushes the new occupancy to the real-time dashboard (SignalR adapter in S4).</summary>
public sealed class DashboardYayinHandler(IDashboardNotifier notifier)
    : INotificationHandler<SubeDurumuDegistiNotification>
{
    public Task Handle(SubeDurumuDegistiNotification n, CancellationToken ct)
        => notifier.DolulukYayinlaAsync(
            [new SubeDolulukYaniti(n.SubeId, n.Ad, n.DolulukOrani, n.MaksimumKapasite, n.AktifPersonelSayisi, n.Seviye)],
            ct);
}

/// <summary>Subscriber #2 — triggers the optimization engine via the Facade to re-evaluate bottlenecks.</summary>
public sealed class OptimizasyonTetikleyiciHandler(IKaynakYonetimFasadi fasad, ILogger<OptimizasyonTetikleyiciHandler> log)
    : INotificationHandler<SubeDurumuDegistiNotification>
{
    public async Task Handle(SubeDurumuDegistiNotification n, CancellationToken ct)
    {
        var darbogazlar = await fasad.DarbogazlariDegerlendirAsync(ct);
        var darbogazSayisi = darbogazlar.Count(d => d.DarbogazMi);
        log.LogInformation("Şube {SubeId} değişti → optimizasyon değerlendirildi: {Darbogaz} darboğaz", n.SubeId, darbogazSayisi);
    }
}
