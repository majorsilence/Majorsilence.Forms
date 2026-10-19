using System;

namespace Majorsilence.Forms.Backends
{
    /// <summary>
    /// Optional: implemented by a platform backend that can answer whether the operating system's appearance is dark (Windows' app
    /// mode, macOS's Dark appearance, GNOME's color-scheme, Android's and iOS's dark theme), and tell when that changes. A backend that
    /// does not implement it answers "light" through <see cref="SystemInformation.PrefersDarkColorScheme"/>, which is also what a
    /// browser assumes when it cannot tell.
    /// </summary>
    public interface IColorSchemeSource
    {
        /// <summary>Gets whether the operating system's appearance is currently dark.</summary>
        bool PrefersDarkColorScheme { get; }

        /// <summary>Raised, on the UI thread, when <see cref="PrefersDarkColorScheme"/> changes.</summary>
        event EventHandler? PrefersDarkColorSchemeChanged;
    }
}
