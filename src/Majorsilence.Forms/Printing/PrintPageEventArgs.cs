using System;
using System.Drawing;
using Majorsilence.Forms.Drawing;

namespace Majorsilence.Forms.Printing
{
    /// <summary>
    /// Provides data for the <see cref="PrintDocument.PrintPage"/> event. The handler draws the page
    /// onto <see cref="Graphics"/> and sets <see cref="HasMorePages"/> to indicate whether another
    /// page should follow.
    /// </summary>
    public sealed class PrintPageEventArgs : EventArgs
    {
        /// <summary>Initializes a new instance of the PrintPageEventArgs class.</summary>
        public PrintPageEventArgs (SkiaGraphics graphics, RectangleF marginBounds, RectangleF pageBounds, PageSettings pageSettings)
        {
            ArgumentNullException.ThrowIfNull (graphics);

            SkiaGraphics = graphics;
            Graphics = new Graphics (graphics.Canvas);
            MarginBounds = Round (marginBounds);
            PageBounds = Round (pageBounds);
            PageSettings = pageSettings;
        }

        /// <summary>Gets the drawing surface for the page (in pixels at the page DPI).</summary>
        /// <remarks>
        /// The same <c>Majorsilence.Forms.Graphics</c> a control receives in OnPaint, because
        /// that is the whole point of the WinForms printing model: a control draws a page with the same
        /// routine it draws itself with, just against different bounds. Exposing only the lower-level
        /// Skia surface here meant print code could not call into paint code at all.
        /// </remarks>
        public Graphics Graphics { get; }

        /// <summary>Gets the underlying Skia surface for the page.</summary>
        /// <remarks>
        /// The lower-level surface behind <see cref="Graphics"/>, for callers that want Skia directly.
        /// Both draw onto the same canvas.
        /// </remarks>
        public SkiaGraphics SkiaGraphics { get; }

        /// <summary>Gets the area inside the margins, in pixels.</summary>
        public Rectangle MarginBounds { get; }

        /// <summary>Gets the full printable page area, in pixels.</summary>
        public Rectangle PageBounds { get; }

        /// <summary>Gets the page settings for this page.</summary>
        public PageSettings PageSettings { get; }

        /// <summary>Gets or sets whether an additional page should be printed.</summary>
        public bool HasMorePages { get; set; }

        /// <summary>Gets or sets whether the print job should be cancelled.</summary>
        public bool Cancel { get; set; }

        // WinForms reports both bounds as integer Rectangles, and handlers pass them straight into
        // drawing calls that take Rectangle. The page geometry is computed in floats from the margins,
        // so round rather than truncate -- truncating loses up to a pixel off each edge.
        private static Rectangle Round (RectangleF bounds)
            => Rectangle.Round (bounds);
    }
}
