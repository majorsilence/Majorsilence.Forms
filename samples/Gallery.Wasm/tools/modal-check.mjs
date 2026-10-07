// Runs the Gallery.Wasm browser-head checks (ModalCheckForm.cs, issue #406) in headless Chrome and
// prints what each blocking or awaited modal call did: RETURNED, THREW, or HUNG.
//
//   dotnet publish samples/Gallery.Wasm -c Release -o out
//   node samples/Gallery.Wasm/tools/modal-check.mjs out/wwwroot [check ...]
//
// Node 22+ (built-in WebSocket) and a local Chrome/Chromium; set CHROME to its path if it is not in
// the usual place. Nothing is installed and nothing leaves the machine: the bundle is served from
// 127.0.0.1 and Chrome is driven over its DevTools protocol.
//
// Each check gets its own page load, because a call that blocks the page's only thread stops
// everything after it; such a call shows up here as a START with no RETURNED/THREW, reported as HUNG.

import { spawn } from "node:child_process";
import { createServer } from "node:http";
import { existsSync, mkdtempSync, readFileSync, rmSync, statSync, writeFileSync } from "node:fs";
import { tmpdir } from "node:os";
import { extname, join, normalize, resolve, sep } from "node:path";

const root = resolve(process.argv[2] ?? "out/wwwroot");
const checks = process.argv.length > 3 ? process.argv.slice(3) : [
    "showdialog", "messagebox", "commondialog", "taskdialog",
    "showdialogasync", "messageboxasync", "taskdialogasync", "a11y",
];
const timeoutMs = Number(process.env.MF_CHECK_TIMEOUT_MS ?? 20000);

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
const browser = spawn(chrome, [
    "--headless=new", "--remote-debugging-port=0", `--user-data-dir=${profile}`,
    "--no-first-run", "--no-default-browser-check", "about:blank",
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

// Reads the accessibility DOM next to the canvas: one line per mirrored element.
const a11yDump = `(() => {
    const root = document.querySelector('.mf-a11y-root');
    if (!root) return 'NO .mf-a11y-root ELEMENT';
    const lines = [];
    const walk = (el, depth) => {
        for (const c of el.children) {
            if (c.classList.contains('mf-a11y-text')) continue;
            const attrs = ['role', 'aria-label', 'aria-checked', 'aria-disabled', 'aria-modal', 'data-mf-automation-id']
                .filter(a => c.hasAttribute(a)).map(a => a + '=' + JSON.stringify(c.getAttribute(a))).join(' ');
            const text = c.querySelector(':scope > .mf-a11y-text');
            lines.push('  '.repeat(depth) + '#' + c.id + ' ' + attrs + (text ? ' text=' + JSON.stringify(text.textContent) : '')
                + ' @' + c.style.left + ',' + c.style.top + ' ' + c.style.width + ' x ' + c.style.height);
            walk(c, depth + 1);
        }
    };
    walk(root, 0);
    const host = root.closest('.avalonia-container');
    return 'active-descendant=' + (host && host.getAttribute('aria-activedescendant')) + '\\n' + lines.join('\\n');
})()`;

const results = [];

for (const check of checks) {
    const { result: { targetId } } = await send("Target.createTarget", { url: "about:blank" });
    const { result: { sessionId } } = await send("Target.attachToTarget", { targetId, flatten: true });
    const lines = [];
    let done;
    const finished = new Promise(r => done = r);

    const onMessage = msg => {
        if (msg.sessionId !== sessionId) return;
        if (msg.method === "Runtime.consoleAPICalled") {
            const text = msg.params.args.map(a => a.value ?? a.description ?? "").join(" ");
            if (process.env.MF_CHECK_VERBOSE) console.log("   [console] " + text.trim());
            if (text.includes("MFCHECK")) {
                lines.push(text.trim());
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
    let a11y = "";

    if (!timedOut && check === "a11y") {
        // Let the mirror's first sync land after the controls were added.
        await new Promise(r => setTimeout(r, Number(process.env.MF_A11Y_SETTLE_MS ?? 1500)));
        const evaluated = await Promise.race([
            send("Runtime.evaluate", { expression: a11yDump, returnByValue: true }, sessionId),
            new Promise(r => setTimeout(() => r(null), 5000)),
        ]);
        a11y = evaluated?.result?.result?.value ?? "(evaluate did not return)";
    }

    // MF_CHECK_SCREENSHOTS=<dir> saves what the page showed at the end of each check.
    if (process.env.MF_CHECK_SCREENSHOTS) {
        const shot = await Promise.race([
            send("Page.captureScreenshot", { format: "png" }, sessionId),
            new Promise(r => setTimeout(() => r(null), 5000)),
        ]);
        if (shot?.result?.data) writeFileSync(join(process.env.MF_CHECK_SCREENSHOTS, `${check}.png`), Buffer.from(shot.result.data, "base64"));
    }

    listeners.delete(onMessage);

    const started = lines.some(l => l.includes("MFCHECK START"));
    const outcome = lines.find(l => /MFCHECK (RETURNED|THREW)/.test(l));
    const verdict = outcome ? outcome.replace(/^.*MFCHECK /, "")
        : started && timedOut ? `HUNG (no return within ${timeoutMs} ms)`
        : check === "a11y" && !timedOut ? "READY"
        : timedOut ? `NO RESULT (timed out after ${timeoutMs} ms; the page may not have booted)`
        : "NO RESULT";

    results.push({ check, verdict, lines, a11y });
    console.log(`\n== ${check}: ${verdict}`);
    for (const l of lines) console.log("   " + l);
    if (a11y) console.log(a11y.split("\n").map(l => "   | " + l).join("\n"));

    // Closing a target whose thread is blocked still works: the browser process owns it.
    await send("Target.closeTarget", { targetId });
}

ws.close();
browser.kill();
server.close();
try { rmSync(profile, { recursive: true, force: true }); } catch { }

console.log("\nSummary");
for (const r of results) console.log(`  ${r.check.padEnd(16)} ${r.verdict}`);
