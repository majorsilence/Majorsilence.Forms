# Accessibility & UI automation

Majorsilence.Forms exposes a backend-neutral **automation tree** — a snapshot of the live control
hierarchy with ids, names, roles, values, state, and bounds. It is the single model behind three
consumers:

1. **In-process UI tests** — `Majorsilence.Forms.Automation` (in the core package).
2. **Remote automation** — `Majorsilence.Forms.WebDriver`, a W3C WebDriver server that any
   Selenium client can drive.
3. **OS screen readers & magnifiers** — `Majorsilence.Forms.WindowsUIAutomation` bridges the tree to
   **Windows UI Automation** (Narrator/NVDA/JAWS + focus-following magnifiers). AT-SPI (Linux) and
   NSAccessibility (macOS) bridges are still *planned*.

The tree reads the same logical bounds/state the renderers use, so it behaves identically on the
headless and real (Avalonia/Uno) backends.

## Making controls findable

Locators key off two control properties you already set:

- `Control.Name` → the element's **AutomationId** (the stable locator — prefer this).
- `Control.AccessibleName` (falls back to `Text`, then `Name`) → the element's **Name**.

```csharp
var okButton = new Button { Name = "okButton", Text = "OK" };
var nameBox  = new TextBox { Name = "nameBox", AccessibleName = "Full name" };
```

Roles are inferred from the control type (`button`, `textbox`, `checkbox`, `radio`, `combobox`,
`list`, `label`, `tablist`, `window`, …) unless you set `Control.AccessibleRole` explicitly.

## In-process automation (C# UI tests)

```csharp
using Majorsilence.Forms.Automation;

var session = new AutomationSession (form);

session.Click   (session.FindOrThrow (By.Id ("okButton")));
session.SendKeys(session.FindOrThrow (By.Id ("nameBox")), "Ada Lovelace");

var value = session.GetText (session.FindOrThrow (By.Id ("nameBox")));   // "Ada Lovelace"
```

`By.Id` / `By.Name` / `By.Role` / `By.Type` / `By.Text` / `By.XPath` locate elements; `Find`,
`FindOrThrow`, and `FindAll` query a fresh snapshot each call. Actions (`Click`, `SendKeys`,
`PressKey`, `Clear`) are delivered through the same neutral input pipeline a real backend uses, so they
exercise the real routing/focus/layout — no pixel-coordinate math required.

`By.XPath` evaluates against the tree's XML rendering — the same shape `session.GetPageSource ()`
returns — where each control is an element tagged by its control type with `id`, `name`, `role`,
`type`, `value`, `enabled`, `visible`, and bounds (`x`/`y`/`width`/`height`) attributes:

```csharp
session.Find (By.XPath ("//Button[@id='okButton']"));
session.Find (By.XPath ("//TextBox[@name='Full name']"));
session.FindAll (By.XPath ("//Panel//Button"));          // descendant axis, position, etc.
```

## Remote automation with Selenium (WebDriver server)

`Majorsilence.Forms.WebDriver` hosts a minimal **W3C WebDriver** endpoint over HTTP. Because WebDriver
is just HTTP+JSON, any WebDriver client in any language can drive the app.

```csharp
using Majorsilence.Forms.WebDriver;

var server = new WebDriverServer (form, port: 4444);
server.Start ();          // listens on http://127.0.0.1:4444/
// ... run your Selenium / WebDriver client against server.Url ...
server.Stop ();
```

