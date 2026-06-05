namespace Arabica.Contracts.Olaylar;

/// <summary>
/// Kafka event contracts. KVKK (NFR-L1): payloads carry ONLY anonymized numeric IDs and timestamps —
/// no TC Kimlik No, no real name, no phone. <c>UretimZamani</c> is stamped at the edge (ISO-8601) and
/// is the basis of the ≤2 s end-to-end latency measurement (NFR-P1).
/// </summary>

/// <summary>A POS (sales) event from a branch — topic <c>arabica.pos.olaylari</c>.</summary>
public sealed record PosOlayi(
    int SubeId,
    int SiparisAdedi,
    decimal ToplamTutar,
    DateTimeOffset UretimZamani);

/// <summary>A PDKS (staff attendance) event — topic <c>arabica.pdks.olaylari</c>. Hareket: "GIRIS" | "CIKIS".</summary>
public sealed record PdksOlayi(
    int SubeId,
    int PersonelId,
    string Hareket,
    DateTimeOffset UretimZamani);

/// <summary>A transfer state-change notification published from the outbox — topic <c>arabica.transfer.bildirimleri</c>.</summary>
public sealed record TransferBildirimOlayi(
    long TransferId,
    int KaynakSubeId,
    int HedefSubeId,
    string Tip,
    int Adet,
    string Durum,
    DateTimeOffset UretimZamani);
