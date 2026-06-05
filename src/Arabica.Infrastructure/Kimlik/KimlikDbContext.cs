using Microsoft.EntityFrameworkCore;

namespace Arabica.Infrastructure.Kimlik;

/// <summary>
/// Credential store. Separate <c>kimlik</c> schema / context (access-controlled) so authentication data is
/// isolated from the analytics hot/hist models. KVKK: holds only the login handle (isim.soyisim), a salted
/// password hash, role, optional branch, and a TOTP secret — no TC/phone/real-name analytics data.
/// </summary>
public sealed class KimlikDbContext(DbContextOptions<KimlikDbContext> options) : DbContext(options)
{
    public const string Sema = "kimlik";

    public DbSet<Kullanici> Kullanicilar => Set<Kullanici>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema(Sema);

        modelBuilder.Entity<Kullanici>(b =>
        {
            b.ToTable("kullanicilar");
            b.HasKey(x => x.KullaniciAdi);
            b.Property(x => x.KullaniciAdi).HasColumnName("kullanici_adi").HasMaxLength(150);
            b.Property(x => x.ParolaHash).HasColumnName("parola_hash").HasMaxLength(500).IsRequired();
            b.Property(x => x.Rol).HasColumnName("rol").HasMaxLength(50).IsRequired();
            b.Property(x => x.SubeId).HasColumnName("sube_id");
            b.Property(x => x.MfaSecret).HasColumnName("mfa_secret").HasMaxLength(100).IsRequired();
        });
    }
}

/// <summary>A system user/credential. Password is stored salted+hashed (PBKDF2); never in clear text.</summary>
public sealed class Kullanici
{
    public string KullaniciAdi { get; set; } = string.Empty;
    public string ParolaHash { get; set; } = string.Empty;
    public string Rol { get; set; } = string.Empty;
    public int? SubeId { get; set; }
    public string MfaSecret { get; set; } = string.Empty;
}
