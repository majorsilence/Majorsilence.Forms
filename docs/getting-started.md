## Getting Started with Majorsilence.Forms

## From Template

The easiest way to get started creating a Majorsilence.Forms application is with our `dotnet` template available from NuGet.

To install and run:
```
dotnet new --install MajorsilenceForms.Templates
dotnet new majorsilenceforms
dotnet run
```

This will run create and run a basic Hello World Majorsilence.Forms application.

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
    <TargetFramework>net6.0</TargetFramework>
    <Nullable>enable</Nullable>
</PropertyGroup>
```

Add a NuGet reference to `Majorsilence.Forms`:
```
<ItemGroup>
    <PackageReference Include="Majorsilence.Forms" Version="0.2.0" />
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