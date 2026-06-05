# Yazılım Gereksinim Spesifikasyonu (Software Requirements Specification — SRS)

**Proje:** Arabica Cafe Dinamik Kaynak Yönetim Sistemi
**Proje No:** 3 — "Kapasite Arttırma ve Rekabet Gücü Kazanma"
**Kurum:** Isparta Uygulamalı Bilimler Üniversitesi — Bilgisayar Mühendisliği Bölümü
**Belge Tipi:** Resmî Teknik Gereksinim Spesifikasyonu
**Standart Uyumu:** IEEE 830-1998
**Sürüm:** 1.0 (Gereksinim & Tasarım fazları dondurulmuş — *frozen*)

> Bu belge; *Sistem Tanımı ve Gereksinim Raporu*, *Yazılım Tasarım Raporu* ve *Yazılım Gerçekleştirim Raporu* kaynaklarının sentezlenmesiyle, IEEE 830-1998 SRS şablonuna uygun olarak hazırlanmıştır.

---

## 1. Giriş ve Amaç (Introduction & Purpose)

### 1.1 Çekirdek Problem (Core Problem)

Arabica Cafe, özellikle **Isparta** gibi öğrenci nüfusunun yoğun ve dönemsel hareketliliğin (yaz/kış döngüsü, vize/final haftaları, yerel etkinlikler) yüksek olduğu bölgelerde **şubeler arası mesleki/trafik dengesizliği** (occupational/traffic imbalance) sorunu yaşamaktadır. Müşteri yoğunluğu yıl içinde ve günün belirli saatlerinde şubeler arasında büyük farklılıklar gösterir:

- **Atıl kapasite:** Bazı şubelerde boş masa ve ihtiyaç fazlası personel (barista) oluşur.
- **Darboğaz (bottleneck):** Aynı anda diğer şubelerde kapasite yetersizliği, sipariş gecikmeleri ve müşteri memnuniyetsizliği doğar.

Kaynakların (barista, ekipman) **manuel, sezgisel ve anlık olmayan** yöntemlerle yönetilmesi; işletme için ciddi bir **fırsat maliyeti** ve **müşteri kaybı** yaratmaktadır. Mevcut POS ve PDKS sistemleri birbirinden bağımsız, kapalı ve toplu (batch) veri aktaran sistemler olduğundan anlık reaksiyon imkânsızdır.

### 1.2 Vizyon (Vision)

Şubelerden **POS** (Satış Noktası) ve **PDKS** (Personel Devam Kontrol Sistemi) cihazlarından akan **gerçek zamanlı** verileri Apache Kafka üzerinden toplayıp; Java tabanlı, olay güdümlü (event-driven) bir backend mimarisiyle işleyerek, şubeler arası yoğunluğu izleyen ve atıl kaynakları yoğun şubelere otonom olarak yönlendiren **veri odaklı bir karar destek (decision support) sistemi** kurmaktır. Hedef; insan inisiyatifini minimize eden, proaktif ve eyleme geçirilebilir bir **dinamik kaynak optimizasyonu** ağıdır.

> **Kapsam Sınırı (Scope):** Sistem yalnızca *dinamik kaynak optimizasyonu ve karar desteği* odaklıdır. Genel muhasebe, bordrolama ve merkezi tedarik zinciri (hammadde) yönetimi **kapsam dışıdır**. Pilot dağıtım Isparta'daki iki şube ile sınırlandırılmıştır.

### 1.3 Başarı Ölçütleri (Success Metrics)

| # | Metrik | Hedef Değer | Açıklama |
|---|--------|-------------|----------|
| M1 | **Atıl Kapasite Azaltımı** | **%30 azalma** | Düşük yoğunluklu sezonlarda (örn. yaz dönemi) tespit edilen atıl masa ve personel oranında. |
| M2 | **Hizmet Süresi / Müşteri Memnuniyeti** | **%20 iyileşme** | Yoğun şubelere anlık personel takviyesi ile sipariş bekleme sürelerinde. |
| M3 | **Operasyonel Maliyet Düşüşü** | **%15 düşüş** | Şube bazlı gereksiz mesai ve sabit giderlerin optimize edilmesiyle toplam operasyonel maliyette. |
| M4 | **Sistem Gecikme Süresi (Performans)** | **< 2 saniye** | Şubeden akan verinin Kafka üzerinden işlenip karar destek paneline yansıması (uçtan uca). |

