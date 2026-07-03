using System.Reflection;

namespace Majorsilence.Forms
{
    /// <summary>
    /// A cross-platform stand-in for classic VB's <c>My.Application.Info</c> object. The VB WinForms
    /// application framework (<c>MyType=WindowsForms</c>) exposes this as a property on the compiler-
    /// synthesized <c>My.Application</c> singleton; migrated code reaches it as
    /// <c>My.Application.Info.Title</c>, <c>.Version</c>, etc. <c>MyType=Empty</c> (required for a
    /// cross-platform build) removes that synthesis, so a migrated project defines its own <c>My</c>
    /// shim that forwards <c>My.Application.Info</c> to <see cref="Application.Info"/>.
    /// <code>
    /// Namespace My
    ///     Friend Module MyApplication
    ///         Friend ReadOnly Property Info As Majorsilence.Forms.ApplicationInfo = Majorsilence.Forms.Application.Info
    ///     End Module
    /// End Namespace
    /// </code>
    /// Every member here wraps the reflection-based assembly metadata <see cref="Application"/> already
    /// computes (<see cref="Application.ProductName"/>, <see cref="Application.ProductVersion"/>,
    /// <see cref="Application.CompanyName"/>) rather than reimplementing it.
    /// </summary>
    public sealed class ApplicationInfo
    {
        private readonly Assembly? _assembly;

        internal ApplicationInfo(Assembly? assembly) => _assembly = assembly;

        /// <summary>
        /// Gets the title of the application, as recorded in <see cref="AssemblyTitleAttribute"/>. Falls
        /// back to the assembly's simple name (matching classic VB's own fallback) when no title
        /// attribute is present.
        /// </summary>
        public string Title =>
            _assembly?.GetCustomAttribute<AssemblyTitleAttribute>()?.Title is { Length: > 0 } title
                ? title
                : _assembly?.GetName().Name ?? string.Empty;

        /// <summary>Gets the simple name of the entry assembly (no path, no extension).</summary>
        public string AssemblyName => _assembly?.GetName().Name ?? string.Empty;

        /// <summary>
        /// Gets the application's version. Classic VB's <c>My.Application.Info.Version</c> is a real
        /// <see cref="System.Version"/> (not a string) — code calls <c>.ToString()</c>, <c>.Major</c>,
        /// etc. on it directly.
        /// </summary>
        public Version Version => _assembly?.GetName().Version ?? new Version(0, 0, 0, 0);

        /// <summary>Gets the copyright notice, from <see cref="AssemblyCopyrightAttribute"/>.</summary>
        public string Copyright =>
            _assembly?.GetCustomAttribute<AssemblyCopyrightAttribute>()?.Copyright ?? string.Empty;

        /// <summary>
        /// Gets the company name associated with the application. Wraps
        /// <see cref="Application.CompanyName"/> so the two stay in lockstep.
        /// </summary>
        public string CompanyName => Application.CompanyName ?? string.Empty;

        /// <summary>Gets the application's description, from <see cref="AssemblyDescriptionAttribute"/>.</summary>
        public string Description =>
            _assembly?.GetCustomAttribute<AssemblyDescriptionAttribute>()?.Description ?? string.Empty;

        /// <summary>
        /// Gets the product name associated with the application. Wraps
        /// <see cref="Application.ProductName"/> so the two stay in lockstep.
        /// </summary>
        public string ProductName => Application.ProductName ?? string.Empty;
    }
}
