using Arabica.Application.Ortak;
using Arabica.Application.Yonetim;
using Arabica.Contracts.Api;
using Arabica.Domain.Subeler;
using MediatR;

namespace Arabica.Application.Subeler;

/// <summary>CQRS QUERY — live occupancy for all branches (GET /api/v1/sube/doluluk). Reads the hot model.</summary>
public sealed record SubeDolulukQuery : ISorgu<IReadOnlyList<SubeDolulukYaniti>>;

public sealed class SubeDolulukQueryHandler(ISubeRepository repo, DolulukEsikleri esikler)
    : IRequestHandler<SubeDolulukQuery, IReadOnlyList<SubeDolulukYaniti>>
{
    public async Task<IReadOnlyList<SubeDolulukYaniti>> Handle(SubeDolulukQuery sorgu, CancellationToken ct)
    {
        var subeler = await repo.AktifleriGetirAsync(ct); // yalnız aktif şubeler (pasif olanlar hariç)
        return subeler.Select(s => new SubeDolulukYaniti(
            SubeId: s.SubeId,
            Ad: s.Ad,
            DolulukOrani: s.DolulukOraniHesapla(),
            MaksimumKapasite: s.MaksimumKapasite,
            AktifPersonelSayisi: s.AktifPersonelSayisi,
            Seviye: s.SeviyeHesapla(esikler).ToString())).ToList();
    }
}

/// <summary>CQRS QUERY — one branch's detail + staff status (GET /api/v1/sube/{subeId}/detay). Reads the hot model.</summary>
public sealed record SubeDetayQuery(int SubeId) : ISorgu<SubeDetayYaniti?>;

public sealed class SubeDetayQueryHandler(ISubeRepository repo, IPersonelDeposu personelDeposu, DolulukEsikleri esikler)
    : IRequestHandler<SubeDetayQuery, SubeDetayYaniti?>
{
    public async Task<SubeDetayYaniti?> Handle(SubeDetayQuery sorgu, CancellationToken ct)
    {
        var sube = await repo.GetirAsync(sorgu.SubeId, ct);
        if (sube is null) return null;

        var personeller = await personelDeposu.SubeyeGoreGetirAsync(sorgu.SubeId, ct);
        return new SubeDetayYaniti(
            SubeId: sube.SubeId,
            Ad: sube.Ad,
            DolulukOrani: sube.DolulukOraniHesapla(),
            Seviye: sube.SeviyeHesapla(esikler).ToString(),
            Personeller: personeller.Select(p => new PersonelDurumYaniti((int)p.PersonelId, p.TakmaAd, p.Aktif ? "Aktif" : "Pasif"))
                .ToList()); // KVKK: yalnız anonim ID + takma ad
    }
}
