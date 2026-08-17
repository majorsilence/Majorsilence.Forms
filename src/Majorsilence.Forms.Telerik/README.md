# Majorsilence.Forms.Telerik

A **compatibility surface for Telerik UI for WinForms**, for apps migrating onto
[Majorsilence.Forms](https://www.nuget.org/packages/Majorsilence.Forms).

A WinForms app built on Telerik's `Rad*` controls normally can't move cross-platform without also
replacing every one of them. This package provides Majorsilence.Forms-native stand-ins under the same
type names and a familiar API shape, so that code keeps compiling and running.

> ⚠️ These are **compatible reimplementations, not Telerik**. They are not affiliated with or endorsed
> by Progress/Telerik, and they approximate behavior and appearance rather than matching pixel-for-pixel.

## Install

```bash
dotnet add package Majorsilence.Forms
dotnet add package Majorsilence.Forms.Telerik
dotnet add package Majorsilence.Forms.Avalonia   # or another backend
```

Types live under the `Majorsilence.Forms.Telerik` namespace, so migrating is largely an import change:

```diff
- using Telerik.WinControls.UI;
+ using Majorsilence.Forms.Telerik;
```

```csharp
var grid = new RadGridView { Left = 10, Top = 10, Width = 600, Height = 400 };
grid.DataSource = customers;
form.Controls.Add (grid);
```

## What's covered

Controls including `RadGridView`, `RadButton`, `RadCheckBox`, `RadCalendar`, `RadDropDownList`,
`RadCommandBar`, `RadCollapsiblePanel`, `RadScheduler`, `RadPdfViewer`, `RadRichTextEditor`,
`RadDesktopAlert`, and the grid export / scheduler data / printing surfaces around them.

Telerik's several source namespaces (`Telerik.WinControls.UI`, `.Enumerations`, `.UI.Docking`,
`.UI.Export`, `Telerik.WinForms.Documents.*`, ...) all collapse into this single flat namespace.

Some namespaces have no equivalent and are deliberately left alone — `Telerik.WinControls.Themes`,
`.Design`, `.Primitives`, `.Layouts`. Those need a human.

## Migrate automatically

The [`majorsilence-migrate`](https://github.com/majorsilence/Majorsilence.Forms/blob/main/MIGRATION.md)
CLI knows this mapping out of the box: it rewrites the Telerik namespaces, drops the Telerik UI for
WinForms NuGet packages, collapses the resulting duplicate imports, and flags anything with no
equivalent for manual review.

```bash
majorsilence-migrate MySolution.sln --dry-run --diff
```

## Links

- [**Documentation**](https://forms.majorsilence.com) · [Migration guide](https://forms.majorsilence.com/migration/)
- [Repository](https://github.com/majorsilence/Majorsilence.Forms)
- [Migration guide](https://github.com/majorsilence/Majorsilence.Forms/blob/main/MIGRATION.md)
- [Compatibility matrix](https://github.com/majorsilence/Majorsilence.Forms/blob/main/COMPATIBILITY_MATRIX.md) — what's real vs. approximated
- [Control gallery sample](https://github.com/majorsilence/Majorsilence.Forms/tree/main/samples/ControlGallery) — includes Telerik control panels

Licensed under the MIT License. Telerik and RadControls are trademarks of Progress Software Corporation.
