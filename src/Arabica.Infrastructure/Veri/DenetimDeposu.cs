using Arabica.Application.Yonetim;
using Arabica.Contracts.Api;
using Microsoft.EntityFrameworkCore;

namespace Arabica.Infrastructure.Veri;

/// <summary>Read-only, paginated projection over hist.denetim_log (newest first). CQRS read side.</summary>
public sealed class DenetimDeposu(HistoryDbContext ctx) : IDenetimDeposu
{
    public async Task<IReadOnlyList<DenetimKaydiYaniti>> SayfaGetirAsync(int sayfa, int boyut, CancellationToken ct)
    {
        var atla = Math.Max(0, sayfa - 1) * Math.Clamp(boyut, 1, 200);
        var al = Math.Clamp(boyut, 1, 200);
        return await ctx.DenetimKayitlari.AsNoTracking()
            .OrderByDescending(d => d.Id)
            .Skip(atla).Take(al)
            .Select(d => new DenetimKaydiYaniti(d.Id, d.Aktor, d.IpAdresi, d.Eylem, d.Detay, d.Zaman))
            .ToListAsync(ct);
    }
}
