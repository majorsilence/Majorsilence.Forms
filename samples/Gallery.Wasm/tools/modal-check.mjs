// Runs the Gallery.Wasm browser-head checks (ModalCheckForm.cs, issue #406) in headless Chrome and
// prints what each blocking or awaited modal call did: RETURNED, THREW, or HUNG.
//
// The rendering checks (timeradd, dialogvisual, messageboxvisual, owneddialogvisual) also log a SNAP line
// saying what the screen should show at that moment; this screenshots the page then, reads the pixels
// (and clicks the dialog's OK button), and prints what it found. With --expect a disagreement is a
// mismatch like any other.
//
// The a11y check steps through popups and announcements after READY (a STEP line each); this reads the
// accessibility DOM after each one, records what the live regions said (LIVE lines) and flags any
// aria-controls/-describedby/-activedescendant naming an element that is not there (DANGLING lines).
//
//   dotnet publish samples/Gallery.Wasm -c Release -o out
//   node samples/Gallery.Wasm/tools/modal-check.mjs out/wwwroot [--expect] [--report=<file>] [check ...]
//
// Without --expect it only reports, and exits 0 whatever the checks did: that is the mode for measuring
// a platform change. With --expect (what CI runs) each check is compared with the `expected` table
// below and the script exits 1 if any differs; --report writes every result, and every mismatch, as JSON.
// Exit code 2 means the harness itself could not run (no bundle, no Chrome).
//
// Node 22+ (built-in WebSocket) and a local Chrome/Chromium; set CHROME to its path if it is not in
// the usual place. Nothing is installed and nothing leaves the machine: the bundle is served from
// 127.0.0.1 and Chrome is driven over its DevTools protocol.
//
// Each check gets its own page load, because a call that blocks the page's only thread stops
// everything after it; such a call shows up here as a START with no RETURNED/THREW, reported as HUNG.

import { spawn } from "node:child_process";
import { createServer } from "node:http";
import { existsSync, mkdirSync, mkdtempSync, readFileSync, rmSync, statSync, writeFileSync } from "node:fs";
import { inflateSync } from "node:zlib";
import { tmpdir } from "node:os";
import { dirname, extname, join, normalize, resolve, sep } from "node:path";

// What each check should do today. When the platform moves (BACKLOG.md, "Browser blocking calls: re-evaluate
// when the platform moves") and a blocking call starts to return, this table is what changes.
//
//   outcome   RETURNED, THREW, READY (a11y) -- or HUNG / NO RESULT, which never match.
//   detail    a regular expression the rest of the outcome line must match.
//   after     the AFTER line: what the call left behind (a dialog still open, a disabled owner).
//   steps     a11y only: what the accessibility DOM shows after READY ("ready") and after each STEP the
//             page logs; a step that is never logged is a mismatch, and so is a dangling reference in any
//             read. Per step:
//     nodes     each entry must match one mirrored element: a string is the exact value, a null value
//               means "attribute absent", a RegExp must match, and { ref: {...} } means the attribute is
//               an id whose element matches that entry (aria-controls, aria-describedby).
//     absent    no mirrored element may match any of these entries.
//     activeDescendant  what aria-activedescendant points at: a data-mf-automation-id, or an entry.
//     live      exactly what the live regions said during the step, as [region, text] pairs, in order.
//   logs      regular expressions some MFCHECK line must match.
//   snaps     the rendering checks: how many SNAP lines the page must log. Every SNAP is checked against
//             the screenshot taken when it arrives (colour probes, the dialog's drawn edges, the
//             accessibility DOM, the click answering the dialog), and any failure there is a mismatch.
//
// Any PAGE EXCEPTION fails a check too.
const blocking = (api, alternative) => ({
    outcome: "THREW",
    detail: new RegExp(`^System\\.PlatformNotSupportedException: ${reEscape(api)} blocks until the dialog is closed\\b.*\\bUse ${reEscape(alternative)} and await it instead\\.`),
    after: "open forms=1, owner enabled=True",
});
const awaited = result => ({ outcome: "RETURNED", detail: new RegExp(`^${result}$`), after: "open forms=1, owner enabled=True" });
// A rendering check: it returns what the harness's click answered (OK) and every SNAP matched the pixels;
// `after` null means it logs no AFTER line.
const rendered = (result, after = "open forms=1, owner enabled=True") => ({ outcome: "RETURNED", detail: new RegExp(`^${result}$`), after, snaps: 1 });

