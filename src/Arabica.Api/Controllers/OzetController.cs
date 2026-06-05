using System.Security.Claims;
using Arabica.Api.Guvenlik;
using Arabica.Application.Kimlik;
using Arabica.Application.Yonetim;
using Arabica.Contracts.Api;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Arabica.Api.Controllers;

/// <summary>Role-aware summary statistics for the dashboard cards. Both roles; manager gets own-branch subset.</summary>
[ApiController]
[Route("api/v1")]
[Authorize(Policy = Politikalar.Yonetici)]
public sealed class OzetController(ISender sender) : ControllerBase
{
    /// <summary>GET /api/v1/ozet — şube/atıl/darboğaz/bekleyen-transfer/ortalama-gecikme özeti (role-aware).</summary>
    [HttpGet("ozet")]
    public async Task<ActionResult<OzetYaniti>> Ozet(CancellationToken ct)
    {
        var koordinator = User.IsInRole(Roller.BolgeKoordinatoru);
        var subeId = int.TryParse(User.FindFirstValue("subeId"), out var id) ? id : (int?)null;
        return Ok(await sender.Send(new OzetQuery(koordinator, subeId), ct));
    }
}
