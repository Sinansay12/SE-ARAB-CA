using Arabica.Application.Mesajlasma;
using Arabica.Contracts.Api;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace Arabica.Api.RealTime;

/// <summary>
/// Real-time hub (gate #10). The server pushes; clients only receive. Authorized — the JS client supplies
/// the JWT via the access_token query string (wired in Program for the /hubs path).
/// Events: "DolulukGuncellendi" (occupancy snapshots) and "TransferBildirimi" (transfer notifications).
/// </summary>
[Authorize]
public sealed class DolulukHub : Hub;

/// <summary>
/// ADAPTER (structural): adapts the application's <see cref="IDashboardNotifier"/> port to SignalR's
/// <see cref="IHubContext{T}"/> for genuine browser push. Replaces the logging fallback in the running app.
/// </summary>
public sealed class SignalRDashboardNotifier(IHubContext<DolulukHub> hub) : IDashboardNotifier
{
    public Task DolulukYayinlaAsync(IReadOnlyList<SubeDolulukYaniti> anlikDoluluk, CancellationToken ct)
        => hub.Clients.All.SendAsync("DolulukGuncellendi", anlikDoluluk, ct);

    public Task TransferBildirimiYayinlaAsync(TransferBildirimGorunumu bildirim, CancellationToken ct)
        => hub.Clients.All.SendAsync("TransferBildirimi", bildirim, ct);
}
