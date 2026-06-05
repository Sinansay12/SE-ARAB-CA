using Arabica.Application.Ortak;
using Arabica.Domain.Subeler;
using MediatR;

namespace Arabica.Application.Raporlama;

/// <summary>A branch capacity/occupancy report (SRS "Rapor 1"). Immutable result of the builder.</summary>
public sealed record KapasiteRaporu(
    DateTimeOffset OlusturulmaZamani,
    int ToplamSube,
    int DarbogazSube,
    int AtilSube,
    decimal OrtalamaDoluluk,
    IReadOnlyList<KapasiteRaporSatiri> Satirlar);

public sealed record KapasiteRaporSatiri(int SubeId, string Ad, decimal DolulukOrani, string Seviye);

/// <summary>
/// BUILDER (creational pattern). Assembles a <see cref="KapasiteRaporu"/> step by step (fluent), computing
/// aggregates as branches are added. Separates the multi-step construction from the final representation.
/// </summary>
public sealed class KapasiteRaporuBuilder(DolulukEsikleri esikler)
{
    private readonly List<KapasiteRaporSatiri> _satirlar = [];
    private int _darbogaz;
    private int _atil;
    private DateTimeOffset _zaman = DateTimeOffset.UnixEpoch;

    public KapasiteRaporuBuilder ZamanDamgasi(DateTimeOffset an)
    {
        _zaman = an;
        return this;
    }

    public KapasiteRaporuBuilder SubeEkle(Sube sube)
    {
        var oran = sube.DolulukOraniHesapla();
        var seviye = sube.SeviyeHesapla(esikler);
        if (seviye == DolulukSeviyesi.Kirmizi) _darbogaz++;
        if (seviye == DolulukSeviyesi.Yesil) _atil++;
        _satirlar.Add(new KapasiteRaporSatiri(sube.SubeId, sube.Ad, oran, seviye.ToString()));
        return this;
    }

    public KapasiteRaporuBuilder SubeleriEkle(IEnumerable<Sube> subeler)
    {
        foreach (var s in subeler) SubeEkle(s);
        return this;
    }

    public KapasiteRaporu Insaa()
    {
        var ortalama = _satirlar.Count == 0 ? 0m : Math.Round(_satirlar.Average(s => s.DolulukOrani), 2);
        return new KapasiteRaporu(_zaman, _satirlar.Count, _darbogaz, _atil, ortalama, _satirlar.ToList());
    }
}

/// <summary>CQRS QUERY — produces the capacity report; genuinely uses <see cref="KapasiteRaporuBuilder"/>.</summary>
public sealed record KapasiteRaporuQuery : ISorgu<KapasiteRaporu>;

public sealed class KapasiteRaporuQueryHandler(ISubeRepository repo, DolulukEsikleri esikler, IZamanSaglayici zaman)
    : IRequestHandler<KapasiteRaporuQuery, KapasiteRaporu>
{
    public async Task<KapasiteRaporu> Handle(KapasiteRaporuQuery sorgu, CancellationToken ct)
    {
        var subeler = await repo.TumunuGetirAsync(ct);
        return new KapasiteRaporuBuilder(esikler)
            .ZamanDamgasi(zaman.Simdi)
            .SubeleriEkle(subeler)
            .Insaa();
    }
}
