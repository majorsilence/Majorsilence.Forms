## Getting Started with Majorsilence.Forms

## From Template

The easiest way to get started creating a Majorsilence.Forms application is with our `dotnet` template available from NuGet.

To install and run:
```
dotnet new install Majorsilence.Forms.Templates
dotnet new majorsilenceforms
dotnet run --project MajorsilenceFormsApp
```

This scaffolds a solution with a shared UI library and a desktop head (Windows/macOS/Linux on the
Avalonia backend) showing a basic Hello World `MainForm`.

Add mobile and browser heads over the same shared UI with switches:
```
dotnet new majorsilenceforms --IncludeAndroid --IncludeWasm --IncludeiOS
```
Each needs its workload (`android`, `wasm-tools`, `ios`); all default to off. See the
[template README](../tools/Majorsilence.Forms.Templates/README.md).

There isn't documentation available yet, but the API should be relatively familiar for developers with Windows.Forms
experience.  A good resource is to look at the source code of our sample applications:
* [ControlGallery](../samples/ControlGallery)
* [Explore](../samples/Explorer)

## From Scratch

To turn a regular .NET Core Console Application into a Majorsilence.Forms application, make the following changes.

#### Project File

Ensure the following properties are set:
```
<PropertyGroup>
    <OutputType>WinExe</OutputType>
    <TargetFramework>net10.0</TargetFramework>
    <Nullable>enable</Nullable>
</PropertyGroup>
```

Add a NuGet reference to `Majorsilence.Forms` and a backend (`Majorsilence.Forms.Avalonia` for
desktop):
```
<ItemGroup>
    <PackageReference Include="Majorsilence.Forms" Version="26.0.33" />
    <PackageReference Include="Majorsilence.Forms.Avalonia" Version="26.0.33" />
</ItemGroup>
```

#### Empty Form

Create an empty Form class:
```csharp
using Majorsilence.Forms;

public class MainForm : Form
{
}
```

#### Program.cs
Call `Application.Run ()` with an instance of your Form:

```csharp
static void Main (string [] args)
{
    Application.Run (new MainForm ());
}
```

Your application should now be ready to run.

## Custom-painted controls

A control's `OnPaint` and `OnPaintBackground` overrides, and its `Paint` handlers, draw in **logical
units**, the same units as everything else in the framework: `Left`, `Top`, `Width`, `Height`,
`ClientRectangle`, `ClientSize` and `MouseEventArgs.X`/`Y`. The framework scales the canvas to the
display, so ordinary WinForms drawing code is the right size on a scaled display (a HiDPI desktop, or
any phone, where Android reports a `Scaling` of roughly 2.6-2.75) with no changes:

```csharp
protected override void OnPaint (PaintEventArgs e)
{
    base.OnPaint (e);

    e.Graphics.DrawRectangle (Pens.Gray, 0, 0, Width - 1, Height - 1);   // frames the control
    e.Graphics.FillRectangle (Brushes.LimeGreen, 0, 0, 10, 10);         // a 10x10 logical square
}
```

The same holds for `e.ClipRectangle`, and for `e.Canvas` if you draw with SkiaSharp directly.
`PaintEventArgs.Scaling` is still there, for code that wants to place something on an exact device
pixel. `samples/ControlGallery/Panels/GameOfLifePanel.cs` is a complete custom control.

Before 2026-10-01 this canvas was in device pixels, and a custom control had to call
`e.Graphics.ScaleTransform (e.Scaling, e.Scaling)` itself. Remove that call if you added it: the
drawing would now be scaled twice.

Hit-testing is consistent for free: `MouseEventArgs.X`/`Y` arrive in the same logical units as
`Width`/`Height` and the paint canvas, so geometry built once serves both.