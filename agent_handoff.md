# Proje Yönetimi ve Yapay Zekâ Ajan Devri (Project Management & AI Agent Handoff)

**Proje:** Arabica Cafe Dinamik Kaynak Yönetim Sistemi
**Belge Tipi:** Üst Düzey Durum Devri (Status Handoff) — başka bir geliştirici veya yapay zekâ ajanının projeyi kesintisiz devralabilmesi için.
**Sürüm:** 1.0
**İlgili Belgeler:** [project-srs.md](project-srs.md) (gereksinimler) · [context.md](context.md) (mimari & teknik bağlam)

---

## 1. Mevcut Durum Özeti (Current Status Summary)

Proje, **gereksinim analizi** ve **sistem tasarımı** evrelerini tamamlamış; **gerçekleştirim (implementation)** aşamasına geçmiştir.

| Faz | Durum |
|---|---|
| Dokümantasyon (SRS / Tasarım / Gerçekleştirim raporları) | ✅ **Tamamlandı** |
| Mimari tasarım (UML, ER, Kafka Topic dizaynı) | ✅ **Tamamlandı / Donduruldu (Frozen — 13.03.2026)** |
| Altyapı (Docker + Ubuntu Cloud VPS) | ✅ Ayağa kaldırıldı |
| PostgreSQL 16 LTS entegrasyonu | ✅ Tamamlandı |
| Karar destek algoritması (< 2 sn öneri üretimi) | ✅ Kodlandı |

**Özet:** Yatay ölçeklenebilir Docker konteyner mimarisi Ubuntu Cloud VPS üzerinde çalışır durumda; PostgreSQL entegrasyonu tamam; **2 saniyenin altında** karar destek önerisi üreten iş mantığı algoritmaları kodlanmıştır. Dokümantasyon ve mimari tasarım **finalize edilmiştir** ve devre hazırdır.

### Devralan İçin Sonraki Adımlar (Next Steps)
- P4 fazı kapsamında **yük (Load) testi** ve **Kullanıcı Kabul Testi (UAT)** çalışmaları.
- Stres testi: Aynı anda birden fazla şubeden veri geldiğinde sistemin darboğaza girip girmediğinin doğrulanması.
- Hedef: tepki süresinin Kafka üzerinden **< 2 saniye** kalması ve optimizasyon kararlarının **> %90 doğrulukla** üretilmesi.

---

## 2. Proje Ekibi ve Roller (Project Team & Roles)

| Ad-Soyad | Rol (Kısaltma) | Sorumluluk |
|---|---|---|
| **Tunahan BAŞAR** | Proje Yöneticisi (PM) | Scrum süreç yönetimi, donanım/yazılım eşleme koordinasyonu, risk denetimi. |
| **Sinan SAY** | Analist (AN) | Mimari sınırların doğrulanması, veri akış şemalarının analizi, test senaryolarının hazırlanması. |
| **Abdurrahman Tarık YILMAZ** | Yazılımcı (DEV) | Backend kodlaması, Kafka yapılandırması, PostgreSQL sorgu optimizasyonu, Frontend entegrasyonu. |

> **Süreç Modeli:** Çevik (Agile) tabanlı **Scrum**. Günlük senkronizasyon toplantıları (Daily Scrum), sprint sonu değerlendirme (Sprint Review), mimari önceliklere göre sıralanan ürün birikim listesi (Product Backlog). İteratif/artımlı geliştirme — her sprint çalışan ve test edilebilir bir modül üretir.

---

## 3. Proje Kestirim Metrikleri (COCOMO)

Projenin karmaşıklığı, takım boyutu ve ileri teknoloji yığını (Kafka, EDA) göz önüne alınarak geliştirme eforu ve maliyet, **Orta COCOMO — Yarı Ayrık (Intermediate COCOMO — Semi-detached)** modeliyle hesaplanmıştır.

### 3.1 Formüller (Semi-detached katsayıları: a=3.0, b=1.12, c=2.5, d=0.35)

```text
Efor (Effort)    : E = a × (KLOC)^b = 3.0 × (8)^1.12   ≈ 30.45 adam-ay (person-months)
Süre (Duration)  : D = c × (E)^d    = 2.5 × (30.45)^0.35 ≈ 8.22 ay
Ekip (Staff Size): S = E / D        = 30.45 / 8.22       ≈ 3.7 tam zamanlı kişi (FTE)
```

### 3.2 Kestirim Çıktıları

| Metrik | Değer |
|---|---|
| Tahmini kod boyutu | **~8 KLOC** (8.000 satır kod) |
| Geliştirme eforu (Effort) | **30.45 adam-ay** (person-months) |
| Teorik ekip boyutu (Staff) | **~3.7 tam zamanlı kişi** (FTE) |
| Teorik proje süresi (Duration) | **8.22 ay** |

### 3.3 Teorik vs. Resmî Takvim (Karşılaştırma)

