using System.Globalization;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Arabica.Application.Kimlik;
using Arabica.Application.Ortak;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using OtpNet;

namespace Arabica.Infrastructure.Kimlik;

/// <summary>
/// Authentication: verifies the salted PBKDF2 password hash (<see cref="PasswordHasher{T}"/>) and issues a
/// signed JWT carrying name + role (+ branch for managers).
/// </summary>
public sealed class KimlikDogrulamaServisi(KimlikDbContext ctx, IOptions<JwtSecenekleri> jwtOpt, IZamanSaglayici zaman)
    : IKimlikDogrulamaServisi
{
    private static readonly PasswordHasher<Kullanici> Hasher = new();
    private readonly JwtSecenekleri _jwt = jwtOpt.Value;

    public async Task<GirisSonucu?> GirisYapAsync(string kullaniciAdi, string sifre, CancellationToken ct)
    {
        var kullanici = await ctx.Kullanicilar.AsNoTracking().FirstOrDefaultAsync(x => x.KullaniciAdi == kullaniciAdi, ct);
        if (kullanici is null)
            return null;
        if (Hasher.VerifyHashedPassword(kullanici, kullanici.ParolaHash, sifre) == PasswordVerificationResult.Failed)
            return null;

        return new GirisSonucu(TokenUret(kullanici), kullanici.Rol, _jwt.GecerlilikDakika * 60);
    }

    private string TokenUret(Kullanici kullanici)
    {
        var claims = new List<Claim>
        {
            new(ClaimTypes.Name, kullanici.KullaniciAdi),
            new(ClaimTypes.Role, kullanici.Rol)
        };
        if (kullanici.SubeId.HasValue)
            claims.Add(new Claim("subeId", kullanici.SubeId.Value.ToString(CultureInfo.InvariantCulture)));

        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_jwt.Imza));
        var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);
        var token = new JwtSecurityToken(
            issuer: _jwt.Yayinci,
            audience: _jwt.Kitle,
            claims: claims,
            expires: zaman.Simdi.UtcDateTime.AddMinutes(_jwt.GecerlilikDakika),
            signingCredentials: creds);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}

/// <summary>MFA: verifies a TOTP code against the user's stored secret (Otp.NET), with a small drift window.</summary>
public sealed class MfaDogrulayici(KimlikDbContext ctx) : IMfaDogrulayici
{
    public async Task<bool> DogrulaAsync(string kullaniciAdi, string kod, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(kod))
            return false;

        var kullanici = await ctx.Kullanicilar.AsNoTracking().FirstOrDefaultAsync(x => x.KullaniciAdi == kullaniciAdi, ct);
        if (kullanici is null)
            return false;

        var totp = new Totp(Base32Encoding.ToBytes(kullanici.MfaSecret));
        return totp.VerifyTotp(kod, out _, new VerificationWindow(previous: 2, future: 2));
    }
}
