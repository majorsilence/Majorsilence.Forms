using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using Majorsilence.Forms.Drawing;
using SkiaSharp;

namespace Majorsilence.Forms.Printing
{
    /// <summary>
    /// Defines a reusable object that renders a document one page at a time. Cross-platform analogue
    /// of System.Drawing.Printing.PrintDocument: the <see cref="PrintPage"/> handler draws each page
    /// onto a Skia-backed <see cref="SkiaGraphics"/>, and the document is produced as a PDF (which is
    /// portable across Windows, macOS, and Linux).
    /// </summary>
    public class PrintDocument : IDisposable
    {
        // Safety cap so a handler that never clears HasMorePages cannot loop forever.
        private const int MaxPages = 10000;

        /// <summary>Gets or sets the name of the document.</summary>
        public string DocumentName { get; set; } = "document";

        /// <summary>Gets or sets the printer settings (page range, copies).</summary>
        public PrinterSettings PrinterSettings { get; set; } = new PrinterSettings ();

        /// <summary>Gets or sets the default page settings (paper size, margins, orientation, DPI).</summary>
        public PageSettings DefaultPageSettings { get; set; } = new PageSettings ();

        /// <summary>Raised for each page being printed.</summary>
        public event EventHandler<PrintPageEventArgs>? PrintPage;

        /// <summary>Raised before the first page is printed.</summary>
        public event EventHandler? BeginPrint;

        /// <summary>Raised after the last page is printed.</summary>
        public event EventHandler? EndPrint;

        /// <summary>Raised before each page is printed to allow per-page settings changes. Stub in Majorsilence.Forms.</summary>
#pragma warning disable CS0067 // Event is part of the WinForms-compat surface; not yet raised (printing is a stub).
        public event EventHandler<QueryPageSettingsEventArgs>? QueryPageSettings;
#pragma warning restore CS0067

        /// <summary>Gets or sets whether the origin of the graphics object is at the user-defined margins. Stub in Majorsilence.Forms.</summary>
        public bool OriginAtMargins { get; set; }

        /// <summary>Raises the PrintPage event.</summary>
        protected virtual void OnPrintPage (PrintPageEventArgs e) => PrintPage?.Invoke (this, e);

        /// <summary>Raises the BeginPrint event.</summary>
        protected virtual void OnBeginPrint (EventArgs e) => BeginPrint?.Invoke (this, e);

        /// <summary>Raises the EndPrint event.</summary>
        protected virtual void OnEndPrint (EventArgs e) => EndPrint?.Invoke (this, e);

        /// <summary>Releases resources used by the document. WinForms compatibility — the document holds no unmanaged state between prints.</summary>
        public void Dispose ()
        {
            Dispose (true);
            GC.SuppressFinalize (this);
        }

        /// <summary>Releases resources used by the document.</summary>
        protected virtual void Dispose (bool disposing) { }

        /// <summary>
        /// Renders the document to a PDF file. Returns the path that was written.
        /// </summary>
        public string Print ()
        {
            var path = Path.Combine (Path.GetTempPath (), MakeSafeFileName (DocumentName) + ".pdf");
            PrintToPdf (path);
            return path;
        }

        /// <summary>
        /// Renders the document to a PDF file at the specified path.
        /// </summary>
        public void PrintToPdf (string path)
        {
            using var stream = File.Create (path);
            PrintToPdf (stream);
        }

