using System.Collections.Concurrent;
using System.Drawing;

#pragma warning disable CA1416  // Windows-only System.Drawing types used intentionally in compat layer

namespace Majorsilence.Forms
{
    /// <summary>
    /// WinForms compatibility: provides system color constants mapped to Majorsilence.Forms theme colors.
    /// Colors are approximations; exact values depend on the active theme.
    /// </summary>
    public static class SystemColors
    {
        /// <summary>Gets the face color of a 3-D element.</summary>
        public static Color ButtonFace => Color.FromArgb (240, 240, 240);

        /// <summary>Gets the highlight color of a 3-D element.</summary>
        public static Color ButtonHighlight => Color.White;

        /// <summary>Gets the shadow color of a 3-D element.</summary>
        public static Color ButtonShadow => Color.FromArgb (160, 160, 160);

        /// <summary>Gets the color of a window background.</summary>
        public static Color Window => Color.White;

        /// <summary>Gets the color of the text in a window.</summary>
        public static Color WindowText => Color.Black;

        /// <summary>Gets the color of a control.</summary>
        public static Color Control => Color.FromArgb (240, 240, 240);

        /// <summary>Gets the color of text in a control.</summary>
        public static Color ControlText => Color.Black;

        /// <summary>Gets the dark shadow for 3-D elements.</summary>
        public static Color ControlDark => Color.FromArgb (160, 160, 160);

        /// <summary>Gets the very dark shadow for 3-D elements.</summary>
        public static Color ControlDarkDark => Color.FromArgb (105, 105, 105);

        /// <summary>Gets the light color for 3-D elements.</summary>
        public static Color ControlLight => Color.FromArgb (227, 227, 227);

        /// <summary>Gets the very light color for 3-D elements.</summary>
        public static Color ControlLightLight => Color.White;

        /// <summary>Gets the color of highlighted text background.</summary>
        public static Color Highlight => Color.FromArgb (0, 120, 215);

        /// <summary>Gets the color of highlighted text.</summary>
        public static Color HighlightText => Color.White;

        /// <summary>Gets the color of a menu background.</summary>
        public static Color Menu => Color.FromArgb (240, 240, 240);

        /// <summary>Gets the color of menu text.</summary>
        public static Color MenuText => Color.Black;

        /// <summary>Gets the color of the active title bar.</summary>
        public static Color ActiveCaption => Color.FromArgb (0, 120, 215);

        /// <summary>Gets the color of text in the active title bar.</summary>
        public static Color ActiveCaptionText => Color.White;

        /// <summary>Gets the color of the inactive title bar.</summary>
        public static Color InactiveCaption => Color.FromArgb (191, 205, 219);

        /// <summary>Gets the color of text in the inactive title bar.</summary>
        public static Color InactiveCaptionText => Color.FromArgb (67, 78, 84);

        /// <summary>Gets the color of an active border.</summary>
        public static Color ActiveBorder => Color.FromArgb (180, 180, 180);

        /// <summary>Gets the color of an inactive border.</summary>
        public static Color InactiveBorder => Color.FromArgb (244, 247, 252);

        /// <summary>Gets the color of the desktop.</summary>
        public static Color Desktop => Color.Black;

        /// <summary>Gets the color of a tooltip background.</summary>
        public static Color Info => Color.FromArgb (255, 255, 225);

        /// <summary>Gets the color of tooltip text.</summary>
        public static Color InfoText => Color.Black;

        /// <summary>Gets the color of grayed (disabled) text.</summary>
        public static Color GrayText => Color.FromArgb (109, 109, 109);

        /// <summary>Gets the color of the application workspace.</summary>
        public static Color AppWorkspace => Color.FromArgb (171, 171, 171);

        /// <summary>Gets the scrollbar gray area.</summary>
        public static Color ScrollBar => Color.FromArgb (200, 200, 200);

        /// <summary>Gets the color of the hot-tracking item.</summary>
        public static Color HotTrack => Color.FromArgb (0, 102, 204);

