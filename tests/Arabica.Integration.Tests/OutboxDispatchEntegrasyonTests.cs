using Arabica.Contracts.Entegrasyon;
using Arabica.Domain.Transferler;
using Arabica.Infrastructure.Cikti;
using Arabica.Infrastructure.Veri;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Arabica.Integration.Tests;

/// <summary>
/// Outbox dispatcher against a REAL Postgres outbox (Testcontainers). Seeds an integration event into the
/// outbox, runs the dispatcher (publishing via a capturing fake bus to keep the assertion focused on the
/// DB+dispatcher path), and asserts the event was published and the row marked published. The bus→consumer
/// half is covered Docker-free by <see cref="EsbHarnessTests"/>.
/// </summary>
public sealed class OutboxDispatchEntegrasyonTests(PostgresFixture pg) : IClassFixture<PostgresFixture>
{
    private static readonly DateTimeOffset An = new(2026, 6, 5, 9, 0, 0, TimeSpan.Zero);

    [SkippableFact]
    public async Task Outbox_kaydi_ESB_ye_yayinlanir_ve_isaretlenir()
    {
        Skip.IfNot(pg.Kullanilabilir, pg.AtlamaSebebi);

        // 1) Enqueue an integration event (transfer saved, then outbox row in same context).
        await using var ctx = pg.HistoryContext();
        var emir = new TransferEmriFactory().PersonelTransferiOlustur(7, 9, 2, An);
        await ctx.TransferEmirleri.AddAsync(emir);
        await ctx.SaveChangesAsync();
        new Outbox(ctx).Ekle(new TransferOnaylandi(emir.EmirId, 7, 9, "Personel", 2, An), "7", An);
        await ctx.SaveChangesAsync();

        // 2) Dispatch (capturing fake bus).
        var yayinci = new SahteYayinci();
        await using var gonderiCtx = pg.HistoryContext();
        var gonderici = new OutboxGonderici(
            new OutboxDeposu(gonderiCtx), yayinci, new SabitZaman(), NullLogger<OutboxGonderici>.Instance);

        var adet = await gonderici.BirPartiGonderAsync(100, CancellationToken.None);

        // 3) Assert: published the typed event + row marked published.
        adet.Should().BeGreaterThanOrEqualTo(1);
        yayinci.Yayinlananlar.Should().Contain(y => y.Tip == typeof(TransferOnaylandi));
        (await pg.HistoryContext().Outbox.AsNoTracking().AnyAsync(o => !o.YayinlandiMi)).Should().BeFalse();
    }
}
