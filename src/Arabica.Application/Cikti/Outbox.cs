using Arabica.Contracts.Entegrasyon;
using Arabica.Domain.Transferler;

namespace Arabica.Application.Cikti;

/// <summary>
/// A persisted outbox row carrying an ESB integration event. Plain POCO (no EF attributes) so it lives in
/// Application; EF maps it in Infrastructure. Written in the SAME transaction as the entity change — so the
/// DB commit and the bus publish cannot diverge (blueprint §7).
/// </summary>
public sealed class OutboxKaydi
{
    public Guid Id { get; set; }
    public string OlayTipi { get; set; } = string.Empty;   // integration event simple name
    public string Anahtar { get; set; } = string.Empty;    // partition/routing key (SubeId)
    public string Icerik { get; set; } = string.Empty;     // JSON payload
    public DateTimeOffset OlusmaZamani { get; set; }
    public bool YayinlandiMi { get; set; }
    public DateTimeOffset? YayinlanmaZamani { get; set; }
}

/// <summary>Enqueue port — adds an integration event to the current unit of work (no commit here).</summary>
public interface IOutbox
{
    void Ekle(object entegrasyonOlayi, string anahtar, DateTimeOffset an);
}

/// <summary>Dispatcher-side port — reads unpublished rows and marks them published.</summary>
public interface IOutboxDeposu
{
    Task<IReadOnlyList<OutboxKaydi>> YayinlanmamislariGetirAsync(int enFazla, CancellationToken ct);
    Task YayinlandiOlarakIsaretleAsync(IReadOnlyList<Guid> idler, DateTimeOffset an, CancellationToken ct);
}

/// <summary>
/// Registry mapping an integration-event simple name to its CLR type, so the dispatcher can rehydrate the
/// stored JSON and publish it strongly-typed onto the bus.
/// </summary>
public static class EntegrasyonOlayKayitlari
{
    private static readonly IReadOnlyDictionary<string, Type> Harita = new Dictionary<string, Type>
    {
        [nameof(TransferOnerildi)] = typeof(TransferOnerildi),
        [nameof(TransferOnaylandi)] = typeof(TransferOnaylandi),
        [nameof(TransferReddedildi)] = typeof(TransferReddedildi),
        [nameof(TransferTamamlandi)] = typeof(TransferTamamlandi)
    };

    public static Type Cozumle(string olayTipi)
        => Harita.TryGetValue(olayTipi, out var t)
            ? t
            : throw new InvalidOperationException($"Bilinmeyen entegrasyon olayı: '{olayTipi}'.");
}

/// <summary>Builds the right integration event from a transfer order's resulting state (KVKK: numeric IDs only).</summary>
public static class TransferOlayFabrikasi
{
    public static object Olustur(TransferEmri emir, DateTimeOffset an) => emir.Durum switch
    {
        TransferDurumu.Onaylandi => new TransferOnaylandi(emir.EmirId, emir.KaynakSubeId, emir.HedefSubeId, emir.Tip.ToString(), emir.Adet, an),
        TransferDurumu.Reddedildi => new TransferReddedildi(emir.EmirId, emir.KaynakSubeId, emir.HedefSubeId, emir.Tip.ToString(), emir.Adet, emir.RedGerekcesi ?? string.Empty, an),
        TransferDurumu.Tamamlandi => new TransferTamamlandi(emir.EmirId, emir.KaynakSubeId, emir.HedefSubeId, emir.Tip.ToString(), emir.Adet, an),
        _ => throw new InvalidOperationException($"'{emir.Durum}' durumu için entegrasyon olayı üretilmez.")
    };
}
