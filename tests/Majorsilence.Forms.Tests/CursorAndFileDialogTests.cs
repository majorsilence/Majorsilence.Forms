using Majorsilence.Forms.Backends;
using Majorsilence.Forms.Headless;
using Xunit;

namespace Majorsilence.Forms.Tests;

// #348, the services P1s:
// SVC-12  Cursor.Current was stored only, so `Cursor.Current = Cursors.WaitCursor` showed nothing.
// SVC-13  Control.UseWaitCursor and Application.UseWaitCursor were stored only.
// SVC-14  HSplit and VSplit were swapped.
// SVC-22  FileDialog.FileName threw on "", resolved relative names and was null when unset (covered in
//         FileDialogTests).
// SVC-23  FileOk was never raised, and AddExtension / DefaultExt were never applied.
// SVC-24  FilterIndex never reached the picker and never reported the user's choice.
[Collection ("Headless")]
public sealed class CursorAndFileDialogTests : IDisposable
{
    private readonly Cursor? original_current = Cursor.Current;

    public CursorAndFileDialogTests () => HeadlessRenderer.Use ();

    public void Dispose ()
    {
        Cursor.Current = original_current;
        Application.UseWaitCursor = false;
        HeadlessRenderer.OpenFileResponse = null;
        HeadlessRenderer.SaveFileResponse = null;
    }

    private static HeadlessWindowHost Host (Form form) => (HeadlessWindowHost) form.Backend;

    // ── cursors ──────────────────────────────────────────────────────────────────────────────────

    [Fact]
    public void Cursor_Current_shows_on_the_window_until_the_next_mouse_move ()
    {
        using var form = new Form { Width = 300, Height = 200 };
        var button = new Button { Left = 20, Top = 20, Width = 100, Height = 30, Cursor = Cursors.Hand };
        form.Controls.Add (button);
        form.Show ();

        var at = button.GetPositionInForm ();
        HeadlessRenderer.MouseMove (form, at.X + 5, at.Y + 5);
        HeadlessRenderer.MouseMove (form, at.X + 6, at.Y + 6);
        Assert.Equal (CursorType.Hand, Host (form).Cursor);

        Cursor.Current = Cursors.Wait;   // the busy-cursor idiom
        Assert.Equal (CursorType.Wait, Host (form).Cursor);
        Assert.Same (Cursors.Wait, Cursor.Current);

        // WM_SETCURSOR: the next move puts the control's own cursor back.
        HeadlessRenderer.MouseMove (form, at.X + 7, at.Y + 7);
        Assert.Equal (CursorType.Hand, Host (form).Cursor);
        Assert.Same (Cursors.Hand, Cursor.Current);
    }

    [Fact]
    public void UseWaitCursor_reaches_the_children_and_the_cursor_under_the_pointer ()
    {
        using var form = new Form { Width = 300, Height = 200 };
        var panel = new Panel { Left = 10, Top = 10, Width = 200, Height = 120 };
        var button = new Button { Left = 10, Top = 10, Width = 100, Height = 30 };
        panel.Controls.Add (button);
        form.Controls.Add (panel);
        form.Show ();

        var at = button.GetPositionInForm ();
        HeadlessRenderer.MouseMove (form, at.X + 5, at.Y + 5);

        panel.UseWaitCursor = true;

        Assert.True (button.UseWaitCursor);              // upstream: children take the value
        Assert.Same (Cursors.Wait, button.Cursor);
        Assert.Equal (CursorType.Wait, Host (form).Cursor);   // and the pointer is over it, so it shows now

        panel.UseWaitCursor = false;
        Assert.False (button.UseWaitCursor);
        Assert.NotSame (Cursors.Wait, button.Cursor);
    }

    [Fact]
    public void Application_UseWaitCursor_sets_every_open_form ()
    {
        using var form = new Form { Width = 300, Height = 200 };
        var button = new Button ();
        form.Controls.Add (button);
        form.Show ();

        Application.UseWaitCursor = true;

        Assert.True (form.UseWaitCursor);
        Assert.Same (Cursors.Wait, button.Cursor);
    }

