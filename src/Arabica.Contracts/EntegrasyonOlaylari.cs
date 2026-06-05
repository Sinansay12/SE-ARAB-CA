namespace Arabica.Contracts.Entegrasyon;

/// <summary>
/// ESB (MassTransit) integration events. Published from the transactional outbox and consumed by ≥2
/// independent bus consumers (notification + audit). KVKK (NFR-L1): only anonymized numeric IDs flow —
/// no name/phone/TC. These are distinct from the raw POS/PDKS Kafka ingest stream (real-time) and from
/// the SignalR browser push.
/// </summary>

/// <summary>A new transfer recommendation was created (by the engine or a manual admin order) in BEKLIYOR.</summary>
public sealed record TransferOnerildi(
    long TransferId,
    int KaynakSubeId,
    int HedefSubeId,
    string Tip,
    int Adet,
    DateTimeOffset Zaman);

/// <summary>A transfer order was approved by an authorized manager.</summary>
public sealed record TransferOnaylandi(
    long TransferId,
    int KaynakSubeId,
    int HedefSubeId,
    string Tip,
    int Adet,
    DateTimeOffset Zaman);

/// <summary>A transfer order was rejected (with a human-readable reason).</summary>
public sealed record TransferReddedildi(
    long TransferId,
    int KaynakSubeId,
    int HedefSubeId,
    string Tip,
    int Adet,
    string Gerekce,
    DateTimeOffset Zaman);

/// <summary>A transfer order was physically completed.</summary>
public sealed record TransferTamamlandi(
    long TransferId,
    int KaynakSubeId,
    int HedefSubeId,
    string Tip,
    int Adet,
    DateTimeOffset Zaman);
