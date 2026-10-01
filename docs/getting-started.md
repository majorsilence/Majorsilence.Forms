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

A control's own `OnPaint` receives a canvas in **device pixels**, not the logical units everything
else in the framework uses (`Left`, `Top`, `Width`, `Height`, `MouseEventArgs.X`/`Y`). The built-in
renderers already scale explicitly when they paint; a custom `OnPaint` override has to do the same,
or its drawing comes out the wrong physical size on any scaled display -- a HiDPI desktop, and every
phone (Android reports a `Scaling` of roughly 2.6-2.75):

```csharp
protected override void OnPaint (PaintEventArgs e)
{
    var scale = (float)e.Scaling;
    e.Graphics.ScaleTransform (scale, scale);   // now draw in logical units

    e.Graphics.FillRectangle (Brushes.LimeGreen, 0, 0, 10, 10);   // a 10x10 LOGICAL square
}
```

Without the `ScaleTransform`, that same call draws a 10x10 rectangle in *device* pixels: correct at
scale 1, about a third of its intended size at Android's ~2.75x. `PaintEventArgs.Scaling` is the
factor to use; `Graphics.LogicalToDeviceUnits (...)` is there too, for the odd value that has to be
converted without going through a transform. `samples/ControlGallery/Panels/GameOfLifePanel.cs` is
this idiom in a complete custom control.

Anything a custom control computes *from* a point it was handed -- hit-testing in `OnMouseDown` or
`OnMouseMove`, for instance -- stays consistent for free: `MouseEventArgs.X`/`Y` arrive already
converted to the same logical units as `Left`/`Top`/`Width`/`Height`, so geometry built from those
and compared against a mouse point needs no separate conversion, as long as it is built in the same
logical units `OnPaint` now draws in.