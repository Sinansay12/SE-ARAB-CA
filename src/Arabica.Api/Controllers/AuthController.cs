using Arabica.Application.Denetim;
using Arabica.Application.Kimlik;
using Arabica.Contracts.Api;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Arabica.Api.Controllers;

/// <summary>Authentication endpoint. Frozen contract: POST /api/v1/auth/login.</summary>
[ApiController]
[Route("api/v1/auth")]
public sealed class AuthController(IKimlikDogrulamaServisi kimlik, IDenetimYazici denetim) : ControllerBase
{
    /// <summary>POST /api/v1/auth/login — body { kullaniciAdi, sifre } → JWT.</summary>
    [HttpPost("login")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(GirisYaniti), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<GirisYaniti>> Login([FromBody] GirisIstegi istek, CancellationToken ct)
    {
        var sonuc = await kimlik.GirisYapAsync(istek.KullaniciAdi, istek.Sifre, ct);
        if (sonuc is null)
        {
            await denetim.YazAsync("GIRIS:BASARISIZ", $"kullanıcı: {istek.KullaniciAdi}", ct);
            return Problem(statusCode: StatusCodes.Status401Unauthorized, title: "Kimlik doğrulama başarısız");
        }

        await denetim.YazAsync("GIRIS:BASARILI", $"kullanıcı: {istek.KullaniciAdi}", ct);
        return Ok(new GirisYaniti(sonuc.Token, sonuc.Rol, sonuc.GecerlilikSaniye));
    }
}
