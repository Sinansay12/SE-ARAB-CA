using System.Text.Json;
using Arabica.Application.Cikti;
using Arabica.Application.Ortak;
using Arabica.Application.Yonetim;
using Arabica.Domain.Subeler;
using Arabica.Domain.Transferler;
using Microsoft.EntityFrameworkCore;

namespace Arabica.Infrastructure.Veri;

/// <summary>Branch repository over the hot context.</summary>
public sealed class SubeRepository(HotDbContext ctx) : ISubeRepository
{
    public async Task<IReadOnlyList<Sube>> TumunuGetirAsync(CancellationToken ct)
        => await ctx.Subeler.AsNoTracking().OrderBy(s => s.SubeId).ToListAsync(ct);

    public async Task<IReadOnlyList<Sube>> AktifleriGetirAsync(CancellationToken ct)
        => await ctx.Subeler.AsNoTracking().Where(s => s.Aktif).OrderBy(s => s.SubeId).ToListAsync(ct);

    public async Task<Sube?> GetirAsync(int subeId, CancellationToken ct)
        => await ctx.Subeler.FirstOrDefaultAsync(s => s.SubeId == subeId, ct);

    public async Task EkleAsync(Sube sube, CancellationToken ct) => await ctx.Subeler.AddAsync(sube, ct);

    public Task<int> KaydetAsync(CancellationToken ct) => ctx.SaveChangesAsync(ct);
}

/// <summary>Anonymized personnel repository over the hot context (KVKK-clean).</summary>
public sealed class PersonelDeposu(HotDbContext ctx) : IPersonelDeposu
{
    public async Task EkleAsync(PersonelKaydi kayit, CancellationToken ct) => await ctx.Personeller.AddAsync(kayit, ct);

    public async Task<IReadOnlyList<PersonelKaydi>> SubeyeGoreGetirAsync(int subeId, CancellationToken ct)
        => await ctx.Personeller.AsNoTracking().Where(p => p.SubeId == subeId && p.Aktif).ToListAsync(ct);

    public Task<int> KaydetAsync(CancellationToken ct) => ctx.SaveChangesAsync(ct);
}

/// <summary>Transfer-order repository over the history context. Add/Get do not commit.</summary>
public sealed class TransferEmriRepository(HistoryDbContext ctx) : ITransferEmriRepository
{
    public async Task<TransferEmri?> GetirAsync(long emirId, CancellationToken ct)
        => await ctx.TransferEmirleri.FirstOrDefaultAsync(e => e.EmirId == emirId, ct);

    public async Task EkleAsync(TransferEmri emir, CancellationToken ct)
        => await ctx.TransferEmirleri.AddAsync(emir, ct);

    public async Task<IReadOnlyList<TransferEmri>> BekleyenleriGetirAsync(CancellationToken ct)
        => await ctx.TransferEmirleri.AsNoTracking()
            .Where(e => e.Durum == TransferDurumu.Bekliyor)
            .OrderBy(e => e.EmirId)
            .ToListAsync(ct);

    public async Task<IReadOnlyList<TransferEmri>> GecmisGetirAsync(int enFazla, CancellationToken ct)
        => await ctx.TransferEmirleri.AsNoTracking()
            .OrderByDescending(e => e.EmirId)
            .Take(enFazla)
            .ToListAsync(ct);
}

/// <summary>Unit of work bound to the history context (the transactional boundary for transfer + outbox).</summary>
public sealed class BirimIsi(HistoryDbContext ctx) : IBirimIsi
{
    public Task<int> KaydetAsync(CancellationToken ct) => ctx.SaveChangesAsync(ct);
}

/// <summary>
/// Enqueues an integration event as an outbox row onto the history context — committed atomically with the
/// state change by <see cref="BirimIsi"/> (single SaveChanges). Stores the event's simple type name + JSON.
/// </summary>
public sealed class Outbox(HistoryDbContext ctx) : IOutbox
{
    private static readonly JsonSerializerOptions Secenekler = new(JsonSerializerDefaults.Web);

    public void Ekle(object entegrasyonOlayi, string anahtar, DateTimeOffset an)
    {
        var tip = entegrasyonOlayi.GetType();
        ctx.Outbox.Add(new OutboxKaydi
        {
            Id = Guid.NewGuid(),
            OlayTipi = tip.Name,
            Anahtar = anahtar,
            Icerik = JsonSerializer.Serialize(entegrasyonOlayi, tip, Secenekler),
            OlusmaZamani = an,
            YayinlandiMi = false
        });
    }
}

/// <summary>Dispatcher-side store: reads unpublished rows and marks them published.</summary>
public sealed class OutboxDeposu(HistoryDbContext ctx) : IOutboxDeposu
{
    public async Task<IReadOnlyList<OutboxKaydi>> YayinlanmamislariGetirAsync(int enFazla, CancellationToken ct)
        => await ctx.Outbox
            .Where(o => !o.YayinlandiMi)
            .OrderBy(o => o.OlusmaZamani)
            .Take(enFazla)
            .ToListAsync(ct);

    public async Task YayinlandiOlarakIsaretleAsync(IReadOnlyList<Guid> idler, DateTimeOffset an, CancellationToken ct)
    {
        if (idler.Count == 0) return;
        await ctx.Outbox
            .Where(o => idler.Contains(o.Id))
            .ExecuteUpdateAsync(s => s
                .SetProperty(o => o.YayinlandiMi, true)
                .SetProperty(o => o.YayinlanmaZamani, an), ct);
    }
}
