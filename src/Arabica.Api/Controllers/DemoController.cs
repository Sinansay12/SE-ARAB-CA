using Arabica.Api.Guvenlik;
using Arabica.Application.Gozlem;
using Arabica.Application.Mesajlasma;
using Arabica.Application.Ortak;
using Arabica.Contracts.Olaylar;
using Arabica.Infrastructure.Mesajlasma;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

namespace Arabica.Api.Controllers;

/// <summary>
/// Demo + observability endpoints (NOT part of the 5 frozen contracts). The feeder drives the real
/// POS/PDKS Kafka ingest stream so the ≤2 s latency (NFR-P1 / M4) can be measured end-to-end.
/// </summary>
[ApiController]
[Route("api/v1")]
[Authorize(Policy = Politikalar.Koordinator)]
public sealed class DemoController(
    IKafkaUreticisi uretici,
    IZamanSaglayici zaman,
    ILatencyKaydedici latency,
    IOptions<KafkaSecenekleri> kafka) : ControllerBase
{
    /// <summary>POST /api/v1/demo/besle?adet=N — produces N POS events (timestamped now) to the Kafka stream.</summary>
    [HttpPost("demo/besle")]
    public async Task<IActionResult> Besle([FromQuery] int adet = 20, CancellationToken ct = default)
    {
        var topic = kafka.Value.PosTopic;
        for (var i = 0; i < adet; i++)
        {
            var subeId = (i % 2) + 1;
            var olay = new PosOlayi(subeId, SiparisAdedi: 40 + (i % 60), ToplamTutar: 0m, UretimZamani: zaman.Simdi);
            await uretici.YayinlaAsync(topic, subeId.ToString(), olay, ct);
        }
        return Ok(new { uretildi = adet, topic });
    }

    /// <summary>GET /api/v1/metrik/gecikme — end-to-end ingest→processing latency summary (ms).</summary>
    [HttpGet("metrik/gecikme")]
    public ActionResult<LatencyOzeti> Gecikme() => Ok(latency.Ozet());
}
