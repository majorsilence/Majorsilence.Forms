## Getting Started with Continuum.Forms

## From Template

The easiest way to get started creating a Continuum.Forms application is with our `dotnet` template available from NuGet.

To install and run:
```
dotnet new --install ContinuumForms.Templates
dotnet new continuumforms
dotnet run
```

This will run create and run a basic Hello World Continuum.Forms application.

There isn't documentation available yet, but the API should be relatively familiar for developers with Windows.Forms
experience.  A good resource is to look at the source code of our sample applications:
* [ControlGallery](https://github.com/modern-forms/Continuum.Forms/tree/main/samples/ControlGallery)
* [Explore](https://github.com/modern-forms/Continuum.Forms/tree/main/samples/Explorer)
* [ModernDecompile](https://github.com/modern-forms/ModernDecompile/tree/main/src)

## From Scratch

To turn a regular .NET Core Console Application into a Continuum.Forms application, make the following changes.

#### Project File

Ensure the following properties are set:
```
<PropertyGroup>
    <OutputType>WinExe</OutputType>
    <TargetFramework>net6.0</TargetFramework>
    <Nullable>enable</Nullable>
</PropertyGroup>
```

Add a NuGet reference to `Continuum.Forms`:
```
<ItemGroup>
    <PackageReference Include="Continuum.Forms" Version="0.2.0" />
</ItemGroup>
```

#### Empty Form

Create an empty Form class:
```csharp
using Continuum.Forms;

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