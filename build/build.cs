#:sdk Cake.Sdk

// The build pipeline: what CI and a release do, as targets you can also run on a laptop.
//
//   dotnet build/build.cs -- --target=<Target> [--option=value ...]
//
// The Cake.Sdk version is pinned in /global.json ("msbuild-sdks"), where Dependabot tracks it.
// build/Directory.*.props|targets stop the repo-root MSBuild files applying to this script.
//
// The GitHub workflows keep the things only Actions can do (triggers, runner matrix, setup-dotnet, artifact
// upload/download, the nuget.org OIDC login, the draft Release, the emulator runner) and call into this file
// for everything else. Each workflow step names a target; the tables below are the single copy of what used
// to be pasted across dotnet.yml, release.yml and publish-nuget.yml.
//
// Targets
//   Info                  what this machine would do (OS, version, pack set)
//   Version               resolve the release version; writes version/is_release to $GITHUB_OUTPUT
//   Workloads             install a named workload profile (--workloads=<profile>)
//   Fonts                 CJK + colour-emoji faces on Linux (the text tests need a face to fall back TO)
//   Build                 build the whole solution
//   Test                  run a named test shape (--shape=<shape>)
//   ApiGap                the API-gap gate (ApiDiff --check)
//   Benchmarks            benchmark smoke run + allocation gate
//   BenchmarkRun          full BenchmarkDotNet run (benchmarks.yml)
//   NetFxSmoke            net472 consumer smoke test (Windows)
//   AotSmoke              NativeAOT publish + run (Linux)
//   Template              pack/install the dotnet-new template, scaffold two apps, build them
//   WasmPublish           publish Gallery.Wasm with wasm-tools linking
//   AndroidPublish        publish Gallery.Android (APK + AAB)
//   IosBuild              build Gallery.iOS for the simulator
//   Pack                  pack this OS's share of the shipping NuGet packages
//   SampleWasm | SampleAndroid | SampleIos | SampleThemeStudio | SampleThemeStudioWinForms
//                         the runnable sample heads, zipped for the GitHub Release (into ./sample-out)

var target = Argument ("target", "Info");
var configuration = Argument ("configuration", "Release");
var shape = Argument ("shape", "default");
// Substring filter over the pack set, so a laptop can pack one project instead of the whole set.
var only = Argument ("only", "");
// Overrides the test project/solution a shape runs against (a laptop can run one project's tests).
var testProject = Argument ("test-project", "Majorsilence.Forms.slnx");
var workloadsArg = Argument ("workloads", "");
var rid = Argument ("rid", "");
// A workflow_dispatch input reaches this as an env var rather than being interpolated into a shell line.
var explicitVersion = Argument ("pack-version", EnvironmentVariable ("PACK_VERSION") ?? "");
var benchFilter = Argument ("bench-filter", EnvironmentVariable ("BENCH_FILTER") ?? "*");
var benchJob = Argument ("bench-job", EnvironmentVariable ("BENCH_JOB") ?? "default");

var nupkgDir = Directory ("./nupkg");
var sampleOutDir = Directory ("./sample-out");

