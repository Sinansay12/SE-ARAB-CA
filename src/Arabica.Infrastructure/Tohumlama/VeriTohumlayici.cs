using Arabica.Application.Kimlik;
using Arabica.Application.Ortak;
using Arabica.Domain.Subeler;
using Arabica.Domain.Transferler;
using Arabica.Infrastructure.Kimlik;
using Arabica.Infrastructure.Veri;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Arabica.Infrastructure.Tohumlama;

/// <summary>
/// Idempotent startup seeder: inserts demo users, two Isparta branches, and one pending transfer ONLY when
/// the respective tables are empty. Makes the running stack immediately usable for the demo/report and the
/// API tests. Runs after Liquibase (compose) or against the in-memory store (tests). Schema is NOT created
/// here — Liquibase owns it.
/// </summary>
public sealed class VeriTohumlayici(
    IServiceScopeFactory scopeFactory,
    IZamanSaglayici zaman,
    ILogger<VeriTohumlayici> log) : IHostedService
{
    private static readonly PasswordHasher<Kullanici> Hasher = new();

    public async Task StartAsync(CancellationToken ct)
    {
        using var scope = scopeFactory.CreateScope();
        var sp = scope.ServiceProvider;
        await KullanicilariTohumla(sp.GetRequiredService<KimlikDbContext>(), ct);
        await SubeleriVeTransferiTohumla(
            sp.GetRequiredService<HotDbContext>(),
            sp.GetRequiredService<HistoryDbContext>(),
            sp.GetRequiredService<ITransferEmriFactory>(), ct);
    }

    public Task StopAsync(CancellationToken ct) => Task.CompletedTask;

    private async Task KullanicilariTohumla(KimlikDbContext ctx, CancellationToken ct)
    {
        if (await ctx.Kullanicilar.AnyAsync(ct)) return;

        ctx.Kullanicilar.Add(YeniKullanici(DemoVeriler.KoordinatorKullanici, Roller.BolgeKoordinatoru, subeId: null));
        ctx.Kullanicilar.Add(YeniKullanici(DemoVeriler.MudurKullanici, Roller.SubeMuduru, DemoVeriler.MudurSubeId));
        await ctx.SaveChangesAsync(ct);
        log.LogInformation("Demo kullanıcılar tohumlandı: {Koordinator}, {Mudur}", DemoVeriler.KoordinatorKullanici, DemoVeriler.MudurKullanici);
    }

    private static Kullanici YeniKullanici(string ad, string rol, int? subeId)
    {
        var k = new Kullanici { KullaniciAdi = ad, Rol = rol, SubeId = subeId, MfaSecret = DemoVeriler.MfaSecret };
        k.ParolaHash = Hasher.HashPassword(k, DemoVeriler.Parola);
        return k;
    }

    private async Task SubeleriVeTransferiTohumla(HotDbContext hot, HistoryDbContext hist, ITransferEmriFactory fabrika, CancellationToken ct)
    {
        if (!await hot.Subeler.AnyAsync(ct))
        {
            hot.Subeler.Add(new Sube(1, "Isparta Merkez", maksimumKapasite: 120, anlikMusteriSayisi: 30, aktifPersonelSayisi: 5));
            hot.Subeler.Add(new Sube(2, "S.D.Ü. Kampüs", maksimumKapasite: 100, anlikMusteriSayisi: 95, aktifPersonelSayisi: 2));
            await hot.SaveChangesAsync(ct);
            log.LogInformation("Demo şubeler tohumlandı (Isparta Merkez, S.D.Ü. Kampüs).");
        }

        if (!await hist.TransferEmirleri.AnyAsync(ct))
        {
            // Merkez (atıl) → Kampüs (darboğaz) bekleyen öneri.
            var emir = fabrika.PersonelTransferiOlustur(kaynakSubeId: 1, hedefSubeId: 2, baristaAdedi: 1, zaman.Simdi);
            await hist.TransferEmirleri.AddAsync(emir, ct);
            await hist.SaveChangesAsync(ct);
            log.LogInformation("Demo bekleyen transfer önerisi tohumlandı (1 → 2).");
        }
    }
}