const expected = {
    showdialog: blocking("Form.ShowDialog", "Form.ShowDialogAsync"),
    messagebox: blocking("MessageBox.Show", "MessageBox.ShowAsync"),
    commondialog: blocking("FileDialog.ShowDialog", "FileDialog.ShowDialogAsync"),
    taskdialog: blocking("TaskDialog.ShowDialog", "TaskDialog.ShowDialogAsync"),
    showdialogasync: awaited("OK"),
    // Closing a message box without clicking a button is Cancel, as on Windows.
    messageboxasync: awaited("Cancel"),
    taskdialogasync: awaited("OK"),
    a11y: {
        outcome: "READY",
        logs: [/^MFCHECK A11Y-MESSAGEBOX Cancel$/],
        steps: {
            ready: {
                activeDescendant: "nameBox",
                nodes: [
                    { role: "region", "aria-label": "Modal check: a11y", "data-mf-automation-id": null },
                    { "data-mf-automation-id": "status", role: null, text: "Running check 'a11y'. Results go to the console or device log (MFCHECK lines)." },
                    { "data-mf-automation-id": "okButton", role: "button", text: "OK", "aria-disabled": null },
                    { "data-mf-automation-id": "agree", role: "checkbox", "aria-checked": "true", text: "I agree" },
                    { "data-mf-automation-id": "nameBox", role: "textbox", "aria-label": "Customer name", text: "Ada" },
                    { "data-mf-automation-id": "disabledButton", role: "button", "aria-disabled": "true", text: "Unavailable" },
                    { "data-mf-automation-id": "fruit", role: "combobox", "aria-label": "Fruit", "aria-expanded": "false", "aria-valuetext": "Banana" },
                    { "data-mf-automation-id": "savedLabel", role: null, text: "Not saved" },
                    { role: "menubar" },
                    { role: "menuitem", text: "File", "aria-haspopup": "menu", "aria-expanded": "false" },
                    // The status element is not live itself, so its implicit politeness does not repeat
                    // what the live region says.
                    { role: "status", "aria-live": "off" },
                ],
                absent: [{ "data-mf-popup": /./ }],
                // Nothing on the first sync: the page just loaded.
                live: [],
            },
            "combo-open": {
                nodes: [
                    { "data-mf-automation-id": "fruit", "aria-expanded": "true", "aria-controls": { ref: { role: "listbox", "aria-label": "Fruit" } } },
                    { "data-mf-popup": "listbox" },
                    { role: "option", text: "Apple", "aria-selected": "false" },
                    { role: "option", text: "Banana", "aria-selected": "true" },
                    { role: "option", text: "Cherry", "aria-selected": "false" },
                ],
                // While the list is open, focus is on its highlighted item.
                activeDescendant: { role: "option", text: "Banana" },
                // Focus arriving on the combo box is the reader's to announce.
                live: [],
            },
            "combo-close": {
                nodes: [{ "data-mf-automation-id": "fruit", "aria-expanded": "false", "aria-controls": null }],
                absent: [{ "data-mf-popup": /./ }, { role: "listbox" }],
                activeDescendant: "fruit",
                live: [],
            },
            "combo-value": {
                nodes: [{ "data-mf-automation-id": "fruit", "aria-valuetext": "Cherry" }],
                live: [["polite", "Cherry"]],
            },
            "menu-open": {
                nodes: [
                    { role: "menuitem", text: "File", "aria-expanded": "true", "aria-controls": { ref: { role: "menu", "aria-label": "File" } } },
                    { "data-mf-popup": "menu" },
                    { role: "menuitem", text: "Open" },
                    { role: "menuitem", text: "Recent", "aria-haspopup": "menu", "aria-expanded": "true", "aria-controls": { ref: { role: "menu", "aria-label": "Recent" } } },
                    { role: "menuitemcheckbox", text: "Word wrap", "aria-checked": "true" },
                    { role: "menuitem", text: "Exit", "aria-disabled": "true" },
                    { role: "menuitem", text: "notes.txt" },
                ],
                live: [],
            },
            "menu-close": {
                nodes: [{ role: "menuitem", text: "File", "aria-expanded": "false", "aria-controls": null }],
                absent: [{ "data-mf-popup": /./ }, { role: "menu" }],
                live: [],
            },
            tooltip: {
                nodes: [
                    { role: "tooltip", "data-mf-popup": "tooltip", text: "Saves the order" },
                    { "data-mf-automation-id": "okButton", "aria-describedby": { ref: { role: "tooltip" } } },
                ],
                live: [],
            },
            "tooltip-hide": {
                nodes: [{ "data-mf-automation-id": "okButton", "aria-describedby": null }],
                absent: [{ role: "tooltip" }],
                live: [],
            },
            // A live label (LiveSetting = Polite) and a status bar both changed.
            status: {
                nodes: [{ "data-mf-automation-id": "savedLabel", text: "Saved" }, { role: null, text: "3 records loaded" }],
                live: [["polite", "Saved"], ["polite", "3 records loaded"]],
            },
            notify: { live: [["assertive", "Upload complete"]] },
            // An error message box: an alert dialog described by its message, announced at once.
            messagebox: {
                nodes: [{ role: "alertdialog", "aria-label": "Save failed", "aria-modal": "true", "aria-describedby": { ref: { text: "The disk is full." } } }],
                live: [["assertive", "Save failed. The disk is full."]],
            },
            "messagebox-closed": {
                absent: [{ role: "alertdialog" }],
                activeDescendant: "fruit",
                live: [],
            },
        },
    },
    // A control added from a Timer.Tick after the form is up reaches the canvas and the accessibility DOM.
    // It opens no dialog, so it logs no AFTER line.
    timeradd: rendered("added", null),
    // While an awaited dialog is open its owner is still drawn behind it, its frame is drawn whole, and
    // a click on its OK button answers it.
    dialogvisual: rendered("OK"),
    messageboxvisual: rendered("OK"),
    // The owner is a second top-level form, still open when the AFTER line is logged.
    owneddialogvisual: rendered("OK", "open forms=2, owner enabled=True"),
};

