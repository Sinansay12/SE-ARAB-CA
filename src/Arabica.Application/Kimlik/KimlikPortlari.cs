namespace Arabica.Application.Kimlik;

/// <summary>System roles (RBAC). Kept as constants so policies, tokens, and checks agree.</summary>
public static class Roller
{
    public const string BolgeKoordinatoru = "BolgeKoordinatoru";
    public const string SubeMuduru = "SubeMuduru";
}

/// <summary>Result of a successful login: a signed JWT plus role and lifetime.</summary>
public sealed record GirisSonucu(string Token, string Rol, int GecerlilikSaniye);

/// <summary>Authentication port: verifies credentials (salted hash) and issues a JWT. Impl in Infrastructure.</summary>
public interface IKimlikDogrulamaServisi
{
    Task<GirisSonucu?> GirisYapAsync(string kullaniciAdi, string sifre, CancellationToken ct);
}

/// <summary>MFA (TOTP) verification port for critical approvals. Impl in Infrastructure (Otp.NET).</summary>
public interface IMfaDogrulayici
{
    Task<bool> DogrulaAsync(string kullaniciAdi, string kod, CancellationToken ct);
}
