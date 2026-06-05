# 🎓 ARABICA — Sınav Çalışma & Savunma Dökümanı

> **Amaç:** Bu döküman, projeyi hocaya **sınav gibi** savunabilmen için hazırlanmıştır. Her teknoloji için **“Nedir? · Nasıl çalışır? · Biz nerede/nasıl kullandık? · Olası soru”** kalıbıyla ilerler. Ayrıca kök dizindeki **orijinal raporlarla** (`en_son-1/2/3` = Tasarım/Gereksinim/Gerçekleştirim — Java/Spring tasarımı) bugünkü **.NET 8 gerçekleştirimimiz** arasındaki **farkları ve gerekçelerini** içerir.
>
> İlgili belgeler: [PROJE-RAPORU.md](PROJE-RAPORU.md) · [README.md](README.md) · [project-srs.md](project-srs.md) · [context.md](context.md) · [migration-blueprint.md](migration-blueprint.md)

---

## 0. Projeyi 60 saniyede anlat (ezber pitch)

> “Arabica Cafe’nin şubeleri arasında müşteri yoğunluğu dengesizdir: bazı şube atıl (boş masa/fazla barista), bazısı darboğazdadır. Biz, şubelerdeki **POS** (satış) ve **PDKS** (mesai) cihazlarından **Apache Kafka** ile akan **gerçek zamanlı** veriyi işleyip, **atıl şubeden yoğun şubeye otonom barista/ekipman transfer önerisi** üreten, **olay güdümlü bir karar destek sistemi** yaptık. Hedef uçtan uca gecikme **2 saniyenin altında**. Tasarım Java/Spring olarak kurgulanmıştı; biz **C#/.NET 8** ekosistemine, **Onion mimarisi + CQRS + ESB + SignalR** ile yeniden gerçekleştirdik.”

**Sayılar:** Pilot 2 şube (Isparta) · ≤2 sn gecikme · %30 atıl azaltma / %20 hız / %15 maliyet hedefleri · 87+ test · 10/10 zorunlu kriter.

---

## 1. 10 Zorunlu Kriter — Ne ister · Bizde nerede · Nasıl gösterilir

| # | Kriter | Bizde | Kanıt / Göster |
|---|--------|-------|----------------|
| 1 | **Proje raporu** | `PROJE-RAPORU.md` (TR) | Dosyayı aç, 10-kapı tablosu |
| 2 | **Çalışıyor** | `docker compose up` → `localhost:8080` | Canlı panel + `/health` Healthy |
| 3 | **OOP + SOLID** | `Personel`(abstract)→`Barista`/`SubeMuduru`; private alanlar; arayüzler | `src/Arabica.Domain/**` |
| 4 | **≥1 Creational** | **Factory Method** + **Builder** | `TransferEmriFactory`, `KapasiteRaporuBuilder` |
| 5 | **≥2 Structural** | **Adapter + Decorator + Facade** (3) | aşağıda §4 |
| 6 | **≥2 Behavioural** | **Strategy + State + CoR + Observer** (+Mediator) (4) | aşağıda §4 |
| 7 | **Onion Architecture** | `Domain ← Application ← Infrastructure`, `Api` kök | aşağıda §3.3 |
| 8 | **Enterprise Service Bus** | **MassTransit (Kafka rider)**, 2 consumer | aşağıda §3.6 |
| 9 | **CQRS** | Komut→`hist`+outbox · Sorgu→`hot` | aşağıda §3.5 |
| 10 | **Real-time** | **SignalR** | aşağıda §3.7 |

> **Püf nokta:** istenenler **1 / 2 / 2**, biz **2 / 3 / 4** verdik — “minimumu geçtik mi?” sorusuna marjla *evet*.

---

## 2. Mimarinin Büyük Resmi (tek paragraf savunma)

Sistem **Onion (Soğan) mimarisinde** bir **modüler monolittir**: en içte bağımlılıksız **Domain** (iş kuralları), onu saran **Application** (CQRS komut/sorgu, MediatR), en dışta **Infrastructure** (EF Core, Kafka, MassTransit, SignalR) ve tek kompozisyon kökü **Api**. Bağımlılıklar daima **içe** doğrudur (Dependency Inversion). Veri akışı **olay güdümlüdür (EDA)**: POS/PDKS → **Kafka** → işleme → **SignalR** ile panele canlı yansır. Yazma ve okuma yolları **CQRS** ile ayrılmıştır (komutlar `hist` şemasına + **transactional outbox**, sorgular `hot` şemasından). İş olayları (transfer onaylandı/reddedildi/tamamlandı) outbox’tan **MassTransit (ESB)** ile iki bağımsız tüketiciye (bildirim + denetim) dağılır.

---

## 3. Teknoloji Ansiklopedisi (Nedir / Nasıl çalışır / Bizde / Soru)

