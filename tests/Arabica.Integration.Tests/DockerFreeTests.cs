using System.Text.Json;
using Arabica.Application.Cikti;
using Arabica.Application.Ortak;
using Arabica.Contracts.Entegrasyon;
using Arabica.Contracts.Olaylar;
using Arabica.Domain.Subeler;
using Arabica.Domain.Transferler;
using Arabica.Infrastructure.Cikti;
using Arabica.Infrastructure.Mesajlasma;
using Arabica.Infrastructure.Veri;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Arabica.Integration.Tests;

// These run WITHOUT Docker — they validate infrastructure logic that doesn't need a live DB/broker.

/// <summary>Builds the EF models (no connection needed) to validate Fluent config + constructor binding.</summary>
public sealed class EfModelTests
{
    [Fact]
    public void HistoryDbContext_modeli_gecerli_kurulur()
    {
        var opts = new DbContextOptionsBuilder<HistoryDbContext>()
            .UseNpgsql("Host=localhost;Database=x;Username=u;Password=p").Options;
        using var ctx = new HistoryDbContext(opts);

        var emir = ctx.Model.FindEntityType(typeof(TransferEmri))!;
        emir.Should().NotBeNull();
        emir.GetSchema().Should().Be("hist");
        emir.FindProperty(nameof(TransferEmri.Durum)).Should().NotBeNull();
        emir.FindNavigation(nameof(TransferEmri.Olaylar)).Should().BeNull(); // domain events ignored
        ctx.Model.FindEntityType(typeof(OutboxKaydi)).Should().NotBeNull();
        ctx.Model.FindEntityType(typeof(DenetimKaydi)).Should().NotBeNull();
    }

    [Fact]
    public void HotDbContext_modeli_gecerli_kurulur()
    {
        var opts = new DbContextOptionsBuilder<HotDbContext>()
            .UseNpgsql("Host=localhost;Database=x;Username=u;Password=p").Options;
        using var ctx = new HotDbContext(opts);

        var sube = ctx.Model.FindEntityType(typeof(Sube))!;
        sube.Should().NotBeNull();
        sube.GetSchema().Should().Be("hot");
    }
}

/// <summary>Outbox dispatcher batch logic with fakes (no bus/DB): rehydrates events and publishes them.</summary>
public sealed class OutboxGondericiTests
{
    private static readonly DateTimeOffset An = new(2026, 6, 5, 9, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task BirPartiGonder_entegrasyon_olaylarini_yayinlar_ve_isaretler()
    {
        var depo = new SahteOutboxDeposu();
        depo.Ekle(new TransferOnaylandi(1, 1, 2, "Personel", 1, An));
        depo.Ekle(new TransferReddedildi(2, 1, 2, "Personel", 1, "gerekçe", An));
        var yayinci = new SahteYayinci();
        var gonderici = new OutboxGonderici(depo, yayinci, new SabitZaman(), NullLogger<OutboxGonderici>.Instance);

        var adet = await gonderici.BirPartiGonderAsync(100, CancellationToken.None);

        adet.Should().Be(2);
        yayinci.Yayinlananlar.Should().HaveCount(2);
        yayinci.Yayinlananlar.Select(y => y.Tip).Should().Contain([typeof(TransferOnaylandi), typeof(TransferReddedildi)]);
        depo.IsaretliSayisi.Should().Be(2);
    }

    [Fact]
    public async Task BirPartiGonder_bos_kuyrukta_sifir_doner()
    {
        var gonderici = new OutboxGonderici(new SahteOutboxDeposu(), new SahteYayinci(),
            new SabitZaman(), NullLogger<OutboxGonderici>.Instance);

        (await gonderici.BirPartiGonderAsync(100, CancellationToken.None)).Should().Be(0);
    }
}

/// <summary>Pure ingest mapping (POS/PDKS → branch state).</summary>
public sealed class SubeGuncelleyiciTests
{
    private static readonly DateTimeOffset An = DateTimeOffset.UnixEpoch;

    [Fact]
    public void PosUygula_musteri_sayisini_gunceller()
    {
        var sube = new Sube(1, "Merkez", maksimumKapasite: 100, anlikMusteriSayisi: 10);
        SubeGuncelleyici.PosUygula(sube, new PosOlayi(1, SiparisAdedi: 80, ToplamTutar: 0, An));
        sube.AnlikMusteriSayisi.Should().Be(80);
    }

    [Theory]
    [InlineData("GIRIS", 6)]
    [InlineData("CIKIS", 4)]
    public void PdksUygula_aktif_personeli_gunceller(string hareket, int beklenen)
    {
        var sube = new Sube(1, "Merkez", maksimumKapasite: 100, aktifPersonelSayisi: 5);
        SubeGuncelleyici.PdksUygula(sube, new PdksOlayi(1, PersonelId: 7, Hareket: hareket, An));
        sube.AktifPersonelSayisi.Should().Be(beklenen);
    }
}

// ---- shared fakes ----

internal sealed class SahteOutboxDeposu : IOutboxDeposu
{
    private static readonly JsonSerializerOptions O = new(JsonSerializerDefaults.Web);
    public List<OutboxKaydi> Kayitlar { get; } = [];
    public int IsaretliSayisi { get; private set; }

    public void Ekle(object olay)
        => Kayitlar.Add(new OutboxKaydi
        {
            Id = Guid.NewGuid(),
            OlayTipi = olay.GetType().Name,
            Anahtar = "1",
            Icerik = JsonSerializer.Serialize(olay, olay.GetType(), O),
            OlusmaZamani = DateTimeOffset.UnixEpoch,
            YayinlandiMi = false
        });

    public Task<IReadOnlyList<OutboxKaydi>> YayinlanmamislariGetirAsync(int enFazla, CancellationToken ct)
        => Task.FromResult<IReadOnlyList<OutboxKaydi>>(Kayitlar.Where(k => !k.YayinlandiMi).Take(enFazla).ToList());

    public Task YayinlandiOlarakIsaretleAsync(IReadOnlyList<Guid> idler, DateTimeOffset an, CancellationToken ct)
    {
        foreach (var k in Kayitlar.Where(k => idler.Contains(k.Id)))
        {
            k.YayinlandiMi = true;
            IsaretliSayisi++;
        }
        return Task.CompletedTask;
    }
}

internal sealed class SahteYayinci : IEntegrasyonYayinci
{
    public List<(object Olay, Type Tip)> Yayinlananlar { get; } = [];

    public Task YayinlaAsync(object olay, Type olayTipi, CancellationToken ct)
    {
        Yayinlananlar.Add((olay, olayTipi));
        return Task.CompletedTask;
    }
}

internal sealed class SabitZaman : IZamanSaglayici
{
    public DateTimeOffset Simdi => new(2026, 6, 5, 9, 0, 0, TimeSpan.Zero);
}
