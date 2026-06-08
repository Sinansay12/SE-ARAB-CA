# ☕ Arabica Cafe — Dinamik Kaynak Yönetim Sistemi

Şubeler arası **müşteri yoğunluğu dengesizliğini** (atıl ↔ darboğaz) çözmek için, POS/PDKS cihazlarından **Apache Kafka** üzerinden akan gerçek zamanlı veriyi işleyip **otonom barista/ekipman transfer önerileri** üreten, olay güdümlü bir **karar destek** sistemidir.

> **Platform:** C# 12 / .NET 8 LTS · ASP.NET Core 8 · EF Core 8 + Npgsql · PostgreSQL 16 · Apache Kafka 3.7 · MassTransit (ESB) · SignalR · Docker
> **Mimari:** Onion (Soğan) · CQRS · Event-Driven · Transactional Outbox
> Detaylı teknik rapor için bkz. **[PROJE-RAPORU.md](PROJE-RAPORU.md)**.

---

## 🚀 Hızlı Başlangıç (başka bir bilgisayarda çalıştırma)

Sistemin tamamı Docker ile gelir — **.NET veya veritabanı kurmana gerek yoktur.** Yalnızca Docker yeterlidir.

### 1) Ön koşullar
| Gereksinim | Not |
|---|---|
| **Docker Desktop** | Windows/Mac/Linux. Windows'ta **WSL2** etkin olmalı (kurulum sırasında sorar; gerekiyorsa bir kez yeniden başlat). |
| **Git** | Projeyi klonlamak için (veya klasörü kopyala). |
| İnternet | İlk çalıştırmada imajları (postgres, kafka, liquibase, .NET) indirir. |

> Docker'ın **çalıştığından** emin ol (Docker Desktop açık olmalı). Test için: `docker --version` ve `docker ps`.

### 2) Projeyi al
```bash
git clone <repo-adresi> Arabica
cd Arabica
```
(Veya proje klasörünü bilgisayara kopyalayıp içine gir.)

### 3) Çalıştır
```bash
docker compose up -d --build
```
Bu komut sırasıyla şunları yapar (ilk sefer birkaç dakika sürebilir — imajlar inip uygulama derlenir):
**postgres** sağlıklı olur → **liquibase** şemaları uygular (`hot` / `hist` / `kimlik`) → **app** başlar.

Başlamayı izlemek için:
```bash
docker compose logs -f app
```
`Now listening on: http://[::]:8080` satırını görünce hazırdır.

### 4) Aç
| Adres | Açıklama |
|---|---|
| **http://localhost:8080** | Web paneli (giriş → dashboard → yönetim) |
| http://localhost:8080/swagger | Tüm API uçlarını deneme arayüzü |
| http://localhost:8080/health | Sağlık kontrolü → `Healthy` |

### 5) Demo kullanıcılar
| Kullanıcı | Parola | Rol |
|---|---|---|
| `tunahan.basar` | `Arabica.2026!` | Bölge Koordinatörü (tüm şubeler + yönetim) |
| `sinan.say` | `Arabica.2026!` | Şube Müdürü (yalnız kendi şubesi) |

**MFA (transfer onayı):** TOTP secret `JBSWY3DPEHPK3PXP`. Onay ekranındaki **"demo kod üret"** butonu kodu otomatik üretir (harici authenticator gerekmez).

### 6) Durdurma / sıfırlama
```bash
docker compose down       # konteynerleri durdur (veri korunur)
docker compose down -v    # konteyner + veriyi sil (temiz başlangıç; demo verisi yeniden tohumlanır)
```

---

## 🧪 Testler

### ✅ Sonuçlar — **95 test · 0 başarısız · 0 atlanan**

| Test Projesi | Test | Türü | Kapsam |
|---|:--:|---|---|
| `Arabica.Domain.Tests` | **37** | Birim (saf) | Durum makinesi, İş Kanunu zinciri (CoR), doluluk hesabı, Strategy, Factory |
| `Arabica.Application.Tests` | **12** | Birim (sahte) | CQRS handler'ları, MediatR pipeline (Transaction), Observer, Builder/Factory |
| `Arabica.Api.Tests` | **33** | Entegrasyon (in-process) | 5 frozen uç, JWT/RBAC, MFA, durum makinesi (HTTP), yönetim uçları, KVKK |
| `Arabica.Integration.Tests` | **13** | Entegrasyon (gerçek) | Testcontainers: gerçek **Postgres 16 + Kafka**, outbox→ESB, atomiklik, şema izolasyonu |
| **TOPLAM** | **95** | | |

```
Başarılı! - Başarısız: 0, Başarılı: 37, Atlanan: 0  - Arabica.Domain.Tests.dll
Başarılı! - Başarısız: 0, Başarılı: 12, Atlanan: 0  - Arabica.Application.Tests.dll
Başarılı! - Başarısız: 0, Başarılı: 33, Atlanan: 0  - Arabica.Api.Tests.dll
Başarılı! - Başarısız: 0, Başarılı: 13, Atlanan: 0  - Arabica.Integration.Tests.dll  (gerçek Postgres + Kafka)
```

> 📋 Her testin tek tek ne doğruladığı, kullanılan teknikler (xUnit, FluentAssertions, EF InMemory, WebApplicationFactory, Testcontainers, MassTransit harness) ve olası sınav sorularına hazır cevaplar için: **[TEST-RAPORU.md](TEST-RAPORU.md)**.