function reEscape(s) { return s.replace(/[.*+?^${}()|[\]\\]/g, "\\$&"); }

const args = process.argv.slice(2);
const flags = new Map(args.filter(a => a.startsWith("--")).map(a => { const [k, ...v] = a.slice(2).split("="); return [k, v.join("=")]; }));
const positional = args.filter(a => !a.startsWith("--"));
for (const k of flags.keys()) {
    if (k !== "expect" && k !== "report") {
        console.error(`Unknown option --${k}. Options: --expect, --report=<file>.`);
        process.exit(2);
    }
}

const expectMode = flags.has("expect");
const reportFile = flags.get("report") || null;
const root = resolve(positional[0] ?? "out/wwwroot");
const checks = positional.length > 1 ? positional.slice(1) : Object.keys(expected);
const timeoutMs = Number(process.env.MF_CHECK_TIMEOUT_MS ?? 20000);
// How long after READY or a STEP the a11y check first reads the page: the mirror syncs at most every
// 100 ms and the polite live region waits 250 ms. With --expect a read that does not match yet is
// retried until it does, the page logs its next line, or MF_A11Y_STEP_MS is up.
const a11ySettleMs = Number(process.env.MF_A11Y_SETTLE_MS ?? 700);
const a11yStepMs = Number(process.env.MF_A11Y_STEP_MS ?? 10000);

if (!existsSync(join(root, "index.html"))) {
    console.error(`No index.html under ${root}. Pass the published wwwroot folder.`);
    process.exit(2);
}

const chrome = process.env.CHROME ?? [
    "/Applications/Google Chrome.app/Contents/MacOS/Google Chrome",
    "/Applications/Chromium.app/Contents/MacOS/Chromium",
    "/usr/bin/google-chrome", "/usr/bin/chromium", "/usr/bin/chromium-browser",
    "C:/Program Files/Google/Chrome/Application/chrome.exe",
].find(existsSync);

if (!chrome) {
    console.error("No Chrome/Chromium found; set CHROME to its executable.");
    process.exit(2);
}

const types = {
    ".html": "text/html", ".js": "text/javascript", ".mjs": "text/javascript", ".json": "application/json",
    ".wasm": "application/wasm", ".css": "text/css", ".png": "image/png", ".dat": "application/octet-stream",
    ".dll": "application/octet-stream", ".pdb": "application/octet-stream", ".ico": "image/x-icon",
};

const server = createServer((req, res) => {
    const path = normalize(join(root, decodeURIComponent(new URL(req.url, "http://x").pathname)));
    const file = path.endsWith(sep) ? join(path, "index.html") : path;

    if (!file.startsWith(root) || !existsSync(file) || !statSync(file).isFile()) {
        res.writeHead(404).end();
        return;
    }

    res.writeHead(200, { "Content-Type": types[extname(file)] ?? "application/octet-stream" });
    res.end(readFileSync(file));
});

await new Promise(r => server.listen(0, "127.0.0.1", r));
const base = `http://127.0.0.1:${server.address().port}/`;

const profile = mkdtempSync(join(tmpdir(), "mf-modal-check-"));
// MF_CHECK_CHROME_ARGS adds flags, e.g. --no-sandbox on a CI runner whose kernel refuses Chrome's sandbox.
const extraArgs = (process.env.MF_CHECK_CHROME_ARGS ?? "").split(/\s+/).filter(Boolean);
const browser = spawn(chrome, [
    "--headless=new", "--remote-debugging-port=0", `--user-data-dir=${profile}`,
    "--no-first-run", "--no-default-browser-check", ...extraArgs, "about:blank",
], { stdio: ["ignore", "ignore", "pipe"] });

