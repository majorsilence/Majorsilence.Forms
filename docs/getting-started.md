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