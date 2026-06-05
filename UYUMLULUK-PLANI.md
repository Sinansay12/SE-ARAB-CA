# UYUMLULUK PLANI — Arabica Cafe Dinamik Kaynak Yönetim Sistemi
## 10 Zorunlu Kabul Kapısına Göre Yeniden Kapsamlama (Faz 0)

**Mimari karar:** Onion (Soğan) Mimarisi korunur — **mikroservise GEÇİLMEZ**. Mevcut katmanlı monolit (Domain ← Application ← Infrastructure, Api kompozisyon kökü, yan modül Contracts) Kapı #7 için doğrudan doğru yapıdır; genişletilir.
**Korunan sözleşmeler/davranışlar (S1/S2):** 5 REST sözleşmesi · `TransferEmri` durum makinesi · KVKK anonimleştirme · İş Kanunu (4857) muhafızları · transactional outbox · Hot/History şema izolasyonu · Liquibase şema otoritesi · MediatR 12.x sabiti.
**Hedef çatı:** `net8.0` · C# 12.

> Bu belge yalnızca **plandır**. Onay verilene kadar uygulama kodu yazılmayacaktır.

---

## 1. Kriter Uyumluluk Matrisi (10 Kapı)

| # | Kapı | Durum | Hedef Katman + Sınıf/Dosya | Yapılacak İş |
|---|------|-------|----------------------------|--------------|
| 1 | **Proje raporu (TR)** | ❌ Eksik | (kök) `PROJE-RAPORU.md` | S5'te akademik Türkçe rapor: Onion diyagramı, CQRS akışı, ESB topolojisi, sequence diyagramları, desen kataloğu, dolu 10-kapı tablosu, çalıştırma kılavuzu, test/kanıt, KVKK & 4857 notları. |
| 2 | **Sistem çalışıyor** | 🟡 Kısmî (derleniyor; uçtan uca çalışmıyor) | `Arabica.Api` + `docker-compose.yml` + **Demo profil** | S5'te: (a) `docker compose up` (Postgres+Kafka+Liquibase) tam yol; (b) **Docker'sız Demo profil** (SQLite + MassTransit in-memory + simüle POS/PDKS besleyici) ile bu makinede GERÇEKTEN çalıştırma + kanıt. **Risk: bu ana makinede Docker yok** — bkz. §6 Çalıştırma Stratejisi. |
| 3 | **OOP + SOLID** | 🟢 Büyük ölçüde var | Domain: `Personel`/`Barista`/`SubeMuduru`, `Sube`, `TransferEmri` | Mevcut: kalıtım/çok biçimlilik (`Personel` soyut → türevler), kapsülleme (private set + davranış), soyutlama (her sınırda arayüz, DIP). S3'te CQRS arayüzleriyle DIP güçlenir; raporda açıkça belgelenir. |
| 4 | **≥1 Yaratımsal desen** | 🟡 1/1 var (marj yok) | `ITransferEmriFactory` (Domain) | **Factory Method** mevcut. Marj için **Builder** eklenir: `KapasiteRaporuBuilder` (rapor montajı) — Application/Raporlama. → **2 yaratımsal**. |
| 5 | **≥2 Yapısal desen** | ❌ Eksik | Infrastructure/Application | **Adapter** (`SignalRDashboardNotifier`, `KafkaOlayAdaptoru`), **Decorator** (MediatR `IPipelineBehavior` zinciri), **Facade** (`KaynakYonetimFasadi`). → **3 yapısal**. |
| 6 | **≥2 Davranışsal desen** | 🟡 2 var ama biri örtük | Domain/Application/Infrastructure | **Strategy** (var: `IOptimizasyonServisi` + keyed DI), **State** (var: `TransferEmri` durum makinesi), **Chain of Responsibility** (İş Kanunu muhafız zinciri — *refactor*), **Observer** (Kafka→MediatR notification fan-out — *yeni*). → **4 davranışsal** + **Mediator** (MediatR, bonus). |
| 7 | **Onion Mimarisi** | 🟢 Var | Tüm çözüm | Bağımlılıklar içe dönük; `Arabica.Api` tek kompozisyon kökü. Korunur, raporda diyagramlanır. |
| 8 | **ESB** | ❌ Eksik | `Arabica.Infrastructure/Esb` + Contracts | **MassTransit** servis veri yolu eklenir; üretimde **Kafka Rider**, demo/testte **in-memory**. Integration-event'ler (transfer onaylandı/başlatıldı/tamamlandı, denetim) pub/sub + consumer'larla yol üzerinden akar. Ham POS/PDKS ingest **ESB'ye girmez** (o gerçek-zaman akışıdır). |
| 9 | **CQRS** | ❌ Eksik | `Arabica.Application` | Komut/sorgu ayrımı: `TransferIslemiUygulaCommand` (+handler, yazım=`HistoryDbContext`+outbox); `SubeDolulukQuery`/`SubeDetayQuery`/`BekleyenTransferOnerileriQuery`/`GirisYapCommand` (okuma=`HotDbContext`/projeksiyon). Yapısal CQRS — kozmetik değil. |
| 10 | **Gerçek-zamanlı iletişim** | ❌ Eksik | `Arabica.Api/RealTime` + `web/` | **SignalR** hub: canlı doluluk + transfer bildirimi (1 Hz / değişimde), Kafka akışıyla beslenir; `@microsoft/signalr` JS istemcisi. |