### 3.1 C# 12 / .NET 8 (LTS)
- **Nedir?** Microsoft’un açık kaynak, çapraz platform çalışma ortamı + dili. LTS = uzun destekli sürüm.
- **Nasıl çalışır?** Kod IL’e derlenir, CLR/JIT ile çalışır; çöp toplayıcı (GC) belleği yönetir.
- **Bizde?** Tüm backend `net8.0` hedefler. (Makinede .NET 9 SDK var ama projeler `-f net8.0` ile 8’e derleniyor; Docker `aspnet:8.0` imajını kullanıyor.)
- **Soru:** “Neden Java değil?” → Sahibinin kararıyla **.NET ekosistemine taşıdık**; davranışsal olarak rapora sadık kaldık (bkz. §7).

### 3.2 ASP.NET Core 8
- **Nedir?** .NET’in web/API çatısı (Spring Boot’un karşılığı).
- **Nasıl çalışır?** **Middleware pipeline** (istek sırayla katmanlardan geçer: auth → routing → controller), yerleşik **Dependency Injection**, `appsettings.json`+`IOptions` ile konfig.
- **Bizde?** `Arabica.Api` host’u; **Controller**’lar (`AuthController`, `SubeController`, `TransferController`, `AdminController`, `OzetController`); JWT/RBAC, ProblemDetails, SignalR hub, statik SPA (`wwwroot`).
- **Soru:** “Spring Boot karşılığı?” → ASP.NET Core; Spring Data JPA→EF Core, Spring Security→JwtBearer, Actuator→HealthChecks.

### 3.3 Onion Architecture (Kriter #7)
- **Nedir?** Katmanların **iç içe halkalar** gibi dizildiği, bağımlılıkların **içe** (çekirdeğe) doğru aktığı mimari. Çekirdek hiçbir dış teknolojiyi bilmez.
- **Nasıl çalışır?** `Domain` (en iç, saf iş kuralları) ← `Application` (use-case’ler, **portlar**=arayüzler) ← `Infrastructure` (portları uygulayan EF/Kafka adaptörleri). Dış katman içteki **arayüzü** uygular (Dependency Inversion).
- **Bizde?** `Arabica.Domain` (varlıklar, durum makinesi, Strategy, İş Kanunu) → `Arabica.Application` (CQRS + portlar: `ITransferEmriRepository`, `IOutbox`, `IEntegrasyonYayinci`…) → `Arabica.Infrastructure` (EF Core, Kafka, MassTransit). `Arabica.Api` her şeyi birleştiren **tek kompozisyon kökü**. `Arabica.Contracts` = frozen sözleşmeler/olaylar.
- **Soru:** “Onion’ı nereden anlıyoruz?” → `Domain` projesinin **hiçbir** dış pakete referansı yok; bağımlılık oku hep içeri. “Mikroservis miydi?” → Rapor mikroservis diyordu ama rubrik **#7 Onion** istiyor; biz Onion **monolit** yaptık (bkz. §7).

### 3.4 OOP + SOLID (Kriter #3)
- **OOP:** Kapsülleme (`private set` + davranış metotları), kalıtım (`Personel`→`Barista`/`SubeMuduru`), çok biçimlilik (strateji arayüzü), soyutlama (her sınırda interface).
- **SOLID:** **S**RP (her sınıf tek iş — Factory yalnız yaratır), **O**CP (yeni strateji eklemek ana kodu bozmaz), **L**SP (alt sınıflar yerine geçebilir), **I**SP (`IKomut`/`ISorgu` ince arayüzler), **D**IP (Application portlara bağlı, Infrastructure uygular).
- **Soru:** “Bir SOLID örneği ver” → OCP: yeni sezon stratejisi = yeni sınıf + tek DI kaydı, `OptimizasyonMotoru` değişmez.

### 3.5 CQRS — Command Query Responsibility Segregation (Kriter #9)
- **Nedir?** **Yazma (Command)** ve **okuma (Query)** sorumluluklarını ayırma deseni. Komut durumu değiştirir, sorgu yalnız okur.
- **Nasıl çalışır?** İstek bir **Command** ya da **Query** nesnesi olur; **MediatR** uygun **handler**’a yollar. Yazma modeli ve okuma modeli farklı olabilir.
- **Bizde?** Komut `TransferIslemiUygulaCommand` → `hist` şeması + **outbox** (yazma). Sorgular `SubeDolulukQuery`/`SubeDetayQuery`/`BekleyenTransferOnerileriQuery` → `hot` şeması (okuma). İşaretçi arayüzler: `IKomut<T>` / `ISorgu<T>` (`src/Arabica.Application/Ortak/Markerlar.cs`). Bu, raporun zaten istediği **hot/hist şema izolasyonunu (NFR-P4)** birebir CQRS’e oturtur.
- **Soru:** “CQRS kozmetik mi?” → Hayır: okuma `HotDbContext`, yazma `HistoryDbContext` — **fiziksel olarak ayrı** modeller.

