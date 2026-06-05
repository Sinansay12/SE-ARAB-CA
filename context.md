# Sistem Mimarisi ve Teknik Bağlam (System Architecture & Technical Context)

**Proje:** Arabica Cafe Dinamik Kaynak Yönetim Sistemi
**Belge Tipi:** Mimari & Teknik Tasarım Bağlamı (Architecture Decision & Technical Context)
**Sürüm:** 1.0

> Bu belge, sistemin teknoloji yığınını, mimari kararlarını, tasarım kalıplarını, veri yönetimini, REST API yüzeyini ve dayanıklılık (resilience) mekanizmalarını tanımlar. Gereksinimler için bkz. [project-srs.md](project-srs.md).

---

## 1. Teknoloji Yığını (Tech Stack)

### 1.1 Backend & Çekirdek İş Mantığı

| Katman | Teknoloji | Gerekçe |
|---|---|---|
| **Programlama Dili** | **Java 21 (LTS)** | Yüksek performans, güçlü tip güvenliği, geniş kütüphane ekosistemi. OOP prensipleriyle sürdürülebilir, modüler genişlemeye uygun kod yapısı. |
| **Eşzamanlılık** | **Virtual Threads (Project Loom)** | Şubelerdeki POS/PDKS cihazlarından gelen binlerce eşzamanlı veri akışının, CPU/RAM kaynaklarını minimize ederek düşük gecikmeyle işlenmesi. |
| **Uygulama Çatısı** | **Spring Boot** | Mikroservis odaklı yapı; Spring Data JPA (veri erişimi), Spring Security (JWT güvenliği), Spring Boot Actuator (sağlık/metrik izleme). |
| **Mesajlaşma** | **Apache Kafka 3.7** | Şubeler arası tüm anlık veri akışının asenkron, decoupled ve yüksek hatalı-toleranslı iletimi. Kafka Streams API ile gerçek zamanlı analiz. |
| **Veritabanı** | **PostgreSQL 16 (LTS)** | İlişkisel veri bütünlüğü; şube envanteri, tarihsel doluluk istatistikleri ve personel rollerinin kalıcı saklanması. |
| **ORM / Persistence** | **Hibernate / JPA** | Java nesneleri ile veritabanı yığınları arasındaki çevrimin (object-relational mapping) otonom eşlenmesi. |
| **DB Sürüm Kontrolü** | **Liquibase** | Veritabanı şema değişikliklerinin sürümlenmesi ve izlenebilir migrasyonu. |
| **Konteynerizasyon** | **Docker, Docker Compose** | Donanımdan bağımsız, yüksek izolasyonlu ve yatay ölçeklenebilir çalışma zemini. |
| **Altyapı / İşletim** | **Ubuntu Cloud VPS** | Linux tabanlı, yüksek erişilebilirlikli sanal özel bulut sunucu ortamı. |

### 1.2 Frontend (İstemci Tarafı)

| Teknoloji | İşlev |
|---|---|
| **HTML5** | Web uygulamasının anlamsal (semantic) iskeleti. |
| **CSS3 + Bootstrap 5** | Görsel tasarım ve duyarlı (responsive) yapı; ızgara sistemi, kartlar, ilerleme çubukları, modal pencereler, uyarı rozetleri. Farklı ekran boyutlarına (masaüstü, endüstriyel tablet, mobil) tam uyum. |
| **JavaScript (ES6+)** | İstemci tarafı dinamik etkileşim ve iş mantığı; DOM manipülasyonu, veri bağlama, kullanıcı aksiyon yönetimi. |
| **WebSocket** | Kafka üzerinden gelen anlık kapasite değişimlerinin sayfa yenilenmeksizin DOM'a yansıtılması. |
| **Fetch API / AJAX** | Spring Boot RESTful API'leriyle asenkron, JSON tabanlı haberleşme. |

> **Uygulama Modeli:** Tek Sayfa Uygulaması (SPA) — sayfa yenilenmeden modüller arası geçiş. Arayüz varsayılan dili **Türkçe**'dir; ancak metin etiketleri statik HTML yerine **dil dosyalarından dinamik** çekilecek şekilde, ileride çoklu dil (i18n) desteğine uygun modüler bir yapıda tasarlanmıştır.

---

## 2. Yazılım Mimarisi (Software Architecture)

