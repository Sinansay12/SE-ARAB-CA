using System.Text.Json;
using Arabica.Application.Cikti;
using Arabica.Application.Ortak;
using Microsoft.Extensions.Logging;

namespace Arabica.Infrastructure.Cikti;

/// <summary>
/// Core outbox-publish logic (no hosting concern → unit-testable with fakes). Reads a batch of unpublished
/// rows, rehydrates each integration event from its stored type+JSON, publishes it onto the ESB via
/// <see cref="IEntegrasyonYayinci"/>, then marks the rows published. At-least-once: consumers dedupe, so a
/// crash between publish and mark cannot lose or corrupt data.
/// </summary>
public sealed class OutboxGonderici(
    IOutboxDeposu depo,
    IEntegrasyonYayinci yayinci,
    IZamanSaglayici zaman,
    ILogger<OutboxGonderici> log)
{
    private static readonly JsonSerializerOptions Secenekler = new(JsonSerializerDefaults.Web);

    public async Task<int> BirPartiGonderAsync(int enFazla, CancellationToken ct)
    {
        var kayitlar = await depo.YayinlanmamislariGetirAsync(enFazla, ct);
        if (kayitlar.Count == 0) return 0;

        var gonderilenler = new List<Guid>(kayitlar.Count);
        foreach (var kayit in kayitlar)
        {
            var tip = EntegrasyonOlayKayitlari.Cozumle(kayit.OlayTipi);
            var olay = JsonSerializer.Deserialize(kayit.Icerik, tip, Secenekler)
                       ?? throw new InvalidOperationException($"Outbox içeriği çözümlenemedi: {kayit.Id}");
            await yayinci.YayinlaAsync(olay, tip, ct);
            gonderilenler.Add(kayit.Id);
        }

        await depo.YayinlandiOlarakIsaretleAsync(gonderilenler, zaman.Simdi, ct);
        log.LogInformation("Outbox: {Adet} entegrasyon olayı ESB'ye yayınlandı.", gonderilenler.Count);
        return gonderilenler.Count;
    }
}
