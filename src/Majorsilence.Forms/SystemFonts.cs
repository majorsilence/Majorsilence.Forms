using SkiaSharp;

namespace Majorsilence.Forms
{
    /// <summary>
    /// WinForms compatibility: provides default system font information.
    /// Returns cross-platform <see cref="Majorsilence.Forms.Drawing.Font"/> objects so WinForms code that assigns
    /// these to Control.Font compiles and runs on every platform.
    /// </summary>
    public static class SystemFonts
    {
        // Every one of these is a WinForms UI font, and on Windows they are all about 9pt -- the shell
        // uses one size for menus, captions, status bars and message boxes alike. They used to be built
        // at Theme.FontSize, which is the Avalonia chrome size (14 on Windows and macOS): more than half
        // again as large. That is the same mismatch DefaultFont is commented for below, and it lands on
        // any control assigned a system font -- RibbonWinForms sets Font = SystemFonts.CaptionFont, and
        // its panel and button text overflowed the slots the ribbon had measured for it.
        //
        // The family still tracks the theme, so the text matches the rest of the UI; only the size is
        // pinned to what WinForms code was laid out against.
        private const float SystemFontSize = 9f;

        // The caller's own property name is stamped on as SystemFontName, which is what makes
        // Font.IsSystemFont a real answer rather than a hardcoded false.
        private static Majorsilence.Forms.Drawing.Font Create ([System.Runtime.CompilerServices.CallerMemberName] string systemFontName = "")
            => new Majorsilence.Forms.Drawing.Font (Theme.UIFont.FamilyName, SystemFontSize) { SystemFontName = systemFontName };

        // DefaultFont is the ambient fallback every unfonted Control.Font resolves to (see
        // Control.Font's getter). Real System.Windows.Forms.SystemFonts.DefaultFont is
        // "Microsoft Sans Serif, 8.25pt" -- NOT the active theme's chrome font. Conflating the
        // two meant any WinForms designer control that never sets Font explicitly (relying on
        // WinForms' own ambient-default behavior) rendered at Theme.FontSize (14pt "Segoe UI
        // Emoji" on Windows) instead of the classic 8.25pt default -- roughly 70% wider text
        // than the designer's AutoSize-computed Size accounted for, clipping the text of
        // fixed-Size labels whose designer files never assign a Font. "Microsoft Sans Serif" is
        // unavailable on non-Windows (see Theme's _uiFontFamily comment on the per-glyph
        // fallback cost of missing font families), so fall back to "sans-serif" there, matching
        // Theme's own platform check.
        private static readonly string _defaultFontFamily =
            System.Runtime.InteropServices.RuntimeInformation.IsOSPlatform (System.Runtime.InteropServices.OSPlatform.Windows)
                ? "Microsoft Sans Serif"
                : "sans-serif";

        // Set by Application.SetDefaultFont. WinForms lets an app replace the ambient default that
        // every unfonted control inherits; null means "use the classic platform default below".
        private static Majorsilence.Forms.Drawing.Font? _defaultFontOverride;

        /// <summary>Gets the default font of the system.</summary>
        public static Majorsilence.Forms.Drawing.Font DefaultFont =>
            _defaultFontOverride
            ?? new Majorsilence.Forms.Drawing.Font (_defaultFontFamily, 8.25f) { SystemFontName = nameof (DefaultFont) };

        /// <summary>
        /// Replaces the ambient default font, or restores the platform default when passed null.
        /// </summary>
        internal static void SetDefaultFont (Majorsilence.Forms.Drawing.Font? font)
            => _defaultFontOverride = font;

        /// <summary>Gets the dialog box font.</summary>
        public static Majorsilence.Forms.Drawing.Font DialogFont => Create ();

        /// <summary>Gets the icon title font.</summary>
        public static Majorsilence.Forms.Drawing.Font IconTitleFont => Create ();

        /// <summary>Gets the menu font.</summary>
        public static Majorsilence.Forms.Drawing.Font MenuFont => Create ();

        /// <summary>Gets the message box font.</summary>
        public static Majorsilence.Forms.Drawing.Font MessageBoxFont => Create ();

        /// <summary>Gets the small caption font.</summary>
        public static Majorsilence.Forms.Drawing.Font SmallCaptionFont => Create ();

        /// <summary>Gets the status bar font.</summary>
        public static Majorsilence.Forms.Drawing.Font StatusFont => Create ();

        /// <summary>Gets the caption font.</summary>
        public static Majorsilence.Forms.Drawing.Font CaptionFont => Create ();

        // Lazily built once: SKTypeface.FromFamilyName involves a font-manager lookup, and
        // GetEffectiveFont() (Control.cs) calls this on every ambient-font resolution that
        // reaches the root of the parent chain.
        private static readonly SKTypeface _defaultTypeface =
            SKTypeface.FromFamilyName (_defaultFontFamily, SKFontStyleWeight.Normal, SKFontStyleWidth.Normal, SKFontStyleSlant.Upright);

        /// <summary>Gets the default font as an SKTypeface (for internal Majorsilence.Forms use).</summary>
        internal static SKTypeface DefaultTypeface => _defaultTypeface;

        /// <summary>Gets the default font size in points.</summary>
        /// <summary>
        /// Returns the system font with the given name (e.g. "MenuFont", "CaptionFont"), or null when
        /// the name is not one of them — matching System.Drawing.SystemFonts.GetFontByName.
        /// </summary>
        public static Majorsilence.Forms.Drawing.Font? GetFontByName (string systemFontName) => systemFontName switch {
            "DefaultFont" => DefaultFont,
            "DialogFont" => DialogFont,
            "IconTitleFont" => IconTitleFont,
            "MenuFont" => MenuFont,
            "MessageBoxFont" => MessageBoxFont,
            "SmallCaptionFont" => SmallCaptionFont,
            "StatusFont" => StatusFont,
            "CaptionFont" => CaptionFont,
            _ => null,
        };

        /// <summary>Gets the default font size, in points.</summary>
        public static float DefaultFontSize => 8.25f;
    }
}
