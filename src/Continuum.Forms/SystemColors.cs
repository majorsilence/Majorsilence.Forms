using System.Drawing;

#pragma warning disable CA1416  // Windows-only System.Drawing types used intentionally in compat layer

namespace Continuum.Forms
{
    /// <summary>
    /// WinForms compatibility: provides system color constants mapped to Continuum.Forms theme colors.
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

        /// <summary>Gets the color used to shade alternate rows in a ListView.</summary>
        public static Color AlternateRow => Color.FromArgb (240, 248, 255);

        /// <summary>Gets the border color of the active window.</summary>
        public static Color WindowFrame => Color.FromArgb (100, 100, 100);

        /// <summary>Gets the text color of a button control.</summary>
        public static Color ButtonText => Color.Black;

        /// <summary>Gets the color used to highlight a menu item when the menu item is selected.</summary>
        public static Color MenuBar => Color.FromArgb (240, 240, 240);
    }

    /// <summary>Provides pre-created Pen objects for system colors. Stub in Continuum.Forms — creates new pens each call.</summary>
    public static class SystemPens
    {
        /// <summary>Gets a pen for the ButtonFace color.</summary>
        public static Pen ButtonFace => new Pen (SystemColors.ButtonFace);
        /// <summary>Gets a pen for the ButtonHighlight color.</summary>
        public static Pen ButtonHighlight => new Pen (SystemColors.ButtonHighlight);
        /// <summary>Gets a pen for the ButtonShadow color.</summary>
        public static Pen ButtonShadow => new Pen (SystemColors.ButtonShadow);
        /// <summary>Gets a pen for the Control color.</summary>
        public static Pen Control => new Pen (SystemColors.Control);
        /// <summary>Gets a pen for the ControlDark color.</summary>
        public static Pen ControlDark => new Pen (SystemColors.ControlDark);
        /// <summary>Gets a pen for the ControlDarkDark color.</summary>
        public static Pen ControlDarkDark => new Pen (SystemColors.ControlDarkDark);
        /// <summary>Gets a pen for the ControlLight color.</summary>
        public static Pen ControlLight => new Pen (SystemColors.ControlLight);
        /// <summary>Gets a pen for the ControlLightLight color.</summary>
        public static Pen ControlLightLight => new Pen (SystemColors.ControlLightLight);
        /// <summary>Gets a pen for the ControlText color.</summary>
        public static Pen ControlText => new Pen (SystemColors.ControlText);
        /// <summary>Gets a pen for the GrayText color.</summary>
        public static Pen GrayText => new Pen (SystemColors.GrayText);
        /// <summary>Gets a pen for the Highlight color.</summary>
        public static Pen Highlight => new Pen (SystemColors.Highlight);
        /// <summary>Gets a pen for the HighlightText color.</summary>
        public static Pen HighlightText => new Pen (SystemColors.HighlightText);
        /// <summary>Gets a pen for the Window color.</summary>
        public static Pen Window => new Pen (SystemColors.Window);
        /// <summary>Gets a pen for the WindowText color.</summary>
        public static Pen WindowText => new Pen (SystemColors.WindowText);
        /// <summary>Gets a pen for the ActiveBorder color.</summary>
        public static Pen ActiveBorder => new Pen (SystemColors.ActiveBorder);
        /// <summary>Gets a pen for the InactiveBorder color.</summary>
        public static Pen InactiveBorder => new Pen (SystemColors.InactiveBorder);
    }

    /// <summary>Provides pre-created SolidBrush objects for system colors. Stub in Continuum.Forms — creates new brushes each call.</summary>
    public static class SystemBrushes
    {
        /// <summary>Gets a brush for the ButtonFace color.</summary>
        public static SolidBrush ButtonFace => new SolidBrush (SystemColors.ButtonFace);
        /// <summary>Gets a brush for the Control color.</summary>
        public static SolidBrush Control => new SolidBrush (SystemColors.Control);
        /// <summary>Gets a brush for the ControlDark color.</summary>
        public static SolidBrush ControlDark => new SolidBrush (SystemColors.ControlDark);
        /// <summary>Gets a brush for the ControlDarkDark color.</summary>
        public static SolidBrush ControlDarkDark => new SolidBrush (SystemColors.ControlDarkDark);
        /// <summary>Gets a brush for the ControlLight color.</summary>
        public static SolidBrush ControlLight => new SolidBrush (SystemColors.ControlLight);
        /// <summary>Gets a brush for the ControlLightLight color.</summary>
        public static SolidBrush ControlLightLight => new SolidBrush (SystemColors.ControlLightLight);
        /// <summary>Gets a brush for the ControlText color.</summary>
        public static SolidBrush ControlText => new SolidBrush (SystemColors.ControlText);
        /// <summary>Gets a brush for the GrayText color.</summary>
        public static SolidBrush GrayText => new SolidBrush (SystemColors.GrayText);
        /// <summary>Gets a brush for the Highlight color.</summary>
        public static SolidBrush Highlight => new SolidBrush (SystemColors.Highlight);
        /// <summary>Gets a brush for the HighlightText color.</summary>
        public static SolidBrush HighlightText => new SolidBrush (SystemColors.HighlightText);
        /// <summary>Gets a brush for the Window color.</summary>
        public static SolidBrush Window => new SolidBrush (SystemColors.Window);
        /// <summary>Gets a brush for the WindowText color.</summary>
        public static SolidBrush WindowText => new SolidBrush (SystemColors.WindowText);
        /// <summary>Gets a brush for the Info (tooltip) background.</summary>
        public static SolidBrush Info => new SolidBrush (SystemColors.Info);
        /// <summary>Gets a brush for the InfoText color.</summary>
        public static SolidBrush InfoText => new SolidBrush (SystemColors.InfoText);
        /// <summary>Gets a brush for the Menu background.</summary>
        public static SolidBrush Menu => new SolidBrush (SystemColors.Menu);
        /// <summary>Gets a brush for the MenuText color.</summary>
        public static SolidBrush MenuText => new SolidBrush (SystemColors.MenuText);
        /// <summary>Gets a brush for the ActiveCaption color.</summary>
        public static SolidBrush ActiveCaption => new SolidBrush (SystemColors.ActiveCaption);
        /// <summary>Gets a brush for the InactiveCaption color.</summary>
        public static SolidBrush InactiveCaption => new SolidBrush (SystemColors.InactiveCaption);
        /// <summary>Gets a brush for the InactiveCaptionText color.</summary>
        public static SolidBrush InactiveCaptionText => new SolidBrush (SystemColors.InactiveCaptionText);
        /// <summary>Gets a brush for the WindowFrame color.</summary>
        public static SolidBrush WindowFrame => new SolidBrush (SystemColors.WindowFrame);
        /// <summary>Gets a brush for the ButtonText color.</summary>
        public static SolidBrush ButtonText => new SolidBrush (SystemColors.ButtonText);
        /// <summary>Gets a brush for the Desktop color.</summary>
        public static SolidBrush Desktop => new SolidBrush (SystemColors.Desktop);
        /// <summary>Gets a brush for the HotTrack color.</summary>
        public static SolidBrush HotTrack => new SolidBrush (SystemColors.HotTrack);
        /// <summary>Gets a brush for the MenuHighlight color.</summary>
        public static SolidBrush MenuHighlight => new SolidBrush (SystemColors.MenuHighlight);
        /// <summary>Gets a brush for the ScrollBar color.</summary>
        public static SolidBrush ScrollBar => new SolidBrush (SystemColors.ScrollBar);
    }
}