// ── Packable projects ──────────────────────────────────────────────────────────────────────────────────
// THE list. Adding a consumer-facing project means adding it here and nowhere else (CLAUDE.md).
//
// Each OS packs its own share, and the three shares together are the release:
//  - Windows-only projects (net*-windows, UseWindowsForms/UseWPF) compile to an empty IsPackable=false
//    placeholder off Windows, so they can only be packed there.
//  - Packages carrying a net10.0-ios row need Apple's toolchain, so they pack on macOS. Essentials and
//    Avalonia each ship android + ios + desktop (+ browser, for Avalonia) in ONE package, so they are packed
//    ONLY there; a second pack of the same id would collide on nuget.org. Packing Avalonia on Linux once
//    shipped every release without its net10.0-ios asset, so an iOS head silently got the desktop build.
string[] crossPlatformProjects = [
    "src/Majorsilence.Forms/Majorsilence.Forms.csproj",
    "src/Majorsilence.Forms.Theming.Avalonia/Majorsilence.Forms.Theming.Avalonia.csproj",
    "src/Majorsilence.Forms.Headless/Majorsilence.Forms.Headless.csproj",
    "src/Majorsilence.Forms.Terminal/Majorsilence.Forms.Terminal.csproj",
    "src/Majorsilence.Forms.Mvvm/Majorsilence.Forms.Mvvm.csproj",
    "src/Majorsilence.Forms.Uno/Majorsilence.Forms.Uno.csproj",
    "src/Majorsilence.Forms.Gtk4/Majorsilence.Forms.Gtk4.csproj",
    "src/Majorsilence.Forms.Telerik/Majorsilence.Forms.Telerik.csproj",
    "src/Majorsilence.Forms.Drawing.Common/Majorsilence.Forms.Drawing.Common.csproj",
    "src/Majorsilence.Forms.WebDriver/Majorsilence.Forms.WebDriver.csproj",
    "src/Majorsilence.Forms.Wpf/Majorsilence.Forms.Wpf.csproj",
    "tools/Majorsilence.Forms.Templates/Majorsilence.Forms.Templates.csproj",
    "tools/Majorsilence.Forms.Migrator/Majorsilence.Forms.Migrator.csproj",
    "tools/Majorsilence.Forms.Mcp/Majorsilence.Forms.Mcp.csproj",
    "src/Majorsilence.Forms.WinFormsShims.Compat/Majorsilence.Forms.WinFormsShims.Compat.csproj",
];
string[] windowsProjects = [
    "src/Majorsilence.Forms.WinForms/Majorsilence.Forms.WinForms.csproj",
    "src/Majorsilence.Forms.WindowsFormsInterop/Majorsilence.Forms.WindowsFormsInterop.csproj",
    "src/Majorsilence.Forms.WindowsUIAutomation/Majorsilence.Forms.WindowsUIAutomation.csproj",
    "src/Majorsilence.Forms.Theming.WinForms/Majorsilence.Forms.Theming.WinForms.csproj",
];
string[] macProjects = [
    "src/Majorsilence.Forms.Essentials/Majorsilence.Forms.Essentials.csproj",
    "src/Majorsilence.Forms.Avalonia/Majorsilence.Forms.Avalonia.csproj",
];

string[] ProjectsForThisOs () =>
    IsRunningOnWindows () ? windowsProjects :
    IsRunningOnMacOs () ? macProjects :
    crossPlatformProjects;

// ── Workload profiles ──────────────────────────────────────────────────────────────────────────────────
// A workload belongs to the SDK that runs the build, not to the project's target framework, which is why
// several of these name more than one id or pin a version. `dotnet workload install` arguments, verbatim.
var workloadProfiles = new Dictionary<string, string[]>
{
    // Also what Majorsilence.Forms.Theming.Avalonia needs to build against Majorsilence.Forms.Avalonia.
    ["android"] = ["android"],

    // Pinned to workload set 10.0.401 (iOS 26.5.10318). Set 10.0.401.1 brought .NET for iOS 27.0, which
    // refuses to build without Xcode 27, and the macos runner image still ships Xcode 26.6 -- every iOS build
    // went red on 2026-09-29 with no code change. Drop the pin once the runner image carries Xcode 27 (the
    // build error names the version it wants).
    ["ios"] = ["ios", "--version", "10.0.401"],

    // SDK 11 with its own workload set, pinned to 11.0.100.1: verified on macos-latest (Xcode 26.6) to build
    // the net10.0-ios row against Microsoft.iOS.Sdk.net10.0_26.0, which SDK 11's set carries for .NET 10
    // targets, while adding the net11.0 row. The pin is for the same reason the 10.0.401 one is: an unpinned
    // set once moved .NET for iOS to a version that demands an Xcode the runner image lacks, and every iOS
    // build went red overnight. Move it deliberately, with the runner's Xcode in view. android and ios go in
    // together so the Essentials and Avalonia packages carry both rows.
    ["mac-pack"] = ["android", "ios", "--version", "11.0.100.1"],

    // Both ids: under SDK 11, plain `wasm-tools` brings only the net11 packs, and Gallery.Wasm targets
    // net10.0-browser, which needs `wasm-tools-net10`. The runner image began shipping SDK 11 on 2026-11-11
    // and it wins over the 10.0.x installed alongside it, so the wasm job went red with no code change: the
    // publish "succeeded" without linking libSkiaSharp.a and the app died at boot.
    ["wasm"] = ["wasm-tools", "wasm-tools-net10"],

    // The template smoke scaffolds an Android + WASM app.
    ["template"] = ["android", "wasm-tools"],
};

