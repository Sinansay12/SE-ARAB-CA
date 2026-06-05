namespace Arabica.Domain.IsHukuku;

/// <summary>
/// İş Kanunu (4857) statutory limits used by the transfer guards.
/// Defaults are conservative placeholders and REQUIRE legal review before production (blueprint §9 G6).
/// Overridable from configuration.
/// </summary>
public sealed record IsKanunuLimitleri(
    TimeSpan GunlukAzamiMesai,
    TimeSpan HaftalikAzamiMesai,
    TimeSpan MolaGerektirenSurekliCalisma)
{
    public static IsKanunuLimitleri Varsayilan { get; } = new(
        GunlukAzamiMesai: TimeSpan.FromHours(11),          // 4857: günlük azami çalışma
        HaftalikAzamiMesai: TimeSpan.FromHours(45),        // 4857: haftalık azami çalışma
        MolaGerektirenSurekliCalisma: TimeSpan.FromHours(5)); // sürekli çalışma → ara dinlenmesi (mola) hak edişi
}

/// <summary>
/// Evaluation context for one candidate barista against a proposed transfer.
/// NFR-L2: travel time between branches counts as working time — see the computed totals below.
/// </summary>
public sealed record IsKanunuBaglami(
    int PersonelId,
    TimeSpan BugunCalisilanSure,
    TimeSpan HaftaCalisilanSure,
    TimeSpan SonMoladanBuyanaSure,
    TimeSpan SeyahatSuresi)
{
    /// <summary>Daily worked time INCLUDING the inter-branch travel the transfer would add (NFR-L2).</summary>
    public TimeSpan TransferSonrasiGunlukMesai => BugunCalisilanSure + SeyahatSuresi;

    /// <summary>Weekly worked time including travel.</summary>
    public TimeSpan TransferSonrasiHaftalikMesai => HaftaCalisilanSure + SeyahatSuresi;
}

/// <summary>Result of a single guard rule: allowed, or rejected with a human-readable Turkish reason.</summary>
public sealed record KuralSonucu(bool Uygun, string? Gerekce)
{
    public static KuralSonucu Uygundur { get; } = new(true, null);
    public static KuralSonucu Reddet(string gerekce) => new(false, gerekce);
}

/// <summary>A composable İş Kanunu guard rule (Strategy-of-rules).</summary>
public interface IIsKanunuKurali
{
    KuralSonucu Degerlendir(IsKanunuBaglami baglam, IsKanunuLimitleri limitler);
}

/// <summary>Blocks the transfer if (worked + travel) would exceed the daily maximum (NFR-L2).</summary>
public sealed class GunlukAzamiMesaiKurali : IIsKanunuKurali
{
    public KuralSonucu Degerlendir(IsKanunuBaglami baglam, IsKanunuLimitleri limitler)
        => baglam.TransferSonrasiGunlukMesai > limitler.GunlukAzamiMesai
            ? KuralSonucu.Reddet(
                $"Personel {baglam.PersonelId}: yol süresi dahil günlük çalışma " +
                $"({baglam.TransferSonrasiGunlukMesai.TotalHours:0.#} s) yasal azami " +
                $"({limitler.GunlukAzamiMesai.TotalHours:0.#} s) sınırını aşıyor.")
            : KuralSonucu.Uygundur;
}

/// <summary>Blocks the transfer if (worked + travel) would exceed the weekly maximum.</summary>
public sealed class HaftalikAzamiMesaiKurali : IIsKanunuKurali
{
    public KuralSonucu Degerlendir(IsKanunuBaglami baglam, IsKanunuLimitleri limitler)
        => baglam.TransferSonrasiHaftalikMesai > limitler.HaftalikAzamiMesai
            ? KuralSonucu.Reddet(
                $"Personel {baglam.PersonelId}: yol süresi dahil haftalık çalışma " +
                $"({baglam.TransferSonrasiHaftalikMesai.TotalHours:0.#} s) yasal azami " +
                $"({limitler.HaftalikAzamiMesai.TotalHours:0.#} s) sınırını aşıyor.")
            : KuralSonucu.Uygundur;
}