### Çalıştırma

Testleri Docker dışında çalıştırmak için **.NET 8 SDK** gerekir (.NET 9 SDK de `net8.0` derler). Entegrasyon testleri **Docker'ın açık olmasını** ister (Testcontainers ile gerçek Postgres + Kafka başlatır).

```bash
# Proje-proje çalıştır (önerilen):
dotnet test tests/Arabica.Domain.Tests/Arabica.Domain.Tests.csproj
dotnet test tests/Arabica.Application.Tests/Arabica.Application.Tests.csproj
dotnet test tests/Arabica.Api.Tests/Arabica.Api.Tests.csproj
dotnet test tests/Arabica.Integration.Tests/Arabica.Integration.Tests.csproj
```

> ⚠️ **Not:** `docker compose` stack'i ayaktayken **tüm test paketini tek seferde** (`dotnet test Arabica.sln`) çalıştırmak, test host'u + Testcontainers'ın RAM için yarışması nedeniyle `OutOfMemoryException` verebilir. Ya **proje-proje** çalıştır ya da önce `docker compose down` ile stack'i durdur.

---

## 📁 Proje Yapısı

```
Arabica/
├─ src/
│  ├─ Arabica.Domain/          # Çekirdek: varlıklar, durum makinesi, İş Kanunu (CoR), Strategy
│  ├─ Arabica.Application/     # CQRS komut/sorgu, MediatR pipeline (Decorator), portlar, Facade
│  ├─ Arabica.Contracts/       # API sözleşmeleri + olaylar (frozen)
│  ├─ Arabica.Infrastructure/  # EF Core+Npgsql, Kafka, MassTransit (ESB), SignalR adaptörü
│  └─ Arabica.Api/             # ASP.NET Core host + controller'lar + wwwroot/ (SPA)
├─ tests/
│  ├─ Arabica.Domain.Tests/        # birim
│  ├─ Arabica.Application.Tests/    # birim (CQRS, atomiklik)
│  ├─ Arabica.Api.Tests/           # sözleşme (WebApplicationFactory)
│  └─ Arabica.Integration.Tests/   # Testcontainers: gerçek Postgres + Kafka
├─ db/liquibase/               # Şema changeset'leri (hot/hist/kimlik) — şema otoritesi
├─ docker/Dockerfile           # Çok aşamalı .NET 8 imajı
├─ docker-compose.yml          # app + postgres + kafka + liquibase
├─ PROJE-RAPORU.md             # Detaylı rapor (10 kapı, diyagramlar, çalıştırma kanıtı)
├─ TEST-RAPORU.md              # Test raporu (95 test, tek tek açıklama + SSS)
├─ UYUMLULUK-PLANI.md          # Uyumluluk planı
└─ migration-blueprint.md      # Java/Spring → .NET 8 eşleme planı
```

---

## 🏗️ Mimari (özet)

- **Onion:** `Domain ← Application ← Infrastructure`; `Arabica.Api` tek kompozisyon kökü.
- **CQRS:** komutlar → `hist` şeması (+ transactional outbox), sorgular → `hot` şeması (NFR-P4 şema izolasyonu).
- **ESB:** MassTransit (Kafka rider) — transfer olayları 2 bağımsız consumer'a (bildirim + denetim). POS/PDKS ham akışı ayrı bir Kafka kanalı, gerçek-zaman push'u ayrı bir SignalR kanalıdır.
- **Gerçek-zaman:** SignalR ile canlı doluluk + transfer bildirimi.
- **Tasarım desenleri:** Factory Method, Builder · Adapter, Decorator, Facade · Strategy, State, Chain of Responsibility, Observer (+ Mediator).
- **KVKK:** TC/ad-soyad/telefon **işlenmez**; yalnız anonim sayısal ID + takma ad. Kimlik bilgileri izole `kimlik` şemasında, parolalar PBKDF2.

---

## 🔧 Sorun Giderme

| Belirti | Çözüm |
|---|---|
| `docker: command not found` / daemon hatası | Docker Desktop'ı aç (kurulu olmalı, çalışıyor olmalı). Windows'ta WSL2 etkin mi? |
| `port 8080 is already allocated` | 8080'i kullanan uygulamayı kapat, ya da `docker-compose.yml`'de `"8080:8080"` → `"8081:8080"` yap. |
| App "unhealthy" / başlamıyor | `docker compose logs app` ile loglara bak; genelde postgres/kafka'nın healthy olmasını bekler (ilk açılışta normal). |
| Demo verisi karıştı / temiz başlamak | `docker compose down -v` → `docker compose up -d --build` (demo kullanıcı/şube/transfer yeniden tohumlanır). |
| Testlerde `OutOfMemoryException` | Stack'i durdur (`docker compose down`) veya testleri **proje-proje** çalıştır (yukarıya bkz.). |

---

## 🔐 Güvenlik notu

`docker-compose.yml` içindeki bağlantı bilgileri ve JWT imza anahtarı **yalnızca yerel geliştirme/demo içindir.** Gerçek bir ortama dağıtırken bu değerleri (özellikle `Jwt__Imza` ve veritabanı parolaları) ortam değişkenleri / Docker secrets ile **mutlaka geçersiz kıl.** Hassas dosyalar `.gitignore` ile sürüm kontrolü dışında tutulur.
