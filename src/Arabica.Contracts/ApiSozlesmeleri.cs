using System.Text.Json.Serialization;

namespace Arabica.Contracts.Api;

// REST request/response contracts. JSON property names are pinned with [JsonPropertyName] so the wire
// contract is exact and independent of the host's global JsonNamingPolicy. These mirror the 5 endpoints
// frozen in project-srs.md / context.md.

/// <summary>POST /api/v1/auth/login — request body.</summary>
public sealed record GirisIstegi(
    [property: JsonPropertyName("kullaniciAdi")] string KullaniciAdi,
    [property: JsonPropertyName("sifre")] string Sifre);

/// <summary>POST /api/v1/auth/login — response (JWT).</summary>
public sealed record GirisYaniti(
    [property: JsonPropertyName("token")] string Token,
    [property: JsonPropertyName("rol")] string Rol,
    [property: JsonPropertyName("gecerlilikSaniye")] int GecerlilikSaniye);

/// <summary>GET /api/v1/sube/doluluk — one row per branch (live occupancy stream for the dashboard).</summary>
public sealed record SubeDolulukYaniti(
    [property: JsonPropertyName("subeId")] int SubeId,
    [property: JsonPropertyName("ad")] string Ad,
    [property: JsonPropertyName("dolulukOrani")] decimal DolulukOrani,
    [property: JsonPropertyName("maksimumKapasite")] int MaksimumKapasite,
    [property: JsonPropertyName("aktifPersonelSayisi")] int AktifPersonelSayisi,
    [property: JsonPropertyName("seviye")] string Seviye);

/// <summary>One staff member's status within a branch detail response.</summary>
public sealed record PersonelDurumYaniti(
    [property: JsonPropertyName("personelId")] int PersonelId,
    [property: JsonPropertyName("takmaAd")] string TakmaAd,
    [property: JsonPropertyName("durum")] string Durum);

/// <summary>GET /api/v1/sube/{subeId}/detay — branch details + current staff status.</summary>
public sealed record SubeDetayYaniti(
    [property: JsonPropertyName("subeId")] int SubeId,
    [property: JsonPropertyName("ad")] string Ad,
    [property: JsonPropertyName("dolulukOrani")] decimal DolulukOrani,
    [property: JsonPropertyName("seviye")] string Seviye,
    [property: JsonPropertyName("personeller")] IReadOnlyList<PersonelDurumYaniti> Personeller);

/// <summary>GET /api/v1/transfer/oneriler — one pending engine-generated recommendation.</summary>
public sealed record TransferOneriYaniti(
    [property: JsonPropertyName("transferId")] long TransferId,
    [property: JsonPropertyName("kaynakSubeId")] int KaynakSubeId,
    [property: JsonPropertyName("hedefSubeId")] int HedefSubeId,
    [property: JsonPropertyName("tip")] string Tip,
    [property: JsonPropertyName("adet")] int Adet,
    [property: JsonPropertyName("durum")] string Durum,
    [property: JsonPropertyName("tahminiVarisDakika")] int TahminiVarisDakika);

/// <summary>POST /api/v1/transfer/islem — request body. Aksiyon: "ONAYLA" | "REDDET".</summary>
public sealed record TransferIslemIstegi(
    [property: JsonPropertyName("transferId")] long TransferId,
    [property: JsonPropertyName("aksiyon")] string Aksiyon);

/// <summary>POST /api/v1/transfer/islem — response.</summary>
public sealed record TransferIslemYaniti(
    [property: JsonPropertyName("transferId")] long TransferId,
    [property: JsonPropertyName("durum")] string Durum,
    [property: JsonPropertyName("mesaj")] string Mesaj);

/// <summary>GET /api/v1/transfer/gecmis — one historical transfer row (Bölge Koordinatörü only).</summary>
public sealed record TransferGecmisYaniti(
    [property: JsonPropertyName("transferId")] long TransferId,
    [property: JsonPropertyName("kaynakSubeId")] int KaynakSubeId,
    [property: JsonPropertyName("hedefSubeId")] int HedefSubeId,
    [property: JsonPropertyName("tip")] string Tip,
    [property: JsonPropertyName("adet")] int Adet,
    [property: JsonPropertyName("durum")] string Durum,
    [property: JsonPropertyName("redGerekcesi")] string? RedGerekcesi,
    [property: JsonPropertyName("olusturulmaZamani")] DateTimeOffset OlusturulmaZamani);

/// <summary>Real-time transfer notification pushed to dashboards over SignalR (KVKK: numeric IDs only).</summary>
public sealed record TransferBildirimGorunumu(
    [property: JsonPropertyName("transferId")] long TransferId,
    [property: JsonPropertyName("kaynakSubeId")] int KaynakSubeId,
    [property: JsonPropertyName("hedefSubeId")] int HedefSubeId,
    [property: JsonPropertyName("tip")] string Tip,
    [property: JsonPropertyName("adet")] int Adet,
    [property: JsonPropertyName("durum")] string Durum);
