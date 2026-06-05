using Arabica.Application.Ortak;
using Arabica.Application.Tohumlama;
using Arabica.Application.Yonetim;
using Arabica.Domain.Subeler;
using Arabica.Domain.Transferler;
using Arabica.Infrastructure.Veri;
using Microsoft.EntityFrameworkCore;

namespace Arabica.Infrastructure.Tohumlama;

/// <summary>
/// Rich, internally-consistent demo snapshot so the dashboard / charts / reports / audit look full in the
/// demo. Branches + personnel land in <c>hot</c>; transfer history + audit log in <c>hist</c> (schema
/// isolation preserved; no DDL). The snapshot is coherent: every branch's <c>AktifPersonelSayisi</c> equals
/// the number of seeded personnel rows, transfer orders sit in valid state-machine states, and KVKK is
/// respected everywhere (personnel = handle + numeric IDs; no TC/name/phone).
/// Only runs against a relational store — the InMemory test store keeps the minimal seed the API tests rely
/// on (so the 92 tests are untouched).
/// </summary>
public sealed class DemoVeriTohumlayici(
    HotDbContext hot,
    HistoryDbContext hist,
    ITransferEmriFactory fabrika,
    IZamanSaglayici zaman) : IDemoVeriTohumlayici
{
    // Branch blueprint → deliberately a MIX of occupancy levels (Yeşil ≤60% atıl · Sarı ≤85% · Kırmızı >85%
    // darboğaz). One branch is inactive so the Aktifleştir flow stays demoable. Personel == staff count.
    private static readonly SubeTohum[] Subeler =
    [
        new(1, "Isparta Merkez",  120, 42, Personel: 6, Aktif: true,  Onek: "MRK"), // %35  Yeşil  (atıl)
        new(2, "S.D.Ü. Kampüs",   100, 96, Personel: 3, Aktif: true,  Onek: "KMP"), // %96  Kırmızı (darboğaz)
        new(3, "Isparta Çarşı",    80, 60, Personel: 4, Aktif: true,  Onek: "CRS"), // %75  Sarı
        new(4, "Gölcük Sahil",     90, 20, Personel: 3, Aktif: false, Onek: "GLC"), // %22  Yeşil  (PASİF)
        new(5, "Yalvaç Şube",      70, 66, Personel: 3, Aktif: true,  Onek: "YLV"), // %94  Kırmızı (darboğaz)
        new(6, "Eğirdir Sahil",   110, 75, Personel: 5, Aktif: true,  Onek: "EGR"), // %68  Sarı
        new(7, "Isparta Garaj",    60, 15, Personel: 3, Aktif: true,  Onek: "GRJ"), // %25  Yeşil  (atıl)
    ];

    public async Task<DemoTohumSonucu> TohumlaAsync(bool sifirla, CancellationToken ct)
    {
        // The rich snapshot uses ExecuteDelete + relational semantics; the InMemory test store must keep its
        // own minimal seed. Guard hard so the demo seeder never touches a test run.
        if (!hot.Database.IsRelational())
            return new DemoTohumSonucu(false, 0, 0, 0, 0, 0);

        if (sifirla)
        {
            // DEMO ONLY: wipe the demo tables (NOT the kimlik users — logins survive). No cross-schema FKs.
            await hist.Outbox.ExecuteDeleteAsync(ct);
            await hist.TransferEmirleri.ExecuteDeleteAsync(ct);
            await hist.DenetimKayitlari.ExecuteDeleteAsync(ct);
            await hot.Personeller.ExecuteDeleteAsync(ct);
            await hot.Subeler.ExecuteDeleteAsync(ct);
        }
        else if (await hot.Subeler.AnyAsync(ct))
        {
            // Already populated → idempotent no-op (don't duplicate on every startup).
            return new DemoTohumSonucu(false,
                await hot.Subeler.CountAsync(ct),
                await hot.Personeller.CountAsync(ct),
                await hist.TransferEmirleri.CountAsync(ct),
                await hist.TransferEmirleri.CountAsync(e => e.Durum == TransferDurumu.Bekliyor, ct),
                await hist.DenetimKayitlari.CountAsync(ct));
        }

        var personelSayisi = await SubeleriVePersoneliTohumla(ct);
        var (transferSayisi, bekleyen) = await TransferleriTohumla(ct);
        var denetimSayisi = await DenetimLogunuTohumla(ct);

        return new DemoTohumSonucu(true, Subeler.Length, personelSayisi, transferSayisi, bekleyen, denetimSayisi);
    }

    // ---- hot: branches + their baristas (count == AktifPersonelSayisi) ----
    private async Task<int> SubeleriVePersoneliTohumla(CancellationToken ct)
    {
        var personelSayisi = 0;
        foreach (var t in Subeler)
        {
            var sube = new Sube(t.SubeId, t.Ad, t.MaksimumKapasite, anlikMusteriSayisi: t.AnlikMusteri, aktifPersonelSayisi: t.Personel);
            if (!t.Aktif) sube.Pasiflestir();
            hot.Subeler.Add(sube);

            for (var i = 1; i <= t.Personel; i++)
            {
                // KVKK: anonymized handle + branch id only — no TC/name/phone. First of each branch is the shift lead.
                hot.Personeller.Add(new PersonelKaydi
                {
                    SubeId = t.SubeId,
                    TakmaAd = $"Barista-{t.Onek}-{i:00}",
                    Tip = i == 1 ? "Vardiya Amiri" : "Barista",
                    Aktif = true
                });
                personelSayisi++;
            }
        }
        await hot.SaveChangesAsync(ct);
        return personelSayisi;
    }

    // ---- hist: ~2 weeks of transfer history across all states; exactly 3 pending at the end ----
    private async Task<(int toplam, int bekleyen)> TransferleriTohumla(CancellationToken ct)
    {
        var simdi = zaman.Simdi;

        // (kaynak, hedef, tip, adet, durum, gerekçe, günÖnce) — oldest first so newer orders get higher ids
        // (the history view sorts by id desc, i.e. newest first). Flows: atıl branches → darboğaz (2, 5).
        TransferTohum[] tohumlar =
        [
            new(1, 2, KaynakTipi.Personel, 2, TransferDurumu.Tamamlandi, null, 14),
            new(7, 5, KaynakTipi.Personel, 1, TransferDurumu.Tamamlandi, null, 13),
            new(6, 2, KaynakTipi.Ekipman,  1, TransferDurumu.Tamamlandi, null, 12),
            new(1, 5, KaynakTipi.Personel, 1, TransferDurumu.Reddedildi, "Kaynak şubede vardiya açığı oluşuyor", 11),
            new(3, 2, KaynakTipi.Personel, 1, TransferDurumu.Tamamlandi, null, 10),
            new(7, 2, KaynakTipi.Ekipman,  2, TransferDurumu.Reddedildi, "Talep edilen ekipman bakımda", 9),
            new(6, 5, KaynakTipi.Personel, 2, TransferDurumu.Tamamlandi, null, 8),
            new(1, 3, KaynakTipi.Personel, 1, TransferDurumu.Reddedildi, "Hedef şube kapasitesi yeterli", 7),
            new(7, 2, KaynakTipi.Personel, 1, TransferDurumu.Tamamlandi, null, 6),
            new(6, 2, KaynakTipi.Personel, 1, TransferDurumu.Onaylandi,  null, 4),
            new(1, 5, KaynakTipi.Ekipman,  1, TransferDurumu.Onaylandi,  null, 3),
            new(3, 5, KaynakTipi.Personel, 1, TransferDurumu.Reddedildi, "İş Kanunu haftalık çalışma limiti aşılıyor", 3),
            new(7, 2, KaynakTipi.Personel, 1, TransferDurumu.Onaylandi,  null, 2),
            new(1, 2, KaynakTipi.Personel, 2, TransferDurumu.Bekliyor,   null, 1),
            new(6, 5, KaynakTipi.Personel, 1, TransferDurumu.Bekliyor,   null, 1),
            new(7, 2, KaynakTipi.Ekipman,  1, TransferDurumu.Bekliyor,   null, 0),
        ];

        var bekleyen = 0;
        foreach (var t in tohumlar)
        {
            var an = simdi.AddDays(-t.GunOnce).AddHours(-(t.GunOnce % 7));
            var emir = t.Tip == KaynakTipi.Personel
                ? fabrika.PersonelTransferiOlustur(t.Kaynak, t.Hedef, t.Adet, an)
                : fabrika.EkipmanTransferiOlustur(t.Kaynak, t.Hedef, t.Adet, an);

            switch (t.Durum)
            {
                case TransferDurumu.Onaylandi:
                    emir.DurumGuncelle("ONAYLANDI");
                    break;
                case TransferDurumu.Reddedildi:
                    emir.DurumGuncelle("REDDEDILDI", t.Gerekce);
                    break;
                case TransferDurumu.Tamamlandi:
                    emir.DurumGuncelle("ONAYLANDI");
                    emir.DurumGuncelle("TAMAMLANDI");
                    break;
                case TransferDurumu.Bekliyor:
                default:
                    bekleyen++;
                    break;
            }
            await hist.TransferEmirleri.AddAsync(emir, ct);
        }
        // Domain events raised by the transitions are in-memory only (HistoryDbContext ignores them) — so
        // seeding does NOT enqueue outbox rows / re-fire the ESB. Pure snapshot.
        await hist.SaveChangesAsync(ct);
        return (tohumlar.Length, bekleyen);
    }

    // ---- hist: a full audit trail (logins + admin/transfer actions) with actor + IP + timestamp ----
    private async Task<int> DenetimLogunuTohumla(CancellationToken ct)
    {
        var simdi = zaman.Simdi;
        const string Koord = "tunahan.basar";
        const string Mudur = "sinan.say";

        (string Aktor, string Ip, string Eylem, string Detay, int GunOnce, int SaatOnce)[] kayitlar =
        [
            (Koord, "192.168.10.21", "GIRIS:BASARILI",          "kullanıcı: tunahan.basar",            14, 1),
            (Mudur, "192.168.10.34", "GIRIS:BASARILI",          "kullanıcı: sinan.say",                14, 0),
            (Koord, "192.168.10.21", "ADMIN:OptimizasyonTetikle","darboğaz taraması: 2 öneri üretildi", 14, 0),
            (Koord, "192.168.10.21", "ADMIN:ManuelTransfer",     "emir: Merkez → Kampüs (personel ×2)", 14, 0),
            (Mudur, "192.168.10.34", "TRANSFER:Onayla",          "emir #1 onaylandı",                   13, 2),
            (Mudur, "10.0.0.5",      "GIRIS:BASARISIZ",          "kullanıcı: sinan.say",                13, 1),
            (Mudur, "192.168.10.34", "GIRIS:BASARILI",           "kullanıcı: sinan.say",                13, 0),
            (Koord, "192.168.10.21", "TRANSFER:Onayla",          "emir #2 tamamlandı",                  13, 0),
            (Koord, "172.16.4.9",    "ADMIN:StratejiAyarla",     "strateji: yaz → vize-final",          12, 3),
            (Koord, "192.168.10.21", "ADMIN:ManuelTransfer",     "emir: Eğirdir → Kampüs (ekipman ×1)", 12, 1),
            (Mudur, "192.168.10.34", "TRANSFER:Reddet",          "emir #4: vardiya açığı",              11, 2),
            (Koord, "192.168.10.21", "GIRIS:BASARILI",           "kullanıcı: tunahan.basar",            11, 0),
            (Koord, "192.168.10.21", "ADMIN:OptimizasyonTetikle","darboğaz taraması: 3 öneri üretildi", 10, 4),
            (Mudur, "192.168.10.34", "TRANSFER:Onayla",          "emir #5 tamamlandı",                  10, 1),
            (Koord, "192.168.10.21", "ADMIN:SubeOlustur",        "şube: Isparta Garaj",                  9,  5),
            (Koord, "203.0.113.7",   "GIRIS:BASARISIZ",          "kullanıcı: tunahan.basar",            9,  2),
            (Koord, "192.168.10.21", "ADMIN:ManuelTransfer",     "emir: Garaj → Kampüs (ekipman ×2)",   9,  0),
            (Mudur, "192.168.10.34", "TRANSFER:Reddet",          "emir #6: ekipman bakımda",            9,  0),
            (Koord, "192.168.10.21", "ADMIN:SubeGuncelle",       "şube #6 kapasite güncellendi",        8,  3),
            (Mudur, "192.168.10.34", "TRANSFER:Onayla",          "emir #7 tamamlandı",                  8,  0),
            (Koord, "192.168.10.21", "ADMIN:SubePasiflestir",    "şube #4 (Gölcük Sahil) pasifleştirildi", 7, 4),
            (Koord, "192.168.10.21", "GIRIS:BASARILI",           "kullanıcı: tunahan.basar",            5,  2),
            (Mudur, "192.168.10.34", "TRANSFER:Reddet",          "emir #12: İş Kanunu limiti",          3,  1),
            (Koord, "192.168.10.21", "ADMIN:OptimizasyonTetikle","darboğaz taraması: 3 öneri üretildi", 2,  3),
            (Koord, "192.168.10.21", "ADMIN:ManuelTransfer",     "emir: Garaj → Kampüs (personel ×1)",  1,  2),
            (Mudur, "192.168.10.34", "GIRIS:BASARILI",           "kullanıcı: sinan.say",                0,  1),
        ];

        foreach (var k in kayitlar)
        {
            hist.DenetimKayitlari.Add(new DenetimKaydi
            {
                Aktor = k.Aktor,
                IpAdresi = k.Ip,
                Eylem = k.Eylem,
                Detay = k.Detay,
                Zaman = simdi.AddDays(-k.GunOnce).AddHours(-k.SaatOnce)
            });
        }
        await hist.SaveChangesAsync(ct);
        return kayitlar.Length;
    }

    private readonly record struct SubeTohum(
        int SubeId, string Ad, int MaksimumKapasite, int AnlikMusteri, int Personel, bool Aktif, string Onek);

    private readonly record struct TransferTohum(
        int Kaynak, int Hedef, KaynakTipi Tip, int Adet, TransferDurumu Durum, string? Gerekce, int GunOnce);
}