**Özet:** Kapı 3 ve 7 hazır; 1, 2, 4(marj), 5, 6(CoR+Observer), 8, 9, 10 için iş var.

---

## 2. Tasarım Deseni Kataloğu (minimumları açıkça aşar: 2 / 3 / 4)

| Kategori | Desen | Uygulayan tip(ler) | Dosya (hedef) | Gerekçe (tek satır) |
|----------|-------|--------------------|---------------|---------------------|
| Yaratımsal | **Factory Method** | `ITransferEmriFactory` / `TransferEmriFactory` | `src/Arabica.Domain/Transferler/Transferler.cs` *(var)* | Personel/Ekipman transfer emri yaratımını merkezîleştirir; istemci somut tipten ayrışır. |
| Yaratımsal | **Builder** | `KapasiteRaporuBuilder` | `src/Arabica.Application/Raporlama/KapasiteRaporuBuilder.cs` *(yeni)* | Karmaşık kapasite/transfer-geçmişi raporunu adım adım (fluent) kurar. |
| Yapısal | **Adapter** | `SignalRDashboardNotifier` (`IDashboardNotifier`→`IHubContext`); `KafkaOlayAdaptoru` (ham mesaj↔olay) | `src/Arabica.Infrastructure/RealTime/`, `.../Mesajlasma/` *(yeni)* | Port'u SignalR'a / ham Kafka'yı domain-olayına uyarlar. |
| Yapısal | **Decorator** | `ValidationBehavior`, `LoggingBehavior`, `TransactionBehavior`, `DenetimBehavior` (`IPipelineBehavior<,>`) | `src/Arabica.Application/Davranislar/` *(yeni)* | Her handler'ı kesişen ilgilerle (doğrulama/log/transaction/denetim) sarar. |
| Yapısal | **Facade** | `IKaynakYonetimFasadi` / `KaynakYonetimFasadi` | `src/Arabica.Application/Fasad/` *(yeni)* | Optimizasyon + transfer + doluluk alt sistemlerini tek sade arayüzde toplar. |
| Davranışsal | **Strategy** | `IOptimizasyonServisi` → `VizeFinalSezonStratejisi`/`YazDonemiStratejisi` + `IOptimizasyonStratejiResolver` | `src/Arabica.Domain/Optimizasyon/Optimizasyon.cs` *(var)* + resolver *(yeni)* | Sezona göre çalışma-zamanı algoritma değişimi (keyed DI). |
| Davranışsal | **State** | `TransferEmri` (izinli geçiş tablosu + korumalı `DurumGuncelle`) | `src/Arabica.Domain/Transferler/Transferler.cs` *(var)* | Emir yaşam döngüsü durum makinesi; geçersiz geçiş engellenir. |
| Davranışsal | **Chain of Responsibility** | `IsKanunuHalkasi` zinciri (her `IIsKanunuKurali` bir halka) | `src/Arabica.Domain/IsHukuku/IsHukuku.cs` *(refactor)* | Her muhafız ya geçirir ya engeller; ilk ihlal zinciri keser (4857). |
| Davranışsal | **Observer** | `SubeDurumuDegistiNotification` + `INotificationHandler`'lar (dashboard/engine/denetim) | `src/Arabica.Application/Olaylar/`, Infra consumer *(yeni)* | Kafka olayını çok sayıda aboneye gevşek bağlı dağıtır. |
| Davranışsal (bonus) | **Mediator** | MediatR `ISender`/`IPublisher` | Application geneli | Komut/sorgu/notification dağıtımı; controller↔handler ayrışması. |

---

## 3. Refactor Listesi (mevcut S1/S2 dosyaları)

> Kural: aşağıdaki değişikliklerin **hiçbiri** 5 sözleşmeyi, durum makinesini, KVKK'yı, İş Kanunu semantiğini veya outbox atomikliğini bozmaz. Davranış birebir korunur; yalnızca yapı/yerleşim değişir.

