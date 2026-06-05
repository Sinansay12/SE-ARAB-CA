/* Arabica Cafe — Dinamik Kaynak Yönetim Paneli (vanilla ES6 SPA)
   JWT sessionStorage (NFR-S1) · 15 dk idle çıkış (NFR-S8) · rol bazlı menü (RBAC §2)
   SignalR canlı doluluk/bildirim · Chart.js görselleştirme · Yönetim (Koordinatör)
   UI: Claude Design "Arabica Panel" espresso/krem tasarım sistemiyle yeniden kaplandı.
   Demo amaçlıdır; 5 frozen sözleşme değiştirilmemiştir. */

const ROLLER = { KOORD: "BolgeKoordinatoru", MUDUR: "SubeMuduru" };
const MFA_SECRET = "JBSWY3DPEHPK3PXP"; // DEMO ONLY
const IDLE_MS = 15 * 60 * 1000;

let token = sessionStorage.getItem("arabica_token");
let rol = sessionStorage.getItem("arabica_rol");
let kullanici = sessionStorage.getItem("arabica_kullanici");
let subeId = sessionStorage.getItem("arabica_subeId");
let conn = null, idleTimer = null, baslatildi = false, sonDoluluk = [];
let panelTimer = null, barChart = null, trendChart = null, saatTimer = null, kapali = false;
const trendNoktalari = []; // {etiket, harita:{subeId:oran}}

