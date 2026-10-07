// Runs the Gallery.Wasm browser-head checks (ModalCheckForm.cs, issue #406) in headless Chrome and
// prints what each blocking or awaited modal call did: RETURNED, THREW, or HUNG.
//
// The rendering checks (timeradd, dialogvisual, messageboxvisual, owneddialogvisual) also log a SNAP line
// saying what the screen should show at that moment; this screenshots the page then, reads the pixels
// (and clicks the dialog's OK button), and prints what it found. With --expect a disagreement is a
// mismatch like any other.
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
//   nodes     a11y only: each entry must match one mirrored element; a null value means "attribute absent".
//   activeDescendant  a11y only: data-mf-automation-id of the element aria-activedescendant points at.
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
        activeDescendant: "nameBox",
        nodes: [
            { role: "region", "aria-label": "Modal check: a11y", "data-mf-automation-id": null },
            { "data-mf-automation-id": "status", role: null, text: "Running check 'a11y'. Results go to the console or device log (MFCHECK lines)." },
            { "data-mf-automation-id": "okButton", role: "button", text: "OK", "aria-disabled": null },
            { "data-mf-automation-id": "agree", role: "checkbox", "aria-checked": "true", text: "I agree" },
            { "data-mf-automation-id": "nameBox", role: "textbox", "aria-label": "Customer name", text: "Ada" },
            { "data-mf-automation-id": "disabledButton", role: "button", "aria-disabled": "true", text: "Unavailable" },
        ],
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
// How long the a11y check waits for the mirror to match (with --expect) or to settle (without).
const a11ySettleMs = Number(process.env.MF_A11Y_SETTLE_MS ?? (expectMode ? 10000 : 1500));

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
// look at, its text and its box.
const a11yRead = `(() => {
    const root = document.querySelector('.mf-a11y-root');
    if (!root) return { error: 'NO .mf-a11y-root ELEMENT' };
    const names = ['role', 'aria-label', 'aria-checked', 'aria-disabled', 'aria-modal', 'data-mf-automation-id'];
    const nodes = [];
    const walk = (el, depth) => {
        for (const c of el.children) {
            if (c.classList.contains('mf-a11y-text')) continue;
            const attrs = {};
            for (const a of names) if (c.hasAttribute(a)) attrs[a] = c.getAttribute(a);
            const text = c.querySelector(':scope > .mf-a11y-text');
            nodes.push({ id: c.id, depth, attrs, text: text ? text.textContent : null,
                box: c.style.left + ',' + c.style.top + ' ' + c.style.width + ' x ' + c.style.height });
            walk(c, depth + 1);
        }
    };
    walk(root, 0);
    const host = root.closest('.avalonia-container');
    return { activeDescendant: host && host.getAttribute('aria-activedescendant'), nodes };
})()`;

const formatA11y = a11y => a11y.error ?? ["active-descendant=" + a11y.activeDescendant, ...a11y.nodes.map(n =>
    "  ".repeat(n.depth) + "#" + n.id + " " + Object.entries(n.attrs).map(([k, v]) => k + "=" + JSON.stringify(v)).join(" ")
    + (n.text !== null ? " text=" + JSON.stringify(n.text) : "") + " @" + n.box)].join("\n");

// The ways a11y differs from its expectation; empty when it matches.
function a11yProblems(a11y, want) {
    if (!a11y || a11y.error) return [a11y?.error ?? "the accessibility DOM could not be read"];
    const problems = [];
    const value = (n, k) => k === "text" ? n.text : n.attrs[k] ?? null;
    for (const node of want.nodes ?? []) {
        if (!a11y.nodes.some(n => Object.entries(node).every(([k, v]) => value(n, k) === v)))
            problems.push("no mirrored element matching " + JSON.stringify(node));
    }
    if (want.activeDescendant) {
        const target = a11y.nodes.find(n => n.attrs["data-mf-automation-id"] === want.activeDescendant);
        if (!target || a11y.activeDescendant !== target.id)
            problems.push(`aria-activedescendant is ${JSON.stringify(a11y.activeDescendant)}, not the element of ${want.activeDescendant} (${target ? "#" + target.id : "not mirrored"})`);
    }
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
    if (r.check === "a11y" && outcome === "READY") problems.push(...a11yProblems(r.a11y, want));
    if (want.snaps !== undefined && r.snaps.length < want.snaps)
        problems.push(`expected ${want.snaps} SNAP line(s), got ${r.snaps.length} -- the page never asked for its screenshot`);
    for (const f of r.snaps.flatMap(s => s.failures)) problems.push("snap: " + f);
    return problems;
}

const results = [];

for (const check of checks) {
    const { result: { targetId } } = await send("Target.createTarget", { url: "about:blank" });
    const { result: { sessionId } } = await send("Target.attachToTarget", { targetId, flatten: true });
    const lines = [];
    let done;
    const finished = new Promise(r => done = r);
    const snaps = [];

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
                if (/MFCHECK (END|READY)/.test(text)) done();
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
    await send("Page.navigate", { url: `${base}index.html?check=${encodeURIComponent(check)}` }, sessionId);

    const timedOut = await Promise.race([finished.then(() => false), new Promise(r => setTimeout(() => r(true), timeoutMs))]);
    let a11y = null;

    if (!timedOut && check === "a11y") {
        // The mirror follows painting, so its first sync can land after READY. Without --expect, wait a
        // fixed time and report what is there; with it, poll until it matches or the time is up.
        const deadline = Date.now() + a11ySettleMs;
        do {
            await new Promise(r => setTimeout(r, expectMode ? 250 : a11ySettleMs));
            const evaluated = await Promise.race([
                send("Runtime.evaluate", { expression: a11yRead, returnByValue: true }, sessionId),
                new Promise(r => setTimeout(() => r(null), 5000)),
            ]);
            a11y = evaluated?.result?.result?.value ?? { error: "(evaluate did not return)" };
        } while (expectMode && Date.now() < deadline && (expected.a11y ? a11yProblems(a11y, expected.a11y).length : 0));
    }

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
    for (const r of snapResults) {
        for (const n of r.notes) console.log("   snap ok   " + n);
        for (const f of r.failures) console.log("   snap FAIL " + f);
    }
    if (a11y) console.log(formatA11y(a11y).split("\n").map(l => "   | " + l).join("\n"));
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
