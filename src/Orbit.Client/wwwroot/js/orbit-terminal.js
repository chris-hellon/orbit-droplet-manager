// xterm.js terminal bridged to the Orbit SSH hub. Loaded from CDN as ES modules; vendor into
// wwwroot for a fully offline deploy.

const XTERM = "https://esm.sh/@xterm/xterm@5.5.0";
const FIT = "https://esm.sh/@xterm/addon-fit@0.10.0";
const XTERM_CSS = "https://cdn.jsdelivr.net/npm/@xterm/xterm@5.5.0/css/xterm.css";

let term = null;
let fitAddon = null;
let handler = null;

function ensureCss() {
    if (document.getElementById("xterm-css")) return;
    const link = document.createElement("link");
    link.id = "xterm-css";
    link.rel = "stylesheet";
    link.href = XTERM_CSS;
    document.head.appendChild(link);
}

export async function init(container, dotnetRef) {
    ensureCss();
    const [{ Terminal }, { FitAddon }] = await Promise.all([import(XTERM), import(FIT)]);

    handler = dotnetRef;
    term = new Terminal({
        cursorBlink: true,
        fontFamily: "'JetBrains Mono', ui-monospace, monospace",
        fontSize: 13,
        theme: { background: "#05070d", foreground: "#e6edf7", cursor: "#38e8d0" },
    });
    fitAddon = new FitAddon();
    term.loadAddon(fitAddon);
    term.open(container);
    try { fitAddon.fit(); } catch { /* container not laid out yet */ }

    term.onData((d) => handler?.invokeMethodAsync("OnInput", d));
    window.addEventListener("resize", onResize);

    return { cols: term.cols, rows: term.rows };
}

function onResize() {
    try { fitAddon?.fit(); } catch { /* ignore */ }
}

export function write(text) {
    term?.write(text);
}

export function fit() {
    if (!fitAddon || !term) return null;
    try { fitAddon.fit(); } catch { /* ignore */ }
    return { cols: term.cols, rows: term.rows };
}

export function focus() {
    term?.focus();
}

export function dispose() {
    window.removeEventListener("resize", onResize);
    try { term?.dispose(); } catch { /* ignore */ }
    term = null;
    fitAddon = null;
    handler = null;
}