### 3.6 Enterprise Service Bus (ESB) / MassTransit (Kriter #8)
- **Nedir?** ESB = servisler/bileşenler arası mesajlaşmayı **yönlendirme + pub/sub + dönüştürme** ile merkezîleştiren entegrasyon katmanı. **MassTransit** = .NET’in tanınmış servis bus framework’ü.
- **Nasıl çalışır?** Üretici bir **integration event** **publish** eder; bus, o olaya abone **birden çok consumer**’a bağımsızca dağıtır. Biz bunu gerçek **Kafka broker** üzerinde (“Kafka rider”) çalıştırıyoruz.
- **Bizde?** Outbox → MassTransit (Kafka rider) → `TransferOnaylandi/Reddedildi/Tamamlandi` olayları **iki bağımsız consumer**’a: `TransferBildirimConsumer` (→ SignalR push) ve `DenetimConsumer` (→ `hist.denetim_log`). Dosya: `src/Arabica.Infrastructure/Esb/`.
- **3 kanalı karıştırma!** (A) **ESB** = MassTransit/Kafka (iş olayları) · (B) **ham Kafka** ingest = POS/PDKS yüksek hacimli akış · (C) **SignalR** = tarayıcıya canlı push. Üçü ayrıdır.
- **Soru:** “Kafka varken neden ESB?” → Kafka bir **mesaj broker’ı/akış platformu**; ESB ise üstünde **yönlendirme + çok-aboneli iş olayı** katmanı. MassTransit bunu sağlar ve gerçek Kafka üzerinde koşar.

### 3.7 SignalR — Gerçek zamanlı iletişim (Kriter #10)
- **Nedir?** ASP.NET Core’un gerçek zamanlı kütüphanesi; sunucudan istemciye **anlık push** yapar.
- **Nasıl çalışır?** Önce **WebSocket** dener, olmazsa Server-Sent Events / Long Polling’e düşer (otomatik fallback). Sunucu bir **Hub** metodu çağırır, bağlı istemcilere mesaj iter.
- **Bizde?** `DolulukHub` (`/hubs/doluluk`); olaylar `DolulukGuncellendi` (canlı doluluk) ve `TransferBildirimi` (transfer toast’u). JS istemci `@microsoft/signalr` (CDN). Doluluk değişince ESB consumer → hub → tarayıcı, sayfa yenilemeden güncellenir.
- **Soru:** “WebSocket ile farkı?” → SignalR, WebSocket’i kullanır ama **fallback + bağlantı yönetimi + gruplar + auth** soyutlamasını üstüne koyar.

### 3.8 WebSocket (alttaki teknoloji)
- **Nedir?** Tek TCP bağlantısı üzerinden **çift yönlü, sürekli açık** iletişim protokolü (HTTP gibi iste-cevapla değil).
- **Bizde?** SignalR’ın varsayılan taşıması. Orijinal rapor da “WebSocket” diyordu; biz onu **SignalR** ile sardık (bkz. §7).

### 3.9 EF Core 8 + Npgsql (ORM)
- **Nedir?** **ORM** = nesne ↔ ilişkisel tablo eşleyici. EF Core Microsoft’un ORM’i; Npgsql = PostgreSQL sürücüsü. (Hibernate/JPA karşılığı.)
- **Nasıl çalışır?** `DbContext` + entity eşlemesi; LINQ sorguları SQL’e çevrilir; sorgular **parametreli** üretildiği için SQL injection’a karşı korur.
- **Bizde?** Üç bağlam: `HotDbContext` (hot şema, okuma), `HistoryDbContext` (hist şema, yazma+outbox), `KimlikDbContext` (kimlik şema, kullanıcılar). EF **şemayı oluşturmaz** — şema otoritesi **Liquibase**.
- **Soru:** “SQL injection?” → EF Core parametreli sorgu üretir; elle string birleştirmiyoruz.

### 3.10 PostgreSQL 16 + Şema İzolasyonu (NFR-P4)
- **Nedir?** Açık kaynak ilişkisel veritabanı. **Şema** = tablo grupları için isim alanı.
- **Bizde?** Tek veritabanı, üç şema: `hot` (anlık okuma: `sube`, `personel`), `hist` (yazma/arşiv: `transfer_emirleri`, `outbox`, `denetim_log`), `kimlik` (kullanıcı/parola). Anlık okuma ile tarihsel yazma **ayrı şemalarda** → kilit/deadlock azaltma.
- **Soru:** “Neden 3 şema?” → Performans/izolasyon (NFR-P4) + CQRS okuma/yazma ayrımı + KVKK (kimlik izole).

### 3.11 Liquibase (DB sürüm kontrolü / migration)
- **Nedir?** Veritabanı şema değişikliklerini **changeset** dosyalarıyla sürümleyen, dilden bağımsız araç.
- **Nasıl çalışır?** `changelog-master.xml` changeset’leri sırayla uygular; uygulananları `databasechangelog` tablosunda takip eder (tekrar uygulamaz).
- **Bizde?** `db/liquibase/` (hot/hist/kimlik changeset’leri). Compose’da **ayrı bir liquibase adımı** postgres healthy olunca şemaları kurar, **sonra** app başlar. EF **DDL sahibi değil** → çift otorite çakışması yok.
- **Soru:** “EF migration neden değil?” → Şema otoritesini tek elde (Liquibase) tutmak için; EF yalnız eşler.

