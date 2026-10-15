using SkiaSharp;

namespace Majorsilence.Forms
{
    /// <summary>
    /// Makes rendering independent of the machine it runs on, for pixel tests whose baseline is made on one
    /// machine and checked on another (HeadlessRenderer.UseDeterministic).
    /// </summary>
    /// <remarks>
    /// Two things made the same form render differently per machine. Chrome: macOS uses system decorations,
    /// so a render there has no title bar where Windows and Linux draw one. Fonts: the UI and default fonts
    /// are "Segoe UI"/"Microsoft Sans Serif" on Windows and "sans-serif" elsewhere, resolved through the
    /// machine's font manager, so two Linux machines with different fonts installed rendered the same title
    /// hundreds of pixels apart. The bundled Noto Sans is used everywhere instead, registered privately so
    /// that a lookup by its name finds the bundled copy and not a different installed version.
    /// </remarks>
    internal static class DeterministicRendering
    {
        internal const string FontFamily = "Noto Sans";

        // SystemFonts.DefaultFont's own size, fixed here because an app's SetDefaultFont may have changed it.
        internal const float FontSize = 8.25f;

        /// <summary>Read by <see cref="Form"/>'s constructor: draw the custom title bar on macOS too.</summary>
        internal static bool ForceCustomChrome { get; private set; }

        internal static System.IDisposable Enable ()
        {
            var previous = new State (ForceCustomChrome, Theme.UIFont, Theme.UIFontBold, SystemFonts.DefaultFontOverride);

            var faces = Drawing.FontSubstitution.EmbeddedFaces (FontFamily);
            var regular = faces.FirstOrDefault (f => f.FontWeight == (int) SKFontStyleWeight.Normal && f.FontSlant == SKFontStyleSlant.Upright);
            var bold = faces.FirstOrDefault (f => f.FontWeight == (int) SKFontStyleWeight.Bold && f.FontSlant == SKFontStyleSlant.Upright);
            if (regular is null || bold is null)
                throw new System.InvalidOperationException ($"The bundled {FontFamily} faces are missing from Majorsilence.Forms.Drawing.Common.");

            foreach (var face in faces)
                Drawing.Text.PrivateFontRegistry.Add (face);

            ForceCustomChrome = true;
            Theme.UIFont = regular;
            Theme.UIFontBold = bold;
            SystemFonts.SetDefaultFont (new Drawing.Font (FontFamily, FontSize));

            return new Restore (previous, faces);
        }

        private sealed record State (bool ForceCustomChrome, SKTypeface UIFont, SKTypeface UIFontBold, Drawing.Font? DefaultFont);

        private sealed class Restore (State previous, IReadOnlyList<SKTypeface> faces) : System.IDisposable
        {
            private bool disposed;

            public void Dispose ()
            {
                if (disposed)
                    return;
                disposed = true;

                foreach (var face in faces)
                    Drawing.Text.PrivateFontRegistry.Remove (face);

                ForceCustomChrome = previous.ForceCustomChrome;
                Theme.UIFont = previous.UIFont;
                Theme.UIFontBold = previous.UIFontBold;
                SystemFonts.SetDefaultFont (previous.DefaultFont);
            }
        }
    }
}