        /// <summary>Gets the color of highlighted menu item background.</summary>
        public static Color MenuHighlight => Color.FromArgb (0, 120, 215);

        /// <summary>Gets the lighter end of the active title bar's gradient.</summary>
        public static Color GradientActiveCaption => Color.FromArgb (185, 209, 234);

        /// <summary>Gets the lighter end of the inactive title bar's gradient.</summary>
        public static Color GradientInactiveCaption => Color.FromArgb (215, 228, 242);

        /// <summary>Gets the color used to shade alternate rows in a ListView.</summary>
        public static Color AlternateRow => Color.FromArgb (240, 248, 255);

        /// <summary>Gets the border color of the active window.</summary>
        public static Color WindowFrame => Color.FromArgb (100, 100, 100);

        /// <summary>Gets the text color of a button control.</summary>
        public static Color ButtonText => Color.Black;

        /// <summary>Gets the color used to highlight a menu item when the menu item is selected.</summary>
        public static Color MenuBar => Color.FromArgb (240, 240, 240);
    }

    /// <summary>
    /// WinForms compatibility: a <see cref="Pen"/> of width 1 for every <see cref="SystemColors"/>
    /// entry. Each property returns the same cached instance for a given color, matching
    /// System.Drawing.SystemPens (whose pens are process-wide singletons and must not be disposed).
    /// </summary>
    public static class SystemPens
    {
        private static readonly ConcurrentDictionary<Color, Pen> cache = new ();

        private static Pen Get (Color color) => cache.GetOrAdd (color, static c => new Pen (c));

        /// <summary>Gets a cached pen for an arbitrary system color.</summary>
        public static Pen FromSystemColor (Color c) => Get (c);

        /// <summary>Gets a cached pen for the <see cref="SystemColors.GradientActiveCaption"/> color.</summary>
        public static Pen GradientActiveCaption => Get (SystemColors.GradientActiveCaption);
        /// <summary>Gets a cached pen for the <see cref="SystemColors.GradientInactiveCaption"/> color.</summary>
        public static Pen GradientInactiveCaption => Get (SystemColors.GradientInactiveCaption);