        /// <summary>
        /// Renders the document to a PDF written to the specified stream.
        /// </summary>
        public void PrintToPdf (Stream stream)
        {
            ArgumentNullException.ThrowIfNull (stream);

            var settings = DefaultPageSettings;
            var dpi = settings.Dpi <= 0 ? 96f : settings.Dpi;

            // Page size in PDF points (1/72").
            var width_points = settings.EffectiveWidthHundredths / 100f * 72f;
            var height_points = settings.EffectiveHeightHundredths / 100f * 72f;

            // Page size in pixels at the requested DPI (the caller's drawing units).
            var width_px = settings.EffectiveWidthHundredths / 100f * dpi;
            var height_px = settings.EffectiveHeightHundredths / 100f * dpi;

            var margin_left = settings.Margins.Left / 100f * dpi;
            var margin_top = settings.Margins.Top / 100f * dpi;
            var margin_right = settings.Margins.Right / 100f * dpi;
            var margin_bottom = settings.Margins.Bottom / 100f * dpi;

            var page_bounds = new RectangleF (0, 0, width_px, height_px);
            var margin_bounds = new RectangleF (
                margin_left,
                margin_top,
                width_px - margin_left - margin_right,
                height_px - margin_top - margin_bottom);

            // Scale so the caller can draw in pixel units while the PDF is sized in points.
            var scale = 72f / dpi;

            OnBeginPrint (EventArgs.Empty);

            using (var document = SKDocument.CreatePdf (stream)) {
                var page = 0;
                bool has_more;

                do {
                    var page_canvas = document.BeginPage (width_points, height_points);
                    page_canvas.Scale (scale);

                    var graphics = new SkiaGraphics (page_canvas) { DpiX = dpi, DpiY = dpi };
                    var e = new PrintPageEventArgs (graphics, margin_bounds, page_bounds, settings);

                    OnPrintPage (e);

                    document.EndPage ();

                    if (e.Cancel)
                        break;

                    has_more = e.HasMorePages;
                    page++;
                } while (has_more && page < MaxPages);

                document.Close ();
            }

            OnEndPrint (EventArgs.Empty);
        }

        /// <summary>Gets or sets the print controller. Stored but not used in Majorsilence.Forms — the PDF pipeline is always used.</summary>
        public PrintController PrintController { get; set; } = new StandardPrintController ();

        private static string MakeSafeFileName (string name)
        {
            if (string.IsNullOrWhiteSpace (name))
                return "document";

            foreach (var c in Path.GetInvalidFileNameChars ())
                name = name.Replace (c, '_');

            return name;
        }
    }

    /// <summary>
    /// Base class for print controllers: the object a <see cref="PrintDocument"/> calls as it walks a
    /// print job, once at the start and end of the job and once per page.
    /// </summary>
    /// <remarks>
    /// The overridable shape is real, so a controller written against System.Drawing (a page counter, a
    /// progress reporter, a preview collector) compiles and its overrides get called. The base
    /// implementations are deliberately empty: this layer renders through the PDF pipeline in
    /// <see cref="PrintDocument"/>, so a controller observes the job rather than driving the device.
    /// </remarks>
    public abstract class PrintController
    {
        /// <summary>Gets whether this controller is producing a preview rather than printed output.</summary>
        public virtual bool IsPreview => false;

        /// <summary>Called once before the first page of a print job.</summary>
        public virtual void OnStartPrint (PrintDocument document, PrintEventArgs e) { }

        /// <summary>
        /// Called before each page. Returning a <see cref="Majorsilence.Forms.Graphics"/> lets a
        /// controller redirect that page's drawing; returning null uses the document's own surface.
        /// </summary>
        public virtual Majorsilence.Forms.Graphics? OnStartPage (PrintDocument document, PrintPageEventArgs e) => null;

        /// <summary>Called after each page has been drawn.</summary>
        public virtual void OnEndPage (PrintDocument document, PrintPageEventArgs e) { }

        /// <summary>Called once after the last page of a print job.</summary>
        public virtual void OnEndPrint (PrintDocument document, PrintEventArgs e) { }
    }

    /// <summary>Sends print jobs straight through, without preview or a status dialog.</summary>
    public class StandardPrintController : PrintController
    {
        /// <inheritdoc/>
        public override void OnStartPrint (PrintDocument document, PrintEventArgs e) { }