### 3.12 Transactional Outbox (tutarlılık deseni)
- **Nedir?** “DB yazımı ile mesaj yayınını **birbirinden ayrılamaz** kılma” deseni.
- **Nasıl çalışır?** İş verisi + “gönderilecek olay” **aynı transaction**’da DB’ye yazılır (outbox tablosu). Ayrı bir gönderici (dispatcher) outbox’ı okuyup mesajı yayınlar, işaretler. Böylece DB commit oldu ama mesaj gitmedi (veya tersi) durumu **olmaz**.
- **Bizde?** `TransferIslemiUygulaCommandHandler` durum değiştirir + outbox satırını **enqueue** eder; `TransactionBehavior` **tek `SaveChanges`** ile ikisini **atomik** commit eder. `OutboxGonderici` outbox’ı Kafka rider’a basar. (`src/Arabica.Application/Cikti/Outbox.cs`, `Infrastructure/Cikti/`.)
- **Soru:** “DB+Kafka diverjansını nasıl engelliyorsun?” → Outbox: olay önce DB’ye, sonra dispatcher’la bus’a; tek transaction garantisi.

### 3.13 JWT — JSON Web Token (kimlik doğrulama)
- **Nedir?** İmzalı, **durumsuz (stateless)** oturum token’ı. `header.payload.signature` üç parçadan oluşur; payload’da **claim**’ler (rol, subeId) vardır.
- **Nasıl çalışır?** Giriş başarılıysa sunucu imzalı token üretir; istemci her istekte `Authorization: Bearer <token>` yollar; sunucu imzayı doğrular (DB’ye bakmadan).
- **Bizde?** `POST /auth/login` → JWT (15 dk geçerli). İstemci **sessionStorage**’da tutar. Claim’ler: rol + subeId (KVKK: yalnız sayısal). `KimlikServisleri.cs`.
- **Soru:** “Neden stateless?” → Ölçeklenebilirlik; sunucu oturum tutmaz. “Idle 15 dk?” → İstemci tarafı idle-timer + 15 dk’lık token ömrü.

### 3.14 RBAC / Policy-based Authorization
- **Nedir?** **Rol Bazlı Erişim Kontrolü** — kim neye erişir.
- **Bizde?** İki policy: `Koordinator` (= rol BolgeKoordinatoru) ve `Yonetici` (= her iki yönetici rol). `[Authorize(Policy=...)]` controller’larda. **Şube-kapsam:** Şube Müdürü yalnız kendi `subeId` claim’indeki şubeyi görür (`SubeController` 403 kontrolü). UI’da yetkisiz menü **DOM’a hiç eklenmez** (SRS §2). `Program.cs`, `Guvenlik/Guvenlik.cs`.
- **Soru:** “Müdür başka şubeyi görür mü?” → Hayır, 403; üstelik menüsü bile gelmez.

### 3.15 MFA / TOTP (çok faktörlü doğrulama)
- **Nedir?** İkinci faktör. **TOTP** = zamana dayalı tek kullanımlık 6 haneli kod (authenticator uygulaması üretir).
- **Bizde?** Kritik **transfer onayında** zorunlu: `POST /transfer/islem` `ONAYLA` için `X-MFA-Code` header’ı; geçersizse **401**, durum değişmez. Gövde sözleşmesi `{transferId, aksiyon}` **bozulmadı** (kod header’da). `Otp.NET`. Demo’da “kod üret” butonu client-side TOTP üretir.
- **Soru:** “Frozen sözleşmeyi bozdun mu?” → Hayır; MFA’yı **header** ile ekledik, JSON gövde aynı kaldı.

### 3.16 Parola güvenliği (PBKDF2 + salt) & AES-256
- **PBKDF2 + salt:** Parola düz değil, **tuzlanmış + geri döndürülemez hash** olarak saklanır (`PasswordHasher<T>`). Aynı parola farklı tuzla farklı hash → rainbow table’a dayanıklı.
- **AES-256 / Data Protection:** Hassas alanlar simetrik **AES-256** ile şifrelenir; ASP.NET Core **Data Protection** keyring kullanılır (compose’da `/keys` volume).
- **Soru:** “Parolayı nasıl saklıyorsun?” → Asla düz metin; tuzlu PBKDF2 hash, izole `kimlik` şemasında.

### 3.17 ProblemDetails (RFC 7807) + FluentValidation
- **ProblemDetails:** Hata yanıtları için standart JSON formatı (status, title…). Bizde **Türkçe** mesajlarla; geçersiz durum geçişi → **409**, doğrulama → **400**, yetki → **403**.
- **FluentValidation:** İstek DTO’ları için kural motoru; MediatR **ValidationBehavior** pipeline’ında çalışır.