/// <summary>Blocks the transfer if a legally-required break (ara dinlenmesi) is already due.</summary>
public sealed class ZorunluMolaKurali : IIsKanunuKurali
{
    public KuralSonucu Degerlendir(IsKanunuBaglami baglam, IsKanunuLimitleri limitler)
        => baglam.SonMoladanBuyanaSure >= limitler.MolaGerektirenSurekliCalisma
            ? KuralSonucu.Reddet(
                $"Personel {baglam.PersonelId}: yasal ara dinlenmesi (mola) hak edildi " +
                $"(kesintisiz {baglam.SonMoladanBuyanaSure.TotalHours:0.#} s); transfer engellendi.")
            : KuralSonucu.Uygundur;
}

/// <summary>
/// CHAIN OF RESPONSIBILITY (behavioural pattern). One link per İş Kanunu rule; each link either blocks
/// the transfer (short-circuit) or delegates to the next link. The first violation stops the chain and
/// returns its reason — identical semantics to the previous loop, now expressed as an explicit chain.
/// </summary>
public abstract class IsKanunuHalkasi
{
    private IsKanunuHalkasi? _sonraki;

    /// <summary>Links the next handler; returns it so links can be chained fluently.</summary>
    public IsKanunuHalkasi SonrakiniAyarla(IsKanunuHalkasi sonraki)
    {
        _sonraki = sonraki;
        return sonraki;
    }

    /// <summary>Evaluates this link; on success passes the request down the chain.</summary>
    public KuralSonucu Isle(IsKanunuBaglami baglam, IsKanunuLimitleri limitler)
    {
        var sonuc = Degerlendir(baglam, limitler);
        if (!sonuc.Uygun)
            return sonuc; // zinciri kes — ilk ihlal kazanır
        return _sonraki?.Isle(baglam, limitler) ?? KuralSonucu.Uygundur;
    }

    protected abstract KuralSonucu Degerlendir(IsKanunuBaglami baglam, IsKanunuLimitleri limitler);
}

/// <summary>Adapts an <see cref="IIsKanunuKurali"/> rule into a chain link.</summary>
public sealed class KuralHalkasi(IIsKanunuKurali kural) : IsKanunuHalkasi
{
    protected override KuralSonucu Degerlendir(IsKanunuBaglami baglam, IsKanunuLimitleri limitler)
        => kural.Degerlendir(baglam, limitler);
}

/// <summary>
/// Builds and runs the İş Kanunu Chain of Responsibility. Public surface is unchanged
/// (<see cref="Degerlendir"/> + <see cref="Varsayilan"/>) so existing tests keep passing.
/// </summary>
public sealed class IsKanunuDegerlendirici
{
    private readonly IsKanunuHalkasi? _zincirBasi;
    private readonly IsKanunuLimitleri _limitler;

    public IsKanunuDegerlendirici(IEnumerable<IIsKanunuKurali> kurallar, IsKanunuLimitleri? limitler = null)
    {
        _limitler = limitler ?? IsKanunuLimitleri.Varsayilan;
        _zincirBasi = ZincirKur(kurallar);
    }

    public KuralSonucu Degerlendir(IsKanunuBaglami baglam)
        => _zincirBasi?.Isle(baglam, _limitler) ?? KuralSonucu.Uygundur;

    /// <summary>Convenience factory wiring the three default 4857 guards into a chain.</summary>
    public static IsKanunuDegerlendirici Varsayilan(IsKanunuLimitleri? limitler = null)
        => new(
            [new GunlukAzamiMesaiKurali(), new HaftalikAzamiMesaiKurali(), new ZorunluMolaKurali()],
            limitler);

    private static IsKanunuHalkasi? ZincirKur(IEnumerable<IIsKanunuKurali> kurallar)
    {
        IsKanunuHalkasi? bas = null;
        IsKanunuHalkasi? son = null;
        foreach (var kural in kurallar)
        {
            var halka = new KuralHalkasi(kural);
            if (bas is null)
            {
                bas = son = halka;
            }
            else
            {
                son = son!.SonrakiniAyarla(halka);
            }
        }
        return bas;
    }
}