    [Fact]
    public void HSplit_is_the_up_and_down_cursor_and_VSplit_the_left_and_right_one ()
    {
        // Upstream's Splitter: HSplit over a Top/Bottom bar, VSplit over a Left/Right one (Splitter.cs).
        Assert.Equal (CursorType.SizeNorthSouth, Cursors.HSplit.CursorType);
        Assert.Equal (CursorType.SizeWestEast, Cursors.VSplit.CursorType);
    }

    // ── file dialogs ─────────────────────────────────────────────────────────────────────────────

    private const string Filter = "PNG image|*.png|JPEG image|*.jpg;*.jpeg|All files|*.*";

    [Fact]
    public async Task A_save_without_an_extension_gets_the_selected_filters ()
    {
        using var form = new Form ();
        form.Show ();
        HeadlessRenderer.SaveFileResponse = _ => "/tmp/picture";

        using var dialog = new SaveFileDialog { Filter = Filter, FilterIndex = 2, DefaultExt = "bmp" };

        Assert.Equal (DialogResult.OK, await dialog.ShowDialogAsync (form));
        Assert.Equal ("/tmp/picture.jpg", dialog.FileName);   // the filter's first concrete extension wins
    }

    [Fact]
    public async Task With_no_concrete_filter_extension_DefaultExt_is_used_and_AddExtension_false_adds_none ()
    {
        using var form = new Form ();
        form.Show ();
        HeadlessRenderer.SaveFileResponse = _ => "/tmp/report";

        using var dialog = new SaveFileDialog { Filter = Filter, FilterIndex = 3, DefaultExt = "txt" };
        await dialog.ShowDialogAsync (form);
        Assert.Equal ("/tmp/report.txt", dialog.FileName);

        using var plain = new SaveFileDialog { Filter = Filter, FilterIndex = 3, DefaultExt = "txt", AddExtension = false };
        await plain.ShowDialogAsync (form);
        Assert.Equal ("/tmp/report", plain.FileName);
    }

    [Fact]
    public async Task FilterIndex_is_sent_to_the_picker_and_read_back_from_the_chosen_file ()
    {
        using var form = new Form ();
        form.Show ();
        int? sent = null;
        HeadlessRenderer.SaveFileResponse = request => {
            sent = request.FilterIndex;
            return "/tmp/photo.jpeg";
        };

        // Opens on "All files" -- not the request's default of 1, so a dropped FilterIndex shows.
        using var dialog = new SaveFileDialog { Filter = Filter, FilterIndex = 3 };
        await dialog.ShowDialogAsync (form);

        Assert.Equal (3, sent);
        Assert.Equal (2, dialog.FilterIndex);   // a .jpeg was chosen: the JPEG filter, not the one it opened on
    }

    [Fact]
    public async Task A_FileOk_handler_that_cancels_keeps_the_dialog_open ()
    {
        using var form = new Form ();
        form.Show ();
        var picks = new Queue<string[]> (new[] { new[] { "/tmp/first.png" }, new[] { "/tmp/second.png" } });
        HeadlessRenderer.OpenFileResponse = _ => picks.Count > 0 ? picks.Dequeue () : System.Array.Empty<string> ();

        using var dialog = new OpenFileDialog { Filter = Filter };
        var seen = new List<string> ();
        dialog.FileOk += (_, e) => {
            seen.Add (dialog.FileName);
            e.Cancel = dialog.FileName.EndsWith ("first.png", StringComparison.Ordinal);   // reject the first pick
        };

        Assert.Equal (DialogResult.OK, await dialog.ShowDialogAsync (form));
        Assert.Equal (new[] { "/tmp/first.png", "/tmp/second.png" }, seen);   // shown again after the veto
        Assert.Equal ("/tmp/second.png", dialog.FileName);
    }