const wsUrl = await new Promise((resolveUrl, reject) => {
    let err = "";
    browser.stderr.on("data", d => {
        err += d;
        const m = /DevTools listening on (ws:\/\/\S+)/.exec(err);
        if (m) resolveUrl(m[1]);
    });
    browser.on("exit", c => reject(new Error(`Chrome exited (${c}): ${err}`)));
});

const ws = new WebSocket(wsUrl);
await new Promise(r => ws.addEventListener("open", r, { once: true }));

let nextId = 1;
const pending = new Map();
const listeners = new Set();

ws.addEventListener("message", e => {
    const msg = JSON.parse(e.data);
    if (msg.id && pending.has(msg.id)) {
        pending.get(msg.id)(msg);
        pending.delete(msg.id);
    } else {
        for (const l of listeners) l(msg);
    }
});

const send = (method, params = {}, sessionId) => new Promise(r => {
    const id = nextId++;
    pending.set(id, r);
    ws.send(JSON.stringify({ id, method, params, sessionId }));
});

// Reads the accessibility DOM next to the canvas: every mirrored element with the attributes the checks
// look at, its text and its box (and its page box, for popups and the controls they belong to, to
// check placement); what the live regions said since the last read, recorded by the observer below;
// and every aria-controls/-describedby/-activedescendant that names a missing element.
const a11yRead = `(() => {
    const root = document.querySelector('.mf-a11y-root');
    if (!root) return { error: 'NO .mf-a11y-root ELEMENT' };
    const names = ['role', 'aria-label', 'aria-checked', 'aria-selected', 'aria-disabled', 'aria-modal', 'aria-expanded',
        'aria-haspopup', 'aria-controls', 'aria-describedby', 'aria-pressed', 'aria-live', 'aria-valuetext',
        'data-mf-popup', 'data-mf-automation-id'];
    const nodes = [];
    const walk = (el, depth) => {
        for (const c of el.children) {
            if (c.classList.contains('mf-a11y-text') || c.classList.contains('mf-a11y-live')) continue;
            const attrs = {};
            for (const a of names) if (c.hasAttribute(a)) attrs[a] = c.getAttribute(a);
            const text = c.querySelector(':scope > .mf-a11y-text');
            const linked = c.hasAttribute('data-mf-popup') || c.hasAttribute('aria-controls') || c.hasAttribute('aria-describedby');
            const r = c.getBoundingClientRect();
            nodes.push({ id: c.id, depth, attrs, text: text ? text.textContent : null,
                box: c.style.left + ',' + c.style.top + ' ' + c.style.width + ' x ' + c.style.height,
                page: linked ? Math.round(r.left) + ',' + Math.round(r.top) + '-' + Math.round(r.right) + ',' + Math.round(r.bottom) : null });
            walk(c, depth + 1);
        }
    };
    walk(root, 0);
    const host = root.closest('.avalonia-container');
    const live = (window.__mfLive ?? []).splice(0).map(e => [e.region, e.text]);
    const dangling = [...document.querySelectorAll('[aria-controls],[aria-describedby],[aria-activedescendant]')]
        .flatMap(e => ['aria-controls', 'aria-describedby', 'aria-activedescendant'].map(a => e.getAttribute(a)).filter(Boolean))
        .filter(id => !document.getElementById(id));
    return { activeDescendant: host && host.getAttribute('aria-activedescendant'), nodes, live, dangling };
})()`;

// Records what the accessibility DOM's live regions say, as a screen reader would hear it: each line
// added to a [data-mf-live] region. Installed before the page's own scripts run.
const liveObserver = `window.__mfLive = [];
new MutationObserver(records => {
    for (const r of records) for (const n of r.addedNodes) {
        const region = n.parentElement && n.parentElement.closest('[data-mf-live]');
        if (region && n.nodeType === 1 && !n.matches('[data-mf-live]')) window.__mfLive.push({ region: region.getAttribute('data-mf-live'), text: n.textContent });
    }
}).observe(document, { childList: true, subtree: true });`;

const formatA11y = a11y => a11y.error ?? ["active-descendant=" + a11y.activeDescendant, ...a11y.nodes.map(n =>
    "  ".repeat(n.depth) + "#" + n.id + " " + Object.entries(n.attrs).map(([k, v]) => k + "=" + JSON.stringify(v)).join(" ")
    + (n.text !== null ? " text=" + JSON.stringify(n.text) : "") + " @" + n.box + (n.page ? " page=" + n.page : "")),
    ...(a11y.live ?? []).map(([region, text]) => `LIVE ${region}: ${JSON.stringify(text)}`),
    ...(a11y.dangling ?? []).map(id => "DANGLING REFERENCE " + id)].join("\n");

