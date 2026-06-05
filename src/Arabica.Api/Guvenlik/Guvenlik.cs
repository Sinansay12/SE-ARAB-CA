using Arabica.Application.Denetim;
using FluentValidation;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Http;

namespace Arabica.Api.Guvenlik;

/// <summary>RBAC authorization policy names.</summary>
public static class Politikalar
{
    public const string Koordinator = "Koordinator"; // yalnız Bölge Koordinatörü
    public const string Yonetici = "Yonetici";       // Bölge Koordinatörü veya Şube Müdürü
}

/// <summary>Supplies the current actor + client IP to the audit writer from the HTTP context (NFR-S7).</summary>
public sealed class HttpDenetimBaglami(IHttpContextAccessor accessor) : IDenetimBaglami
{
    public string Aktor => accessor.HttpContext?.User.Identity?.Name ?? "anonim";

    public string IpAdresi
    {
        get
        {
            var ctx = accessor.HttpContext;
            if (ctx is null) return "-";
            var iletilen = ctx.Request.Headers["X-Forwarded-For"].FirstOrDefault();
            return !string.IsNullOrWhiteSpace(iletilen)
                ? iletilen.Split(',')[0].Trim()
                : ctx.Connection.RemoteIpAddress?.ToString() ?? "-";
        }
    }
}

/// <summary>
/// Maps domain/validation exceptions to RFC-7807 ProblemDetails (Turkish titles). Illegal state transitions
/// (InvalidOperationException) → 409; unknown status / bad input (ArgumentException) → 400; etc.
/// </summary>
public sealed class GlobalHataYakalayici(IProblemDetailsService problemDetails, ILogger<GlobalHataYakalayici> log)
    : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(HttpContext ctx, Exception ex, CancellationToken ct)
    {
        var (durum, baslik) = ex switch
        {
            ValidationException => (StatusCodes.Status400BadRequest, "Doğrulama hatası"),
            ArgumentException => (StatusCodes.Status400BadRequest, "Geçersiz istek"),
            InvalidOperationException => (StatusCodes.Status409Conflict, "Geçersiz durum geçişi"),
            UnauthorizedAccessException => (StatusCodes.Status403Forbidden, "Yetkisiz işlem"),
            _ => (StatusCodes.Status500InternalServerError, "Beklenmeyen hata")
        };

        log.LogWarning(ex, "İstek hatası ({Durum}): {Baslik}", durum, baslik);
        ctx.Response.StatusCode = durum;
        return await problemDetails.TryWriteAsync(new ProblemDetailsContext
        {
            HttpContext = ctx,
            Exception = ex,
            ProblemDetails = { Status = durum, Title = baslik, Detail = ex.Message }
        });
    }
}