---

## 2. Kullanıcı Rolleri ve Yetkilendirme Matrisi (RBAC)

Sisteme erişim **Rol Bazlı Erişim Kontrolü** (Role-Based Access Control) ve **en az ayrıcalık** (least privilege) prensibi ile yönetilir. Menü öğeleri, JWT içindeki yetki (claim) verilerine göre **dinamik** olarak oluşturulur; erişim izni olmayan sekmeler DOM ağacına hiç eklenmez.

### 2.1 Aktörler ve Operasyonel Ayrıcalıklar

#### Barista / Kasiyer (Uç Birim Kullanıcıları)
- Web tabanlı yönetim paneline **doğrudan erişim yetkisi yoktur.**
- Sistemle etkileşimi fiziksel uç cihazlarla sınırlıdır: **POS** üzerinden sipariş girişi, **PDKS** üzerinden mesai işlemleri — `mesaiBaslat()`, `mesaiBitir()`.
- Bu kullanıcıların her eylemi Apache Kafka mesaj kuyruğu üzerinden **asenkron** olarak sisteme aktarılır ve optimizasyon motorunun anlık veri havuzunu besler.

#### Şube Müdürü (Yerel Yönetici)
- Kendilerine tahsis edilen kimlik bilgileri ve **JWT tabanlı yetkilendirme** ile giriş yapar.
- **Görüntüleme:** Yalnızca **sorumlu olduğu şubenin** anlık doluluk oranları, aktif personel sayısı ve kapasite durumu (yeşil/sarı/kırmızı uyarı seviyeleri).
- **İşlem:** Kendi şubesine yönlendirilen veya kendi şubesinden çıkacak transfer emirlerini (`TransferEmri`) operasyonel uygunluğa göre **onaylar** (`transferOnayla()`) / **reddeder** (`transferReddet()`).

#### Bölge Koordinatörü (Merkez Yönetici / Sistem Yöneticisi)
- Sistem hiyerarşisinde **en üst düzey** yetkiye sahip aktör.
- **Küresel İzleme:** Franchise ağına bağlı **tüm şubelerin** anlık doluluk, darboğaz ve atıl kapasite durumlarını tek merkezi gösterge panelinden eşzamanlı izler.
- **Transfer & Optimizasyon Yönetimi:** Optimizasyon motorunun ürettiği çapraz-şube önerilerini denetler; sistemin otonom kararlarına müdahale ederek **manuel transfer emri** oluşturabilir.
- **Sistem & Denetim:** Sistem loglarını, donanım/sensör bağlantı hatalarını ve kullanıcı yetkilendirmelerini yönetir.
- **Raporlama:** PostgreSQL'de arşivlenen tarihsel verilerden kapasite analiz raporları, OEE (Toplam Ekipman Etkinliği) verileri ve şube bazlı performans metriklerini sorgular/dışa aktarır.

### 2.2 Rol-Yetki (Erişim) Matrisi

| Sistem İşlevi / Yetki Alanı | Bölge Koordinatörü (Merkez) | Şube Müdürü | Barista / Kasiyer |
|---|---|---|---|
| **Sisteme Giriş (Login)** | ✅ Yetkili | ✅ Yetkili | ❌ Yetkisiz (Yalnızca PDKS) |
| **Anlık Şube Doluluk Verilerini Görüntüleme** | ✅ Tüm şubeler için | 🟡 Yalnızca kendi şubesi için | ❌ Yetkisiz |
| **Personel / Ekipman Transferi Başlatma & Onaylama** | ✅ Tüm şubeler arasında | 🟡 Yalnızca kendi şubesine/şubesinden | ❌ Yetkisiz |
| **Sistem Loglarını, Hataları ve Kullanıcıları Yönetme** | ✅ Yetkili | ❌ Yetkisiz | ❌ Yetkisiz |
| **Tarihsel Rapor Üretimi (Reports)** | ✅ Yetkili | ❌ Yetkisiz | ❌ Yetkisiz |

---

## 3. Fonksiyonel Gereksinimler (Functional Requirements)

### 3.1 Olay Güdümlü Veri Hattı (Event-Driven Data Pipeline via Kafka)