| | COCOMO Teorik Kestirim | Resmî Proje Takvimi |
|---|---|---|
| Süre | **8.22 ay** | **109 gün** (≈ 3.6 ay) |
| Tarih | — | **02.02.2026 – 22.05.2026** |
| Ekip | ~3.7 FTE | 3 kişi (PM + AN + DEV) |

> **Yorum:** Resmî akademik takvim (109 gün), COCOMO'nun öngördüğü 8.22 aylık teorik süreden belirgin biçimde kısadır. Bu **sıkıştırılmış (compressed) takvim**, çevik Scrum ritüelleri, hazır açık kaynaklı bileşenlerin (Kafka, PostgreSQL, Docker, Spring Boot) kullanımıyla geliştirme eforunun düşürülmesi ve sıkı kapsam sınırlandırması (yalnızca dinamik kaynak optimizasyonu) sayesinde yönetilmektedir.

### 3.4 İş Paketleri (Work Packages — WBS)

| Paket | Ad | Tarih |
|---|---|---|
| **P1** | Proje Fizibilite ve Ön Araştırma | 02.02.2026 – 20.02.2026 |
| **P2** | Sistem Analiz ve Tasarımı (UML, ER, Kafka Topic, Mock-up) | 23.02.2026 – 13.03.2026 *(mimari freeze)* |
| **P3** | Sistem Yazılımının ve Prototipinin Geliştirilmesi | 16.03.2026 – 24.04.2026 |
| **P4** | Prototip Uygulama, Test (Load/UAT/Stres) ve Revizyon | 27.04.2026 – 22.05.2026 |

---

## 4. Dağıtım ve Bakım (Deployment & Maintenance)

### 4.1 CI/CD ve Sıfır Kesintili Dağıtım (Zero-Downtime Deployment)

- Bakım işlemleri **CI/CD (Sürekli Entegrasyon / Sürekli Dağıtım) boru hatları** ile otomatikleştirilmiştir; insan hatası riski en aza indirilir.
- Yeni sürüm dağıtımları **sistemi durdurmadan (Zero Downtime)** gerçekleştirilir.
- Tüm backend servisleri ve Kafka altyapısı **Docker imajları** haline getirilir; Ubuntu 22.04 LTS üzerinde koşar.
- Olası servis çökmesinde **orkestrasyon araçları (Docker Swarm / Kubernetes)** ile konteynerler otomatik yeniden başlatılır; sistem maksimum 2 dakikada tam kapasiteye döner.

### 4.2 Sürüm ve Bakım Politikaları

| Politika | Kural |
|---|---|
| Kritik hata düzeltmeleri (Hotfix) | Tespitten sonra en geç **48 saat** içinde, sistem durdurulmadan canlı ortama. |
| Planlı sürüm güncellemeleri | En az yoğun zaman diliminde (gece vardiyası, 02:00–04:00), kesintisiz. |
| Regresyon testleri | Her yeni sürüm öncesi otomatik regresyon testleri (ERP/entegrasyon bozulmadığının doğrulanması). |
| İzleme | Spring Boot Actuator + Docker metrikleri ile 7/24 sağlık izlemesi. |

---

## 5. Gelecek Yol Haritası ve Öneriler (Future Roadmap & Recommendations)

Sistemin uzun ömürlü ve ölçeklenebilir olması adına önerilen geliştirmeler:

1. **Makine Öğrenmesi (ML) Entegrasyonu:** Proaktif tahmin hassasiyetini artırmak için karar destek motoruna makine öğrenmesi modelleri eklenmesi. Şube müdürünün bir öneriyi reddetmesi durumunda sistem bu davranıştan öğrenerek (reinforcement) bir sonraki önerisini günceller.
2. **Dış Veri Kaynaklarının Entegrasyonu:** Tahmin doğruluğunu artırmak için harici verilerin sisteme dahil edilmesi:
   - 🌦️ **Hava durumu tahmini** (weather forecasts) — kafe trafiğiyle doğrudan ilişkili.
   - 🚦 **Trafik verisi** (traffic data) — şubeler arası fiziksel transfer lojistiği için.
   - 🎉 **Yerel şehir etkinlikleri** (local city events) — örn. festival haftaları, konserler.
3. **Çoklu Dil Desteği (i18n):** Olası uluslararası franchise genişlemesi için mevcut modüler dil-dosyası altyapısının İngilizce başta olmak üzere ek dil paketleriyle aktive edilmesi.
4. **Kapsam Genişlemesi:** İlerleyen fazlarda tedarik zinciri veya müşteri sadakat modüllerinin, ana yapıyı bozmadan (servisler arası gevşek bağ sayesinde) eklenmesi.

---

## Sonuç (Conclusion)

Geliştirilen ürün; Arabica Cafe şubeleri arasındaki kapasite asimetrisini çözmek adına Apache Kafka ve Spring Boot mimarisini kullanarak hedeflenen **2 saniyelik otonom tepki süresini** başarıyla sağlamakta ve atıl kapasite maliyetlerini düşürmektedir. Dokümantasyon ve mimari tasarım finalize edilmiş olup; proje, test/devreye-alma fazına ve yukarıdaki yol haritası doğrultusunda evrimleşmeye hazırdır.
