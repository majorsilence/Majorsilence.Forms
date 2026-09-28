# Majorsilence.Forms.Theming.Avalonia

Applies a [Majorsilence.Forms CSS theme](https://github.com/majorsilence/Majorsilence.Forms/blob/main/docs/theming.md)
to **native Avalonia controls** (Fluent theme), so an app that mixes Avalonia views with
Majorsilence.Forms surfaces — or an Avalonia shell next to a WinForms client themed by
`Majorsilence.Forms.Theming.WinForms` — is themed from one stylesheet: same tokens, same selectors,
same diagnostics.

```csharp
using Majorsilence.Forms.Theming.Avalonia;

// Once the Avalonia Application exists (e.g. in OnFrameworkInitializationCompleted):
AvaloniaCssTheme.Apply (File.ReadAllText ("Themes/brand.css"));   // throws ThemeCssException on errors

// Or keep re-applying while you edit the file:
using var watch = AvaloniaCssTheme.Watch ("Themes/brand.css");

// What could not be expressed on Avalonia (never silently ignored):
foreach (var d in AvaloniaCssTheme.Diagnostics)
    Console.WriteLine (d);
```

Colours are written as the Fluent theme resources the control themes read (`ButtonBackground`,
`ButtonBackgroundPointerOver`, `TabItemHeaderSelectedPipeFill`, the DataGrid selection brushes, ...),
so hover and selection states follow; geometry and fonts are setters in a generated `Styles`. Every
token and author variable of the sheet is also published under its PascalCase name
(`--line2` → `{DynamicResource Line2}`) for your own views, and the theme variant follows the
background.

The property × control support matrix (native / approximate / unsupported) is `AvaloniaThemeSupport`
and is published as
[docs/theming-avalonia.md](https://github.com/majorsilence/Majorsilence.Forms/blob/main/docs/theming-avalonia.md).
