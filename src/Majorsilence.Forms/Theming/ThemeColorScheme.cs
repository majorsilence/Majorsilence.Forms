namespace Majorsilence.Forms
{
    /// <summary>
    /// The colour scheme a CSS theme's <c>@media (prefers-color-scheme: ...)</c> queries test: the operating system's appearance, or
    /// one the application chose. See <see cref="Theme.ColorScheme"/>.
    /// </summary>
    public enum ThemeColorScheme
    {
        /// <summary>Follow the operating system (<see cref="SystemInformation.PrefersDarkColorScheme"/>).</summary>
        System = 0,

        /// <summary>Always light: <c>(prefers-color-scheme: light)</c> matches, whatever the system says.</summary>
        Light = 1,

        /// <summary>Always dark: <c>(prefers-color-scheme: dark)</c> matches, whatever the system says.</summary>
        Dark = 2,
    }
}