From Selenium (C#), point a `RemoteWebDriver` at the server and use the normal API:

```csharp
var driver = new RemoteWebDriver (server.Url, new DriverOptions ());   // sketch
driver.FindElement (By.CssSelector ("#okButton")).Click ();
driver.FindElement (By.Name ("nameBox")).SendKeys ("Ada Lovelace");
```

Supported commands: new/delete session, find element(s), click, send keys, clear, get text, get name
(role), get attribute, get rect, get enabled, **page source** (`GET …/source`, XML), screenshot (PNG
via the offscreen renderer), and `GET /status`. Locator strategies: `id`, `name`, `tag name` (role),
`xpath`, `css selector` (`#id` and `[name='…']` forms), plus the custom `role`, `type`, and
`link text`. Element references re-resolve against a fresh snapshot on every use (preferring the stable
AutomationId), so values stay live after edits.

## Inspecting & recording with a WebDriver inspector

Because the server exposes XML **page source** (`GET …/source`) and an **xpath** locator strategy that
runs against that exact source, any Appium-style inspector can render the live element tree, overlay it
on a screenshot, and let you capture locators by clicking nodes. This is the recommended recording path
— Selenium IDE itself records DOM events inside a browser and has no way to attach to a native app.

The loop an inspector uses is just three commands the server implements:

| Step | Command | Returns |
| --- | --- | --- |
| Snapshot the tree | `GET /session/{id}/source` | XML (tag = control type; attributes below) |
| Show the UI | `GET /session/{id}/screenshot` | base64 PNG |
| Confirm a locator | `POST /session/{id}/element` + `…/attribute/{name}` | the element / its attributes |

Every node in the source carries the attributes you build locators from:

```xml
<Form name="Login" role="window" type="Form" x="0" y="0" width="400" height="300">
  <Button id="okButton" name="OK" role="button" type="Button"
          value="" enabled="true" visible="true" x="10" y="10" width="100" height="30" />
  <TextBox id="nameBox" name="Full name" role="textbox" type="TextBox"
           value="" enabled="true" visible="true" x="10" y="50" width="200" height="30" />
</Form>
```

### 1. Start the server with the window shown

The inspector needs a live window to snapshot. In a normal app the UI thread already pumps its message
loop, so just start the server before/after the window is shown:

```csharp
using Majorsilence.Forms.WebDriver;

var server = new WebDriverServer (form, port: 4444);
server.Start ();                 // http://127.0.0.1:4444/  (loopback only)
// run the inspector against server.Url … then:
server.Stop ();
```

(For headless inspection with no message loop, pump the queue on another thread — see the threading
note below.)

### 2. Connect the inspector

Point the inspector at the server as a **remote WebDriver host**. In Appium Inspector, choose
*Attach to Session* / *Remote* and use:

| Setting | Value |
| --- | --- |
| Remote Host | `127.0.0.1` |
| Remote Port | the `port` you passed (e.g. `4444`) |
| Remote Path | `/` |
| Protocol / SSL | `http`, no SSL |
| Capabilities | any JSON object — the server ignores capability matching and always grants a session |

The inspector then polls `source` + `screenshot` and draws the clickable tree. Selecting a node shows
its attributes; that's where you read the `id` (best) or compose an XPath.

### 3. Capture locators that replay

Prefer, in order:

1. **`id`** — `By.Id("okButton")` / `using: "id"`. Maps to `Control.Name`; the most stable locator, and
   the one element references re-resolve against first.
2. **`xpath`** — `//Button[@id='okButton']`, `//TextBox[@name='Full name']`, `//Panel//Button`,
   `(//Button)[2]`. Evaluated against the exact XML the inspector showed you, so what you capture is
   what replays. Element-selecting expressions only (positions, attribute predicates, and the
   descendant axis all work).
3. **`name` / `role` / `type`** — coarser fallbacks when there's no stable id.

`GET …/element/{id}/attribute/{name}` exposes the same fields as the source (`id`, `name`, `role`,
`type`, `value`, `enabled`, `visible`, `x`/`y`/`width`/`height`), so an attribute-based locator a
recorder captures resolves identically on playback.

### Generic WebDriver client (most reliable)

Any W3C WebDriver client can act as your inspector/recorder — no Appium needed. A few lines of Selenium
print the tree and verify a locator:

```python
from selenium import webdriver
from selenium.webdriver.common.by import By

driver = webdriver.Remote("http://127.0.0.1:4444", options=webdriver.ChromeOptions())
print(driver.page_source)                               # the XML tree to pick locators from
el = driver.find_element(By.XPATH, "//Button[@id='okButton']")
print(el.get_attribute("role"))                         # -> "button"
el.click()
driver.quit()
```

Or drive it by hand with `curl` to confirm the endpoints:

```bash
SID=$(curl -s -XPOST 127.0.0.1:4444/session -d '{}' | jq -r .value.sessionId)
curl -s 127.0.0.1:4444/session/$SID/source                     # XML page source
curl -s -XPOST 127.0.0.1:4444/session/$SID/element \
     -d '{"using":"xpath","value":"//Button[@id=\"okButton\"]"}'
```

### Caveats

- **Appium-specific commands.** This is a W3C WebDriver server, not a full Appium server. The
  status / session / source / screenshot / findElement / getAttribute subset an inspector needs all
  work, but Appium-only endpoints (settings, gestures, app management) return `404` — Appium Inspector
  tolerates the ones it treats as optional. A generic WebDriver client (above) is the most reliable
  path if the inspector trips on a missing Appium endpoint.
- **Overlay alignment.** Bounds are logical client coordinates; if the screenshot is captured at a
  different DPI/scale the inspector's bounding-box overlay may be offset even though the locators are
  correct.
- **Single window.** One session/window at a time; no frame or window switching.
- **Visible controls only.** Hidden controls are omitted from the tree (and so from the source).

Element actions are marshalled onto the UI thread, so the server can run on its own thread while your
app's UI thread pumps its message loop as usual.

### Threading note for tests

In a headless test there is no running message loop, so pump the queue while your WebDriver/HTTP calls
run on a worker thread (see `WebDriverServerTests`):

```csharp
var task = Task.Run (RunWebDriverFlow);
while (!task.IsCompleted) { Platform.Backend.DoEvents (); Thread.Sleep (5); }
```

## Screen readers & magnifiers (Windows)

`Majorsilence.Forms.WindowsUIAutomation` projects the automation tree onto **Windows UI Automation** so
screen readers (Narrator, NVDA, JAWS) can navigate, read, and activate controls, and focus-following
magnifiers track the caret. It's backend-neutral (works for any Windows host that supplies a native
window handle) and Windows-only — off Windows the package is an empty stub.

