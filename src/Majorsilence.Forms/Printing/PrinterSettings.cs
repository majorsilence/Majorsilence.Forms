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
        SomePages
    }

    /// <summary>
    /// Specifies information about how a document is printed, including which pages and how many copies.
    /// </summary>
    public sealed class PrinterSettings
    {
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

        /// <summary>Gets or sets whether the printer settings are valid. Stub in Majorsilence.Forms.</summary>
        public bool IsValid => true;

        /// <summary>Gets the default page settings for the printer.</summary>
        public PageSettings DefaultPageSettings { get; } = new PageSettings ();

        /// <summary>Gets or sets whether the printer supports duplex printing. Stub in Majorsilence.Forms.</summary>
        public bool CanDuplex => false;

        /// <summary>Gets or sets the duplex setting. Stub in Majorsilence.Forms.</summary>
        public Duplex Duplex { get; set; } = Duplex.Simplex;

        /// <summary>Gets or sets the file name when printing to a file. Stub in Majorsilence.Forms.</summary>
        public string PrintFileName { get; set; } = string.Empty;

        /// <summary>Gets the list of installed printers. Stub in Majorsilence.Forms — returns empty collection.</summary>
        public static System.Collections.Specialized.StringCollection InstalledPrinters { get; } = new System.Collections.Specialized.StringCollection ();
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