        /// <summary>Gets a cached pen for the <see cref="SystemColors.ButtonFace"/> color.</summary>
        public static Pen ButtonFace => Get (SystemColors.ButtonFace);
        /// <summary>Gets a cached pen for the <see cref="SystemColors.ButtonHighlight"/> color.</summary>
        public static Pen ButtonHighlight => Get (SystemColors.ButtonHighlight);
        /// <summary>Gets a cached pen for the <see cref="SystemColors.ButtonShadow"/> color.</summary>
        public static Pen ButtonShadow => Get (SystemColors.ButtonShadow);
        /// <summary>Gets a cached pen for the <see cref="SystemColors.Window"/> color.</summary>
        public static Pen Window => Get (SystemColors.Window);
        /// <summary>Gets a cached pen for the <see cref="SystemColors.WindowText"/> color.</summary>
        public static Pen WindowText => Get (SystemColors.WindowText);
        /// <summary>Gets a cached pen for the <see cref="SystemColors.Control"/> color.</summary>
        public static Pen Control => Get (SystemColors.Control);
        /// <summary>Gets a cached pen for the <see cref="SystemColors.ControlText"/> color.</summary>
        public static Pen ControlText => Get (SystemColors.ControlText);
        /// <summary>Gets a cached pen for the <see cref="SystemColors.ControlDark"/> color.</summary>
        public static Pen ControlDark => Get (SystemColors.ControlDark);
        /// <summary>Gets a cached pen for the <see cref="SystemColors.ControlDarkDark"/> color.</summary>
        public static Pen ControlDarkDark => Get (SystemColors.ControlDarkDark);
        /// <summary>Gets a cached pen for the <see cref="SystemColors.ControlLight"/> color.</summary>
        public static Pen ControlLight => Get (SystemColors.ControlLight);
        /// <summary>Gets a cached pen for the <see cref="SystemColors.ControlLightLight"/> color.</summary>
        public static Pen ControlLightLight => Get (SystemColors.ControlLightLight);
        /// <summary>Gets a cached pen for the <see cref="SystemColors.Highlight"/> color.</summary>
        public static Pen Highlight => Get (SystemColors.Highlight);
        /// <summary>Gets a cached pen for the <see cref="SystemColors.HighlightText"/> color.</summary>
        public static Pen HighlightText => Get (SystemColors.HighlightText);
        /// <summary>Gets a cached pen for the <see cref="SystemColors.Menu"/> color.</summary>
        public static Pen Menu => Get (SystemColors.Menu);
        /// <summary>Gets a cached pen for the <see cref="SystemColors.MenuText"/> color.</summary>
        public static Pen MenuText => Get (SystemColors.MenuText);
        /// <summary>Gets a cached pen for the <see cref="SystemColors.ActiveCaption"/> color.</summary>
        public static Pen ActiveCaption => Get (SystemColors.ActiveCaption);
        /// <summary>Gets a cached pen for the <see cref="SystemColors.ActiveCaptionText"/> color.</summary>
        public static Pen ActiveCaptionText => Get (SystemColors.ActiveCaptionText);
        /// <summary>Gets a cached pen for the <see cref="SystemColors.InactiveCaption"/> color.</summary>
        public static Pen InactiveCaption => Get (SystemColors.InactiveCaption);
        /// <summary>Gets a cached pen for the <see cref="SystemColors.InactiveCaptionText"/> color.</summary>
        public static Pen InactiveCaptionText => Get (SystemColors.InactiveCaptionText);
        /// <summary>Gets a cached pen for the <see cref="SystemColors.ActiveBorder"/> color.</summary>
        public static Pen ActiveBorder => Get (SystemColors.ActiveBorder);
        /// <summary>Gets a cached pen for the <see cref="SystemColors.InactiveBorder"/> color.</summary>
        public static Pen InactiveBorder => Get (SystemColors.InactiveBorder);
        /// <summary>Gets a cached pen for the <see cref="SystemColors.Desktop"/> color.</summary>
        public static Pen Desktop => Get (SystemColors.Desktop);
        /// <summary>Gets a cached pen for the <see cref="SystemColors.Info"/> color.</summary>
        public static Pen Info => Get (SystemColors.Info);
        /// <summary>Gets a cached pen for the <see cref="SystemColors.InfoText"/> color.</summary>
        public static Pen InfoText => Get (SystemColors.InfoText);
        /// <summary>Gets a cached pen for the <see cref="SystemColors.GrayText"/> color.</summary>
        public static Pen GrayText => Get (SystemColors.GrayText);
        /// <summary>Gets a cached pen for the <see cref="SystemColors.AppWorkspace"/> color.</summary>
        public static Pen AppWorkspace => Get (SystemColors.AppWorkspace);
        /// <summary>Gets a cached pen for the <see cref="SystemColors.ScrollBar"/> color.</summary>
        public static Pen ScrollBar => Get (SystemColors.ScrollBar);
        /// <summary>Gets a cached pen for the <see cref="SystemColors.HotTrack"/> color.</summary>
        public static Pen HotTrack => Get (SystemColors.HotTrack);
        /// <summary>Gets a cached pen for the <see cref="SystemColors.MenuHighlight"/> color.</summary>
        public static Pen MenuHighlight => Get (SystemColors.MenuHighlight);
        /// <summary>Gets a cached pen for the <see cref="SystemColors.AlternateRow"/> color.</summary>
        public static Pen AlternateRow => Get (SystemColors.AlternateRow);
        /// <summary>Gets a cached pen for the <see cref="SystemColors.WindowFrame"/> color.</summary>
        public static Pen WindowFrame => Get (SystemColors.WindowFrame);
        /// <summary>Gets a cached pen for the <see cref="SystemColors.ButtonText"/> color.</summary>
        public static Pen ButtonText => Get (SystemColors.ButtonText);
        /// <summary>Gets a cached pen for the <see cref="SystemColors.MenuBar"/> color.</summary>
        public static Pen MenuBar => Get (SystemColors.MenuBar);
    }

