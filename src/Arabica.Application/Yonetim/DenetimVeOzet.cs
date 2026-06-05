using Arabica.Application.Gozlem;
using Arabica.Application.Ortak;
using Arabica.Contracts.Api;
using Arabica.Domain.Subeler;
using Arabica.Domain.Transferler;
using MediatR;

namespace Arabica.Application.Yonetim;

/// <summary>Read-only paginated audit log (hist.denetim_log), newest first.</summary>
public sealed record DenetimLoglariQuery(int Sayfa = 1, int Boyut = 20) : ISorgu<IReadOnlyList<DenetimKaydiYaniti>>;

public sealed class DenetimLoglariQueryHandler(IDenetimDeposu depo)
    : IRequestHandler<DenetimLoglariQuery, IReadOnlyList<DenetimKaydiYaniti>>
{
    public Task<IReadOnlyList<DenetimKaydiYaniti>> Handle(DenetimLoglariQuery q, CancellationToken ct)
        => depo.SayfaGetirAsync(q.Sayfa, q.Boyut, ct);
}

/// <summary>
/// Role-aware summary stats. Koordinatör → all active branches; Şube Müdürü → own-branch subset.
/// Read-only query; the caller passes the role + branch from the JWT claims.
/// </summary>
public sealed record OzetQuery(bool Koordinator, int? SubeId) : ISorgu<OzetYaniti>;

public sealed class OzetQueryHandler(
    ISubeRepository subeRepo, ITransferEmriRepository transferRepo, ILatencyKaydedici latency, DolulukEsikleri esikler)
    : IRequestHandler<OzetQuery, OzetYaniti>
{
    public async Task<OzetYaniti> Handle(OzetQuery q, CancellationToken ct)
    {
        var subeler = await subeRepo.AktifleriGetirAsync(ct);
        if (!q.Koordinator && q.SubeId is { } sid)
            subeler = subeler.Where(s => s.SubeId == sid).ToList();

        var atil = subeler.Count(s => s.SeviyeHesapla(esikler) == DolulukSeviyesi.Yesil);
        var darbogaz = subeler.Count(s => s.SeviyeHesapla(esikler) == DolulukSeviyesi.Kirmizi);

        var bekleyenler = await transferRepo.BekleyenleriGetirAsync(ct);
        var bekleyen = q.Koordinator
            ? bekleyenler.Count
            : bekleyenler.Count(e => e.KaynakSubeId == q.SubeId || e.HedefSubeId == q.SubeId);

        return new OzetYaniti(
            SubeSayisi: subeler.Count,
            AtilSube: atil,
            DarbogazSube: darbogaz,
            BekleyenTransfer: bekleyen,
            OrtalamaGecikmeMs: latency.Ozet().OrtalamaMs);
    }
}
