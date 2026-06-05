using Arabica.Application.Transferler;
using Arabica.Contracts.Entegrasyon;
using Arabica.Domain.Transferler;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Arabica.Application.Tests;

/// <summary>
/// CQRS command handler (write side). The handler mutates + enqueues the integration event but does NOT
/// commit (the TransactionBehavior owns the single atomic SaveChanges). Proves: valid transition enqueues
/// exactly the right event; invalid/unknown throws and enqueues nothing.
/// </summary>
public sealed class TransferIslemiUygulaCommandHandlerTests
{
    private static readonly DateTimeOffset An = new(2026, 6, 5, 9, 0, 0, TimeSpan.Zero);
    private static readonly ITransferEmriFactory Fabrika = new TransferEmriFactory();

    private static TransferEmri BekleyenEmir() => Fabrika.PersonelTransferiOlustur(1, 2, 1, An, emirId: 1045);

    private static (TransferIslemiUygulaCommandHandler handler, SahteOutbox outbox) Sistem(TransferEmri? emir)
    {
        var outbox = new SahteOutbox();
        var handler = new TransferIslemiUygulaCommandHandler(
            new SahteTransferEmriDeposu(emir), outbox, new SabitZaman(An),
            NullLogger<TransferIslemiUygulaCommandHandler>.Instance);
        return (handler, outbox);
    }

    [Fact]
    public async Task Onaylama_TransferOnaylandi_olayini_outbox_a_yazar()
    {
        var emir = BekleyenEmir();
        var (handler, outbox) = Sistem(emir);

        var sonuc = await handler.Handle(new TransferIslemiUygulaCommand(1045, "ONAYLANDI", null), CancellationToken.None);

        sonuc.Should().BeOfType<TransferIslemSonucu.Basarili>();
        emir.Durum.Should().Be(TransferDurumu.Onaylandi);
        outbox.Olaylar.Should().ContainSingle().Which.Should().BeOfType<TransferOnaylandi>();
        emir.Olaylar.Should().BeEmpty(); // OlaylariTemizle çağrıldı
    }

    [Fact]
    public async Task Reddetme_TransferReddedildi_olayini_gerekceyle_yazar()
    {
        var emir = BekleyenEmir();
        var (handler, outbox) = Sistem(emir);

        await handler.Handle(new TransferIslemiUygulaCommand(1045, "REDDEDILDI", "Operasyonel uygun değil"), CancellationToken.None);

        var olay = outbox.Olaylar.Should().ContainSingle().Which.Should().BeOfType<TransferReddedildi>().Subject;
        olay.Gerekce.Should().Be("Operasyonel uygun değil");
    }

    [Fact]
    public async Task Gecersiz_gecis_firlatir_ve_outbox_a_yazmaz()
    {
        var emir = BekleyenEmir(); // Bekliyor → Tamamlandi geçersiz
        var (handler, outbox) = Sistem(emir);

        var eylem = async () => await handler.Handle(new TransferIslemiUygulaCommand(1045, "TAMAMLANDI", null), CancellationToken.None);

        await eylem.Should().ThrowAsync<InvalidOperationException>();
        emir.Durum.Should().Be(TransferDurumu.Bekliyor);
        outbox.Olaylar.Should().BeEmpty();
    }

    [Fact]
    public async Task Bilinmeyen_durum_firlatir_ve_outbox_a_yazmaz()
    {
        var (handler, outbox) = Sistem(BekleyenEmir());

        var eylem = async () => await handler.Handle(new TransferIslemiUygulaCommand(1045, "BILINMEYEN", null), CancellationToken.None);

        await eylem.Should().ThrowAsync<ArgumentException>();
        outbox.Olaylar.Should().BeEmpty();
    }

    [Fact]
    public async Task Bulunamayan_emir_Bulunamadi_doner()
    {
        var (handler, outbox) = Sistem(emir: null);

        var sonuc = await handler.Handle(new TransferIslemiUygulaCommand(9999, "ONAYLANDI", null), CancellationToken.None);

        sonuc.Should().BeOfType<TransferIslemSonucu.Bulunamadi>();
        outbox.Olaylar.Should().BeEmpty();
    }
}
