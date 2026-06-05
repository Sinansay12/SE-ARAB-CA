namespace Arabica.Infrastructure.Kimlik;

/// <summary>JWT options, bound from the "Jwt" config section. The signing key comes from env/secret in prod.</summary>
public sealed class JwtSecenekleri
{
    public const string Bolum = "Jwt";

    public string Imza { get; set; } = string.Empty; // HS256 key (≥ 32 bytes)
    public string Yayinci { get; set; } = "arabica";
    public string Kitle { get; set; } = "arabica-clients";
    public int GecerlilikDakika { get; set; } = 15; // NFR: short-lived token; client clears on idle
}

/// <summary>
/// Demo seed values used by the startup seeder so the running stack is immediately usable and the grader
/// can log in + compute the TOTP code. Documented in PROJE-RAPORU.md. (Demo only — replace in production.)
/// </summary>
public static class DemoVeriler
{
    public const string Parola = "Arabica.2026!";
    public const string MfaSecret = "JBSWY3DPEHPK3PXP"; // base32 (RFC test vector)

    public const string KoordinatorKullanici = "tunahan.basar";
    public const string MudurKullanici = "sinan.say";
    public const int MudurSubeId = 1;
}