// Prints an expectation entry, RegExps included.
const show = v => JSON.stringify(v, (_, x) => x instanceof RegExp ? String(x) : x);

// The ways one read of the accessibility DOM differs from a step's expectation; empty when it matches.
// `live` is everything the live regions said during the step so far.
function a11yProblems(a11y, want, live) {
    if (!a11y || a11y.error) return [a11y?.error ?? "the accessibility DOM could not be read"];
    const problems = [];
    const byId = id => a11y.nodes.find(n => n.id === id);
    const matches = (n, entry) => Object.entries(entry).every(([k, v]) => {
        const got = k === "text" ? n.text : n.attrs[k] ?? null;
        if (v === null || typeof v === "string") return got === v;
        if (v instanceof RegExp) return got !== null && v.test(got);
        if (v?.ref) { const target = got && byId(got); return !!target && matches(target, v.ref); }
        return false;
    });
    for (const entry of want.nodes ?? [])
        if (!a11y.nodes.some(n => matches(n, entry))) problems.push("no mirrored element matching " + show(entry));
    for (const entry of want.absent ?? []) {
        const found = a11y.nodes.find(n => matches(n, entry));
        if (found) problems.push(`#${found.id} should not be mirrored now (matches ${show(entry)})`);
    }
    if (want.activeDescendant) {
        const entry = typeof want.activeDescendant === "string" ? { "data-mf-automation-id": want.activeDescendant } : want.activeDescendant;
        const target = byId(a11y.activeDescendant);
        if (!target || !matches(target, entry))
            problems.push(`aria-activedescendant is ${JSON.stringify(a11y.activeDescendant)}, not an element matching ${show(entry)}`);
    }
    if (want.live && JSON.stringify(live) !== JSON.stringify(want.live))
        problems.push(`live regions said ${JSON.stringify(live)}, expected ${JSON.stringify(want.live)}`);
    for (const id of a11y.dangling ?? []) problems.push(`an ARIA reference names #${id}, which is not in the page`);
    return problems;
}

// Decodes the 8-bit RGB/RGBA, non-interlaced PNG Chrome's screenshots are, into a pixel reader.
function decodePng(buf) {
    let pos = 8, width = 0, height = 0, channels = 0;
    const idat = [];
    while (pos < buf.length) {
        const len = buf.readUInt32BE(pos), type = buf.toString("ascii", pos + 4, pos + 8), data = buf.subarray(pos + 8, pos + 8 + len);
        if (type === "IHDR") {
            width = data.readUInt32BE(0); height = data.readUInt32BE(4);
            if (data[8] !== 8 || data[12] !== 0 || (data[9] !== 2 && data[9] !== 6)) throw new Error("unsupported PNG");
            channels = data[9] === 6 ? 4 : 3;
        } else if (type === "IDAT") idat.push(data);
        pos += 12 + len;
    }
    const raw = inflateSync(Buffer.concat(idat)), stride = width * channels, px = Buffer.alloc(stride * height);
    for (let y = 0; y < height; y++) {
        const f = raw[y * (stride + 1)], src = y * (stride + 1) + 1, row = y * stride;
        for (let i = 0; i < stride; i++) {
            const a = i >= channels ? px[row + i - channels] : 0, b = y > 0 ? px[row - stride + i] : 0;
            const c = i >= channels && y > 0 ? px[row - stride + i - channels] : 0;
            const p = a + b - c, pa = Math.abs(p - a), pb = Math.abs(p - b), pc = Math.abs(p - c);
            const pred = f === 1 ? a : f === 2 ? b : f === 3 ? (a + b) >> 1 : f === 4 ? (pa <= pb && pa <= pc ? a : pb <= pc ? b : c) : 0;
            px[row + i] = (raw[src + i] + pred) & 255;
        }
    }
    return { width, height, at: (x, y) => {
        if (x < 0 || y < 0 || x >= width || y >= height) return null;
        const o = y * stride + x * channels;
        return [px[o], px[o + 1], px[o + 2]];
    } };
}

const hex = c => c ? c.map(v => v.toString(16).padStart(2, "0")).join("") : "off-screen";
const near = (c, want) => c && [0, 1, 2].every(i => Math.abs(c[i] - parseInt(want.substr(i * 2, 2), 16)) <= 8);

