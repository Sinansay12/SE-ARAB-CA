using Arabica.Application.Cikti;
using Arabica.Domain.Transferler;
using Microsoft.EntityFrameworkCore;

namespace Arabica.Infrastructure.Veri;

/// <summary>
/// "History" context — transfer orders, the transactional outbox, and the audit log. Default schema
/// <c>hist</c>; targets the primary database. A single SaveChanges over this context commits the transfer
/// state change AND its outbox row atomically (blueprint §7).
/// </summary>
public sealed class HistoryDbContext(DbContextOptions<HistoryDbContext> options) : DbContext(options)
{
    public const string Sema = "hist";

    public DbSet<TransferEmri> TransferEmirleri => Set<TransferEmri>();
    public DbSet<OutboxKaydi> Outbox => Set<OutboxKaydi>();
    public DbSet<DenetimKaydi> DenetimKayitlari => Set<DenetimKaydi>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema(Sema);

        modelBuilder.Entity<TransferEmri>(b =>
        {
            b.ToTable("transfer_emirleri");
            b.HasKey(x => x.EmirId);
            b.Property(x => x.EmirId).HasColumnName("emir_id").ValueGeneratedOnAdd();
            b.Property(x => x.KaynakSubeId).HasColumnName("kaynak_sube_id");
            b.Property(x => x.HedefSubeId).HasColumnName("hedef_sube_id");
            b.Property(x => x.Tip).HasColumnName("tip").HasConversion<string>().HasMaxLength(20);
            b.Property(x => x.Adet).HasColumnName("adet");
            b.Property(x => x.Durum).HasColumnName("durum").HasConversion<string>().HasMaxLength(20);
            b.Property(x => x.RedGerekcesi).HasColumnName("red_gerekcesi").HasMaxLength(500);
            b.Property(x => x.OlusturulmaZamani).HasColumnName("olusturulma_zamani").HasColumnType("timestamptz");
            // Domain events are in-memory only; never persisted.
            b.Ignore(x => x.Olaylar);
        });

        modelBuilder.Entity<OutboxKaydi>(b =>
        {
            b.ToTable("outbox");
            b.HasKey(x => x.Id);
            b.Property(x => x.Id).HasColumnName("id");
            b.Property(x => x.OlayTipi).HasColumnName("olay_tipi").HasMaxLength(100).IsRequired();
            b.Property(x => x.Anahtar).HasColumnName("anahtar").HasMaxLength(100).IsRequired();
            b.Property(x => x.Icerik).HasColumnName("icerik").HasColumnType("jsonb").IsRequired();
            b.Property(x => x.OlusmaZamani).HasColumnName("olusma_zamani").HasColumnType("timestamptz");
            b.Property(x => x.YayinlandiMi).HasColumnName("yayinlandi_mi");
            b.Property(x => x.YayinlanmaZamani).HasColumnName("yayinlanma_zamani").HasColumnType("timestamptz");
            b.HasIndex(x => x.YayinlandiMi).HasDatabaseName("ix_outbox_yayinlandi_mi");
        });

        modelBuilder.Entity<DenetimKaydi>(b =>
        {
            b.ToTable("denetim_log");
            b.HasKey(x => x.Id);
            b.Property(x => x.Id).HasColumnName("id").ValueGeneratedOnAdd();
            b.Property(x => x.Aktor).HasColumnName("aktor").HasMaxLength(150).IsRequired();
            b.Property(x => x.IpAdresi).HasColumnName("ip_adresi").HasMaxLength(64).IsRequired();
            b.Property(x => x.Eylem).HasColumnName("eylem").HasMaxLength(100).IsRequired();
            b.Property(x => x.Detay).HasColumnName("detay").HasMaxLength(1000);
            b.Property(x => x.Zaman).HasColumnName("zaman").HasColumnType("timestamptz");
        });
    }
}

/// <summary>
/// Audit-log row (NFR-S7): who, when, from which IP, which action. Table is created now; populated by the
/// audit middleware in Slice 3. Stored in the immutable <c>hist</c> schema.
/// </summary>
public sealed class DenetimKaydi
{
    public long Id { get; set; }
    public string Aktor { get; set; } = string.Empty;
    public string IpAdresi { get; set; } = string.Empty;
    public string Eylem { get; set; } = string.Empty;
    public string? Detay { get; set; }
    public DateTimeOffset Zaman { get; set; }
}