const el = (id) => document.getElementById(id);
const esc = (s) => String(s ?? "").replace(/[&<>"]/g, (c) => ({ "&": "&amp;", "<": "&lt;", ">": "&gt;", '"': "&quot;" }[c]));
const koordMu = () => rol === ROLLER.KOORD;
const rolAd = () => (koordMu() ? "Bölge Koordinatörü" : "Şube Müdürü");
const basHarf = (s) => esc(String(s ?? "").replace(/[._-]/g, " ").trim().split(" ").filter(Boolean).map((w) => w[0]).slice(0, 2).join("").toUpperCase() || "?");

const stCls = (s) => ({ Yesil: "st-green", Sari: "st-yellow", Kirmizi: "st-red" }[s] || "st-green");
const seviyeAd = (s) => ({ Yesil: "Normal", Sari: "Yoğun", Kirmizi: "Kritik" }[s] || esc(s));
const seviyeHex = (s) => ({ Yesil: "#5f8d5a", Sari: "#c79328", Kirmizi: "#bf5639" }[s] || "#7a6b5d");
const durumCls = (d) => ({ Bekliyor: "t-wait", Onaylandi: "t-ok", Reddedildi: "t-no", Tamamlandi: "t-done" }[d] || "t-wait");

/* ---------- ikonlar (minimal stroke SVG — tasarım setinden) ---------- */
const IKON = {
  cup: ["M17 8h1a4 4 0 1 1 0 8h-1", "M3 8h14v9a4 4 0 0 1-4 4H7a4 4 0 0 1-4-4z", "M6 1v3", "M10 1v3", "M14 1v3"],
  dashboard: ["M3 13h7V3H3z", "M14 21h7v-9h-7z", "M14 9h7V3h-7z", "M3 21h7v-5H3z"],
  branch: ["M4 9l1-4h14l1 4", "M4 9v11h16V9", "M9 20v-5h6v5", "M3 9h18"],
  staff: ["M16 21v-2a4 4 0 0 0-4-4H6a4 4 0 0 0-4 4v2", "M9 11a4 4 0 1 0 0-8 4 4 0 0 0 0 8z", "M22 21v-2a4 4 0 0 0-3-3.87", "M16 3.13a4 4 0 0 1 0 7.75"],
  transfer: ["M7 4 3 8l4 4", "M3 8h13a4 4 0 0 1 0 8h-1", "M17 20l4-4-4-4", "M21 16H8"],
  report: ["M3 3v18h18", "M7 15l3-4 3 2 4-6"],
  settings: ["M12 15a3 3 0 1 0 0-6 3 3 0 0 0 0 6z", "M19.4 15a1.65 1.65 0 0 0 .33 1.82l.06.06a2 2 0 1 1-2.83 2.83l-.06-.06a1.65 1.65 0 0 0-1.82-.33 1.65 1.65 0 0 0-1 1.51V21a2 2 0 0 1-4 0v-.09A1.65 1.65 0 0 0 9 19.4a1.65 1.65 0 0 0-1.82.33l-.06.06a2 2 0 1 1-2.83-2.83l.06-.06a1.65 1.65 0 0 0 .33-1.82 1.65 1.65 0 0 0-1.51-1H3a2 2 0 0 1 0-4h.09A1.65 1.65 0 0 0 4.6 9a1.65 1.65 0 0 0-.33-1.82l-.06-.06a2 2 0 1 1 2.83-2.83l.06.06a1.65 1.65 0 0 0 1.82.33H9a1.65 1.65 0 0 0 1-1.51V3a2 2 0 0 1 4 0v.09a1.65 1.65 0 0 0 1 1.51 1.65 1.65 0 0 0 1.82-.33l.06-.06a2 2 0 1 1 2.83 2.83l-.06.06a1.65 1.65 0 0 0-.33 1.82V9a1.65 1.65 0 0 0 1.51 1H21a2 2 0 0 1 0 4h-.09a1.65 1.65 0 0 0-1.51 1z"],
  shield: ["M12 22s8-4 8-10V5l-8-3-8 3v7c0 6 8 10 8 10z", "M9 12l2 2 4-4"],
  bell: ["M18 8a6 6 0 0 0-12 0c0 7-3 9-3 9h18s-3-2-3-9", "M13.73 21a2 2 0 0 1-3.46 0"],
  search: ["M11 19a8 8 0 1 0 0-16 8 8 0 0 0 0 16z", "M21 21l-4.35-4.35"],
  logout: ["M9 21H5a2 2 0 0 1-2-2V5a2 2 0 0 1 2-2h4", "M16 17l5-5-5-5", "M21 12H9"],
  user: ["M20 21v-2a4 4 0 0 0-4-4H8a4 4 0 0 0-4 4v2", "M12 11a4 4 0 1 0 0-8 4 4 0 0 0 0 8z"],
  check: ["M20 6 9 17l-5-5"],
  x: ["M18 6 6 18", "M6 6l12 12"],
  clock: ["M12 22a10 10 0 1 0 0-20 10 10 0 0 0 0 20z", "M12 6v6l4 2"],
  pos: ["M7 2h10a2 2 0 0 1 2 2v16a2 2 0 0 1-2 2H7a2 2 0 0 1-2-2V4a2 2 0 0 1 2-2z", "M9 6h6", "M9 10h6", "M9 18h2"],
  alert: ["M10.29 3.86 1.82 18a2 2 0 0 0 1.71 3h16.94a2 2 0 0 0 1.71-3L13.71 3.86a2 2 0 0 0-3.42 0z", "M12 9v4", "M12 17h.01"],
  bolt: ["M13 2 3 14h9l-1 8 10-12h-9z"],
  chevron: ["M9 18l6-6-6-6"],
  arrowRight: ["M5 12h14", "M12 5l7 7-7 7"],
  menu: ["M3 12h18", "M3 6h18", "M3 18h18"],
  keyboard: ["M2 6h20v12H2z", "M6 10h.01M10 10h.01M14 10h.01M18 10h.01M6 14h12"],
  signal: ["M2 20h.01", "M7 20v-4", "M12 20v-8", "M17 20V8", "M22 4v16"],
  pin: ["M20 10c0 6-8 12-8 12s-8-6-8-12a8 8 0 0 1 16 0z", "M12 12a2 2 0 1 0 0-4 2 2 0 0 0 0 4z"],
};
function ICO(name, size = 20, sw = 1.6) {
  const d = IKON[name]; if (!d) return "";
  const dolu = name === "bolt";
  const paths = d.map((p) => `<path d="${p}"/>`).join("");
  return `<svg width="${size}" height="${size}" viewBox="0 0 24 24" fill="${dolu ? "currentColor" : "none"}" stroke="currentColor" stroke-width="${dolu ? 0 : sw}" stroke-linecap="round" stroke-linejoin="round" aria-hidden="true">${paths}</svg>`;
}

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
  if (saatTimer) { clearInterval(saatTimer); saatTimer = null; }
  baslatildi = false;
  if (mesaj) toast(mesaj, "warning");
  location.hash = "#/login";
  render();
}

function uygulamaBaslat() {
  if (baslatildi) return;
  baslatildi = true;
  signalRBaglan();
  saatBaslat();
  const sifirla = () => { if (idleTimer) clearTimeout(idleTimer); idleTimer = setTimeout(() => cikis("Oturumunuz 15 dk işlemsizlik nedeniyle sonlandırıldı."), IDLE_MS); };
  ["mousemove", "keydown", "click", "scroll"].forEach((e) => document.addEventListener(e, sifirla, { passive: true }));
  // profil menüsü dışına tıklama + klavye kısayolları
  document.addEventListener("click", (e) => { const p = el("profile"); if (p && !p.contains(e.target)) profilKapat(); });
  document.addEventListener("keydown", (e) => {
    if (e.altKey && e.code === "KeyD") { e.preventDefault(); location.hash = "#/panel"; }
    if (e.altKey && e.code === "KeyT") { e.preventDefault(); location.hash = "#/transferler"; }
    if (e.key === "Escape") modalKapat();
  });
  sifirla();
}

function saatBaslat() {
  if (saatTimer) return;
  const tick = () => { const e = el("saat"); if (e) e.textContent = new Date().toLocaleTimeString("tr-TR", { hour: "2-digit", minute: "2-digit", second: "2-digit" }); };
  tick(); saatTimer = setInterval(tick, 1000);
}

/* ---------- SignalR ---------- */
async function signalRBaglan() {
  conn = new signalR.HubConnectionBuilder().withUrl("/hubs/doluluk", { accessTokenFactory: () => token }).withAutomaticReconnect().build();
  conn.on("DolulukGuncellendi", (liste) => { sonDoluluk = liste; canliGoster(true); if (location.hash.startsWith("#/panel")) { occListeCiz(liste); grafikleriGuncelle(liste); } });
  conn.on("TransferBildirimi", (b) => { toast(`Transfer #${b.transferId}: Şube ${b.kaynakSubeId}→${b.hedefSubeId} · ${b.durum}`, b.durum === "Reddedildi" ? "danger" : "info"); if (location.hash.startsWith("#/transferler")) transferlerYukle(); });
  conn.onreconnecting(() => canliGoster(false));
  conn.onreconnected(() => canliGoster(true));
  try { await conn.start(); canliGoster(true); } catch { canliGoster(false); }
}
function canliGoster(ok) {
  const t = el("canli"); if (t) t.textContent = ok ? "Kafka akışı · canlı" : "çevrimdışı";
  const d = el("canliDot"); if (d) d.className = "live-pulse" + (ok ? "" : " off");
  const tag = el("canliTag"); if (tag) { tag.className = "live-tag" + (ok ? "" : " off"); tag.innerHTML = `<span class="live-pulse sm${ok ? "" : " off"}" id="canliDot2"></span> ${ok ? "CANLI" : "ÇEVRİMDIŞI"}`; }
}

function toast(msg, tip = "info") {
  const map = { success: "ok", danger: "no", warning: "warn", info: "info", secondary: "info", ok: "ok", no: "no" };
  const k = map[tip] || "info";
  const d = document.createElement("div");
  d.className = `toast ${k}`;
  d.innerHTML = `<span class="toast-ico">${ICO(k === "ok" ? "check" : k === "no" ? "x" : "bell", 14)}</span><span>${esc(msg)}</span>`;
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
  if (yonetimMi && !koordMu()) { contentYaz(`<div class="screen"><div class="banner err">${ICO("alert", 16)} Bu sayfaya erişim yetkiniz yok (yalnızca Bölge Koordinatörü).</div></div>`); return; }

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

/* ---------- app shell (sidebar + topbar) ---------- */
function sayfaMeta(h) {
  if (h.startsWith("#/panel")) return ["Genel Bakış", koordMu() ? "Franchise ağı · canlı operasyon merkezi" : "Şubeniz · canlı doluluk"];
  if (h.startsWith("#/transferler")) return ["Transfer Onayları", "Optimizasyon motoru transfer emirleri"];
  if (h.startsWith("#/sube/")) return ["Şube Detayı", "Kapasite, POS ve personel durumu"];
  if (h.startsWith("#/raporlar")) return ["Raporlar", "Transfer geçmişi ve performans"];
  if (h.startsWith("#/yonetim/subeler")) return ["Şube Yönetimi", "Şube CRUD · pasifleştir / aktifleştir"];
  if (h.startsWith("#/yonetim/personel")) return ["Personel Yönetimi", "Anonim personel ekleme (KVKK)"];
  if (h.startsWith("#/yonetim/transfer")) return ["Manuel Transfer", "Koordinatör transfer emri"];
  if (h.startsWith("#/yonetim/optimizasyon")) return ["Optimizasyon Motoru", "Strateji ve darboğaz taraması"];
  if (h.startsWith("#/yonetim/denetim")) return ["Denetim Logları", "Aktör · IP · zaman (NFR-S7)"];
  if (h.startsWith("#/kvkk")) return ["KVKK / Uyumluluk", "Veri minimizasyonu ve güvenlik"];
  return ["Genel Bakış", ""];
}

function navItem(href, ikon, etiket, active, ekstra = "") {
  const on = active.startsWith(href) ? "active" : "";
  return `<a class="nav-item ${on}" href="${href}"><span class="nav-ico">${ICO(ikon, 20)}</span><span class="nav-label">${etiket}</span>${ekstra}</a>`;
}
function menuHtml(active) {
  let m = navItem("#/panel", "dashboard", "Genel Bakış", active);
  m += navItem("#/transferler", "transfer", "Transfer Onayları", active, `<span class="nav-badge" id="navBek" style="display:none">0</span>`);
  if (koordMu()) { // RBAC §2: yalnızca Koordinatör — Müdür için DOM'a EKLENMEZ
    m += `<div class="nav-group">Yönetim</div>`;
    m += navItem("#/raporlar", "report", "Raporlar", active);
    m += navItem("#/yonetim/subeler", "branch", "Şubeler", active);
    m += navItem("#/yonetim/personel", "staff", "Personel", active);
    m += navItem("#/yonetim/transfer", "transfer", "Manuel Transfer", active);
    m += navItem("#/yonetim/optimizasyon", "settings", "Optimizasyon", active);
    m += navItem("#/yonetim/denetim", "shield", "Denetim Logları", active);
  }
  m += `<div class="nav-group">Uyumluluk</div>`;
  m += navItem("#/kvkk", "shield", "KVKK / Uyumluluk", active);
  return m;
}

function cerceveCiz(active) {
  const [title, sub] = sayfaMeta(active);
  el("app").innerHTML = `
    <div class="app ${kapali ? "is-collapsed" : ""}">
      <aside class="sidebar ${kapali ? "collapsed" : ""}">
        <div class="brand"><span class="brand-mark">${ICO("cup", 22)}</span>${kapali ? "" : `<span class="brand-text"><span class="brand-name">Arabica</span><span class="brand-sub">Kaynak Yönetimi</span></span>`}</div>
        <nav class="nav">${menuHtml(active)}</nav>
        <div class="side-foot"><div class="live-chip"><span class="live-pulse off" id="canliDot"></span>${kapali ? "" : `<span id="canli">çevrimdışı</span>`}</div></div>
      </aside>
      <div class="main">
        <header class="topbar">
          <div class="top-left">
            <button class="icon-btn" onclick="menuToggle()" title="Menü">${ICO("menu", 20)}</button>
            <div><h1 class="page-title">${esc(title)}</h1>${sub ? `<p class="page-sub">${esc(sub)}</p>` : ""}</div>
          </div>
          <div class="top-right">
            <span class="role-tag">${ICO("shield", 14)} ${rolAd()}</span>
            <button class="icon-btn" onclick="kisayolAc()" title="Klavye kısayolları">${ICO("keyboard", 19)}</button>
            <div class="clock">${ICO("clock", 15)}<span id="saat">--:--</span></div>
            <button class="icon-btn" title="Bildirimler">${ICO("bell", 19)}<span class="bell-dot"></span></button>
            <div class="profile" id="profile">
              <button class="profile-btn" onclick="profilToggle(event)">
                <span class="avatar" style="width:34px;height:34px;font-size:12px">${basHarf(kullanici)}</span>
                <span class="profile-meta"><span class="profile-name">${esc(kullanici)}</span><span class="profile-role">${rolAd()}</span></span>
                ${ICO("chevron", 15)}
              </button>
              <div class="profile-menu" id="profileMenu" style="display:none">
                <div class="pm-head"><span class="avatar" style="width:40px;height:40px;font-size:14px">${basHarf(kullanici)}</span><div><div class="pm-name">${esc(kullanici)}</div><div class="pm-mail">${esc(kullanici)}@arabica.com.tr</div></div></div>
                <a class="pm-item" href="#/kvkk" onclick="profilKapat()">${ICO("shield", 17)} KVKK / Uyumluluk</a>
                <div class="pm-div"></div>
                <button class="pm-item danger" onclick="cikis()">${ICO("logout", 17)} Çıkış Yap</button>
              </div>
            </div>
          </div>
        </header>
        <main class="content" id="content"></main>
      </div>
    </div>`;
  saatBaslat();
  canliGoster(conn && conn.state === "Connected");
}
function contentYaz(html) { const c = el("content"); if (c) c.innerHTML = html; }
function menuToggle() { kapali = !kapali; render(); }
function profilToggle(e) { e.stopPropagation(); const m = el("profileMenu"); if (m) m.style.display = m.style.display === "none" ? "block" : "none"; }
function profilKapat() { const m = el("profileMenu"); if (m) m.style.display = "none"; }
function modalKapat() { document.querySelectorAll(".modal-scrim").forEach((m) => m.remove()); }
function kisayolAc() {
  const rows = [["Alt + D", "Genel Bakış ekranına geç"], ["Alt + T", "Transfer Onayları ekranına geç"], ["Enter", "Açık MFA modalında onayla"], ["Esc", "Modalı kapat"]];
  document.body.insertAdjacentHTML("beforeend", `
    <div class="modal-scrim" onmousedown="if(event.target===this)modalKapat()"><div class="modal" style="max-width:440px"><div class="tm">
      <div class="tm-head"><span class="tm-tag">${ICO("keyboard", 14)} Klavye Kısayolları</span><button class="icon-btn sm" onclick="modalKapat()">${ICO("x", 18)}</button></div>
      <div class="sc-list">${rows.map(([k, d]) => `<div class="sc-row"><span class="sc-keys">${k.split(" + ").map((x) => `<kbd>${x}</kbd>`).join("")}</span><span class="sc-desc">${d}</span></div>`).join("")}</div>
    </div></div></div>`);
}
function bekleyenRozetGuncelle(n) { const e = el("navBek"); if (e) { e.textContent = n; e.style.display = n > 0 ? "" : "none"; } }

/* ---------- login ---------- */
function renderLogin() {
  el("app").innerHTML = `
    <div class="login">
      <div class="login-brand">
        <div class="lb-top"><span class="brand-mark big">${ICO("cup", 30)}</span><div class="lb-word"><span class="brand-name">Arabica</span><span class="lb-cafe">CAFE</span></div></div>
        <div class="lb-mid">
          <h2 class="lb-h">Dinamik Kaynak<br>Yönetim Sistemi</h2>
          <p class="lb-p">Şubeler arası kapasite asimetrisini, olay güdümlü mimari ile saniyeler içinde dengeler.</p>
          <div class="lb-stats"><div><b>8</b><span>Aktif Şube</span></div><div><b>&lt;2sn</b><span>Karar Süresi</span></div><div><b>%21</b><span>Verim Artışı</span></div></div>
        </div>
        <div class="lb-foot">Isparta Uygulamalı Bilimler Üniversitesi · Bilgisayar Mühendisliği</div>
        <div class="lb-grain"></div>
      </div>
      <div class="login-form-wrap">
        <form class="login-form" id="loginForm">
          <div class="lf-head"><h1>Tekrar hoş geldiniz</h1><p>Yönetim paneline güvenli giriş yapın</p></div>
          <label class="field"><span>Kullanıcı Adı <em>(kurumsal e-posta / sicil no)</em></span><div class="input-ico">${ICO("user", 16)}<input id="gk" value="tunahan.basar" autocomplete="username"></div></label>
          <label class="field"><span>Parola</span><div class="input-ico">${ICO("logout", 16)}<input id="gs" type="password" value="Arabica.2026!" autocomplete="current-password"></div></label>
          <div class="lf-row"><label class="check"><input type="checkbox" checked> Beni hatırla</label><a class="link" href="#" onclick="return false">Şifremi unuttum</a></div>
          <button class="btn btn-primary btn-lg" id="gbtn" type="submit">Giriş Yap ${ICO("arrowRight", 17)}</button>
          <div id="ghata" class="lf-error"></div>
          <div class="lf-secure">${ICO("check", 13)} JWT tabanlı şifreli oturum · HTTPS üzerinden iletilir</div>
          <div class="lf-demo">Demo: <code>tunahan.basar</code> (Koordinatör) · <code>sinan.say</code> (Müdür) · şifre <code>Arabica.2026!</code></div>
        </form>
      </div>
    </div>`;
  el("loginForm").onsubmit = async (e) => {
    e.preventDefault();
    el("ghata").textContent = "";
    const btn = el("gbtn"); btn.classList.add("busy"); btn.textContent = "Doğrulanıyor…";
    const ok = await girisYap(el("gk").value, el("gs").value);
    if (ok) { location.hash = "#/panel"; render(); }
    else { btn.classList.remove("busy"); btn.innerHTML = "Giriş Yap " + ICO("arrowRight", 17); el("ghata").textContent = "Giriş başarısız (kullanıcı adı / şifre hatalı)."; }
  };
}

/* ---------- panel + charts (Genel Bakış) ---------- */
async function renderPanel() {
  contentYaz(`
    <div class="screen">
      <div class="stat-row five" id="statRow"></div>
      <div id="latRow"></div>
      <div class="dash-grid">
        <section class="panel">
          <div class="panel-head">
            <div><h2 class="panel-title">Şube Doluluk Oranları</h2><p class="panel-sub">Kafka akışıyla canlı güncellenir</p></div>
            <div style="display:flex;gap:8px;align-items:center">
              ${koordMu() ? `<button class="btn btn-warn btn-sm" id="simBtn">${ICO("bolt", 14)} POS yükü simüle et</button>` : ""}
              <span class="live-tag off" id="canliTag"><span class="live-pulse sm off" id="canliDot2"></span> ÇEVRİMDIŞI</span>
            </div>
          </div>
          <div class="occ-list" id="occList"><div class="spinner">Yükleniyor…</div></div>
        </section>
        <section class="panel accent-panel">
          <div class="panel-head"><div><h2 class="panel-title">Otonom Transfer Önerileri</h2><p class="panel-sub">Optimizasyon motoru · 2 sn altı karar</p></div><span class="bolt-tag">${ICO("bolt", 14)}</span></div>
          <div class="sugg-list" id="suggList"><div class="empty">Yükleniyor…</div></div>
        </section>
      </div>
      <section class="panel"><div class="panel-head"><div><h2 class="panel-title">Şube Doluluk Grafiği</h2><p class="panel-sub">Anlık doluluk (%) · seviye renkli</p></div></div><div class="chart-wrap"><canvas id="barChart"></canvas></div></section>
      <section class="panel"><div class="panel-head"><div><h2 class="panel-title">Doluluk Trendi (canlı)</h2><p class="panel-sub">Son ölçümler · şube bazlı</p></div></div><div class="chart-wrap"><canvas id="trendChart"></canvas></div></section>
    </div>`);
  canliGoster(conn && conn.state === "Connected");
  if (koordMu()) el("simBtn").onclick = simuleEt;
  grafikleriHazirla();
  await panelYenile();
  panelTimer = setInterval(panelYenile, 5000);
}

async function panelYenile() {
  const ozet = await api("GET", "/api/v1/ozet");
  if (ozet.ok) { statKartlariCiz(ozet.data); bekleyenRozetGuncelle(ozet.data.bekleyenTransfer); }
  let liste = [];
  if (koordMu()) { const r = await api("GET", "/api/v1/sube/doluluk"); if (r.ok) liste = r.data; }
  else { const r = await api("GET", `/api/v1/sube/${subeId}/detay`); if (r.ok) liste = [{ subeId: r.data.subeId, ad: r.data.ad, dolulukOrani: r.data.dolulukOrani, seviye: r.data.seviye, aktifPersonelSayisi: r.data.personeller ? r.data.personeller.length : null }]; }
  sonDoluluk = liste;
  occListeCiz(liste);
  grafikleriGuncelle(liste);
  const on = await api("GET", "/api/v1/transfer/oneriler");
  if (on.ok) suggCiz(on.data);
}

function statKartlariCiz(o) {
  const kartlar = [
    { i: 0, label: "Aktif Şube", ikon: "branch", acc: "a-blue", val: o.subeSayisi, sub: "Çevrimiçi" },
    { i: 1, label: "Atıl Şube", ikon: "staff", acc: "a-green", val: o.atilSube, sub: "Yeşil seviye" },
    { i: 2, label: "Darboğaz", ikon: "alert", acc: "a-red", val: o.darbogazSube, sub: "Kırmızı seviye", alert: o.darbogazSube > 0 },
    { i: 3, label: "Bekleyen Transfer", ikon: "transfer", acc: "a-amber", val: o.bekleyenTransfer, sub: "Onay bekliyor" },
    { i: 4, label: "Ort. Gecikme", ikon: "signal", acc: "a-blue", val: Math.round(o.ortalamaGecikmeMs), sub: "ms · NFR-P1" },
  ];
  const row = el("statRow"); if (!row) return;
  if (!row.dataset.kuruldu) {
    row.innerHTML = kartlar.map((k) => `<div class="stat" id="statCard${k.i}"><div class="stat-top"><span class="stat-label">${k.label}</span><span class="stat-ico ${k.acc}">${ICO(k.ikon, 18)}</span></div><div class="stat-value" id="stat${k.i}">0</div><div class="stat-sub">${k.sub}</div></div>`).join("");
    row.dataset.kuruldu = "1";
  }
  kartlar.forEach((k) => {
    sayacAnimasyon(el("stat" + k.i), k.val);
    const c = el("statCard" + k.i); if (c) c.classList.toggle("stat-alert", !!k.alert);
  });
}
function sayacAnimasyon(elm, hedef) {
  if (!elm) return;
  const bas = parseInt(elm.textContent) || 0;
  if (bas === hedef) return;
  const adim = (hedef - bas) / 15; let n = bas, k = 0;
  const t = setInterval(() => { k++; n += adim; if (k >= 15) { n = hedef; clearInterval(t); } elm.textContent = Math.round(n); }, 20);
}

function occListeCiz(liste) {
  const k = el("occList"); if (!k) return;
  const sorted = liste.slice().sort((a, b) => b.dolulukOrani - a.dolulukOrani);
  k.innerHTML = sorted.length ? sorted.map((s) => {
    const c = stCls(s.seviye);
    return `<a class="occ-row" href="#/sube/${s.subeId}">
      <div class="occ-id">${esc(s.subeId)}</div>
      <div class="occ-main">
        <div class="occ-line"><span class="occ-name">${esc(s.ad)}</span><span class="occ-meta"><span class="occ-staff">${ICO("staff", 13)} ${s.aktifPersonelSayisi ?? "—"}</span><span class="occ-pct ${c}">%${s.dolulukOrani}</span></span></div>
        <div class="occ-track"><div class="occ-fill ${c}" style="width:${Math.min(100, s.dolulukOrani)}%"></div></div>
      </div></a>`;
  }).join("") : `<div class="empty">Şube verisi yok.</div>`;
}

function suggCiz(list) {
  const k = el("suggList"); if (!k) return;
  k.innerHTML = list.length ? list.map((s) => `
    <article class="sugg">
      <div class="sugg-top"><span class="prio prio-hi">Otonom Öneri</span><span class="sugg-conf">#${s.transferId}</span></div>
      <div class="sugg-route"><span class="route-b">Şube ${s.kaynakSubeId}</span><span class="route-arrow">${ICO("arrowRight", 16)}</span><span class="route-b to">Şube ${s.hedefSubeId}</span></div>
      <div class="sugg-detail"><span class="chip">${ICO(s.tip === "Ekipman" ? "pos" : "staff", 13)} ${s.adet} ${esc(s.tip)}</span><span class="chip ghost">${ICO("clock", 13)} ${esc(s.durum)}</span></div>
      <p class="sugg-reason">Optimizasyon motoru önerisi — kaynak şubeden hedef şubeye kaynak aktarımı.</p>
      <div class="sugg-actions"><button class="btn btn-approve" onclick="onaylaAc(${s.transferId})">${ICO("check", 16)} Onayla</button><button class="btn btn-reject" onclick="reddet(${s.transferId})">${ICO("x", 16)} Reddet</button></div>
    </article>`).join("") : `<div class="empty">Bekleyen öneri yok. ✦</div>`;
}

function grafikleriHazirla() {
  if (window.Chart) { Chart.defaults.font.family = "'Hanken Grotesk',system-ui,sans-serif"; Chart.defaults.color = "#7a6b5d"; }
  const bc = el("barChart"), tc = el("trendChart");
  if (bc) barChart = new Chart(bc, { type: "bar", data: { labels: [], datasets: [{ label: "Doluluk %", data: [], backgroundColor: [], borderRadius: 6 }] }, options: { responsive: true, maintainAspectRatio: false, scales: { y: { beginAtZero: true, suggestedMax: 100, grid: { color: "#f1e9dc" } }, x: { grid: { display: false } } }, plugins: { legend: { display: false } } } });
  if (tc) trendChart = new Chart(tc, { type: "line", data: { labels: [], datasets: [] }, options: { responsive: true, maintainAspectRatio: false, animation: false, scales: { y: { beginAtZero: true, suggestedMax: 100, grid: { color: "#f1e9dc" } }, x: { grid: { display: false } } }, plugins: { legend: { labels: { boxWidth: 12, font: { size: 11 } } } } } });
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
    const renkler = ["#b06a3c", "#4a6b8a", "#5f8d5a", "#c79328", "#9a3e25", "#7a6b5d"];
    trendChart.data.labels = trendNoktalari.map((p) => p.etiket);
    trendChart.data.datasets = liste.map((s, i) => ({ label: s.ad, data: trendNoktalari.map((p) => p.harita[s.subeId] ?? null), borderColor: renkler[i % renkler.length], backgroundColor: renkler[i % renkler.length], tension: .3, fill: false, pointRadius: 2 }));
    trendChart.update();
  }
}
async function simuleEt() {
  toast("POS yükü Kafka'ya gönderiliyor...", "secondary");
  const r = await api("POST", "/api/v1/demo/besle?adet=40");
  if (!r.ok) { toast("Simülasyon başarısız.", "danger"); return; }
  await new Promise((x) => setTimeout(x, 1500));
  const m = await api("GET", "/api/v1/metrik/gecikme");
  if (m.ok) {
    const o = m.data, ok = o.esikAsanSayisi === 0;
    const lat = el("latRow");
    if (lat) lat.innerHTML = `<div class="banner ${ok ? "ok" : "err"}"><span class="grow">${ICO("signal", 15)} Uçtan uca gecikme — ort. <b>${o.ortalamaMs} ms</b> · p95 <b>${o.p95Ms} ms</b> · örnek ${o.adet} · 2 sn aşan ${o.esikAsanSayisi}</span><span class="t-pill ${ok ? "t-done" : "t-no"}">${ok ? "≤ 2 sn ✓ (NFR-P1)" : "2 sn aşıldı"}</span></div>`;
  }
  await panelYenile();
}

/* ---------- transferler (sekmeli) ---------- */
let trAktifTab = "Bekliyor";
let _trGrup = null;
async function renderTransferler() {
  contentYaz(`<div class="screen"><div class="t-tabs" id="trTabs"></div><section class="panel flush" id="trPanel"><div class="spinner">Yükleniyor…</div></section></div>`);
  await transferlerYukle();
}
async function transferlerYukle() {
  if (!el("trTabs")) return;
  const [on, gec] = await Promise.all([api("GET", "/api/v1/transfer/oneriler"), api("GET", "/api/v1/transfer/gecmis")]);
  const bekleyen = on.ok ? on.data : [];
  const gecmis = gec.ok ? gec.data : [];
  _trGrup = {
    Bekliyor: bekleyen,
    Onaylandi: gecmis.filter((t) => t.durum === "Onaylandi"),
    Reddedildi: gecmis.filter((t) => t.durum === "Reddedildi"),
    Tamamlandi: gecmis.filter((t) => t.durum === "Tamamlandi"),
  };
  bekleyenRozetGuncelle(bekleyen.length);
  trCiz();
}
function trCiz() {
  if (!_trGrup || !el("trTabs")) return;
  const tabs = [["Bekliyor", "t-wait"], ["Onaylandi", "t-ok"], ["Reddedildi", "t-no"], ["Tamamlandi", "t-done"]];
  el("trTabs").innerHTML = tabs.map(([k, c]) => `<button class="t-tab ${trAktifTab === k ? "on" : ""}" onclick="trTabSec('${k}')">${esc(k)} <span class="t-count ${k === "Bekliyor" ? "t-wait" : ""}">${_trGrup[k].length}</span></button>`).join("");
  trPanelCiz(_trGrup[trAktifTab], trAktifTab);
}
function trTabSec(k) { trAktifTab = k; trCiz(); }
function trPanelCiz(rows, tab) {
  const p = el("trPanel"); if (!p) return;
  const aksiyon = tab === "Bekliyor";
  const head = `<thead><tr><th>#</th><th>Kaynak</th><th></th><th>Hedef</th><th>Tip</th><th>Durum</th>${aksiyon ? "<th></th>" : "<th>Gerekçe / Zaman</th>"}</tr></thead>`;
  const body = rows.length ? rows.map((t) => `<tr>
      <td class="mono">#${t.transferId}</td>
      <td>Şube ${t.kaynakSubeId}</td>
      <td class="arr">${ICO("arrowRight", 15)}</td>
      <td class="strong">Şube ${t.hedefSubeId}</td>
      <td><span class="chip sm">${ICO(t.tip === "Ekipman" ? "pos" : "staff", 12)} ${t.adet} ${esc(t.tip)}</span></td>
      <td><span class="t-pill ${durumCls(t.durum)}">${esc(t.durum)}</span></td>
      ${aksiyon
      ? `<td><button class="btn btn-sm btn-approve" onclick="onaylaAc(${t.transferId})">İncele</button> <button class="btn btn-sm btn-reject" onclick="reddet(${t.transferId})">Reddet</button></td>`
      : `<td class="muted-cell small">${esc(t.redGerekcesi || "—")}${t.olusturulmaZamani ? ` · ${new Date(t.olusturulmaZamani).toLocaleString("tr-TR")}` : ""}</td>`}
    </tr>`).join("") : `<tr><td colspan="7" class="empty-cell">Bu durumda transfer bulunmuyor.</td></tr>`;
  p.innerHTML = `<table class="tbl">${head}<tbody>${body}</tbody></table>`;
}
function transferEkranlariYenile() {
  if (location.hash.startsWith("#/transferler")) transferlerYukle();
  if (location.hash.startsWith("#/panel")) panelYenile();
}

/* MFA onay modalı */
function onaylaAc(id) {
  document.body.insertAdjacentHTML("beforeend", `
    <div class="modal-scrim" onmousedown="if(event.target===this)modalKapat()"><div class="modal" style="max-width:440px"><div class="tm">
      <div class="tm-head"><span class="tm-tag">${ICO("bolt", 13)} Transfer #${id} · MFA Onayı</span><button class="icon-btn sm" onclick="modalKapat()">${ICO("x", 18)}</button></div>
      <p class="muted small" style="margin-bottom:12px">Kritik onay için 6 haneli TOTP (MFA) kodu gereklidir.</p>
      <input id="mfaKod" class="flat" placeholder="6 haneli kod" maxlength="6" inputmode="numeric" style="margin-bottom:10px">
      <div><button class="btn btn-ghost btn-sm" onclick="mfaDemoDoldur()">${ICO("check", 14)} Demo MFA kodu üret</button></div>
      <div class="muted small" style="margin-top:6px">Demo amaçlı istemci üretimi; gerçek dağıtımda authenticator uygulamasından girilir.</div>
      <div id="mfaHata" class="lf-error" style="text-align:left;margin-top:8px"></div>
      <div class="tm-actions" style="margin-top:16px"><button class="btn btn-reject flex" onclick="modalKapat()">Vazgeç</button><button class="btn btn-approve flex" onclick="onayla(${id})">${ICO("check", 16)} Transferi Onayla <kbd>Enter</kbd></button></div>
    </div></div></div>`);
  const k = el("mfaKod"); k.focus();
  k.addEventListener("keydown", (e) => { if (e.key === "Enter") { e.preventDefault(); onayla(id); } });
}
async function mfaDemoDoldur() { el("mfaKod").value = await totpUret(MFA_SECRET); }
async function onayla(id) {
  const kod = (el("mfaKod") ? el("mfaKod").value || "" : "").trim();
  const r = await api("POST", "/api/v1/transfer/islem", { transferId: id, aksiyon: "ONAYLA" }, { "X-MFA-Code": kod });
  if (r.status === 200) {
    modalKapat();
    const durum = r.data?.durum || "Tamamlandi";
    toast(`Transfer #${id} ${durum.toLowerCase()}; personel sayıları güncellendi.`, "success");
    transferEkranlariYenile();
  }
  else if (r.status === 401) { const h = el("mfaHata"); if (h) h.textContent = "MFA kodu geçersiz veya eksik."; }
  else if (r.status === 409) { modalKapat(); toast(r.data?.detail || r.data?.title || "Geçersiz işlem (örn. yetersiz personel veya zaten işlenmiş).", "danger"); transferEkranlariYenile(); }
  else if (r.status === 403) { modalKapat(); toast("Bu transfer şubenizi ilgilendirmiyor (RBAC).", "danger"); }
  else { const h = el("mfaHata"); if (h) h.textContent = "Hata: HTTP " + r.status; }
}
async function reddet(id) {
  if (prompt("Reddetme gerekçesi (kayda işlenir):") === null) return;
  const r = await api("POST", "/api/v1/transfer/islem", { transferId: id, aksiyon: "REDDET" });
  if (r.status === 200) { toast(`Transfer #${id} reddedildi.`, "success"); transferEkranlariYenile(); }
  else if (r.status === 403) toast("Bu transfer şubenizi ilgilendirmiyor (RBAC).", "danger");
  else if (r.status === 409) { toast("Geçersiz durum geçişi.", "danger"); transferEkranlariYenile(); }
  else toast("Hata: HTTP " + r.status, "danger");
}

/* ---------- şube detay ---------- */
async function renderSubeDetay(id) {
  contentYaz(`<div class="screen"><a class="link" href="#/panel">${ICO("arrowRight", 14)} Panele dön</a><div id="detayBody"><div class="spinner">Yükleniyor…</div></div></div>`);
  const r = await api("GET", `/api/v1/sube/${id}/detay`);
  const b = el("detayBody"); if (!b) return;
  if (r.status === 403) { b.innerHTML = `<div class="banner err">${ICO("alert", 16)} Yalnızca kendi şubenizi görüntüleyebilirsiniz (RBAC §2).</div>`; return; }
  if (r.status === 404) { b.innerHTML = `<div class="banner warn">${ICO("alert", 16)} Şube bulunamadı.</div>`; return; }
  if (!r.ok) { b.innerHTML = `<div class="banner warn">Veri alınamadı.</div>`; return; }
  const d = r.data, c = stCls(d.seviye);
  const bot = d.seviye === "Kirmizi" ? [ICO("alert", 16), "Kapasite aşıldı — destek personeli öneriliyor"]
    : d.seviye === "Sari" ? [ICO("signal", 16), "Yoğunluk artıyor — izleniyor"]
      : [ICO("check", 16), "Atıl kapasite — transfere uygun"];
  b.innerHTML = `<section class="panel">
      <div class="bd-head"><div><div class="bd-name">${esc(d.ad)}</div><div class="muted small" style="margin-top:3px">Şube #${esc(d.subeId)}</div></div><span class="pill ${c}"><span class="pill-dot"></span>${seviyeAd(d.seviye)}</span></div>
      <div class="bd-stats">
        <div class="bd-stat"><span class="bds-v">%${d.dolulukOrani}</span><span class="bds-l">Anlık doluluk</span></div>
        <div class="bd-stat"><span class="bds-v">${d.personeller.length}</span><span class="bds-l">PDKS personel</span></div>
        <div class="bd-stat"><span class="bds-v">${d.maksimumKapasite ?? "—"}</span><span class="bds-l">Maks. kapasite</span></div>
      </div>
      <div class="bd-section"><div class="bd-section-h">Darboğaz Durumu</div><div class="bottleneck ${c}">${bot[0]} ${bot[1]}</div></div>
      <div class="bd-section">
        <div class="bd-section-h">PDKS · Personel <span class="count">${d.personeller.length}</span></div>
        <div class="muted small" style="margin:-4px 0 10px">KVKK: yalnız anonim sayısal ID + takma ad.</div>
        <div class="bd-staff">${d.personeller.length ? d.personeller.map((p) => `<div class="bd-person"><span class="avatar tone-muted" style="width:30px;height:30px;font-size:10px">${basHarf(p.takmaAd)}</span><div class="bp-meta"><span class="bp-name">${esc(p.takmaAd)}</span><span class="bp-role">#${p.personelId}</span></div></div>`).join("") : `<div class="muted small">Personel kaydı yok.</div>`}</div>
      </div>
    </section>`;
}

/* ---------- raporlar (Koordinatör) ---------- */
async function renderRaporlar() {
  contentYaz(`<div class="screen"><div class="stat-row" id="rapStats"></div><section class="panel flush"><div class="panel-head pad"><div><h2 class="panel-title">Personel & Ekipman Transfer Geçmişi</h2><p class="panel-sub">Optimizasyon motoru kararlarının operasyonel sonuçları</p></div></div><div id="rapBody"><div class="spinner">Yükleniyor…</div></div></section></div>`);
  const r = await api("GET", "/api/v1/transfer/gecmis");
  const b = el("rapBody"); if (!b) return;
  if (!r.ok) { b.innerHTML = `<div class="empty-cell">Alınamadı.</div>`; return; }
  const d = r.data || [];
  const cnt = (s) => d.filter((t) => t.durum === s).length;
  const stats = [
    { l: "Toplam Transfer", v: d.length, a: "a-blue", i: "transfer" },
    { l: "Tamamlanan", v: cnt("Tamamlandi"), a: "a-green", i: "check" },
    { l: "Reddedilen", v: cnt("Reddedildi"), a: "a-red", i: "x" },
    { l: "Bekleyen", v: cnt("Bekliyor"), a: "a-amber", i: "clock" },
  ];
  const rs = el("rapStats"); if (rs) rs.innerHTML = stats.map((c) => `<div class="stat"><div class="stat-top"><span class="stat-label">${c.l}</span><span class="stat-ico ${c.a}">${ICO(c.i, 18)}</span></div><div class="stat-value">${c.v}</div></div>`).join("");
  if (!d.length) { b.innerHTML = `<div class="empty-cell">Kayıt yok.</div>`; return; }
  b.innerHTML = `<table class="tbl"><thead><tr><th>#</th><th>Güzergah</th><th>Tip</th><th>Sonuç</th><th>Gerekçe</th><th>Oluşturulma</th></tr></thead><tbody>${
    d.map((t) => `<tr><td class="mono">#${t.transferId}</td><td>Şube ${t.kaynakSubeId} <span class="arr-inline">→</span> Şube ${t.hedefSubeId}</td><td><span class="chip sm">${ICO(t.tip === "Ekipman" ? "pos" : "staff", 12)} ${t.adet} ${esc(t.tip)}</span></td><td><span class="t-pill ${durumCls(t.durum)}">${esc(t.durum)}</span></td><td class="muted-cell small">${esc(t.redGerekcesi || "—")}</td><td class="muted-cell mono">${new Date(t.olusturulmaZamani).toLocaleString("tr-TR")}</td></tr>`).join("")
    }</tbody></table>`;
}

/* ---------- YÖNETİM: Şubeler ---------- */
async function aktifSubeler() { const r = await api("GET", "/api/v1/admin/sube"); return r.ok ? r.data.filter((s) => s.aktif) : []; }

async function renderYonetimSubeler() {
  contentYaz(`<div class="screen"><div class="toolbar between"><div class="muted small">Şube oluştur · düzenle · pasifleştir / aktifleştir</div><button class="btn btn-primary btn-sm" onclick="subeModalAc()">${ICO("branch", 15)} Yeni Şube</button></div><section class="panel flush" id="subePanel"><div class="spinner">Yükleniyor…</div></section></div>`);
  await subeListeYukle();
}
async function subeListeYukle() {
  const r = await api("GET", "/api/v1/admin/sube");
  const b = el("subePanel"); if (!b) return;
  if (!r.ok) { b.innerHTML = `<div class="empty-cell">Alınamadı.</div>`; return; }
  b.innerHTML = `<table class="tbl"><thead><tr><th>#</th><th>Ad</th><th>Maks.</th><th>Anlık</th><th>Personel</th><th>Durum</th><th></th></tr></thead><tbody>${
    r.data.map((s) => `<tr><td class="mono">${s.subeId}</td><td class="strong">${esc(s.ad)}</td><td class="mono">${s.maksimumKapasite}</td><td class="mono">${s.anlikMusteriSayisi}</td><td class="mono">${s.aktifPersonelSayisi}</td>
      <td><span class="t-pill ${s.aktif ? "t-done" : "t-no"}">${s.aktif ? "Aktif" : "Pasif"}</span></td>
      <td><button class="btn btn-ghost btn-sm" onclick='subeModalAc(${esc(JSON.stringify(s))})'>Düzenle</button>
          ${s.aktif ? `<button class="btn btn-reject btn-sm" onclick="subePasiflestir(${s.subeId})">Pasifleştir</button>` : `<button class="btn btn-approve btn-sm" onclick="subeAktiflestir(${s.subeId})">Aktifleştir</button>`}</td></tr>`).join("")
    }</tbody></table>`;
}
function subeModalAc(s) {
  const d = s || { subeId: 0, ad: "", maksimumKapasite: 100, aktifPersonelSayisi: 0 };
  document.body.insertAdjacentHTML("beforeend", `
    <div class="modal-scrim" onmousedown="if(event.target===this)modalKapat()"><div class="modal" style="max-width:460px"><div class="tm">
      <div class="tm-head"><span class="tm-tag">${ICO("branch", 13)} ${d.subeId ? "Şube Düzenle #" + d.subeId : "Yeni Şube"}</span><button class="icon-btn sm" onclick="modalKapat()">${ICO("x", 18)}</button></div>
      <div class="tm-body">
        <label class="field"><span>Ad</span><input id="sAd" value="${esc(d.ad)}"></label>
        <label class="field"><span>Maksimum kapasite</span><input id="sKap" type="number" value="${d.maksimumKapasite}"></label>
        <label class="field"><span>Aktif personel sayısı</span><input id="sPer" type="number" value="${d.aktifPersonelSayisi}"></label>
        <div id="sHata" class="lf-error" style="text-align:left"></div>
      </div>
      <div class="tm-actions"><button class="btn btn-reject flex" onclick="modalKapat()">Vazgeç</button><button class="btn btn-primary flex" onclick="subeKaydet(${d.subeId})">Kaydet</button></div>
    </div></div></div>`);
}
async function subeKaydet(id) {
  const body = { ad: el("sAd").value, maksimumKapasite: parseInt(el("sKap").value), aktifPersonelSayisi: parseInt(el("sPer").value) };
  const r = id ? await api("PUT", `/api/v1/admin/sube/${id}`, body) : await api("POST", "/api/v1/admin/sube", body);
  if (r.ok) { modalKapat(); toast(id ? "Şube güncellendi." : "Şube oluşturuldu.", "success"); subeListeYukle(); }
  else { const h = el("sHata"); if (h) h.textContent = r.status === 400 ? "Doğrulama hatası (ad boş olamaz, kapasite > 0)." : "Hata: HTTP " + r.status; }
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
    <div class="screen narrow">
      <div class="kvkk-note"><strong>⚖️ KVKK:</strong> Yalnızca <strong>takma ad</strong> ve şube toplanır. TC / ad-soyad / telefon <u>istenmez ve saklanmaz</u>. Kayıtlar anonim sayısal ID ile tutulur.</div>
      <section class="panel form-card">
        <div class="panel-head"><div><h2 class="panel-title">Personel Ekle</h2><p class="panel-sub">Anonim — yalnız takma ad + şube</p></div></div>
        <div class="form-grid">
          <label class="field"><span>Şube</span><select id="pSube">${subeler.map((s) => `<option value="${s.subeId}">${esc(s.ad)} (#${s.subeId})</option>`).join("")}</select></label>
          <label class="field"><span>Tip</span><select id="pTip"><option>Barista</option></select></label>
          <label class="field" style="grid-column:1/-1"><span>Takma ad</span><input id="pTakma" placeholder="örn. Barista-A1"></label>
        </div>
        <div style="margin-top:14px;display:flex;gap:12px;align-items:center;flex-wrap:wrap"><button class="btn btn-primary" onclick="personelEkle()">${ICO("staff", 15)} Personel Ekle</button><span id="pSonuc" class="small"></span></div>
      </section>
    </div>`);
}
async function personelEkle() {
  const body = { subeId: parseInt(el("pSube").value), takmaAd: el("pTakma").value, tip: el("pTip").value };
  const r = await api("POST", "/api/v1/admin/personel", body);
  if (r.ok) { toast(`Personel eklendi: #${r.data.personelId} ${r.data.takmaAd}`, "success"); el("pSonuc").innerHTML = `<span style="color:var(--green-ink)">Eklendi (anonim ID #${r.data.personelId}). KVKK: gönderilen veri yalnız {subeId, takmaAd, tip}.</span>`; el("pTakma").value = ""; }
  else el("pSonuc").innerHTML = `<span style="color:var(--red-ink)">${r.status === 400 ? "Takma ad boş olamaz." : r.status === 404 ? "Aktif şube bulunamadı." : "Hata: HTTP " + r.status}</span>`;
}

