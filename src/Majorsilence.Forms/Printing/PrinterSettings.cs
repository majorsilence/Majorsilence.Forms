namespace Majorsilence.Forms.Printing
{
    /// <summary>
    /// Specifies which pages to print.
    /// </summary>
    public enum PrintRange
    {
        /// <summary>All pages are printed.</summary>
        AllPages,
        /// <summary>The selected pages are printed.</summary>
        Selection,
        /// <summary>The pages between FromPage and ToPage are printed.</summary>
        SomePages,

        // --- Aliases and values completed from upstream System.Drawing.Common (see docs/gdi-gap-plan.md, Phase 2). ---
        /// <summary>Current page.</summary>
        CurrentPage = 4194304,
    }

    /// <summary>
    /// Specifies information about how a document is printed, including which pages and how many copies.
    /// </summary>
    public sealed partial class PrinterSettings
    {
        /// <summary>Initializes a new instance with its own default page settings.</summary>
        public PrinterSettings ()
        {
            // Wire the back-reference here so PageSettings.PrinterSettings never has to allocate a
            // second PrinterSettings for the settings this instance owns.
            DefaultPageSettings = new PageSettings { PrinterSettings = this };
        }

        /// <summary>Gets or sets the number of the first page to print.</summary>
        public int FromPage { get; set; }

        /// <summary>Gets or sets the number of the last page to print.</summary>
        public int ToPage { get; set; }

        /// <summary>Gets or sets the minimum page number allowed in FromPage/ToPage.</summary>
        public int MinimumPage { get; set; }

        /// <summary>Gets or sets the maximum page number allowed in FromPage/ToPage.</summary>
        public int MaximumPage { get; set; }

        /// <summary>Gets or sets the number of copies to print.</summary>
        public int Copies { get; set; } = 1;

        /// <summary>Gets or sets the maximum number of copies allowed.</summary>
        public int MaximumCopies { get; set; } = 9999;

        /// <summary>Gets or sets which pages of the document to print.</summary>
        public PrintRange PrintRange { get; set; } = PrintRange.AllPages;

        /// <summary>Gets or sets the name of the printer to use.</summary>
        public string PrinterName { get; set; } = string.Empty;

        /// <summary>Gets or sets whether to print to a file instead of a printer.</summary>
        public bool PrintToFile { get; set; }

        /// <summary>Gets or sets whether this is the default printer. Stub in Majorsilence.Forms.</summary>
        public bool IsDefaultPrinter => true;

        /// <summary>
        /// Gets the paper sources (trays) supported by the printer. Stub: a single automatic-feed
        /// source (Majorsilence.Forms has no OS print-spooler integration).
        /// </summary>
        public List<PaperSource> PaperSources { get; } = new () { new PaperSource () };

        /// <summary>
        /// Gets the paper sizes supported by the printer. Majorsilence.Forms has no real OS
        /// print-spooler integration (see MIGRATION-NOTES.md in the Majorsilence Reporting repo),
        /// so this can't reflect an actual printer driver's capabilities the way
        /// System.Drawing.Printing.PrinterSettings.PaperSizes does -- instead it returns a fixed
        /// list of common ISO/ANSI paper sizes, useful for populating a page-size picker even
        /// without a real printer behind it.
        /// </summary>
        public System.Collections.Generic.List<PaperSize> PaperSizes { get; } = new()
        {
            new PaperSize ("Letter", 850, 1100) { Kind = PaperKind.Letter },
            new PaperSize ("Legal", 850, 1400) { Kind = PaperKind.Legal },
            new PaperSize ("A3", 1169, 1654) { Kind = PaperKind.A3 },
            new PaperSize ("A4", 827, 1169) { Kind = PaperKind.A4 },
            new PaperSize ("A5", 583, 827) { Kind = PaperKind.Custom },
            new PaperSize ("Tabloid", 1100, 1700) { Kind = PaperKind.Custom },
            new PaperSize ("Executive", 725, 1050) { Kind = PaperKind.Custom },
        };

        /// <summary>Gets or sets whether the printer settings are valid. Stub in Majorsilence.Forms.</summary>
        public bool IsValid => true;

        /// <summary>Gets the default page settings for the printer.</summary>
        public PageSettings DefaultPageSettings { get; }

        /// <summary>Gets or sets whether the printer supports duplex printing. Stub in Majorsilence.Forms.</summary>
        public bool CanDuplex => false;

        /// <summary>Gets or sets the duplex setting. Stub in Majorsilence.Forms.</summary>
        public Duplex Duplex { get; set; } = Duplex.Simplex;

        /// <summary>Gets or sets the file name when printing to a file. Stub in Majorsilence.Forms.</summary>
        public string PrintFileName { get; set; } = string.Empty;

        /// <summary>Gets the list of installed printers. Stub in Majorsilence.Forms — returns empty collection.</summary>
        public static System.Collections.Specialized.StringCollection InstalledPrinters { get; } = new System.Collections.Specialized.StringCollection ();

        /// <summary>Gets or sets whether the printed output is collated.</summary>
        public bool Collate { get; set; }

        /// <summary>Gets whether this printer supports color. Always true: output is a PDF.</summary>
        public bool SupportsColor => true;

        /// <summary>Gets whether this printer is a plotter. Always false for the PDF pipeline.</summary>
        public bool IsPlotter => false;

        /// <summary>
        /// Gets the angle, in degrees, that landscape orientation is rotated by. Zero here: the page is
        /// laid out in landscape directly (see <see cref="PageSettings.Landscape"/>) rather than by
        /// rotating a portrait surface, which is what a driver's angle describes.
        /// </summary>
        public int LandscapeAngle => 0;

        /// <summary>
        /// Returns whether the given image format can be sent straight to the printer. Always false:
        /// everything is rendered through the PDF pipeline rather than handed to a driver.
        /// </summary>
        public bool IsDirectPrintingSupported (Majorsilence.Forms.Drawing.Imaging.ImageFormat imageFormat) => false;

        /// <inheritdoc cref="IsDirectPrintingSupported(Majorsilence.Forms.Drawing.Imaging.ImageFormat)"/>
        public bool IsDirectPrintingSupported (Majorsilence.Forms.Drawing.Image image) => false;

        /// <summary>Gets the printer resolutions this printer supports.</summary>
        public PrinterResolutionCollection PrinterResolutions { get; } = new ([new PrinterResolution ()]);

        /// <summary>
        /// Creates a <see cref="Majorsilence.Forms.Graphics"/> for measuring against this printer's
        /// page, backed by an offscreen surface at the default page settings' DPI.
        /// </summary>
        public Majorsilence.Forms.Graphics CreateMeasurementGraphics ()
        {
            var bounds = DefaultPageSettings.Bounds;
            var scale = DefaultPageSettings.Dpi / 100f;   // Bounds are hundredths of an inch.
            var width = Math.Max (1, (int)(bounds.Width * scale));
            var height = Math.Max (1, (int)(bounds.Height * scale));
            return Majorsilence.Forms.Graphics.FromImage (new Majorsilence.Forms.Drawing.Bitmap (width, height));
        }

        /// <inheritdoc cref="CreateMeasurementGraphics()"/>
        public Majorsilence.Forms.Graphics CreateMeasurementGraphics (PageSettings pageSettings) => CreateMeasurementGraphics ();


        // Nested, matching System.Drawing: these are PrinterSettings.PaperSizeCollection and friends in
        // source, so declaring them at namespace level would not satisfy migrated code that names them.

        /// <summary>A collection of <see cref="PaperSize"/> values.</summary>
        public sealed class PaperSizeCollection : System.Collections.ObjectModel.ReadOnlyCollection<PaperSize>
        {
            /// <summary>Initializes a new instance wrapping the given sizes.</summary>
            public PaperSizeCollection (System.Collections.Generic.IList<PaperSize> array) : base (array) { }
        }

        /// <summary>A collection of <see cref="PaperSource"/> values.</summary>
        public sealed class PaperSourceCollection : System.Collections.ObjectModel.ReadOnlyCollection<PaperSource>
        {
            /// <summary>Initializes a new instance wrapping the given sources.</summary>
            public PaperSourceCollection (System.Collections.Generic.IList<PaperSource> array) : base (array) { }
        }

        /// <summary>A collection of <see cref="PrinterResolution"/> values.</summary>
        public sealed class PrinterResolutionCollection : System.Collections.ObjectModel.ReadOnlyCollection<PrinterResolution>
        {
            /// <summary>Initializes a new instance wrapping the given resolutions.</summary>
            public PrinterResolutionCollection (System.Collections.Generic.IList<PrinterResolution> array) : base (array) { }
        }

        /// <summary>A collection of strings, used for the installed-printer list.</summary>
        public sealed class StringCollection : System.Collections.ObjectModel.ReadOnlyCollection<string>
        {
            /// <summary>Initializes a new instance wrapping the given strings.</summary>
            public StringCollection (System.Collections.Generic.IList<string> array) : base (array) { }
        }

        /// <inheritdoc cref="CreateMeasurementGraphics()"/>
        /// <param name="honorOriginAtMargins">Accepted for API compatibility; the surface always starts at the page origin.</param>
        public Majorsilence.Forms.Graphics CreateMeasurementGraphics (bool honorOriginAtMargins) => CreateMeasurementGraphics ();

        /// <inheritdoc cref="CreateMeasurementGraphics(bool)"/>
        public Majorsilence.Forms.Graphics CreateMeasurementGraphics (PageSettings pageSettings, bool honorOriginAtMargins)
            => CreateMeasurementGraphics ();

        /// <summary>Creates an independent copy of these settings.</summary>
        public PrinterSettings Clone () => new () {
            FromPage = FromPage,
            ToPage = ToPage,
            MinimumPage = MinimumPage,
            MaximumPage = MaximumPage,
            Copies = Copies,
            MaximumCopies = MaximumCopies,
            PrintRange = PrintRange,
            PrinterName = PrinterName,
            PrintToFile = PrintToFile,
            Duplex = Duplex,
            PrintFileName = PrintFileName,
            Collate = Collate,
        };
    }




    /// <summary>Specifies the duplex (double-sided) printing setting.</summary>
    public enum Duplex
    {
        /// <summary>The printer's default duplex setting.</summary>
        Default = -1,
        /// <summary>Single-sided printing.</summary>
        Simplex = 1,
        /// <summary>Double-sided, flipped along the vertical axis.</summary>
        Vertical = 2,
        /// <summary>Double-sided, flipped along the horizontal axis.</summary>
        Horizontal = 3
    }
}
