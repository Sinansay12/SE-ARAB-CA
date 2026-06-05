using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Arabica.Contracts.Api;
using Arabica.Domain.Transferler;
using Arabica.Infrastructure.Kimlik;
using Arabica.Infrastructure.Veri;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using OtpNet;
using Xunit;

namespace Arabica.Api.Tests;

/// <summary>
/// API contract tests over the real pipeline (WebApplicationFactory). Verifies the 5 frozen endpoints,
/// JWT auth, policy-based RBAC, TOTP MFA on approval, and the TransferEmri state machine via HTTP.
/// </summary>
public sealed class ApiSozlesmeTests
{
    private static string MfaKodu() => new Totp(Base32Encoding.ToBytes(DemoVeriler.MfaSecret)).ComputeTotp();

    private static async Task<string> TokenAlAsync(HttpClient c, string kullanici)
    {
        var yanit = await c.PostAsJsonAsync("/api/v1/auth/login",
            new GirisIstegi(kullanici, DemoVeriler.Parola));
        yanit.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await yanit.Content.ReadFromJsonAsync<GirisYaniti>();
        return body!.Token;
    }

    private static void Yetkilendir(HttpClient c, string token)
        => c.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

    // Seeds a pending transfer between the given branches directly (bypassing the engine) and returns its id.
    private static long TransferTohumla(ApiFabrika fabrika, int kaynak, int hedef)
    {
        using var scope = fabrika.Services.CreateScope();
        var ctx = scope.ServiceProvider.GetRequiredService<HistoryDbContext>();
        var fk = scope.ServiceProvider.GetRequiredService<ITransferEmriFactory>();
        var emir = fk.PersonelTransferiOlustur(kaynak, hedef, 1, new DateTimeOffset(2026, 6, 5, 9, 0, 0, TimeSpan.Zero));
        ctx.TransferEmirleri.Add(emir);
        ctx.SaveChanges();
        return emir.EmirId;
    }

    private static HttpRequestMessage OnayIstegi(long transferId)
    {
        var m = new HttpRequestMessage(HttpMethod.Post, "/api/v1/transfer/islem")
        {
            Content = JsonContent.Create(new TransferIslemIstegi(transferId, "ONAYLA"))
        };
        m.Headers.Add("X-MFA-Code", MfaKodu());
        return m;
    }

    [Fact]
    public async Task Login_gecerli_kimlikle_JWT_doner()
    {
        using var fabrika = new ApiFabrika();
        using var c = fabrika.CreateClient();

        var yanit = await c.PostAsJsonAsync("/api/v1/auth/login",
            new GirisIstegi(DemoVeriler.KoordinatorKullanici, DemoVeriler.Parola));

        yanit.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await yanit.Content.ReadFromJsonAsync<GirisYaniti>();
        body!.Token.Should().NotBeNullOrWhiteSpace();
        body.Rol.Should().Be("BolgeKoordinatoru");
    }

