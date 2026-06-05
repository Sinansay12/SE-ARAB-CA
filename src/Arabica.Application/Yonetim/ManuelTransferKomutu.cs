using Arabica.Application.Cikti;
using Arabica.Application.Denetim;
using Arabica.Application.Ortak;
using Arabica.Contracts.Api;
using Arabica.Contracts.Entegrasyon;
using Arabica.Domain.Transferler;
using FluentValidation;
using MediatR;

namespace Arabica.Application.Yonetim;

/// <summary>
/// Manual transfer order by a coordinator (SRS §2.1). REUSES the Factory Method (<see cref="ITransferEmriFactory"/>),
/// persists to the hist schema + outbox → ESB exactly like the engine path, in BEKLIYOR (so it appears in
/// /transfer/oneriler and fires a real-time "yeni öneri" notification).
/// Plain IRequest (not IKomut): the handler manages its own unit of work because the outbox event needs the
/// DB-generated EmirId, so the order is committed first, then the integration event.
/// </summary>
public sealed record ManuelTransferOlusturCommand(int KaynakSubeId, int HedefSubeId, int Adet, string Tip)
    : IRequest<TransferOneriYaniti?>;

public sealed class ManuelTransferOlusturCommandValidator : AbstractValidator<ManuelTransferOlusturCommand>
{
    public ManuelTransferOlusturCommandValidator()
    {
        RuleFor(x => x.KaynakSubeId).GreaterThan(0);
        RuleFor(x => x.HedefSubeId).GreaterThan(0);
        RuleFor(x => x.Adet).GreaterThan(0);
        RuleFor(x => x).Must(x => x.KaynakSubeId != x.HedefSubeId).WithMessage("Kaynak ve hedef şube aynı olamaz.");
        RuleFor(x => x.Tip).Must(t => t is "Personel" or "Ekipman").WithMessage("Tip 'Personel' veya 'Ekipman' olmalıdır.");
    }
}

public sealed class ManuelTransferOlusturCommandHandler(
    ISubeRepository subeRepo,
    ITransferEmriRepository transferRepo,
    ITransferEmriFactory fabrika,
    IOutbox outbox,
    IBirimIsi birimIsi,
    IZamanSaglayici zaman,
    IDenetimYazici denetim) : IRequestHandler<ManuelTransferOlusturCommand, TransferOneriYaniti?>
{
    public async Task<TransferOneriYaniti?> Handle(ManuelTransferOlusturCommand k, CancellationToken ct)
    {
        var kaynak = await subeRepo.GetirAsync(k.KaynakSubeId, ct);
        var hedef = await subeRepo.GetirAsync(k.HedefSubeId, ct);
        if (kaynak is null || hedef is null || !kaynak.Aktif || !hedef.Aktif)
            return null; // → controller 404 ProblemDetails

        var tip = k.Tip == "Ekipman" ? KaynakTipi.Ekipman : KaynakTipi.Personel;
        var emir = tip == KaynakTipi.Ekipman
            ? fabrika.EkipmanTransferiOlustur(k.KaynakSubeId, k.HedefSubeId, k.Adet, zaman.Simdi)
            : fabrika.PersonelTransferiOlustur(k.KaynakSubeId, k.HedefSubeId, k.Adet, zaman.Simdi);

        await transferRepo.EkleAsync(emir, ct);
        await birimIsi.KaydetAsync(ct); // commit → EmirId atanır

        outbox.Ekle(new TransferOnerildi(emir.EmirId, emir.KaynakSubeId, emir.HedefSubeId, emir.Tip.ToString(), emir.Adet, zaman.Simdi),
            emir.KaynakSubeId.ToString(System.Globalization.CultureInfo.InvariantCulture), zaman.Simdi);
        await birimIsi.KaydetAsync(ct); // commit outbox → dispatcher → ESB → SignalR

        await denetim.YazAsync("ADMIN:ManuelTransfer", $"{k.KaynakSubeId}→{k.HedefSubeId} {k.Adet} {k.Tip} (emir {emir.EmirId})", ct);
        return new TransferOneriYaniti(emir.EmirId, emir.KaynakSubeId, emir.HedefSubeId, emir.Tip.ToString(), emir.Adet, emir.Durum.ToString(), TahminiVarisDakika: 0);
    }
}
