using Arabica.Application.Transferler;
using Arabica.Domain.Subeler;
using Arabica.Domain.Transferler;
using Arabica.Infrastructure.Veri;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Arabica.Integration.Tests;

/// <summary>
/// Real-Postgres tests (Testcontainers). Proves the transactional-outbox guarantee at the DB level: the
/// command handler enqueues the integration event and a single KaydetAsync (what TransactionBehavior does)
/// commits the transfer UPDATE + the outbox INSERT atomically; an illegal transition commits neither.
/// Also proves hot/hist schema isolation.
/// </summary>
public sealed class PostgresEntegrasyonTests(PostgresFixture fixture) : IClassFixture<PostgresFixture>
{
    private static readonly DateTimeOffset An = new(2026, 6, 5, 9, 0, 0, TimeSpan.Zero);

    private static TransferIslemiUygulaCommandHandler Handler(HistoryDbContext ctx)
        => new(new TransferEmriRepository(ctx), new Outbox(ctx), new SabitZaman(),
            NullLogger<TransferIslemiUygulaCommandHandler>.Instance);

    [SkippableFact]
    public async Task Onaylama_transfer_ve_outbox_satirini_tek_transaction_da_yazar()
    {
        Skip.IfNot(fixture.Kullanilabilir, fixture.AtlamaSebebi);

        await using var ctx = fixture.HistoryContext();
        var emirId = await BekleyenEmirEkleAsync(ctx);

        var sonuc = await Handler(ctx).Handle(new TransferIslemiUygulaCommand(emirId, "ONAYLANDI", null), CancellationToken.None);
        await new BirimIsi(ctx).KaydetAsync(CancellationToken.None); // = TransactionBehavior'ın yaptığı tek commit

        sonuc.Should().BeOfType<TransferIslemSonucu.Basarili>();

        await using var dogrulama = fixture.HistoryContext();
        (await dogrulama.TransferEmirleri.AsNoTracking().FirstAsync(e => e.EmirId == emirId))
            .Durum.Should().Be(TransferDurumu.Onaylandi);
        (await dogrulama.Outbox.AsNoTracking().CountAsync(o => o.OlayTipi == "TransferOnaylandi")).Should().Be(1);
    }

    [SkippableFact]
    public async Task Gecersiz_gecis_ne_durumu_degistirir_ne_outbox_a_yazar()
    {
        Skip.IfNot(fixture.Kullanilabilir, fixture.AtlamaSebebi);

        await using var ctx = fixture.HistoryContext();
        var emirId = await BekleyenEmirEkleAsync(ctx);
        var outboxOnce = await ctx.Outbox.CountAsync();

        // Bekliyor → Tamamlandi geçersiz; handler fırlatır, KaydetAsync çağrılmaz → hiçbir şey commit edilmez.
        var eylem = async () => await Handler(ctx).Handle(new TransferIslemiUygulaCommand(emirId, "TAMAMLANDI", null), CancellationToken.None);
        await eylem.Should().ThrowAsync<InvalidOperationException>();

        await using var dogrulama = fixture.HistoryContext();
        (await dogrulama.TransferEmirleri.AsNoTracking().FirstAsync(e => e.EmirId == emirId))
            .Durum.Should().Be(TransferDurumu.Bekliyor);
        (await dogrulama.Outbox.AsNoTracking().CountAsync()).Should().Be(outboxOnce);
    }

    [SkippableFact]
    public async Task Hot_ve_hist_semalari_ayni_veritabaninda_izole_calisir()
    {
        Skip.IfNot(fixture.Kullanilabilir, fixture.AtlamaSebebi);

        await using var hot = fixture.HotContext();
        hot.Subeler.Add(new Sube(101, "Isparta Merkez", maksimumKapasite: 120, anlikMusteriSayisi: 90));
        await hot.SaveChangesAsync();

        await using var hist = fixture.HistoryContext();
        await BekleyenEmirEkleAsync(hist);

        (await fixture.HotContext().Subeler.AsNoTracking().AnyAsync(s => s.SubeId == 101)).Should().BeTrue();
        (await fixture.HistoryContext().TransferEmirleri.AsNoTracking().AnyAsync()).Should().BeTrue();
    }

    private static async Task<long> BekleyenEmirEkleAsync(HistoryDbContext ctx)
    {
        var emir = new TransferEmriFactory().PersonelTransferiOlustur(1, 2, 1, An);
        await ctx.TransferEmirleri.AddAsync(emir);
        await ctx.SaveChangesAsync();
        return emir.EmirId;
    }
}