/* ---------- YÖNETİM: Manuel Transfer ---------- */
async function renderYonetimTransfer() {
  const subeler = await aktifSubeler();
  const opt = subeler.map((s) => `<option value="${s.subeId}">${esc(s.ad)} (#${s.subeId})</option>`).join("");
  contentYaz(`
    <div class="screen narrow">
      <p class="muted small">Koordinatör manuel emir oluşturur (SRS §2.1). Factory Method ile üretilir, outbox→ESB üzerinden gerçek-zamanlı bildirim doğar ve <a href="#/transferler">Transferler</a>'de belirir.</p>
      <section class="panel form-card">
        <div class="panel-head"><div><h2 class="panel-title">Manuel Transfer Emri</h2><p class="panel-sub">Kaynak → hedef · personel veya ekipman</p></div></div>
        <div class="form-grid">
          <label class="field"><span>Kaynak şube</span><select id="mKaynak">${opt}</select></label>
          <label class="field"><span>Hedef şube</span><select id="mHedef">${opt}</select></label>
          <label class="field"><span>Adet</span><input id="mAdet" type="number" value="1" min="1"></label>
          <label class="field"><span>Tip</span><select id="mTip"><option>Personel</option><option>Ekipman</option></select></label>
        </div>
        <div style="margin-top:14px;display:flex;gap:12px;align-items:center;flex-wrap:wrap"><button class="btn btn-primary" onclick="manuelTransfer()">${ICO("transfer", 15)} Transfer Emri Oluştur</button><span id="mSonuc" class="small"></span></div>
      </section>
    </div>`);
  if (subeler.length > 1) el("mHedef").selectedIndex = 1;
}
async function manuelTransfer() {
  const body = { kaynakSubeId: parseInt(el("mKaynak").value), hedefSubeId: parseInt(el("mHedef").value), adet: parseInt(el("mAdet").value), tip: el("mTip").value };
  const r = await api("POST", "/api/v1/admin/transfer/manuel", body);
  if (r.ok) { toast(`Manuel transfer emri #${r.data.transferId} oluşturuldu (Bekliyor).`, "success"); el("mSonuc").innerHTML = `<span style="color:var(--green-ink)">Emir #${r.data.transferId} oluşturuldu — Transferler ekranında onaya hazır.</span>`; }
  else el("mSonuc").innerHTML = `<span style="color:var(--red-ink)">${r.status === 400 ? "Kaynak ve hedef aynı olamaz / geçersiz alan." : r.status === 404 ? "Şube bulunamadı veya pasif." : "Hata: HTTP " + r.status}</span>`;
}

