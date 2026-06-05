/* Arabica Cafe — Dinamik Kaynak Yönetim Paneli (vanilla ES6 SPA)
   JWT sessionStorage (NFR-S1) · 15 dk idle çıkış (NFR-S8) · rol bazlı menü (RBAC §2)
   SignalR canlı doluluk/bildirim · Chart.js görselleştirme · Yönetim (Koordinatör)
   Demo amaçlıdır; 5 frozen sözleşme değiştirilmemiştir. */

const ROLLER = { KOORD: "BolgeKoordinatoru", MUDUR: "SubeMuduru" };
const MFA_SECRET = "JBSWY3DPEHPK3PXP"; // DEMO ONLY
const IDLE_MS = 15 * 60 * 1000;

let token = sessionStorage.getItem("arabica_token");
let rol = sessionStorage.getItem("arabica_rol");
let kullanici = sessionStorage.getItem("arabica_kullanici");
let subeId = sessionStorage.getItem("arabica_subeId");
let conn = null, idleTimer = null, baslatildi = false, sonDoluluk = [];
let panelTimer = null, barChart = null, trendChart = null;
const trendNoktalari = []; // {etiket, harita:{subeId:oran}}

const el = (id) => document.getElementById(id);
const esc = (s) => String(s ?? "").replace(/[&<>"]/g, (c) => ({ "&": "&amp;", "<": "&lt;", ">": "&gt;", '"': "&quot;" }[c]));
const seviyeRenk = (s) => ({ Yesil: "success", Sari: "warning", Kirmizi: "danger" }[s] || "secondary");
const seviyeHex = (s) => ({ Yesil: "#198754", Sari: "#ffc107", Kirmizi: "#dc3545" }[s] || "#6c757d");
const durumRenk = (d) => ({ Onaylandi: "success", Reddedildi: "danger", Tamamlandi: "primary", Bekliyor: "secondary" }[d] || "secondary");
const koordMu = () => rol === ROLLER.KOORD;

function jwtCoz(t) { try { return JSON.parse(atob(t.split(".")[1].replace(/-/g, "+").replace(/_/g, "/"))); } catch { return {}; } }

async function api(method, path, body, extra) {
    const headers = { "Content-Type": "application/json", ...(extra || {}) };
    if (token) headers["Authorization"] = "Bearer " + token;
    let res;
    try { res = await fetch(path, { method, headers, body: body !== undefined ? JSON.stringify(body) : undefined }); }
    catch { return { ok: false, status: 0, data: null }; }
    if (res.status === 401 && token) { cikis("Oturum geçersiz veya süresi doldu. Lütfen tekrar giriş yapın."); return { ok: false, status: 401, data: null }; }
    let data = null;
    if ((res.headers.get("content-type") || "").includes("json")) { try { data = await res.json(); } catch { } }
    return { ok: res.ok, status: res.status, data };
}

/* ---------- auth ---------- */
async function girisYap(k, s) {
    const r = await api("POST", "/api/v1/auth/login", { kullaniciAdi: k, sifre: s });
    if (!r.ok) return false;
    token = r.data.token; rol = r.data.rol; kullanici = k;
    subeId = jwtCoz(token)["subeId"] ?? "";
    sessionStorage.setItem("arabica_token", token);
    sessionStorage.setItem("arabica_rol", rol);
    sessionStorage.setItem("arabica_kullanici", kullanici);
    sessionStorage.setItem("arabica_subeId", subeId);
    uygulamaBaslat();
    return true;
}

function cikis(mesaj) {
    token = rol = kullanici = subeId = null;
    sessionStorage.clear();
    if (conn) { try { conn.stop(); } catch { } conn = null; }
    if (idleTimer) clearTimeout(idleTimer);
    baslatildi = false;
    if (mesaj) toast(mesaj, "warning");
    location.hash = "#/login";
    render();
}

function uygulamaBaslat() {
    if (baslatildi) return;
    baslatildi = true;
    signalRBaglan();
    const sifirla = () => { if (idleTimer) clearTimeout(idleTimer); idleTimer = setTimeout(() => cikis("Oturumunuz 15 dk işlemsizlik nedeniyle sonlandırıldı."), IDLE_MS); };
    ["mousemove", "keydown", "click", "scroll"].forEach((e) => document.addEventListener(e, sifirla, { passive: true }));
    sifirla();
}

/* ---------- SignalR ---------- */
async function signalRBaglan() {
    conn = new signalR.HubConnectionBuilder().withUrl("/hubs/doluluk", { accessTokenFactory: () => token }).withAutomaticReconnect().build();
    conn.on("DolulukGuncellendi", (liste) => { sonDoluluk = liste; canliGoster(true); if (location.hash.startsWith("#/panel")) { dolulukKartlariCiz(liste); grafikleriGuncelle(liste); } });
    conn.on("TransferBildirimi", (b) => { toast(`Transfer #${b.transferId}: ${b.kaynakSubeId}→${b.hedefSubeId} · ${b.durum}`, b.durum === "Reddedildi" ? "danger" : "info"); if (location.hash.startsWith("#/transferler")) transferlerYukle(); });
    conn.onreconnecting(() => canliGoster(false));
    conn.onreconnected(() => canliGoster(true));
    try { await conn.start(); canliGoster(true); } catch { canliGoster(false); }
}
function canliGoster(ok) { const b = el("canli"); if (b) { b.className = "badge bg-" + (ok ? "success" : "secondary"); b.textContent = ok ? "canlı" : "çevrimdışı"; } }

function toast(msg, tip = "info") {
    const d = document.createElement("div");
    d.className = `toast align-items-center text-bg-${tip} border-0 show`;
    d.innerHTML = `<div class="d-flex"><div class="toast-body">${esc(msg)}</div><button class="btn-close btn-close-white me-2 m-auto" onclick="this.closest('.toast').remove()"></button></div>`;
    el("toastlar").appendChild(d);
    setTimeout(() => d.remove(), 6000);
}

/* ---------- router ---------- */
function render() {
    temizle();
    const h = location.hash || "#/panel";
    if (!token) { if (h !== "#/login") { location.hash = "#/login"; return; } renderLogin(); return; }
    if (h === "#/login") { location.hash = "#/panel"; return; }
    uygulamaBaslat();
    cerceveCiz(h);
    const yonetimMi = h.startsWith("#/yonetim/") || h.startsWith("#/raporlar");
    if (yonetimMi && !koordMu()) { contentYaz('<div class="alert alert-danger">Bu sayfaya erişim yetkiniz yok (yalnızca Bölge Koordinatörü).</div>'); return; }

    if (h.startsWith("#/panel")) renderPanel();
    else if (h.startsWith("#/transferler")) renderTransferler();
    else if (h.startsWith("#/sube/")) renderSubeDetay(h.split("/")[2]);
    else if (h.startsWith("#/raporlar")) renderRaporlar();
    else if (h.startsWith("#/kvkk")) renderKvkk();
    else if (h.startsWith("#/yonetim/subeler")) renderYonetimSubeler();
    else if (h.startsWith("#/yonetim/personel")) renderYonetimPersonel();
    else if (h.startsWith("#/yonetim/transfer")) renderYonetimTransfer();
    else if (h.startsWith("#/yonetim/optimizasyon")) renderYonetimOptimizasyon();
    else if (h.startsWith("#/yonetim/denetim")) renderYonetimDenetim();
    else renderPanel();
}
window.addEventListener("hashchange", render);
window.addEventListener("load", render);

function temizle() {
    if (panelTimer) { clearInterval(panelTimer); panelTimer = null; }
    if (barChart) { barChart.destroy(); barChart = null; }
    if (trendChart) { trendChart.destroy(); trendChart = null; }
}

function menuHtml(active) {
    let m = `<a class="nav-link ${active.startsWith("#/panel") ? "active" : ""}" href="#/panel">📊 Panel</a>
             <a class="nav-link ${active.startsWith("#/transferler") ? "active" : ""}" href="#/transferler">🔁 Transferler</a>`;
    if (koordMu()) { // RBAC §2: yalnızca Koordinatör — Müdür için DOM'a EKLENMEZ
        m += `<a class="nav-link ${active.startsWith("#/raporlar") ? "active" : ""}" href="#/raporlar">📈 Raporlar</a>
              <div class="menu-baslik">Yönetim</div>
              <a class="nav-link ${active.startsWith("#/yonetim/subeler") ? "active" : ""}" href="#/yonetim/subeler">🏪 Şubeler</a>
              <a class="nav-link ${active.startsWith("#/yonetim/personel") ? "active" : ""}" href="#/yonetim/personel">👤 Personel</a>
              <a class="nav-link ${active.startsWith("#/yonetim/transfer") ? "active" : ""}" href="#/yonetim/transfer">📦 Manuel Transfer</a>
              <a class="nav-link ${active.startsWith("#/yonetim/optimizasyon") ? "active" : ""}" href="#/yonetim/optimizasyon">⚙️ Optimizasyon</a>
              <a class="nav-link ${active.startsWith("#/yonetim/denetim") ? "active" : ""}" href="#/yonetim/denetim">🛡️ Denetim Logları</a>
              <div class="menu-baslik">&nbsp;</div>`;
    }
    m += `<a class="nav-link ${active.startsWith("#/kvkk") ? "active" : ""}" href="#/kvkk">⚖️ KVKK / Uyumluluk</a>`;
    return m;
}

function cerceveCiz(active) {
    el("app").innerHTML = `
      <nav class="navbar navbar-dark bg-dark px-3">
        <span class="navbar-brand">☕ Arabica Kaynak Yönetimi</span>
        <div class="d-flex align-items-center gap-2">
          <span class="badge bg-info">${esc(koordMu() ? "Bölge Koordinatörü" : "Şube Müdürü")}</span>
          <span class="text-light small">${esc(kullanici)}</span>
          <button class="btn btn-sm btn-outline-light" onclick="cikis()">Çıkış</button>
        </div>
      </nav>
      <div class="container-fluid"><div class="row">
        <div class="col-md-2 sidebar bg-light border-end p-2"><div class="nav flex-column">${menuHtml(active)}</div></div>
        <div class="col-md-10 p-3" id="content"></div>
      </div></div>`;
}
function contentYaz(html) { const c = el("content"); if (c) c.innerHTML = html; }

/* ---------- login ---------- */
function renderLogin() {
    el("app").innerHTML = `
      <div class="d-flex align-items-center justify-content-center" style="min-height:100vh">
        <div class="card shadow" style="width:390px">
          <div class="card-body">
            <h4 class="text-center mb-1">☕ Arabica Cafe</h4>
            <p class="text-muted text-center small">Dinamik Kaynak Yönetim Sistemi</p>
            <label class="form-label">Kullanıcı adı</label>
            <input id="gk" class="form-control mb-2" value="tunahan.basar" />
            <label class="form-label">Şifre</label>
            <input id="gs" type="password" class="form-control mb-3" value="Arabica.2026!" />
            <button id="gbtn" class="btn btn-primary w-100">Giriş Yap</button>
            <div id="ghata" class="text-danger small mt-2"></div>
            <hr />
            <div class="small text-muted">Demo: <code>tunahan.basar</code> (Koordinatör) · <code>sinan.say</code> (Müdür) · şifre <code>Arabica.2026!</code></div>
          </div>
        </div>
      </div>`;
    el("gbtn").onclick = async () => {
        el("ghata").textContent = "";
        const ok = await girisYap(el("gk").value, el("gs").value);
        if (ok) { location.hash = "#/panel"; render(); } else el("ghata").textContent = "Giriş başarısız (kullanıcı adı/şifre hatalı).";
    };
    el("gs").addEventListener("keydown", (e) => { if (e.key === "Enter") el("gbtn").click(); });
}

/* ---------- panel + charts ---------- */
async function renderPanel() {
    contentYaz(`
      <div class="d-flex justify-content-between align-items-center mb-3">
        <h5 class="m-0">Panel — ${koordMu() ? "Tüm Şubeler" : "Şubem"} <span id="canli" class="badge bg-secondary">çevrimdışı</span></h5>
        ${koordMu() ? `<button id="simBtn" class="btn btn-sm btn-warning">⚡ POS yükü simüle et (40)</button>` : ""}
      </div>
      <div id="statRow" class="row g-2 mb-3"></div>
      <div id="latRow" class="mb-3"></div>
      <div class="row g-3 mb-3">
        <div class="col-lg-6"><div class="card h-100"><div class="card-body"><h6 class="text-muted">Şube Doluluk (%)</h6><canvas id="barChart"></canvas></div></div></div>
        <div class="col-lg-6"><div class="card h-100"><div class="card-body"><h6 class="text-muted">Doluluk Trendi (canlı)</h6><canvas id="trendChart"></canvas></div></div></div>
      </div>
      <div id="kartlar" class="row g-3"></div>`);
    canliGoster(conn && conn.state === "Connected");
    if (koordMu()) el("simBtn").onclick = simuleEt;
    grafikleriHazirla();
    await panelYenile();
    panelTimer = setInterval(panelYenile, 5000); // otomatik yenileme
}

async function panelYenile() {
    const ozet = await api("GET", "/api/v1/ozet");
    if (ozet.ok) statKartlariCiz(ozet.data);
    let liste = [];
    if (koordMu()) { const r = await api("GET", "/api/v1/sube/doluluk"); if (r.ok) liste = r.data; }
    else { const r = await api("GET", `/api/v1/sube/${subeId}/detay`); if (r.ok) liste = [{ subeId: r.data.subeId, ad: r.data.ad, dolulukOrani: r.data.dolulukOrani, seviye: r.data.seviye }]; }
    sonDoluluk = liste;
    dolulukKartlariCiz(liste);
    grafikleriGuncelle(liste);
}

function statKartlariCiz(o) {
    const kartlar = [
        { e: "Şube", d: o.subeSayisi, r: "primary" },
        { e: "Atıl (Yeşil)", d: o.atilSube, r: "success" },
        { e: "Darboğaz (Kırmızı)", d: o.darbogazSube, r: "danger" },
        { e: "Bekleyen Transfer", d: o.bekleyenTransfer, r: "info" },
        { e: "Ort. Gecikme (ms)", d: Math.round(o.ortalamaGecikmeMs), r: "secondary" }
    ];
    const row = el("statRow"); if (!row) return;
    if (!row.dataset.kuruldu) {
        row.innerHTML = kartlar.map((k, i) => `<div class="col"><div class="card stat-card text-center border-${k.r}"><div class="card-body py-2">
            <div class="deger text-${k.r}" id="stat${i}">0</div><div class="etiket">${k.e}</div></div></div></div>`).join("");
        row.dataset.kuruldu = "1";
    }
    kartlar.forEach((k, i) => sayacAnimasyon(el("stat" + i), k.d));
}
function sayacAnimasyon(elm, hedef) {
    if (!elm) return;
    const bas = parseInt(elm.textContent) || 0;
    if (bas === hedef) return;
    const adim = (hedef - bas) / 15; let n = bas, k = 0;
    const t = setInterval(() => { k++; n += adim; if (k >= 15) { n = hedef; clearInterval(t); } elm.textContent = Math.round(n); }, 20);
}

function grafikleriHazirla() {
    const bc = el("barChart"), tc = el("trendChart");
    if (bc) barChart = new Chart(bc, { type: "bar", data: { labels: [], datasets: [{ label: "Doluluk %", data: [], backgroundColor: [] }] }, options: { responsive: true, scales: { y: { beginAtZero: true, suggestedMax: 100 } }, plugins: { legend: { display: false } } } });
    if (tc) trendChart = new Chart(tc, { type: "line", data: { labels: [], datasets: [] }, options: { responsive: true, animation: false, scales: { y: { beginAtZero: true, suggestedMax: 100 } } } });
}
function grafikleriGuncelle(liste) {
    if (barChart) {
        barChart.data.labels = liste.map((s) => s.ad);
        barChart.data.datasets[0].data = liste.map((s) => s.dolulukOrani);
        barChart.data.datasets[0].backgroundColor = liste.map((s) => seviyeHex(s.seviye));
        barChart.update();
    }
    if (trendChart) {
        const etiket = new Date().toLocaleTimeString("tr-TR");
        trendNoktalari.push({ etiket, harita: Object.fromEntries(liste.map((s) => [s.subeId, s.dolulukOrani])) });
        while (trendNoktalari.length > 15) trendNoktalari.shift();
        const renkler = ["#0d6efd", "#198754", "#fd7e14", "#6f42c1", "#20c997"];
        trendChart.data.labels = trendNoktalari.map((p) => p.etiket);
        trendChart.data.datasets = liste.map((s, i) => ({ label: s.ad, data: trendNoktalari.map((p) => p.harita[s.subeId] ?? null), borderColor: renkler[i % renkler.length], tension: .3, fill: false }));
        trendChart.update();
    }
}
function dolulukKartlariCiz(liste) {
    const k = el("kartlar"); if (!k) return;
    k.innerHTML = liste.map((s) => `
      <div class="col-md-4"><div class="card h-100"><div class="card-body">
        <div class="d-flex justify-content-between"><h6>${esc(s.ad)}</h6><span class="badge bg-${seviyeRenk(s.seviye)}">${s.seviye}</span></div>
        <div class="progress my-2"><div class="progress-bar bg-${seviyeRenk(s.seviye)}" style="width:${Math.min(100, s.dolulukOrani)}%">%${s.dolulukOrani}</div></div>
        <div class="small text-muted">Aktif personel: ${s.aktifPersonelSayisi ?? "—"} · Maks. kapasite: ${s.maksimumKapasite ?? "—"}</div>
        <a class="btn btn-sm btn-outline-secondary mt-2" href="#/sube/${s.subeId}">Şube detayı</a>
      </div></div></div>`).join("");
}
async function simuleEt() {
    toast("POS yükü Kafka'ya gönderiliyor...", "secondary");
    const r = await api("POST", "/api/v1/demo/besle?adet=40");
    if (!r.ok) { toast("Simülasyon başarısız.", "danger"); return; }
    await new Promise((x) => setTimeout(x, 1500));
    const m = await api("GET", "/api/v1/metrik/gecikme");
    if (m.ok) {
        const o = m.data, ok = o.esikAsanSayisi === 0;
        el("latRow").innerHTML = `<div class="alert alert-${ok ? "success" : "danger"} mb-0 d-flex justify-content-between align-items-center">
          <span>Uçtan uca gecikme — ort. <b>${o.ortalamaMs} ms</b> · p95 <b>${o.p95Ms} ms</b> · örnek ${o.adet} · 2 sn aşan ${o.esikAsanSayisi}</span>
          <span class="badge bg-${ok ? "success" : "danger"} fs-6">${ok ? "≤ 2 sn ✓ (NFR-P1)" : "2 sn aşıldı"}</span></div>`;
    }
    await panelYenile();
}

/* ---------- transferler ---------- */
async function renderTransferler() {
    contentYaz(`<h5 class="mb-3">Transfer Önerileri</h5><div id="trBody">Yükleniyor...</div>`);
    await transferlerYukle();
}
async function transferlerYukle() {
    const r = await api("GET", "/api/v1/transfer/oneriler");
    const body = el("trBody"); if (!body) return;
    if (!r.ok) { body.innerHTML = '<div class="alert alert-warning">Liste alınamadı.</div>'; return; }
    if (!r.data.length) { body.innerHTML = '<div class="alert alert-info">Bekleyen transfer önerisi yok.</div>'; return; }
    body.innerHTML = `<table class="table table-hover align-middle"><thead><tr><th>#</th><th>Kaynak → Hedef</th><th>Tip</th><th>Adet</th><th>Durum</th><th></th></tr></thead><tbody>${
        r.data.map((t) => `<tr><td>${t.transferId}</td><td>${t.kaynakSubeId} → ${t.hedefSubeId}</td><td>${t.tip}</td><td>${t.adet}</td>
          <td><span class="badge bg-${durumRenk(t.durum)}">${t.durum}</span></td>
          <td><button class="btn btn-sm btn-success" onclick="onaylaAc(${t.transferId})">Onayla</button>
              <button class="btn btn-sm btn-outline-danger" onclick="reddet(${t.transferId})">Reddet</button></td></tr>`).join("")
    }</tbody></table>`;
}
function onaylaAc(id) {
    document.body.insertAdjacentHTML("beforeend", `
      <div class="modal fade show" id="mfaModal" style="display:block;background:rgba(0,0,0,.5)">
        <div class="modal-dialog"><div class="modal-content">
          <div class="modal-header"><h5 class="modal-title">Transfer #${id} — MFA Onayı</h5><button class="btn-close" onclick="modalKapat()"></button></div>
          <div class="modal-body">
            <p class="small text-muted">Kritik onay için 6 haneli TOTP (MFA) kodu gereklidir.</p>
            <input id="mfaKod" class="form-control mb-2" placeholder="6 haneli kod" maxlength="6" inputmode="numeric" />
            <button class="btn btn-sm btn-outline-secondary" onclick="mfaDemoDoldur()">🔑 Demo MFA kodu üret</button>
            <div class="form-text">Demo amaçlı istemci tarafı üretim; gerçek dağıtımda authenticator uygulamasından girilir.</div>
            <div id="mfaHata" class="text-danger small mt-2"></div>
          </div>
          <div class="modal-footer"><button class="btn btn-secondary" onclick="modalKapat()">Vazgeç</button><button class="btn btn-success" onclick="onayla(${id})">Onayla</button></div>
        </div></div>
      </div>`);
    el("mfaKod").focus();
}
function modalKapat() { document.querySelectorAll(".modal").forEach((m) => m.remove()); }
async function mfaDemoDoldur() { el("mfaKod").value = await totpUret(MFA_SECRET); }
async function onayla(id) {
    const kod = (el("mfaKod").value || "").trim();
    const r = await api("POST", "/api/v1/transfer/islem", { transferId: id, aksiyon: "ONAYLA" }, { "X-MFA-Code": kod });
    if (r.status === 200) {
        modalKapat();
        const durum = r.data?.durum || "Tamamlandi";
        toast(`Transfer #${id} ${durum.toLowerCase()}; personel sayıları güncellendi.`, "success");
        transferlerYukle();
        // counts refresh live via SignalR DolulukGuncellendi; if already on panel, pull immediately too.
        if (location.hash.startsWith("#/panel")) panelYenile();
    }
    else if (r.status === 401) { el("mfaHata").textContent = "MFA kodu geçersiz veya eksik."; }
    else if (r.status === 409) { modalKapat(); toast(r.data?.detail || r.data?.title || "Geçersiz işlem (örn. yetersiz personel veya zaten işlenmiş).", "danger"); transferlerYukle(); }
    else if (r.status === 403) { modalKapat(); toast("Bu transfer şubenizi ilgilendirmiyor (RBAC).", "danger"); }
    else { el("mfaHata").textContent = "Hata: HTTP " + r.status; }
}
async function reddet(id) {
    if (prompt("Reddetme gerekçesi (kayda işlenir):") === null) return;
    const r = await api("POST", "/api/v1/transfer/islem", { transferId: id, aksiyon: "REDDET" });
    if (r.status === 200) { toast(`Transfer #${id} reddedildi.`, "success"); transferlerYukle(); }
    else if (r.status === 403) toast("Bu transfer şubenizi ilgilendirmiyor (RBAC).", "danger");
    else if (r.status === 409) { toast("Geçersiz durum geçişi.", "danger"); transferlerYukle(); }
    else toast("Hata: HTTP " + r.status, "danger");
}

/* ---------- şube detay ---------- */
async function renderSubeDetay(id) {
    contentYaz(`<a href="#/panel" class="btn btn-sm btn-link px-0">← Panel</a><h5 class="mb-3">Şube #${esc(id)} Detayı</h5><div id="detayBody">Yükleniyor...</div>`);
    const r = await api("GET", `/api/v1/sube/${id}/detay`);
    const b = el("detayBody"); if (!b) return;
    if (r.status === 403) { b.innerHTML = '<div class="alert alert-danger">Yalnızca kendi şubenizi görüntüleyebilirsiniz (RBAC §2).</div>'; return; }
    if (r.status === 404) { b.innerHTML = '<div class="alert alert-warning">Şube bulunamadı.</div>'; return; }
    if (!r.ok) { b.innerHTML = '<div class="alert alert-warning">Veri alınamadı.</div>'; return; }
    const d = r.data;
    b.innerHTML = `<div class="card"><div class="card-body">
        <h6>${esc(d.ad)} <span class="badge bg-${seviyeRenk(d.seviye)}">${d.seviye}</span></h6>
        <div class="progress my-2"><div class="progress-bar bg-${seviyeRenk(d.seviye)}" style="width:${Math.min(100, d.dolulukOrani)}%">%${d.dolulukOrani}</div></div>
        <p class="small text-muted mb-0">Operasyonel durum: ${d.seviye}. Personel (KVKK: yalnız anonim ID): ${d.personeller.length ? d.personeller.map((p) => "#" + p.personelId + " " + esc(p.takmaAd)).join(", ") : "—"}</p>
      </div></div>`;
}

/* ---------- raporlar (Koordinatör) ---------- */
async function renderRaporlar() {
    contentYaz(`<h5 class="mb-3">Raporlar — Transfer Geçmişi</h5><div id="rapBody">Yükleniyor...</div>`);
    const r = await api("GET", "/api/v1/transfer/gecmis");
    const b = el("rapBody"); if (!b) return;
    if (!r.ok) { b.innerHTML = '<div class="alert alert-warning">Alınamadı.</div>'; return; }
    if (!r.data.length) { b.innerHTML = '<div class="alert alert-info">Kayıt yok.</div>'; return; }
    b.innerHTML = `<table class="table table-sm table-striped align-middle"><thead><tr><th>#</th><th>Kaynak→Hedef</th><th>Tip</th><th>Adet</th><th>Durum</th><th>Gerekçe</th><th>Oluşturulma</th></tr></thead><tbody>${
        r.data.map((t) => `<tr><td>${t.transferId}</td><td>${t.kaynakSubeId}→${t.hedefSubeId}</td><td>${t.tip}</td><td>${t.adet}</td>
          <td><span class="badge bg-${durumRenk(t.durum)}">${t.durum}</span></td><td class="small">${esc(t.redGerekcesi || "—")}</td>
          <td class="small">${new Date(t.olusturulmaZamani).toLocaleString("tr-TR")}</td></tr>`).join("")
    }</tbody></table>`;
}

/* ---------- YÖNETİM: Şubeler ---------- */
async function aktifSubeler() { const r = await api("GET", "/api/v1/admin/sube"); return r.ok ? r.data.filter((s) => s.aktif) : []; }

async function renderYonetimSubeler() {
    contentYaz(`<div class="d-flex justify-content-between mb-3"><h5 class="m-0">Yönetim — Şubeler</h5><button class="btn btn-sm btn-primary" onclick="subeModalAc()">+ Yeni Şube</button></div><div id="subeBody">Yükleniyor...</div>`);
    await subeListeYukle();
}
async function subeListeYukle() {
    const r = await api("GET", "/api/v1/admin/sube");
    const b = el("subeBody"); if (!b) return;
    if (!r.ok) { b.innerHTML = '<div class="alert alert-warning">Alınamadı.</div>'; return; }
    b.innerHTML = `<table class="table table-hover align-middle"><thead><tr><th>#</th><th>Ad</th><th>Maks. Kap.</th><th>Anlık Müşteri</th><th>Aktif Personel</th><th>Durum</th><th></th></tr></thead><tbody>${
        r.data.map((s) => `<tr><td>${s.subeId}</td><td>${esc(s.ad)}</td><td>${s.maksimumKapasite}</td><td>${s.anlikMusteriSayisi}</td><td>${s.aktifPersonelSayisi}</td>
          <td><span class="badge bg-${s.aktif ? "success" : "secondary"}">${s.aktif ? "Aktif" : "Pasif"}</span></td>
          <td><button class="btn btn-sm btn-outline-secondary" onclick='subeModalAc(${JSON.stringify(s)})'>Düzenle</button>
              ${s.aktif
                  ? `<button class="btn btn-sm btn-outline-danger" onclick="subePasiflestir(${s.subeId})">Pasifleştir</button>`
                  : `<button class="btn btn-sm btn-outline-success" onclick="subeAktiflestir(${s.subeId})">Aktifleştir</button>`}</td></tr>`).join("")
    }</tbody></table>`;
}
function subeModalAc(s) {
    const d = s || { subeId: 0, ad: "", maksimumKapasite: 100, aktifPersonelSayisi: 0 };
    document.body.insertAdjacentHTML("beforeend", `
      <div class="modal fade show" id="subeModal" style="display:block;background:rgba(0,0,0,.5)"><div class="modal-dialog"><div class="modal-content">
        <div class="modal-header"><h5 class="modal-title">${d.subeId ? "Şube Düzenle #" + d.subeId : "Yeni Şube"}</h5><button class="btn-close" onclick="modalKapat()"></button></div>
        <div class="modal-body">
          <label class="form-label">Ad</label><input id="sAd" class="form-control mb-2" value="${esc(d.ad)}">
          <label class="form-label">Maksimum kapasite</label><input id="sKap" type="number" class="form-control mb-2" value="${d.maksimumKapasite}">
          <label class="form-label">Aktif personel sayısı</label><input id="sPer" type="number" class="form-control" value="${d.aktifPersonelSayisi}">
          <div id="sHata" class="text-danger small mt-2"></div>
        </div>
        <div class="modal-footer"><button class="btn btn-secondary" onclick="modalKapat()">Vazgeç</button><button class="btn btn-primary" onclick="subeKaydet(${d.subeId})">Kaydet</button></div>
      </div></div></div>`);
}
async function subeKaydet(id) {
    const body = { ad: el("sAd").value, maksimumKapasite: parseInt(el("sKap").value), aktifPersonelSayisi: parseInt(el("sPer").value) };
    const r = id ? await api("PUT", `/api/v1/admin/sube/${id}`, body) : await api("POST", "/api/v1/admin/sube", body);
    if (r.ok) { modalKapat(); toast(id ? "Şube güncellendi." : "Şube oluşturuldu.", "success"); subeListeYukle(); }
    else el("sHata").textContent = r.status === 400 ? "Doğrulama hatası (ad boş olamaz, kapasite > 0)." : "Hata: HTTP " + r.status;
}
async function subePasiflestir(id) {
    if (!confirm("Şube pasifleştirilsin mi? (doluluk/optimizasyon dışı kalır, geçmiş korunur)")) return;
    const r = await api("PATCH", `/api/v1/admin/sube/${id}/pasiflestir`);
    if (r.ok) { toast("Şube pasifleştirildi.", "success"); subeListeYukle(); } else toast("Hata: HTTP " + r.status, "danger");
}
async function subeAktiflestir(id) {
    const r = await api("PATCH", `/api/v1/admin/sube/${id}/aktiflestir`);
    if (r.ok) { toast("Şube aktifleştirildi. (doluluk/optimizasyona geri döner)", "success"); subeListeYukle(); } else toast("Hata: HTTP " + r.status, "danger");
}

/* ---------- YÖNETİM: Personel (KVKK) ---------- */
async function renderYonetimPersonel() {
    const subeler = await aktifSubeler();
    contentYaz(`
      <h5 class="mb-3">Yönetim — Personel Ekle</h5>
      <div class="card kvkk-vurgu mb-3"><div class="card-body py-2 small">
        <strong>⚖️ KVKK:</strong> Yalnızca <strong>takma ad</strong> ve şube toplanır.
        <span class="text-success">TC Kimlik No / ad-soyad / telefon <u>istenmez ve saklanmaz</u>.</span> Kayıtlar anonim sayısal ID ile tutulur.
      </div></div>
      <div class="card" style="max-width:520px"><div class="card-body">
        <label class="form-label">Şube</label>
        <select id="pSube" class="form-select mb-2">${subeler.map((s) => `<option value="${s.subeId}">${esc(s.ad)} (#${s.subeId})</option>`).join("")}</select>
        <label class="form-label">Takma ad</label><input id="pTakma" class="form-control mb-2" placeholder="örn. Barista-A1">
        <label class="form-label">Tip</label><select id="pTip" class="form-select mb-3"><option>Barista</option></select>
        <button class="btn btn-primary" onclick="personelEkle()">Personel Ekle</button>
        <div id="pSonuc" class="small mt-2"></div>
      </div></div>`);
}
async function personelEkle() {
    const body = { subeId: parseInt(el("pSube").value), takmaAd: el("pTakma").value, tip: el("pTip").value };
    const r = await api("POST", "/api/v1/admin/personel", body);
    if (r.ok) { toast(`Personel eklendi: #${r.data.personelId} ${r.data.takmaAd}`, "success"); el("pSonuc").innerHTML = `<span class="text-success">Eklendi (anonim ID #${r.data.personelId}). KVKK: gönderilen veri yalnız {subeId, takmaAd, tip}.</span>`; el("pTakma").value = ""; }
    else el("pSonuc").innerHTML = `<span class="text-danger">${r.status === 400 ? "Takma ad boş olamaz." : r.status === 404 ? "Aktif şube bulunamadı." : "Hata: HTTP " + r.status}</span>`;
}

/* ---------- YÖNETİM: Manuel Transfer ---------- */
async function renderYonetimTransfer() {
    const subeler = await aktifSubeler();
    const opt = subeler.map((s) => `<option value="${s.subeId}">${esc(s.ad)} (#${s.subeId})</option>`).join("");
    contentYaz(`
      <h5 class="mb-3">Yönetim — Manuel Transfer Emri</h5>
      <p class="small text-muted">Koordinatör manuel emir oluşturur (SRS §2.1). Factory Method ile üretilir, outbox→ESB üzerinden gerçek-zamanlı bildirim doğar ve <a href="#/transferler">Transferler</a>'de belirir.</p>
      <div class="card" style="max-width:560px"><div class="card-body row g-2">
        <div class="col-6"><label class="form-label">Kaynak şube</label><select id="mKaynak" class="form-select">${opt}</select></div>
        <div class="col-6"><label class="form-label">Hedef şube</label><select id="mHedef" class="form-select">${opt}</select></div>
        <div class="col-6"><label class="form-label">Adet</label><input id="mAdet" type="number" class="form-control" value="1" min="1"></div>
        <div class="col-6"><label class="form-label">Tip</label><select id="mTip" class="form-select"><option>Personel</option><option>Ekipman</option></select></div>
        <div class="col-12"><button class="btn btn-primary mt-2" onclick="manuelTransfer()">Transfer Emri Oluştur</button></div>
        <div id="mSonuc" class="small mt-1"></div>
      </div></div>`);
    if (subeler.length > 1) el("mHedef").selectedIndex = 1;
}
async function manuelTransfer() {
    const body = { kaynakSubeId: parseInt(el("mKaynak").value), hedefSubeId: parseInt(el("mHedef").value), adet: parseInt(el("mAdet").value), tip: el("mTip").value };
    const r = await api("POST", "/api/v1/admin/transfer/manuel", body);
    if (r.ok) { toast(`Manuel transfer emri #${r.data.transferId} oluşturuldu (Bekliyor).`, "success"); el("mSonuc").innerHTML = `<span class="text-success">Emir #${r.data.transferId} oluşturuldu — Transferler ekranında onaya hazır.</span>`; }
    else el("mSonuc").innerHTML = `<span class="text-danger">${r.status === 400 ? "Kaynak ve hedef aynı olamaz / geçersiz alan." : r.status === 404 ? "Şube bulunamadı veya pasif." : "Hata: HTTP " + r.status}</span>`;
}

/* ---------- YÖNETİM: Optimizasyon + Strateji ---------- */
async function renderYonetimOptimizasyon() {
    contentYaz(`
      <h5 class="mb-3">Yönetim — Optimizasyon Motoru</h5>
      <div class="card mb-3"><div class="card-body">
        <h6>Aktif Strateji <span id="stratejiRozet" class="badge bg-info">—</span></h6>
        <p id="stratejiAciklama" class="small text-muted mb-2"></p>
        <div class="btn-group">
          <button class="btn btn-sm btn-outline-primary" onclick="stratejiAyarla('vize-final')">Vize-Final Sezonu</button>
          <button class="btn btn-sm btn-outline-primary" onclick="stratejiAyarla('yaz')">Yaz Dönemi</button>
          <button class="btn btn-sm btn-outline-secondary" onclick="stratejiAyarla('')">Takvim Varsayılanı</button>
        </div>
        <div class="form-text">Strateji deseni canlı: seçim, darboğaz eşiklerini çalışma-zamanında değiştirir.</div>
      </div></div>
      <button class="btn btn-warning mb-3" onclick="optimizasyonTetikle()">⚙️ Darboğazı tespit et & öneri üret</button>
      <div id="optSonuc"></div>`);
    await stratejiGoster();
}
async function stratejiGoster() {
    const r = await api("GET", "/api/v1/admin/strateji");
    if (r.ok) { el("stratejiRozet").textContent = r.data.aktifSezon; el("stratejiAciklama").textContent = r.data.aciklama; }
}
async function stratejiAyarla(ad) {
    const r = await api("POST", `/api/v1/admin/strateji?ad=${encodeURIComponent(ad)}`);
    if (r.ok) { toast(`Strateji: ${r.data.aktifSezon}`, "info"); el("stratejiRozet").textContent = r.data.aktifSezon; el("stratejiAciklama").textContent = r.data.aciklama; }
    else toast(r.status === 400 ? "Geçersiz strateji." : "Hata: HTTP " + r.status, "danger");
}
async function optimizasyonTetikle() {
    const r = await api("POST", "/api/v1/admin/optimizasyon/tetikle");
    const b = el("optSonuc"); if (!b) return;
    if (!r.ok) { b.innerHTML = '<div class="alert alert-warning">Hata.</div>'; return; }
    if (!r.data.length) { b.innerHTML = '<div class="alert alert-info">Bu strateji ile darboğaz/atıl eşleşmesi bulunamadı (öneri üretilmedi).</div>'; return; }
    toast(`${r.data.length} öneri üretildi.`, "success");
    b.innerHTML = `<div class="alert alert-success">Üretilen öneriler (Bekliyor):</div><ul class="list-group">${
        r.data.map((o) => `<li class="list-group-item">#${o.transferId}: ${o.kaynakSubeId} → ${o.hedefSubeId} · ${o.adet} ${o.tip} <span class="badge bg-secondary">${o.durum}</span></li>`).join("")
    }</ul>`;
}

/* ---------- YÖNETİM: Denetim Logları ---------- */
let denetimSayfa = 1;
async function renderYonetimDenetim() {
    denetimSayfa = 1;
    contentYaz(`<h5 class="mb-3">Yönetim — Denetim Logları</h5><div id="denetimBody">Yükleniyor...</div>
      <div class="mt-2"><button class="btn btn-sm btn-outline-secondary" onclick="denetimSayfaDegis(-1)">← Önceki</button>
      <span id="denetimSayfaNo" class="mx-2">1</span>
      <button class="btn btn-sm btn-outline-secondary" onclick="denetimSayfaDegis(1)">Sonraki →</button></div>`);
    await denetimYukle();
}
async function denetimYukle() {
    const r = await api("GET", `/api/v1/admin/denetim?sayfa=${denetimSayfa}&boyut=15`);
    const b = el("denetimBody"); if (!b) return;
    if (!r.ok) { b.innerHTML = '<div class="alert alert-warning">Alınamadı.</div>'; return; }
    if (!r.data.length) { b.innerHTML = '<div class="alert alert-info">Kayıt yok.</div>'; return; }
    b.innerHTML = `<table class="table table-sm table-striped align-middle"><thead><tr><th>Zaman</th><th>Aktör</th><th>IP</th><th>Eylem</th><th>Detay</th></tr></thead><tbody>${
        r.data.map((d) => `<tr><td class="small">${new Date(d.zaman).toLocaleString("tr-TR")}</td><td>${esc(d.aktor)}</td><td class="small">${esc(d.ipAdresi)}</td>
          <td><span class="badge bg-dark">${esc(d.eylem)}</span></td><td class="small">${esc(d.detay || "—")}</td></tr>`).join("")
    }</tbody></table>`;
}
function denetimSayfaDegis(d) { denetimSayfa = Math.max(1, denetimSayfa + d); el("denetimSayfaNo").textContent = denetimSayfa; denetimYukle(); }

/* ---------- KVKK ---------- */
async function renderKvkk() {
    contentYaz(`
      <h5 class="mb-3">KVKK / Uyumluluk Paneli</h5>
      <div class="row g-3">
        <div class="col-md-6"><div class="card h-100"><div class="card-body">
          <h6>KVKK (6698) — Veri Minimizasyonu</h6>
          <ul class="small mb-2">
            <li>Kişisel veri (TC, ad-soyad, telefon) <strong>saklanmaz</strong>.</li>
            <li>Personel ve şubeler yalnızca <strong>anonim sayısal ID</strong> ile referanslanır.</li>
            <li>Kimlik bilgileri ayrı, erişimi kısıtlı <code>kimlik</code> şemasında; parolalar <strong>PBKDF2</strong> (tuzlu) hash.</li>
            <li>Denetim logu: <strong>aktör + IP + zaman damgası</strong> (NFR-S7).</li>
          </ul>
          <button id="kvkkBtn" class="btn btn-sm btn-outline-primary">Örnek kayıt getir (yalnız ID kanıtı)</button>
          <pre id="kvkkOrnek" class="small bg-light p-2 mt-2 d-none"></pre>
        </div></div></div>
        <div class="col-md-6"><div class="card h-100"><div class="card-body">
          <h6>İş Kanunu (4857) — Muhafız Zinciri (Chain of Responsibility)</h6>
          <ul class="small mb-2">
            <li>Günlük azami mesai aşımı → transfer <strong>engellenir</strong>.</li>
            <li>Haftalık azami mesai aşımı → engellenir.</li>
            <li>Yasal mola (ara dinlenmesi) hak edilmişse → engellenir.</li>
            <li><strong>Yol süresi mesaiye dahildir.</strong></li>
          </ul>
          <h6 class="mt-3">Güvenlik</h6>
          <ul class="small mb-0">
            <li>JWT (stateless, 15 dk) + policy-tabanlı RBAC; menü rol bazlı.</li>
            <li>MFA (TOTP) kritik onayda (<code>X-MFA-Code</code>).</li>
            <li>AES-256 (Data Protection); DB portları dışa kapalı; HTTPS/TLS.</li>
          </ul>
        </div></div></div>
      </div>`);
    el("kvkkBtn").onclick = async () => {
        const r = await api("GET", "/api/v1/transfer/oneriler");
        const p = el("kvkkOrnek"); p.classList.remove("d-none");
        p.textContent = "GET /api/v1/transfer/oneriler →\n" + JSON.stringify(r.data, null, 2) + "\n\n↑ Yalnızca sayısal kimlikler; kişisel veri yok.";
    };
}

/* ---------- TOTP (demo) ---------- */
function base32Coz(s) {
    const a = "ABCDEFGHIJKLMNOPQRSTUVWXYZ234567"; let bits = "";
    for (const c of s.toUpperCase().replace(/=+$/, "")) { const i = a.indexOf(c); if (i >= 0) bits += i.toString(2).padStart(5, "0"); }
    const out = []; for (let i = 0; i + 8 <= bits.length; i += 8) out.push(parseInt(bits.substr(i, 8), 2));
    return new Uint8Array(out);
}
async function totpUret(secret) {
    const key = base32Coz(secret);
    const counter = Math.floor(Date.now() / 1000 / 30);
    const buf = new ArrayBuffer(8); new DataView(buf).setUint32(4, counter);
    const k = await crypto.subtle.importKey("raw", key, { name: "HMAC", hash: "SHA-1" }, false, ["sign"]);
    const sig = new Uint8Array(await crypto.subtle.sign("HMAC", k, buf));
    const o = sig[sig.length - 1] & 0x0f;
    const bin = ((sig[o] & 0x7f) << 24) | ((sig[o + 1] & 0xff) << 16) | ((sig[o + 2] & 0xff) << 8) | (sig[o + 3] & 0xff);
    return (bin % 1000000).toString().padStart(6, "0");
}
