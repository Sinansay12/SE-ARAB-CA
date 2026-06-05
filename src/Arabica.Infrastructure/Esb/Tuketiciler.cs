using Arabica.Application.Mesajlasma;
using Arabica.Contracts.Api;
using Arabica.Contracts.Entegrasyon;
using Arabica.Infrastructure.Veri;
using MassTransit;
using Microsoft.Extensions.Logging;

namespace Arabica.Infrastructure.Esb;

/// <summary>
/// ESB CONSUMER #1 — notification. Subscribes to transfer integration events and pushes them to the
/// real-time channel (SignalR via <see cref="IDashboardNotifier"/>). Independent of the audit consumer.
/// </summary>
public sealed class TransferBildirimConsumer(IDashboardNotifier notifier, ILogger<TransferBildirimConsumer> log)
    : IConsumer<TransferOnerildi>, IConsumer<TransferOnaylandi>, IConsumer<TransferReddedildi>, IConsumer<TransferTamamlandi>
{
    public Task Consume(ConsumeContext<TransferOnerildi> ctx)
        => Yayinla(ctx.Message.TransferId, ctx.Message.KaynakSubeId, ctx.Message.HedefSubeId,
            ctx.Message.Tip, ctx.Message.Adet, "Bekliyor", ctx.CancellationToken);

    public Task Consume(ConsumeContext<TransferOnaylandi> ctx)
        => Yayinla(ctx.Message.TransferId, ctx.Message.KaynakSubeId, ctx.Message.HedefSubeId,
            ctx.Message.Tip, ctx.Message.Adet, "Onaylandi", ctx.CancellationToken);

    public Task Consume(ConsumeContext<TransferReddedildi> ctx)
        => Yayinla(ctx.Message.TransferId, ctx.Message.KaynakSubeId, ctx.Message.HedefSubeId,
            ctx.Message.Tip, ctx.Message.Adet, "Reddedildi", ctx.CancellationToken);

    public Task Consume(ConsumeContext<TransferTamamlandi> ctx)
        => Yayinla(ctx.Message.TransferId, ctx.Message.KaynakSubeId, ctx.Message.HedefSubeId,
            ctx.Message.Tip, ctx.Message.Adet, "Tamamlandi", ctx.CancellationToken);

    private Task Yayinla(long id, int kaynak, int hedef, string tip, int adet, string durum, CancellationToken ct)
    {
        log.LogInformation("ESB▸bildirim: transfer {Id} {Durum} ({K}→{H})", id, durum, kaynak, hedef);
        return notifier.TransferBildirimiYayinlaAsync(
            new TransferBildirimGorunumu(id, kaynak, hedef, tip, adet, durum), ct);
    }
}

/// <summary>
/// ESB CONSUMER #2 — audit. Independently persists each transfer integration event to the immutable
/// <c>hist.denetim_log</c>. A second, decoupled subscriber on the same events demonstrates a real bus.
/// </summary>
public sealed class DenetimConsumer(HistoryDbContext ctx, ILogger<DenetimConsumer> log)
    : IConsumer<TransferOnerildi>, IConsumer<TransferOnaylandi>, IConsumer<TransferReddedildi>, IConsumer<TransferTamamlandi>
{
    public Task Consume(ConsumeContext<TransferOnerildi> c)
        => DenetimYaz(c.Message.TransferId, "ESB:TransferOnerildi",
            $"{c.Message.KaynakSubeId}→{c.Message.HedefSubeId} {c.Message.Adet} {c.Message.Tip}", c.Message.Zaman, c.CancellationToken);

    public Task Consume(ConsumeContext<TransferOnaylandi> c)
        => DenetimYaz(c.Message.TransferId, "ESB:TransferOnaylandi",
            $"{c.Message.KaynakSubeId}→{c.Message.HedefSubeId} {c.Message.Adet} {c.Message.Tip}", c.Message.Zaman, c.CancellationToken);

    public Task Consume(ConsumeContext<TransferReddedildi> c)
        => DenetimYaz(c.Message.TransferId, "ESB:TransferReddedildi", c.Message.Gerekce, c.Message.Zaman, c.CancellationToken);

    public Task Consume(ConsumeContext<TransferTamamlandi> c)
        => DenetimYaz(c.Message.TransferId, "ESB:TransferTamamlandi", "tamamlandı", c.Message.Zaman, c.CancellationToken);

    private async Task DenetimYaz(long transferId, string eylem, string detay, DateTimeOffset zaman, CancellationToken ct)
    {
        ctx.DenetimKayitlari.Add(new DenetimKaydi
        {
            Aktor = "esb",
            IpAdresi = "-",
            Eylem = eylem,
            Detay = $"transfer {transferId}: {detay}",
            Zaman = zaman
        });
        await ctx.SaveChangesAsync(ct);
        log.LogInformation("ESB▸denetim: {Eylem} (transfer {Id}) loglandı", eylem, transferId);
    }
}
