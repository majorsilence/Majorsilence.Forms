using System.Collections.Generic;
using Majorsilence.Forms.Printing;
using Xunit;

using SkiaSharp;

using Point = System.Drawing.Point;
using RectangleF = System.Drawing.RectangleF;
using Size = System.Drawing.Size;

namespace Majorsilence.Forms.Tests
{
    /// <summary>
    /// Phase 6 of docs/gdi-gap-plan.md: the printing surface. Printing itself already rendered through
    /// the PDF pipeline, so this phase was API-shape completion — with two exceptions that do real work
    /// and are tested as such: <see cref="PrinterUnitConvert"/> and <see cref="PreviewPrintController"/>.
    /// </summary>
    public class PrintingSurfaceTests : IDisposable
    {
        // The SKCanvas handed to SkiaGraphics does not own its SKBitmap, so both have to stay rooted for
        // as long as the canvas is used -- otherwise the bitmap is collected out from under native code
        // and the test process aborts rather than failing.
        private readonly List<IDisposable> _surfaces = [];

        /// <inheritdoc/>
        public void Dispose ()
        {
            foreach (var surface in _surfaces)
                surface.Dispose ();
            GC.SuppressFinalize (this);
        }

        // PrintPageEventArgs carries the real drawing surface and the page geometry, so build one the
        // way PrintDocument does rather than faking it.
        private PrintPageEventArgs NewPageArgs (PageSettings? settings = null)
        {
            settings ??= new PageSettings ();
            var bounds = new RectangleF (0, 0, settings.Bounds.Width, settings.Bounds.Height);

            var bitmap = new SKBitmap (Math.Max (1, settings.Bounds.Width), Math.Max (1, settings.Bounds.Height));
            var canvas = new SKCanvas (bitmap);
            _surfaces.Add (canvas);
            _surfaces.Add (bitmap);

            return new PrintPageEventArgs (new Majorsilence.Forms.Drawing.SkiaGraphics (canvas), bounds, bounds, settings);
        }

        // ---- PrinterUnitConvert: real arithmetic ----

        [Theory]
        // Display is hundredths of an inch, so 100 display units == 1 inch == 1000 thousandths.
        [InlineData (100, PrinterUnit.Display, PrinterUnit.ThousandthsOfAnInch, 1000)]
        [InlineData (1000, PrinterUnit.ThousandthsOfAnInch, PrinterUnit.Display, 100)]
        // 1 inch == 2540 hundredths of a millimetre.
        [InlineData (100, PrinterUnit.Display, PrinterUnit.HundredthsOfAMillimeter, 2540)]
        [InlineData (100, PrinterUnit.Display, PrinterUnit.TenthsOfAMillimeter, 254)]
        public void Convert_translates_between_printer_units (int value, PrinterUnit from, PrinterUnit to, int expected)
            => Assert.Equal (expected, PrinterUnitConvert.Convert (value, from, to));

        [Fact]
        public void Convert_round_trips ()
        {
            const int original = 850;   // US Letter width in hundredths of an inch
            var asMillimetres = PrinterUnitConvert.Convert (original, PrinterUnit.Display, PrinterUnit.HundredthsOfAMillimeter);

            Assert.Equal (original, PrinterUnitConvert.Convert (asMillimetres, PrinterUnit.HundredthsOfAMillimeter, PrinterUnit.Display));
        }

        [Fact]
        public void Convert_handles_the_composite_shapes ()
        {
            var point = PrinterUnitConvert.Convert (new Point (100, 200), PrinterUnit.Display, PrinterUnit.ThousandthsOfAnInch);
            Assert.Equal (new Point (1000, 2000), point);

            var size = PrinterUnitConvert.Convert (new Size (100, 200), PrinterUnit.Display, PrinterUnit.ThousandthsOfAnInch);
            Assert.Equal (new Size (1000, 2000), size);

            var margins = PrinterUnitConvert.Convert (new Margins (100, 100, 50, 50),
                PrinterUnit.Display, PrinterUnit.ThousandthsOfAnInch);
            Assert.Equal (1000, margins.Left);
            Assert.Equal (500, margins.Top);
        }

        // ---- PreviewPrintController: really captures pages ----

        [Fact]
        public void PreviewPrintController_captures_a_page_per_OnStartPage ()
        {
            var controller = new PreviewPrintController ();
            var document = new PrintDocument ();

            Assert.True (controller.IsPreview);
            Assert.Empty (controller.GetPreviewPageInfo ());

            controller.OnStartPrint (document, new PrintEventArgs ());
            var surface = controller.OnStartPage (document, NewPageArgs ());

            Assert.NotNull (surface);   // pages are redirected onto a capture surface
            var pages = controller.GetPreviewPageInfo ();
            Assert.Single (pages);
            Assert.True (pages[0].Image.Width > 0 && pages[0].Image.Height > 0);
            Assert.Equal (pages[0].Image.Width, pages[0].PhysicalSize.Width);
        }