### 2.1 Mikroservis + Olay Güdümlü Mimari (Microservices & Event-Driven Architecture — EDA)

Sistem, dağıtık ve yüksek trafikli veri setlerini gerçek zamanlı yönetmek amacıyla **Olay Güdümlü Mimari (EDA)** prensiplerine göre yapılandırılmıştır:

- Şubelerden gelen anlık satış ve personel hareket verileri, bileşenler arasında **doğrudan bağımlılık (tight-coupling) yaratmaksızın** Apache Kafka 3.7 üzerinden asenkron iletilir.
- Bu mimari, **analiz motoru ile veri kaynaklarını birbirinden ayırarak (decoupling)** sistemin hata toleransını artırır ve *"2 saniyenin altında analiz"* hedefine ulaşılmasını sağlar.
- Her servis, Ubuntu Cloud VPS üzerinde koşan **Docker Engine** ile kendi konteyner ağında izole edilir (mikroservis bazlı ağ izolasyonu).

#### Mevcut Durumdan (As-Is) Önerilen Mimariye (To-Be)

| Boyut | Mevcut (Eski) Mimari | Önerilen Mimari |
|---|---|---|
| Yapı | Birbiriyle entegre olmayan, monolitik, kapalı sistemler | Mikroservis odaklı, konteynerize, decoupled |
| Veri Akışı | Gün sonu / periyodik toplu (batch) aktarım | Gerçek zamanlı, asenkron, olay güdümlü stream |
| Reaksiyon | Anlık reaksiyon imkânsız | < 2 sn karar destek önerisi |

### 2.2 Alt Sistem Hizmetleri (Subsystem Services)

1. **Yetkilendirme ve Güvenlik Alt Sistemi** — JWT üretimi, şifreleme, oturum yönetimi, RBAC, kritik işlem denetim logu.
2. **Gerçek Zamanlı Veri Transferi Alt Sistemi** — POS/PDKS verilerini Kafka ile toplar, kuyrukta bekletir, darboğaz yaratmadan işleme motoruna aktarır.
3. **Transfer Karar Destek Alt Sistemi** *(en kritik)* — Anlık Kafka verileri + statik kapasite verilerini karşılaştırarak optimizasyon algoritmalarını çalıştırır, otonom transfer kararları üretir.
4. **Kalıcı Veri ve Arşiv Alt Sistemi** — PostgreSQL üzerinde aktif veri yönetimi; geçmiş doluluk oranları ve tamamlanmış transfer loglarının arşivlenmesi, yedekleme/geri dönme koordinasyonu.
5. **Kullanıcı Arayüzü ve Raporlama Alt Sistemi** — HTML5/Bootstrap/JS ile anlık doluluk grafikleri, performans metrikleri ve transfer onay/red ekranları.

---

## 3. Tasarım Kalıpları (Design Patterns)

Sistemin değişime dirençli olması için **SOLID** prensipleri ve aşağıdaki tasarım kalıpları uygulanmıştır:

### 3.1 Strategy Pattern (Strateji Kalıbı)

