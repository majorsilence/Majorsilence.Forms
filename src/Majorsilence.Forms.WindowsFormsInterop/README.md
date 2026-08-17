# Majorsilence.Forms.WindowsFormsInterop

**Migrate a WinForms application one screen at a time**, instead of all at once.

A Windows-only bridge that lets real `System.Windows.Forms` forms and
[Majorsilence.Forms](https://www.nuget.org/packages/Majorsilence.Forms) forms live in the same
process, in both directions. On Windows, Avalonia's Win32 backend shares the existing Win32 message
pump, so both toolkits are serviced by a single `Application.Run` — no second thread and no second
message loop.

That makes an incremental migration practical: port the screens that matter, keep the rest on real
WinForms, and ship working software the whole way through. When every screen is over, drop this
package and the app is cross-platform.

Windows-only by definition — `System.Windows.Forms` does not exist on macOS or Linux. Off Windows it
builds as an empty placeholder assembly so a cross-platform solution still compiles everywhere;
calls throw `PlatformNotSupportedException` at runtime.

## Install

```bash
dotnet add package Majorsilence.Forms.WindowsFormsInterop
```

## Direction A — open a legacy WinForms form from a Majorsilence.Forms app

```csharp
using Majorsilence.Forms.Interop;

WindowsFormsInterop.Show (new LegacyReportForm ());                  // modeless
var result = WindowsFormsInterop.ShowDialog (new LegacySettingsForm ());
```

## Direction B — open a Majorsilence.Forms form from an existing WinForms app

Call `InitializeMajorsilence()` once in `Program.Main`, after WinForms initializes:

```csharp
using Majorsilence.Forms.Interop;

WindowsFormsInterop.InitializeMajorsilence ();

// … later, from a WinForms event handler:
WindowsFormsInterop.ShowMajorsilenceForm (new NewDashboardForm ());
```

Call these on the UI (STA) thread. From a background thread, marshal first:

```csharp
Majorsilence.Forms.Application.RunOnUIThread (() => WindowsFormsInterop.Show (form));
```

## Links

- [Migration guide](https://forms.majorsilence.com/migration/) — the `majorsilence-migrate` CLI and
  the `--dual-build` path, the other way to migrate incrementally
- [WinForms interop reference](https://github.com/majorsilence/Majorsilence.Forms/blob/main/docs/winforms-interop.md)
- [Documentation](https://forms.majorsilence.com)
- [Repository](https://github.com/majorsilence/Majorsilence.Forms)

Licensed under the MIT License.