/* ---------- YÖNETİM: Optimizasyon + Strateji ---------- */
async function renderYonetimOptimizasyon() {
  contentYaz(`
    <div class="screen">
      <section class="panel">
        <div class="panel-head"><div><h2 class="panel-title">Aktif Strateji <span id="stratejiRozet" class="chip" style="margin-left:6px">—</span></h2><p class="panel-sub" id="stratejiAciklama"></p></div></div>
        <div class="seg" style="margin-bottom:8px"><button onclick="stratejiAyarla('vize-final')">Vize-Final Sezonu</button><button onclick="stratejiAyarla('yaz')">Yaz Dönemi</button><button onclick="stratejiAyarla('')">Takvim Varsayılanı</button></div>
        <p class="muted small">Strateji deseni canlı: seçim, darboğaz eşiklerini çalışma-zamanında değiştirir.</p>
      </section>
      <div><button class="btn btn-warn" onclick="optimizasyonTetikle()">${ICO("bolt", 15)} Darboğazı tespit et & öneri üret</button></div>
      <div id="optSonuc"></div>
    </div>`);
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
  if (!r.ok) { b.innerHTML = `<div class="banner warn">Hata.</div>`; return; }
  if (!r.data.length) { b.innerHTML = `<div class="banner info">${ICO("signal", 15)} Bu strateji ile darboğaz/atıl eşleşmesi bulunamadı (öneri üretilmedi).</div>`; return; }
  toast(`${r.data.length} öneri üretildi.`, "success");
  b.innerHTML = `<div class="banner ok" style="margin-bottom:12px">${ICO("check", 15)} ${r.data.length} öneri üretildi (Bekliyor).</div>
    <section class="panel flush"><table class="tbl"><thead><tr><th>#</th><th>Güzergah</th><th>Tip</th><th>Durum</th></tr></thead><tbody>${
    r.data.map((o) => `<tr><td class="mono">#${o.transferId}</td><td>Şube ${o.kaynakSubeId} <span class="arr-inline">→</span> Şube ${o.hedefSubeId}</td><td><span class="chip sm">${o.adet} ${esc(o.tip)}</span></td><td><span class="t-pill t-wait">${esc(o.durum)}</span></td></tr>`).join("")
    }</tbody></table></section>`;
}

/* ---------- YÖNETİM: Denetim Logları ---------- */
let denetimSayfa = 1;
async function renderYonetimDenetim() {
  denetimSayfa = 1;
  contentYaz(`<div class="screen"><section class="panel flush" id="denetimPanel"><div class="spinner">Yükleniyor…</div></section>
    <div class="pager"><button class="btn btn-ghost btn-sm" onclick="denetimSayfaDegis(-1)">← Önceki</button><span id="denetimSayfaNo">1</span><button class="btn btn-ghost btn-sm" onclick="denetimSayfaDegis(1)">Sonraki →</button></div></div>`);
  await denetimYukle();
}
async function denetimYukle() {
  const r = await api("GET", `/api/v1/admin/denetim?sayfa=${denetimSayfa}&boyut=15`);
  const b = el("denetimPanel"); if (!b) return;
  if (!r.ok) { b.innerHTML = `<div class="empty-cell">Alınamadı.</div>`; return; }
  if (!r.data.length) { b.innerHTML = `<div class="empty-cell">Kayıt yok.</div>`; return; }
  b.innerHTML = `<table class="tbl"><thead><tr><th>Zaman</th><th>Aktör</th><th>IP</th><th>Eylem</th><th>Detay</th></tr></thead><tbody>${
    r.data.map((d) => `<tr><td class="muted-cell small mono">${new Date(d.zaman).toLocaleString("tr-TR")}</td><td class="strong">${esc(d.aktor)}</td><td class="mono small">${esc(d.ipAdresi)}</td><td><span class="tag-dark">${esc(d.eylem)}</span></td><td class="muted-cell small">${esc(d.detay || "—")}</td></tr>`).join("")
    }</tbody></table>`;
}
function denetimSayfaDegis(d) { denetimSayfa = Math.max(1, denetimSayfa + d); el("denetimSayfaNo").textContent = denetimSayfa; denetimYukle(); }

/* ---------- KVKK ---------- */
async function renderKvkk() {
  contentYaz(`
    <div class="screen">
      <div class="dash-grid">
        <section class="panel">
          <div class="panel-head"><div><h2 class="panel-title">KVKK (6698) — Veri Minimizasyonu</h2><p class="panel-sub">Kişisel veri saklanmaz</p></div><span class="stat-ico a-green">${ICO("shield", 18)}</span></div>
          <ul class="list-tight">
            <li>Kişisel veri (TC, ad-soyad, telefon) <strong>saklanmaz</strong>.</li>
            <li>Personel ve şubeler yalnızca <strong>anonim sayısal ID</strong> ile referanslanır.</li>
            <li>Kimlik bilgileri ayrı, erişimi kısıtlı <strong>kimlik</strong> şemasında; parolalar <strong>PBKDF2</strong> (tuzlu) hash.</li>
            <li>Denetim logu: <strong>aktör + IP + zaman damgası</strong> (NFR-S7).</li>
          </ul>
          <div style="margin-top:14px"><button id="kvkkBtn" class="btn btn-ghost btn-sm">${ICO("signal", 14)} Örnek kayıt getir (yalnız ID kanıtı)</button></div>
          <pre id="kvkkOrnek" class="code-block" style="margin-top:12px;display:none"></pre>
        </section>
        <section class="panel">
          <div class="panel-head"><div><h2 class="panel-title">İş Kanunu (4857) & Güvenlik</h2><p class="panel-sub">Muhafız zinciri + erişim</p></div><span class="stat-ico a-amber">${ICO("alert", 18)}</span></div>
          <div class="h-sec">İş Kanunu — Muhafız Zinciri (Chain of Responsibility)</div>
          <ul class="list-tight">
            <li>Günlük azami mesai aşımı → transfer <strong>engellenir</strong>.</li>
            <li>Haftalık azami mesai aşımı → engellenir.</li>
            <li>Yasal mola (ara dinlenmesi) hak edilmişse → engellenir.</li>
            <li><strong>Yol süresi mesaiye dahildir.</strong></li>
          </ul>
          <div class="h-sec" style="margin-top:16px">Güvenlik</div>
          <ul class="list-tight">
            <li>JWT (stateless, 15 dk) + policy-tabanlı <strong>RBAC</strong>; menü rol bazlı.</li>
            <li>MFA (TOTP) kritik onayda (<strong>X-MFA-Code</strong>).</li>
            <li>AES-256 (Data Protection); DB portları dışa kapalı; HTTPS/TLS.</li>
          </ul>
        </section>
      </div>
    </div>`);
  el("kvkkBtn").onclick = async () => {
    const r = await api("GET", "/api/v1/transfer/oneriler");
    const p = el("kvkkOrnek"); p.style.display = "block";
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
