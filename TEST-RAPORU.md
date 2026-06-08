# Arabica Cafe — Test Raporu

**Proje:** Arabica Cafe Dinamik Kaynak Yönetim Sistemi
**Platform:** .NET 8 · C# 12 · xUnit · FluentAssertions · EF Core 8 (InMemory + Testcontainers) · MassTransit test harness
**Toplam test:** **95** · **Başarısız: 0** · **Atlanan: 0**
**Çalıştırma biçimi:** proje-bazlı (`dotnet test` her test projesi için ayrı)

> Bu belge, hocaya sunum/savunma için hazırlanmıştır: hangi testin neyi doğruladığı, kullanılan test teknikleri, çalıştırma çıktısı ve olası sorulara hazır cevaplar (§7 SSS) içerir.

---

## 1. Özet — Proje Bazlı Sonuçlar

| Test Projesi | Test Sayısı | Türü | Kapsadığı katman |
|--------------|:----:|------|------------------|
| `Arabica.Domain.Tests` | **37** | Birim (saf) | Etki alanı: varlıklar, durum makinesi, İş Kanunu zinciri, optimizasyon stratejileri |
| `Arabica.Application.Tests` | **12** | Birim (sahte bağımlılık) | CQRS handler'ları, MediatR pipeline (Transaction), Observer, tasarım desenleri |
| `Arabica.Api.Tests` | **33** | Entegrasyon (in-process) | HTTP uç sözleşmeleri, JWT/RBAC, MFA, durum makinesi (HTTP), yönetim uçları |
| `Arabica.Integration.Tests` | **13** | Entegrasyon (gerçek altyapı) | Gerçek PostgreSQL 16 + Kafka (Testcontainers), outbox→ESB, şema izolasyonu |
| **TOPLAM** | **95** | | |

**`dotnet test` çıktısı (proje-bazlı):**

```
Başarılı! - Başarısız: 0, Başarılı: 37, Atlanan: 0  - Arabica.Domain.Tests.dll
Başarılı! - Başarısız: 0, Başarılı: 12, Atlanan: 0  - Arabica.Application.Tests.dll
Başarılı! - Başarısız: 0, Başarılı: 33, Atlanan: 0  - Arabica.Api.Tests.dll
Başarılı! - Başarısız: 0, Başarılı: 13, Atlanan: 0  - Arabica.Integration.Tests.dll  (gerçek Postgres + Kafka)
```

---

## 2. Test Mimarisi ve Kullanılan Araçlar

**Test piramidi** uygulanmıştır: tabanda çok sayıda hızlı birim testi (Domain + Application = 49), üstte daha az ama daha kapsamlı entegrasyon testi (Api + Integration = 46).

| Araç / Teknik | Nerede | Amaç |
|---------------|--------|------|
| **xUnit** | tüm projeler | Test çatısı (`[Fact]` tekil, `[Theory]`+`[InlineData]` parametreli) |
| **FluentAssertions** | tüm projeler | Okunabilir doğrulamalar (`.Should().Be(...)`, `.Should().Throw<...>()`) |
| **EF Core InMemory** | Application + Api | Veritabanını taklit eden, izole, hızlı in-memory bağlam |
| **WebApplicationFactory** (`ApiFabrika`) | Api.Tests | Gerçek API'yi süreç-içi ayağa kaldırır; gerçek controller + auth + RBAC + MFA + pipeline çalışır |
| **Testcontainers** | Integration.Tests | Gerçek **PostgreSQL 16** ve **Kafka** konteynerlerini test sırasında başlatır |
| **MassTransit In-Memory Test Harness** | Integration.Tests | ESB'yi (iki bağımsız consumer) broker'sız doğrular |
| **Xunit.SkippableFact** | Integration.Tests | Docker yoksa testi başarısız saymak yerine **atlar** (`Skip.IfNot`) |
| **Sahte (fake) nesneler** (`Sahteler.cs`) | Application.Tests | `SahteOutbox`, `SahteTransferEmriDeposu`, `SahteTamamlayici`, `SabitZaman` — bağımlılık izolasyonu |
| **Otp.NET** | Api.Tests | Testte gerçek TOTP (MFA) kodu üretmek |