    [Fact]
    public async Task A_cancelled_dialog_leaves_FileName_empty_not_null ()
    {
        using var form = new Form ();
        form.Show ();

        using var dialog = new OpenFileDialog { FileName = "old.txt" };
        Assert.Equal (DialogResult.Cancel, await dialog.ShowDialogAsync (form));
        Assert.Equal (string.Empty, dialog.FileName);
    }

    // ── DataObject mapped formats (SVC-17) ──────────────────────────────────────────────────────

    [Fact]
    public void Text_answers_to_every_text_name_and_to_typeof_string ()
    {
        var text = new DataObject ("hi");               // stored as Text
        Assert.Equal ("hi", text.GetData (typeof (string)));
        Assert.Equal ("hi", text.GetData (DataFormats.UnicodeText.Name));
        Assert.True (text.GetDataPresent (typeof (string)));

        var unicode = new DataObject (DataFormats.UnicodeText.Name, "uni");
        Assert.True (unicode.GetDataPresent (DataFormats.Text.Name));
        Assert.Equal ("uni", unicode.GetData (typeof (string)));

        // autoConvert false is the exact name only, as upstream.
        Assert.False (unicode.GetDataPresent (DataFormats.Text.Name, autoConvert: false));
        Assert.Contains ("System.String", unicode.GetFormats ());
        Assert.DoesNotContain ("System.String", unicode.GetFormats (autoConvert: false));
    }

    [Fact]
    public void A_file_list_answers_to_FileName_and_rich_text_stays_its_own_format ()
    {
        var files = new DataObject (DataFormats.FileDrop.Name, new[] { "/tmp/a.txt" });
        Assert.True (files.GetDataPresent ("FileNameW"));
        Assert.True (files.GetDataPresent ("FileName"));

        // RTF is a different piece of data: reading it as text would paste the markup.
        var rtf = new DataObject (DataFormats.Rtf.Name, @"{\rtf1 x}");
        Assert.False (rtf.GetDataPresent (DataFormats.Text.Name));
    }

    // ── printing in hundredths of an inch (SVC-28) ──────────────────────────────────────────────

    [Fact]
    public void A_PrintPage_handler_draws_in_hundredths_of_an_inch ()
    {
        // Upstream's unit, the one every migrated PrintPage handler is written in: filling MarginBounds
        // must cover exactly the area inside one-inch margins. Captured through a preview, whose page
        // bitmap is one pixel per unit.
        var preview = new Majorsilence.Forms.Printing.PreviewPrintController ();
        var document = new Majorsilence.Forms.Printing.PrintDocument { PrintController = preview };
        using var font = new Majorsilence.Forms.Drawing.Font ("Arial", 24f);
        float printed = 0;
        document.PrintPage += (_, e) => {
            using var brush = new Majorsilence.Forms.Drawing.SolidBrush (System.Drawing.Color.Black);
            e.Graphics.FillRectangle (brush, e.MarginBounds);
            printed = e.Graphics.MeasureString ("WWWWWWWWWW", font).Width;
        };

        document.RunThroughController (Majorsilence.Forms.Printing.PrintAction.PrintToPreview);

        var page = preview.GetPreviewPageInfo ().Single ();
        Assert.Equal (new System.Drawing.Size (850, 1100), page.PhysicalSize);

        var bitmap = ((Majorsilence.Forms.Drawing.Bitmap) page.Image).GetSKBitmap ()!;
        Assert.Equal (850, bitmap.Width);
        bool Inked (int x, int y) => bitmap.GetPixel (x, y).Alpha > 200 && bitmap.GetPixel (x, y).Red < 60;

        Assert.True (Inked (101, 101));      // just inside the top-left margin corner
        Assert.True (Inked (748, 998));      // just inside the bottom-right one
        Assert.False (Inked (98, 500));      // the left margin
        Assert.False (Inked (752, 500));     // the right margin: 100 + 650

        // The handler's own Graphics sizes fonts for the page's unit too.
        using var screen = Majorsilence.Forms.Drawing.Graphics.FromImage (new Majorsilence.Forms.Drawing.Bitmap (10, 10));
        Assert.InRange (printed / screen.MeasureString ("WWWWWWWWWW", font).Width, 100f / 96f - 0.02f, 100f / 96f + 0.02f);
    }

