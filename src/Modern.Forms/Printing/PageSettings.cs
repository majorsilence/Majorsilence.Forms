namespace Modern.Forms.Printing
{
    /// <summary>
    /// Specifies page margins, in hundredths of an inch.
    /// </summary>
    public sealed class Margins
    {
        /// <summary>Initializes a new instance of the Margins class (1 inch on all sides).</summary>
        public Margins () { }

        /// <summary>Initializes a new instance of the Margins class.</summary>
        public Margins (int left, int right, int top, int bottom)
        {
            Left = left;
            Right = right;
            Top = top;
            Bottom = bottom;
        }

        /// <summary>Gets or sets the left margin, in hundredths of an inch.</summary>
        public int Left { get; set; } = 100;

        /// <summary>Gets or sets the right margin, in hundredths of an inch.</summary>
        public int Right { get; set; } = 100;

        /// <summary>Gets or sets the top margin, in hundredths of an inch.</summary>
        public int Top { get; set; } = 100;

        /// <summary>Gets or sets the bottom margin, in hundredths of an inch.</summary>
        public int Bottom { get; set; } = 100;
    }

    /// <summary>
    /// Specifies settings for a printed page. Sizes are expressed in hundredths of an inch
    /// (following the WinForms convention, e.g. US Letter is 850 x 1100).
    /// </summary>
    public sealed class PageSettings
    {
        /// <summary>Gets or sets the paper width, in hundredths of an inch.</summary>
        public int PaperWidth { get; set; } = 850;

        /// <summary>Gets or sets the paper height, in hundredths of an inch.</summary>
        public int PaperHeight { get; set; } = 1100;

        /// <summary>Gets or sets whether the page is printed in landscape orientation.</summary>
        public bool Landscape { get; set; }

        /// <summary>Gets or sets the page margins, in hundredths of an inch.</summary>
        public Margins Margins { get; set; } = new Margins ();

        /// <summary>
        /// Gets or sets the resolution, in DPI, of the drawing surface handed to PrintPage. Drawing
        /// is done in pixels at this DPI; the produced PDF is scaled to the correct physical size.
        /// </summary>
        public float Dpi { get; set; } = 96f;

        // Effective (orientation-adjusted) paper width in hundredths of an inch.
        internal int EffectiveWidthHundredths => Landscape ? PaperHeight : PaperWidth;

        // Effective (orientation-adjusted) paper height in hundredths of an inch.
        internal int EffectiveHeightHundredths => Landscape ? PaperWidth : PaperHeight;
    }
}
