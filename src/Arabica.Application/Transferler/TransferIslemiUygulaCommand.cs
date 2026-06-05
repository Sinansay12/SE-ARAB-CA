using Arabica.Application.Cikti;
using Arabica.Application.Ortak;
using FluentValidation;
using MediatR;
using Microsoft.Extensions.Logging;

namespace Arabica.Application.Transferler;

/// <summary>Outcome of the transfer state-change command.</summary>
public abstract record TransferIslemSonucu
{
    public sealed record Basarili(long TransferId, string Durum) : TransferIslemSonucu;
    public sealed record Bulunamadi(long TransferId) : TransferIslemSonucu;
    // Illegal transition / unknown status is NOT a result: the domain throws and the pipeline propagates it
    // (mapped to HTTP 409/400 in the API). The throw happens before persist+enqueue, so nothing commits.
}

/// <summary>
/// CQRS COMMAND (write side). Approve/reject/complete a transfer. <see cref="IKomut{T}"/> ⇒ the
/// TransactionBehavior commits it atomically with the outbox row.
/// </summary>
public sealed record TransferIslemiUygulaCommand(long TransferId, string YeniDurum, string? Gerekce)
    : IKomut<TransferIslemSonucu>;

public sealed class TransferIslemiUygulaCommandValidator : AbstractValidator<TransferIslemiUygulaCommand>
{
    public TransferIslemiUygulaCommandValidator()
    {
        RuleFor(x => x.TransferId).GreaterThan(0);
        RuleFor(x => x.YeniDurum).NotEmpty();
    }
}

/// <summary>
/// FR-10 write handler.
/// • ONAYLA ("ONAYLANDI") → delegates to <see cref="ITransferTamamlayici"/>, which drives Bekliyor→Onaylandı
///   →Tamamlandı AND moves staff (Personel) atomically (one transaction, no partial state). Insufficient
///   source staff → 409 with no change; terminal re-approve → 409 (state machine), no double-move.
/// • REDDET ("REDDEDILDI") / other → guarded state transition + outbox (TransactionBehavior commits atomically).
/// </summary>
public sealed class TransferIslemiUygulaCommandHandler(
    ITransferEmriRepository repo,
    IOutbox outbox,
    IZamanSaglayici zaman,
    ITransferTamamlayici tamamlayici,
    ILogger<TransferIslemiUygulaCommandHandler> log) : IRequestHandler<TransferIslemiUygulaCommand, TransferIslemSonucu>
{
    public async Task<TransferIslemSonucu> Handle(TransferIslemiUygulaCommand komut, CancellationToken ct)
    {
        if (string.Equals(komut.YeniDurum.Trim(), "ONAYLANDI", StringComparison.OrdinalIgnoreCase))
            return OnaylaSonucuMap(komut.TransferId, await tamamlayici.OnaylaAsync(komut.TransferId, ct));

        var emir = await repo.GetirAsync(komut.TransferId, ct);
        if (emir is null)
        {
            log.LogWarning("Transfer emri bulunamadı: {TransferId}", komut.TransferId);
            return new TransferIslemSonucu.Bulunamadi(komut.TransferId);
        }

        emir.DurumGuncelle(komut.YeniDurum, komut.Gerekce); // throws on invalid → nothing committed

        if (emir.Olaylar.Count > 0)
        {
            var entegrasyonOlayi = TransferOlayFabrikasi.Olustur(emir, zaman.Simdi);
            outbox.Ekle(entegrasyonOlayi, emir.KaynakSubeId.ToString(System.Globalization.CultureInfo.InvariantCulture), zaman.Simdi);
        }

        emir.OlaylariTemizle();
        return new TransferIslemSonucu.Basarili(komut.TransferId, emir.Durum.ToString());
    }

    private static TransferIslemSonucu OnaylaSonucuMap(long transferId, TransferTamamlamaSonucu sonuc) => sonuc switch
    {
        TransferTamamlamaSonucu.Tamamlandi t => new TransferIslemSonucu.Basarili(t.TransferId, "Tamamlandi"),
        TransferTamamlamaSonucu.Bulunamadi => new TransferIslemSonucu.Bulunamadi(transferId),
        TransferTamamlamaSonucu.YetersizPersonel y =>
            throw new InvalidOperationException($"Kaynak şubede yeterli aktif personel yok — gerekli: {y.Gerekli}, mevcut: {y.Mevcut}."),
        _ => throw new InvalidOperationException("Bilinmeyen tamamlama sonucu.")
    };
}
