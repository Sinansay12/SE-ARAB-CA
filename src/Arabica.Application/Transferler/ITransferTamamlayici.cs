namespace Arabica.Application.Transferler;

/// <summary>
/// Atomically approves AND completes a transfer (Bekliyor → Onaylandı → Tamamlandı) and moves staff for
/// Personel transfers (kaynak −Adet, hedef +Adet) — committed in a single transaction with the state change
/// (no partial state). Implemented in Infrastructure because cross-schema (hist + hot) atomicity is a
/// persistence concern; the domain rules live in the entities (TransferEmri / Sube).
/// </summary>
public interface ITransferTamamlayici
{
    Task<TransferTamamlamaSonucu> OnaylaAsync(long transferId, CancellationToken ct);
}

public abstract record TransferTamamlamaSonucu
{
    /// <summary>Completed. <paramref name="PersonelTasindi"/> is false for Ekipman (no staff change).</summary>
    public sealed record Tamamlandi(long TransferId, int KaynakSubeId, int HedefSubeId, string Tip, int Adet, bool PersonelTasindi) : TransferTamamlamaSonucu;
    public sealed record Bulunamadi(long TransferId) : TransferTamamlamaSonucu;
    public sealed record YetersizPersonel(int Gerekli, int Mevcut) : TransferTamamlamaSonucu;
}
