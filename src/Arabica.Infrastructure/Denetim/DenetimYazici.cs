using Arabica.Application.Denetim;
using Arabica.Application.Ortak;
using Arabica.Infrastructure.Veri;
using Microsoft.Extensions.DependencyInjection;

namespace Arabica.Infrastructure.Denetim;

/// <summary>
/// Writes an audit row (NFR-S7) to <c>hist.denetim_log</c> in its OWN scope/context, so the audit commit is
/// independent of any business transaction — failed attempts are still recorded. Actor + IP come from the
/// request-scoped <see cref="IDenetimBaglami"/>.
/// </summary>
public sealed class DenetimYazici(IServiceScopeFactory scopeFactory, IDenetimBaglami baglam, IZamanSaglayici zaman)
    : IDenetimYazici
{
    public async Task YazAsync(string eylem, string detay, CancellationToken ct)
    {
        using var scope = scopeFactory.CreateScope();
        var ctx = scope.ServiceProvider.GetRequiredService<HistoryDbContext>();
        ctx.DenetimKayitlari.Add(new DenetimKaydi
        {
            Aktor = baglam.Aktor,
            IpAdresi = baglam.IpAdresi,
            Eylem = eylem,
            Detay = detay,
            Zaman = zaman.Simdi
        });
        await ctx.SaveChangesAsync(ct);
    }
}