        /// <inheritdoc/>
        public override Majorsilence.Forms.Graphics? OnStartPage (PrintDocument document, PrintPageEventArgs e) => null;

        /// <inheritdoc/>
        public override void OnEndPage (PrintDocument document, PrintPageEventArgs e) { }

        /// <inheritdoc/>
        public override void OnEndPrint (PrintDocument document, PrintEventArgs e) { }
    }

    /// <summary>Wraps a PrintController and shows a status dialog. Stub in Majorsilence.Forms.</summary>
    public class PrintControllerWithStatusDialog : PrintController
    {
        /// <summary>Initializes a new instance wrapping the specified controller.</summary>
        public PrintControllerWithStatusDialog (PrintController underlyingController)
        {
        }

        /// <summary>Initializes a new instance wrapping the specified controller with a dialog title.</summary>
        public PrintControllerWithStatusDialog (PrintController underlyingController, string dialogTitle)
        {
        }
    }

    /// <summary>
    /// A print controller that captures each page as an image instead of printing it, for a preview UI.
    /// </summary>
    public class PreviewPrintController : PrintController
    {
        private readonly List<PreviewPageInfo> pages = [];

        /// <inheritdoc/>
        public override bool IsPreview => true;

        /// <summary>Gets or sets whether previewed pages are rendered with anti-aliasing.</summary>
        public bool UseAntiAlias { get; set; }

        /// <summary>Returns the pages captured during the last print job.</summary>
        public PreviewPageInfo[] GetPreviewPageInfo () => [.. pages];

        /// <inheritdoc/>
        public override void OnStartPrint (PrintDocument document, PrintEventArgs e) => pages.Clear ();

        /// <inheritdoc/>
        public override Majorsilence.Forms.Graphics? OnStartPage (PrintDocument document, PrintPageEventArgs e)
        {
            if (e is null)
                return null;

            // Capture the page at its own size, in hundredths of an inch scaled to pixels at 96 DPI.
            var bounds = System.Drawing.Rectangle.Round (e.PageBounds);
            var image = new Majorsilence.Forms.Drawing.Bitmap (Math.Max (1, bounds.Width), Math.Max (1, bounds.Height));
            pages.Add (new PreviewPageInfo (image, bounds.Size));
            return Majorsilence.Forms.Graphics.FromImage (image);
        }

        /// <inheritdoc/>
        public override void OnEndPage (PrintDocument document, PrintPageEventArgs e) { }

        /// <inheritdoc/>
        public override void OnEndPrint (PrintDocument document, PrintEventArgs e) { }
    }

    /// <summary>One captured page from a <see cref="PreviewPrintController"/>.</summary>
    public sealed class PreviewPageInfo
    {
        /// <summary>Initializes a new instance for the given image and physical size.</summary>
        public PreviewPageInfo (Majorsilence.Forms.Drawing.Image image, System.Drawing.Size physicalSize)
        {
            Image = image;
            PhysicalSize = physicalSize;
        }

        /// <summary>Gets the rendered page.</summary>
        public Majorsilence.Forms.Drawing.Image Image { get; }

        /// <summary>Gets the physical size of the page, in hundredths of an inch.</summary>
        public System.Drawing.Size PhysicalSize { get; }
    }

    /// <summary>Provides data for the <c>BeginPrint</c> and <c>EndPrint</c> events.</summary>
    public class PrintEventArgs : System.ComponentModel.CancelEventArgs
    {
        /// <summary>Gets the reason the print operation occurred.</summary>
        public PrintAction PrintAction { get; internal set; } = PrintAction.PrintToFile;
    }

    /// <summary>Handles the <c>BeginPrint</c> and <c>EndPrint</c> events.</summary>
    public delegate void PrintEventHandler (object sender, PrintEventArgs e);

    /// <summary>Handles the <c>PrintPage</c> event.</summary>
    public delegate void PrintPageEventHandler (object sender, PrintPageEventArgs e);

