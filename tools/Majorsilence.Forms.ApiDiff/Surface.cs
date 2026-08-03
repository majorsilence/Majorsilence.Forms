namespace Majorsilence.Forms.ApiDiff;

/// <summary>
/// One API surface this repo reimplements: the upstream assembly to compare against, how its
/// namespaces map onto ours, and which of our assemblies to look in.
/// </summary>
/// <param name="Name">Short name used on the command line and in the baseline filename.</param>
/// <param name="PackageRootKey">
/// The <c>AssemblyMetadata</c> key holding the NuGet package root of the upstream assembly, baked in
/// at build time (see the csproj).
/// </param>
/// <param name="UpstreamAssembly">File name of the upstream reference assembly inside that package.</param>
/// <param name="NamespaceMap">Upstream namespace to the namespace(s) we put its types in.</param>
/// <param name="ExcludedTypeNames">
/// Types deliberately not reimplemented, so they are not reported as gaps at all.
/// </param>
/// <param name="IncludeOverloads">
/// Whether to report missing overloads of methods that do exist. Worth it once a surface is close to
/// complete; on a surface with hundreds of wholly-missing members it would bury the signal.
/// </param>
internal sealed record Surface(
    string Name,
    string PackageRootKey,
    string UpstreamAssembly,
    Dictionary<string, string[]> NamespaceMap,
    HashSet<string> ExcludedTypeNames,
    bool IncludeOverloads)
{
    /// <summary>
    /// GDI+ — <c>System.Drawing.Common</c> against <c>Majorsilence.Forms.Drawing</c>. See
    /// docs/gdi-gap-plan.md; this surface is complete apart from documented non-goals, so overload
    /// checking is on.
    /// </summary>
    public static Surface Drawing { get; } = new(
        "drawing",
        "SystemDrawingCommonPackageRoot",
        "System.Drawing.Common.dll",
        new(StringComparer.Ordinal)
        {
            // System.Drawing has two targets: a few of its types (Graphics, SystemBrushes/Pens/Fonts,
            // the buffered-graphics trio) live in Majorsilence.Forms rather than the drawing package,
            // because they depend on the Forms layer and would otherwise be a circular reference.
            ["System.Drawing"] = ["Majorsilence.Forms.Drawing", "Majorsilence.Forms"],
            ["System.Drawing.Drawing2D"] = ["Majorsilence.Forms.Drawing.Drawing2D"],
            ["System.Drawing.Imaging"] = ["Majorsilence.Forms.Drawing.Imaging"],
            ["System.Drawing.Text"] = ["Majorsilence.Forms.Drawing.Text"],
            ["System.Drawing.Printing"] = ["Majorsilence.Forms.Printing"],
        },
        // The real cross-platform BCL System.Drawing.Primitives types, used as-is. Reimplementing them
        // would make every bare Point/Rectangle/Color ambiguous wherever both namespaces are imported.
        new(StringComparer.Ordinal) { "Color", "Point", "PointF", "Size", "SizeF", "Rectangle", "RectangleF", "KnownColor" },
        IncludeOverloads: true);

    /// <summary>
    /// WinForms — <c>System.Windows.Forms</c> against <c>Majorsilence.Forms</c>.
    /// </summary>
    /// <remarks>
    /// Overload checking was off here until the wholly-missing member count came down: an overload
    /// report on a type that is itself half-implemented is noise rather than signal. It is on now,
    /// which is what turns "the method exists" into "the call site compiles" — a caller writing
    /// <c>grid.AutoResizeColumn (0, mode)</c> does not care that some <c>AutoResizeColumn</c> exists.
    /// </remarks>
    public static Surface WinForms { get; } = new(
        "winforms",
        "WindowsDesktopRefPackageRoot",
        "System.Windows.Forms.dll",
        new(StringComparer.Ordinal)
        {
            ["System.Windows.Forms"] = ["Majorsilence.Forms"],
            ["System.Windows.Forms.Layout"] = ["Majorsilence.Forms.Layout", "Majorsilence.Forms"],
        },
        new(StringComparer.Ordinal)
        {
            // ActiveX hosting: COM interop with no cross-platform meaning.
            "AxHost",
            // Win32 window-message plumbing. Majorsilence.Forms has no HWND and no message pump to
            // filter; the backends deliver input through their own neutral seam instead.
            "Message", "IMessageFilter", "IWin32Window", "NativeWindow", "IWindowTarget",
            // Windows-only shell/OS integration.
            "SystemInformation", "InputLanguage", "InputLanguageCollection", "OSFeature", "FeatureSupport",
            "ImeContext", "ImeModeConversion", "WindowsFormsSection", "PowerStatus", "SystemParameter",
            // The Windows registry. Application.UserAppDataRegistry and CommonAppDataRegistry are
            // typed as RegistryKey, which lives in the Windows-only Microsoft.Win32.Registry package;
            // taking that dependency to return a handle that throws everywhere but Windows would make
            // the library less portable in exchange for nothing.
            "RegistryKey",
        },
        IncludeOverloads: true);

    public static Surface[] All { get; } = [Drawing, WinForms];

    /// <summary>Where this surface's committed baseline lives.</summary>
    public string BaselinePath(string repoRoot) =>
        Path.Combine(repoRoot, "tools", "Majorsilence.Forms.ApiDiff", $"baseline.{Name}.txt");

    /// <summary>The assemblies of ours that may contain this surface's replacements.</summary>
    public string[] OurAssemblies(string repoRoot, string configuration) =>
        [
            Path.Combine(repoRoot, "src", "Majorsilence.Forms", "bin", configuration, "net10.0", "Majorsilence.Forms.dll"),
            Path.Combine(repoRoot, "src", "Majorsilence.Forms.Drawing.Common", "bin", configuration, "net10.0", "Majorsilence.Forms.Drawing.Common.dll"),
        ];
}
