# WinFormsInterop sample

Demonstrates bi-directional interop between `System.Windows.Forms` and Majorsilence.Forms
(Avalonia-backed) in a single process.

**Windows-only.** Run with:

```bash
dotnet run --project samples/WinFormsInterop
```

---

## What you will see

The sample starts as a **real WinForms application** (`WinFormsShellForm`). From there you can:

| Button | What it demonstrates |
|---|---|
| Open Majorsilence.Forms window (modeless) | **Direction B** — WF opens an MF window; returns immediately |
| Open Majorsilence.Forms window (modal) | **Direction B** — WF opens an MF window; blocks until closed, prints `DialogResult` |

Each Majorsilence.Forms window (`SpikeForm`) has its own buttons:

| Button | What it demonstrates |
|---|---|
| Open legacy WinForms form (modeless) | **Direction A** — MF opens a WF form; returns immediately |
| Open legacy WinForms form (modal) | **Direction A** — MF opens a WF form; blocks until closed, prints `DialogResult` |
| Close (OK) / Close (Cancel) | Sets `DialogResult` before closing so the WF shell can read it |

---

## Key files

| File | Role |
|---|---|
| `Program.cs` | WinForms entry point; calls `WindowsFormsInterop.InitializeMajorsilence()` |
| `WinFormsShellForm.cs` | Real `System.Windows.Forms.Form` shell (Direction B host) |
| `SpikeForm.cs` | Majorsilence.Forms window opened by the shell; opens WF forms (Direction A) |
| `SampleLegacyForm.cs` | Real `System.Windows.Forms.Form` opened from within `SpikeForm` |

---

## How it works

On Windows, Avalonia's Win32 backend shares the existing Win32 message pump with WinForms.
`WindowsFormsInterop.InitializeMajorsilence()` initializes the Avalonia platform without
starting a separate loop — a single `WF.Application.Run` services both toolkits.

Modal dialogs in both directions use nested Win32/Avalonia dispatcher frames, the same
mechanism both frameworks use internally for their own `ShowDialog` calls.

See [docs/winforms-interop.md](../../docs/winforms-interop.md) for full API reference.
