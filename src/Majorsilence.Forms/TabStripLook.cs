using System.Drawing;

namespace Majorsilence.Forms
{
    /// <summary>
    /// How a derived tab control lays out and paints its strip, tabs and page frame when it does so its own way, as
    /// Telerik's RadPageView does. A tab control without one (<see cref="TabControl.StripLook"/> null) is laid out as
    /// the theme, or in a ported app as upstream's tab control, lays it out. The strip is then transparent: the tab
    /// control paints all of it, and the strip keeps only the tabs' hit-testing, selection and scrolling. Logical
    /// pixels throughout, except where a paint canvas is in device pixels.
    /// </summary>
    internal abstract class TabStripLook
    {
        /// <summary>Where the first tab starts in the strip.</summary>
        public abstract Point TabOrigin { get; }

        /// <summary>The height of the row of tabs.</summary>
        public abstract int RowHeight { get; }

        /// <summary>The width of a tab.</summary>
        public abstract int TabWidth (TabStripItem tab);

        /// <summary>The width the strip keeps clear after the tabs, for its buttons and padding.</summary>
        public abstract int TrailingWidth { get; }

        /// <summary>The height of the strip control.</summary>
        public abstract int StripHeight { get; }

        /// <summary>Where a page goes in the tab control.</summary>
        public abstract Rectangle PageBounds { get; }

        /// <summary>The strip's scroll buttons, back and forward, which scroll the tabs when they overflow.</summary>
        public abstract Rectangle ScrollButtons { get; }

        /// <summary>Paints the strip, the tabs and the page frame onto the tab control's background (a device canvas).</summary>
        public abstract void Paint (PaintEventArgs e);

        /// <summary>A left press at <paramref name="location"/> in the strip: true when the look handled it (a button of
        /// its own), so the strip does not select a tab there.</summary>
        public abstract bool OnStripPress (Point location);

        /// <summary>The typeface a font draws in.</summary>
        protected static SkiaSharp.SKTypeface TypefaceOf (Drawing.Font font) => font.GetSKTypeface ();

        /// <summary>A font's em, in logical pixels.</summary>
        protected static float PixelSizeOf (Drawing.Font font) => font.PixelSize;
    }
}