// Checks one SNAP: the probes' colours, the dialog's drawn edges against its bounds, and the
// accessibility DOM; then clicks where it asks. Coordinates come from the page in CSS pixels; the
// screenshot is device pixels. Returns what matched (notes) and what did not (failures).
async function checkSnap(spec, sessionId, check, index) {
    const shot = await send("Page.captureScreenshot", { format: "png" }, sessionId);
    const png = Buffer.from(shot.result.data, "base64");
    if (process.env.MF_CHECK_SCREENSHOTS) {
        mkdirSync(process.env.MF_CHECK_SCREENSHOTS, { recursive: true });
        writeFileSync(join(process.env.MF_CHECK_SCREENSHOTS, `${check}-snap${index}.png`), png);
    }
    const dpr = (await send("Runtime.evaluate", { expression: "devicePixelRatio", returnByValue: true }, sessionId)).result.result.value ?? 1;
    const img = decodePng(png);
    const at = (x, y) => img.at(Math.round(x * dpr), Math.round(y * dpr));
    const failures = [], notes = [];

    for (const p of spec.probes ?? []) {
        const got = at(p.x, p.y);
        (near(got, p.rgb) ? notes : failures).push(`${p.what} at ${p.x},${p.y}: ${hex(got)} (want ${p.rgb})`);
    }

    if (spec.dialog) {
        // Walk in from a few pixels beyond each far edge, across the owner, to the first pixel that is
        // not the owner's colour: that is where the dialog's drawing really ends.
        const d = spec.dialog, edge = (x, y, dx, dy) => {
            if (!near(at(x, y), spec.owner)) return { start: hex(at(x, y)) };
            for (let i = 0; i < 40; i++, x += dx, y += dy)
                if (!near(at(x, y), spec.owner)) return { at: dx ? x : y };
            return {};
        };
        const right = edge(d.x + d.w + 6, d.y + Math.floor(d.h / 2), -1, 0);
        const bottom = edge(d.x + Math.floor(d.w / 2), d.y + d.h + 6, 0, -1);
        // The frame is drawn whole when each far edge is where the bounds say and is drawn in the
        // same colour as the opposite, near edge -- a clipped frame ends in the dialog's background.
        const midX = d.x + Math.floor(d.w / 2), midY = d.y + Math.floor(d.h / 2);
        for (const [name, found, want, edgeAt, nearAt] of [
            ["right", right, d.x + d.w - 1, v => at(v, midY), () => at(d.x, midY)],
            ["bottom", bottom, d.y + d.h - 1, v => at(midX, v), () => at(midX, d.y)]]) {
            if (found.start) {
                failures.push(`${name}: the owner is not drawn beyond the dialog (${found.start} instead of ${spec.owner})`);
                continue;
            }
            const line = `dialog ${name} edge: drawn to ${found.at ?? "?"}, bounds end at ${want}`;
            (found.at !== undefined && Math.abs(found.at - want) <= 1 ? notes : failures).push(line);
            if (found.at !== undefined) {
                const far = edgeAt(found.at), nearEdge = nearAt();
                (near(far, hex(nearEdge)) ? notes : failures).push(
                    `dialog ${name} frame: ${hex(far)}, opposite edge ${hex(nearEdge)}`);
            }
        }
    }

    if (spec.a11y) {
        const has = (await send("Runtime.evaluate", {
            expression: `[...document.querySelectorAll('.mf-a11y-root *')].some(e => (e.getAttribute('aria-label') ?? '') === ${JSON.stringify(spec.a11y)} || e.textContent === ${JSON.stringify(spec.a11y)})`,
            returnByValue: true }, sessionId)).result.result.value;
        (has ? notes : failures).push(`accessibility DOM ${has ? "has" : "lacks"} "${spec.a11y}"`);
    }

    // Then answer the dialog the way a user would. The page logs what it returned; a click that
    // never arrived shows up there as the fallback's Cancel.
    if (spec.click) {
        for (const type of ["mousePressed", "mouseReleased"])
            await send("Input.dispatchMouseEvent", { type, x: spec.click.x, y: spec.click.y, button: "left", clickCount: 1 }, sessionId);
        notes.push(`clicked ${spec.click.x},${spec.click.y}`);
    }

    return { failures, notes, result: spec.result };
}