### 3.18 Health Checks (Actuator karşılığı)
- **Nedir?** `/health` ucu sistemin (DB, Kafka) sağlığını döner. Spring Boot Actuator’ın karşılığı.
- **Bizde?** `/health` → `Healthy`; compose’da app **readiness** için kullanılır.

### 3.19 Docker / Docker Compose
- **Docker:** Uygulamayı bağımlılıklarıyla **konteyner** imajına paketler (donanımdan bağımsız, izole).
- **Compose:** Çok-servisli yığını tek dosyayla yönetir. Bizde: `postgres` → `liquibase` → `app` (+ `kafka`); DB/Kafka portları **host’a açılmaz** (NFR-S4), sadece app 8080.
- **Soru:** “Başka makinede nasıl çalışır?” → Sadece Docker yeter: `docker compose up -d --build` (README §Hızlı Başlangıç).

### 3.20 Apache Kafka 3.7 (olay akışı)
- **Nedir?** Yüksek hacimli, dağıtık **olay akışı / mesaj kuyruğu** platformu. **Topic**’lere **producer** yazar, **consumer group** okur; mesajlar diskte **retention** ile saklanır.
- **Nasıl çalışır?** Producer mesajı bir topic’in partition’ına **key** ile yazar (key=SubeId → şube bazlı sıra). Consumer **offset** ilerletir; biz **manuel commit** ile “işlendikten sonra commit” → en az bir kez + kayıpsız.
- **Bizde?** POS/PDKS ham akışı `Confluent.Kafka` ile tüketilir → şube doluluğu güncellenir. Ayrıca outbox olayları MassTransit (Kafka rider) ile yayınlanır. `KafkaTuketiciServisi`, `KafkaUreticisi`.
- **Soru:** “Neden Kafka, HTTP değil?” → Gerçek zamanlı, asenkron, **decoupled**, yüksek hacim + offline tolerans; HTTP istek-cevap bunun için hantal.

### 3.21 async/await + Channels (eşzamanlılık)
- **Nedir?** .NET’in **bloklamayan** asenkron G/Ç modeli. `Channel<T>` = üretici/tüketici arası **bounded** (geri-basınçlı) bellek-içi kuyruk.
- **Bizde?** Tüm G/Ç (DB, Kafka, HTTP) `async/await` + `CancellationToken`. Kafka tüketici → bounded channel → işleme (thread-per-event yok). Bu, raporun **Virtual Threads (Java Loom)** hedefini .NET-idiomatik karşılar (bkz. §7).

### 3.22 Test altyapısı (xUnit, WebApplicationFactory, Testcontainers)
- **xUnit:** .NET birim test çatısı.
- **WebApplicationFactory:** API’yi **bellek-içi** ayağa kaldırıp gerçek HTTP pipeline üzerinden sözleşme testi (Api.Tests).
- **Testcontainers:** Test sırasında **gerçek Postgres + Kafka** konteynerleri açar (Integration.Tests) → entegrasyon gerçek altyapıyla doğrulanır.
- **Bizde?** 87+ test: Domain (durum makinesi/İş Kanunu), Application (CQRS atomiklik), Api (5 sözleşme + RBAC + MFA), Integration (gerçek Postgres/Kafka + outbox + ESB).
- **Soru:** “Testler gerçek DB ile mi?” → Evet, Integration testleri Testcontainers ile gerçek Postgres+Kafka açar.

---

## 4. Tasarım Desenleri Kataloğu (Kriter #4/5/6) — nerede, neden

### Creational (Yaratımsal) — 2
- **Factory Method — `TransferEmriFactory`** (`Domain/Transferler/`): Personel/Ekipman transfer emrini **merkezî** yaratır; istemciyi somut tipten ayırır (SRP). *Neden:* ileride yeni transfer türleri eklenince ana kod bozulmasın.
- **Builder — `KapasiteRaporuBuilder`** (`Application/Raporlama/`): Kapasite raporunu **adım adım (fluent)** kurar.

### Structural (Yapısal) — 3
- **Adapter** — `KafkaOlayAdaptoru` (ham Kafka olayını domain olayına), `MassTransitYayinci` (portu ESB’ye), SignalR notifier (portu hub’a). *Neden:* dış teknolojiyi domain arayüzüne uyarlamak.
- **Decorator** — MediatR **pipeline behaviors** (`PipelineDavranislari.cs`): `LoggingBehavior` → `ValidationBehavior` → `TransactionBehavior`. Her handler’ı **kesişen ilgilerle** sarar, handler’a dokunmadan.
- **Facade** — `KaynakYonetimFasadi` (`Application/Fasad/`): optimizasyon+şube+strateji alt sistemlerini **tek arayüzde** toplar.

