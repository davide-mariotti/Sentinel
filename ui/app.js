"use strict";
// Sentinel's settings window. The exe sends: init, settings (when they change), status (every second).
// The page sends back: ready, set, open. Nothing here touches the network.

const $ = id => document.getElementById(id);
const host = window.chrome && window.chrome.webview;
const send = m => host && host.postMessage(m);
const icon = n => `<svg class="i" viewBox="0 0 24 24" aria-hidden="true">${ICONS[n]}</svg>`;
const esc = s => String(s == null ? "" : s).replace(/[&<>"']/g, c => ({ "&": "&amp;", "<": "&lt;", ">": "&gt;", '"': "&quot;", "'": "&#39;" })[c]);

const TEXT = {
  it: {
    pill: { on: "Attivo", paused: "In pausa pranzo", locked: "PC bloccato", off: "Spento" },
    kicker: { on: "Sentinel sta lavorando", paused: "Pausa programmata", locked: "Sentinel sta lavorando", off: "Sentinel è spento" },
    title: {
      on: 'Il PC resta <span class="serif grad">sveglio</span>',
      paused: 'In pausa <span class="serif grad">pranzo</span>',
      locked: 'Bloccato, ma <span class="serif grad">sveglio</span>',
      off: 'Decide <span class="serif">Windows</span>',
    },
    idle: (s, n) => s < n ? `Mouse e tastiera fermi da ${s} s. Dopo ${n} s arriva una spinta di zero pixel: il puntatore resta dov'è, Windows la conta come attività.`
                          : `Fermi da ${s} s: Sentinel tiene sveglio il PC con una spinta di zero pixel ogni 20 secondi.`,
    paused: to => `Fino alle ${to} il PC è libero di andare in standby. Poi Sentinel riprende da solo.`,
    locked: "Lo schermo si può spegnere, ma il PC resta acceso: download e build vanno avanti.",
    off: "Standby, screensaver e «assente» tornano come Windows li vuole. Accendilo e smettono.",
    legend: ["fermo da", "spinta"],
    startup: "Avvia con Windows",
    startupNote: "Al login va dritto nella tray, senza finestre.",
    startupPortable: "Versione portable: le impostazioni stanno in Sentinel.ini, accanto all'exe, e Sentinel non parte da solo.",
    pause: "Pausa programmata",
    from: "Dalle", to: "alle",
    pauseNote: "Anche a cavallo della mezzanotte. In quella fascia Sentinel si fa da parte e il PC può dormire.",
    battery: "Avvisi batteria",
    high: "Avvisami di staccare il caricatore al", low: "Avvisami di attaccarlo al", minutes: "L'avviso «staccami» insiste per",
    batteryNote: "Una batteria che non sta tutto il giorno al 100% dura di più.",
    noBattery: "nessuna batteria", charging: "in carica",
    about: v => `Sentinel ${v} · licenza MIT · niente internet, niente telemetria, niente admin.`,
    links: ["Codice sorgente", "Offrimi un caffè"],
  },
  en: {
    pill: { on: "Active", paused: "Lunch break", locked: "PC locked", off: "Off" },
    kicker: { on: "Sentinel is at work", paused: "Scheduled pause", locked: "Sentinel is at work", off: "Sentinel is off" },
    title: {
      on: 'Your PC stays <span class="serif grad">awake</span>',
      paused: 'On its lunch <span class="serif grad">break</span>',
      locked: 'Locked, but <span class="serif grad">awake</span>',
      off: '<span class="serif">Windows</span> decides',
    },
    idle: (s, n) => s < n ? `Mouse and keyboard still for ${s} s. After ${n} s comes a nudge of zero pixels: the pointer stays put, Windows counts it as activity.`
                          : `Still for ${s} s: Sentinel keeps the PC awake with a zero-pixel nudge every 20 seconds.`,
    paused: to => `Until ${to} the PC is free to sleep. Then Sentinel picks up again by itself.`,
    locked: "The screen can turn off, but the PC stays on: downloads and builds keep going.",
    off: "Sleep, screensaver and \"away\" are back the way Windows wants them. Turn it on and they stop.",
    legend: ["still for", "nudge"],
    startup: "Start with Windows",
    startupNote: "At login it goes straight to the tray, no window.",
    startupPortable: "Portable version: settings live in Sentinel.ini, next to the exe, and Sentinel doesn't start by itself.",
    pause: "Scheduled pause",
    from: "From", to: "to",
    pauseNote: "Across midnight too. In that slot Sentinel steps aside and the PC may sleep.",
    battery: "Battery alerts",
    high: "Tell me to unplug the charger at", low: "Tell me to plug it in at", minutes: "The \"unplug me\" alert keeps nagging for",
    batteryNote: "A battery that doesn't sit at 100% all day lives longer.",
    noBattery: "no battery", charging: "charging",
    about: v => `Sentinel ${v} · MIT license · no internet, no telemetry, no admin.`,
    links: ["Source code", "Buy me a coffee"],
  },
};
let T = TEXT.en, init = null, settings = null, status = null;

if (host) host.addEventListener("message", e => {
  const m = e.data;
  if (m.t === "init") { init = m; T = TEXT[m.lang] || TEXT.en; document.documentElement.lang = m.lang; build(); }
  else if (m.t === "settings") { settings = m; fill(); }
  else if (m.t === "status") { status = m; live(); }
});
send({ t: "ready" });

function build() {
  $("logo").innerHTML = icon("eye");
  $("version").textContent = "v" + init.version;
  const row = (label, id, attrs, unit) => `<label class="field-row"><span class="l">${esc(label)}</span><input id="${id}" ${attrs}>${unit != null ? `<span class="u">${unit}</span>` : ""}</label>`;
  $("main").innerHTML = `
    <section class="hero spot reveal" id="hero">
      <div class="hero-row">
        <div class="eye"><span class="ring"></span><span class="ring r2"></span><span class="tile" id="eye"></span></div>
        <div class="hero-text"><p class="kicker" id="kicker"></p><h1 id="title"></h1></div>
        <input type="checkbox" class="switch lg" id="awake" aria-label="${esc(T.pill.on)}">
      </div>
      <p class="detail" id="detail"></p>
      <div class="idle"><div class="idle-bar"><i id="idleBar"></i></div><div class="idle-legend"><span id="idleLeft"></span><span id="idleRight"></span></div></div>
    </section>
    <section class="card spot reveal" style="--d:70ms">
      <div class="card-head"><span class="icon-tile">${icon("power")}</span><h2>${esc(T.startup)}</h2><input type="checkbox" class="switch" id="startup"${init.portable ? " disabled" : ""}></div>
      <p class="note">${esc(init.portable ? T.startupPortable : T.startupNote)}</p>
    </section>
    <section class="card spot reveal" style="--d:140ms">
      <div class="card-head"><span class="icon-tile">${icon("utensils")}</span><h2>${esc(T.pause)}</h2><input type="checkbox" class="switch" id="schedule"></div>
      <div class="fields" id="pauseFields">${row(T.from, "from", 'type="time"')}${row(T.to, "to", 'type="time"')}</div>
      <p class="note">${esc(T.pauseNote)}</p>
    </section>
    <section class="card spot reveal" style="--d:210ms">
      <div class="card-head"><span class="icon-tile" id="batIcon"></span><h2>${esc(T.battery)}</h2><span class="chip" id="batChip"></span><input type="checkbox" class="switch" id="battery"></div>
      <div class="fields" id="batFields">${row(T.high, "high", 'type="number" min="1" max="100"', "%")}${row(T.low, "low", 'type="number" min="1" max="100"', "%")}${row(T.minutes, "highMinutes", 'type="number" min="1" max="60"', "min")}</div>
      <p class="note">${esc(T.batteryNote)}</p>
    </section>
    <footer class="reveal" style="--d:280ms"><span class="about">${esc(T.about(init.version))}</span>
      <button class="btn" data-open="https://github.com/davide-mariotti/Sentinel">${icon("code-xml")}${esc(T.links[0])}</button>
      <button class="btn" data-open="https://ko-fi.com/Z0X32ATNHL">${icon("coffee")}${esc(T.links[1])}</button></footer>`;

  for (const id of ["awake", "startup", "schedule", "battery"]) $(id).onchange = e => send({ t: "set", [id]: e.target.checked });
  for (const id of ["from", "to"]) $(id).onchange = e => { if (e.target.value) send({ t: "set", [id]: e.target.value }); };
  for (const id of ["high", "low", "highMinutes"]) $(id).onchange = e => { const v = parseInt(e.target.value, 10); if (v > 0) send({ t: "set", [id]: v }); };
  document.querySelectorAll("[data-open]").forEach(b => b.onclick = () => send({ t: "open", url: b.dataset.open }));
  requestAnimationFrame(() => requestAnimationFrame(() => document.querySelectorAll(".reveal").forEach(el => el.classList.add("in"))));
  if (settings) fill();
  if (status) live();
}

// the settings as Sentinel holds them, written back into the fields (unless you're typing in one)
function fill() {
  if (!init || !$("awake")) return;
  for (const id of ["awake", "startup", "schedule", "battery"]) $(id).checked = !!settings[id];
  for (const id of ["from", "to", "high", "low", "highMinutes"]) if (document.activeElement !== $(id)) $(id).value = settings[id];
  $("pauseFields").classList.toggle("dim", !settings.schedule);
  $("batFields").classList.toggle("dim", !settings.battery);
}

function live() {
  if (!init || !$("hero")) return;
  const s = status;
  const mode = !s.awake ? "off" : s.paused ? "paused" : s.locked ? "locked" : "on";
  document.body.className = mode === "off" ? "off" : mode === "paused" ? "paused" : "";
  const pill = $("pill");
  pill.className = "pill" + (mode === "off" ? " off" : mode === "paused" ? " warn" : "");
  pill.innerHTML = `<span class="dot"></span>${esc(T.pill[mode])}`;
  $("eye").innerHTML = icon(mode === "off" || mode === "paused" ? "eye-off" : "eye");
  $("logo").innerHTML = icon(mode === "off" || mode === "paused" ? "eye-off" : "eye");
  $("kicker").textContent = T.kicker[mode];
  $("title").innerHTML = T.title[mode];
  $("detail").textContent = mode === "on" ? T.idle(s.idle, s.nudgeAfter) : mode === "paused" ? T.paused(settings ? settings.to : "") : mode === "locked" ? T.locked : T.off;
  $("idleBar").style.width = Math.min(100, s.idle / s.nudgeAfter * 100) + "%";
  $("idleLeft").textContent = `${T.legend[0]} ${s.idle} s`;
  $("idleRight").textContent = `${T.legend[1]} ${s.nudgeAfter} s`;
  $("batIcon").innerHTML = icon(s.charging && s.battery != null ? "battery-charging" : s.battery != null && settings && s.battery <= settings.low ? "battery-exclamation-point" : "battery");
  $("batChip").textContent = s.battery == null ? T.noBattery : `${s.battery} %${s.charging ? " · " + T.charging : ""}`;
}

document.addEventListener("pointermove", e => {
  const el = e.target.closest && e.target.closest(".spot");
  if (!el) return;
  const b = el.getBoundingClientRect();
  el.style.setProperty("--x", e.clientX - b.left + "px");
  el.style.setProperty("--y", e.clientY - b.top + "px");
});
