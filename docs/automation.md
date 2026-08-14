# Automation & UI testing

Majorsilence.Forms exposes a backend-neutral **automation tree**: a snapshot of the live control
hierarchy with ids, names, roles, values, state, and bounds. This page is both the reference for that
tree and the practitioner's guide to testing **your** application on top of it — page objects, waits,
the industry tools, visual regression, CI recipes, and how AI agents plug into the same surface.

[Module 8](https://forms.majorsilence.com/training/#module-8) of the training guide is the short version.

Everything here was run against `main` on macOS while writing it, including
a real Selenium `RemoteWebDriver` session. Where something doesn't work, it says so.

---

## Contents

- [One tree, three consumers](#one-tree-three-consumers)
- [Pick your level](#pick-your-level)
- [Four prerequisites](#four-prerequisites)
- [Level 1 — in-process tests on the headless backend](#level-1--in-process-tests-on-the-headless-backend)
- [Waiting, without `Thread.Sleep`](#waiting-without-threadsleep)
- [Page objects](#page-objects)
- [Level 2 — Selenium and the WebDriver server](#level-2--selenium-and-the-webdriver-server)
- [Recording locators with an inspector](#recording-locators-with-an-inspector)
- [Level 3 — Windows-native tools (FlaUI, WinAppDriver, Appium)](#level-3--windows-native-tools-flaui-winappdriver-appium)
- [Visual regression with golden images](#visual-regression-with-golden-images)
- [BDD: Reqnroll / SpecFlow on top](#bdd-reqnroll--specflow-on-top)
- [CI recipes](#ci-recipes)
- [How AI tools hook into all of this](#how-ai-tools-hook-into-all-of-this)
- [Limits and anti-patterns](#limits-and-anti-patterns)
- [Roadmap](#roadmap)

---

## One tree, three consumers

The tree reads the same logical bounds and state the renderers use, so it behaves identically on the
headless and the real (Avalonia/Uno) backends — **a test written against Headless describes what a user
sees on Avalonia.** Three things consume that one model:

| Consumer | Package | What it gives you |
|---|---|---|
| In-process UI tests | `Majorsilence.Forms.Automation` (in the core package) | Drive a form from C#/VB without pixel math |
| Remote automation | `Majorsilence.Forms.WebDriver` | A W3C WebDriver server any Selenium client can drive |
| Screen readers & magnifiers | `Majorsilence.Forms.WindowsUIAutomation` | Narrator / NVDA / JAWS on Windows |

Every one of them sees the same thing, and that thing is **text**. `session.GetPageSource()` renders the
live UI as XML — which is what makes locators recordable, snapshots diffable, and
[AI agents useful](#how-ai-tools-hook-into-all-of-this) without a single pixel:

```xml
<Form name="Login" role="window" type="Form" x="0" y="0" width="400" height="300">
  <Button id="okButton" name="OK" role="button" type="Button"
          value="" enabled="true" visible="true" x="10" y="10" width="100" height="30" />
  <TextBox id="nameBox" name="Full name" role="textbox" type="TextBox"
           value="" enabled="true" visible="true" x="10" y="50" width="200" height="30" />
</Form>
```

The tag is the control type; `id` is `Control.Name`, `name` is the accessible name. Those attributes are
exactly what every locator below matches on.

---

## Pick your level

Three ways in, and they consume the *same* automation tree — so a locator you write at one level is
valid at the others.

| Level | What drives the app | Runs without a display | Use it for |
|---|---|---|---|
| **1. In-process** — `AutomationSession` | Your test code, in the same process | **Yes** (Headless backend) | The bulk of your suite. Fast, debuggable, no ports, no drivers. |
| **2. Remote** — `WebDriverServer` + Selenium | Any W3C WebDriver client, over HTTP | Yes | Reusing an existing Selenium suite, non-.NET test languages, recording locators with an inspector. |
| **3. Windows-native** — UIA bridge | FlaUI, WinAppDriver, Appium, Accessibility Insights | No (needs a Windows desktop session) | Verifying real screen-reader behaviour, and driving the app with the tooling your Windows QA team already owns. |

**Default to level 1** for coverage and level 3 for accessibility verification. Reach for level 2 when
something outside .NET has to drive the app.

---

## Four prerequisites

Get these wrong and every level misbehaves in confusing ways.

### 1. Name every interactive control

Locators key off two properties. `Control.Name` becomes the element's **AutomationId** — the stable
locator. `Control.AccessibleName` (falling back to `Text`, then `Name`) becomes its **Name**.

**C#**

```csharp
var okButton = new Button  { Name = "okButton", Text = "OK" };
var nameBox  = new TextBox { Name = "nameBox",  AccessibleName = "Full name" };
```

**VB.NET**

```vb
Dim okButton As New Button With {.Name = "okButton", .Text = "OK"}
Dim nameBox As New TextBox With {.Name = "nameBox", .AccessibleName = "Full name"}
```

Make it a review rule. The same keystroke buys a test locator *and* screen-reader support, and
retrofitting names across an existing app is the single most tedious part of adopting UI tests.

### 2. Force a layout pass before you automate

Bounds and hit-testing don't exist until the form has laid out. On the headless backend nothing lays out
until something asks for a frame, so the first thing a test does is render once:

**C#**

```csharp
using var form = new GreetForm ();
HeadlessRenderer.CapturePng (form, 360, 140);   // forces layout; discard the bytes
```

**VB.NET**

```vb
Using form As New GreetForm()
    HeadlessRenderer.CapturePng(form, 360, 140)   ' forces layout; discard the bytes
End Using
```

Skip it and `Click` lands on nothing, because every control's `Bounds` is still empty. You do **not**
need to call `Show()` — an unshown form is fully automatable in-process.

### 3. Run UI tests serially

The active backend (`Platform.Backend`) and `Application.OpenForms` are process-global. Tests that share
them cannot run in parallel — a modal dialog in one test picks its owner from the global open-forms list
and can wait forever on another test's window.

**C# (xUnit)**

```csharp
using Xunit;

// The backend and Application.OpenForms are global process state.
[assembly: CollectionBehavior (DisableTestParallelization = true)]
```

NUnit: `[assembly: LevelOfParallelism(1)]` and no `[Parallelizable]`. MSTest: leave
`<Parallelize>` out of your `.runsettings`, or set `Workers` to `1`.

### 4. Install the Headless backend once per assembly

**C# — a module initializer is the tidiest hook**

```csharp
using System.Runtime.CompilerServices;
using Majorsilence.Forms.Backends;
using Majorsilence.Forms.Headless;

internal static class TestBackend
{
    // Runs before any test in the assembly. The Headless backend has no UI-thread
    // dispatcher affinity, which is what makes it safe under a test runner's worker threads.
    [ModuleInitializer]
    internal static void Init () => Platform.Backend = new HeadlessPlatformBackend ();
}
```

**VB.NET — VB has no module initializer**

```vb
Imports Majorsilence.Forms.Backends
Imports Majorsilence.Forms.Headless
Imports Microsoft.VisualStudio.TestTools.UnitTesting

<TestClass>
Public Class TestBackend
    ' VB cannot use <ModuleInitializer> — the VB compiler does not emit module
    ' initializers, so the attribute alone would silently do nothing and every test
    ' would run against no backend. Use the framework's assembly-level hook instead:
    ' MSTest <AssemblyInitialize>, NUnit <SetUpFixture> + <OneTimeSetUp>, or an
    ' xUnit assembly fixture.
    <AssemblyInitialize>
    Public Shared Sub Init(context As TestContext)
        Platform.Backend = New HeadlessPlatformBackend()
    End Sub
End Class
```

`HeadlessRenderer.Use()` does the same thing if you prefer it.

> **Don't run UI tests on the Avalonia backend.** Avalonia's dispatcher is thread-bound and conflicts
> with a test runner's worker threads. The Headless backend exists precisely so your suite doesn't need
> a display *or* a UI thread — that's why the framework's own suite runs on it.

---

## Level 1 — in-process tests on the headless backend

The API is small enough to learn in one sitting.

| Call | Does |
|---|---|
| `new AutomationSession (form)` | Wraps a form (or any `WindowBase`) |
| `session.Find (by)` / `FindOrThrow (by)` / `FindAll (by)` | Query a **fresh** snapshot each time |
| `By.Id` / `By.Name` / `By.Role` / `By.Type` / `By.Text` / `By.XPath` | Locators |
| `session.Click (element)` | Press, through the real input pipeline |
| `session.SendKeys (element, text)` | Focus + type |
| `session.PressKey (Keys.Enter)` | A bare key, to the focused control |
| `session.Clear (element)` | Empty an editable control |
| `session.GetText (element)` | Read the value/text |
| `session.Root` / `session.GetPageSource ()` | The whole tree as objects / as XML |

Elements expose `AutomationId`, `Name`, `Role`, `ControlType`, `Value`, `Enabled`, `Visible`, `Focused`,
`Bounds`, `Children`, `ClickPoint`, and `Descendants()`.

> **An `AutomationElement` is an immutable snapshot — re-resolve before you read.** This is the one
> gotcha everybody hits, and it's asymmetric: *actions* on a previously-captured element work fine
> (`Click`/`SendKeys` route to the live control), but *reads* return the state as of capture time.
>
> ```csharp
> var box = session.FindOrThrow (By.Id ("nameBox"));
> session.SendKeys (box, "Ada");          // works — the action reaches the live control
> session.GetText (box);                  // "" — this snapshot predates the typing
> session.GetText (session.FindOrThrow (By.Id ("nameBox")));   // "Ada" — fresh snapshot
> ```
>
> So: capture for actions if you like, but always re-`Find` for `GetText`, `Enabled`, `Visible`, `Value`
> and `Bounds`. The [page-object pattern below](#page-objects) makes this automatic by exposing
> locators as *properties*.

A complete test:

**C#**

```csharp
using Majorsilence.Forms;
using Majorsilence.Forms.Automation;
using Majorsilence.Forms.Headless;
using Xunit;

public class GreetFormTests
{
    [Fact]
    public void Entering_a_name_and_pressing_OK_accepts_the_dialog ()
    {
        using var form = new GreetForm ();
        HeadlessRenderer.CapturePng (form, 360, 140);        // layout pass

        var session = new AutomationSession (form);

        session.SendKeys (session.FindOrThrow (By.Id ("nameBox")), "Ada Lovelace");
        Assert.Equal ("Ada Lovelace", session.GetText (session.FindOrThrow (By.Id ("nameBox"))));

        session.Click (session.FindOrThrow (By.Id ("okButton")));

        Assert.Equal (DialogResult.OK, form.DialogResult);
    }

    [Fact]
    public void OK_is_disabled_until_a_name_is_entered ()
    {
        using var form = new GreetForm ();
        HeadlessRenderer.CapturePng (form, 360, 140);

        var session = new AutomationSession (form);

        // Assert on state read from the tree, not on your own field references.
        Assert.False (session.FindOrThrow (By.Id ("okButton")).Enabled);
    }
}
```

**VB.NET**

```vb
Imports Majorsilence.Forms
Imports Majorsilence.Forms.Automation
Imports Majorsilence.Forms.Headless
Imports Microsoft.VisualStudio.TestTools.UnitTesting

<TestClass>
Public Class GreetFormTests

    <TestMethod>
    Public Sub Entering_a_name_and_pressing_OK_accepts_the_dialog()
        Using form As New GreetForm()
            HeadlessRenderer.CapturePng(form, 360, 140)      ' layout pass

            Dim session As New AutomationSession(form)

            session.SendKeys(session.FindOrThrow(By.Id("nameBox")), "Ada Lovelace")
            Assert.AreEqual("Ada Lovelace",
                            session.GetText(session.FindOrThrow(By.Id("nameBox"))))

            session.Click(session.FindOrThrow(By.Id("okButton")))

            Assert.AreEqual(DialogResult.OK, form.DialogResult)
        End Using
    End Sub
End Class
```

`Click` and `SendKeys` go through the same neutral input path a real backend uses, so they exercise real
routing, focus and layout — not a test-only shortcut. That's what makes a headless assertion meaningful.

### XPath, when there's no stable id

`By.XPath` runs against the same XML `GetPageSource()` returns, so what you see is what you can match:

**C#**

```csharp
session.Find    (By.XPath ("//Button[@id='okButton']"));
session.Find    (By.XPath ("//TextBox[@name='Full name']"));
session.FindAll (By.XPath ("//Panel//Button"));
session.Find    (By.XPath ("(//Button)[2]"));
```

**VB.NET**

```vb
session.Find(By.XPath("//Button[@id='okButton']"))
session.Find(By.XPath("//TextBox[@name='Full name']"))
session.FindAll(By.XPath("//Panel//Button"))
session.Find(By.XPath("(//Button)[2]"))
```

Element-selecting expressions only — positions, attribute predicates and the descendant axis all work.

### Lower-level input, when you need a gesture

`AutomationSession` covers click and type. For drags, wheel scrolling, or a press held across a move, go
one level down to `HeadlessRenderer`, which injects at window coordinates:

**C#**

```csharp
HeadlessRenderer.MouseDown (form, 40, 60);
HeadlessRenderer.MouseMove (form, 120, 60, MouseButtons.Left);
HeadlessRenderer.MouseUp   (form, 120, 60);

HeadlessRenderer.KeyDown   (form, Keys.Control | Keys.A);
HeadlessRenderer.TextInput (form, "typed text");
```

**VB.NET**

```vb
HeadlessRenderer.MouseDown(form, 40, 60)
HeadlessRenderer.MouseMove(form, 120, 60, MouseButtons.Left)
HeadlessRenderer.MouseUp(form, 120, 60)

HeadlessRenderer.KeyDown(form, Keys.Control Or Keys.A)
HeadlessRenderer.TextInput(form, "typed text")
```

Coordinates here are **logical**, and the renderer converts them to device pixels for you — which is why
these keep working when you run the same test at `MF_HEADLESS_SCALE=2`.

---

## Waiting, without `Thread.Sleep`

There is **no built-in implicit wait**, and that's the right default for in-process tests: nothing is
asynchronous unless your app made it so. But the moment your code does work on a background thread and
marshals back with `Application.RunOnUIThread`, you need to pump and poll rather than sleep.

Write this once and use it everywhere:

**C#**

```csharp
using System.Diagnostics;
using Majorsilence.Forms.Backends;
using Majorsilence.Forms.Automation;

public static class Wait
{
    public static AutomationElement For (AutomationSession session, By by, int timeoutMs = 2000)
        => Until (() => session.Find (by), timeoutMs, $"no element matched {by.Description}");

    public static T Until<T> (Func<T?> probe, int timeoutMs, string message) where T : class
    {
        var clock = Stopwatch.StartNew ();

        do {
            var hit = probe ();
            if (hit is not null)
                return hit;

            // Let queued work (timers, RunOnUIThread callbacks) run before probing again.
            Platform.Backend.DoEvents ();
            Thread.Sleep (10);
        } while (clock.ElapsedMilliseconds < timeoutMs);

        throw new TimeoutException ($"Timed out after {timeoutMs}ms: {message}");
    }
}
```

**VB.NET**

```vb
Imports System.Diagnostics
Imports Majorsilence.Forms.Automation
Imports Majorsilence.Forms.Backends

Public Module Wait

    Public Function ForElement(session As AutomationSession, by As By,
                               Optional timeoutMs As Integer = 2000) As AutomationElement
        Return Until(Function() session.Find(by), timeoutMs,
                     $"no element matched {by.Description}")
    End Function

    Public Function Until(Of T As Class)(probe As Func(Of T), timeoutMs As Integer,
                                        message As String) As T
        Dim clock = Stopwatch.StartNew()

        Do
            Dim hit = probe()
            If hit IsNot Nothing Then Return hit

            ' Let queued work (timers, RunOnUIThread callbacks) run before probing again.
            Platform.Backend.DoEvents()
            Thread.Sleep(10)
        Loop While clock.ElapsedMilliseconds < timeoutMs

        Throw New TimeoutException($"Timed out after {timeoutMs}ms: {message}")
    End Function
End Module
```

Then: `Wait.For (session, By.Id ("resultsGrid"))`, or
`Wait.Until (() => session.GetText (label) == "Done" ? label : null, 5000, "label never said Done")`.

**Never `Thread.Sleep` alone.** Without `DoEvents()` the queued callback never runs, so a bare sleep
makes the test slower *and* still failing.

---

## Page objects

`By.Id ("okButton")` scattered across 200 tests is the thing that makes UI suites expensive to own. Wrap
each screen once.

**C#**

```csharp
using Majorsilence.Forms.Automation;
using Majorsilence.Forms.Headless;

public sealed class GreetPage
{
    private readonly AutomationSession session;

    public GreetPage (GreetForm form)
    {
        HeadlessRenderer.CapturePng (form, 360, 140);   // layout pass, once, here
        session = new AutomationSession (form);
    }

    // Locators live in exactly one place.
    private AutomationElement NameBox => session.FindOrThrow (By.Id ("nameBox"));
    private AutomationElement Ok      => session.FindOrThrow (By.Id ("okButton"));
    private AutomationElement Cancel  => session.FindOrThrow (By.Id ("cancelButton"));

    // Methods read as user intent, not as clicks.
    public GreetPage EnterName (string name)
    {
        session.Clear (NameBox);
        session.SendKeys (NameBox, name);
        return this;
    }

    public void Accept () => session.Click (Ok);
    public void Dismiss () => session.Click (Cancel);

    public string EnteredName => session.GetText (NameBox);
    public bool CanAccept => Ok.Enabled;
}
```

**VB.NET**

```vb
Imports Majorsilence.Forms.Automation
Imports Majorsilence.Forms.Headless

Public NotInheritable Class GreetPage
    Private ReadOnly session As AutomationSession

    Public Sub New(form As GreetForm)
        HeadlessRenderer.CapturePng(form, 360, 140)     ' layout pass, once, here
        session = New AutomationSession(form)
    End Sub

    Private ReadOnly Property NameBox As AutomationElement
        Get
            Return session.FindOrThrow(By.Id("nameBox"))
        End Get
    End Property

    Private ReadOnly Property Ok As AutomationElement
        Get
            Return session.FindOrThrow(By.Id("okButton"))
        End Get
    End Property

    Public Function EnterName(name As String) As GreetPage
        session.Clear(NameBox)
        session.SendKeys(NameBox, name)
        Return Me
    End Function

    Public Sub Accept()
        session.Click(Ok)
    End Sub

    Public ReadOnly Property EnteredName As String
        Get
            Return session.GetText(NameBox)
        End Get
    End Property

    Public ReadOnly Property CanAccept As Boolean
        Get
            Return Ok.Enabled
        End Get
    End Property
End Class
```

Locators as **properties, not fields** matters: `Find` queries a fresh snapshot every call, so a property
re-resolves after the UI changes while a cached field goes stale.

The test then reads as behaviour:

**C#**

```csharp
using var form = new GreetForm ();
var page = new GreetPage (form);

Assert.False (page.CanAccept);
page.EnterName ("Ada Lovelace").Accept ();
Assert.Equal (DialogResult.OK, form.DialogResult);
```

**VB.NET**

```vb
Using form As New GreetForm()
    Dim page As New GreetPage(form)

    Assert.IsFalse(page.CanAccept)
    page.EnterName("Ada Lovelace").Accept()
    Assert.AreEqual(DialogResult.OK, form.DialogResult)
End Using
```

---

## Level 2 — Selenium and the WebDriver server

`Majorsilence.Forms.WebDriver` hosts a **W3C WebDriver** endpoint over HTTP on loopback. Because
WebDriver is just HTTP and JSON, any client in any language can drive your desktop app — including the
Selenium bindings your web team already uses.

**C#**

```csharp
using Majorsilence.Forms.WebDriver;

using var server = new WebDriverServer (form, port: 4444);
server.Start ();

Console.WriteLine (server.Url);      // http://127.0.0.1:4444/  (loopback only)

// … drive it …

server.Stop ();
```

**VB.NET**

```vb
Imports Majorsilence.Forms.WebDriver

Using server As New WebDriverServer(form, port:=4444)
    server.Start()

    Console.WriteLine(server.Url)    ' http://127.0.0.1:4444/  (loopback only)

    ' … drive it …

    server.Stop()
End Using
```

**Supported commands:** new/delete session, find element(s), click, send keys, clear, get text, get name
(role), get attribute, get rect, get enabled, **page source** (`GET …/source`, XML), screenshot (PNG, via
the offscreen renderer), and `GET /status`.

**Locator strategies:** `id`, `name`, `tag name` (role), `xpath`, `css selector` (`#id` and `[name='…']`
forms), plus the custom `role`, `type`, and `link text`. Element references re-resolve against a fresh
snapshot on every use, preferring the stable AutomationId — so a reference stays valid after the UI
changes underneath it.

### Driving it with the real Selenium client

Element actions are marshalled onto the UI thread, so in a test with no message loop you pump the queue
on the main thread while the HTTP calls run on a worker. This is the pattern the framework's own tests
use, and it's the one to copy:

**C#**

```csharp
using System.Net;
using System.Net.Sockets;
using Majorsilence.Forms.WebDriver;
using OpenQA.Selenium;
using OpenQA.Selenium.Chrome;
using OpenQA.Selenium.Remote;
using MFPlatform = Majorsilence.Forms.Backends.Platform;   // see the gotcha below

static int FreePort ()
{
    var listener = new TcpListener (IPAddress.Loopback, 0);
    listener.Start ();
    var port = ((IPEndPoint) listener.LocalEndpoint).Port;
    listener.Stop ();
    return port;                                   // never hard-code 4444 in CI
}

static T RunPumped<T> (Func<T> work)
{
    var task = Task.Run (work);
    while (!task.IsCompleted) {
        MFPlatform.Backend.DoEvents ();            // pump on this thread
        Thread.Sleep (5);
    }
    return task.GetAwaiter ().GetResult ();
}

[Fact]
public void Selenium_can_drive_the_form ()
{
    using var form = new GreetForm ();
    HeadlessRenderer.CapturePng (form, 360, 140);

    using var server = new WebDriverServer (form, FreePort ());
    server.Start ();

    var typed = RunPumped (() => {
        var driver = new RemoteWebDriver (server.Url,
            new ChromeOptions ().ToCapabilities (), TimeSpan.FromSeconds (30));
        try {
            driver.FindElement (By.CssSelector ("#okButton")).Click ();
            driver.FindElement (By.Name ("nameBox")).SendKeys ("Ada Lovelace");
            return driver.FindElement (By.CssSelector ("#nameBox")).Text;
        } finally {
            driver.Quit ();
        }
    });

    Assert.Equal ("Ada Lovelace", typed);
}
```

**VB.NET**

```vb
Imports System.Net
Imports System.Net.Sockets
Imports Majorsilence.Forms.WebDriver
Imports OpenQA.Selenium
Imports OpenQA.Selenium.Chrome
Imports OpenQA.Selenium.Remote
Imports MFPlatform = Majorsilence.Forms.Backends.Platform

Private Shared Function FreePort() As Integer
    Dim listener As New TcpListener(IPAddress.Loopback, 0)
    listener.Start()
    Dim port = CType(listener.LocalEndpoint, IPEndPoint).Port
    listener.Stop()
    Return port
End Function

Private Shared Function RunPumped(Of T)(work As Func(Of T)) As T
    Dim task = Threading.Tasks.Task.Run(work)
    While Not task.IsCompleted
        MFPlatform.Backend.DoEvents()
        Threading.Thread.Sleep(5)
    End While
    Return task.GetAwaiter().GetResult()
End Function
```

Three gotchas, all found by actually running it:

1. **`Platform` is ambiguous.** `Majorsilence.Forms.Backends.Platform` collides with
   `OpenQA.Selenium.Platform` — CS0104 the moment you import both namespaces. Alias one, as above.
2. **Use `GetDomAttribute`, not `GetAttribute`.** Selenium 4's legacy `GetAttribute` runs a JavaScript
   atom via `/session/{id}/execute/sync`, which a native app has no equivalent for — it throws
   `NotImplementedException`. `GetDomAttribute` hits the plain `/attribute/{name}` endpoint, which is
   implemented, and returns `id`, `name`, `role`, `type`, `value`, `enabled`, `visible`, and the bounds.
3. **Prefer `By.CssSelector ("#id")` and `By.XPath`.** Selenium's .NET client doesn't send
   `using: "name"` for `By.Name` — it rewrites it as the CSS selector `*[name ="x"]`. That form is
   accepted, but `By.CssSelector ("#okButton")` and XPath are the least surprising choices. Anything
   requiring JavaScript (`ExecuteScript`, implicit waits built on it) is unavailable by construction.

### Or skip the bindings entirely

For a smoke test — or from a language with no Selenium install — the raw protocol is three calls:

```bash
SID=$(curl -s -XPOST 127.0.0.1:4444/session -d '{}' | jq -r .value.sessionId)

curl -s 127.0.0.1:4444/session/$SID/source                       # the XML tree
curl -s -XPOST 127.0.0.1:4444/session/$SID/element \
     -d '{"using":"css selector","value":"#okButton"}'            # find
curl -s -XPOST 127.0.0.1:4444/session/$SID/element/$EID/click -d '{}'
```

Python, with no framework knowledge at all:

```python
from selenium import webdriver
from selenium.webdriver.common.by import By

driver = webdriver.Remote("http://127.0.0.1:4444", options=webdriver.ChromeOptions())
print(driver.page_source)                          # the XML tree
driver.find_element(By.CSS_SELECTOR, "#okButton").click()
driver.quit()
```

Capabilities are ignored — the server always grants a session, so pass whatever your client requires.

---

## Recording locators with an inspector

Because the server exposes XML **page source** *and* an `xpath` strategy evaluated against exactly that
source, any Appium-style inspector can render the live element tree over a screenshot and let you capture
locators by clicking nodes. The loop an inspector uses is just three commands the server implements:

| Step | Command | Returns |
|---|---|---|
| Snapshot the tree | `GET /session/{id}/source` | XML (see [the sample above](#one-tree-three-consumers)) |
| Show the UI | `GET /session/{id}/screenshot` | base64 PNG |
| Confirm a locator | `POST /session/{id}/element` + `…/attribute/{name}` | the element / its attributes |

This is the recommended recording path: Selenium IDE records DOM events inside a browser and has no way
to attach to a native app.

| Setting | Value |
|---|---|
| Remote Host | `127.0.0.1` |
| Remote Port | whatever you passed to `WebDriverServer` |
| Remote Path | `/` |
| Protocol | `http`, no SSL |
| Capabilities | any JSON object — capability matching is ignored |

Prefer captured locators in this order: **`id`** (maps to `Control.Name`; element references re-resolve
against it first) → **`xpath`** → `name` / `role` / `type`.

Caveats: this is a W3C WebDriver server, not a full Appium server, so Appium-only endpoints (settings,
gestures, app management) return 404 — a generic WebDriver client is the most reliable inspector. Bounds
are logical client coordinates, so an overlay captured at a different DPI may be offset even when the
locators are right. One window per session, and hidden controls are omitted from the tree.

---

## Level 3 — Windows-native tools (FlaUI, WinAppDriver, Appium)

`Majorsilence.Forms.WindowsUIAutomation` projects the same tree onto **Windows UI Automation**. That's
what lets Narrator, NVDA and JAWS read your app — and it also means UIA-based test tools can drive it
with no custom protocol.

**C#**

```csharp
using Majorsilence.Forms.WindowsUIAutomation;

form.Show ();                       // must be shown first — it needs a native handle
WindowsUIAutomation.Enable (form);  // detaches automatically when the window closes
```

**VB.NET**

```vb
Imports Majorsilence.Forms.WindowsUIAutomation

form.Show()                         ' must be shown first — it needs a native handle
WindowsUIAutomation.Enable(form)    ' detaches automatically when the window closes
```

> **This does not compile off Windows** — verified, not theorised. Away from Windows the package ships
> as an empty stub, so the namespace doesn't exist and you get **CS0234**, not a runtime
> `PlatformNotSupportedException`. Multi-target (`net10.0;net10.0-windows`) and guard with `#if WINDOWS`,
> or keep the call in a Windows-only project.

Each control becomes a UIA element with **Name**, **AutomationId** (`Control.Name`), **ControlType**,
**IsEnabled**, **HasKeyboardFocus** and a screen **BoundingRectangle**. Focus changes raise UIA
focus-changed events.

It's backend-neutral — it works for any Windows host that supplies a native window handle — and moving
keyboard focus fires a UIA focus-changed event, which is what makes a screen reader announce the new
control and a magnifier follow the caret. The focused control's value changes raise a property-changed
event.

**What works today, and what to expect:** the `Invoke` pattern (buttons) is live, so a FlaUI or
WinAppDriver script can find controls and click them. `Value` (text, combo) and `Toggle` (checkbox) are
exposed **for reading**; write support is a later phase — so *setting* text through UIA may not work yet,
and typing via the level-1 or level-2 surfaces is the reliable path. Not in this first cut: per-keystroke
`TextBox` value events (the control doesn't yet raise `TextChanged`, so screen readers fall back to their
own typed-character echo — the field's value is still announced on focus), structure-changed events, and
sub-control items (individual tabs, list rows).

Because of that split, the pragmatic division of labour on Windows is: **drive with level 1 or 2, verify
accessibility with level 3.**

The pure tree/role logic is unit-tested in the framework itself
(`Majorsilence.Forms.WindowsUIAutomation.Tests`, on Windows CI), but the full COM round-trip needs an
interactive Windows desktop session — it can't be asserted headlessly. Run your app, `Enable` the bridge,
then inspect with **Accessibility Insights** or `inspect.exe` to see the tree as a screen reader sees it.
The check that actually matters is turning on **Narrator** and tabbing through the window: each control
should be announced with its name and role, and a button should activate from the screen reader.

Linux (AT-SPI) and macOS (NSAccessibility) bridges over the same tree are roadmap. If you have an
accessibility obligation on those platforms, plan around that now.

---

## Visual regression with golden images

`HeadlessRenderer.CapturePng` renders a form offscreen to PNG bytes. That's your golden-image primitive,
and it needs no display:

**C#**

```csharp
[Fact]
public void GreetForm_matches_its_golden_image ()
{
    using var form = new GreetForm ();
    var actual = HeadlessRenderer.CapturePng (form, 360, 140);

    var goldenPath = Path.Combine (AppContext.BaseDirectory, "Golden", "greetform.png");

    if (!File.Exists (goldenPath) || Environment.GetEnvironmentVariable ("UPDATE_GOLDEN") == "1") {
        Directory.CreateDirectory (Path.GetDirectoryName (goldenPath)!);
        File.WriteAllBytes (goldenPath, actual);
        return;                                   // first run records; never silently passes later
    }

    var expected = File.ReadAllBytes (goldenPath);

    if (!expected.AsSpan ().SequenceEqual (actual)) {
        // Write the actual bytes so CI can attach them as an artifact.
        File.WriteAllBytes (Path.ChangeExtension (goldenPath, ".actual.png"), actual);
        Assert.Fail ($"Render differs from {goldenPath}. Actual written alongside it.");
    }
}
```

**VB.NET**

```vb
<TestMethod>
Public Sub GreetForm_matches_its_golden_image()
    Using form As New GreetForm()
        Dim actual = HeadlessRenderer.CapturePng(form, 360, 140)

        Dim goldenPath = Path.Combine(AppContext.BaseDirectory, "Golden", "greetform.png")

        If Not File.Exists(goldenPath) OrElse
           Environment.GetEnvironmentVariable("UPDATE_GOLDEN") = "1" Then
            Directory.CreateDirectory(Path.GetDirectoryName(goldenPath))
            File.WriteAllBytes(goldenPath, actual)
            Return
        End If

        Dim expected = File.ReadAllBytes(goldenPath)

        If Not expected.SequenceEqual(actual) Then
            File.WriteAllBytes(Path.ChangeExtension(goldenPath, ".actual.png"), actual)
            Assert.Fail($"Render differs from {goldenPath}. Actual written alongside it.")
        End If
    End Using
End Sub
```

Practical rules, learned the boring way:

- **Regenerate deliberately, never automatically.** An `UPDATE_GOLDEN=1` env gate (or the equivalent in
  [Verify](https://github.com/VerifyTests/Verify) / ApprovalTests, which give you this plus a diff-tool
  launcher for free) keeps "the test went green" from meaning "the baseline moved."
- **Always publish the `.actual.png`** as a CI artifact. A byte-compare failure with no image attached is
  a bug report nobody can action.
- **Golden-image a handful of screens, not all of them.** They catch layout and theming regressions; they
  also fail on every intentional pixel change, so keep the set small and high-value.
- **Fonts differ across OSes.** Pin golden images to one platform in CI (Linux is the cheapest) rather
  than maintaining a baseline per OS.
- **Don't golden-image at `MF_HEADLESS_SCALE=2`** unless you also keep a 2× baseline — assert layout
  *proportionally* in scaled runs instead, and keep pixel comparison at scale 1.

For semantic (non-pixel) snapshots, `session.GetPageSource()` is a far more stable baseline: it changes
when structure changes and ignores rendering. Snapshot that XML for "did the UI keep the same shape"
checks and reserve PNGs for "does it still look right."

---

## BDD: Reqnroll / SpecFlow on top

Nothing special is required — the page objects do the work, and the step definitions stay thin.

```gherkin
Feature: Greeting

  Scenario: A name is required
    Given the greeter is open
    When I press OK without entering a name
    Then I am warned that a name is required

  Scenario: Entering a name accepts the dialog
    Given the greeter is open
    When I enter the name "Ada Lovelace"
    And I press OK
    Then the dialog is accepted
```

**C#**

```csharp
using Reqnroll;

[Binding]
public sealed class GreetingSteps : IDisposable
{
    private readonly GreetForm form = new ();
    private GreetPage? page;

    [Given ("the greeter is open")]
    public void GivenTheGreeterIsOpen () => page = new GreetPage (form);

    [When ("I enter the name {string}")]
    public void WhenIEnterTheName (string name) => page!.EnterName (name);

    [When ("I press OK")]
    public void WhenIPressOk () => page!.Accept ();

    [Then ("the dialog is accepted")]
    public void ThenTheDialogIsAccepted ()
        => Assert.Equal (DialogResult.OK, form.DialogResult);

    public void Dispose () => form.Dispose ();
}
```

**VB.NET**

```vb
Imports Reqnroll

<Binding>
Public NotInheritable Class GreetingSteps
    Implements IDisposable

    Private ReadOnly form As New GreetForm()
    Private page As GreetPage

    <Given("the greeter is open")>
    Public Sub GivenTheGreeterIsOpen()
        page = New GreetPage(form)
    End Sub

    <When("I enter the name {string}")>
    Public Sub WhenIEnterTheName(name As String)
        page.EnterName(name)
    End Sub

    <Then("the dialog is accepted")>
    Public Sub ThenTheDialogIsAccepted()
        Assert.AreEqual(DialogResult.OK, form.DialogResult)
    End Sub

    Public Sub Dispose() Implements IDisposable.Dispose
        form.Dispose()
    End Sub
End Class
```

Keep the parallelism disabled at the assembly level ([above](#3-run-ui-tests-serially)) — that applies to
BDD runners too.

---

## CI recipes

No display, no driver downloads, no X server. A UI suite is just `dotnet test`.

### GitHub Actions

```yaml
name: CI

on: [push, pull_request]

jobs:
  test:
    runs-on: ubuntu-latest          # no display needed — Headless backend
    steps:
      - uses: actions/checkout@v4
      - uses: actions/setup-dotnet@v4
        with:
          dotnet-version: 10.0.x

      - run: dotnet build --configuration Release
      - run: dotnet test --configuration Release --no-build --logger "trx;LogFileName=test.trx"

      # Layout at 2x. Keep it a separate job/step so a scaling failure is legible.
      - name: UI tests at simulated HiDPI
        env:
          MF_HEADLESS_SCALE: "2"
        run: dotnet test --configuration Release --no-build --filter "Category=Scaling"

      - name: Upload failed golden images
        if: failure()
        uses: actions/upload-artifact@v4
        with:
          name: golden-image-diffs
          path: |
            **/*.actual.png
            **/Golden/**
```

Add a Windows job only for what genuinely needs Windows — the UIA/accessibility pass:

```yaml
  accessibility:
    runs-on: windows-latest
    steps:
      - uses: actions/checkout@v4
      - uses: actions/setup-dotnet@v4
        with:
          dotnet-version: 10.0.x
      - run: dotnet test tests/YourApp.Accessibility.Tests --configuration Release
```

### Azure DevOps

```yaml
pool:
  vmImage: ubuntu-latest

steps:
  - task: UseDotNet@2
    inputs:
      version: 10.0.x

  - script: dotnet build --configuration Release
    displayName: Build

  - script: dotnet test --configuration Release --no-build --logger trx
    displayName: Test

  - task: PublishTestResults@2
    condition: always()
    inputs:
      testResultsFormat: VSTest
      testResultsFiles: '**/*.trx'
```

### Jenkins

Jenkins needs a little more setup than the hosted services, because it can't read .NET's native test output
and because Windows agents are usually installed in a way that breaks UI automation. Both are one-time
fixes.

**First, pick a test-results publisher.** `dotnet test` writes TRX, which no Jenkins publisher reads
natively — so you add a logger package to each test project and point the matching step at its output.
Three combinations work; pick by which plugin your Jenkins already has:

| Publisher step | Logger package | Plugin |
|---|---|---|
| `junit` | `JunitXml.TestLogger` | JUnit plugin — present in the standard Jenkins setup, so this needs no plugin work |
| `nunit` | `NunitXml.TestLogger` | NUnit plugin — a separate install, but many .NET shops already run it |
| `mstest` | *none* — use `--logger trx` | MSTest plugin — a separate install; converts TRX, and is the only option if you can't add a package reference |

```xml
<!-- one of these, in each test project -->
<PackageReference Include="JunitXml.TestLogger" Version="8.0.0" />
<PackageReference Include="NunitXml.TestLogger" Version="8.0.0" />
```

The two loggers are interchangeable in every way that matters here: same `--logger "<name>;LogFilePath=…"`
syntax, same `{assembly}` token, same namespace caveat below. Only the output dialect and the Jenkins step
differ. Without the package, `--logger junit` **fails the build** with
`Could not find a test logger with AssemblyQualifiedName, URI or FriendlyName 'junit'` — not a silent
no-op, which at least makes the omission obvious the first time.

Then the `Jenkinsfile` (JUnit variant; the NUnit swap is [below](#using-the-nunit-plugin-instead)):

```groovy
pipeline {
    agent none

    options {
        timestamps()
        buildDiscarder(logRotator(numToKeepStr: '30', artifactNumToKeepStr: '10'))
    }

    environment {
        DOTNET_CLI_TELEMETRY_OPTOUT = '1'
        DOTNET_NOLOGO               = '1'
    }

    stages {
        stage('Verify') {
            parallel {

                stage('Headless UI suite') {
                    agent {
                        docker {
                            image 'mcr.microsoft.com/dotnet/sdk:10.0'
                            // dotnet needs a writable HOME; the mount keeps NuGet restores warm
                            // between builds (NUGET_PACKAGES resolves under HOME).
                            args '-e DOTNET_CLI_HOME=/tmp -e HOME=/tmp ' +
                                 '-v $HOME/.nuget/packages:/tmp/.nuget/packages'
                        }
                    }
                    steps {
                        sh 'dotnet build --configuration Release'

                        // No display, no Xvfb, no driver downloads — the Headless backend.
                        sh '''
                            dotnet test --configuration Release --no-build \
                                --logger "junit;LogFilePath=$WORKSPACE/artifacts/junit/{assembly}.xml"
                        '''

                        // Layout at 2x. Separate invocation so a scaling failure is legible
                        // in the Jenkins test report rather than mixed into the main run.
                        withEnv(['MF_HEADLESS_SCALE=2']) {
                            sh '''
                                dotnet test --configuration Release --no-build \
                                    --filter "Category=Scaling" \
                                    --logger "junit;LogFilePath=$WORKSPACE/artifacts/junit/{assembly}.hidpi.xml"
                            '''
                        }
                    }
                    post {
                        always {
                            junit testResults: 'artifacts/junit/*.xml', allowEmptyResults: false
                            // Golden-image failures are unreviewable without the image.
                            archiveArtifacts artifacts: '**/*.actual.png', allowEmptyArchive: true
                        }
                    }
                }

                stage('Accessibility (Windows)') {
                    // Must be an agent running in an interactive desktop session — see below.
                    agent { label 'windows-desktop' }
                    steps {
                        // Triple-quoted: a single-quoted Groovy string cannot span lines.
                        // Forward slashes are fine for dotnet on Windows.
                        bat '''
                            dotnet test tests/YourApp.Accessibility.Tests --configuration Release ^
                                --logger "junit;LogFilePath=%WORKSPACE%/artifacts/junit/uia.xml"
                        '''
                    }
                    post {
                        always {
                            junit testResults: 'artifacts/junit/uia.xml', allowEmptyResults: true
                        }
                    }
                }
            }
        }
    }
}
```

#### Using the NUnit plugin instead

If your Jenkins already has the NUnit plugin, swap the package for `NunitXml.TestLogger` and change two
lines — the logger name and the publisher step. Note the parameter name differs: `junit` takes
`testResults`, `nunit` takes `testResultsPattern`.

```groovy
steps {
    sh '''
        dotnet test --configuration Release --no-build \
            --logger "nunit;LogFilePath=$WORKSPACE/artifacts/nunit/{assembly}.xml"
    '''
}
post {
    always {
        nunit testResultsPattern: 'artifacts/nunit/*.xml', failedTestsFailBuild: true
        archiveArtifacts artifacts: '**/*.actual.png', allowEmptyArchive: true
    }
}
```

That produces NUnit v3 `<test-run>` XML, which the plugin converts on the way in — so the Jenkins test
trend, per-test history, and failure browsing all behave exactly as they do with `junit`. There is no
functional reason to prefer one over the other; use whichever plugin you already maintain.

#### Four Jenkins-specific things worth knowing

All of these cost an afternoon to discover otherwise.

- **Namespace your test classes.** Both loggers derive each test's `classname` from its namespace. A class
  in the global namespace comes out as `classname="UnknownNamespace.UnknownType"` — verified by running
  both loggers side by side — so every test lands in one meaningless bucket and Jenkins' test browser can't
  group anything. A class in `namespace MyApp.UiTests` comes out as
  `classname="MyApp.UiTests.GreetFormTests"` and the report becomes navigable.
- **`{assembly}` in `LogFilePath` expands to the test assembly name**, so multiple test projects don't
  overwrite each other's results. Create the directory or let the logger do it, and point `junit` at the
  glob rather than a single file.
- **The Windows agent must run in an interactive desktop session.** The Windows UIA bridge needs a real
  native window and a desktop to attach to. A Jenkins agent installed as a *Windows service* has no
  interactive session, so windowed apps and every UIA assertion fail in ways that look like framework bugs.
  Launch that agent from a logged-in user session instead (a scheduled task at logon running the agent
  JAR, or the agent started manually on a dedicated box). The Linux stage has no such requirement —
  that's the whole point of the Headless backend.
- **Each `agent` block gets its own workspace.** `--no-build` only works within one agent; across agents
  the build output isn't there. Either build in each stage (as above) or `stash`/`unstash` the output
  explicitly. Adding a per-stage `agent` to a pipeline that previously used one is the usual cause of a
  sudden "project file not found" or "assembly missing" failure.

If your Jenkins has no Docker, drop the `docker` agent for a plain `agent { label 'linux' }` and install
the .NET SDK on the node (or use the **.NET SDK Support** plugin's `dotnetsdk` tool and wrap the steps in
`withDotNet`). Nothing about the test suite changes — it needs no display either way.

> **What was verified here, and what wasn't.** The .NET half was run: both loggers (`JunitXml.TestLogger`
> and `NunitXml.TestLogger`, 8.0.0), the failure message when the package is missing, the `{assembly}`
> token expanding to one file per test assembly, and the namespace-to-`classname` behaviour above. The
> Jenkins steps and plugin parameters come from those plugins' own documentation rather than from a live
> controller — check `testResults` / `testResultsPattern` against your installed plugin versions.

### The one gate people skip

If you ship to the browser, **building the wasm target is not evidence it works.** `dotnet publish` is
what runs the wasm-tools pipeline (the emcc/wasm-opt native link), and only a real boot in a browser
proves the bundle. Publish it and smoke-test with headless Chromium:

```yaml
  wasm:
    runs-on: ubuntu-latest
    steps:
      - uses: actions/checkout@v4
      - uses: actions/setup-dotnet@v4
        with: { dotnet-version: 10.0.x }
      - run: dotnet workload install wasm-tools
      - run: dotnet publish src/YourApp.Wasm -c Release -o out
      - run: npx playwright install --with-deps chromium
      # Serve out/wwwroot and assert the canvas renders / no console errors.
      - run: node scripts/wasm-smoke.mjs
```

Note the irony worth knowing: Playwright can't drive your *app's* UI (no DOM — see
[limits](#limits-and-anti-patterns)), but it's exactly the right tool for asserting that the WebAssembly bundle boots.

---

## How AI tools hook into all of this

This framework is unusually friendly to AI coding assistants and agents, for one structural reason:

> **The automation tree is text.** `session.GetPageSource()` returns the live UI as XML, with ids, names,
> roles, values, state and bounds.

A model can *read* your user interface without pixels, without OCR, and without a vision model. That
turns "drive the GUI" from a computer-use problem into a text problem — which is both far cheaper and far
more reliable.

```xml
<Form name="Greeter" role="window" type="Form" x="0" y="0" width="360" height="140">
  <Label   id="promptLabel" name="Your name:" role="label" ... />
  <TextBox id="nameBox" name="Full name" role="textbox" value="" enabled="true" ... />
  <Button  id="okButton" name="OK" role="button" enabled="false" ... />
</Form>
```

Four integration patterns, cheapest first.

### 1. Shell + curl — zero integration work

Any agent that can run shell commands can already drive your app: start the WebDriver server, then have
it `curl` the endpoints from [level 2](#or-skip-the-bindings-entirely). No bindings, no SDK, no MCP server. This is the
fastest way to let a coding assistant *check its own work* on a running app, and it's usually where to
start.

Give the agent the three commands (`/source`, `/element`, `/element/{id}/click`) and it can explore.

### 2. Point the assistant at the test loop

The highest-value pattern needs no new API surface at all — it's that **the whole loop closes without a
display**:

1. The agent reads `GetPageSource()` output (or an existing test) to learn the control names.
2. It writes a test using `By.Id` locators.
3. It runs `dotnet test`.
4. It reads the failure, edits, repeats.

That works in a container, in CI, and on a machine with no GUI session — which is exactly where coding
agents run. Compare it to pixel-driven GUI automation, where the agent can't see the result at all
without a screen.

A short `AGENTS.md` / `CLAUDE.md` in your repo is enough to make an assistant good at this:

```markdown
## Running and testing the UI

- UI tests run headlessly: `dotnet test`. No display required. Never add `Thread.Sleep`;
  use the `Wait` helper in `tests/Support/Wait.cs` (it pumps the backend queue).
- The backend is installed once per test assembly in `TestBackend.Init` — don't set it per test.
- Tests must stay serial: `Platform.Backend` and `Application.OpenForms` are global.
- Locators come from `Control.Name` (`By.Id("okButton")`). If a control has no `Name`, add one
  rather than locating by text or index.
- Call `HeadlessRenderer.CapturePng(form, w, h)` once before automating a form — it forces layout.
- To see the UI as the automation layer sees it: `session.GetPageSource()` prints the tree as XML.
- Assert the *effect* (state, DialogResult, rendered output), never that a member exists.
```

### 3. An MCP tool surface — for interactive assistants

To let an assistant drive a *running* app conversationally, expose the automation surface as
[Model Context Protocol](https://modelcontextprotocol.io) tools. **There is a server for this in the
repo** — `tools/Majorsilence.Forms.Mcp`, a `dotnet` tool that speaks MCP over stdin/stdout and drives
the app through the WebDriver endpoint from [level 2](#level-2--selenium-and-the-webdriver-server):

```
assistant  ──MCP/stdio──▶  majorsilence-mcp  ──HTTP/loopback──▶  your app (WebDriverServer)
```

Bridging over HTTP rather than linking against the framework is what makes it version- and
backend-independent: it drives any Majorsilence.Forms app that starts a `WebDriverServer`, and never has
to marshal onto somebody else's UI thread.

So the app-side setup is the two lines you already need for Selenium:

```csharp
using var server = new WebDriverServer (form, 4444);
server.Start ();
```

Then point a client at it. Claude Code:

```
claude mcp add majorsilence-ui -- majorsilence-mcp --port 4444
```

Any client that launches MCP servers itself takes the same command in its own config format:

```json
{
  "mcpServers": {
    "majorsilence-ui": {
      "command": "majorsilence-mcp",
      "args": ["--port", "4444"]
    }
  }
}
```

The tools it exposes:

| Tool | Arguments | Returns |
|---|---|---|
| `ui_snapshot` | — | the whole control tree as XML |
| `ui_find` | `target`, `strategy` | id, name, role, type, value, text, enabled/visible, bounds |
| `ui_read` | `target`, `strategy` | what the control currently displays |
| `ui_click` | `target`, `strategy` | confirmation, or why it was refused |
| `ui_type` | `target`, `text`, `strategy`, `clear` | what the control reads afterwards |
| `ui_wait_for` | `target`, `strategy`, `timeoutMs`, `requireEnabled` | readiness, or why the wait timed out |
| `ui_screenshot` | — | a PNG of the window |

Three decisions in there worth copying if you build your own:

- **Every tool takes a locator, not an element handle.** Each call re-resolves what it acts on, so the
  [staleness trap](#level-1--in-process-tests-on-the-headless-backend) has no way to bite: there is no handle for a model to hold across turns.
- **`strategy` defaults to `id`** — the control's `Name` — and an unrecognised strategy is rejected by
  name rather than passed through. The server falls back to a *name* lookup for anything it doesn't
  recognise, so an unchecked typo would silently search the wrong way and answer "not found".
- **`ui_click` and `ui_type` are annotated destructive, the rest read-only**, which is what a host shows
  the user when deciding what to auto-approve.

Where it comes from: it packs as the `Majorsilence.Forms.Mcp` global tool
(`dotnet tool install -g Majorsilence.Forms.Mcp`) from the next release onwards. Before that — and today —
run it out of the repo, which also gets you `Majorsilence.Forms.WebDriver` (not yet on NuGet either, so
the app under test references that project from source):

```
dotnet run --project tools/Majorsilence.Forms.Mcp -- --port 4444
```

#### Or host the surface inside your own process

If you'd rather expose these tools from inside the app — or wire them into an agent framework that isn't
MCP — the framework-specific part is a thin adapter over `AutomationSession`:

**C#**

```csharp
using Majorsilence.Forms;
using Majorsilence.Forms.Automation;
using Majorsilence.Forms.Headless;

// One instance per app-under-test. Every method is called on the UI thread.
public sealed class UiAgentSurface
{
    private readonly Form form;
    private readonly AutomationSession session;

    public UiAgentSurface (Form form)
    {
        this.form = form;
        session = new AutomationSession (form);
    }

    public string Snapshot () => session.GetPageSource ();

    public string Click (string id)
    {
        var element = session.Find (By.Id (id));
        if (element is null)
            return $"no element with id '{id}'";       // a plain error the model can act on
        if (!element.Enabled)
            return $"'{id}' is disabled";

        session.Click (element);
        return "ok";
    }

    public string Type (string id, string text)
    {
        var element = session.FindOrThrow (By.Id (id));
        session.Clear (element);
        session.SendKeys (element, text);

        // Re-resolve to read: the captured element is a snapshot from before the typing.
        return session.GetText (session.FindOrThrow (By.Id (id)));
    }

    public string Read (string id) => session.GetText (session.FindOrThrow (By.Id (id)));

    public byte[] Screenshot (int width, int height)
        => HeadlessRenderer.CapturePng (form, width, height);
}
```

**VB.NET**

```vb
Imports Majorsilence.Forms
Imports Majorsilence.Forms.Automation
Imports Majorsilence.Forms.Headless

Public NotInheritable Class UiAgentSurface
    Private ReadOnly form As Form
    Private ReadOnly session As AutomationSession

    Public Sub New(form As Form)
        Me.form = form
        session = New AutomationSession(form)
    End Sub

    Public Function Snapshot() As String
        Return session.GetPageSource()
    End Function

    Public Function Click(id As String) As String
        Dim element = session.Find(By.Id(id))
        If element Is Nothing Then Return $"no element with id '{id}'"
        If Not element.Enabled Then Return $"'{id}' is disabled"

        session.Click(element)
        Return "ok"
    End Function

    Public Function Type(id As String, text As String) As String
        Dim element = session.FindOrThrow(By.Id(id))
        session.Clear(element)
        session.SendKeys(element, text)

        ' Re-resolve to read: the captured element is a snapshot from before the typing.
        Return session.GetText(session.FindOrThrow(By.Id(id)))
    End Function

    Public Function Screenshot(width As Integer, height As Integer) As Byte()
        Return HeadlessRenderer.CapturePng(form, width, height)
    End Function
End Class
```

Two design notes that matter more than the plumbing:

- **Return errors as text, not exceptions.** "no element with id 'okButton'" is something a model can
  recover from; a stack trace across a tool boundary usually isn't.
- **Marshal onto the UI thread.** If your host app runs a real message loop, wrap each call with
  `Application.RunOnUIThread`. The WebDriver server already does this for you — which is a good argument
  for wrapping *it* instead of `AutomationSession` when the app is a running desktop process.

**Or skip MCP entirely:** because the WebDriver endpoint is plain HTTP on loopback, an assistant with
shell access ([pattern 1](#1-shell--curl--zero-integration-work)) or a generic HTTP-capable MCP server can drive the same app with no
extra process at all.

### 4. Your own agent loop, in-process

If you're building an agent *into* your product — a "do this for me" assistant that operates the UI —
the same adapter becomes tool definitions in a normal tool-use loop. With the
[Anthropic C# SDK](https://github.com/anthropics/anthropic-sdk-csharp) (`dotnet add package Anthropic`),
tools are raw JSON schemas and `client.Beta.Messages.ToolRunner(...)` runs the loop for you, calling your
functions and feeding results back until the model stops:

```csharp
using System.Text.Json;
using Anthropic;
using Anthropic.Models.Messages;

var clickTool = new Tool {
    Name = "ui_click",
    Description = "Click a control in the running application by its automation id. "
                + "Call ui_snapshot first to discover valid ids.",
    InputSchema = new () {
        Properties = new Dictionary<string, JsonElement> {
            ["id"] = JsonSerializer.SerializeToElement (
                new { type = "string", description = "The control's automation id, e.g. okButton" }),
        },
        Required = ["id"],
    },
};

// Model default: claude-opus-5. Then dispatch tool calls to UiAgentSurface above.
```

Give each tool a **prescriptive** description — say *when* to call it, not just what it does ("Call
`ui_snapshot` before your first `ui_click` in a screen you haven't inspected"). That single habit does
more for reliability than any amount of prompt tuning.

### Guardrails for agent-written tests

Agents are genuinely good at writing this kind of test. They're also good at writing tests that pass
without proving anything, so review for these specifically:

- **Assert effects, not existence.** `Assert.NotNull(session.Find(By.Id("okButton")))` proves the control
  exists; it doesn't prove clicking it does anything. This matters more here than in most frameworks,
  because unimplemented members
  [safely no-op rather than throwing](https://forms.majorsilence.com/training/#module-3-stub-policy) — a test
  that only checks a member is reachable will pass against a stub.
- **Watch for assertions loosened to make a test green.** A changed `Assert.Equal` is a behaviour change
  in disguise. Diff the assertions, not just the test count.
- **Never let an agent regenerate golden images.** Keep `UPDATE_GOLDEN` a human action; a baseline the
  agent rewrote to match its own output tests nothing.
- **Require a `Name` rather than an index.** `By.XPath("(//Button)[3]")` works until someone adds a
  button. If a control has no `Name`, the correct fix is to add one.
- **Have it read the page source before locating.** Locators invented from source code, rather than from
  the actual tree, are the most common cause of agent-written flakes.

---

## Limits and anti-patterns

**Playwright cannot drive your app.** It automates browser engines over the Chrome DevTools Protocol
against a DOM; a Majorsilence.Forms app renders natively with Skia and has no DOM or browser engine to
attach to. The only way it would apply is hosting the UI inside a real web view, which this framework
does not do. The HTTP surface *could* be exercised from Playwright's API-request client, treating it as an
HTTP service — but that isn't browser automation and offers nothing over a plain WebDriver client. Use
[the WebDriver server](#level-2--selenium-and-the-webdriver-server) instead. (Playwright *is* the right tool for smoke-testing that your
[WebAssembly bundle boots](#the-one-gate-people-skip) — a different job.)

Other boundaries worth knowing before you design a suite around them:

| Limit | Consequence |
|---|---|
| One window per WebDriver session | No frame or window switching; multi-window flows belong at level 1 |
| Hidden controls omitted from the tree | You can't assert on an invisible control's contents — assert visibility instead |
| No JavaScript execution endpoint | Selenium APIs built on `execute/sync` (`ExecuteScript`, `GetAttribute`, JS-based waits) are unavailable; use `GetDomAttribute` and your own polling |
| No implicit waits | Bring your own [`Wait` helper](#waiting-without-threadsleep) |
| UIA write patterns incomplete | Set text via level 1/2; use level 3 to verify announcement, not to drive input |
| UIA package is Windows-only at compile time | Guard with `#if WINDOWS` or isolate in a Windows-only project |
| `AutomationElement` is an immutable snapshot | Actions accept a captured element; **reads must re-resolve** ([above](#level-1--in-process-tests-on-the-headless-backend)) |
| Tests share global backend state | Serial execution, always |

And the two habits that cause most of the pain:

- **Don't assert scale-1 pixel geometry.** The framework's own HiDPI failures were almost entirely one
  confusion — logical versus device units. `Bounds`, `MouseEventArgs` and `GetTabRect` are logical;
  `ClientRectangle`, back buffers and captured bitmaps are device pixels. They're identical at scale 1,
  so mixing them is invisible until a scaled display shows up. Assert proportionally.
- **Don't test on the Avalonia backend under a runner.** It will appear to work and then deadlock or
  behave inconsistently, because its dispatcher is thread-bound. Headless exists for this.

---

## Roadmap

- ✅ **Windows UI Automation bridge** — screen readers, magnifiers, and existing UIA tools (FlaUI,
  Appium/WinAppDriver) with no custom protocol.
- Complete the UIA patterns: `Value`/`Toggle` write support, structure-changed events, and `TextBox`
  per-keystroke value events (raising `TextChanged` from the editor).
- **AT-SPI (Linux)** and **NSAccessibility (macOS)** bridges over the same tree.
- Expand roles and states (selection, expand/collapse, value ranges), and surface non-control items such
  as individual tabs and list items.
- A higher-level `Majorsilence.Forms.Testing` ergonomics layer — fluent helpers and golden-image asserts,
  so the [wait helper](#waiting-without-threadsleep) and [golden-image plumbing](#visual-regression-with-golden-images) above stop being yours to own.

---

## Where to go next

- [Training guide, module 8](https://forms.majorsilence.com/training/#module-8) — this material at a glance,
  inside the wider curriculum.
- [Module 10](https://forms.majorsilence.com/training/#module-10-ci) — the full CI gate list for an app,
  including migration drift.
- [Platform backends](backends.md) — what the Headless backend is, and the seam
  that makes one test suite cover every target.
