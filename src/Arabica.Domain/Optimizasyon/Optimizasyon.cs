using Arabica.Domain.IsHukuku;
using Arabica.Domain.Subeler;
using Arabica.Domain.Transferler;

namespace Arabica.Domain.Optimizasyon;

/// <summary>Outcome of evaluating one branch for bottleneck/idle status under a seasonal strategy.</summary>
public sealed record DarbogazSonucu(Sube Sube, decimal DolulukOrani, DolulukSeviyesi Seviye, bool DarbogazMi, bool AtilMi);

/// <summary>A raw recommendation produced by a seasonal strategy (before İş Kanunu guards / persistence).</summary>
public sealed record TransferOnerisi(int KaynakSubeId, int HedefSubeId, int OnerilenAdet, KaynakTipi Tip, string Gerekce);

/// <summary>Per-candidate worked-time context (sourced from PDKS in a later slice).</summary>
public sealed record BaristaMesaiBaglami(
    int PersonelId,
    TimeSpan BugunCalisilanSure,
    TimeSpan HaftaCalisilanSure,
    TimeSpan SonMoladanBuyanaSure);

/// <summary>Result of the engine's recommendation step: a created order, or a rejection with a reason.</summary>
public abstract record OneriSonucu
{
    public sealed record Basarili(TransferEmri Emri) : OneriSonucu;
    public sealed record Reddedildi(string Gerekce) : OneriSonucu;
}

/// <summary>
/// Strategy contract: each concrete strategy embeds its own seasonal tolerance (thresholds), which is
/// the whole point of the pattern — vize/final tolerates less crowding than summer.
/// </summary>
public interface IOptimizasyonServisi
{
    string SezonAnahtari { get; }
    DarbogazSonucu DarbogazHesapla(Sube sube);
    TransferOnerisi TransferOnerisiUret(Sube kaynak, Sube hedef);
}

/// <summary>
/// Exam-season strategy: low crowding tolerance (students fill branches, low wait tolerance) ⇒
/// declares a bottleneck earlier and recommends more aggressively.
/// </summary>
public sealed class VizeFinalSezonStratejisi : IOptimizasyonServisi
{
    private static readonly DolulukEsikleri Esikler = new(YesilUstSinir: 50m, SariUstSinir: 75m);

    public string SezonAnahtari => "vize-final";

    public DarbogazSonucu DarbogazHesapla(Sube sube)
    {
        var oran = sube.DolulukOraniHesapla();
        var seviye = Esikler.Seviye(oran);
        return new DarbogazSonucu(sube, oran, seviye,
            DarbogazMi: seviye == DolulukSeviyesi.Kirmizi,
            AtilMi: seviye == DolulukSeviyesi.Yesil);
    }

    public TransferOnerisi TransferOnerisiUret(Sube kaynak, Sube hedef)
    {
        var hedefOran = hedef.DolulukOraniHesapla();
        var acik = (int)Math.Ceiling(Math.Max(0m, hedefOran - Esikler.SariUstSinir) / 25m);
        var adet = Math.Clamp(Math.Max(acik, 1), 1, Math.Max(1, kaynak.AktifPersonelSayisi - 1));
        return new TransferOnerisi(kaynak.SubeId, hedef.SubeId, adet, KaynakTipi.Personel,
            $"Vize/final sezonu: {hedef.Ad} şubesi %{hedefOran:0.#} doluluk ile darboğazda; " +
            $"{kaynak.Ad} şubesinden {adet} barista önerildi.");
    }
}

/// <summary>
/// Summer strategy: higher crowding tolerance (campus quieter) ⇒ declares bottlenecks later and
/// recommends more conservatively.
/// </summary>
public sealed class YazDonemiStratejisi : IOptimizasyonServisi
{
    private static readonly DolulukEsikleri Esikler = new(YesilUstSinir: 70m, SariUstSinir: 95m);

    public string SezonAnahtari => "yaz";

