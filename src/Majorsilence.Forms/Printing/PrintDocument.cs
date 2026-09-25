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
        public event PrintPageEventHandler? PrintPage;

        /// <summary>Raised before the first page is printed.</summary>
        public event EventHandler? BeginPrint;

        /// <summary>Raised after the last page is printed.</summary>
        public event EventHandler? EndPrint;

        /// <summary>Raised before each page is printed to allow per-page settings changes; cancelling stops the job.</summary>
        public event EventHandler<QueryPageSettingsEventArgs>? QueryPageSettings;

        /// <summary>Gets or sets whether the origin of the graphics object is at the user-defined margins. Stub in Majorsilence.Forms.</summary>
        /// <summary>Gets or sets whether the page's drawing origin sits at the margin corner.</summary>
        /// <remarks>Real as of W6 mechanisms: on, the page canvas is translated to the margin corner
        /// before <c>PrintPage</c> runs and <see cref="PrintPageEventArgs.MarginBounds"/> is reported
        /// relative to that origin, so a handler drawing at (0, 0) starts inside the margin.</remarks>
        public bool OriginAtMargins { get; set; }

        /// <summary>Raises the PrintPage event.</summary>
        protected virtual void OnPrintPage (PrintPageEventArgs e) => PrintPage?.Invoke (this, e);

        /// <summary>Raises <see cref="QueryPageSettings"/>.</summary>
        protected virtual void OnQueryPageSettings (QueryPageSettingsEventArgs e) => QueryPageSettings?.Invoke (this, e);

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
            Guard.ThrowIfNull (stream);

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

            using var document = SKDocument.CreatePdf (stream);

            WalkPages (PrintAction.PrintToFile, settings, dpi, page_bounds, margin_bounds, () => {
                var page_canvas = document.BeginPage (width_points, height_points);
                page_canvas.Scale (scale);
                return (page_canvas, () => document.EndPage ());
            });

            document.Close ();
        }

        /// <summary>
        /// Walks the document's pages through <see cref="PrintController"/> without producing a PDF:
        /// the controller supplies the surface each page is drawn into, which is how
        /// <see cref="PreviewPrintController"/> captures them (W6 mechanisms).
        /// </summary>
        internal void RunThroughController (PrintAction action)
        {
            var settings = DefaultPageSettings;
            var dpi = settings.Dpi <= 0 ? 96f : settings.Dpi;
            var width_px = settings.EffectiveWidthHundredths / 100f * dpi;
            var height_px = settings.EffectiveHeightHundredths / 100f * dpi;
            var margin_left = settings.Margins.Left / 100f * dpi;
            var margin_top = settings.Margins.Top / 100f * dpi;

            WalkPages (action, settings, dpi,
                new RectangleF (0, 0, width_px, height_px),
                new RectangleF (margin_left, margin_top,
                    width_px - margin_left - settings.Margins.Right / 100f * dpi,
                    height_px - margin_top - settings.Margins.Bottom / 100f * dpi),
                beginPage: null);
        }

        // The page walk both print paths share. `beginPage` supplies the PDF page canvas when there is
        // a PDF to write; the PrintController is offered every page first and its Graphics wins, which
        // is what makes a preview controller a real destination (W6 mechanisms). OriginAtMargins moves
        // the canvas origin to the margin corner, as upstream's does, so a handler that draws at (0,0)
        // starts inside the margin and MarginBounds is reported relative to that origin.
        private void WalkPages (PrintAction action, PageSettings settings, float dpi,
            RectangleF pageBounds, RectangleF marginBounds, Func<(SKCanvas Canvas, Action End)>? beginPage)
        {
            var controller = PrintController;
            var start = new PrintEventArgs { PrintAction = action };

            OnBeginPrint (start);
            controller?.OnStartPrint (this, start);

            if (start.Cancel) {
                var cancelled = new PrintEventArgs { PrintAction = action };
                controller?.OnEndPrint (this, cancelled);
                OnEndPrint (cancelled);
                return;
            }

            var reported_margins = OriginAtMargins
                ? new RectangleF (0, 0, marginBounds.Width, marginBounds.Height)
                : marginBounds;

            // A throwaway surface for the args the controller is offered when there is no PDF page to
            // draw into: PreviewPrintController reads the page geometry from them and answers with a
            // surface of its own, which is then what the page is drawn on.
            using var probe_bitmap = new SKBitmap (1, 1);
            using var probe_canvas = new SKCanvas (probe_bitmap);

            var page = 0;
            bool has_more;

            do {
                // Upstream asks before every page; a cancelled query ends the job (W6.1 sweep).
                var query = new QueryPageSettingsEventArgs (settings);
                OnQueryPageSettings (query);

                if (query.Cancel)
                    break;

                var pdf_page = beginPage?.Invoke ();
                var pdf_canvas = pdf_page?.Canvas;

                // The args are built first so the controller sees the page geometry, then rebuilt on
                // the controller's own surface when it supplies one.
                var e = pdf_canvas is null
                    ? null
                    : new PrintPageEventArgs (new SkiaGraphics (pdf_canvas) { DpiX = dpi, DpiY = dpi }, reported_margins, pageBounds, settings);

                var probe = e ?? new PrintPageEventArgs (new SkiaGraphics (probe_canvas) { DpiX = dpi, DpiY = dpi }, reported_margins, pageBounds, settings);
                var supplied = controller?.OnStartPage (this, probe);

                if (supplied?.Canvas is { } surface)
                    e = new PrintPageEventArgs (new SkiaGraphics (surface) { DpiX = dpi, DpiY = dpi }, reported_margins, pageBounds, settings);

                if (e is null)
                    break;

                if (OriginAtMargins)
                    e.SkiaGraphics.Canvas.Translate (marginBounds.Left, marginBounds.Top);

                OnPrintPage (e);

                controller?.OnEndPage (this, e);
                pdf_page?.End ();
                supplied?.Dispose ();

                if (e.Cancel)
                    break;

                has_more = e.HasMorePages;
                page++;
            } while (has_more && page < MaxPages);

            var end = new PrintEventArgs { PrintAction = action };
            controller?.OnEndPrint (this, end);
            OnEndPrint (end);
        }

        /// <summary>Gets or sets the print controller. Stored but not used in Majorsilence.Forms — the PDF pipeline is always used.</summary>
        /// <summary>Gets or sets the controller every page of a job is routed through.</summary>
        /// <remarks>Read as of W6 mechanisms: the controller is told when a job starts and ends, is
        /// offered every page, and may supply the surface the page is drawn into -- which is how
        /// <see cref="PreviewPrintController"/> captures pages for <see cref="PrintPreviewControl"/>.</remarks>
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
        /// Called before each page. Returning a <see cref="Majorsilence.Forms.Drawing.Graphics"/> lets a
        /// controller redirect that page's drawing; returning null uses the document's own surface.
        /// </summary>
        public virtual Majorsilence.Forms.Drawing.Graphics? OnStartPage (PrintDocument document, PrintPageEventArgs e) => null;

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
        public override Majorsilence.Forms.Drawing.Graphics? OnStartPage (PrintDocument document, PrintPageEventArgs e) => null;

        /// <inheritdoc/>
        public override void OnEndPage (PrintDocument document, PrintPageEventArgs e) { }

        /// <inheritdoc/>
        public override void OnEndPrint (PrintDocument document, PrintEventArgs e) { }
    }

    /// <summary>Wraps a PrintController and shows a status dialog. Stub in Majorsilence.Forms.</summary>
    public class PrintControllerWithStatusDialog : PrintController
    {
        private readonly PrintController? underlying;

        /// <summary>Initializes a new instance wrapping the specified controller.</summary>
        /// <remarks>The controller it wraps is real as of W6 mechanisms: every call is forwarded to it,
        /// so wrapping a <see cref="PreviewPrintController"/> in one still captures the pages. It used
        /// to discard the controller it was handed, which turned any job routed through it into a
        /// no-op. There is no status dialog -- nothing is shown.</remarks>
        public PrintControllerWithStatusDialog (PrintController underlyingController)
            => underlying = underlyingController;

        /// <summary>Initializes a new instance wrapping the specified controller with a dialog title.</summary>
        /// <remarks>See <see cref="PrintControllerWithStatusDialog(PrintController)"/>; the title is
        /// kept for the caller and nothing is shown.</remarks>
        public PrintControllerWithStatusDialog (PrintController underlyingController, string dialogTitle)
        {
            underlying = underlyingController;
            DialogTitle = dialogTitle;
        }

        /// <summary>The title a status dialog would carry; nothing is shown.</summary>
        public string? DialogTitle { get; }

        /// <inheritdoc/>
        public override bool IsPreview => underlying?.IsPreview ?? base.IsPreview;

        /// <inheritdoc/>
        public override void OnStartPrint (PrintDocument document, PrintEventArgs e) => underlying?.OnStartPrint (document, e);

        /// <inheritdoc/>
        public override Majorsilence.Forms.Drawing.Graphics? OnStartPage (PrintDocument document, PrintPageEventArgs e)
            => underlying?.OnStartPage (document, e);

        /// <inheritdoc/>
        public override void OnEndPage (PrintDocument document, PrintPageEventArgs e) => underlying?.OnEndPage (document, e);

        /// <inheritdoc/>
        public override void OnEndPrint (PrintDocument document, PrintEventArgs e) => underlying?.OnEndPrint (document, e);
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
        /// <remarks>Read by <see cref="PrintPreviewControl"/> as of W6 mechanisms: it picks the
        /// sampling the captured page bitmaps are scaled with.</remarks>
        public bool UseAntiAlias { get; set; }

        /// <summary>Returns the pages captured during the last print job.</summary>
        public PreviewPageInfo[] GetPreviewPageInfo () => [.. pages];

        /// <inheritdoc/>
        public override void OnStartPrint (PrintDocument document, PrintEventArgs e) => pages.Clear ();

        /// <inheritdoc/>
        public override Majorsilence.Forms.Drawing.Graphics? OnStartPage (PrintDocument document, PrintPageEventArgs e)
        {
            if (e is null)
                return null;

            // Capture the page at its own size, in hundredths of an inch scaled to pixels at 96 DPI.
            var bounds = System.Drawing.Rectangle.Round (e.PageBounds);
            var image = new Majorsilence.Forms.Drawing.Bitmap (Math.Max (1, bounds.Width), Math.Max (1, bounds.Height));
            pages.Add (new PreviewPageInfo (image, bounds.Size));
            return Majorsilence.Forms.Drawing.Graphics.FromImage (image);
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
        /// <summary>The kind of print job this is.</summary>
        /// <remarks>Set by the document as it starts a job as of W6 mechanisms: <c>PrintToPreview</c>
        /// when the run is feeding a preview controller, else <c>PrintToFile</c> -- there is no
        /// spooler here, so a real printer destination never arises.</remarks>
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
