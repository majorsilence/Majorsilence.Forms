# MajorsilenceForms.Templates

`dotnet new` templates for scaffolding a [Majorsilence.Forms](../../readme.md) application.

```
dotnet new --install MajorsilenceForms.Templates
dotnet new majorsilenceforms
dotnet run
```

`dotnet new majorsilenceforms -n MyApp` (optionally with `-o <dir>`) scaffolds into a named
project/namespace instead of using the current directory.

## What it contains

One template, `majorsilenceforms` (`content/majorsilenceforms/`): a `WinExe` project referencing
`Majorsilence.Forms` + `Majorsilence.Forms.Avalonia`, with an empty `Form` (`MainForm.cs` +
`MainForm.Designer.cs`, following the same designer-partial-class split as the samples) showing a
single "Hello, Majorsilence.Forms!" label, and a `Program.cs` calling `Application.Run`.

`sourceName` in `.template.config/template.json` is `MajorsilenceFormsApp` — the templating engine
replaces that token (namespace, file names, project name) with whatever `-n`/the target directory
name supplies.

## Maintenance

The template's `.csproj` **pins exact `Majorsilence.Forms`/`Majorsilence.Forms.Avalonia` package
versions** (unlike this repo's own projects, which use central package management against
in-repo project versions) — a freshly scaffolded app is a standalone project outside this
repo/solution, so it needs real, already-published NuGet versions to restore against. Bump those
two `PackageReference` versions when cutting a new release, and re-verify end to end:

```bash
dotnet pack tools/Majorsilence.Forms.Templates
dotnet new install nupkg/MajorsilenceForms.Templates.<version>.nupkg
dotnet new majorsilenceforms -o /tmp/msf-template-smoke-test
dotnet build /tmp/msf-template-smoke-test
```

This package itself follows the repo's own `Directory.Build.props` version (so its version tracks
Majorsilence.Forms releases), but that's independent from the pinned dependency versions inside
the template content, which must be bumped by hand.
