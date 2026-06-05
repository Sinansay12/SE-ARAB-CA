namespace Arabica.Domain.Ortak;

/// <summary>
/// Base type for domain events. Domain events are raised by aggregates and later drained by the
/// application layer into the transactional outbox (see blueprint §7). Events carry domain data only;
/// persistence/wall-clock timestamps are stamped by the application layer that owns the clock.
/// </summary>
public abstract record DomainOlayi;

/// <summary>
/// Aggregate-root base. Buffers domain events raised during a unit of work so the application layer
/// can persist them atomically with the state change (outbox pattern). The aggregate itself performs
/// NO I/O — this is the idiomatic .NET adaptation of the Java design where the entity persisted itself.
/// </summary>
public abstract class VarlikKoku
{
    private readonly List<DomainOlayi> _olaylar = [];

    /// <summary>Events raised since the last drain. Read-only to callers.</summary>
    public IReadOnlyList<DomainOlayi> Olaylar => _olaylar;

    protected void OlayEkle(DomainOlayi olay) => _olaylar.Add(olay);

    /// <summary>Called by the application layer after the events have been copied into the outbox.</summary>
    public void OlaylariTemizle() => _olaylar.Clear();
}
