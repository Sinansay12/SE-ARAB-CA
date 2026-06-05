using Arabica.Api.Guvenlik;
using Arabica.Application.Yonetim;
using Arabica.Contracts.Api;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Arabica.Api.Controllers;

/// <summary>
/// Admin/management endpoints (additive — NOT among the 5 frozen contracts). Bölge Koordinatörü only.
/// Validation → 400 (ProblemDetails-TR via pipeline), not-found → 404, writes are audited (IP+timestamp).
/// </summary>
[ApiController]
[Route("api/v1/admin")]
[Authorize(Policy = Politikalar.Koordinator)]
public sealed class AdminController(ISender sender) : ControllerBase
{
    // ---- Şube yönetimi (CRUD) ----
    [HttpGet("sube")]
    public async Task<ActionResult<IReadOnlyList<SubeYonetimYaniti>>> SubeListesi(CancellationToken ct)
        => Ok(await sender.Send(new SubeYonetimListesiQuery(), ct));

    [HttpPost("sube")]
    public async Task<ActionResult<SubeYonetimYaniti>> SubeOlustur([FromBody] SubeOlusturIstegi istek, CancellationToken ct)
        => Ok(await sender.Send(new SubeOlusturCommand(istek.Ad, istek.MaksimumKapasite, istek.AktifPersonelSayisi), ct));

    [HttpPut("sube/{id:int}")]
    public async Task<ActionResult<SubeYonetimYaniti>> SubeGuncelle(int id, [FromBody] SubeGuncelleIstegi istek, CancellationToken ct)
    {
        var sonuc = await sender.Send(new SubeGuncelleCommand(id, istek.Ad, istek.MaksimumKapasite, istek.AktifPersonelSayisi), ct);
        return sonuc is null ? Problem(statusCode: StatusCodes.Status404NotFound, title: "Şube bulunamadı") : Ok(sonuc);
    }

    [HttpPatch("sube/{id:int}/pasiflestir")]
    public async Task<ActionResult<SubeYonetimYaniti>> SubePasiflestir(int id, CancellationToken ct)
    {
        var sonuc = await sender.Send(new SubePasiflestirCommand(id), ct);
        return sonuc is null ? Problem(statusCode: StatusCodes.Status404NotFound, title: "Şube bulunamadı") : Ok(sonuc);
    }

    // ---- Personel (anonim — KVKK) ----
    [HttpPost("personel")]
    public async Task<ActionResult<PersonelYaniti>> PersonelEkle([FromBody] PersonelEkleIstegi istek, CancellationToken ct)
    {
        var sonuc = await sender.Send(new PersonelEkleCommand(istek.SubeId, istek.TakmaAd, istek.Tip), ct);
        return sonuc is null ? Problem(statusCode: StatusCodes.Status404NotFound, title: "Aktif şube bulunamadı") : Ok(sonuc);
    }

    // ---- Manuel transfer emri (Factory Method + outbox → ESB) ----
    [HttpPost("transfer/manuel")]
    public async Task<ActionResult<TransferOneriYaniti>> ManuelTransfer([FromBody] ManuelTransferIstegi istek, CancellationToken ct)
    {
        var sonuc = await sender.Send(new ManuelTransferOlusturCommand(istek.KaynakSubeId, istek.HedefSubeId, istek.Adet, istek.Tip), ct);
        return sonuc is null ? Problem(statusCode: StatusCodes.Status404NotFound, title: "Kaynak/hedef şube bulunamadı veya pasif") : Ok(sonuc);
    }

    // ---- Denetim / sistem logları ----
    [HttpGet("denetim")]
    public async Task<ActionResult<IReadOnlyList<DenetimKaydiYaniti>>> Denetim([FromQuery] int sayfa = 1, [FromQuery] int boyut = 20, CancellationToken ct = default)
        => Ok(await sender.Send(new DenetimLoglariQuery(sayfa, boyut), ct));

    // ---- Optimizasyon motoru (canlı) + Strateji (runtime) ----
    [HttpPost("optimizasyon/tetikle")]
    public async Task<ActionResult<IReadOnlyList<TransferOneriYaniti>>> OptimizasyonTetikle(CancellationToken ct)
        => Ok(await sender.Send(new OptimizasyonTetikleCommand(), ct));

    [HttpGet("strateji")]
    public async Task<ActionResult<StratejiYaniti>> StratejiGetir(CancellationToken ct)
        => Ok(await sender.Send(new StratejiQuery(), ct));

    [HttpPost("strateji")]
    public async Task<ActionResult<StratejiYaniti>> StratejiAyarla([FromQuery] string? ad, CancellationToken ct)
        => Ok(await sender.Send(new StratejiAyarlaCommand(ad), ct));
}