- **FR-1:** Sistem, şubelerdeki POS cihazlarından gelen anlık sipariş/satış verilerini ve PDKS'den gelen personel giriş/çıkış hareketlerini **Apache Kafka 3.7** üzerinden, bileşenler arası sıkı bağımlılık (tight-coupling) yaratmadan asenkron olarak toplar.
- **FR-2:** `KafkaVeriServisi` sınıfı, ilgili topic'leri dinleyerek (`posVerisiOku`, `pdksVerisiOku`) JSON formatında gelen anlık verileri Java nesnelerine dönüştürür ve ilgili `Sube` nesnesinin durumunu günceller.
- **FR-3:** Şube nesnesinin `anlikMusteriSayisi` alanı Kafka akışıyla sürekli güncellenir; `dolulukOraniHesapla()` metodu o anki kapasite kullanım yüzdesini hesaplayarak optimizasyon motoruna besler.
- **FR-4:** Olay verileri **hafifletilmiş, hiyerarşik ve sıkıştırılmış JSON** formatında standardize edilir; her transfer paketi evrensel tekil kimlik, şube sicil kodu, aksiyon karakteristiği ve kesinleştirilmiş **ISO zaman damgası** içermek zorundadır.

### 3.2 Optimizasyon Motoru Tetikleme Koşulları (Trigger Conditions)

- **FR-5:** `OptimizasyonMotoru`, şube listesini sürekli tarayarak `darbogazTespitiYap()` / `darbogazHesapla(Sube s)` metoduyla **kapasite aşımı** (darboğaz) yaşayan şubeleri tespit eder.
- **FR-6:** Bir şubenin doluluk oranı kritik eşiği (kırmızı seviye) aştığında ve aynı anda atıl kapasiteli (yeşil seviye) bir kaynak şube mevcut olduğunda, motor **otonom** olarak tetiklenir.
- **FR-7:** Karar tamamen veriye dayalı ve insan inisiyatifinden bağımsız üretilir; örnek çıktı: *"Isparta Merkez şubesinden, S.D.Ü. Kampüs şubesine 1 Barista transfer edilmelidir."*

### 3.3 Transfer Öneri Mekanizması (Transfer Recommendation)

- **FR-8:** `transferOnerisiUret(Sube kaynak, Sube hedef)` metodu, atıl kapasiteli şubeden darboğaz yaşayan şubeye yönelik bir `TransferEmri` nesnesi oluşturur.
- **FR-9:** `TransferEmri` nesnesinin durumu yalnızca önceden tanımlı değerleri alabilir: **`BEKLIYOR` → `ONAYLANDI` / `REDDEDILDI` → `TAMAMLANDI`**.
- **FR-10:** `durumGuncelle(String yeniDurum)` metodu: (1) yeniDurum'u geçerli kısıt kümesine karşı doğrular, (2) nesnenin durumunu günceller, (3) PostgreSQL'e `UPDATE` ile yansıtır, (4) başarılıysa `KafkaVeriServisi.transferBildirimiGonder(this)` ile asenkron bildirim olayı fırlatır. Geçersiz durum → `IllegalArgumentException` (veritabanı ve Kafka işlemleri iptal edilir).
- **FR-11:** Onaylanan transfer ilgili şube müdürlerinin ekranlarına anlık bildirim olarak düşer; HTTP 200 OK dönüşünde backend, ilgili şubelere Kafka üzerinden transferin başladığına dair olay yollar.

### 3.4 Sistem Senaryoları (System Scenarios)

- **Senaryo 1 — Uç Birim Bağlantı Kaybı / Yedekli (Failover) Yoğunluk Tahmini:** Kapı sensörleri/IoT veri akışı kesildiğinde, `Health Check` servisi iletişim kaybını saptar. Sistem kapasite takibini durdurmaz; otonom olarak **yedekli (failover) moda** geçerek yalnızca kapalı devre üzerinden çalışan POS verilerini baz alır ve *"sipariş başına ortalama kalış süresi"* katsayısıyla tahmini doluluğu hesaplamaya devam eder. Yöneticiye "Sensör Bağlantı Hatası" uyarısı iletilir.
- **Senaryo 2 — Tarihsel Veri ile Proaktif Vardiya Planlaması:** Bölge Koordinatörü "Gelecek Hafta Kapasite Tahmini" modülünü çalıştırır; sistem geçen yılın aynı dönemine ait saatlik yoğunluk özetleri (cold data) ile mevcut Kafka trendini çapraz sorgular ve *"Cuma 16:00–23:00 vardiyası için Kampüs şubesinden Merkez şubeye 2 Barista ve 1 Yedek POS transfer edilmelidir"* şeklinde proaktif görev kartı üretir.