// Windows-latest ships the android workload preinstalled for SDK 10, and that pack being on disk is what
// Directory.Build.props keys EnableMobileHeads on, so the solution build compiles Gallery.Android and
// Majorsilence.Forms.Avalonia's net10.0-android row. With SDK 11 installed alongside, the build runs on SDK 11,
// which has no android workload of its own and fails with NETSDK1147. Installing it keeps that row compiled on
// Windows instead of switching it off. Only done on the Actions runner: it is a fix for that runner image, and
// a local `--target=Build` should never install a workload on a developer's machine unasked.
string EffectiveWorkloads ()
{
    if (workloadsArg != "" && workloadsArg != "none") return workloadsArg;
    if (workloadsArg == "" && target == "Build" && IsRunningOnWindows () && EnvironmentVariable ("GITHUB_ACTIONS") == "true")
        return "android";
    return "";
}

// ── Version ────────────────────────────────────────────────────────────────────────────────────────────
// Tag push (v1.2.3 or 1.2.3; the repo has used both spellings) -> a release; otherwise an explicit
// --pack-version / $PACK_VERSION overrides; otherwise empty, meaning "whatever the csproj files declare".
bool IsTagPush () =>
    EnvironmentVariable ("GITHUB_EVENT_NAME") == "push" && EnvironmentVariable ("GITHUB_REF_TYPE") == "tag";

string ResolveVersion ()
{
    if (IsTagPush ())
    {
        var name = EnvironmentVariable ("GITHUB_REF_NAME") ?? "";
        return name.StartsWith ('v') ? name[1..] : name;
    }
    return explicitVersion;
}

string VersionSuffix () => ResolveVersion () is { Length: > 0 } v ? "-" + v : "";

// ── Test shapes ────────────────────────────────────────────────────────────────────────────────────────
// Two things vary independently and both have bitten this repo, so both are gated on every platform that can
// express them:
//   CHROME: macOS is the ONLY platform with system decorations; every other one draws the library's own title
//           bar. MF_FORCE_CUSTOM_CHROME makes a macOS process take the other path.
//   SCALE:  MF_HEADLESS_SCALE makes the headless backend report a scaled display, so logical->device
//           conversions are exercised instead of being identity as they are at scale 1.
// Every shape is the whole solution EXCEPT the migrator suite, which takes over ten minutes (it builds
// generated projects) against roughly eight seconds for everything else; only `full` includes it. Scaled shapes
// run collections serially: a test that opens a modal dialog picks its owner out of the global
// Application.OpenForms and in parallel can pick another test's window and block forever.
var shapes = new Dictionary<string, TestShape>
{
    ["default"] = new (),
    ["full"] = new () { ExcludeMigrator = false },
    ["scale2"] = new () { Env = { ["MF_HEADLESS_SCALE"] = "2" }, Serial = true },
    ["custom-chrome"] = new () { Env = { ["MF_FORCE_CUSTOM_CHROME"] = "1" } },
    ["custom-chrome-scale2"] = new () { Env = { ["MF_FORCE_CUSTOM_CHROME"] = "1", ["MF_HEADLESS_SCALE"] = "2" }, Serial = true },

    // The runs above test the net10.0 build. The net11.0 rows are compiled with runtime async (see
    // Directory.Build.targets), which changes the IL emitted for every await: no state machine, different
    // stack traces, different continuation behaviour. Only running the suite on net11.0 exercises that code.
    // -p:_TargetFramework moves just the test project; the libraries it references resolve their own net11.0
    // row. It builds on its own because the earlier build produced no net11.0 test output.
    ["net11"] = new () { Project = "tests/Majorsilence.Forms.Tests/Majorsilence.Forms.Tests.csproj", NoBuild = false, TestFramework = "net11.0" },
};

