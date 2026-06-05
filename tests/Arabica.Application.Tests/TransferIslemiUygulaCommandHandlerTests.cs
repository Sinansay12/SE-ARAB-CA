using Arabica.Application.Transferler;
using Arabica.Contracts.Entegrasyon;
using Arabica.Domain.Transferler;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Arabica.Application.Tests;

/// <summary>
/// CQRS command handler routing. ONAYLA delegates to <see cref="ITransferTamamlayici"/> (atomic complete +
/// staff move, faked here); REDDET / invalid statuses use the in-handler state transition + outbox.
/// </summary>
public sealed class TransferIslemiUygulaCommandHandlerTests
{
    private static readonly DateTimeOffset An = new(2026, 6, 5, 9, 0, 0, TimeSpan.Zero);
    private static readonly ITransferEmriFactory Fabrika = new TransferEmriFactory();

    private static TransferEmri BekleyenEmir() => Fabrika.PersonelTransferiOlustur(1, 2, 1, An, emirId: 1045);

    private static (TransferIslemiUygulaCommandHandler handler, SahteOutbox outbox) Sistem(
        TransferEmri? emir, TransferTamamlamaSonucu? tamamlamaSonuc = null)
    {
        var outbox = new SahteOutbox();
        var tamamlayici = new SahteTamamlayici(tamamlamaSonuc ?? new TransferTamamlamaSonucu.Tamamlandi(1045, 1, 2, "Personel", 1, true));
        var handler = new TransferIslemiUygulaCommandHandler(
            new SahteTransferEmriDeposu(emir), outbox, new SabitZaman(An), tamamlayici,
            NullLogger<TransferIslemiUygulaCommandHandler>.Instance);
        return (handler, outbox);
    }

    [Fact]
    public async Task Onaylama_tamamlayiciya_delege_eder_ve_Tamamlandi_doner()
    {
        var (handler, outbox) = Sistem(BekleyenEmir());

        var sonuc = await handler.Handle(new TransferIslemiUygulaCommand(1045, "ONAYLANDI", null), CancellationToken.None);

        sonuc.Should().BeOfType<TransferIslemSonucu.Basarili>()
            .Which.Durum.Should().Be("Tamamlandi");           // Bekliyor → Onaylandı → Tamamlandı
        outbox.Olaylar.Should().BeEmpty();                    // outbox tamamlayici içinde (burada faked)
    }

    [Fact]
    public async Task Onaylama_yetersiz_personel_InvalidOperationException_firlatir()
    {
        var (handler, _) = Sistem(BekleyenEmir(), new TransferTamamlamaSonucu.YetersizPersonel(Gerekli: 3, Mevcut: 1));

        var eylem = async () => await handler.Handle(new TransferIslemiUygulaCommand(1045, "ONAYLANDI", null), CancellationToken.None);

        (await eylem.Should().ThrowAsync<InvalidOperationException>()).Which.Message.Should().Contain("yeterli aktif personel yok");
    }

    [Fact]
    public async Task Reddetme_TransferReddedildi_olayini_gerekceyle_yazar()
    {
        var emir = BekleyenEmir();
        var (handler, outbox) = Sistem(emir);

        await handler.Handle(new TransferIslemiUygulaCommand(1045, "REDDEDILDI", "Operasyonel uygun değil"), CancellationToken.None);

        emir.Durum.Should().Be(TransferDurumu.Reddedildi);
        var olay = outbox.Olaylar.Should().ContainSingle().Which.Should().BeOfType<TransferReddedildi>().Subject;
        olay.Gerekce.Should().Be("Operasyonel uygun değil");
    }

    [Fact]
    public async Task Gecersiz_gecis_firlatir_ve_outbox_a_yazmaz()
    {
        var emir = BekleyenEmir(); // Bekliyor → Tamamlandi (doğrudan) geçersiz
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
        var (handler, _) = Sistem(emir: null, new TransferTamamlamaSonucu.Bulunamadi(9999));

        var sonuc = await handler.Handle(new TransferIslemiUygulaCommand(9999, "ONAYLANDI", null), CancellationToken.None);

        sonuc.Should().BeOfType<TransferIslemSonucu.Bulunamadi>();
    }
}
