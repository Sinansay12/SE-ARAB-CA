using System.Security.Claims;
using Arabica.Api.Guvenlik;
using Arabica.Application.Denetim;
using Arabica.Application.Kimlik;
using Arabica.Application.Transferler;
using Arabica.Contracts.Api;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Arabica.Api.Controllers;

/// <summary>
/// Transfer endpoints. Frozen contracts: GET /api/v1/transfer/oneriler, POST /api/v1/transfer/islem
/// (body { transferId, aksiyon }). MFA (TOTP) is required for ONAYLA and is supplied via the
/// <c>X-MFA-Code</c> header so the JSON body contract stays exactly as specified.
/// </summary>
[ApiController]
[Route("api/v1/transfer")]
[Authorize(Policy = Politikalar.Yonetici)]
public sealed class TransferController(ISender sender, IMfaDogrulayici mfa, IDenetimYazici denetim) : ControllerBase
{
    /// <summary>GET /api/v1/transfer/oneriler — pending engine-generated recommendations.</summary>
    [HttpGet("oneriler")]
    public async Task<ActionResult<IReadOnlyList<TransferOneriYaniti>>> Oneriler(CancellationToken ct)
        => Ok(await sender.Send(new BekleyenTransferOnerileriQuery(), ct));

    /// <summary>
    /// GET /api/v1/transfer/gecmis — transfer history (all statuses). NEW read-only endpoint (not one of the
    /// 5 frozen contracts). Bölge Koordinatörü only (method-level Koordinator policy ⊂ class-level Yonetici).
    /// </summary>
    [HttpGet("gecmis")]
    [Authorize(Policy = Politikalar.Koordinator)]
    public async Task<ActionResult<IReadOnlyList<TransferGecmisYaniti>>> Gecmis([FromQuery] int enFazla = 100, CancellationToken ct = default)
        => Ok(await sender.Send(new TransferGecmisiQuery(enFazla), ct));

    /// <summary>POST /api/v1/transfer/islem — approve/reject. ONAYLA requires a valid X-MFA-Code header.</summary>
    [HttpPost("islem")]
    [ProducesResponseType(typeof(TransferIslemYaniti), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<TransferIslemYaniti>> Islem(
        [FromBody] TransferIslemIstegi istek,
        [FromHeader(Name = "X-MFA-Code")] string? mfaKod,
        CancellationToken ct)
    {
        var aksiyon = istek.Aksiyon?.Trim().ToUpperInvariant();
        var hedefDurum = aksiyon switch
        {
            "ONAYLA" => "ONAYLANDI",
            "REDDET" => "REDDEDILDI",
            _ => null
        };
        if (hedefDurum is null)
            return Problem(statusCode: StatusCodes.Status400BadRequest, title: "Geçersiz aksiyon (ONAYLA/REDDET bekleniyor)");

        // RBAC (SRS matrisi): Şube Müdürü yalnızca kaynak VEYA hedefi kendi şubesi olan transferlere
        // müdahale edebilir; Bölge Koordinatörü kısıtsızdır. Şubeler emirden (sunucu tarafı) türetilir.
        if (User.IsInRole(Roller.SubeMuduru))
        {
            var subeler = await sender.Send(new TransferSubeleriQuery(istek.TransferId), ct);
            if (subeler is not null)
            {
                var subeId = KullaniciSubeId();
                if (subeId != subeler.KaynakSubeId && subeId != subeler.HedefSubeId)
                    return Problem(statusCode: StatusCodes.Status403Forbidden,
                        title: "Yalnızca kendi şubenizi ilgilendiren transferlere müdahale edebilirsiniz");
            }
            // null ise emir yok; komut Bulunamadi (404) döndürecek.
        }

        // MFA, yalnızca kritik onay (ONAYLA) için zorunlu.
        if (aksiyon == "ONAYLA" && !await mfa.DogrulaAsync(User.Identity!.Name!, mfaKod ?? string.Empty, ct))
            return Problem(statusCode: StatusCodes.Status401Unauthorized, title: "MFA doğrulaması gerekli veya geçersiz (X-MFA-Code)");

        var gerekce = aksiyon == "REDDET" ? "Yönetici tarafından reddedildi" : null;
        var sonuc = await sender.Send(new TransferIslemiUygulaCommand(istek.TransferId, hedefDurum, gerekce), ct);

        return sonuc switch
        {
            TransferIslemSonucu.Basarili b => await OnayDonusu(b, aksiyon!, ct),
            TransferIslemSonucu.Bulunamadi => Problem(statusCode: StatusCodes.Status404NotFound, title: "Transfer emri bulunamadı"),
            _ => Problem(statusCode: StatusCodes.Status500InternalServerError, title: "Bilinmeyen sonuç")
        };
    }

    private async Task<ActionResult<TransferIslemYaniti>> OnayDonusu(TransferIslemSonucu.Basarili b, string aksiyon, CancellationToken ct)
    {
        await denetim.YazAsync($"TRANSFER:{aksiyon}", $"transfer {b.TransferId} → {b.Durum}", ct);
        return Ok(new TransferIslemYaniti(b.TransferId, b.Durum, "İşlem başarılı"));
    }

    private int? KullaniciSubeId()
        => int.TryParse(User.FindFirstValue("subeId"), out var id) ? id : null;
}
