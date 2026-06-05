using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Arabica.Application.Tohumlama;
using Arabica.Contracts.Api;
using Arabica.Infrastructure.Kimlik;
using FluentAssertions;
using Xunit;

namespace Arabica.Api.Tests;

/// <summary>Admin/management endpoint tests (additive): happy path, 403 for managers, 400 validation, KVKK no-PII.</summary>
public sealed class AdminTests
{
    private static async Task<HttpClient> KoordinatorAsync(ApiFabrika f)
    {
        var c = f.CreateClient();
        var r = await c.PostAsJsonAsync("/api/v1/auth/login", new GirisIstegi(DemoVeriler.KoordinatorKullanici, DemoVeriler.Parola));
        var b = await r.Content.ReadFromJsonAsync<GirisYaniti>();
        c.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", b!.Token);
        return c;
    }

    private static async Task<HttpClient> MudurAsync(ApiFabrika f)
    {
        var c = f.CreateClient();
        var r = await c.PostAsJsonAsync("/api/v1/auth/login", new GirisIstegi(DemoVeriler.MudurKullanici, DemoVeriler.Parola));
        var b = await r.Content.ReadFromJsonAsync<GirisYaniti>();
        c.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", b!.Token);
        return c;
    }

    // ---- Şube CRUD ----
    [Fact]
    public async Task SubeOlustur_koordinator_200_ve_listede_gorunur()
    {
        using var f = new ApiFabrika();
        var c = await KoordinatorAsync(f);

        var r = await c.PostAsJsonAsync("/api/v1/admin/sube", new SubeOlusturIstegi("Yeni Şube Gölcük", 80, 3));
        r.StatusCode.Should().Be(HttpStatusCode.OK);
        var yeni = await r.Content.ReadFromJsonAsync<SubeYonetimYaniti>();
        yeni!.Ad.Should().Be("Yeni Şube Gölcük");
        yeni.Aktif.Should().BeTrue();

        var liste = await c.GetFromJsonAsync<List<SubeYonetimYaniti>>("/api/v1/admin/sube");
        liste!.Should().Contain(s => s.Ad == "Yeni Şube Gölcük");
    }

