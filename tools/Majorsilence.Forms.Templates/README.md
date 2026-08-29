# Majorsilence.Forms.Templates

`dotnet new` template for scaffolding a
[Majorsilence.Forms](https://github.com/majorsilence/Majorsilence.Forms) application — a WinForms-style,
cross-platform UI framework for .NET that keeps `Form`s, controls, event handlers, and `*.Designer.cs`
files.

```
dotnet new install Majorsilence.Forms.Templates
dotnet new majorsilenceforms
dotnet run --project MajorsilenceFormsApp
```

That scaffolds a **solution with two projects** — a shared UI library (`MajorsilenceFormsApp.Shared`,
holding `MainForm` + `MainForm.Designer.cs`) and a desktop head (`MajorsilenceFormsApp`, a `WinExe` on
the Avalonia backend for Windows/macOS/Linux).

`dotnet new majorsilenceforms -n MyApp` (optionally `-o <dir>`) scaffolds into a named
project/namespace.

## Mobile and web heads

Add head projects for the other Avalonia targets with switches — each is a thin head over the same
`MajorsilenceFormsApp.Shared` UI:

```
dotnet new majorsilenceforms --IncludeAndroid --IncludeWasm --IncludeiOS
```

| Switch | Adds | Needs |
|---|---|---|
| `--IncludeAndroid` | `MajorsilenceFormsApp.Android` (`net10.0-android`) | `dotnet workload install android` |
| `--IncludeWasm` | `MajorsilenceFormsApp.Wasm` (`net10.0-browser`) | `dotnet workload install wasm-tools` (for `publish`) |
| `--IncludeiOS` | `MajorsilenceFormsApp.iOS` (`net10.0-ios`) | a Mac with `dotnet workload install ios` |

All default to **off** so `dotnet new majorsilenceforms` + `dotnet build` works with no extra
workload. Each included head is added to the `.slnx`. The iOS head is experimental — the
`Majorsilence.Forms.Avalonia` package does not yet ship a `net10.0-ios` asset, so it may not restore
against a released package.

## Options

| Option | Default | Purpose |
|---|---|---|
| `--msformsVersion <v>` | a recent release | `Majorsilence.Forms` / `Majorsilence.Forms.Avalonia` package version |
| `--avaloniaVersion <v>` | `12.1.1` | Avalonia platform-package version the mobile/web heads reference |

## Maintenance

The scaffolded projects **pin exact package versions** (a fresh app is a standalone project outside
this repo, so it needs already-published NuGet versions). Bump the `defaultValue` of `msformsVersion`
(and, if Avalonia moved, `avaloniaVersion`) in `.template.config/template.json` when cutting a release,
then re-verify:

```bash
dotnet pack tools/Majorsilence.Forms.Templates
# An absolute path is required: `dotnet new install` rejects a relative one (exit 106).
dotnet new install "$PWD/nupkg/Majorsilence.Forms.Templates.<version>.nupkg"
dotnet new majorsilenceforms -o /tmp/msf-smoke --IncludeAndroid --IncludeWasm
dotnet build /tmp/msf-smoke/*.slnx
dotnet new uninstall Majorsilence.Forms.Templates
```

CI (`.github/workflows/dotnet.yml`, `template` job) runs this on every PR.

The template package's own version tracks the repo's `Directory.Build.props`; that is independent of
the dependency versions inside the scaffolded content, which are the `template.json` defaults above.

## Links

- [Repository](https://github.com/majorsilence/Majorsilence.Forms)
- [Getting started](https://github.com/majorsilence/Majorsilence.Forms/blob/main/docs/getting-started.md)
- [Samples](https://github.com/majorsilence/Majorsilence.Forms/tree/main/samples)

Licensed under the MIT License.