// ── Helpers ────────────────────────────────────────────────────────────────────────────────────────────
// A command whose arguments are passed through exactly as written (no Cake escaping), for the few that are
// stranger than an alias settings object can say. Fails the target on a non-zero exit.
void Run (string exe, string args, string workingDirectory = null)
{
    var settings = new ProcessSettings { Arguments = new ProcessArgumentBuilder ().Append (args) };
    if (workingDirectory != null) settings.WorkingDirectory = workingDirectory;
    var code = StartProcess (exe, settings);
    if (code != 0) throw new CakeException ($"'{exe} {args}' exited with code {code}.");
}

// Collapsible log group in the Actions UI; plain text anywhere else.
void Group (string name, Action body)
{
    var onActions = EnvironmentVariable ("GITHUB_ACTIONS") == "true";
    if (onActions) System.Console.WriteLine ($"::group::{name}");
    try { body (); }
    finally { if (onActions) System.Console.WriteLine ("::endgroup::"); }
}

// zip(1) on Unix, because it records the executable bit and a self-contained ThemeStudio's launcher needs it;
// ZipFile on Windows, where there is no zip and the bit means nothing (what Compress-Archive did).
void ZipDirectory (DirectoryPath dir, FilePath zip)
{
    var absZip = MakeAbsolute (zip).FullPath;
    EnsureDirectoryExists (MakeAbsolute (zip).GetDirectory ());
    if (FileExists (absZip)) DeleteFile (absZip);
    if (IsRunningOnWindows ())
        System.IO.Compression.ZipFile.CreateFromDirectory (MakeAbsolute (dir).FullPath, absZip);
    else
        Run ("zip", $"-r -q \"{absZip}\" .", MakeAbsolute (dir).FullPath);
    Information ($"zipped {dir} -> {zip}");
}

DirectoryPath FindDirectory (string glob)
{
    var found = GetDirectories (glob).FirstOrDefault ();
    return found ?? throw new CakeException ($"Nothing matched '{glob}'.");
}

// ── Setup ──────────────────────────────────────────────────────────────────────────────────────────────
Task ("Info")
    .Does (() =>
{
    Information ($"OS:        {(IsRunningOnWindows () ? "Windows" : IsRunningOnMacOs () ? "macOS" : "Linux")}");
    Information ($"Version:   '{ResolveVersion ()}' (empty = from csproj), release tag push: {IsTagPush ()}");
    Information ($"Workloads: '{EffectiveWorkloads ()}'");
    Information ($"Pack set:  {ProjectsForThisOs ().Length} projects");
    foreach (var p in ProjectsForThisOs ())
        Information ($"  {p}");
});

Task ("Version")
    .Does (() =>
{
    var version = ResolveVersion ();
    Information ($"version='{version}' is_release={IsTagPush ()}");
    // Consumed by release.yml's release job; harmless anywhere else.
    if (EnvironmentVariable ("GITHUB_OUTPUT") is { Length: > 0 } outputFile)
        System.IO.File.AppendAllText (outputFile, $"version={version}\nis_release={IsTagPush ().ToString ().ToLowerInvariant ()}\n");
});

Task ("Workloads")
    .WithCriteria (() => EffectiveWorkloads () != "", "no workload profile requested")
    .Does (() =>
{
    var profile = EffectiveWorkloads ();
    if (!workloadProfiles.TryGetValue (profile, out var ids))
        throw new CakeException ($"Unknown --workloads '{profile}'. Known: {string.Join (", ", workloadProfiles.Keys)}.");
    Run ("dotnet", $"workload install {string.Join (" ", ids)}");
});

// The CJK font-fallback tests (DrawString / AddString / TextRenderer) assert that a non-Latin string renders as
// real glyphs rather than a row of identical tofu boxes. That can only work if a CJK face is installed for
// SkiaSharp to fall back TO: the Windows and macOS runners ship one, ubuntu-latest ships none.
// fonts-noto-color-emoji is the same requirement for EmojiVariationSelectorTests: it asserts a VARIATION
// SELECTOR-16 sequence renders with real colour ink, which needs a colour emoji face for MatchCharacter
// (hinted "und-Zsye") to find. The text benchmarks need the same faces to resolve what they do in the tests.
Task ("Fonts")
    .WithCriteria (() => IsRunningOnLinux (), "fonts are only installed on Linux")
    .Does (() =>
{
    Run ("sudo", "apt-get update");
    Run ("sudo", "apt-get install -y fonts-noto-cjk fonts-noto-color-emoji");
});

