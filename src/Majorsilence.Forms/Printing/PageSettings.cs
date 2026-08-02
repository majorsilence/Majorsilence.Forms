namespace Majorsilence.Forms.Printing
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

        /// <summary>Gets or sets the paper size. Stub in Majorsilence.Forms.</summary>
        public PaperSize PaperSize {
            get => new PaperSize ("Custom", PaperWidth, PaperHeight);
            set { PaperWidth = value.Width; PaperHeight = value.Height; }
        }

        /// <summary>Gets or sets the paper source. Stub in Majorsilence.Forms.</summary>
        public PaperSource PaperSource { get; set; } = new PaperSource ();

        /// <summary>Gets or sets the printer resolution. Stub in Majorsilence.Forms.</summary>
        public PrinterResolution PrinterResolution { get; set; } = new PrinterResolution ();

        /// <summary>Gets or sets whether to print in color. Stub in Majorsilence.Forms.</summary>
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

        /// <summary>Gets or sets the paper kind. Stub in Majorsilence.Forms.</summary>
        public PaperKind Kind { get; set; } = PaperKind.Custom;
    }

    /// <summary>Specifies the paper source tray.</summary>
    public class PaperSource
    {
        /// <summary>Gets or sets the name of the paper source. Stub in Majorsilence.Forms.</summary>
        public string SourceName { get; set; } = "Auto";

        /// <summary>Gets or sets the paper source kind. Stub in Majorsilence.Forms.</summary>
        public PaperSourceKind Kind { get; set; } = PaperSourceKind.AutomaticFeed;
    }

    /// <summary>Specifies the printer resolution.</summary>
    public class PrinterResolution
    {
        /// <summary>Gets or sets the horizontal resolution in DPI. Stub in Majorsilence.Forms.</summary>
        public int X { get; set; } = 600;

        /// <summary>Gets or sets the vertical resolution in DPI. Stub in Majorsilence.Forms.</summary>
        public int Y { get; set; } = 600;

        /// <summary>Gets or sets the resolution kind. Stub in Majorsilence.Forms.</summary>
        public PrinterResolutionKind Kind { get; set; } = PrinterResolutionKind.High;
    }

    /// <summary>Specifies a standard paper kind.</summary>
    public enum PaperKind
    {
        // Explicit values: these were previously implicit (0..4), which put Legal/A4/A3 on the wrong
        // numbers and collided with Tabloid/Ledger/LetterSmall once the full upstream set was added.
        // Corrected in Phase 2 of docs/gdi-gap-plan.md.
        /// <summary>A paper kind defined by the user.</summary>
        Custom = 0,
        /// <summary>Letter paper (8.5 x 11 in).</summary>
        Letter = 1,
        /// <summary>Legal paper (8.5 x 14 in).</summary>
        Legal = 5,
        /// <summary>A3 paper (297 x 420 mm).</summary>
        A3 = 8,
        /// <summary>A4 paper (210 x 297 mm).</summary>
        A4 = 9,

        // --- Aliases and values completed from upstream System.Drawing.Common (see docs/gdi-gap-plan.md, Phase 2). ---
        /// <summary>C sheet.</summary>
        CSheet = 24,
        /// <summary>D sheet.</summary>
        DSheet = 25,
        /// <summary>E sheet.</summary>
        ESheet = 26,
        /// <summary>Letter small.</summary>
        LetterSmall = 2,
        /// <summary>Tabloid.</summary>
        Tabloid = 3,
        /// <summary>Ledger.</summary>
        Ledger = 4,
        /// <summary>Statement.</summary>
        Statement = 6,
        /// <summary>Executive.</summary>
        Executive = 7,
        /// <summary>A 4 small.</summary>
        A4Small = 10,
        /// <summary>A 5.</summary>
        A5 = 11,
        /// <summary>B 4.</summary>
        B4 = 12,
        /// <summary>B 5.</summary>
        B5 = 13,
        /// <summary>Folio.</summary>
        Folio = 14,
        /// <summary>Quarto.</summary>
        Quarto = 15,
        /// <summary>Standard 10 x 14.</summary>
        Standard10x14 = 16,
        /// <summary>Standard 11 x 17.</summary>
        Standard11x17 = 17,
        /// <summary>Note.</summary>
        Note = 18,
        /// <summary>Number 9 envelope.</summary>
        Number9Envelope = 19,
        /// <summary>Number 10 envelope.</summary>
        Number10Envelope = 20,
        /// <summary>Number 11 envelope.</summary>
        Number11Envelope = 21,
        /// <summary>Number 12 envelope.</summary>
        Number12Envelope = 22,
        /// <summary>Number 14 envelope.</summary>
        Number14Envelope = 23,
        /// <summary>Dl envelope.</summary>
        DLEnvelope = 27,
        /// <summary>C 5 envelope.</summary>
        C5Envelope = 28,
        /// <summary>C 3 envelope.</summary>
        C3Envelope = 29,
        /// <summary>C 4 envelope.</summary>
        C4Envelope = 30,
        /// <summary>C 6 envelope.</summary>
        C6Envelope = 31,
        /// <summary>C 65 envelope.</summary>
        C65Envelope = 32,
        /// <summary>B 4 envelope.</summary>
        B4Envelope = 33,
        /// <summary>B 5 envelope.</summary>
        B5Envelope = 34,
        /// <summary>B 6 envelope.</summary>
        B6Envelope = 35,
        /// <summary>Italy envelope.</summary>
        ItalyEnvelope = 36,
        /// <summary>Monarch envelope.</summary>
        MonarchEnvelope = 37,
        /// <summary>Personal envelope.</summary>
        PersonalEnvelope = 38,
        /// <summary>Us standard fanfold.</summary>
        USStandardFanfold = 39,
        /// <summary>German standard fanfold.</summary>
        GermanStandardFanfold = 40,
        /// <summary>German legal fanfold.</summary>
        GermanLegalFanfold = 41,
        /// <summary>Iso b 4.</summary>
        IsoB4 = 42,
        /// <summary>Japanese postcard.</summary>
        JapanesePostcard = 43,
        /// <summary>Standard 9 x 11.</summary>
        Standard9x11 = 44,
        /// <summary>Standard 10 x 11.</summary>
        Standard10x11 = 45,
        /// <summary>Standard 15 x 11.</summary>
        Standard15x11 = 46,
        /// <summary>Invite envelope.</summary>
        InviteEnvelope = 47,
        /// <summary>Letter extra.</summary>
        LetterExtra = 50,
        /// <summary>Legal extra.</summary>
        LegalExtra = 51,
        /// <summary>Tabloid extra.</summary>
        TabloidExtra = 52,
        /// <summary>A 4 extra.</summary>
        A4Extra = 53,
        /// <summary>Letter transverse.</summary>
        LetterTransverse = 54,
        /// <summary>A 4 transverse.</summary>
        A4Transverse = 55,
        /// <summary>Letter extra transverse.</summary>
        LetterExtraTransverse = 56,
        /// <summary>A plus.</summary>
        APlus = 57,
        /// <summary>B plus.</summary>
        BPlus = 58,
        /// <summary>Letter plus.</summary>
        LetterPlus = 59,
        /// <summary>A 4 plus.</summary>
        A4Plus = 60,
        /// <summary>A 5 transverse.</summary>
        A5Transverse = 61,
        /// <summary>B 5 transverse.</summary>
        B5Transverse = 62,
        /// <summary>A 3 extra.</summary>
        A3Extra = 63,
        /// <summary>A 5 extra.</summary>
        A5Extra = 64,
        /// <summary>B 5 extra.</summary>
        B5Extra = 65,
        /// <summary>A 2.</summary>
        A2 = 66,
        /// <summary>A 3 transverse.</summary>
        A3Transverse = 67,
        /// <summary>A 3 extra transverse.</summary>
        A3ExtraTransverse = 68,
        /// <summary>Japanese double postcard.</summary>
        JapaneseDoublePostcard = 69,
        /// <summary>A 6.</summary>
        A6 = 70,
        /// <summary>Japanese envelope kaku number 2.</summary>
        JapaneseEnvelopeKakuNumber2 = 71,
        /// <summary>Japanese envelope kaku number 3.</summary>
        JapaneseEnvelopeKakuNumber3 = 72,
        /// <summary>Japanese envelope chou number 3.</summary>
        JapaneseEnvelopeChouNumber3 = 73,
        /// <summary>Japanese envelope chou number 4.</summary>
        JapaneseEnvelopeChouNumber4 = 74,
        /// <summary>Letter rotated.</summary>
        LetterRotated = 75,
        /// <summary>A 3 rotated.</summary>
        A3Rotated = 76,
        /// <summary>A 4 rotated.</summary>
        A4Rotated = 77,
        /// <summary>A 5 rotated.</summary>
        A5Rotated = 78,
        /// <summary>B 4 jis rotated.</summary>
        B4JisRotated = 79,
        /// <summary>B 5 jis rotated.</summary>
        B5JisRotated = 80,
        /// <summary>Japanese postcard rotated.</summary>
        JapanesePostcardRotated = 81,
        /// <summary>Japanese double postcard rotated.</summary>
        JapaneseDoublePostcardRotated = 82,
        /// <summary>A 6 rotated.</summary>
        A6Rotated = 83,
        /// <summary>Japanese envelope kaku number 2 rotated.</summary>
        JapaneseEnvelopeKakuNumber2Rotated = 84,
        /// <summary>Japanese envelope kaku number 3 rotated.</summary>
        JapaneseEnvelopeKakuNumber3Rotated = 85,
        /// <summary>Japanese envelope chou number 3 rotated.</summary>
        JapaneseEnvelopeChouNumber3Rotated = 86,
        /// <summary>Japanese envelope chou number 4 rotated.</summary>
        JapaneseEnvelopeChouNumber4Rotated = 87,
        /// <summary>B 6 jis.</summary>
        B6Jis = 88,
        /// <summary>B 6 jis rotated.</summary>
        B6JisRotated = 89,
        /// <summary>Standard 12 x 11.</summary>
        Standard12x11 = 90,
        /// <summary>Japanese envelope you number 4.</summary>
        JapaneseEnvelopeYouNumber4 = 91,
        /// <summary>Japanese envelope you number 4 rotated.</summary>
        JapaneseEnvelopeYouNumber4Rotated = 92,
        /// <summary>Prc 16 k.</summary>
        Prc16K = 93,
        /// <summary>Prc 32 k.</summary>
        Prc32K = 94,
        /// <summary>Prc 32 k big.</summary>
        Prc32KBig = 95,
        /// <summary>Prc envelope number 1.</summary>
        PrcEnvelopeNumber1 = 96,
        /// <summary>Prc envelope number 2.</summary>
        PrcEnvelopeNumber2 = 97,
        /// <summary>Prc envelope number 3.</summary>
        PrcEnvelopeNumber3 = 98,
        /// <summary>Prc envelope number 4.</summary>
        PrcEnvelopeNumber4 = 99,
        /// <summary>Prc envelope number 5.</summary>
        PrcEnvelopeNumber5 = 100,
        /// <summary>Prc envelope number 6.</summary>
        PrcEnvelopeNumber6 = 101,
        /// <summary>Prc envelope number 7.</summary>
        PrcEnvelopeNumber7 = 102,
        /// <summary>Prc envelope number 8.</summary>
        PrcEnvelopeNumber8 = 103,
        /// <summary>Prc envelope number 9.</summary>
        PrcEnvelopeNumber9 = 104,
        /// <summary>Prc envelope number 10.</summary>
        PrcEnvelopeNumber10 = 105,
        /// <summary>Prc 16 k rotated.</summary>
        Prc16KRotated = 106,
        /// <summary>Prc 32 k rotated.</summary>
        Prc32KRotated = 107,
        /// <summary>Prc 32 k big rotated.</summary>
        Prc32KBigRotated = 108,
        /// <summary>Prc envelope number 1 rotated.</summary>
        PrcEnvelopeNumber1Rotated = 109,
        /// <summary>Prc envelope number 2 rotated.</summary>
        PrcEnvelopeNumber2Rotated = 110,
        /// <summary>Prc envelope number 3 rotated.</summary>
        PrcEnvelopeNumber3Rotated = 111,
        /// <summary>Prc envelope number 4 rotated.</summary>
        PrcEnvelopeNumber4Rotated = 112,
        /// <summary>Prc envelope number 5 rotated.</summary>
        PrcEnvelopeNumber5Rotated = 113,
        /// <summary>Prc envelope number 6 rotated.</summary>
        PrcEnvelopeNumber6Rotated = 114,
        /// <summary>Prc envelope number 7 rotated.</summary>
        PrcEnvelopeNumber7Rotated = 115,
        /// <summary>Prc envelope number 8 rotated.</summary>
        PrcEnvelopeNumber8Rotated = 116,
        /// <summary>Prc envelope number 9 rotated.</summary>
        PrcEnvelopeNumber9Rotated = 117,
        /// <summary>Prc envelope number 10 rotated.</summary>
        PrcEnvelopeNumber10Rotated = 118,
    }

    /// <summary>Specifies the paper source tray.</summary>
    public enum PaperSourceKind
    {
        // Explicit values: these were previously implicit (0..5) and so disagreed with GDI+ for
        // Manual/Envelope/AutomaticFeed/Custom. Corrected in Phase 2 of docs/gdi-gap-plan.md.
        /// <summary>The upper bin.</summary>
        Upper = 1,
        /// <summary>The lower bin.</summary>
        Lower = 2,
        /// <summary>Manual feed.</summary>
        Manual = 4,
        /// <summary>The envelope feed.</summary>
        Envelope = 5,
        /// <summary>The automatically selected feed.</summary>
        AutomaticFeed = 7,
        /// <summary>A paper source defined by the user.</summary>
        Custom = 257,

        // --- Aliases and values completed from upstream System.Drawing.Common (see docs/gdi-gap-plan.md, Phase 2). ---
        /// <summary>Middle.</summary>
        Middle = 3,
        /// <summary>Manual feed.</summary>
        ManualFeed = 6,
        /// <summary>Tractor feed.</summary>
        TractorFeed = 8,
        /// <summary>Small format.</summary>
        SmallFormat = 9,
        /// <summary>Large format.</summary>
        LargeFormat = 10,
        /// <summary>Large capacity.</summary>
        LargeCapacity = 11,
        /// <summary>Cassette.</summary>
        Cassette = 14,
        /// <summary>Form source.</summary>
        FormSource = 15,
    }

    /// <summary>Specifies the print resolution kind.</summary>
    public enum PrinterResolutionKind
    {
        // Explicit values: these were implicit (0..4), but GDI+ numbers them negatively, with Custom at
        // zero, because the non-custom kinds are Win32 DMRES_* sentinels. Corrected in Phase 2 of
        // docs/gdi-gap-plan.md; a stored PrinterResolutionKind previously round-tripped to the wrong kind.
        /// <summary>High resolution.</summary>
        High = -4,
        /// <summary>Medium resolution.</summary>
        Medium = -3,
        /// <summary>Low resolution.</summary>
        Low = -2,
        /// <summary>Draft-quality resolution.</summary>
        Draft = -1,
        /// <summary>A resolution defined by the user.</summary>
        Custom = 0
    }

    /// <summary>Provides data for the QueryPageSettings event.</summary>
    public class QueryPageSettingsEventArgs : System.ComponentModel.CancelEventArgs
    {
        /// <summary>Initializes a new instance of QueryPageSettingsEventArgs.</summary>
        public QueryPageSettingsEventArgs (PageSettings pageSettings) { PageSettings = pageSettings; }

        /// <summary>Gets the page settings for the page about to be printed.</summary>
        public PageSettings PageSettings { get; }
    }
}