    [Fact]
    public void Print_measurement_graphics_sizes_a_font_in_points_not_pixels ()
    {
        // A font is in points, so on a page measured in hundredths of an inch it is 100/96 the size it is
        // on a 96-DPI screen: the same physical size. Drawn at its screen pixel size it printed 4% small.
        using var font = new Majorsilence.Forms.Drawing.Font ("Arial", 24f);
        using var printer = new Majorsilence.Forms.Printing.PrinterSettings ().CreateMeasurementGraphics ();
        using var screen = Majorsilence.Forms.Drawing.Graphics.FromImage (new Majorsilence.Forms.Drawing.Bitmap (10, 10));

        var ratio = printer.MeasureString ("WWWWWWWWWW", font).Width / screen.MeasureString ("WWWWWWWWWW", font).Width;
        Assert.InRange (ratio, 100f / 96f - 0.02f, 100f / 96f + 0.02f);
    }

    // ── PrintDocument.Print reaches a printer (SVC-29) ──────────────────────────────────────────

    [Fact]
    public void Print_hands_the_pdf_to_the_system_print_command ()
    {
        System.Diagnostics.ProcessStartInfo? sent = null;
        Majorsilence.Forms.Printing.NativePrinting.LauncherOverride = info => { sent = info; return true; };

        try {
            var document = new Majorsilence.Forms.Printing.PrintDocument { DocumentName = "Invoice" };
            document.PrinterSettings.PrinterName = "Office Laser";
            document.PrintPage += (_, e) => e.HasMorePages = false;

            var path = document.Print ();

            Assert.NotNull (sent);
            Assert.True (System.IO.File.Exists (path));

            if (OperatingSystem.IsWindows ()) {
                Assert.Equal ("printto", sent!.Verb);
                Assert.Equal (path, sent.FileName);
            } else {
                Assert.Equal ("lp", sent!.FileName);
                Assert.Equal (new[] { "-d", "Office Laser", path }, sent.ArgumentList);
            }
        } finally {
            Majorsilence.Forms.Printing.NativePrinting.LauncherOverride = null;
        }
    }

    [Fact]
    public void Print_to_file_writes_the_file_and_prints_nothing ()
    {
        var launched = false;
        Majorsilence.Forms.Printing.NativePrinting.LauncherOverride = _ => launched = true;
        var file = System.IO.Path.Combine (System.IO.Path.GetTempPath (), $"print-to-file-{Guid.NewGuid ():N}.pdf");

        try {
            var document = new Majorsilence.Forms.Printing.PrintDocument ();
            document.PrinterSettings.PrintToFile = true;
            document.PrinterSettings.PrintFileName = file;
            document.PrintPage += (_, e) => e.HasMorePages = false;

            Assert.Equal (file, document.Print ());
            Assert.True (System.IO.File.Exists (file));
            Assert.False (launched);
        } finally {
            Majorsilence.Forms.Printing.NativePrinting.LauncherOverride = null;
            System.IO.File.Delete (file);
        }
    }

    [Fact]
    public void A_print_that_cannot_reach_a_printer_says_so ()
    {
        Majorsilence.Forms.Printing.NativePrinting.LauncherOverride = _ => false;

        try {
            var document = new Majorsilence.Forms.Printing.PrintDocument ();
            document.PrintPage += (_, e) => e.HasMorePages = false;

            Assert.Throws<Majorsilence.Forms.Printing.InvalidPrinterException> (() => document.Print ());
        } finally {
            Majorsilence.Forms.Printing.NativePrinting.LauncherOverride = null;
        }
    }
}