---

## 4. Fonksiyonel Olmayan Gereksinimler (Non-Functional Requirements)

### 4.1 Performans (Performance)

- **NFR-P1 (Uçtan Uca Gecikme):** Şubeden akan verinin Kafka üzerinden işlenip yönetici arayüzüne karar destek önerisi olarak düşmesi arasındaki **maksimum gecikme 2 saniye** ile sınırlandırılmıştır (değiştirilemez operasyonel kısıt).
- **NFR-P2 (Dashboard Güncellemesi):** Gösterge panelindeki "Şube Doluluk Oranları" ilerleme çubukları Kafka akışıyla **saniyede bir** tazelenir; arayüz yenilenme süresi (ağ gecikmeleri hariç) **< 2 saniye** olmalıdır.
- **NFR-P3 (Düşük Gecikmeli İşleme):** Java 21 **Virtual Threads (Project Loom)** mimarisi sayesinde binlerce eşzamanlı POS/PDKS veri akışı, CPU/RAM kaynakları minimize edilerek düşük gecikmeyle işlenir.
- **NFR-P4 (Mimari İzolasyon):** Yüksek işlem hacminde veritabanı kilitlenmelerini (deadlock) önlemek için olay odaklı anlık okumalar ile tarihsel veri yazma işlemleri **mimari düzeyde farklı şemalarda izole** edilmiştir.

### 4.2 Güvenlik ve Gizlilik (Security & Privacy)

- **NFR-S1 (Kimlik Doğrulama):** Oturum yönetimi **durumsuz (stateless)**, zaman aşımı özellikli **JSON Web Token (JWT)** ile sağlanır. Token istemcide güvenli depolama alanında (SessionStorage) tutulur ve sonraki tüm isteklerin `Authorization` başlığına eklenir.
- **NFR-S2 (Parola Saklama):** Kullanıcı parolaları, açık metin yerine **tuzlanmış (salted)** ve geri döndürülemez **güçlü hash** algoritmalarıyla saklanır.
- **NFR-S3 (Taşıma Güvenliği):** Tüm veri trafiği yalnızca **HTTPS** üzerinden **SSL/TLS 1.3** protokolleri ile şifreli gerçekleştirilir.
- **NFR-S4 (Veritabanı İzolasyonu):** Veritabanı portları kesinlikle dışa açılmaz; yalnızca Spring Boot backend servisinden gelen IP trafiğine izin veren **sıkı Firewall/Güvenlik Duvarı** kuralları uygulanır.
- **NFR-S5 (Enjeksiyon Koruması):** PostgreSQL sorgularında **prepared statements** (hazırlanmış ifadeler) kullanılır; XSS'e karşı tüm çıktılar **output encoding**'den geçirilir; OWASP Top 10 prensipleri geliştirme aşamasına entegre edilmiştir.
- **NFR-S6 (Şifreleme):** Hassas veriler ve ticari sırlar **AES-256** standardı ile kriptolanır.
- **NFR-S7 (Denetim İzi):** Tüm başarılı/başarısız girişler, parola sıfırlama ve transfer onayları; işlemi yapan kullanıcının **IP adresi ve zaman damgasıyla** kalıcı denetim loglarına işlenir.
- **NFR-S8 (Oturum Güvenliği):** 15 dakika işlemsizlikte oturum otomatik sonlandırılır (Session Timeout); çoklu hatalı girişte hesap geçici kilitlenir ve **reCAPTCHA** devreye girer. Kritik transfer/maliyet onaylarında **MFA** zorunludur.

### 4.3 Yasal Uyumluluk ve Kısıtlar (Legal Compliance & Constraints)

