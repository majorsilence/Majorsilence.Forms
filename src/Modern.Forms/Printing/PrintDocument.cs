using System;
using System.Drawing;
using System.IO;
using Modern.Drawing;
using SkiaSharp;

namespace Modern.Forms.Printing
{
    /// <summary>
    /// Defines a reusable object that renders a document one page at a time. Cross-platform analogue
    /// of System.Drawing.Printing.PrintDocument: the <see cref="PrintPage"/> handler draws each page
    /// onto a Skia-backed <see cref="SkiaGraphics"/>, and the document is produced as a PDF (which is
    /// portable across Windows, macOS, and Linux).
    /// </summary>
    public class PrintDocument
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

        /// <summary>Raised before each page is printed to allow per-page settings changes. Stub in Modern.Forms.</summary>
        public event EventHandler<QueryPageSettingsEventArgs>? QueryPageSettings;

        /// <summary>Gets or sets whether the origin of the graphics object is at the user-defined margins. Stub in Modern.Forms.</summary>
        public bool OriginAtMargins { get; set; }

        /// <summary>Raises the PrintPage event.</summary>
        protected virtual void OnPrintPage (PrintPageEventArgs e) => PrintPage?.Invoke (this, e);

        /// <summary>Raises the BeginPrint event.</summary>
        protected virtual void OnBeginPrint (EventArgs e) => BeginPrint?.Invoke (this, e);

        /// <summary>Raises the EndPrint event.</summary>
        protected virtual void OnEndPrint (EventArgs e) => EndPrint?.Invoke (this, e);

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

        /// <summary>Gets or sets the print controller. Stored but not used in Modern.Forms — the PDF pipeline is always used.</summary>
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

    /// <summary>Abstract base class for print controllers. Stub in Modern.Forms.</summary>
    public abstract class PrintController
    {
    }

    /// <summary>Sends print jobs directly to the printer. Stub in Modern.Forms — the PDF pipeline is always used.</summary>
    public class StandardPrintController : PrintController
    {
    }

    /// <summary>Wraps a PrintController and shows a status dialog. Stub in Modern.Forms.</summary>
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

    /// <summary>Represents a print controller that drives a PrintPreviewControl. Stub in Modern.Forms.</summary>
    public class PreviewPrintController : PrintController
    {
    }
}