// ── Build / test / gates ───────────────────────────────────────────────────────────────────────────────
// The cross-platform compile gate: Release also turns on TreatWarningsAsErrors + doc generation.
Task ("Build")
    .IsDependentOn ("Workloads")
    .Does (() =>
{
    DotNetBuild ("Majorsilence.Forms.slnx", new DotNetBuildSettings { Configuration = configuration });
});

Task ("Test")
    .Does (() =>
{
    if (!shapes.TryGetValue (shape, out var s))
        throw new CakeException ($"Unknown --shape '{shape}'. Known: {string.Join (", ", shapes.Keys)}.");

    Information ($"Test shape '{shape}'");
    var settings = new DotNetTestSettings
    {
        Configuration = configuration,
        NoBuild = s.NoBuild,
        Filter = s.ExcludeMigrator ? "FullyQualifiedName!~Migrator" : null,
        EnvironmentVariables = new Dictionary<string, string> (s.Env),
        ArgumentCustomization = a => s.Serial ? a.Append ("--").Append ("xunit.parallelizeTestCollections=false") : a,
    };
    if (s.TestFramework != null)
        settings.MSBuildSettings = new DotNetMSBuildSettings ().WithProperty ("_TargetFramework", s.TestFramework);

    DotNetTest (s.Project ?? testProject, settings);
});

// Compares both reimplemented surfaces -- Majorsilence.Forms.Drawing against System.Drawing.Common, and
// Majorsilence.Forms against System.Windows.Forms -- and fails if either gap set grew since its committed
// baseline. Reflection-only, so it runs anywhere despite both upstream assemblies being Windows-only at
// runtime. Closing gaps never fails the build; it prints them and asks for the baseline to be regenerated.
// See docs/gdi-gap-plan.md and docs/winforms-gap-plan.md.
Task ("ApiGap")
    .Does (() =>
{
    Run ("dotnet", $"run --project tools/Majorsilence.Forms.ApiDiff --configuration {configuration} --no-build -- --check");
});

// benchmarks/Majorsilence.Forms.Benchmarks is in the solution, so Build already compiles it; this proves every
// benchmark still RUNS (BenchmarkDotNet's Dry job: one invocation each, no statistics), then runs the
// allocation gate: the benchmarks tagged [BenchmarkCategory ("AllocGate")] must not allocate more per
// operation than AllocationBaseline.txt records (see AllocationGate.cs). Allocated bytes are the same run to
// run; timings are not, and are never gated here. Linux only in CI: the baseline is recorded on Linux, and font
// paths can allocate differently elsewhere.
const string benchmarksProject = "benchmarks/Majorsilence.Forms.Benchmarks/Majorsilence.Forms.Benchmarks.csproj";
Task ("Benchmarks")
    .Does (() =>
{
    DotNetBuild (benchmarksProject, new DotNetBuildSettings { Configuration = configuration });
    Run ("dotnet", $"run --configuration {configuration} --no-build --project {benchmarksProject} -- --filter '*' --job dry");
    Run ("dotnet", $"run --configuration {configuration} --no-build --project {benchmarksProject} -- --alloc-gate");
});

// Full BenchmarkDotNet run, on demand, so timings come from one consistent kind of machine.
Task ("BenchmarkRun")
    .Does (() =>
{
    Run ("dotnet", $"run --configuration {configuration} --project {benchmarksProject} -- --filter \"{benchFilter}\" --job \"{benchJob}\"");
});

// .NET Framework (net472) consumer smoke test. Majorsilence.Forms / .Telerik ship a netstandard2.0 target so
// classic .NET Framework 4.7.2+ apps can consume the core; tests/Majorsilence.Forms.NetFxSmoke exercises the
// netstandard2.0-only code paths -- most importantly ComponentResourceManager's AppDomain.AssemblyResolve
// fallback. Deliberately NOT in the solution: `dotnet test` on it would abort on the Linux/macOS legs (no mono
// host for net472).
Task ("NetFxSmoke")
    .WithCriteria (() => IsRunningOnWindows (), "net472 needs Windows")
    .Does (() =>
{
    DotNetTest ("tests/Majorsilence.Forms.NetFxSmoke/Majorsilence.Forms.NetFxSmoke.csproj",
        new DotNetTestSettings { Configuration = configuration });
});

