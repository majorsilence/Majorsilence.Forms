# Majorsilence.Forms.Theming.WinForms

Applies a [Majorsilence.Forms CSS theme](https://github.com/majorsilence/Majorsilence.Forms/blob/main/docs/theming.md)
to **real `System.Windows.Forms` controls**, so a mixed migration app — Majorsilence.Forms controls
embedded in a WinForms shell, or the reverse — is themed from one stylesheet: same tokens, same
selectors, same diagnostics.

```csharp
using Majorsilence.Forms.Theming.WinForms;

// Once, as early as possible (before the first form for the process-wide switches):
WinFormsCssTheme.Apply (File.ReadAllText ("Themes/graphite.css"));   // throws ThemeCssException on errors

// Or keep re-applying while you edit the file (the Theme Studio workflow):
using var watch = WinFormsCssTheme.Watch ("Themes/graphite.css");

// Forms constructed after the apply: style them and keep styling controls added later.
var form = new MainForm ();
WinFormsCssTheme.Track (form);
form.Show ();

// What could not be expressed on WinForms (never silently ignored):
foreach (var d in WinFormsCssTheme.Diagnostics)
    Console.WriteLine (d);
```

WinForms has no styling system to hook, so the applier walks the control tree and sets properties:
`BackColor` / `ForeColor` / `Font` everywhere, `FlatAppearance` on buttons, `DataGridView` cell styles,
a token-built `ToolStripProfessionalRenderer` for every menu, tool bar, status bar and context menu,
selection colours on ListBox / TreeView / ListView (selection-only owner draw, never on a control the
app already owner-draws), the `ProgressBar` fill, `Application.SetDefaultFont` / `SetColorMode`, and
the Windows 11 title bar via DWM. A control whose `BackColor` / `ForeColor` / `Font` the app set
explicitly keeps it.

The property × control support matrix (native / approximate / unsupported) is
`WinFormsThemeSupport` and is published as
[docs/theming-winforms.md](https://github.com/majorsilence/Majorsilence.Forms/blob/main/docs/theming-winforms.md).