// The ways a check's result differs from its expectation; empty when it matches.
function problemsOf(r) {
    const want = expected[r.check];
    if (!want) return [`no expectation for check '${r.check}' (known: ${Object.keys(expected).join(", ")})`];
    const problems = [];
    const [outcome] = r.verdict.split(" ");
    const detail = r.verdict.slice(outcome.length + 1);
    if (outcome !== want.outcome) problems.push(`expected ${want.outcome}, got ${r.verdict}`);
    else if (want.detail && !want.detail.test(detail)) problems.push(`${outcome} detail does not match ${want.detail}: ${detail}`);
    if (want.after) {
        const after = r.lines.find(l => l.includes("MFCHECK AFTER"))?.replace(/^.*MFCHECK AFTER /, "");
        if (after !== want.after) problems.push(`expected AFTER "${want.after}", got ${after === undefined ? "no AFTER line" : `"${after}"`}`);
    }
    for (const l of r.lines.filter(l => l.startsWith("PAGE EXCEPTION"))) problems.push(l);
    for (const re of want.logs ?? [])
        if (!r.lines.some(l => re.test(l.replace(/^.*?(?=MFCHECK )/, "")))) problems.push(`no MFCHECK line matching ${re}`);
    if (want.steps && outcome === "READY") {
        for (const [name, step] of Object.entries(want.steps)) {
            const read = r.a11y?.find(s => s.step === name);
            if (!read) { problems.push(`step '${name}' was never logged (or never read)`); continue; }
            for (const p of a11yProblems(read.dom, step, read.live)) problems.push(`${name}: ${p}`);
        }
    }
    if (want.snaps !== undefined && r.snaps.length < want.snaps)
        problems.push(`expected ${want.snaps} SNAP line(s), got ${r.snaps.length} -- the page never asked for its screenshot`);
    for (const f of r.snaps.flatMap(s => s.failures)) problems.push("snap: " + f);
    return problems;
}

// Reads the accessibility DOM after an a11y step: once after a11ySettleMs; with --expect, again every
// 200 ms while it differs from the step's expectation and the page has not moved on to its next line.
// Live-region output is collected across the reads.
async function readStep(sessionId, step, lines) {
    const linesAtStep = lines.length;
    const started = Date.now();
    const want = expected.a11y?.steps?.[step];
    const live = [];
    let dom;
    await new Promise(r => setTimeout(r, a11ySettleMs));
    // MF_CHECK_SCREENSHOTS also keeps what the canvas showed at each step, to compare with the DOM.
    if (process.env.MF_CHECK_SCREENSHOTS) {
        mkdirSync(process.env.MF_CHECK_SCREENSHOTS, { recursive: true });
        const shot = await Promise.race([
            send("Page.captureScreenshot", { format: "png" }, sessionId),
            new Promise(r => setTimeout(() => r(null), 5000)),
        ]);
        if (shot?.result?.data) writeFileSync(join(process.env.MF_CHECK_SCREENSHOTS, `a11y-${step}.png`), Buffer.from(shot.result.data, "base64"));
    }
    for (;;) {
        const evaluated = await Promise.race([
            send("Runtime.evaluate", { expression: a11yRead, returnByValue: true }, sessionId),
            new Promise(r => setTimeout(() => r(null), 5000)),
        ]);
        dom = evaluated?.result?.result?.value ?? { error: "(evaluate did not return)" };
        live.push(...(dom.live ?? []));
        if (!expectMode || !want || !a11yProblems(dom, want, live).length) break;
        if (lines.length > linesAtStep || Date.now() - started > a11yStepMs) break;
        await new Promise(r => setTimeout(r, 200));
    }
    return { step, dom, live };
}

const results = [];

