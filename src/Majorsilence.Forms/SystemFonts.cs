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
        // Builds a font from the active theme so values track the UI font / size.
        private static Majorsilence.Forms.Drawing.Font Create () => new Majorsilence.Forms.Drawing.Font (Theme.UIFont.FamilyName, Theme.FontSize);

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

        /// <summary>Gets the default font of the system.</summary>
        public static Majorsilence.Forms.Drawing.Font DefaultFont => new Majorsilence.Forms.Drawing.Font (_defaultFontFamily, 8.25f);

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
        public static float DefaultFontSize => 8.25f;
    }
}
