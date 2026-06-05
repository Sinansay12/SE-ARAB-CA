using Arabica.Domain.Ortak;

namespace Arabica.Domain.Transferler;

/// <summary>Lifecycle status of a transfer order. State machine: see <see cref="TransferEmri"/>.</summary>
public enum TransferDurumu
{
    Bekliyor,
    Onaylandi,
    Reddedildi,
    Tamamlandi
}

/// <summary>What is being transferred: personnel (barista) or equipment (spare POS, chairs, …).</summary>
public enum KaynakTipi
{
    Personel,
    Ekipman
}

/// <summary>Raised whenever a transfer order changes status. Drained to the outbox by the app layer.</summary>
public sealed record TransferDurumuDegistiOlayi(
    long EmirId,
    TransferDurumu EskiDurum,
    TransferDurumu YeniDurum,
    string? RedGerekcesi) : DomainOlayi;

/// <summary>
/// Transfer order — the central decision-support work item (FR-9/FR-10).
/// Construction is restricted to <see cref="TransferEmriFactory"/> (internal ctor) honoring the SRS
/// rule that only the engine/factory may instantiate it. The state machine is enforced in
/// <see cref="DurumGuncelle"/>: an invalid transition throws and (by raising no event) leaves nothing
/// for the outbox to publish — so DB write and Kafka emit cannot diverge.
/// </summary>
public sealed class TransferEmri : VarlikKoku
{
    private static readonly IReadOnlyDictionary<TransferDurumu, TransferDurumu[]> IzinliGecisler =
        new Dictionary<TransferDurumu, TransferDurumu[]>
        {
            [TransferDurumu.Bekliyor] = [TransferDurumu.Onaylandi, TransferDurumu.Reddedildi],
            [TransferDurumu.Onaylandi] = [TransferDurumu.Tamamlandi],
            [TransferDurumu.Reddedildi] = [],
            [TransferDurumu.Tamamlandi] = []
        };

    public long EmirId { get; private set; }
    public int KaynakSubeId { get; }
    public int HedefSubeId { get; }
    public KaynakTipi Tip { get; }
    public int Adet { get; }
    public TransferDurumu Durum { get; private set; }
    public string? RedGerekcesi { get; private set; }
    public DateTimeOffset OlusturulmaZamani { get; }

    internal TransferEmri(long emirId, int kaynakSubeId, int hedefSubeId, KaynakTipi tip, int adet, DateTimeOffset olusturulmaZamani)
    {
        if (kaynakSubeId <= 0) throw new ArgumentOutOfRangeException(nameof(kaynakSubeId));
        if (hedefSubeId <= 0) throw new ArgumentOutOfRangeException(nameof(hedefSubeId));
        if (kaynakSubeId == hedefSubeId) throw new ArgumentException("Kaynak ve hedef şube aynı olamaz.", nameof(hedefSubeId));
        if (adet <= 0) throw new ArgumentOutOfRangeException(nameof(adet), "Transfer adedi pozitif olmalıdır.");

        EmirId = emirId;
        KaynakSubeId = kaynakSubeId;
        HedefSubeId = hedefSubeId;
        Tip = tip;
        Adet = adet;
        Durum = TransferDurumu.Bekliyor;
        OlusturulmaZamani = olusturulmaZamani;
    }

    /// <summary>
    /// FR-10 state transition. (1) validates <paramref name="yeniDurum"/> is a known status,
    /// (2) validates the transition is allowed from the current status, (3) mutates state,
    /// (4) raises a domain event for the outbox. Persistence/Kafka are performed by the application
    /// handler within a transaction — not here (idiomatic adaptation, blueprint §2/§7).
    /// </summary>
    /// <exception cref="ArgumentException">Unknown status string (e.g. "BILINMEYEN_DURUM").</exception>
    /// <exception cref="InvalidOperationException">Disallowed transition (e.g. Bekliyor → Tamamlandi).</exception>
    public void DurumGuncelle(string yeniDurum, string? gerekce = null)
    {
        if (string.IsNullOrWhiteSpace(yeniDurum))
            throw new ArgumentException("Durum değeri boş olamaz.", nameof(yeniDurum));

        var hedefAd = Enum.GetNames<TransferDurumu>()
            .FirstOrDefault(n => n.Equals(yeniDurum.Trim(), StringComparison.OrdinalIgnoreCase));
        if (hedefAd is null)
            throw new ArgumentException($"Geçersiz transfer durumu: '{yeniDurum}'.", nameof(yeniDurum));

        var hedef = Enum.Parse<TransferDurumu>(hedefAd);
        if (!IzinliGecisler[Durum].Contains(hedef))
            throw new InvalidOperationException($"Geçersiz durum geçişi: {Durum} → {hedef}.");

        if (hedef == TransferDurumu.Reddedildi && string.IsNullOrWhiteSpace(gerekce))
            throw new ArgumentException("Reddetme işlemi için gerekçe zorunludur.", nameof(gerekce));

        var eski = Durum;
        Durum = hedef;
        if (hedef == TransferDurumu.Reddedildi)
            RedGerekcesi = gerekce;

        OlayEkle(new TransferDurumuDegistiOlayi(EmirId, eski, hedef, RedGerekcesi));
    }

    /// <summary>Assigns the DB-generated identity after first persist (later slice). No-op if already set.</summary>
    public void EmirIdAta(long emirId)
    {
        if (emirId <= 0) throw new ArgumentOutOfRangeException(nameof(emirId));
        if (EmirId == 0) EmirId = emirId;
    }
}

/// <summary>Factory Method (pattern): centralizes creation of personnel vs. equipment transfer orders.</summary>
public interface ITransferEmriFactory
{
    TransferEmri PersonelTransferiOlustur(int kaynakSubeId, int hedefSubeId, int baristaAdedi, DateTimeOffset an, long emirId = 0);
    TransferEmri EkipmanTransferiOlustur(int kaynakSubeId, int hedefSubeId, int ekipmanAdedi, DateTimeOffset an, long emirId = 0);
}

/// <inheritdoc cref="ITransferEmriFactory"/>
public sealed class TransferEmriFactory : ITransferEmriFactory
{
    public TransferEmri PersonelTransferiOlustur(int kaynakSubeId, int hedefSubeId, int baristaAdedi, DateTimeOffset an, long emirId = 0)
        => new(emirId, kaynakSubeId, hedefSubeId, KaynakTipi.Personel, baristaAdedi, an);

    public TransferEmri EkipmanTransferiOlustur(int kaynakSubeId, int hedefSubeId, int ekipmanAdedi, DateTimeOffset an, long emirId = 0)
        => new(emirId, kaynakSubeId, hedefSubeId, KaynakTipi.Ekipman, ekipmanAdedi, an);
}