### Behavioural (Davranışsal) — 4 (+Mediator)
- **Strategy** — `IOptimizasyonServisi` → `VizeFinalSezonStratejisi` / `YazDonemiStratejisi` + resolver (keyed-DI): mevsime göre **çalışma zamanı** algoritma değişimi.
- **State** — `TransferEmri` durum makinesi: `Bekliyor→Onaylandı→Tamamlandı` / `→Reddedildi`; geçersiz geçiş `InvalidOperationException` (→409).
- **Chain of Responsibility** — `IsKanunuHalkasi` zinciri (`Domain/IsHukuku/`): her 4857 muhafızı (günlük/haftalık azami mesai, zorunlu mola) sırayla geçirir/engeller; ilk ihlal zinciri keser.
- **Observer** — `SubeDurumuDegistiNotification` + handler’lar: bir Kafka olayını **çok aboneye** (dashboard, optimizasyon, log) gevşek bağlı dağıtır.
- **Mediator (bonus)** — MediatR `ISender`/`IPublisher`: komut/sorgu/notification dağıtımı.

> **Karşılaştırma:** Orijinal tasarım raporunda **yalnız 3** desen vardı (Strategy, Observer, Factory). Biz **9 desen** (2/3/4 + Mediator) ile rubriğin structural≥2 ve behavioural≥2 şartını **fazlasıyla** karşıladık (bkz. §7).

---

## 5. Domain Modeli & İş Kuralları (savunmada en çok sorulan)

- **`Personel` (abstract)** → `Barista`, `SubeMuduru`. Kapsülleme: alanlar `private set`, davranış metotları (`MesaiBaslat`).
- **`Sube`:** `AnlikMusteriSayisi` (Kafka’dan), `AktifPersonelSayisi`, `Aktif` (pasifleştirme), `DolulukOraniHesapla()`, `SeviyeHesapla()` (Yeşil/Sarı/Kırmızı eşikleri).
- **`TransferEmri`:** `KaynakSubeId`, `HedefSubeId`, `Tip` (Personel/Ekipman), `Adet`, **durum makinesi**. `DurumGuncelle()` izinli geçişi doğrular, yoksa fırlatır.
- **Optimizasyon Motoru:** `DarbogazTespitiYap()` / `DarbogazHesapla()` darboğaz şubeyi bulur, atıl şubeden `TransferOnerisiUret()` ile öneri yaratır (Strategy + Factory + İş Kanunu CoR).
- **KVKK:** Hiçbir yerde **TC/ad-soyad/telefon yok**; yalnız sayısal ID + `TakmaAd`. Kimlik bilgisi izole `kimlik` şemasında, parola PBKDF2.
- **İş Kanunu 4857:** Günlük/haftalık azami mesaiyi aşacak ya da molası gelmiş baristanın transferi **engellenir**; yol süresi mesaiye dahil edilir.

---

## 6. Uçtan Uca Senaryo (bir transfer onayının hayatı — adım adım)

1. Yönetici panelde **Onayla** → `POST /api/v1/transfer/islem` `{transferId, aksiyon:"ONAYLA"}` + `X-MFA-Code`.
2. Controller **RBAC** (Yonetici) + **MFA (TOTP)** doğrular; geçmezse 401, durum değişmez.
3. **MediatR** komutu pipeline’dan geçirir: Logging → Validation → **Transaction**.
4. Handler: `TransferEmri.DurumGuncelle` (State; geçersizse 409) + **outbox** olayını enqueue + (personel transferinde) kaynak −Adet / hedef +Adet.
5. **TransactionBehavior** tek `SaveChanges` ile DB + outbox’ı **atomik** commit eder.
6. `OutboxGonderici` olayı **MassTransit (Kafka rider)**’a basar.
7. **İki consumer** bağımsızca tüketir: `TransferBildirimConsumer` → **SignalR** push; `DenetimConsumer` → `hist.denetim_log` (aktör+IP+zaman).
8. Tarayıcı **SignalR** ile “Transfer Onaylandı” toast’ı + doluluk/personel sayısı **canlı** güncellenir. (Hepsi ≤2 sn.)

---

## 7. ⭐ Orijinal Raporlar (Java/Spring) ↔ Bizim .NET Build’i: FARKLAR ve GEREKÇELER

> Hocanın “raporda Java yazıyor ama siz C# yapmışsınız” sorusuna hazır cevaplar. **Çekirdek mantık aynı; teknoloji yığını ve bazı mimari kararlar değişti. Her API sözleşmesi, iş kuralı ve NFR korundu (davranışsal sadakat).**

