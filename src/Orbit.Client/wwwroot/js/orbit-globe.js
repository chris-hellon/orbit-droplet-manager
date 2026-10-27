// Orbit globe renderer.
//
// Renders each droplet as a glowing pillar on a 3D globe using globe.gl:
//   - pillar height  = CPU%  (taller = busier)
//   - pillar colour  = health (grey / green / amber / red)
//   - pulsing ring   = an action (reboot, snapshot, ...) is in progress
//   - multiple droplets in one region orbit the region point as a small cluster
//
// globe.gl (and its three.js dependency) are loaded from a CDN as ES modules. For a fully
// offline single-image deploy, vendor these into wwwroot and import them locally instead.

const GLOBE_CDN = "https://esm.sh/globe.gl@2";
const TEX = "https://unpkg.com/three-globe/example/img";

const HEALTH_COLORS = ["#a9c2ea", "#3ff09a", "#ffc24b", "#ff6678"]; // unknown, healthy, warning, critical
const HEALTH_RGB = ["169,194,234", "63,240,154", "255,194,75", "255,102,120"];
const ACTION_RGB = "56, 232, 208";

function healthRgb(d) {
    if (!d.isOn) return HEALTH_RGB[0];
    return HEALTH_RGB[d.health] ?? HEALTH_RGB[0];
}

const state = {
    globe: null,
    container: null,
    tooltip: null,
    handler: null,
    droplets: [],
    centered: false,
    fleetCenter: null,
    resizeObserver: null,
    mouse: { x: 0, y: 0 },
    autoRotate: false,
    onMouseMove: null,
};

function healthColor(d) {
    if (!d.isOn) return HEALTH_COLORS[0];
    return HEALTH_COLORS[d.health] ?? HEALTH_COLORS[0];
}

// Low, wide discs sitting on the surface (deliberately not pillars) — still tall enough to hover/click.
function altitude() {
    return 0.06;
}

// Dot size carries CPU load (busier = bigger); colour carries health, pulse ring carries liveness.
// Kept generous so the marker is an easy hover/click target.
function radius(d) {
    if (!d.isOn) return 1.6;
    const cpu = typeof d.cpu === "number" ? d.cpu : 25;
    return 1.7 + (Math.max(0, Math.min(100, cpu)) / 100) * 0.8;
}

// Spread droplets that share a region around the region point so clusters read as an orbit.
function withClusterOffsets(droplets) {
    const groups = new Map();
    for (const d of droplets) {
        const key = d.regionSlug || `${d.lat},${d.lng}`;
        if (!groups.has(key)) groups.set(key, []);
        groups.get(key).push(d);
    }

    const out = [];
    for (const members of groups.values()) {
        const n = members.length;
        const r = n > 1 ? 1.6 + Math.min(n, 8) * 0.3 : 0;
        members.forEach((d, i) => {
            const angle = (i / n) * Math.PI * 2;
            out.push({ ...d, dispLat: d.lat + r * Math.sin(angle), dispLng: d.lng + r * Math.cos(angle) });
        });
    }
    return out;
}