    [Fact]
    public async Task SubeOlustur_gecersiz_kapasite_400()
    {
        using var f = new ApiFabrika();
        var c = await KoordinatorAsync(f);
        (await c.PostAsJsonAsync("/api/v1/admin/sube", new SubeOlusturIstegi("X", 0, 0))).StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task SubePasiflestir_dolulukdan_dusurur()
    {
        using var f = new ApiFabrika();
        var c = await KoordinatorAsync(f);
        var oncekiDoluluk = await c.GetFromJsonAsync<List<SubeDolulukYaniti>>("/api/v1/sube/doluluk");

        (await c.PatchAsync("/api/v1/admin/sube/2/pasiflestir", null)).StatusCode.Should().Be(HttpStatusCode.OK);

        var sonra = await c.GetFromJsonAsync<List<SubeDolulukYaniti>>("/api/v1/sube/doluluk");
        sonra!.Count.Should().Be(oncekiDoluluk!.Count - 1); // pasif şube doluluktan çıkar
        sonra.Should().NotContain(s => s.SubeId == 2);
    }

    [Fact]
    public async Task SubeAktiflestir_pasif_subeyi_dolulukda_geri_getirir()
    {
        using var f = new ApiFabrika();
        var c = await KoordinatorAsync(f);

        // önce pasifleştir → doluluk/optimizasyon dışında kalır
        (await c.PatchAsync("/api/v1/admin/sube/2/pasiflestir", null)).StatusCode.Should().Be(HttpStatusCode.OK);
        (await c.GetFromJsonAsync<List<SubeDolulukYaniti>>("/api/v1/sube/doluluk"))!.Should().NotContain(s => s.SubeId == 2);

        // aktifleştir → 200, aktif=true
        var r = await c.PatchAsync("/api/v1/admin/sube/2/aktiflestir", null);
        r.StatusCode.Should().Be(HttpStatusCode.OK);
        (await r.Content.ReadFromJsonAsync<SubeYonetimYaniti>())!.Aktif.Should().BeTrue();

        // AktifleriGetirAsync yine içerir → doluluk + optimizasyona geri döner
        (await c.GetFromJsonAsync<List<SubeDolulukYaniti>>("/api/v1/sube/doluluk"))!.Should().Contain(s => s.SubeId == 2);
    }

    [Fact]
    public async Task SubeAktiflestir_bilinmeyen_sube_404()
    {
        using var f = new ApiFabrika();
        var c = await KoordinatorAsync(f);
        (await c.PatchAsync("/api/v1/admin/sube/9999/aktiflestir", null)).StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Admin_uclari_sube_muduru_icin_403()
    {
        using var f = new ApiFabrika();
        var c = await MudurAsync(f);
        (await c.PostAsJsonAsync("/api/v1/admin/sube", new SubeOlusturIstegi("X", 10, 0))).StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await c.GetAsync("/api/v1/admin/denetim")).StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await c.PostAsync("/api/v1/admin/optimizasyon/tetikle", null)).StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await c.PatchAsync("/api/v1/admin/sube/1/aktiflestir", null)).StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await c.PostAsync("/api/v1/admin/seed", null)).StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task DemoSeed_koordinator_200_ve_testte_iliskisel_olmadigi_icin_noop()
    {
        using var f = new ApiFabrika();
        var c = await KoordinatorAsync(f);

        var r = await c.PostAsync("/api/v1/admin/seed", null);
        r.StatusCode.Should().Be(HttpStatusCode.OK);

        // Zengin tohum yalnızca ilişkisel (Postgres) store'da çalışır; testler EF InMemory → güvenli no-op,
        // mevcut minimal tohum (ve 92 test) değişmez.
        (await r.Content.ReadFromJsonAsync<DemoTohumSonucu>())!.Tohumlandi.Should().BeFalse();
    }

    // ---- Personel (KVKK) ----
    [Fact]
    public async Task PersonelEkle_anonim_KVKK_PII_icermez()
    {
        using var f = new ApiFabrika();
        var c = await KoordinatorAsync(f);

        var r = await c.PostAsJsonAsync("/api/v1/admin/personel", new PersonelEkleIstegi(1, "Barista-A1", "Barista"));
        r.StatusCode.Should().Be(HttpStatusCode.OK);

        var ham = await r.Content.ReadAsStringAsync();
        ham.ToLowerInvariant().Should().NotContainAny("\"tc", "adsoyad", "ad_soyad", "telefon", "phone"); // KVKK: PII yok
        var yanit = await r.Content.ReadFromJsonAsync<PersonelYaniti>();
        yanit!.TakmaAd.Should().Be("Barista-A1");
        yanit.PersonelId.Should().BeGreaterThan(0);

        // Şube detayında yalnız anonim ID + takma ad görünür
        var detay = await c.GetFromJsonAsync<SubeDetayYaniti>("/api/v1/sube/1/detay");
        detay!.Personeller.Should().Contain(p => p.TakmaAd == "Barista-A1");
    }

    // ---- Manuel transfer (Factory Method + outbox) ----
    [Fact]
    public async Task ManuelTransfer_olusturur_ve_onerilerde_gorunur()
    {
        using var f = new ApiFabrika();
        var c = await KoordinatorAsync(f);

        var r = await c.PostAsJsonAsync("/api/v1/admin/transfer/manuel", new ManuelTransferIstegi(2, 1, 1, "Personel"));
        r.StatusCode.Should().Be(HttpStatusCode.OK);
        var emir = await r.Content.ReadFromJsonAsync<TransferOneriYaniti>();
        emir!.Durum.Should().Be("Bekliyor");

        var oneriler = await c.GetFromJsonAsync<List<TransferOneriYaniti>>("/api/v1/transfer/oneriler");
        oneriler!.Should().Contain(o => o.TransferId == emir.TransferId);
    }

    [Fact]
    public async Task ManuelTransfer_kaynak_hedef_ayni_400()
    {
        using var f = new ApiFabrika();
        var c = await KoordinatorAsync(f);
        (await c.PostAsJsonAsync("/api/v1/admin/transfer/manuel", new ManuelTransferIstegi(1, 1, 1, "Personel")))
            .StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    // ---- Optimizasyon + Strateji (live Strategy) ----
    [Fact]
    public async Task OptimizasyonTetikle_darbogazdan_oneri_uretir()
    {
        using var f = new ApiFabrika();
        var c = await KoordinatorAsync(f); // seed: 1=%25 (atıl), 2=%95 (darboğaz)

        var r = await c.PostAsync("/api/v1/admin/optimizasyon/tetikle", null);
        r.StatusCode.Should().Be(HttpStatusCode.OK);
        var oneriler = await r.Content.ReadFromJsonAsync<List<TransferOneriYaniti>>();
        oneriler!.Should().NotBeEmpty();
        oneriler.Should().Contain(o => o.Durum == "Bekliyor");
    }

    [Fact]
    public async Task Strateji_runtime_gecersiz_kilma_yansir()
    {
        using var f = new ApiFabrika();
        var c = await KoordinatorAsync(f);

        (await (await c.PostAsync("/api/v1/admin/strateji?ad=yaz", null)).Content.ReadFromJsonAsync<StratejiYaniti>())!
            .AktifSezon.Should().Be("yaz");
        (await c.GetFromJsonAsync<StratejiYaniti>("/api/v1/admin/strateji"))!.AktifSezon.Should().Be("yaz");
        (await c.PostAsync("/api/v1/admin/strateji?ad=gecersiz", null)).StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    // ---- Denetim + Özet ----
    [Fact]
    public async Task DenetimLoglari_IP_ve_zaman_ile_listelenir()
    {
        using var f = new ApiFabrika();
        var c = await KoordinatorAsync(f);
        await c.PostAsJsonAsync("/api/v1/admin/sube", new SubeOlusturIstegi("Log Tetik", 50, 1)); // bir denetim kaydı üret

        var loglar = await c.GetFromJsonAsync<List<DenetimKaydiYaniti>>("/api/v1/admin/denetim?sayfa=1&boyut=20");
        loglar!.Should().NotBeEmpty();
        loglar.Should().Contain(l => l.Eylem.StartsWith("ADMIN:") && !string.IsNullOrEmpty(l.IpAdresi));
    }

    [Fact]
    public async Task Ozet_koordinator_ve_mudur_icin_doner()
    {
        using var f = new ApiFabrika();
        var ck = await KoordinatorAsync(f);
        var ozetK = await ck.GetFromJsonAsync<OzetYaniti>("/api/v1/ozet");
        ozetK!.SubeSayisi.Should().BeGreaterThanOrEqualTo(2);
        ozetK.DarbogazSube.Should().BeGreaterThanOrEqualTo(1); // Kampüs %95

        var cm = await MudurAsync(f);
        var ozetM = await cm.GetFromJsonAsync<OzetYaniti>("/api/v1/ozet");
        ozetM!.SubeSayisi.Should().Be(1); // yalnız kendi şubesi
    }
}