        [Fact]
        public void PreviewPrintController_clears_previous_pages_when_a_new_job_starts ()
        {
            var controller = new PreviewPrintController ();
            var document = new PrintDocument ();

            controller.OnStartPrint (document, new PrintEventArgs ());
            controller.OnStartPage (document, NewPageArgs ());
            controller.OnStartPage (document, NewPageArgs ());
            Assert.Equal (2, controller.GetPreviewPageInfo ().Length);

            controller.OnStartPrint (document, new PrintEventArgs ());
            Assert.Empty (controller.GetPreviewPageInfo ());
        }

        [Fact]
        public void A_standard_controller_does_not_redirect_pages_and_is_not_a_preview ()
        {
            var controller = new StandardPrintController ();

            Assert.False (controller.IsPreview);
            Assert.Null (controller.OnStartPage (new PrintDocument (), NewPageArgs ()));
        }

        [Fact]
        public void A_custom_controller_can_observe_the_job ()
        {
            // The point of making the shape virtual: a controller written against System.Drawing works.
            var controller = new CountingController ();
            var document = new PrintDocument ();

            controller.OnStartPrint (document, new PrintEventArgs ());
            controller.OnStartPage (document, NewPageArgs ());
            controller.OnEndPage (document, NewPageArgs ());
            controller.OnEndPrint (document, new PrintEventArgs ());

            Assert.Equal (1, controller.Starts);
            Assert.Equal (1, controller.Pages);
            Assert.Equal (1, controller.Ends);
        }

        private sealed class CountingController : PrintController
        {
            public int Starts, Pages, Ends;
            public override void OnStartPrint (PrintDocument d, PrintEventArgs e) => Starts++;
            public override Majorsilence.Forms.Drawing.Graphics? OnStartPage (PrintDocument d, PrintPageEventArgs e)
            {
                Pages++;
                return null;
            }
            public override void OnEndPrint (PrintDocument d, PrintEventArgs e) => Ends++;
        }

        // ---- Settings shapes ----

        [Fact]
        public void PageSettings_Clone_is_independent ()
        {
            var settings = new PageSettings { PaperWidth = 999, Landscape = true };
            settings.Margins.Left = 42;

            var clone = settings.Clone ();
            Assert.Equal (999, clone.PaperWidth);
            Assert.True (clone.Landscape);
            Assert.Equal (42, clone.Margins.Left);

            clone.Margins.Left = 7;
            Assert.Equal (42, settings.Margins.Left);
        }

        [Fact]
        public void PrintableArea_matches_the_page_bounds_when_there_is_no_hard_margin ()
        {
            var settings = new PageSettings ();

            Assert.Equal (0f, settings.HardMarginX);
            Assert.Equal (0f, settings.HardMarginY);
            Assert.Equal (settings.Bounds.Width, settings.PrintableArea.Width);
            Assert.Equal (settings.Bounds.Height, settings.PrintableArea.Height);
        }

        [Fact]
        public void RawKind_reads_and_writes_the_same_storage_as_Kind ()
        {
            var size = new PaperSize { Kind = PaperKind.A4 };
            Assert.Equal ((int)PaperKind.A4, size.RawKind);
            size.RawKind = (int)PaperKind.Legal;
            Assert.Equal (PaperKind.Legal, size.Kind);

            var source = new PaperSource { Kind = PaperSourceKind.Manual };
            Assert.Equal ((int)PaperSourceKind.Manual, source.RawKind);
            source.RawKind = (int)PaperSourceKind.Envelope;
            Assert.Equal (PaperSourceKind.Envelope, source.Kind);
        }

        [Fact]
        public void PrinterSettings_Clone_copies_the_job_settings ()
        {
            var settings = new PrinterSettings { Copies = 3, Collate = true, PrinterName = "PDF" };

            var clone = settings.Clone ();

            Assert.Equal (3, clone.Copies);
            Assert.True (clone.Collate);
            Assert.Equal ("PDF", clone.PrinterName);

            clone.Copies = 9;
            Assert.Equal (3, settings.Copies);
        }

        [Fact]
        public void CreateMeasurementGraphics_returns_a_usable_surface ()
        {
            var settings = new PrinterSettings ();

            using var g = settings.CreateMeasurementGraphics ();
            using var font = new Font ("Arial", 10f);

            Assert.True (g.MeasureString ("hello", font).Width > 0);
        }

        [Fact]
        public void The_nested_collection_types_are_named_as_System_Drawing_names_them ()
        {
            // Migrated code writes PrinterSettings.PaperSizeCollection, so these must be nested rather
            // than sitting at namespace level.
            var sizes = new PrinterSettings.PaperSizeCollection ([new PaperSize ("A4", 827, 1169)]);
            var sources = new PrinterSettings.PaperSourceCollection ([new PaperSource ()]);
            var resolutions = new PrinterSettings.PrinterResolutionCollection ([new PrinterResolution ()]);
            var strings = new PrinterSettings.StringCollection (["PDF"]);

            Assert.Single (sizes);
            Assert.Single (sources);
            Assert.Single (resolutions);
            Assert.Equal ("PDF", strings[0]);
        }

        [Fact]
        public void InvalidPrinterException_names_the_offending_printer ()
        {
            var settings = new PrinterSettings { PrinterName = "Nope" };

            var exception = new InvalidPrinterException (settings);

            Assert.Contains ("Nope", exception.Message, System.StringComparison.Ordinal);
            Assert.Same (settings, exception.Settings);
        }
    }
}
