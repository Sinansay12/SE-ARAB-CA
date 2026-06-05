using Arabica.Application.Mesajlasma;
using Arabica.Contracts.Api;
using Microsoft.Extensions.Logging;

namespace Arabica.Infrastructure.RealTime;

/// <summary>
/// Placeholder <see cref="IDashboardNotifier"/> for S3 (logs). In S4 it is replaced by
/// <c>SignalRDashboardNotifier</c> (ADAPTER over <c>IHubContext</c>) for true browser push.
/// </summary>
public sealed class LogDashboardNotifier(ILogger<LogDashboardNotifier> log) : IDashboardNotifier
{
    public Task DolulukYayinlaAsync(IReadOnlyList<SubeDolulukYaniti> anlikDoluluk, CancellationToken ct)
    {
        foreach (var s in anlikDoluluk)
            log.LogInformation("Dashboard▸ {Ad}: %{Oran} ({Seviye})", s.Ad, s.DolulukOrani, s.Seviye);
        return Task.CompletedTask;
    }

    public Task TransferBildirimiYayinlaAsync(TransferBildirimGorunumu bildirim, CancellationToken ct)
    {
        log.LogInformation("Dashboard▸ transfer {Id}: {Durum} ({K}→{H})",
            bildirim.TransferId, bildirim.Durum, bildirim.KaynakSubeId, bildirim.HedefSubeId);
        return Task.CompletedTask;
    }
}