    public DarbogazSonucu DarbogazHesapla(Sube sube)
    {
        var oran = sube.DolulukOraniHesapla();
        var seviye = Esikler.Seviye(oran);
        return new DarbogazSonucu(sube, oran, seviye,
            DarbogazMi: seviye == DolulukSeviyesi.Kirmizi,
            AtilMi: seviye == DolulukSeviyesi.Yesil);
    }

    public TransferOnerisi TransferOnerisiUret(Sube kaynak, Sube hedef)
    {
        var hedefOran = hedef.DolulukOraniHesapla();
        var acik = (int)Math.Ceiling(Math.Max(0m, hedefOran - Esikler.SariUstSinir) / 40m);
        var adet = Math.Clamp(Math.Max(acik, 1), 1, Math.Max(1, kaynak.AktifPersonelSayisi - 1));
        return new TransferOnerisi(kaynak.SubeId, hedef.SubeId, adet, KaynakTipi.Personel,
            $"Yaz dönemi: {hedef.Ad} şubesi %{hedefOran:0.#} doluluk ile yoğun; " +
            $"{kaynak.Ad} şubesinden {adet} barista önerildi.");
    }
}

/// <summary>
/// The optimization engine (domain service). Pure: no I/O. Receives the already-resolved seasonal
/// strategy (resolution by calendar happens in the application layer), the transfer factory, and the
/// İş Kanunu evaluator. Honors the SRS surface: DarbogazTespitiYap / DarbogazHesapla / TransferOnerisiUret.
/// </summary>
public sealed class OptimizasyonMotoru(
    IOptimizasyonServisi strateji,
    ITransferEmriFactory fabrika,
    IsKanunuDegerlendirici isKanunuDegerlendirici)
{
    /// <summary>Evaluates every branch and returns its bottleneck/idle classification.</summary>
    public IReadOnlyList<DarbogazSonucu> DarbogazTespitiYap(IEnumerable<Sube> subeler)
        => subeler.Select(strateji.DarbogazHesapla).ToList();

    public DarbogazSonucu DarbogazHesapla(Sube sube) => strateji.DarbogazHesapla(sube);

    /// <summary>
    /// Produces a transfer recommendation from <paramref name="kaynak"/> to <paramref name="hedef"/>,
    /// applying the İş Kanunu guards to each candidate barista (travel time counted as work time).
    /// Returns <see cref="OneriSonucu.Basarili"/> with a freshly-built (Bekliyor) <see cref="TransferEmri"/>
    /// if at least one barista may legally move, otherwise <see cref="OneriSonucu.Reddedildi"/> with reasons.
    /// </summary>
    public OneriSonucu TransferOnerisiUret(
        Sube kaynak,
        Sube hedef,
        IReadOnlyList<BaristaMesaiBaglami> adaylar,
        TimeSpan seyahatSuresi,
        DateTimeOffset an)
    {
        var oneri = strateji.TransferOnerisiUret(kaynak, hedef);

        var uygunAdaySayisi = 0;
        var gerekceler = new List<string>();
        foreach (var aday in adaylar)
        {
            var baglam = new IsKanunuBaglami(
                aday.PersonelId,
                aday.BugunCalisilanSure,
                aday.HaftaCalisilanSure,
                aday.SonMoladanBuyanaSure,
                seyahatSuresi);

            var sonuc = isKanunuDegerlendirici.Degerlendir(baglam);
            if (sonuc.Uygun)
                uygunAdaySayisi++;
            else
                gerekceler.Add(sonuc.Gerekce!);
        }

        if (uygunAdaySayisi == 0)
            return new OneriSonucu.Reddedildi(
                $"İş Kanunu (4857) gereği transfer edilebilecek uygun personel bulunamadı. {string.Join(" ", gerekceler)}".Trim());

        var adet = Math.Min(oneri.OnerilenAdet, uygunAdaySayisi);
        var emir = fabrika.PersonelTransferiOlustur(kaynak.SubeId, hedef.SubeId, adet, an);
        return new OneriSonucu.Basarili(emir);
    }
}