**Neden hem InMemory hem gerçek Postgres?** InMemory testleri hızlıdır ama SQL/transaction davranışını taklit etmez. Kritik **atomiklik** ve **şema izolasyonu** garantileri (outbox + personel taşıma tek transaction'da) bu yüzden `Integration.Tests`'te **gerçek PostgreSQL** üzerinde ayrıca doğrulanır.

---

## 3. Arabica.Domain.Tests (37 test) — Saf Birim Testleri

Etki alanı sınıfları hiçbir altyapıya (DB, ağ) bağlı olmadan, deterministik biçimde test edilir.

### 3.1 `SubeTests` — Şube varlığı & doluluk hesabı (14 test)
| Test | Doğrulanan |
|------|-----------|
| `DolulukOraniHesapla_dogru_yuzde_uretir` *(Theory ×5)* | Doluluk yüzdesi; %0, %100, **%130 kapasite aşımı**, 2 ondalık yuvarlama |
| `SeviyeHesapla_varsayilan_esiklerle_dogru_seviye_verir` *(Theory ×6)* | Yeşil (≤60) / Sarı (60–85) / Kırmızı (>85) sınır değerleri |
| `Sifir_veya_negatif_kapasite_reddedilir` | Geçersiz kapasite → istisna |
| `Bos_sube_adi_reddedilir` | Boş ad → istisna |
| `MusteriSayisiniGuncelle_orani_degistirir` | Anlık müşteri güncellemesi oranı değiştirir |

### 3.2 `TransferEmriTests` — Transfer durum makinesi (12 test)
| Test | Doğrulanan |
|------|-----------|
| `Yeni_emir_Bekliyor_durumunda_baslar` | Başlangıç durumu = Bekliyor |
| `Bekliyor_durumundan_Onaylandi_gecisi_gecerli` *(Theory ×2)* | Geçerli geçiş + **büyük/küçük harf duyarsız** |
| `Bekliyor_dan_Reddedildi_gerekce_ile_gecerli` | Red ancak gerekçeyle geçerli |
| `Reddetme_gerekce_olmadan_ArgumentException_firlatir` | Gerekçesiz red → hata |
| `Onaylandi_dan_Tamamlandi_gecisi_gecerli` | Onaylandı → Tamamlandı geçerli |
| `Bekliyor_dan_gecersiz_gecis_InvalidOperationException_firlatir` | Bekliyor → Tamamlandı **atlanamaz** |
| `Terminal_Reddedildi_durumundan_gecis_yapilamaz` | Terminal durumdan çıkış yok |
| `Bilinmeyen_durum_ArgumentException_firlatir` | Tanımsız durum string'i → hata |
| `Numerik_durum_degeri_de_reddedilir` | "1" gibi sayısal durum reddedilir |
| `Fabrika_kaynak_ve_hedef_ayni_ise_hata_verir` | **Factory Method**: aynı şube → hata |
| `Fabrika_ekipman_transferi_uretebilir` | Factory: ekipman tipi emir üretimi |

### 3.3 `IsKanunuTests` — İş Kanunu 4857 Sorumluluk Zinciri (Chain of Responsibility) (7 test)
| Test | Doğrulanan |
|------|-----------|
| `Gunluk_azami_asilirsa_yol_dahil_reddedilir` | Günlük azami mesai aşımı → engellenir |
| `Yol_suresi_dahil_sinir_asilmiyorsa_uygundur` | Sınır aşılmıyorsa uygun |
| `Yol_suresinin_mesaiye_dahil_oldugu_ispatlanir` | **Yol süresi mesaiye dahildir** kuralı |
| `Haftalik_azami_asilirsa_reddedilir` | Haftalık azami aşımı → engellenir |
| `Mola_hak_edilmisse_reddedilir` | Yasal ara dinlenmesi hakkı → engellenir |
| `Tum_kurallar_gecince_degerlendirici_uygun_doner` | Tüm halkalar geçerse → uygun |
| `Degerlendirici_ilk_ihlali_gerekcesiyle_dondurur` | Zincir ilk ihlalde durur, gerekçe döner |

### 3.4 `OptimizasyonMotoruTests` — Strateji deseni & öneri üretimi (4 test)
| Test | Doğrulanan |
|------|-----------|
| `VizeFinal_stratejisi_yaz_dan_daha_dusuk_doluluk_ta_darbogaz_ilan_eder` | **Strategy**: sezona göre farklı eşik |
| `DarbogazTespitiYap_tum_subeleri_siniflandirir` | Darboğaz/atıl sınıflandırması |
| `TransferOnerisiUret_uygun_aday_varsa_Bekliyor_emri_dondurur` | Uygun aday → Bekliyor emri |
| `TransferOnerisiUret_tum_adaylar_is_kanunu_na_takilirsa_reddeder` | Tüm adaylar İş Kanunu'na takılırsa öneri yok |

---

## 4. Arabica.Application.Tests (12 test) — Sahte Bağımlılıklı Birim Testleri

Uygulama katmanı (CQRS handler'ları, pipeline, desenler) sahte (fake) portlarla izole test edilir.

### 4.1 `TransferIslemiUygulaCommandHandlerTests` — CQRS komut yönlendirmesi (6 test)
| Test | Doğrulanan |
|------|-----------|
| `Onaylama_tamamlayiciya_delege_eder_ve_Tamamlandi_doner` | ONAYLA → tamamlayıcıya delege, sonuç Tamamlandı |
| `Onaylama_yetersiz_personel_InvalidOperationException_firlatir` | Yetersiz personel → istisna (409'a dönüşür) |
| `Reddetme_TransferReddedildi_olayini_gerekceyle_yazar` | REDDET → outbox'a olay + gerekçe |
| `Gecersiz_gecis_firlatir_ve_outbox_a_yazmaz` | Geçersiz geçiş → istisna, **outbox temiz** |
| `Bilinmeyen_durum_firlatir_ve_outbox_a_yazmaz` | Tanımsız aksiyon → hata, outbox temiz |
| `Bulunamayan_emir_Bulunamadi_doner` | Olmayan emir → Bulunamadı |

### 4.2 `TransactionBehaviorTests` — MediatR pipeline (Decorator deseni) (2 test)
| Test | Doğrulanan |
|------|-----------|
| `Basarili_handler_da_tam_bir_kez_commit_eder` | Başarılı komut → **tam bir kez** commit |
| `Handler_firlatirsa_commit_etmez` | Handler hata atarsa → commit yok (atomiklik) |

### 4.3 `ObserverTests` — Observer deseni (1 test)
| Test | Doğrulanan |
|------|-----------|
| `Bildirim_iki_aboneye_birden_dagitilir` | Tek olay → birden çok aboneye dağıtım (MediatR notification) |

### 4.4 `PatternTests` — Tasarım desenleri (3 test)
| Test | Doğrulanan |
|------|-----------|
| `TransferOlayFabrikasi_duruma_gore_dogru_olayi_uretir` | **Factory**: duruma göre doğru entegrasyon olayı |
| `TransferOlayFabrikasi_reddetmede_gerekceyi_tasir` | Red olayında gerekçe taşınır |
| `KapasiteRaporuBuilder_agregalari_dogru_hesaplar` | **Builder**: rapor agregaları doğru hesaplanır |

---

## 5. Arabica.Api.Tests (33 test) — HTTP Entegrasyon Testleri

`ApiFabrika` (WebApplicationFactory) ile gerçek API süreç-içi ayağa kalkar; veritabanı izole InMemory'ye, arka plan servisleri kapalıya alınır. Gerçek controller + JWT + RBAC politikaları + MFA + pipeline çalışır.

### 5.1 `ApiSozlesmeTests` — 5 frozen uç + RBAC + MFA + durum makinesi (HTTP) (19 test)
| Test | Doğrulanan |
|------|-----------|
| `Login_gecerli_kimlikle_JWT_doner` | Giriş → JWT |
| `Login_yanlis_parola_401_doner` | Yanlış parola → 401 |
| `Doluluk_tokensiz_401_doner` | Token yoksa → 401 |
| `Doluluk_sube_muduru_icin_403_doner` | **RBAC**: tüm-şube doluluk Müdür'e kapalı (403) |
| `Doluluk_koordinator_icin_tum_subeleri_doner` | Koordinatör tüm şubeleri görür |
| `Detay_sube_muduru_baska_subeyi_goremez_403` | **Şube-kapsamlı RBAC**: başka şube → 403 |
| `Oneriler_bekleyen_transferi_listeler` | Bekleyen öneriler listelenir |
| `Islem_onayla_MFA_olmadan_401_doner` | MFA kodu yoksa onay → 401 |
| `Islem_onayla_gecerli_MFA_ile_200_ve_Tamamlandi` | Geçerli TOTP → 200, Tamamlandı |
| `Islem_zaten_onaylanmis_transferi_tekrar_onaylayinca_409` | Terminal emir tekrar onay → 409 |
| `Islem_sube_muduru_kendi_subesini_ilgilendirmeyen_transferi_onaylayamaz_403` | Müdür yetki sınırı → 403 |
| `Islem_sube_muduru_kendi_subesini_ilgilendiren_transferi_onaylar_200` | Müdür kendi şubesini onaylar → 200 |
| `Islem_koordinator_her_subedeki_transferi_onaylayabilir_200` | Koordinatör tüm transferleri onaylar |
| `Gecmis_koordinator_icin_200_ve_liste_doner` | Transfer geçmişi (CQRS sorgu) |
| `Gecmis_sube_muduru_icin_403_doner` | Geçmiş Müdür'e kapalı |
| `Onayla_personel_transferi_personel_sayilarini_tasir` | **Personel onayında kaynak −N / hedef +N** |
| `Onayla_ekipman_transferi_personel_sayilarini_degistirmez` | Ekipman onayı personeli değiştirmez |
| `Onayla_yetersiz_personel_409_ve_hicbir_degisiklik_yok` | Yetersiz personel → 409, değişiklik yok |
| `Onayla_terminal_emri_tekrar_onaylayinca_409_ve_cift_tasima_yok` | İdempotent: çift taşıma yok |

### 5.2 `AdminTests` — Yönetim uçları (Koordinatör) (14 test)
| Test | Doğrulanan |
|------|-----------|
| `SubeOlustur_koordinator_200_ve_listede_gorunur` | Şube oluşturma (CRUD) |
| `SubeOlustur_gecersiz_kapasite_400` | Doğrulama (ValidationBehavior) → 400 |
| `SubePasiflestir_dolulukdan_dusurur` | Pasifleştirme → doluluktan çıkar |
| `SubeAktiflestir_pasif_subeyi_dolulukda_geri_getirir` | Yeniden aktifleştirme → geri döner |
| `SubeAktiflestir_bilinmeyen_sube_404` | Olmayan şube → 404 |
| `Admin_uclari_sube_muduru_icin_403` | **Tüm /admin/* Müdür'e kapalı (403)** |
| `DemoSeed_koordinator_200_ve_testte_iliskisel_olmadigi_icin_noop` | Demo tohum ucu; InMemory'de güvenli no-op |
| `PersonelEkle_anonim_KVKK_PII_icermez` | **KVKK**: TC/ad-soyad/telefon yok, yalnız takma ad + ID |
| `ManuelTransfer_olusturur_ve_onerilerde_gorunur` | Manuel transfer (Factory + outbox→ESB) |
| `ManuelTransfer_kaynak_hedef_ayni_400` | Aynı kaynak/hedef → 400 |
| `OptimizasyonTetikle_darbogazdan_oneri_uretir` | Canlı optimizasyon motoru |
| `Strateji_runtime_gecersiz_kilma_yansir` | Strateji çalışma-zamanında değiştirilir |
| `DenetimLoglari_IP_ve_zaman_ile_listelenir` | Denetim logu (aktör + IP + zaman, NFR-S7) |
| `Ozet_koordinator_ve_mudur_icin_doner` | Rol-duyarlı özet (Koordinatör tümü / Müdür kendi şubesi) |

---

## 6. Arabica.Integration.Tests (13 test) — Gerçek Altyapı Testleri

Testcontainers ile **gerçek PostgreSQL 16 + Kafka** başlatılır. Docker yoksa `SkippableFact` ile atlanır (bu çalıştırmada Docker mevcut → tümü koştu).

### 6.1 `PostgresEntegrasyonTests` — Gerçek PG üzerinde atomiklik (3 test)
| Test | Doğrulanan |
|------|-----------|
| `Onayla_personel_transferi_atomik_olarak_personel_tasir` | Gerçek transaction: kaynak −2 / hedef +2 + outbox tek seferde |
| `Onayla_yetersiz_personel_atomik_olarak_hicbir_sey_degistirmez` | Yetersiz personel → geri sarma, hiçbir değişiklik yok |
| `Hot_ve_hist_semalari_ayni_veritabaninda_izole_calisir` | **Şema izolasyonu** (hot ↔ hist, NFR-P4) |

### 6.2 `OutboxDispatchEntegrasyonTests` — Transactional Outbox (1 test)
| `Outbox_kaydi_ESB_ye_yayinlanir_ve_isaretlenir` | Outbox satırı ESB'ye yayınlanır ve "yayınlandı" işaretlenir |

### 6.3 `EsbHarnessTests` — Kurumsal Servis Yolu / ESB (1 test)
| `Transfer_olayi_iki_bagimsiz_consumer_tarafindan_tuketilir` | **Bir olay → iki bağımsız consumer** (bildirim + denetim) |

### 6.4 `KafkaEntegrasyonTests` — Ham Kafka (1 test)
| `Uretici_yayinlar_tuketici_alir_round_trip` | Gerçek Kafka üzerinde üretici→tüketici gidiş-dönüş |

### 6.5 `DockerFreeTests` — Docker'sız altyapı/ingest mantığı (7 test)
| Test | Doğrulanan |
|------|-----------|
| `HistoryDbContext_modeli_gecerli_kurulur` | hist şema EF modeli geçerli |
| `HotDbContext_modeli_gecerli_kurulur` | hot şema EF modeli geçerli |
| `BirPartiGonder_entegrasyon_olaylarini_yayinlar_ve_isaretler` | Outbox dağıtıcı bir parti yayınlar |
| `BirPartiGonder_bos_kuyrukta_sifir_doner` | Boş kuyruk → sıfır |
| `PosUygula_musteri_sayisini_gunceller` | POS olayı anlık müşteriyi günceller |
| `PdksUygula_aktif_personeli_gunceller` *(Theory ×2)* | PDKS GİRİŞ → +, ÇIKIŞ → − |

---

## 7. Hocaya Karşı Olası Sorular ve Cevaplar (SSS)

**S: Kaç testiniz var, hangi türler?**
95 test. Birim testleri (Domain 37 + Application 12 = 49) + entegrasyon testleri (Api 33 + Integration 13 = 46). Test piramidine uygun: çok sayıda hızlı birim, daha az ama kapsamlı entegrasyon.

**S: Hangi test çatısını ve neden kullandınız?**
**xUnit** (.NET'in modern, standart test çatısı; `[Fact]`/`[Theory]` ile parametreli test). Doğrulamalar için **FluentAssertions** (okunabilirlik). İkisi de .NET ekosisteminde yaygındır.

**S: Bağımlılıkları nasıl izole ettiniz (mock)?**
Application testlerinde elle yazılmış **sahte (fake) nesneler** kullandık (`Sahteler.cs`): `SahteOutbox`, `SahteTransferEmriDeposu`, `SahteTamamlayici`, `SabitZaman`. Zamanı sabitlemek testleri **deterministik** kılar.

**S: Gerçek veritabanı olmadan nasıl test ettiniz? Peki gerçek DB?**
İki seviye: (1) Hızlı testler **EF Core InMemory** ile çalışır. (2) Kritik atomiklik/şema garantileri **Testcontainers** ile **gerçek PostgreSQL 16 + Kafka** üzerinde doğrulanır — InMemory transaction/SQL davranışını taklit etmediği için bu şarttır.

**S: API'yi nasıl test ettiniz?**
`WebApplicationFactory` tabanlı `ApiFabrika` ile API süreç-içi ayağa kaldırılır; gerçek controller, JWT doğrulama, RBAC politikaları, MFA ve MediatR pipeline çalışır. Sadece DB (InMemory) ve broker (in-memory) izole edilir.

**S: Tasarım desenlerini test ettiniz mi?**
Evet: **Factory** (TransferEmriFactory/TransferOlayFabrikasi), **Builder** (KapasiteRaporuBuilder), **Strategy** (sezon stratejileri), **State** (transfer durum makinesi — 12 test), **Chain of Responsibility** (İş Kanunu — 7 test), **Observer** (bildirim dağıtımı), **Decorator** (MediatR TransactionBehavior).

**S: Durum makinesini nasıl doğruladınız?**
`TransferEmriTests` ile: geçerli geçişler (Bekliyor→Onaylandı→Tamamlandı), geçersiz geçişlerin istisna fırlatması (Bekliyor→Tamamlandı atlanamaz), terminal durumlardan çıkış olmaması, tanımsız/sayısal durum reddi. HTTP seviyesinde de `ApiSozlesmeTests`'te 409 ile doğrulanır.

**S: Güvenlik/uyumluluk testleri var mı?**
Evet: **JWT** (token yoksa 401), **RBAC** (Müdür ↔ Koordinatör yetki sınırları, şube-kapsamı), **MFA/TOTP** (kodsuz onay 401, geçerli kodla 200), **KVKK** (personel verisinde TC/ad-soyad/telefon olmadığının doğrulanması), **denetim logu** (aktör+IP+zaman).

**S: Atomiklik (outbox + personel taşıma) nasıl kanıtlandı?**
Gerçek Postgres üzerinde `PostgresEntegrasyonTests`: onayda personel taşıma + emir tamamlama + outbox satırı **tek transaction**'da; yetersiz personelde **geri sarma** ve "hiçbir şey değişmedi" doğrulaması; `TransactionBehaviorTests` ile pipeline'ın hata durumunda commit etmediği.

**S: Testleri neden tek komutla (tüm çözüm) değil proje-bazlı çalıştırıyorsunuz?**
`Integration.Tests` Testcontainers ile gerçek Postgres+Kafka konteynerleri başlatır. Çalışan Docker yığını + Testcontainers aynı anda tüm çözümle koşturulursa test-host bellek (OOM) sorunu yaşayabilir. Bu yüzden **her proje ayrı** çalıştırılır (veya yığın durdurulup koşulur). Sonuç aynı: 95/95 yeşil.

**S: Kod kapsamı (coverage) yüzdesi nedir?**
Sayısal coverage aracı (coverlet) bu sürümde pipeline'a bağlanmamıştır; ancak tüm kritik yollar (durum makinesi tüm geçişler, İş Kanunu tüm halkalar, RBAC tüm roller, atomiklik başarı/başarısızlık yolları) testlerle kapsanmıştır. İstenirse `coverlet.collector` ile `dotnet test --collect:"XPlat Code Coverage"` eklenebilir.

---

## 8. Testleri Çalıştırma

```powershell
# Önkoşul: .NET 8 SDK (entegrasyon testleri için çalışan Docker).
# Proje-bazlı (önerilen):
dotnet test tests/Arabica.Domain.Tests/Arabica.Domain.Tests.csproj
dotnet test tests/Arabica.Application.Tests/Arabica.Application.Tests.csproj
dotnet test tests/Arabica.Api.Tests/Arabica.Api.Tests.csproj
dotnet test tests/Arabica.Integration.Tests/Arabica.Integration.Tests.csproj
```

> Not: `Arabica.Integration.Tests` Docker gerektirir; Docker yoksa `SkippableFact` ile **atlanır** (başarısız sayılmaz). Diğer üç proje Docker'sız çalışır.

---

*Bu rapor proje kök dizinindeki `PROJE-RAPORU.md` (§11 Test Sonuçları) ile tutarlıdır.*
