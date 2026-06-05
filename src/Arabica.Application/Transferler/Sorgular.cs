using Arabica.Application.Ortak;
using Arabica.Contracts.Api;
using MediatR;

namespace Arabica.Application.Transferler;

/// <summary>
/// CQRS QUERY — pending engine-generated transfer recommendations (GET /api/v1/transfer/oneriler).
/// Reads the transfer model (Bekliyor orders).
/// </summary>
public sealed record BekleyenTransferOnerileriQuery : ISorgu<IReadOnlyList<TransferOneriYaniti>>;

public sealed class BekleyenTransferOnerileriQueryHandler(ITransferEmriRepository repo)
    : IRequestHandler<BekleyenTransferOnerileriQuery, IReadOnlyList<TransferOneriYaniti>>
{
    public async Task<IReadOnlyList<TransferOneriYaniti>> Handle(BekleyenTransferOnerileriQuery sorgu, CancellationToken ct)
    {
        var bekleyenler = await repo.BekleyenleriGetirAsync(ct);
        return bekleyenler.Select(e => new TransferOneriYaniti(
            TransferId: e.EmirId,
            KaynakSubeId: e.KaynakSubeId,
            HedefSubeId: e.HedefSubeId,
            Tip: e.Tip.ToString(),
            Adet: e.Adet,
            Durum: e.Durum.ToString(),
            TahminiVarisDakika: 0)).ToList(); // travel-time provider integrates here (roadmap §G7)
    }
}

/// <summary>CQRS QUERY — transfer history (all statuses), newest first. Bölge Koordinatörü only (RBAC at API).</summary>
public sealed record TransferGecmisiQuery(int EnFazla = 100) : ISorgu<IReadOnlyList<TransferGecmisYaniti>>;

public sealed class TransferGecmisiQueryHandler(ITransferEmriRepository repo)
    : IRequestHandler<TransferGecmisiQuery, IReadOnlyList<TransferGecmisYaniti>>
{
    public async Task<IReadOnlyList<TransferGecmisYaniti>> Handle(TransferGecmisiQuery sorgu, CancellationToken ct)
    {
        var kayitlar = await repo.GecmisGetirAsync(sorgu.EnFazla, ct);
        return kayitlar.Select(e => new TransferGecmisYaniti(
            TransferId: e.EmirId,
            KaynakSubeId: e.KaynakSubeId,
            HedefSubeId: e.HedefSubeId,
            Tip: e.Tip.ToString(),
            Adet: e.Adet,
            Durum: e.Durum.ToString(),
            RedGerekcesi: e.RedGerekcesi,
            OlusturulmaZamani: e.OlusturulmaZamani)).ToList();
    }
}

/// <summary>Source/target branches of a transfer order — used for branch-scoped RBAC on approval.</summary>
public sealed record TransferSubeBilgisi(int KaynakSubeId, int HedefSubeId);

/// <summary>CQRS QUERY — resolves a transfer's branches server-side (the request body is never trusted).</summary>
public sealed record TransferSubeleriQuery(long TransferId) : ISorgu<TransferSubeBilgisi?>;

public sealed class TransferSubeleriQueryHandler(ITransferEmriRepository repo)
    : IRequestHandler<TransferSubeleriQuery, TransferSubeBilgisi?>
{
    public async Task<TransferSubeBilgisi?> Handle(TransferSubeleriQuery sorgu, CancellationToken ct)
    {
        var emir = await repo.GetirAsync(sorgu.TransferId, ct);
        return emir is null ? null : new TransferSubeBilgisi(emir.KaynakSubeId, emir.HedefSubeId);
    }
}