### 7.1 Teknoloji eşlemeleri (1:1 “tercüme”)
| Konu | Rapor (tasarım/gerçekleştirim) | Bizim build | Gerekçe (bahane) |
|---|---|---|---|
| Dil/çatı | Java 21 + Spring Boot | **C# 12 + ASP.NET Core 8** | Projeyi **.NET ekosistemine taşıma** kararı; davranışsal sadakat korundu |
| Eşzamanlılık | Virtual Threads (Loom) | **async/await + Channels** | .NET’te asenkron G/Ç zaten Loom’un hedeflediği bloklamayan modeli verir; thread-per-event gereksiz |
| ORM | Spring Data JPA / Hibernate | **EF Core 8 + Npgsql** | .NET’in standart ORM’i; aynı parametreli-sorgu güvenliği |
| Güvenlik | Spring Security | **JwtBearer + policy RBAC** | ASP.NET Core yerleşik karşılığı |
| Akış işleme | **Kafka Streams API** | **Confluent.Kafka consumer + Channels** | .NET’te Kafka Streams birebir yok; sade consumer ≤2 sn’yi karşılıyor, operasyonel olarak daha basit |
| Gerçek zaman | WebSocket + Fetch | **SignalR** | WebSocket’i sarıp fallback+auth+grup ekler; .NET-idiomatik |
| Sağlık | Spring Boot Actuator | **HealthChecks** | Birebir karşılık |
| Derleme | Maven + npm/Webpack | **dotnet CLI** + CDN SPA | .NET araç zinciri; SPA build adımı gerektirmesin diye CDN |
| İzleme | (Actuator metrikleri) | HealthChecks + (OpenTelemetry hazır) | Eşdeğer |

