# Accessibility & UI automation

Majorsilence.Forms exposes a backend-neutral **automation tree** — a snapshot of the live control
hierarchy with ids, names, roles, values, state, and bounds. It is the single model behind three
consumers:

1. **In-process UI tests** — `Majorsilence.Forms.Automation` (in the core package).
2. **Remote automation** — `Majorsilence.Forms.WebDriver`, a W3C WebDriver server that any
   Selenium client can drive.
3. **OS screen readers** — *(planned)* per-platform bridges (UI Automation / AT-SPI / NSAccessibility).

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

`By.Id` / `By.Name` / `By.Role` / `By.Type` / `By.Text` locate elements; `Find`, `FindOrThrow`,
and `FindAll` query a fresh snapshot each call. Actions (`Click`, `SendKeys`, `PressKey`, `Clear`)
are delivered through the same neutral input pipeline a real backend uses, so they exercise the real
routing/focus/layout — no pixel-coordinate math required.

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
(role), get rect, get enabled, screenshot (PNG via the offscreen renderer), and `GET /status`. Locator
strategies: `id`, `name`, `tag name` (role), `css selector` (`#id` and `[name='…']` forms), plus the
custom `role`, `type`, and `link text`. Element references re-resolve against a fresh snapshot on every
use (preferring the stable AutomationId), so values stay live after edits.

Element actions are marshalled onto the UI thread, so the server can run on its own thread while your
app's UI thread pumps its message loop as usual.

### Threading note for tests

In a headless test there is no running message loop, so pump the queue while your WebDriver/HTTP calls
run on a worker thread (see `WebDriverServerTests`):

```csharp
var task = Task.Run (RunWebDriverFlow);
while (!task.IsCompleted) { Platform.Backend.DoEvents (); Thread.Sleep (5); }
```

## What about Playwright?

**Playwright is not a fit.** It automates *browser engines* over the Chrome DevTools Protocol against a
DOM. Majorsilence.Forms renders natively with Skia and has no DOM or browser engine for Playwright to
attach to. The only way it would apply is hosting the UI inside a real web view, which this framework
does not do.

The same HTTP automation surface *could* be exercised from Playwright's API-request client (treating it
as an HTTP service), but that is not browser automation and offers nothing over a plain HTTP/WebDriver
client. For desktop UI automation, use the WebDriver server (Selenium) or the in-process API above.

## Roadmap

- Bridge the automation tree to **OS accessibility** (UI Automation on Windows first) so screen readers
  announce controls and so existing UIA tools (FlaUI, Appium/WinAppDriver) drive the app with no custom
  protocol code.
- Expand roles/states (selection, expand/collapse, value ranges) and surface non-control items such as
  individual tabs and list items.
- A higher-level `Majorsilence.Forms.Testing` ergonomics layer (fluent helpers, golden-image asserts).
