using System;
using System.Drawing;
using Modern.Forms.Drawing;

namespace Modern.Forms.Printing
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
            Graphics = graphics;
            MarginBounds = marginBounds;
            PageBounds = pageBounds;
            PageSettings = pageSettings;
        }

        /// <summary>Gets the drawing surface for the page (in pixels at the page DPI).</summary>
        public SkiaGraphics Graphics { get; }

        /// <summary>Gets the area inside the margins, in pixels.</summary>
        public RectangleF MarginBounds { get; }

        /// <summary>Gets the full printable page area, in pixels.</summary>
        public RectangleF PageBounds { get; }

        /// <summary>Gets the page settings for this page.</summary>
        public PageSettings PageSettings { get; }

        /// <summary>Gets or sets whether an additional page should be printed.</summary>
        public bool HasMorePages { get; set; }

        /// <summary>Gets or sets whether the print job should be cancelled.</summary>
        public bool Cancel { get; set; }
    }
}