### 7.2 Mimari/yöntem farkları (raporda olmayan, bizim eklediğimiz)
| Konu | Raporda | Bizde | Gerekçe |
|---|---|---|---|
| **Mimari** | “**Mikroservis** + EDA” | **Onion monolit** + EDA | **Rubrik #7 Onion** istiyor (mikroservis değil). 8 KLOC ölçeğinde tek koşulabilir uygulama “**çalışıyor (#2)**” kanıtını da kolaylaştırır; alt sistemler **modül** olarak ayrık, ileride bölünebilir |
| **ESB (#8)** | **Hiç geçmiyor** (sadece spring-kafka) | **MassTransit (Kafka rider)** eklendi | Rubrik #8 ESB istiyor; mevcut Kafka üzerine oturttuk, ham POS/PDKS akışından ayrı tuttuk |
| **CQRS (#9)** | Açıkça yok (katmanlı servis) | **CQRS** (komut/sorgu + hot/hist) | Rubrik #9; raporun zaten istediği **hot/hist şema izolasyonuna** doğal oturdu |
| **Desen sayısı** | **3** (Strategy/Observer/Factory) | **9** (2/3/4 +Mediator) | Rubrik structural≥2 & behavioural≥2; eksikleri (Builder/Adapter/Decorator/Facade/State/CoR) ekledik |
| **Migration** | (genel) | **Liquibase** şema otoritesi + EF yalnız eşler | Çift-otorite çakışmasını önlemek |

### 7.3 Tasarlanıp **sadeleştirilen / demo’ya indirgenen** noktalar (dürüst liste + gerekçe)
| Rapor diyor | Bizde durum | Gerekçe (bahane) |
|---|---|---|
| PostgreSQL **Primary-Replica failover** | Compose’da tek Postgres; bağlantı dizgileri replica’ya hazır | Pilot/demo ortamı; failover bir **operasyon (ops)** konusu, kod hazır |
| Edge **Local Buffering** (offline) | Idempotent producer + manuel commit (kayıpsız/sıralı) | Gerçek POS donanımı yok; **simüle besleyici** ile gösteriliyor |
| **reCAPTCHA + hesap kilidi** | MFA (TOTP) uygulandı; reCAPTCHA/kilit kavramsal | Demo akışını kilitlememek; güvenlik çekirdeği (JWT/MFA/audit) hazır |
| **OEE / üretim metrikleri** | Yok | OEE bir **üretim (fabrika)** metriği — domaine ait değil (aşağıya bak) |
| Klavye kısayolları, **Ayarlar** menüsü | Yok/sınırlı | Puanlama kapılarını etkilemiyor; ileriye bırakıldı |
| ML/reinforcement, hava/trafik/etkinlik | Yok | Raporun da **gelecek yol haritası**; kapsam dışı |
| Sensör/IoT failover modu | POS-only tahmin kavramsal | Donanım yok; mantık tarif edildi |

### 7.4 Orijinal raporlardaki **tutarsızlık** (bunu sen biliyorsan artı puan)
Gereksinim raporunun **Veri Sözlüğü (7c)** ve **Anahtar Kelimeler (7a)** bölümleri **fabrika/üretim** terimleri içeriyor: `Üretim_Emri`, `Makine_Durum_Verisi`, **OEE**, **MES**, **BOM**, “üretim hattı”, “sensör/sıcaklık”. Bunlar projeye **bir üretim/fabrika şablonundan** uyarlanırken tam çevrilmemiş **şablon kalıntılarıdır**. Bizim asıl domain modelimiz (Tasarım raporundaki `Personel/Sube/TransferEmri`) **kafe** domaini için doğrudur ve biz **onu** uyguladık. Yani: *“Veri sözlüğündeki üretim terimleri şablon artığıdır; gerçek sınıf modeli kafe domaini olup .NET’te birebir gerçeklenmiştir.”*

### 7.5 Korunan her şey (savunmanın bel kemiği)
- **5 frozen REST sözleşmesi** birebir aynı (`/auth/login`, `/sube/doluluk`, `/sube/{id}/detay`, `/transfer/oneriler`, `/transfer/islem` + gövdeler).
- **TransferEmri durum makinesi** (`BEKLIYOR→ONAYLANDI/REDDEDILDI→TAMAMLANDI`), geçersiz → exception.
- **RBAC matrisi** (Koordinatör/Müdür/Barista) birebir.
- **NFR’ler:** ≤2 sn gecikme, JWT+MFA+audit(IP+zaman), KVKK anonimleştirme, İş Kanunu 4857, hot/hist izolasyon, Kafka offline tolerans, Liquibase.
- **Domain isimleri Türkçe** (Sube, Barista, TransferEmri, dolulukOraniHesapla) — rapordaki adlandırmayla aynı.

---

## 8. Olası Sınav Soruları — Hızlı Cevaplar (Q&A Bankası)

- **“Onion ile mikroservis farkı?”** → Onion = tek uygulama içinde **katmanlı bağımlılık** (içe doğru). Mikroservis = ayrı deploy edilen servisler. Rubrik Onion istedi; biz Onion monolit yaptık, alt sistemler modül olarak ayrık.
- **“CQRS neden?”** → Okuma/yazma farklı ihtiyaçlar; yazma `hist`+outbox, okuma `hot`. Ölçek + izolasyon + netlik.
- **“ESB ile Kafka aynı şey mi?”** → Hayır. Kafka = broker/akış. ESB (MassTransit) = üstünde yönlendirme + çok-aboneli iş olayı katmanı; biz Kafka **üzerinde** koşturuyoruz.
- **“Gerçek zamanlı nasıl?”** → SignalR (WebSocket) ile sunucudan tarayıcıya push; doluluk/transfer anlık.
- **“2 saniye garantisi nasıl ölçülüyor?”** → Olaya `UretimZamani` damgası; SignalR’a düşünce `şimdi − üretim` hesaplanır; `/metrik/gecikme` (demo’da p95 ~55 ms).
- **“KVKK’yı nasıl sağladın?”** → DB/Kafka’da PII yok; sayısal ID + TakmaAd; kimlik izole şema; parola PBKDF2.
- **“Atomiklik (DB+mesaj)?”** → Transactional outbox; tek SaveChanges; dispatcher bus’a basar.
- **“Hangi tasarım desenleri ve nerede?”** → §4 (Factory/Builder · Adapter/Decorator/Facade · Strategy/State/CoR/Observer/Mediator).
- **“Neden Java değil de C#?”** → Sahibinin .NET kararı; davranışsal sadakat; her sözleşme/NFR korundu.
- **“Test ettiniz mi?”** → 87+ test; Integration testleri **gerçek** Postgres+Kafka (Testcontainers).
- **“Şube müdürü başka şubeyi görür mü?”** → Hayır (403 + menü DOM’a eklenmez).
- **“Transferi onaylayınca ne olur?”** → State→Tamamlandı, personel kaynak−/hedef+, outbox→ESB→SignalR+audit; hepsi ≤2 sn.

---

## 9. Demo Checklist (sırayla göster)

`docker compose down -v; docker compose up -d --build` → `localhost:8080`
1. Koordinatör giriş → **JWT/RBAC**
2. Dashboard: stat kartları + **Chart.js** + canlı trend → **#10 + görsel**
3. “POS yükü simüle et” → **≤2 sn** rozeti → **NFR-P1**
4. Yönetim → Yeni Şube / Personel(anonim) / Manuel Transfer → **admin + KVKK + Factory**
5. Optimizasyon → Darboğaz tespit + **strateji değiştir** → **Strategy**
6. Transferler → Onayla (**MFA demo kod**) → Tamamlandı + toast + personel sayısı değişir → **State+MFA+ESB+SignalR**
7. Denetim Logları (aktör+IP+zaman) → **NFR-S7**
8. **Çıkış → Şube Müdürü** girişi → Yönetim/Raporlar menüsü **yok**, yalnız kendi şubesi → **RBAC §2**
9. `/swagger` + `PROJE-RAPORU.md` 10-kapı tablosu → **#1 + mimari**

---

> **Son söz (savunma cümlesi):** *“Raporlarda kurgulanan Java/Spring tasarımını, çekirdek iş mantığına ve tüm sözleşmelerine sadık kalarak .NET 8 ekosistemine taşıdık; üstüne notlandırma rubriğinin gerektirdiği Onion + CQRS + ESB ve genişletilmiş tasarım desenlerini ekledik. Sistem çalışıyor, test edilmiş ve 10 kriterin tamamını karşılıyor.”*