function escapeHtml(s) {
    return String(s ?? "").replace(/[&<>"']/g, (c) =>
        ({ "&": "&amp;", "<": "&lt;", ">": "&gt;", '"': "&quot;", "'": "&#39;" }[c]));
}

function healthWord(d) {
    if (!d.isOn) return "off";
    return ["no data", "healthy", "warning", "critical"][d.health] ?? "no data";
}

function tooltipHtml(d) {
    const cpu = typeof d.cpu === "number" ? `${d.cpu.toFixed(0)}%` : "—";
    const mem = typeof d.memory === "number" ? `${d.memory.toFixed(0)}%` : "—";
    const color = healthColor(d);
    return `
        <div class="gt-name">${escapeHtml(d.name)}</div>
        <div class="gt-sub">${escapeHtml(d.regionName)} · ${escapeHtml(d.sizeSlug)}</div>
        <div class="gt-row"><span class="gt-dot" style="background:${color}"></span>${escapeHtml(healthWord(d))}</div>
        <div class="gt-metrics"><span>CPU ${cpu}</span><span>RAM ${mem}</span></div>`;
}

function showTooltip(d) {
    if (!state.tooltip) return;
    state.tooltip.innerHTML = tooltipHtml(d);
    state.tooltip.style.display = "block";
    positionTooltip();
}

function hideTooltip() {
    if (state.tooltip) state.tooltip.style.display = "none";
}

function positionTooltip() {
    if (!state.tooltip || state.tooltip.style.display === "none") return;
    const pad = 14;
    const { x, y } = state.mouse;
    const w = state.tooltip.offsetWidth;
    const h = state.tooltip.offsetHeight;
    const cw = state.container.clientWidth;
    let left = x + pad;
    let top = y + pad;
    if (left + w > cw) left = x - w - pad;
    if (top + h > state.container.clientHeight) top = y - h - pad;
    state.tooltip.style.left = `${Math.max(4, left)}px`;
    state.tooltip.style.top = `${Math.max(4, top)}px`;
}

// A pulsing HTML marker per droplet: reliably visible, real pointer cursor, hover tooltip, clickable.
function createMarker(d) {
    const el = document.createElement("div");
    el.className = "gmarker" + (d.actionInProgress ? " acting" : "");
    el.style.setProperty("--mc", healthColor(d));
    // Core size nudges up with CPU so busy droplets read a touch bigger.
    const cpu = typeof d.cpu === "number" ? d.cpu : 20;
    el.style.setProperty("--sz", `${10 + (Math.max(0, Math.min(100, cpu)) / 100) * 6}px`);
    el.innerHTML = '<span class="gm-pulse"></span><span class="gm-core"></span>';

    el.addEventListener("click", (e) => {
        e.stopPropagation();
        if (state.handler) state.handler.invokeMethodAsync("OnDropletSelected", d.id);
    });
    el.addEventListener("mouseenter", () => showTooltip(d));
    el.addEventListener("mousemove", () => showTooltip(d));
    el.addEventListener("mouseleave", () => hideTooltip());
    return el;
}

export async function init(container, handler) {
    state.container = container;
    state.handler = handler;

    const { default: Globe } = await import(GLOBE_CDN);

    const globe = Globe()(container)
        .width(container.clientWidth)
        .height(container.clientHeight)
        .backgroundColor("#05070d")
        .globeImageUrl(`${TEX}/earth-night.jpg`)
        .bumpImageUrl(`${TEX}/earth-topology.png`)
        .showAtmosphere(true)
        .atmosphereColor("#4d8bff")
        .atmosphereAltitude(0.2)
        // Flat 2D HTML markers — always face the camera (no 3D protrusion), pulse via CSS, clickable.
        .htmlElementsData([])
        .htmlLat((d) => d.dispLat)
        .htmlLng((d) => d.dispLng)
        .htmlAltitude(0.005)
        .htmlElementVisibilityModifier((el, isVisible) => {
            el.style.opacity = isVisible ? "1" : "0";
            el.style.pointerEvents = isVisible ? "auto" : "none";
        })
        .htmlElement(createMarker);

    globe.controls().autoRotate = false; // still by default — pulsing markers provide the life; drag to rotate
    globe.controls().autoRotateSpeed = 0.28;
    globe.controls().enableDamping = true;
    globe.controls().minDistance = 180;
    // The globe lives inside a scrollable dashboard — let the wheel scroll the page, not zoom the globe.
    globe.controls().enableZoom = false;

    globe.pointOfView({ lat: 20, lng: 0, altitude: 2.4 }, 0);
    state.globe = globe;

    // Custom tooltip — created AFTER globe init, since Globe()(container) clears the container.
    const tooltip = document.createElement("div");
    tooltip.className = "globe-tooltip";
    tooltip.style.display = "none";
    container.appendChild(tooltip);
    state.tooltip = tooltip;

    state.onMouseMove = (e) => {
        const rect = container.getBoundingClientRect();
        state.mouse = { x: e.clientX - rect.left, y: e.clientY - rect.top };
        positionTooltip();
    };
    state.onMouseLeave = () => hideTooltip();
    container.addEventListener("mousemove", state.onMouseMove);
    container.addEventListener("mouseleave", state.onMouseLeave);

    state.resizeObserver = new ResizeObserver(() => {
        if (!state.container) return;
        globe.width(state.container.clientWidth).height(state.container.clientHeight);
    });
    state.resizeObserver.observe(container);
}

export function setDroplets(droplets) {
    if (!state.globe) return;
    state.droplets = withClusterOffsets(droplets ?? []);
    state.globe.htmlElementsData(state.droplets);

    // Remember the fleet's centre (used on first load and when closing the detail panel).
    if (state.droplets.length) {
        const lat = state.droplets.reduce((s, d) => s + d.dispLat, 0) / state.droplets.length;
        const lng = state.droplets.reduce((s, d) => s + d.dispLng, 0) / state.droplets.length;
        state.fleetCenter = { lat, lng };
        // Deferred a beat: calling pointOfView the instant the globe is created gets ignored.
        if (!state.centered) {
            state.centered = true;
            setTimeout(() => {
                if (state.globe) state.globe.pointOfView({ lat, lng, altitude: 2.1 }, 1200);
            }, 350);
        }
    }
}

export function focus(id) {
    if (!state.globe) return;
    const d = state.droplets.find((x) => x.id === id);
    if (!d) return;
    state.autoRotate = false;
    state.globe.controls().autoRotate = false;
    state.globe.pointOfView({ lat: d.dispLat, lng: d.dispLng, altitude: 1.35 }, 900);
}

export function resetView() {
    if (!state.globe) return;
    const c = state.fleetCenter || { lat: 20, lng: 0 };
    state.globe.pointOfView({ lat: c.lat, lng: c.lng, altitude: 2.1 }, 900);
}

export function dispose() {
    try {
        if (state.container) {
            if (state.onMouseMove) state.container.removeEventListener("mousemove", state.onMouseMove);
            if (state.onMouseEnter) state.container.removeEventListener("mouseenter", state.onMouseEnter);
            if (state.onMouseLeave) state.container.removeEventListener("mouseleave", state.onMouseLeave);
        }
        state.resizeObserver?.disconnect();
        if (state.globe) {
            state.globe._destructor?.();
            state.globe = null;
        }
        if (state.container) state.container.innerHTML = "";
    } catch {
        /* ignore teardown errors */
    }
    state.droplets = [];
    state.handler = null;
    state.container = null;
    state.tooltip = null;
}
