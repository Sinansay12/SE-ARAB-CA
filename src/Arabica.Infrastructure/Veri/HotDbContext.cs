using Arabica.Application.Yonetim;
using Arabica.Domain.Subeler;
using Microsoft.EntityFrameworkCore;

namespace Arabica.Infrastructure.Veri;

/// <summary>
/// "Hot" context — real-time reads/writes (branch occupancy). Default schema <c>hot</c>.
/// NFR-P4: kept separate from the historical/write context to avoid read/write lock contention; can be
/// pointed at a read-replica connection string.
/// </summary>
public sealed class HotDbContext(DbContextOptions<HotDbContext> options) : DbContext(options)
{
    public const string Sema = "hot";

    public DbSet<Sube> Subeler => Set<Sube>();
    public DbSet<PersonelKaydi> Personeller => Set<PersonelKaydi>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema(Sema);

        modelBuilder.Entity<Sube>(b =>
        {
            b.ToTable("sube");
            b.HasKey(x => x.SubeId);
            b.Property(x => x.SubeId).HasColumnName("sube_id").ValueGeneratedNever();
            b.Property(x => x.Ad).HasColumnName("ad").HasMaxLength(200).IsRequired();
            b.Property(x => x.MaksimumKapasite).HasColumnName("maksimum_kapasite");
            b.Property(x => x.AnlikMusteriSayisi).HasColumnName("anlik_musteri_sayisi");
            b.Property(x => x.AktifPersonelSayisi).HasColumnName("aktif_personel_sayisi");
            b.Property(x => x.Aktif).HasColumnName("aktif");
        });

        modelBuilder.Entity<PersonelKaydi>(b =>
        {
            b.ToTable("personel");
            b.HasKey(x => x.PersonelId);
            b.Property(x => x.PersonelId).HasColumnName("personel_id").ValueGeneratedOnAdd();
            b.Property(x => x.SubeId).HasColumnName("sube_id");
            b.Property(x => x.TakmaAd).HasColumnName("takma_ad").HasMaxLength(150).IsRequired();
            b.Property(x => x.Tip).HasColumnName("tip").HasMaxLength(20).IsRequired();
            b.Property(x => x.Aktif).HasColumnName("aktif");
        });
    }
}