**Problem:** Kapasite Optimizasyon Motoru, yılın farklı dönemlerinde farklı algoritmalara ihtiyaç duyar (örn. Isparta'da öğrenci nüfusunun yoğun olduğu vize/final haftalarındaki müşteri bekleme toleransı, yaz aylarındakinden farklıdır).

**Çözüm:** Algoritmalar `OptimizasyonStratejisi` (interface: `IOptimizasyonServisi`) arkasında soyutlanmıştır. Sistemin ana kodu değiştirilmeden, sadece yeni bir strateji sınıfı eklenerek algoritma **çalışma zamanında (runtime)** değiştirilebilir.

```text
IOptimizasyonServisi (interface)
        ├── darbogazHesapla(Sube s)
        └── transferOnerisiUret(Sube kaynak, Sube hedef)
                │
   ┌────────────┴────────────┐
   ▼                         ▼
VizeFinalSezonStratejisi   YazDonemiStratejisi
```

### 3.2 Observer Pattern (Gözlemci Kalıbı)

**Problem:** Kafka üzerinden yeni bir POS/PDKS verisi geldiğinde, bu veriye bağımlı birden fazla bileşenin (**Dashboard arayüzü, Optimizasyon Motoru, Loglama Servisi**) anında haberdar olması gerekir.

**Çözüm:** Observer kalıbı ile veri üreten sınıflar ile tüketen sınıflar arasındaki sıkı bağ koparılır. Sisteme yeni bir dinleyici (listener) eklemek **kod değişikliği gerektirmez** — gelen Kafka akışının dashboard, logger ve motora decoupled dağıtımı sağlanır.

### 3.3 Factory Method Pattern (Fabrika Metodu Kalıbı)

**Problem:** Üretilen `TransferEmri`, ilerleyen aşamalarda yalnızca Personel için değil, Ekipman (yedek POS cihazı, sandalye vb.) için de üretilebilir.

**Çözüm:** Nesne yaratım süreci `TransferEmriFactory` sınıfında **merkezileştirilir**; istemci kodu somut sınıflardan ayrılır. İstemci sadece ne tür bir transfer emri istediğini belirtir, nasıl yaratılacağı fabrikanın sorumluluğundadır (Tek Sorumluluk Prensibi — SRP).

---

## 4. Veri Yönetimi ve Kalıcılık (Data Management & Persistence)

### 4.1 İlişkisel Şema Tasarımı

Çekirdek nesne yönelimli model, iş mantığını ve veri yapılarını kapsülleyen sınıflardan oluşur:

| Sınıf | Sorumluluk |
|---|---|
| **Personel** *(üst/soyut sınıf)* | Tüm çalışanların ortak nitelikleri (`personelId`, `adSoyad`) ve metotları (`mesaiBaslat()`). Kapsülleme gereği nitelikler `private`. |
| **Barista** *(alt sınıf)* | Personel'den türemiş; operasyonel niteliklere sahip. |
| **SubeMuduru** *(alt sınıf)* | Personel'den türemiş; `transferOnayla()` davranışına sahip. |
| **Sube** | Arabica Cafe şubesini temsil eder; `anlikMusteriSayisi` Kafka ile güncellenir, `dolulukOraniHesapla()` ile kapasite yüzdesi hesaplanır. |
| **TransferEmri** | Karar destek sisteminin ürettiği iş nesnesi; kaynak/hedef şube, durum (`BEKLIYOR`/`ONAYLANDI`/`REDDEDILDI`/`TAMAMLANDI`). PostgreSQL `transfer_emirleri` tablosunda kalıcı + audit arşivi. |
| **OptimizasyonMotoru** | İş mantığını yürüten ana sınıf; `darbogazTespitiYap()` ve `transferOnerisiUret()`. |
| **KafkaVeriServisi** | Dış sistem entegrasyonu; topic dinleyerek (`posVerisiOku`, `pdksVerisiOku`) JSON → Java nesne dönüşümü. |

> **Veri Bütünlüğü:** Varlıklar arası ilişkiler (1-N, N-M) yabancı anahtar (foreign key) referansları ile garanti altına alınır. ACID prensipleri uygulanır.

### 4.2 Yüksek Erişilebilirlik — Primary–Replica (Failover)

- **"Her Zaman Çevrimiçi" (Always-On)** mimarisi benimsenmiştir: aktif çalışan **ana veritabanı (Primary)** ile eşzamanlı veri kopyalayan bir **ikincil sunucu (Replica)** yapılandırılmıştır.
- Ana sunucu plan dışı durduğunda, ikincil sunucu otomatik devreye girerek (**failover**) sistem devamlılığını **hiçbir veri kaybı olmadan** sağlar.
- **Yedekleme:** Haftalık tam yedekleme + günlük artımlı (incremental) yedekler; yedekler şifrelenip izole depolama ünitelerine aktarılır. Zaman damgalı işlem günlükleri (WAL) ile dakikalar içinde son kararlı noktaya geri dönüş (point-in-time recovery) hedeflenir.
- **Şema İzolasyonu:** Anlık akan Kafka verileri ile karar destek motorunun okuduğu tarihsel veriler, deadlock'ları önlemek için farklı şemalarda izole edilir.

---

## 5. REST API Spesifikasyonu (REST API Specification)

İstemci-Sunucu modeli; iletişim **RESTful Web Servisleri** üzerinden, **JSON** formatında ve **HTTPS** protokolü ile sağlanır.

### 5.1 Kimlik Doğrulama & Yetkilendirme

| Uç Nokta | Metot | Açıklama |
|---|---|---|
| `/api/v1/auth/login` | **POST** | Kullanıcının (Bölge Koordinatörü / Şube Müdürü) güvenli girişi. İstek gövdesi: `{ "kullaniciAdi": "isim.soyisim", "sifre": "********" }`. Başarılı girişte, sonraki isteklerin yetkilendirilmesinde kullanılmak üzere şifrelenmiş bir **JWT** döner. |

### 5.2 Şube & Kapasite

| Uç Nokta | Metot | Açıklama |
|---|---|---|
| `/api/v1/sube/doluluk` | **GET** | Dashboard ilerleme çubuklarını besleyen ana veri kaynağı. Kafka üzerinden güncellenmiş **tüm şubelerin anlık doluluk oranları, maksimum kapasiteleri ve aktif personel sayılarını** liste halinde döner (real-time occupancy stream). |
| `/api/v1/sube/{subeId}/detay` | **GET** | Belirtilen benzersiz `subeId`'ye sahip şubenin o anki **detaylı personel listesini ve operasyonel durumunu** getirir. |

### 5.3 Transfer & Optimizasyon

| Uç Nokta | Metot | Açıklama |
|---|---|---|
| `/api/v1/transfer/oneriler` | **GET** | Optimizasyon motoru tarafından üretilen ve **yöneticinin onayını bekleyen** aktif personel/ekipman transfer önerilerini getirir (örn. *Isparta Çarşı'dan Meydan'a 2 Barista*). |
| `/api/v1/transfer/islem` | **POST** | Bir transfer önerisinin onaylanması/reddedilmesinde tetiklenir. İstek gövdesi: `{ "transferId": 1045, "aksiyon": "ONAYLA" }`. Başarılıysa **HTTP 200 OK** döner ve backend, ilgili şubelere Kafka üzerinden transferin başladığına dair olay yollar. |

---

## 6. Risk Yönetimi ve Dayanıklılık (Risk Management & Resilience)

### 6.1 "Local Buffering" (Yerel Tamponlama) Mekanizması

**Risk:** Uç birimlerden (uzak şubeler) veri iletiminde yaşanabilecek **ağ kesintileri**.

**Teknik Spesifikasyon:**
- Şube tarafındaki veri toplama aracı, merkeze erişemediği anlarda verileri **geçici olarak yerel disk üzerinde asenkron** olarak saklar.
- Kafka veri üreticileri (producers) **çevrimdışı (offline) toleransına** sahip olacak şekilde yapılandırılmıştır.
- Bağlantı yeniden tesis edildiğinde, tamponlanan veriler Kafka topic'lerine **sıralı (sequential)** ve **kayıpsız** olarak gönderilir.

### 6.2 Diğer Risk Azaltma Stratejileri

| Risk | Azaltma Stratejisi |
|---|---|
| Uç birimlerden veri iletiminde ağ kesintileri | Kafka mesaj kuyruğunda çevrimdışı tolerans + mesaj saklama (retention). Sensör bağlantı kaybında sistem, POS verileri üzerinden **tahminsel yedekli (failover) çalışma moduna** otonom geçer. |
| Yüksek işlem hacminde veritabanı kilitlenmeleri | Olay odaklı anlık okumalar ile tarihsel veri yazma işlemlerinin **mimari düzeyde izolasyonu** (ayrı şemalar). |
| Beklenmeyen veri yığılmaları sonucu darboğazlar | Ağ geçidi katmanında verilerin **hafifletilmiş hiyerarşik JSON** formatında sıkıştırılarak aktarımı. |

### 6.3 İzleme ve Kontrol Mekanizmaları

- **Sistem Sağlığı (Health):** Backend RAM/CPU kullanımı ve Kafka broker sağlık durumu **Spring Boot Actuator** ve Docker istatistik metrikleriyle 7/24 izlenir.
- **Süreç Denetimi:** 2 saniyelik uçtan uca gecikme kısıtı, loglanan zaman damgaları üzerinden otomatik hesaplamalarla kontrol edilir.
- **Güvenlik Mimarisi:** Veritabanı portları dışa açılmaz; yalnızca backend IP'sine izin veren Firewall kuralları; OWASP Top 10 entegrasyonu; tüm kritik işlemlerin IP + zaman damgalı denetim loglaması.

---

> İlgili belgeler: [project-srs.md](project-srs.md) (gereksinimler) · [agent_handoff.md](agent_handoff.md) (proje yönetimi & devir).