    /// <summary>
    /// WinForms compatibility: a <see cref="SolidBrush"/> for every <see cref="SystemColors"/> entry.
    /// Each property returns the same cached instance for a given color, matching
    /// System.Drawing.SystemBrushes (whose brushes are process-wide singletons and must not be
    /// disposed).
    /// </summary>
    public static class SystemBrushes
    {
        private static readonly ConcurrentDictionary<Color, SolidBrush> cache = new ();

        private static SolidBrush Get (Color color) => cache.GetOrAdd (color, static c => new SolidBrush (c));

        /// <summary>Gets a cached brush for an arbitrary system color.</summary>
        public static SolidBrush FromSystemColor (Color c) => Get (c);

        /// <summary>Gets a cached brush for the <see cref="SystemColors.GradientActiveCaption"/> color.</summary>
        public static SolidBrush GradientActiveCaption => Get (SystemColors.GradientActiveCaption);
        /// <summary>Gets a cached brush for the <see cref="SystemColors.GradientInactiveCaption"/> color.</summary>
        public static SolidBrush GradientInactiveCaption => Get (SystemColors.GradientInactiveCaption);

        /// <summary>Gets a cached brush for the <see cref="SystemColors.ButtonFace"/> color.</summary>
        public static SolidBrush ButtonFace => Get (SystemColors.ButtonFace);
        /// <summary>Gets a cached brush for the <see cref="SystemColors.ButtonHighlight"/> color.</summary>
        public static SolidBrush ButtonHighlight => Get (SystemColors.ButtonHighlight);
        /// <summary>Gets a cached brush for the <see cref="SystemColors.ButtonShadow"/> color.</summary>
        public static SolidBrush ButtonShadow => Get (SystemColors.ButtonShadow);
        /// <summary>Gets a cached brush for the <see cref="SystemColors.Window"/> color.</summary>
        public static SolidBrush Window => Get (SystemColors.Window);
        /// <summary>Gets a cached brush for the <see cref="SystemColors.WindowText"/> color.</summary>
        public static SolidBrush WindowText => Get (SystemColors.WindowText);
        /// <summary>Gets a cached brush for the <see cref="SystemColors.Control"/> color.</summary>
        public static SolidBrush Control => Get (SystemColors.Control);
        /// <summary>Gets a cached brush for the <see cref="SystemColors.ControlText"/> color.</summary>
        public static SolidBrush ControlText => Get (SystemColors.ControlText);
        /// <summary>Gets a cached brush for the <see cref="SystemColors.ControlDark"/> color.</summary>
        public static SolidBrush ControlDark => Get (SystemColors.ControlDark);
        /// <summary>Gets a cached brush for the <see cref="SystemColors.ControlDarkDark"/> color.</summary>
        public static SolidBrush ControlDarkDark => Get (SystemColors.ControlDarkDark);
        /// <summary>Gets a cached brush for the <see cref="SystemColors.ControlLight"/> color.</summary>
        public static SolidBrush ControlLight => Get (SystemColors.ControlLight);
        /// <summary>Gets a cached brush for the <see cref="SystemColors.ControlLightLight"/> color.</summary>
        public static SolidBrush ControlLightLight => Get (SystemColors.ControlLightLight);
        /// <summary>Gets a cached brush for the <see cref="SystemColors.Highlight"/> color.</summary>
        public static SolidBrush Highlight => Get (SystemColors.Highlight);
        /// <summary>Gets a cached brush for the <see cref="SystemColors.HighlightText"/> color.</summary>
        public static SolidBrush HighlightText => Get (SystemColors.HighlightText);
        /// <summary>Gets a cached brush for the <see cref="SystemColors.Menu"/> color.</summary>
        public static SolidBrush Menu => Get (SystemColors.Menu);
        /// <summary>Gets a cached brush for the <see cref="SystemColors.MenuText"/> color.</summary>
        public static SolidBrush MenuText => Get (SystemColors.MenuText);
        /// <summary>Gets a cached brush for the <see cref="SystemColors.ActiveCaption"/> color.</summary>
        public static SolidBrush ActiveCaption => Get (SystemColors.ActiveCaption);
        /// <summary>Gets a cached brush for the <see cref="SystemColors.ActiveCaptionText"/> color.</summary>
        public static SolidBrush ActiveCaptionText => Get (SystemColors.ActiveCaptionText);
        /// <summary>Gets a cached brush for the <see cref="SystemColors.InactiveCaption"/> color.</summary>
        public static SolidBrush InactiveCaption => Get (SystemColors.InactiveCaption);
        /// <summary>Gets a cached brush for the <see cref="SystemColors.InactiveCaptionText"/> color.</summary>
        public static SolidBrush InactiveCaptionText => Get (SystemColors.InactiveCaptionText);
        /// <summary>Gets a cached brush for the <see cref="SystemColors.ActiveBorder"/> color.</summary>
        public static SolidBrush ActiveBorder => Get (SystemColors.ActiveBorder);
        /// <summary>Gets a cached brush for the <see cref="SystemColors.InactiveBorder"/> color.</summary>
        public static SolidBrush InactiveBorder => Get (SystemColors.InactiveBorder);
        /// <summary>Gets a cached brush for the <see cref="SystemColors.Desktop"/> color.</summary>
        public static SolidBrush Desktop => Get (SystemColors.Desktop);
        /// <summary>Gets a cached brush for the <see cref="SystemColors.Info"/> color.</summary>
        public static SolidBrush Info => Get (SystemColors.Info);
        /// <summary>Gets a cached brush for the <see cref="SystemColors.InfoText"/> color.</summary>
        public static SolidBrush InfoText => Get (SystemColors.InfoText);
        /// <summary>Gets a cached brush for the <see cref="SystemColors.GrayText"/> color.</summary>
        public static SolidBrush GrayText => Get (SystemColors.GrayText);
        /// <summary>Gets a cached brush for the <see cref="SystemColors.AppWorkspace"/> color.</summary>
        public static SolidBrush AppWorkspace => Get (SystemColors.AppWorkspace);
        /// <summary>Gets a cached brush for the <see cref="SystemColors.ScrollBar"/> color.</summary>
        public static SolidBrush ScrollBar => Get (SystemColors.ScrollBar);
        /// <summary>Gets a cached brush for the <see cref="SystemColors.HotTrack"/> color.</summary>
        public static SolidBrush HotTrack => Get (SystemColors.HotTrack);
        /// <summary>Gets a cached brush for the <see cref="SystemColors.MenuHighlight"/> color.</summary>
        public static SolidBrush MenuHighlight => Get (SystemColors.MenuHighlight);
        /// <summary>Gets a cached brush for the <see cref="SystemColors.AlternateRow"/> color.</summary>
        public static SolidBrush AlternateRow => Get (SystemColors.AlternateRow);
        /// <summary>Gets a cached brush for the <see cref="SystemColors.WindowFrame"/> color.</summary>
        public static SolidBrush WindowFrame => Get (SystemColors.WindowFrame);
        /// <summary>Gets a cached brush for the <see cref="SystemColors.ButtonText"/> color.</summary>
        public static SolidBrush ButtonText => Get (SystemColors.ButtonText);
        /// <summary>Gets a cached brush for the <see cref="SystemColors.MenuBar"/> color.</summary>
        public static SolidBrush MenuBar => Get (SystemColors.MenuBar);
    }
}