// NativeAOT smoke. IsAotCompatible on core + Drawing.Common + Avalonia + Headless makes the Release build fail
// on a new statically-visible trim/AOT hazard, but the analyzers can't prove a real ILC publish links and runs:
// a reflection call deep in a dependency, or a trimmed static cctor that throws, only shows up here.
// tests/Majorsilence.Forms.AotSmoke renders a Form to a PNG on the Headless backend; the published native binary
// exits non-zero if anything in that path breaks. Not in the solution, same as NetFxSmoke.
Task ("AotSmoke")
    .WithCriteria (() => IsRunningOnLinux (), "the smoke is a linux-x64 publish")
    .Does (() =>
{
    // ILC emits object files and hands them to clang; it is preinstalled on ubuntu-latest, but naming it as a
    // dependency makes a runner-image change that drops it fail loudly here.
    Run ("clang", "--version");
    DotNetPublish ("tests/Majorsilence.Forms.AotSmoke/Majorsilence.Forms.AotSmoke.csproj",
        new DotNetPublishSettings { Configuration = configuration, Runtime = "linux-x64" });
    Run (MakeAbsolute (File ($"tests/Majorsilence.Forms.AotSmoke/bin/{configuration}/net10.0/linux-x64/publish/Majorsilence.Forms.AotSmoke")).FullPath, "");
});

// Template smoke: pack Majorsilence.Forms.Templates, install it, scaffold a project the two supported ways
// (desktop-only, and with the Android + WASM heads) and build them. The scaffolded app restores the *published*
// Majorsilence.Forms packages from nuget.org (the version pinned in template.json), not this branch's build.
Task ("Template")
    .IsDependentOn ("Workloads")
    .Does (() =>
{
    var root = MakeAbsolute (Directory (System.IO.Path.Combine (EnvironmentVariable ("RUNNER_TEMP") ?? System.IO.Path.GetTempPath (), "mf-template-smoke")));
    CleanDirectory (root);
    var pkgDir = root.Combine ("tpl-nupkg");

    DotNetPack ("tools/Majorsilence.Forms.Templates/Majorsilence.Forms.Templates.csproj",
        new DotNetPackSettings { Configuration = configuration, OutputDirectory = pkgDir });
    var pkg = GetFiles ($"{pkgDir}/Majorsilence.Forms.Templates.*.nupkg").Single ();
    Run ("dotnet", $"new install \"{pkg}\"");

    var plain = root.Combine ("plain");
    Run ("dotnet", $"new majorsilenceforms -o \"{plain}\" -n Smoke.App");
    DotNetBuild (plain.CombineWithFilePath ("Smoke.App.slnx").FullPath);

    var mobile = root.Combine ("mobile");
    Run ("dotnet", $"new majorsilenceforms -o \"{mobile}\" -n Smoke.App --IncludeAndroid --IncludeWasm");
    DotNetBuild (mobile.CombineWithFilePath ("Smoke.App.slnx").FullPath);
});

// ── Mobile / web heads ─────────────────────────────────────────────────────────────────────────────────
// `dotnet build` never touches wasm-tools (emcc/wasm-opt native linking, IL trimming, compression of the
// _framework assets); `dotnet publish` does. WasmBuildNative is what publish defaults to when the workload is
// present; stating it turns a missing workload from a warning ("native references won't be linked in") into an
// error. The bundle lands in samples/Gallery.Wasm/bin/<cfg>/net10.0-browser/publish/wwwroot, which the
// workflow's browser smoke steps serve.
Task ("WasmPublish")
    .IsDependentOn ("Workloads")
    .Does (() =>
{
    DotNetPublish ("samples/Gallery.Wasm/Gallery.Wasm.csproj", new DotNetPublishSettings
    {
        Configuration = configuration,
        MSBuildSettings = new DotNetMSBuildSettings ().WithProperty ("WasmBuildNative", "true"),
    });
});

