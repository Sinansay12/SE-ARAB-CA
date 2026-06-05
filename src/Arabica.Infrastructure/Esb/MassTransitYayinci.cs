using Arabica.Application.Ortak;
using MassTransit;

namespace Arabica.Infrastructure.Esb;

/// <summary>
/// ADAPTER (structural): adapts the application's <see cref="IEntegrasyonYayinci"/> port to MassTransit's
/// <see cref="IPublishEndpoint"/>. The outbox dispatcher stays bus-agnostic (and unit-testable).
/// </summary>
public sealed class MassTransitYayinci(IPublishEndpoint yayinUcu) : IEntegrasyonYayinci
{
    public Task YayinlaAsync(object olay, Type olayTipi, CancellationToken ct)
        => yayinUcu.Publish(olay, olayTipi, ct);
}