    /// <summary>Handles the <c>QueryPageSettings</c> event.</summary>
    public delegate void QueryPageSettingsEventHandler (object sender, QueryPageSettingsEventArgs e);

    /// <summary>
    /// Thrown when a printer is not valid. Kept for API compatibility: this layer renders to PDF and
    /// does not resolve OS printers, so nothing here throws it.
    /// </summary>
    public class InvalidPrinterException : SystemException
    {
        /// <summary>Initializes a new instance for the given settings.</summary>
        public InvalidPrinterException (PrinterSettings settings)
            : base ($"Printer '{settings?.PrinterName ?? "(null)"}' is not valid.") => Settings = settings;

        /// <summary>Initializes a new instance with a message.</summary>
        public InvalidPrinterException (string message) : base (message) { }

        /// <summary>Initializes a new instance with a message and inner exception.</summary>
        public InvalidPrinterException (string message, Exception innerException) : base (message, innerException) { }

        /// <summary>Initializes a new instance.</summary>
        public InvalidPrinterException () { }

        /// <summary>Gets the settings the exception was raised for, if any.</summary>
        public PrinterSettings? Settings { get; }
    }

    /// <summary>Converts between the units printing APIs measure in.</summary>
    public static class PrinterUnitConvert
    {
        // Everything is expressed relative to a hundredth of an inch, which is what Display uses.
        private static double UnitsPerDisplay (PrinterUnit unit) => unit switch {
            PrinterUnit.Display => 1.0,
            PrinterUnit.ThousandthsOfAnInch => 10.0,
            PrinterUnit.HundredthsOfAMillimeter => 25.4,
            PrinterUnit.TenthsOfAMillimeter => 2.54,
            _ => 1.0,
        };

        /// <summary>Converts a value from one printer unit to another.</summary>
        public static double Convert (double value, PrinterUnit fromUnit, PrinterUnit toUnit)
            => value * UnitsPerDisplay (toUnit) / UnitsPerDisplay (fromUnit);

        /// <inheritdoc cref="Convert(double, PrinterUnit, PrinterUnit)"/>
        public static int Convert (int value, PrinterUnit fromUnit, PrinterUnit toUnit)
            => (int)Math.Round (Convert ((double)value, fromUnit, toUnit));

        /// <inheritdoc cref="Convert(double, PrinterUnit, PrinterUnit)"/>
        public static System.Drawing.Point Convert (System.Drawing.Point value, PrinterUnit fromUnit, PrinterUnit toUnit)
            => new (Convert (value.X, fromUnit, toUnit), Convert (value.Y, fromUnit, toUnit));

        /// <inheritdoc cref="Convert(double, PrinterUnit, PrinterUnit)"/>
        public static System.Drawing.Size Convert (System.Drawing.Size value, PrinterUnit fromUnit, PrinterUnit toUnit)
            => new (Convert (value.Width, fromUnit, toUnit), Convert (value.Height, fromUnit, toUnit));

        /// <inheritdoc cref="Convert(double, PrinterUnit, PrinterUnit)"/>
        public static System.Drawing.Rectangle Convert (System.Drawing.Rectangle value, PrinterUnit fromUnit, PrinterUnit toUnit)
            => new (Convert (value.X, fromUnit, toUnit), Convert (value.Y, fromUnit, toUnit),
                    Convert (value.Width, fromUnit, toUnit), Convert (value.Height, fromUnit, toUnit));

        /// <inheritdoc cref="Convert(double, PrinterUnit, PrinterUnit)"/>
        public static Margins Convert (Margins value, PrinterUnit fromUnit, PrinterUnit toUnit)
            => value is null ? new Margins () : new Margins (
                Convert (value.Left, fromUnit, toUnit), Convert (value.Right, fromUnit, toUnit),
                Convert (value.Top, fromUnit, toUnit), Convert (value.Bottom, fromUnit, toUnit));
    }
}