// `publish` rather than `build` so it also runs aapt/zipalign/apksigner and produces the installable APK + AAB.
// With no release keystore these are signed with the .NET Android debug key -- sideloadable, not Play-Store
// uploadable.
//
// EnableAndroidTarget=true, not the EnableMobileHeads umbrella, and the difference is load-bearing in both
// directions: the umbrella also covers ios, whose workload is not installed here, and it is self-gated on the
// android workload being *detected*, so if that detection ever missed the job would quietly publish the empty
// stub and pass having built nothing. EnableAndroidTarget is the ungated force: no workload, no build, loudly
// (NETSDK1147).
Task ("AndroidPublish")
    .IsDependentOn ("Workloads")
    .Does (() =>
{
    DotNetPublish ("samples/Gallery.Android/Gallery.Android.csproj", new DotNetPublishSettings
    {
        Configuration = configuration,
        MSBuildSettings = new DotNetMSBuildSettings ().WithProperty ("EnableAndroidTarget", "true"),
        // %3B is MSBuild's escaped ';'. Appended raw so Cake's own property escaping cannot touch it.
        ArgumentCustomization = a => a.Append ("-p:AndroidPackageFormats=apk%3Baab"),
    });
});

// `dotnet build` (not publish) with a simulator RID assembles the runnable .app bundle; iOS `publish` refuses a
// simulator RID (it targets device archives, which need an Apple cert). EnableIOSTarget=true names the one
// platform this runner has a workload for; the EnableMobileHeads umbrella also switches the net10.0-android row
// on and dies with NETSDK1147 asking for a workload nobody installed.
Task ("IosBuild")
    .IsDependentOn ("Workloads")
    .Does (() =>
{
    DotNetBuild ("samples/Gallery.iOS/Gallery.iOS.csproj", new DotNetBuildSettings
    {
        Configuration = configuration,
        MSBuildSettings = new DotNetMSBuildSettings ()
            .WithProperty ("EnableIOSTarget", "true")
            .WithProperty ("RuntimeIdentifier", "iossimulator-arm64"),
    });
});

// ── Packages ───────────────────────────────────────────────────────────────────────────────────────────
Task ("Pack")
    .IsDependentOn ("Workloads")
    .Does (() =>
{
    var version = ResolveVersion ();
    EnsureDirectoryExists (nupkgDir);
    foreach (var project in ProjectsForThisOs ().Where (p => only == "" || p.Contains (only)))
    {
        Group ($"pack {project}", () =>
        {
            var msbuild = new DotNetMSBuildSettings ();
            if (version != "")
                msbuild.WithProperty ("Version", version).WithProperty ("PackageVersion", version);
            // The macOS share is the one that carries the mobile rows; name both ungated (see AndroidPublish).
            if (IsRunningOnMacOs ())
                msbuild.WithProperty ("EnableAndroidTarget", "true").WithProperty ("EnableIOSTarget", "true");
            DotNetPack (project, new DotNetPackSettings
            {
                Configuration = configuration,
                OutputDirectory = nupkgDir,
                MSBuildSettings = msbuild,
            });
        });
    }
    foreach (var file in GetFiles ($"{nupkgDir}/*.*nupkg"))
        Information ($"  {file.GetFilename ()}");
});

// ── Runnable samples (attached to the GitHub Release) ──────────────────────────────────────────────────
// None are code-signed for distribution: a static WASM site, a sideloadable Android APK/AAB, an iOS simulator
// .app, and self-contained ThemeStudio zips (no .NET runtime needed) so a reviewer can try the CSS theme editor
// (docs/samples.md) with no toolchain installed. File names carry the release version when there is one.
Task ("SampleWasm")
    .IsDependentOn ("Workloads")
    .Does (() =>
{
    var outDir = Directory ("./wasm-out");
    CleanDirectory (outDir);
    // Both the workload ids and WasmBuildNative for the reason given on WasmPublish. Nothing here boots the
    // site; the CI wasm job does that against the same publish.
    DotNetPublish ("samples/Gallery.Wasm/Gallery.Wasm.csproj", new DotNetPublishSettings
    {
        Configuration = configuration,
        OutputDirectory = outDir,
        MSBuildSettings = new DotNetMSBuildSettings ()
            .WithProperty ("CompressionEnabled", "false")
            .WithProperty ("WasmBuildNative", "true"),
    });
    ZipDirectory (outDir + Directory ("wwwroot"), sampleOutDir + File ($"gallery-wasm{VersionSuffix ()}.zip"));
});