```csharp
using Majorsilence.Forms.WindowsUIAutomation;

form.Show ();                       // the window must be shown first (it needs a native handle)
WindowsUIAutomation.Enable (form);  // detaches automatically when the window closes
```

How it maps:

- Each control becomes a UIA element: **Name** (accessible name), **AutomationId** (`Control.Name`),
  **ControlType** (button/edit/checkbox/…), **IsEnabled**, **HasKeyboardFocus**, and a screen
  **BoundingRectangle**.
- **Patterns:** `Invoke` (buttons) is live now; `Value` (text/combo) and `Toggle` (checkbox) are
  exposed for reading and Phase 2 completes write support.
- **Events:** when keyboard focus moves, a UIA focus-changed event fires (this is what makes a screen
  reader announce the new control and a magnifier follow it); the focused control's value changes raise
  a property-changed event.

What's not in this first cut: per-keystroke value events for `TextBox` (the control doesn't yet raise
`TextChanged`, so screen readers fall back to their own typed-character echo — the field's value is
still announced on focus); structure-changed events; and sub-control items (individual tabs/list rows).
These are Phase 2.

### Verifying the native bridge

The pure tree/role logic is unit-tested (`Majorsilence.Forms.WindowsUIAutomation.Tests`, Windows CI).
The full COM round-trip needs an interactive Windows desktop session: run a sample app, `Enable` the
bridge, then inspect with **accessibility insights / inspect.exe** or turn on **Narrator** and tab
through the window — each control should be announced with its name and role, and a button should
activate from the screen reader.

## What about Playwright?

**Playwright is not a fit.** It automates *browser engines* over the Chrome DevTools Protocol against a
DOM. Majorsilence.Forms renders natively with Skia and has no DOM or browser engine for Playwright to
attach to. The only way it would apply is hosting the UI inside a real web view, which this framework
does not do.

The same HTTP automation surface *could* be exercised from Playwright's API-request client (treating it
as an HTTP service), but that is not browser automation and offers nothing over a plain HTTP/WebDriver
client. For desktop UI automation, use the WebDriver server (Selenium) or the in-process API above.

## Roadmap

- ✅ **Windows UI Automation bridge** (`Majorsilence.Forms.WindowsUIAutomation`) — screen readers and
  magnifiers, and existing UIA tools (FlaUI, Appium/WinAppDriver) drive the app with no custom protocol.
- Complete the UIA patterns: `Value`/`Toggle` write support, structure-changed events, and `TextBox`
  per-keystroke value events (raise `TextChanged` from the editor).
- **AT-SPI (Linux)** and **NSAccessibility (macOS)** bridges over the same tree.
- Expand roles/states (selection, expand/collapse, value ranges) and surface non-control items such as
  individual tabs and list items.
- A higher-level `Majorsilence.Forms.Testing` ergonomics layer (fluent helpers, golden-image asserts).
