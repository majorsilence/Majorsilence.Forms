using System.Collections.Concurrent;
using System.Drawing;

#pragma warning disable CA1416  // Windows-only System.Drawing types used intentionally in compat layer

namespace Majorsilence.Forms
{
    /// <summary>
    /// WinForms compatibility: the system colors. Each member is the real known color
    /// (<c>Color.FromKnownColor (KnownColor.X)</c>), as in System.Drawing, so
    /// <c>Color.IsSystemColor</c> is true, <see cref="Color.Name"/> is the member's name,
    /// <c>ToKnownColor ()</c> round-trips, and the value equals a <c>KnownColor</c> a designer file or
    /// resx deserialised.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This library paints a system color with its own palette -- the light Windows 10 defaults
    /// (<see cref="Control"/> is 240,240,240, <see cref="Highlight"/> 0,120,215) -- on every platform.
    /// The palette is applied when a color is drawn, so anything handed to a control, pen, brush or
    /// <see cref="Graphics"/> paints those values.
    /// </para>
    /// <para>
    /// Caveat: a known color's <see cref="Color.R"/>/<see cref="Color.G"/>/<see cref="Color.B"/>
    /// (and <see cref="Color.ToArgb"/>) come from the .NET runtime, not from this library. On Windows
    /// they are the live OS colors; elsewhere they are the runtime's built-in table, the Windows XP
    /// palette (<see cref="Control"/> reads 236,233,216). Application code that does its own arithmetic
    /// on a system color's channels -- or builds <c>Color.FromArgb (alpha, SystemColors.X)</c>, which
    /// drops the identity -- gets those values, not the ones painted. Compare system colors by
    /// identity (<c>==</c>, <c>ToKnownColor ()</c>), not by <c>ToArgb ()</c>.
    /// </para>
    /// <para>
    /// <see cref="AlternateRow"/> and <see cref="ButtonText"/> are this library's own extensions, not
    /// members of System.Drawing's <c>SystemColors</c>. There is no known color for them to be, so they
    /// are plain colors (<c>IsSystemColor</c> is false) and are not resolved through the palette.
    /// </para>
    /// </remarks>
    public static class SystemColors
    {
        // Color.FromName returns the real known color for a system color's name (it is
        // Color.FromKnownColor through the BCL's name table), and unlike KnownColor it exists on
        // netstandard2.0. Cached: the lookup is a dictionary hit and these are read on every paint.
        private static Color Known (string name) => Color.FromName (name);

        /// <summary>Gets the face color of a 3-D element.</summary>
        public static Color ButtonFace { get; } = Known ("ButtonFace");

        /// <summary>Gets the highlight color of a 3-D element.</summary>
        public static Color ButtonHighlight { get; } = Known ("ButtonHighlight");

        /// <summary>Gets the shadow color of a 3-D element.</summary>
        public static Color ButtonShadow { get; } = Known ("ButtonShadow");

        /// <summary>Gets the color of a window background.</summary>
        public static Color Window { get; } = Known ("Window");

        /// <summary>Gets the color of the text in a window.</summary>
        public static Color WindowText { get; } = Known ("WindowText");

        /// <summary>Gets the color of a control.</summary>
        public static Color Control { get; } = Known ("Control");

        /// <summary>Gets the color of text in a control.</summary>
        public static Color ControlText { get; } = Known ("ControlText");

        /// <summary>Gets the dark shadow for 3-D elements.</summary>
        public static Color ControlDark { get; } = Known ("ControlDark");

        /// <summary>Gets the very dark shadow for 3-D elements.</summary>
        public static Color ControlDarkDark { get; } = Known ("ControlDarkDark");

        /// <summary>Gets the light color for 3-D elements.</summary>
        public static Color ControlLight { get; } = Known ("ControlLight");

        /// <summary>Gets the very light color for 3-D elements.</summary>
        public static Color ControlLightLight { get; } = Known ("ControlLightLight");

        /// <summary>Gets the color of highlighted text background.</summary>
        public static Color Highlight { get; } = Known ("Highlight");

        /// <summary>Gets the color of highlighted text.</summary>
        public static Color HighlightText { get; } = Known ("HighlightText");

        /// <summary>Gets the color of a menu background.</summary>
        public static Color Menu { get; } = Known ("Menu");

        /// <summary>Gets the color of menu text.</summary>
        public static Color MenuText { get; } = Known ("MenuText");

        /// <summary>Gets the color of the active title bar.</summary>
        public static Color ActiveCaption { get; } = Known ("ActiveCaption");

        /// <summary>Gets the color of text in the active title bar.</summary>
        public static Color ActiveCaptionText { get; } = Known ("ActiveCaptionText");

        /// <summary>Gets the color of the inactive title bar.</summary>
        public static Color InactiveCaption { get; } = Known ("InactiveCaption");

        /// <summary>Gets the color of text in the inactive title bar.</summary>
        public static Color InactiveCaptionText { get; } = Known ("InactiveCaptionText");

        /// <summary>Gets the color of an active border.</summary>
        public static Color ActiveBorder { get; } = Known ("ActiveBorder");

        /// <summary>Gets the color of an inactive border.</summary>
        public static Color InactiveBorder { get; } = Known ("InactiveBorder");

        /// <summary>Gets the color of the desktop.</summary>
        public static Color Desktop { get; } = Known ("Desktop");

        /// <summary>Gets the color of a tooltip background.</summary>
        public static Color Info { get; } = Known ("Info");

        /// <summary>Gets the color of tooltip text.</summary>
        public static Color InfoText { get; } = Known ("InfoText");

        /// <summary>Gets the color of grayed (disabled) text.</summary>
        public static Color GrayText { get; } = Known ("GrayText");

        /// <summary>Gets the color of the application workspace.</summary>
        public static Color AppWorkspace { get; } = Known ("AppWorkspace");

        /// <summary>Gets the scrollbar gray area.</summary>
        public static Color ScrollBar { get; } = Known ("ScrollBar");

        /// <summary>Gets the color of the hot-tracking item.</summary>
        public static Color HotTrack { get; } = Known ("HotTrack");

        /// <summary>Gets the color of highlighted menu item background.</summary>
        public static Color MenuHighlight { get; } = Known ("MenuHighlight");

        /// <summary>Gets the lighter end of the active title bar's gradient.</summary>
        public static Color GradientActiveCaption { get; } = Known ("GradientActiveCaption");

        /// <summary>Gets the lighter end of the inactive title bar's gradient.</summary>
        public static Color GradientInactiveCaption { get; } = Known ("GradientInactiveCaption");

        /// <summary>Gets the color used to shade alternate rows in a ListView.</summary>
        /// <remarks>
        /// A Majorsilence.Forms extension, not part of System.Drawing's <c>SystemColors</c>: a plain
        /// 240,248,255, not a system color.
        /// </remarks>
        public static Color AlternateRow { get; } = Color.FromArgb (240, 248, 255);

        /// <summary>Gets the border color of the active window.</summary>
        public static Color WindowFrame { get; } = Known ("WindowFrame");

        /// <summary>Gets the text color of a button control.</summary>
        /// <remarks>
        /// A Majorsilence.Forms extension, not part of System.Drawing's <c>SystemColors</c> (whose button
        /// text is <see cref="ControlText"/>): plain black, not a system color.
        /// </remarks>
        public static Color ButtonText { get; } = Color.Black;

        /// <summary>Gets the color used to highlight a menu item when the menu item is selected.</summary>
        public static Color MenuBar { get; } = Known ("MenuBar");
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