Task ("SampleAndroid")
    .IsDependentOn ("AndroidPublish")
    .Does (() =>
{
    var built = Directory ($"samples/Gallery.Android/bin/{configuration}/net10.0-android");
    EnsureDirectoryExists (sampleOutDir);
    foreach (var ext in new[] { "apk", "aab" })
    {
        var file = GetFiles ($"{built}/*-Signed.{ext}").FirstOrDefault ()
            ?? throw new CakeException ($"No *-Signed.{ext} under {built}.");
        CopyFile (file, sampleOutDir + File ($"gallery-android{VersionSuffix ()}.{ext}"));
    }
});

// ditto, not a zip library: it keeps the bundle's resource forks and symlinks, which a simulator install needs.
Task ("SampleIos")
    .IsDependentOn ("IosBuild")
    .Does (() =>
{
    var app = FindDirectory ($"samples/Gallery.iOS/bin/{configuration}/**/Gallery.iOS.app");
    var zip = MakeAbsolute (sampleOutDir + File ($"gallery-ios-simulator{VersionSuffix ()}.zip"));
    EnsureDirectoryExists (zip.GetDirectory ());
    Run ("ditto", $"-c -k --sequesterRsrc --keepParent \"{MakeAbsolute (app)}\" \"{zip}\"");
});

// -p:EnableMobileHeads=false: windows-latest ships the android workload's SDK pack preinstalled, which
// auto-adds a net10.0-android row to Majorsilence.Forms.Avalonia (Directory.Build.props' EnableAndroidHead
// detection). That is fine for a plain `dotnet build`, but a self-contained `dotnet publish -r win-x64` then
// tries to restore a Mono runtime pack for that TFM against the win-x64 RID, which does not exist (NU1102:
// Microsoft.NETCore.App.Runtime.Mono.win-x64). ThemeStudio never targets android, so force the row off
// explicitly rather than relying on runner image contents.
Task ("SampleThemeStudio")
    .Does (() =>
{
    if (rid == "") throw new CakeException ("SampleThemeStudio needs --rid=<win-x64|linux-x64|osx-arm64>.");
    var outDir = Directory ($"./themestudio-out/{rid}");
    CleanDirectory (outDir);
    DotNetPublish ("samples/ThemeStudio/ThemeStudio.csproj", new DotNetPublishSettings
    {
        Configuration = configuration,
        Runtime = rid,
        SelfContained = true,
        OutputDirectory = outDir,
        MSBuildSettings = new DotNetMSBuildSettings ().WithProperty ("EnableMobileHeads", "false"),
    });
    ZipDirectory (outDir, sampleOutDir + File ($"themestudio-{rid}{VersionSuffix ()}.zip"));
});

// ThemeStudio.WinForms only builds and runs on Windows (real System.Windows.Forms, via
// Majorsilence.Forms.Theming.WinForms): one RID.
Task ("SampleThemeStudioWinForms")
    .WithCriteria (() => IsRunningOnWindows (), "real System.Windows.Forms needs Windows")
    .Does (() =>
{
    var outDir = Directory ("./themestudio-winforms-out");
    CleanDirectory (outDir);
    DotNetPublish ("samples/ThemeStudio.WinForms/ThemeStudio.WinForms.csproj", new DotNetPublishSettings
    {
        Configuration = configuration,
        Runtime = "win-x64",
        SelfContained = true,
        OutputDirectory = outDir,
    });
    ZipDirectory (outDir, sampleOutDir + File ($"themestudio-winforms-win-x64{VersionSuffix ()}.zip"));
});

RunTarget (target);

// Declared after the top-level statements, as C# requires for types in a file-based app.
class TestShape
{
    public Dictionary<string, string> Env { get; init; } = new ();
    public bool Serial { get; init; }
    public bool ExcludeMigrator { get; init; } = true;
    public bool NoBuild { get; init; } = true;
    public string Project { get; init; }
    public string TestFramework { get; init; }
}
