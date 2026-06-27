# WinForms Interop

`Majorsilence.Forms.WindowsFormsInterop` is a Windows-only bridge that lets Majorsilence.Forms and
legacy `System.Windows.Forms` coexist in the same process during an incremental migration.

**Windows-only.** `System.Windows.Forms` does not exist on macOS or Linux.  
**Threading.** All calls must be made on the UI (STA) thread — exactly like WinForms itself.
To cross from a background thread: `Majorsilence.Forms.Application.RunOnUIThread(() => ...)`.

---

## How the message loop works

On Windows, Avalonia's Win32 backend registers its windows with the OS message pump rather than
running its own independent loop. `System.Windows.Forms` also uses the Win32 pump. The two
toolkits therefore share whichever `Application.Run` is called — one loop services both.

Modal dialogs work through nested Win32/Avalonia dispatcher frames, the same mechanism both
frameworks already use internally.

---

## Direction A — Majorsilence.Forms app opening a WinForms form

Use this when you have migrated your app to Majorsilence.Forms but still have one or more legacy
`System.Windows.Forms` forms that have not been converted yet.

```csharp
// Program.cs — starts a Majorsilence.Forms app
Application.Run(new MyMainForm());
```

```csharp
// From any Majorsilence.Forms event handler:
using Majorsilence.Forms.Interop;

// Modeless — returns immediately
WindowsFormsInterop.Show(new LegacySettingsForm(), owner: this);

// Modal — blocks until the WinForms dialog closes
var result = WindowsFormsInterop.ShowDialog(new LegacyWizardForm(), owner: this);
if (result == System.Windows.Forms.DialogResult.OK) { /* ... */ }

// Factory overloads — construct the form on the UI thread at show time
WindowsFormsInterop.Show(() => new LegacySettingsForm());
var r = WindowsFormsInterop.ShowDialog(() => new LegacyWizardForm());
```

### Owner handle (optional)

To make the WinForms dialog properly owned by (and modal to) the Majorsilence parent window,
wire up `OwnerHandleResolver` once during app startup:

```csharp
// Avalonia backend exposes the underlying HWND via TryGetPlatformHandle().
WindowsFormsInterop.OwnerHandleResolver = mfForm =>
{
    var host = mfForm.Backend as Majorsilence.Forms.Backends.MajorsilenceFormsWindowHost;
    return host?.TryGetPlatformHandle()?.Handle ?? IntPtr.Zero;
};
```

Until this is set, WinForms forms are shown unowned (no forced Z-ordering / modal blocking at
the Win32 level).

---

## Direction B — WinForms app opening a Majorsilence.Forms form

Use this when you are adding new screens to an existing WinForms application and want those
new screens built with Majorsilence.Forms, or when you want to validate Majorsilence.Forms
before committing to a full migration.

```csharp
// Program.cs — starts a WinForms app
[STAThread]
static void Main()
{
    System.Windows.Forms.Application.EnableVisualStyles();
    System.Windows.Forms.Application.SetCompatibleTextRenderingDefault(false);
    System.Windows.Forms.Application.SetHighDpiMode(HighDpiMode.PerMonitorV2);

    // Initialize Majorsilence.Forms (Avalonia) so it shares this Win32 pump.
    // Call once before the first Majorsilence window is created.
    WindowsFormsInterop.InitializeMajorsilence();

    System.Windows.Forms.Application.Run(new MainForm());
}
```

```csharp
// From any WinForms event handler:
using Majorsilence.Forms.Interop;
using MF = Majorsilence.Forms;

// Modeless — returns immediately; WinForms message loop services both toolkits
WindowsFormsInterop.ShowMajorsilenceForm(new NewSettingsForm(), owner: this);

// Modal — blocks via a nested Avalonia/Win32 dispatcher frame until the MF window closes
MF.DialogResult result = WindowsFormsInterop.ShowMajorsilenceDialog(new NewWizardForm(), owner: this);
if (result == MF.DialogResult.OK) { /* ... */ }

// Factory overloads
WindowsFormsInterop.ShowMajorsilenceForm(() => new NewSettingsForm());
var r = WindowsFormsInterop.ShowMajorsilenceDialog(() => new NewWizardForm());
```

`DialogResult.None` means the window was closed without an explicit result (e.g. title-bar ✕).

### Making the MF dialog closeable with a result

In your Majorsilence.Forms form, set `DialogResult` before closing:

```csharp
okButton.Click += (_, _) =>
{
    DialogResult = Majorsilence.Forms.DialogResult.OK;
    Close();
};
cancelButton.Click += (_, _) =>
{
    DialogResult = Majorsilence.Forms.DialogResult.Cancel;
    Close();
};
```

---

## Full round-trip

The two directions compose freely: a WinForms host can open an MF window (Direction B), and
that MF window can open a legacy WinForms form (Direction A). See the
[`samples/WinFormsInterop`](../samples/WinFormsInterop) project for a runnable demonstration.

```
WinFormsShellForm (WF)
  └─ ShowMajorsilenceForm(new SpikeForm())   ← Direction B
       └─ WindowsFormsInterop.ShowDialog(new SampleLegacyForm())  ← Direction A
```

---

## Limitations

| Limitation | Detail |
|---|---|
| Windows-only | `System.Windows.Forms` is not available on macOS/Linux. The `WindowsFormsInterop` assembly compiles to an empty placeholder on non-Windows to keep CI green, but all calls throw `PlatformNotSupportedException` at runtime. |
| Win32 parenting | The `owner` parameter in the MF→WF direction uses the HWND resolver (see above). In the WF→MF direction, Win32-level parenting is currently not enforced by the Avalonia backend — the MF window is unowned at the OS level. |
| One `Application.Run` | Do not call both `Majorsilence.Forms.Application.Run` and `System.Windows.Forms.Application.Run` in the same process. Pick one as the host; use the bridge for the other direction. |
