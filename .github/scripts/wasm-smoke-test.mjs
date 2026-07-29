// Headless-browser smoke test for the published samples/Gallery.Wasm bundle.
//
// `dotnet publish` succeeding (see the CI "wasm" job) only proves wasm-tools could link
// and package the app; it says nothing about whether the resulting bundle actually boots
// in a browser. This script loads the published index.html in headless Chromium and
// checks for the one thing that matters for a smoke test: did Avalonia.Browser attach
// and render, without the wasm runtime throwing during startup.
//
// Usage: node wasm-smoke-test.mjs <url-to-served-index.html>
import { chromium } from 'playwright';

const url = process.argv[2];
if (!url) {
    console.error('Usage: node wasm-smoke-test.mjs <url>');
    process.exit(2);
}

const timeoutMs = 60000;

// The ControlGallery PictureBoxPanel demo intentionally fetches a couple of external
// (google.com) images -- including one with a deliberately-broken URL -- to show how
// PictureBox handles load failures. In a headless/sandboxed browser (and in most real
// browsers, since it's a cross-origin fetch with no CORS headers) those show up as
// console errors. They say nothing about whether the wasm app itself booted, so they're
// filtered out here rather than failing the smoke test on unrelated demo content.
const benignErrorPatterns = [/CORS policy/i, /net::ERR_FAILED/i, /googlelogo/i];

const browser = await chromium.launch();
const page = await browser.newPage();

const unexpectedConsoleErrors = [];
const pageErrors = [];

page.on('console', (msg) => {
    if (msg.type() === 'error') {
        const text = msg.text();
        console.log(`[console.error] ${text}`);
        if (!benignErrorPatterns.some((re) => re.test(text))) {
            unexpectedConsoleErrors.push(text);
        }
        return;
    }
    console.log(`[console.${msg.type()}] ${msg.text()}`);
});
page.on('pageerror', (err) => {
    pageErrors.push(String(err));
    console.log(`[pageerror] ${err}`);
});

console.log(`Navigating to ${url} ...`);
await page.goto(url, { waitUntil: 'domcontentloaded', timeout: timeoutMs });

console.log('Waiting for the <canvas> element Avalonia.Browser renders into...');
try {
    await page.waitForSelector('canvas', { timeout: timeoutMs });
} catch {
    console.error('FAIL: canvas element never appeared within timeout -- the wasm app did not boot.');
    await browser.close();
    process.exit(1);
}

// Give the app a moment past first paint in case boot-adjacent errors land just after
// the canvas appears (e.g. during initial layout).
await page.waitForTimeout(2000);

await browser.close();

if (pageErrors.length > 0) {
    console.error(`FAIL: ${pageErrors.length} uncaught page error(s) during boot.`);
    process.exit(1);
}

if (unexpectedConsoleErrors.length > 0) {
    console.error(`FAIL: ${unexpectedConsoleErrors.length} unexpected console.error message(s) during boot.`);
    process.exit(1);
}

console.log('PASS: canvas rendered and no unexpected console/page errors during boot.');