    [Fact]
    public async Task Login_yanlis_parola_401_doner()
    {
        using var fabrika = new ApiFabrika();
        using var c = fabrika.CreateClient();

        var yanit = await c.PostAsJsonAsync("/api/v1/auth/login",
            new GirisIstegi(DemoVeriler.KoordinatorKullanici, "yanlis-parola"));

        yanit.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Doluluk_tokensiz_401_doner()
    {
        using var fabrika = new ApiFabrika();
        using var c = fabrika.CreateClient();

        (await c.GetAsync("/api/v1/sube/doluluk")).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Doluluk_sube_muduru_icin_403_doner()
    {
        using var fabrika = new ApiFabrika();
        using var c = fabrika.CreateClient();
        Yetkilendir(c, await TokenAlAsync(c, DemoVeriler.MudurKullanici));

        (await c.GetAsync("/api/v1/sube/doluluk")).StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Doluluk_koordinator_icin_tum_subeleri_doner()
    {
        using var fabrika = new ApiFabrika();
        using var c = fabrika.CreateClient();
        Yetkilendir(c, await TokenAlAsync(c, DemoVeriler.KoordinatorKullanici));

        var liste = await c.GetFromJsonAsync<List<SubeDolulukYaniti>>("/api/v1/sube/doluluk");

        liste.Should().NotBeNull();
        liste!.Should().HaveCountGreaterThanOrEqualTo(2);
        liste.Should().Contain(s => s.Seviye == "Kirmizi"); // S.D.Ü. Kampüs %95
    }

    [Fact]
    public async Task Detay_sube_muduru_baska_subeyi_goremez_403()
    {
        using var fabrika = new ApiFabrika();
        using var c = fabrika.CreateClient();
        Yetkilendir(c, await TokenAlAsync(c, DemoVeriler.MudurKullanici)); // SubeId = 1

        (await c.GetAsync("/api/v1/sube/2/detay")).StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await c.GetAsync("/api/v1/sube/1/detay")).StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Oneriler_bekleyen_transferi_listeler()
    {
        using var fabrika = new ApiFabrika();
        using var c = fabrika.CreateClient();
        Yetkilendir(c, await TokenAlAsync(c, DemoVeriler.KoordinatorKullanici));

        var oneriler = await c.GetFromJsonAsync<List<TransferOneriYaniti>>("/api/v1/transfer/oneriler");

        oneriler.Should().NotBeNull();
        oneriler!.Should().ContainSingle().Which.Durum.Should().Be("Bekliyor");
    }

    [Fact]
    public async Task Islem_onayla_MFA_olmadan_401_doner()
    {
        using var fabrika = new ApiFabrika();
        using var c = fabrika.CreateClient();
        Yetkilendir(c, await TokenAlAsync(c, DemoVeriler.KoordinatorKullanici));
        var transferId = (await c.GetFromJsonAsync<List<TransferOneriYaniti>>("/api/v1/transfer/oneriler"))![0].TransferId;

        var yanit = await c.PostAsJsonAsync("/api/v1/transfer/islem", new TransferIslemIstegi(transferId, "ONAYLA"));

        yanit.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Islem_onayla_gecerli_MFA_ile_200_ve_Onaylandi()
    {
        using var fabrika = new ApiFabrika();
        using var c = fabrika.CreateClient();
        Yetkilendir(c, await TokenAlAsync(c, DemoVeriler.KoordinatorKullanici));
        var transferId = (await c.GetFromJsonAsync<List<TransferOneriYaniti>>("/api/v1/transfer/oneriler"))![0].TransferId;

        var istek = new HttpRequestMessage(HttpMethod.Post, "/api/v1/transfer/islem")
        {
            Content = JsonContent.Create(new TransferIslemIstegi(transferId, "ONAYLA"))
        };
        istek.Headers.Add("X-MFA-Code", MfaKodu());
        var yanit = await c.SendAsync(istek);

        yanit.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await yanit.Content.ReadFromJsonAsync<TransferIslemYaniti>();
        body!.Durum.Should().Be("Onaylandi");
    }

    [Fact]
    public async Task Islem_zaten_onaylanmis_transferi_tekrar_onaylayinca_409()
    {
        using var fabrika = new ApiFabrika();
        using var c = fabrika.CreateClient();
        Yetkilendir(c, await TokenAlAsync(c, DemoVeriler.KoordinatorKullanici));
        var transferId = (await c.GetFromJsonAsync<List<TransferOneriYaniti>>("/api/v1/transfer/oneriler"))![0].TransferId;

        // 1) Önce onayla (başarılı)
        var ilk = new HttpRequestMessage(HttpMethod.Post, "/api/v1/transfer/islem")
        {
            Content = JsonContent.Create(new TransferIslemIstegi(transferId, "ONAYLA"))
        };
        ilk.Headers.Add("X-MFA-Code", MfaKodu());
        (await c.SendAsync(ilk)).StatusCode.Should().Be(HttpStatusCode.OK);

        // 2) Tekrar onayla → durum makinesi geçersiz geçişi reddeder → 409
        var ikinci = new HttpRequestMessage(HttpMethod.Post, "/api/v1/transfer/islem")
        {
            Content = JsonContent.Create(new TransferIslemIstegi(transferId, "ONAYLA"))
        };
        ikinci.Headers.Add("X-MFA-Code", MfaKodu());
        (await c.SendAsync(ikinci)).StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    // ---- RBAC: branch-scoped transfer approval (SRS matrix) ----

    [Fact]
    public async Task Islem_sube_muduru_kendi_subesini_ilgilendirmeyen_transferi_onaylayamaz_403()
    {
        using var fabrika = new ApiFabrika();
        using var c = fabrika.CreateClient();
        Yetkilendir(c, await TokenAlAsync(c, DemoVeriler.MudurKullanici)); // SubeId = 1
        var transferId = TransferTohumla(fabrika, kaynak: 2, hedef: 3);    // 1 dahil değil

        (await c.SendAsync(OnayIstegi(transferId))).StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Islem_sube_muduru_kendi_subesini_ilgilendiren_transferi_onaylar_200()
    {
        using var fabrika = new ApiFabrika();
        using var c = fabrika.CreateClient();
        Yetkilendir(c, await TokenAlAsync(c, DemoVeriler.MudurKullanici)); // SubeId = 1
        var transferId = TransferTohumla(fabrika, kaynak: 1, hedef: 2);    // 1 kaynak

        var yanit = await c.SendAsync(OnayIstegi(transferId));

        yanit.StatusCode.Should().Be(HttpStatusCode.OK);
        (await yanit.Content.ReadFromJsonAsync<TransferIslemYaniti>())!.Durum.Should().Be("Onaylandi");
    }

    [Fact]
    public async Task Islem_koordinator_her_subedeki_transferi_onaylayabilir_200()
    {
        using var fabrika = new ApiFabrika();
        using var c = fabrika.CreateClient();
        Yetkilendir(c, await TokenAlAsync(c, DemoVeriler.KoordinatorKullanici)); // kısıtsız
        var transferId = TransferTohumla(fabrika, kaynak: 2, hedef: 3);          // koordinatörü ilgilendirmez ama yetkili

        (await c.SendAsync(OnayIstegi(transferId))).StatusCode.Should().Be(HttpStatusCode.OK);
    }

    // ---- NEW read-only endpoint: GET /api/v1/transfer/gecmis (Koordinatör only) ----

    [Fact]
    public async Task Gecmis_koordinator_icin_200_ve_liste_doner()
    {
        using var fabrika = new ApiFabrika();
        using var c = fabrika.CreateClient();
        Yetkilendir(c, await TokenAlAsync(c, DemoVeriler.KoordinatorKullanici));

        var liste = await c.GetFromJsonAsync<List<TransferGecmisYaniti>>("/api/v1/transfer/gecmis");

        liste.Should().NotBeNull();
        liste!.Should().Contain(t => t.Durum == "Bekliyor"); // tohumlanan bekleyen transfer
    }

    [Fact]
    public async Task Gecmis_sube_muduru_icin_403_doner()
    {
        using var fabrika = new ApiFabrika();
        using var c = fabrika.CreateClient();
        Yetkilendir(c, await TokenAlAsync(c, DemoVeriler.MudurKullanici));

        (await c.GetAsync("/api/v1/transfer/gecmis")).StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }
}
