namespace Modern.Forms.Printing
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
    }
}
