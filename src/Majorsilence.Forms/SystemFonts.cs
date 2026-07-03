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

        /// <summary>Gets the default font of the system.</summary>
        public static Majorsilence.Forms.Drawing.Font DefaultFont => Create ();

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

        /// <summary>Gets the default font as an SKTypeface (for internal Majorsilence.Forms use).</summary>
        internal static SKTypeface DefaultTypeface => Theme.UIFont;

        /// <summary>Gets the default font size in points.</summary>
        public static float DefaultFontSize => Theme.FontSize;
    }
}
