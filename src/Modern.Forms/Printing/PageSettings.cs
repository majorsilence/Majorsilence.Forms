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

        /// <summary>Gets or sets the paper size. Stub in Modern.Forms.</summary>
        public PaperSize PaperSize {
            get => new PaperSize ("Custom", PaperWidth, PaperHeight);
            set { PaperWidth = value.Width; PaperHeight = value.Height; }
        }

        /// <summary>Gets or sets the paper source. Stub in Modern.Forms.</summary>
        public PaperSource PaperSource { get; set; } = new PaperSource ();

        /// <summary>Gets or sets the printer resolution. Stub in Modern.Forms.</summary>
        public PrinterResolution PrinterResolution { get; set; } = new PrinterResolution ();

        /// <summary>Gets or sets whether to print in color. Stub in Modern.Forms.</summary>
        public bool Color { get; set; } = true;

        /// <summary>Gets the bounding rectangle for the page (in hundredths of an inch).</summary>
        public System.Drawing.Rectangle Bounds =>
            new System.Drawing.Rectangle (0, 0, EffectiveWidthHundredths, EffectiveHeightHundredths);
    }

    /// <summary>Specifies the paper size for a page.</summary>
    public class PaperSize
    {
        /// <summary>Initializes a new PaperSize.</summary>
        public PaperSize () { }

        /// <summary>Initializes a new PaperSize with the given name and dimensions (hundredths of an inch).</summary>
        public PaperSize (string paperName, int width, int height) { PaperName = paperName; Width = width; Height = height; }

        /// <summary>Gets or sets the name of the paper type.</summary>
        public string PaperName { get; set; } = "Custom";

        /// <summary>Gets or sets the width in hundredths of an inch.</summary>
        public int Width { get; set; } = 850;

        /// <summary>Gets or sets the height in hundredths of an inch.</summary>
        public int Height { get; set; } = 1100;

        /// <summary>Gets or sets the paper kind. Stub in Modern.Forms.</summary>
        public PaperKind Kind { get; set; } = PaperKind.Custom;
    }

    /// <summary>Specifies the paper source tray.</summary>
    public class PaperSource
    {
        /// <summary>Gets or sets the name of the paper source. Stub in Modern.Forms.</summary>
        public string SourceName { get; set; } = "Auto";

        /// <summary>Gets or sets the paper source kind. Stub in Modern.Forms.</summary>
        public PaperSourceKind Kind { get; set; } = PaperSourceKind.AutomaticFeed;
    }

    /// <summary>Specifies the printer resolution.</summary>
    public class PrinterResolution
    {
        /// <summary>Gets or sets the horizontal resolution in DPI. Stub in Modern.Forms.</summary>
        public int X { get; set; } = 600;

        /// <summary>Gets or sets the vertical resolution in DPI. Stub in Modern.Forms.</summary>
        public int Y { get; set; } = 600;

        /// <summary>Gets or sets the resolution kind. Stub in Modern.Forms.</summary>
        public PrinterResolutionKind Kind { get; set; } = PrinterResolutionKind.High;
    }

    /// <summary>Specifies a standard paper kind.</summary>
    public enum PaperKind { Custom, Letter, Legal, A4, A3 }

    /// <summary>Specifies the paper source tray.</summary>
    public enum PaperSourceKind { AutomaticFeed, Upper, Lower, Manual, Envelope, Custom }

    /// <summary>Specifies the print resolution kind.</summary>
    public enum PrinterResolutionKind { High, Medium, Low, Draft, Custom }

    /// <summary>Provides data for the QueryPageSettings event.</summary>
    public class QueryPageSettingsEventArgs : System.ComponentModel.CancelEventArgs
    {
        /// <summary>Initializes a new instance of QueryPageSettingsEventArgs.</summary>
        public QueryPageSettingsEventArgs (PageSettings pageSettings) { PageSettings = pageSettings; }

        /// <summary>Gets the page settings for the page about to be printed.</summary>
        public PageSettings PageSettings { get; }
    }
}