for (const check of checks) {
    const { result: { targetId } } = await send("Target.createTarget", { url: "about:blank" });
    const { result: { sessionId } } = await send("Target.attachToTarget", { targetId, flatten: true });
    const lines = [];
    let done;
    const finished = new Promise(r => done = r);
    const snaps = [];
    const reads = [];

    const onMessage = msg => {
        if (msg.sessionId !== sessionId) return;
        if (msg.method === "Runtime.consoleAPICalled") {
            const text = msg.params.args.map(a => a.value ?? a.description ?? "").join(" ");
            if (process.env.MF_CHECK_VERBOSE) console.log("   [console] " + text.trim());
            if (text.includes("MFCHECK")) {
                lines.push(text.trim());
                const snap = /MFCHECK SNAP (.*)$/.exec(text.trim());
                if (snap) snaps.push(checkSnap(JSON.parse(snap[1]), sessionId, check, snaps.length + 1)
                    .catch(e => ({ failures: [`the screenshot could not be checked: ${e.message}`], notes: [] })));
                // The a11y check steps through popups and announcements after READY; each STEP (and
                // READY itself) is read once the mirror and the polite region's debounce have settled.
                const step = check === "a11y" && /MFCHECK (?:STEP (\S+)|(READY) a11y)/.exec(text);
                if (step) reads.push(readStep(sessionId, step[1] ?? "ready", lines));
                if (/MFCHECK END/.test(text) || (check !== "a11y" && /MFCHECK READY/.test(text))) done();
            }
        } else if (msg.method === "Runtime.exceptionThrown") {
            lines.push("PAGE EXCEPTION " + (msg.params.exceptionDetails.exception?.description ?? msg.params.exceptionDetails.text));
        }
    };

    listeners.add(onMessage);
    await send("Runtime.enable", {}, sessionId);
    await send("Page.enable", {}, sessionId);
    // A background tab gets no animation frames, so the canvas (and the accessibility DOM, which
    // follows painting) would never catch up after the first frame.
    await send("Page.bringToFront", {}, sessionId);
    await send("Emulation.setFocusEmulationEnabled", { enabled: true }, sessionId);
    await send("Page.addScriptToEvaluateOnNewDocument", { source: liveObserver }, sessionId);
    await send("Page.navigate", { url: `${base}index.html?check=${encodeURIComponent(check)}` }, sessionId);

    // The a11y check runs through all its steps, so it gets twice as long.
    const timedOut = await Promise.race([finished.then(() => false), new Promise(r => setTimeout(() => r(true), check === "a11y" ? timeoutMs * 2 : timeoutMs))]);
    const a11y = reads.length ? await Promise.all(reads) : null;

    // MF_CHECK_SCREENSHOTS=<dir> saves what the page showed at the end of each check.
    if (process.env.MF_CHECK_SCREENSHOTS) {
        mkdirSync(process.env.MF_CHECK_SCREENSHOTS, { recursive: true });
        const shot = await Promise.race([
            send("Page.captureScreenshot", { format: "png" }, sessionId),
            new Promise(r => setTimeout(() => r(null), 5000)),
        ]);
        if (shot?.result?.data) writeFileSync(join(process.env.MF_CHECK_SCREENSHOTS, `${check}.png`), Buffer.from(shot.result.data, "base64"));
    }

    listeners.delete(onMessage);

    // A SNAP that asked for a click also says what the click should make the call return.
    const snapResults = await Promise.all(snaps);
    for (const s of snapResults.filter(s => s.result)) {
        const got = lines.find(l => /MFCHECK RETURNED/.test(l))?.replace(/^.*MFCHECK RETURNED /, "");
        if (got !== s.result) s.failures.push(`the click did not answer the dialog (returned ${got ?? "nothing"}, want ${s.result})`);
    }

    const started = lines.some(l => l.includes("MFCHECK START"));
    const outcome = lines.find(l => /MFCHECK (RETURNED|THREW)/.test(l));
    const verdict = outcome ? outcome.replace(/^.*MFCHECK /, "")
        : started && timedOut ? `HUNG (no return within ${timeoutMs} ms)`
        : check === "a11y" && !timedOut ? "READY"
        : timedOut ? `NO RESULT (timed out after ${timeoutMs} ms; the page may not have booted)`
        : "NO RESULT";

    const result = { check, verdict, lines, a11y, snaps: snapResults };
    if (expectMode) result.problems = problemsOf(result);
    results.push(result);
    console.log(`\n== ${check}: ${verdict}`);
    for (const l of lines) console.log("   " + l);
    for (const r of a11y ?? [])
        console.log(`   | -- ${r.step}\n` + formatA11y({ ...r.dom, live: r.live }).split("\n").map(l => "   | " + l).join("\n"));
    for (const r of snapResults) {
        for (const n of r.notes) console.log("   snap ok   " + n);
        for (const f of r.failures) console.log("   snap FAIL " + f);
    }
    for (const p of result.problems ?? []) console.log("   MISMATCH " + p);

    // Closing a target whose thread is blocked still works: the browser process owns it.
    await send("Target.closeTarget", { targetId });
}

ws.close();
browser.kill();
server.close();
try { rmSync(profile, { recursive: true, force: true }); } catch { }

console.log("\nSummary");
for (const r of results) {
    const mark = !expectMode ? "" : r.problems.length ? "FAIL " : "ok   ";
    console.log(`  ${mark}${r.check.padEnd(16)} ${r.verdict}`);
}

const failed = results.filter(r => r.problems?.length);

if (reportFile) {
    mkdirSync(dirname(resolve(reportFile)), { recursive: true });
    writeFileSync(reportFile, JSON.stringify({ expectMode, timeoutMs, failed: failed.map(r => r.check), results }, null, 2));
    console.log(`\nReport written to ${reportFile}`);
}

if (expectMode) {
    console.log(failed.length
        ? `\n${failed.length} of ${results.length} checks differ from what is expected: ${failed.map(r => r.check).join(", ")}`
        : `\nAll ${results.length} checks behaved as expected.`);
    process.exitCode = failed.length ? 1 : 0;
}
