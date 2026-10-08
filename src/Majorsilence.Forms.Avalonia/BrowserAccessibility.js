// The accessibility DOM next to the Majorsilence.Forms canvas (browser target only).
//
// Majorsilence.Forms draws every control into one canvas, which a screen reader, browser find-in-page
// or a DOM-based test tool cannot see into. This module keeps a tree of transparent, click-through
// elements over the canvas -- one per control, with its ARIA role, name, state and bounds -- that the
// .NET side (BrowserAccessibility.cs, AriaDomMirror) updates with small batches of operations.
//
// Loaded by BrowserAccessibility.cs through JSHost.ImportAsync from a data: URL built from this file
// (embedded in the assembly), so an app needs no extra static file.

const style = `
.mf-a11y-root { position: absolute; left: 0; top: 0; right: 0; bottom: 0; overflow: hidden; pointer-events: none; z-index: 1; }
.mf-a11y-root, .mf-a11y-root * { color: transparent; background: transparent; pointer-events: none; user-select: none; -webkit-user-select: none; }
.mf-a11y-root [data-mf-type] { position: absolute; box-sizing: border-box; margin: 0; padding: 0; border: 0; overflow: hidden; white-space: pre; font: 12px/1 sans-serif; }
.mf-a11y-text { position: static; }
.mf-a11y-root > [data-mf-type], .mf-a11y-root [data-mf-popup] { overflow: visible; }
.mf-a11y-live { position: absolute; left: 0; top: 0; width: 1px; height: 1px; overflow: hidden; clip-path: inset(50%); white-space: normal; }
`;

// How long a polite announcement waits for the UI to settle (a newer one with the same key replaces
// it), the longest it is held back while changes keep coming, and how long the same text for the same
// key is not repeated.
const politeQuietMs = 250;
const politeMaxWaitMs = 1000;
const repeatWindowMs = 1000;

let root = null;
let host = null;
let live = null;
let polite = null;
let assertive = null;
const elements = new Map();

export function attach(hostId) {
    host = document.getElementById(hostId);
    if (!host) return false;

    if (!document.getElementById("mf-a11y-style")) {
        const s = document.createElement("style");
        s.id = "mf-a11y-style";
        s.textContent = style;
        document.head.appendChild(s);
    }

    // Keyboard input belongs to the app drawn in the canvas, so the host is an application to a reader;
    // aria-activedescendant (setActive) needs a role on the element that carries it.
    if (!host.hasAttribute("role")) host.setAttribute("role", "application");

    root = host.querySelector(":scope > .mf-a11y-root");
    if (!root) {
        root = document.createElement("div");
        root.className = "mf-a11y-root";
        host.appendChild(root);
    }

    // The live regions exist from the start: a reader only announces changes to a region it already
    // knows. They stay the root's last child, after every window element (see upsert's placement).
    live = root.querySelector(":scope > .mf-a11y-live");
    if (!live) {
        live = document.createElement("div");
        live.className = "mf-a11y-live";
        polite = document.createElement("div");
        polite.setAttribute("aria-live", "polite");
        polite.setAttribute("data-mf-live", "polite");
        assertive = document.createElement("div");
        assertive.setAttribute("aria-live", "assertive");
        assertive.setAttribute("data-mf-live", "assertive");
        live.append(polite, assertive);
        root.appendChild(live);
    } else {
        polite = live.querySelector('[data-mf-live="polite"]');
        assertive = live.querySelector('[data-mf-live="assertive"]');
    }

    return true;
}

function forget(el) {
    elements.delete(el.id);
    for (const d of el.querySelectorAll("[id]")) elements.delete(d.id);
}

function textSpan(el, text) {
    let span = el.firstElementChild && el.firstElementChild.classList.contains("mf-a11y-text") ? el.firstElementChild : null;

    if (text == null) {
        if (span) span.remove();
        return;
    }

    if (!span) {
        span = document.createElement("span");
        span.className = "mf-a11y-text";
        el.prepend(span);
    }

    if (span.textContent !== text) span.textContent = text;
}

function upsert(op) {
    let el = elements.get(op.id);

    if (!el) {
        el = document.createElement("div");
        el.id = op.id;
        elements.set(op.id, el);
    }

    if (op.role) el.setAttribute("role", op.role); else el.removeAttribute("role");

    const keep = new Set(["id", "role", "style"]);
    for (const [name, value] of Object.entries(op.attrs)) {
        keep.add(name);
        if (el.getAttribute(name) !== value) el.setAttribute(name, value);
    }
    for (const name of el.getAttributeNames()) {
        if (!keep.has(name)) el.removeAttribute(name);
    }

    el.style.left = op.x + "px";
    el.style.top = op.y + "px";
    el.style.width = op.w + "px";
    el.style.height = op.h + "px";

    textSpan(el, op.text ?? null);

    // Place it among the parent's element children; a leading text span is not one of them.
    const parent = op.parent ? elements.get(op.parent) : root;
    if (!parent) return;

    const offset = parent !== root && parent.firstElementChild && parent.firstElementChild.classList.contains("mf-a11y-text") ? 1 : 0;
    const before = parent.children[op.index + offset] ?? null;

    if (el.parentElement !== parent || (before !== el && el.nextElementSibling !== before))
        parent.insertBefore(el, before === el ? el.nextElementSibling : before);
}

export function apply(json) {
    if (!root) return;

    for (const op of JSON.parse(json)) {
        if (op.remove) {
            const el = elements.get(op.remove);
            if (el) {
                forget(el);
                el.remove();
            }
        } else {
            upsert(op);
        }
    }
}

const recent = new Map();     // key + text -> when it was last said
const pending = new Map();    // polite key -> text, oldest first
let pendingSince = 0;
let politeTimer = 0;
let oneOff = 0;

// Replaces a region's content in a later task: clearing first makes a repeat of the same text a change
// the reader hears again.
function speak(region, texts) {
    if (!region) return;
    region.replaceChildren();
    setTimeout(() => {
        for (const t of texts) {
            const line = document.createElement("div");
            line.textContent = t;
            region.appendChild(line);
        }
    }, 30);
}

function flushPolite() {
    politeTimer = 0;
    pendingSince = 0;
    const texts = [...pending.values()];
    pending.clear();
    if (texts.length) speak(polite, texts);
}

export function announce(text, isAssertive, key) {
    if (!root || !text) return;

    const now = performance.now();
    const k = key ?? ("once-" + (++oneOff));
    const said = k + "\u0000" + text;
    const last = recent.get(said);

    if (last !== undefined && now - last < repeatWindowMs) return;
    recent.set(said, now);
    if (recent.size > 200) {
        for (const [s, t] of recent) if (now - t >= repeatWindowMs) recent.delete(s);
    }

    if (isAssertive) {
        pending.delete(k);
        speak(assertive, [text]);
        return;
    }

    // Debounced: a value stepping on every key repeat is said once, when it settles.
    pending.delete(k);
    pending.set(k, text);
    if (!pendingSince) pendingSince = now;
    clearTimeout(politeTimer);
    politeTimer = setTimeout(flushPolite, now - pendingSince >= politeMaxWaitMs ? 0 : politeQuietMs);
}

export function setActive(id) {
    if (!host) return;

    // The host <div> is what Avalonia keeps focused (it has tabindex=0), so this is what tells a reader
    // which mirrored element has focus inside the canvas.
    if (id) host.setAttribute("aria-activedescendant", id);
    else host.removeAttribute("aria-activedescendant");
}
