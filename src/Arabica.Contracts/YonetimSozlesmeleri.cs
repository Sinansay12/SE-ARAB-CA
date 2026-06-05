using System.Text.Json.Serialization;

namespace Arabica.Contracts.Api;

// Admin/management DTOs (additive — under /api/v1/admin/* and /api/v1/ozet). NOT part of the 5 frozen
// contracts. KVKK: personnel carry ONLY a non-identifying handle (TakmaAd) + numeric IDs — no PII fields.

public sealed record SubeOlusturIstegi(
    [property: JsonPropertyName("ad")] string Ad,
    [property: JsonPropertyName("maksimumKapasite")] int MaksimumKapasite,
    [property: JsonPropertyName("aktifPersonelSayisi")] int AktifPersonelSayisi);

public sealed record SubeGuncelleIstegi(
    [property: JsonPropertyName("ad")] string Ad,
    [property: JsonPropertyName("maksimumKapasite")] int MaksimumKapasite,
    [property: JsonPropertyName("aktifPersonelSayisi")] int AktifPersonelSayisi);

public sealed record SubeYonetimYaniti(
    [property: JsonPropertyName("subeId")] int SubeId,
    [property: JsonPropertyName("ad")] string Ad,
    [property: JsonPropertyName("maksimumKapasite")] int MaksimumKapasite,
    [property: JsonPropertyName("anlikMusteriSayisi")] int AnlikMusteriSayisi,
    [property: JsonPropertyName("aktifPersonelSayisi")] int AktifPersonelSayisi,
    [property: JsonPropertyName("aktif")] bool Aktif);

public sealed record ManuelTransferIstegi(
    [property: JsonPropertyName("kaynakSubeId")] int KaynakSubeId,
    [property: JsonPropertyName("hedefSubeId")] int HedefSubeId,
    [property: JsonPropertyName("adet")] int Adet,
    [property: JsonPropertyName("tip")] string Tip);

/// <summary>KVKK: only a non-identifying handle (TakmaAd) + branch — NO TC/name/phone fields exist.</summary>
public sealed record PersonelEkleIstegi(
    [property: JsonPropertyName("subeId")] int SubeId,
    [property: JsonPropertyName("takmaAd")] string TakmaAd,
    [property: JsonPropertyName("tip")] string Tip);

public sealed record PersonelYaniti(
    [property: JsonPropertyName("personelId")] long PersonelId,
    [property: JsonPropertyName("subeId")] int SubeId,
    [property: JsonPropertyName("takmaAd")] string TakmaAd,
    [property: JsonPropertyName("tip")] string Tip);

public sealed record DenetimKaydiYaniti(
    [property: JsonPropertyName("id")] long Id,
    [property: JsonPropertyName("aktor")] string Aktor,
    [property: JsonPropertyName("ipAdresi")] string IpAdresi,
    [property: JsonPropertyName("eylem")] string Eylem,
    [property: JsonPropertyName("detay")] string? Detay,
    [property: JsonPropertyName("zaman")] DateTimeOffset Zaman);

public sealed record StratejiYaniti(
    [property: JsonPropertyName("aktifSezon")] string AktifSezon,
    [property: JsonPropertyName("aciklama")] string Aciklama);

public sealed record OzetYaniti(
    [property: JsonPropertyName("subeSayisi")] int SubeSayisi,
    [property: JsonPropertyName("atilSube")] int AtilSube,
    [property: JsonPropertyName("darbogazSube")] int DarbogazSube,
    [property: JsonPropertyName("bekleyenTransfer")] int BekleyenTransfer,
    [property: JsonPropertyName("ortalamaGecikmeMs")] double OrtalamaGecikmeMs);
