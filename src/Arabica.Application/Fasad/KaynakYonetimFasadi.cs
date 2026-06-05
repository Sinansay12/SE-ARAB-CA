using Arabica.Application.Ortak;
using Arabica.Domain.IsHukuku;
using Arabica.Domain.Optimizasyon;
using Arabica.Domain.Transferler;

namespace Arabica.Application.Fasad;

/// <summary>
/// FACADE (structural pattern). A single, simple entry point over the resource-management subsystems
/// (branch reads + seasonal Strategy resolution + İş Kanunu evaluator + optimization engine). Callers
/// (e.g. the Observer trigger handler, or the API) don't wire the engine themselves.
/// </summary>
public interface IKaynakYonetimFasadi
{
    Task<IReadOnlyList<DarbogazSonucu>> DarbogazlariDegerlendirAsync(CancellationToken ct);
}

/// <inheritdoc cref="IKaynakYonetimFasadi"/>
public sealed class KaynakYonetimFasadi(
    ISubeRepository subeRepo,
    IOptimizasyonStratejiResolver stratejiResolver,
    ITransferEmriFactory fabrika,
    IsKanunuDegerlendirici isKanunu,
    IZamanSaglayici zaman) : IKaynakYonetimFasadi
{
    public async Task<IReadOnlyList<DarbogazSonucu>> DarbogazlariDegerlendirAsync(CancellationToken ct)
    {
        var subeler = await subeRepo.AktifleriGetirAsync(ct); // pasif şubeler optimizasyona dahil edilmez
        var strateji = stratejiResolver.Sec(zaman.Simdi);          // Strategy seçimi (mevsime göre)
        var motor = new OptimizasyonMotoru(strateji, fabrika, isKanunu);
        return motor.DarbogazTespitiYap(subeler);
    }
}
