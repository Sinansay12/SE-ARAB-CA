using Arabica.Domain.Optimizasyon;
using Arabica.Domain.Subeler;
using Arabica.Domain.Transferler;

namespace Arabica.Application.Ortak;

/// <summary>Clock abstraction — keeps services deterministic/testable. Infra provides the system clock.</summary>
public interface IZamanSaglayici
{
    DateTimeOffset Simdi { get; }
}

/// <summary>
/// ESB publish port. Implemented in Infrastructure over MassTransit's <c>IPublishEndpoint</c>. Keeping the
/// outbox dispatcher behind this abstraction means it stays unit-testable without a running bus.
/// </summary>
public interface IEntegrasyonYayinci
{
    Task YayinlaAsync(object olay, Type olayTipi, CancellationToken ct);
}

/// <summary>Maps the current instant to the active season key (Strategy selection input).</summary>
public interface ITakvimAnomaliSaglayici
{
    /// <summary>e.g. "vize-final" | "yaz".</summary>
    string AktifSezon(DateTimeOffset an);
}

/// <summary>Resolves the seasonal optimization Strategy at runtime (keyed DI behind this port).</summary>
public interface IOptimizasyonStratejiResolver
{
    IOptimizasyonServisi Sec(DateTimeOffset an);
}

/// <summary>
/// Transactional boundary for the transfer/outbox/audit (hist) context. A single
/// <see cref="KaydetAsync"/> commits the entity change AND the outbox row atomically (one SaveChanges),
/// which is what makes the DB write and the Kafka publish unable to diverge (blueprint §7).
/// </summary>
public interface IBirimIsi
{
    Task<int> KaydetAsync(CancellationToken ct);
}

/// <summary>Read/write access to branches (hot schema). Save is exposed for the ingest consumer + admin writes.</summary>
public interface ISubeRepository
{
    /// <summary>ALL branches incl. inactive (admin views).</summary>
    Task<IReadOnlyList<Sube>> TumunuGetirAsync(CancellationToken ct);
    /// <summary>Only active branches (occupancy/optimization paths).</summary>
    Task<IReadOnlyList<Sube>> AktifleriGetirAsync(CancellationToken ct);
    Task<Sube?> GetirAsync(int subeId, CancellationToken ct);
    Task EkleAsync(Sube sube, CancellationToken ct);
    Task<int> KaydetAsync(CancellationToken ct);
}

/// <summary>Transfer-order persistence (hist schema). Add/Get do not commit — see <see cref="IBirimIsi"/>.</summary>
public interface ITransferEmriRepository
{
    Task<TransferEmri?> GetirAsync(long emirId, CancellationToken ct);
    Task EkleAsync(TransferEmri emir, CancellationToken ct);
    Task<IReadOnlyList<TransferEmri>> BekleyenleriGetirAsync(CancellationToken ct);
    /// <summary>Recent transfer orders of ALL statuses (history), newest first.</summary>
    Task<IReadOnlyList<TransferEmri>> GecmisGetirAsync(int enFazla, CancellationToken ct);
}