- **NFR-L1 (KVKK — 6698 Sayılı Kanun):** Veritabanında (PostgreSQL) ve Kafka mesaj kuyruğunda; personele veya müşteriye ait **TC Kimlik No, açık ad-soyad, telefon** gibi kişisel veriler **kesinlikle işlenmez**. Veriler kapasite ölçümü amacıyla **tamamen anonimleştirilmiş sayılar ve ID'ler** üzerinden yürütülür (veri anonimleştirme zorunluluğu).
- **NFR-L2 (İş Kanunu — 4857 Sayılı Kanun):** Karar destek motorunun ürettiği transfer bildirimleri, çalışan haklarını koruyan maddelere uymak zorundadır:
  - Günlük **maksimum mesai saatini** doldurmak üzere olan bir baristanın transferi sistem tarafından **engellenir**.
  - Yasal **mola süresi (ara dinlenmesi)** gelmiş personelin transferi engellenir.
  - Transfer edilecek personelin **yolda geçireceği süre, mesai süresinden sayılacak** şekilde lojistik algoritmasına dahil edilir.
- **NFR-L3 (Kültürel Takvim Kısıtları):** Optimizasyon algoritması; akademik takvimi (vize/final), Ramazan/iftar-sahur saatlerini ve dini/milli bayramları **istisnai durum (anomaly)** olarak algılayıp tahminlerini bu takvime göre ayarlamalıdır. Arayüz dili Türkçe ve kafe terminolojisine (Adisyon, Barista, Komi, Kasa, Z Raporu) uygun olmalıdır.

### 4.4 Güvenilirlik ve Erişilebilirlik (Reliability & Availability)

- **NFR-R1:** Sistem **7/24** (yılın 365 günü) kullanıma hazır olmalı; hedef uptime **%99.9 (Three Nines)** seviyesindedir.
- **NFR-R2 (Yüksek Erişilebilirlik):** PostgreSQL için **Primary–Replica (Always-On)** mimarisi; ana sunucu plansız durduğunda ikincil sunucu otomatik devreye girer (**failover**) — veri kaybı yaşanmaz.
- **NFR-R3 (Kafka Retention):** Kafka, ağ kesintilerinde veri kaybını önlemek için olayları diskte **en az 7 gün** yedekler (retention) ve sistem tekrar çevrimiçi olduğunda işlenmemiş verileri sırasıyla aktarır.
- **NFR-R4 (Hata Toleransı):** Uç şubelerde ağ koptuğunda **Local Buffering** (Yerel Tamponlama) ile veriler yerel diske asenkron yazılır, bağlantı dönünce Kafka topic'lerine **sıralı (sequential)** gönderilir.

---

## 5. Standartlar (Standards)

Sistem analizi, tasarımı ve geliştirilmesi uluslararası yazılım mühendisliği standartlarına uygun yürütülmüştür:

| Standart | Kapsam |
|---|---|
| **IEEE 830-1998** | Yazılım gereksinim dokümantasyonu (SRS) formatı ve içeriği bu standart temel alınarak hazırlanmıştır. |
| **UML 2.0 (OMG)** | Sistemin modellenmesi ve gereksinimlerin görselleştirilmesi (Use Case, Class, Sequence diyagramları) Object Management Group UML 2.0 notasyonu ile yapılmıştır. Çizimlerde Mermaid.js ve standart UML araçları kullanılmıştır. |
| **Oracle Java Coding Standards** | Backend yazılım geliştirme sürecinde "Java Coding Guidelines" ve kod kalite metrikleri sıkı şekilde uygulanmıştır. |
| **ISO/IEC 27001** | Veri iletimi ve sunucu erişimleri (Docker & Bulut VPS), Bilgi Güvenliği Yönetim Sistemi prensiplerine uygun şifreli protokoller (HTTPS, SSL/TLS) üzerinden gerçekleştirilir. |

---

## Ekler (Appendices)

- **Ek-A:** REST API Uç Noktaları Spesifikasyon Dokümanı (Swagger/OpenAPI formatı).
- **Ek-B:** Apache Kafka Topic, Producer ve Consumer yapılandırma dosyaları.
- **Ek-C:** PostgreSQL 16 LTS ilişkisel veritabanı fiziksel şema çıktıları (DDL scriptleri).
- **Ek-D:** Docker Compose orkestrasyon yapılandırma dosyası (`docker-compose.yml`).
- **Ek-E:** Sistem Yöneticisi ve Şube Müdürü kullanım kılavuzları.

> İlgili teknik mimari ve tasarım kararları için bkz. [context.md](context.md) · Proje yönetimi ve devir durumu için bkz. [agent_handoff.md](agent_handoff.md)
