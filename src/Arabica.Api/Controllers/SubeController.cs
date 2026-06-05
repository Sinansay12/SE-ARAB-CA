using System.Security.Claims;
using Arabica.Api.Guvenlik;
using Arabica.Application.Kimlik;
using Arabica.Application.Subeler;
using Arabica.Contracts.Api;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Arabica.Api.Controllers;

/// <summary>Branch/occupancy endpoints. Frozen contracts: GET /api/v1/sube/doluluk, GET /api/v1/sube/{subeId}/detay.</summary>
[ApiController]
[Route("api/v1/sube")]
[Authorize]
public sealed class SubeController(ISender sender) : ControllerBase
{
    /// <summary>GET /api/v1/sube/doluluk — live occupancy for ALL branches (Bölge Koordinatörü only).</summary>
    [HttpGet("doluluk")]
    [Authorize(Policy = Politikalar.Koordinator)]
    public async Task<ActionResult<IReadOnlyList<SubeDolulukYaniti>>> Doluluk(CancellationToken ct)
        => Ok(await sender.Send(new SubeDolulukQuery(), ct));

    /// <summary>GET /api/v1/sube/{subeId}/detay — one branch's detail. A Şube Müdürü may read only their own branch.</summary>
    [HttpGet("{subeId:int}/detay")]
    [Authorize(Policy = Politikalar.Yonetici)]
    public async Task<ActionResult<SubeDetayYaniti>> Detay(int subeId, CancellationToken ct)
    {
        if (User.IsInRole(Roller.SubeMuduru) && KullaniciSubeId() != subeId)
            return Problem(statusCode: StatusCodes.Status403Forbidden, title: "Yalnızca kendi şubenizi görüntüleyebilirsiniz");

        var detay = await sender.Send(new SubeDetayQuery(subeId), ct);
        return detay is null
            ? Problem(statusCode: StatusCodes.Status404NotFound, title: "Şube bulunamadı")
            : Ok(detay);
    }

    private int? KullaniciSubeId()
        => int.TryParse(User.FindFirstValue("subeId"), out var id) ? id : null;
}