| Dosya | Değişiklik (tek satır) |
|-------|------------------------|
| `src/Arabica.Application/Arabica.Application.csproj` | NuGet ekle: `MediatR` 12.x, `FluentValidation(.DependencyInjectionExtensions)` 11.x, `MassTransit` 8.x soyutlamaları. |
| `src/Arabica.Application/Transferler/TransferDurumServisi.cs` | `TransferIslemiUygulaCommand` + handler'a dönüştürülür; `SaveChanges` artık `TransactionBehavior`'da (outbox atomikliği aynen korunur). |
| `src/Arabica.Application/Kurulum/ApplicationKurulum.cs` | MediatR + pipeline behavior'lar + FluentValidation validator'ları + (Strateji resolver, Facade) kaydı. |
| `src/Arabica.Domain/IsHukuku/IsHukuku.cs` | `IsKanunuDegerlendirici` döngüsü → açık **Chain of Responsibility** (halka + successor). Semantik aynı; S1 `IsKanunuTests` yeşil kalır. |
| `src/Arabica.Infrastructure/Mesajlasma/KafkaTuketiciServisi.cs` | `Sube` güncellemesinden sonra **Observer** notification yayınlar (dashboard/engine/denetim fan-out). |
| `src/Arabica.Infrastructure/Cikti/OutboxGonderici.cs` | Transfer bildirimi **MassTransit ESB** üzerinden publish edilir (ham topic yerine integration-event); at-least-once korunur. |
| `src/Arabica.Infrastructure/Kurulum/InfrastructureKurulum.cs` | `AddMassTransit` (Kafka rider / in-memory), `IDashboardNotifier`→`SignalRDashboardNotifier`, engine/resolver/facade kaydı. |
| `src/Arabica.Contracts/Olaylar.cs` | **Eklemeli**: `TransferOnaylandi/Baslatildi/Tamamlandi` ve `DenetimKaydi` integration-event record'ları (mevcutlar değişmez). |
| `tests/Arabica.Application.Tests/*` | `TransferDurumServisiTests` → komut handler testine taşınır (assert'ler aynı: geçersiz geçiş ⇒ ne outbox ne commit). |

**Kırılmadığı doğrulanan invariantlar:** 5 REST JSON sözleşmesi (controller'lar `Contracts` record'larını üretir) · `TransferEmri` durum makinesi · KVKK (event'lerde yalnız sayısal kimlik) · 4857 muhafız semantiği (CoR birebir) · outbox tek-`SaveChanges` atomikliği (TransactionBehavior) · Hot/History izolasyonu · Liquibase otoritesi · MediatR 12.x sabiti.

---

## 4. ESB & CQRS Kararları + Yeni NuGet

### 4.1 ESB = MassTransit (gerçek-zaman akışından AYRIDIR)
- **Servis veri yolu:** MassTransit **8.x** (Apache-2.0, ücretsiz — v9 ticari olacağından **8.x'e sabitlenir**; MediatR/Moq/FluentAssertions ile aynı lisans dikkati).
- **Taşıma kararı:** **Üretim = Kafka Rider** (mevcut Kafka broker'ı üzerinde sürer); **Demo/Test = in-memory** (Docker'sız çalışır). `appsettings` bayrağıyla seçilir.
- **Yol üzerindeki integration-event'ler:** `TransferOnaylandi`, `TransferBaslatildi`, `TransferTamamlandi`, `DenetimKaydi`. **Consumer'lar:** `TransferBildirimConsumer` (→ SignalR push), `DenetimConsumer` (→ `denetim_log`). Outbox dispatcher bu event'leri yola publish eder (transactional outbox + ESB birlikte → ıraksama yok).
- **Net ayrım (raporda vurgulanır):** **ESB = MassTransit-over-Kafka** (iş/integration olayları) · **Gerçek-zaman akışı = ham `Confluent.Kafka` POS/PDKS ingest + SignalR push** (yüksek hacim). Bunlar farklı kanallardır.

### 4.2 CQRS (yapısal — okuma/yazma modelleri ayrı)
- **Komutlar (yazım modeli → `HistoryDbContext` + outbox):** `TransferIslemiUygulaCommand` (onayla/reddet), `GirisYapCommand` (token + denetim).
- **Sorgular (okuma modeli → `HotDbContext`/projeksiyon):** `SubeDolulukQuery`, `SubeDetayQuery`, `BekleyenTransferOnerileriQuery`.
- **Dağıtım:** MediatR `ISender`; controller'lar yalnızca `Send(...)` çağırır → ince controller, kalın handler.
- **Pipeline (Decorator):** `Validation → Logging → Transaction(yalnız komut) → Denetim`.

### 4.3 Yeni NuGet (hedef sürümler)
| Paket | Sürüm | Katman |
|-------|-------|--------|
| `MediatR` | 12.4.* (sabit) | Application |
| `FluentValidation.DependencyInjectionExtensions` | 11.* | Application |
| `MassTransit` | 8.* | Application/Infrastructure |
| `MassTransit.Kafka` | 8.* | Infrastructure (üretim rider) |
| `Microsoft.AspNetCore.Authentication.JwtBearer` | 8.0.* | Api |
| `Otp.NET` | 1.4.* | Infrastructure (TOTP MFA) |
| `Microsoft.EntityFrameworkCore.Sqlite` | 8.0.* | Api (yalnız **Demo** profil) |
| `AspNetCore.HealthChecks.NpgSql` / `.Kafka` | 8.* | Api (S5) |
| `Microsoft.AspNetCore.Mvc.Testing` | 8.0.* | yeni `Arabica.Api.Tests` |

*(SignalR sunucu tarafı + Data Protection/AES-256 + `PasswordHasher<T>` çatı içinde — ek paket yok.)*

---

## 5. Revize Dilim Planı (S3…S5) — her sınırda inceleme kapısı

| Dilim | Kapsam | Biten kapılar | Çıkış kapısı |
|-------|--------|---------------|--------------|
| **S3 — CQRS + Desenler + ESB** | Komut/sorgu + MediatR; pipeline behavior'lar (Decorator); İş Kanunu **CoR** refactor; **Observer** fan-out; **Facade**; **Builder**; MassTransit (in-memory + Kafka rider) + consumer'lar. | #4, #5, #6, #8, #9 (altyapı) | `dotnet build` yeşil + birim/handler testleri yeşil; mevcut 51 test korunur. |
| **S4 — Api + Güvenlik** | `Arabica.Api`: 5 controller (**frozen contracts**), JWT + policy RBAC (BolgeKoordinatoru/SubeMuduru), **MFA (TOTP)** onayda, ProblemDetails (TR), denetim middleware (IP+zaman). | #9 (uç), kısmi #2 | Build yeşil + `WebApplicationFactory` API sözleşme testleri yeşil. |
| **S5 — Gerçek-zaman + Çalıştırma + Rapor** | **SignalR** hub + JS istemci (#10); `docker-compose` (app+Postgres+Kafka+Liquibase); health; AES-256; ≤2 sn gecikme ölçer; **Docker'sız Demo profil**; **GERÇEKTEN ÇALIŞTIR** + kanıt; **`PROJE-RAPORU.md`** (#1). | #1, #2, #10 + tümünün doğrulanması | `docker compose up` (veya Demo profil) ile uçtan uca çalışır; 5 uç + SignalR + ingest kanıtlı; 10 kapı yeşil. |

---

## 6. Çalıştırma Stratejisi ve Bilinen Engel (Kapı #2 için kritik)

- **Engel:** Bu derleme makinesinde **Docker kurulu/çalışır değil** (S2'de doğrulandı; Testcontainers entegrasyon testleri bu yüzden *atlanıyor*). `docker compose up` burada doğrudan çalıştırılamaz.
- **Azaltma (iki yollu, ikisi de teslim edilir):**
  1. **Üretim yolu — `docker compose up`:** Postgres 16 + Kafka 3.7 + Liquibase migrate adımı + Api. Docker'lı not makinesinde/grader'da uçtan uca çalışır (asıl hedef).
  2. **Docker'sız Demo profil (`ASPNETCORE_ENVIRONMENT=Demo`):** SQLite (tek şema, demo-bayrağıyla `hot.`/`hist.` niteliği kapatılır) + MassTransit **in-memory** + **simüle POS/PDKS besleyici** (in-process). Böylece bu makinede 5 uç + JWT/MFA + SignalR + (simüle) ingest **gerçekten çalıştırılıp** kanıt (HTTP yanıtları, SignalR mesajı, gecikme ölçümü) toplanabilir.
- **Karar gereği:** Grader Docker'lı ise (1) tercih edilir; değilse (2) Kapı #2 kanıtını sağlar. İkisi de S5'te kurulur. Eğer grader'ın Docker'ı varsa belirtmeniz yeterli; yoksa Demo profil kanıtı esas alınır.
- **Dürüstlük taahhüdü:** Sistemi gerçekten çalıştıramazsam, başarı iddia etmeden engeli raporlayacağım.

---

Onay bekleniyor — Faz 1'e geçmek için.
