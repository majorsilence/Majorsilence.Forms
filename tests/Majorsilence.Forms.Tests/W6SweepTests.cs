using System.ComponentModel;
using System.Drawing;
using Majorsilence.Forms.Headless;
using SkiaSharp;
using Xunit;

namespace Majorsilence.Forms.Tests;

// W6 mechanisms, fourteenth chunk: the small stored-only clusters left after the big ones -- the
// mask engine's input and clipboard options, the picture box's failure image and blocking switch,
// the grid's combo-cell settings, the error provider's icon, and the graphics page transform.
[Collection ("Headless")]
public class W6SweepTests
{
    // ── MaskedTextBox ───────────────────────────────────────────────────────────────────────────────

    [Fact]
    public void RejectInputOnFirstFailure_stops_at_the_first_refused_character ()
    {
        // A digits-only mask fed a string with a letter in the middle.
        using var skipping = new MaskedTextBox { Mask = "00000", RejectInputOnFirstFailure = false };
        skipping.Text = "12x34";

        // Off (the default): the letter is skipped and the rest still goes in.
        Assert.Equal ("1234", Digits (skipping.Text));

        using var stopping = new MaskedTextBox { Mask = "00000", RejectInputOnFirstFailure = true };
        stopping.Text = "12x34";

        // On: everything from the refusal is dropped.
        Assert.Equal ("12", Digits (stopping.Text));
    }

    private static string Digits (string text) => new string (text.Where (char.IsDigit).ToArray ());

    [Fact]
    public void CutCopyMaskFormat_shapes_what_reaches_the_clipboard ()
    {
        using var box = new MaskedTextBox { Mask = "(000) 000", CutCopyMaskFormat = MaskFormat.IncludeLiterals };
        box.Text = "5551234";
        box.SelectAll ();
        box.Copy ();

        var with_literals = Clipboard.GetText ();
        Assert.Contains ("(", with_literals);

        box.CutCopyMaskFormat = MaskFormat.ExcludePromptAndLiterals;
        box.SelectAll ();
        box.Copy ();

        var bare = Clipboard.GetText ();
        Assert.DoesNotContain ("(", bare);
        Assert.Contains ("555", bare);
    }

    // ── PictureBox ──────────────────────────────────────────────────────────────────────────────────

    [Fact]
    public void ErrorImage_is_shown_when_a_load_fails ()
    {
        HeadlessRenderer.Use ();
        using var box = new PictureBox { Width = 60, Height = 60 };
        var missing = Path.Combine (Path.GetTempPath (), "majorsilence-no-such-image-" + Guid.NewGuid ().ToString ("N") + ".png");

        // With no ErrorImage a failed load leaves the box empty, as it always did.
        box.ImageLocation = missing;
        Assert.True (box.IsErrored);
        Assert.Null (box.SKImage);

        var marker = new SKBitmap (8, 8);
        marker.Erase (SKColors.Magenta);
        box.ErrorImage = new Majorsilence.Forms.Drawing.Bitmap (marker);

        box.ImageLocation = missing + "b";

        Assert.True (box.IsErrored);
        Assert.NotNull (box.SKImage);
        Assert.Equal (SKColors.Magenta, box.SKImage!.GetPixel (2, 2));
    }

    [Fact]
    public void WaitOnLoad_decides_whether_assigning_a_location_blocks ()
    {
        HeadlessRenderer.Use ();
        var file = Path.Combine (Path.GetTempPath (), "majorsilence-sweep-" + Guid.NewGuid ().ToString ("N") + ".png");

        using (var bitmap = new SKBitmap (8, 8)) {
            bitmap.Erase (SKColors.Lime);
            using var data = bitmap.Encode (SKEncodedImageFormat.Png, 100);
            using var stream = File.Create (file);
            data.SaveTo (stream);
        }

        try {
            // The default blocks, so the image is there on the next line.
            using var blocking = new PictureBox { Width = 40, Height = 40 };
            Assert.True (blocking.WaitOnLoad);
            blocking.ImageLocation = file;
            Assert.NotNull (blocking.SKImage);

            // Cleared, the assignment starts a background load and returns before it finishes; the
            // placeholder is what is on screen meanwhile.
            var placeholder = new SKBitmap (4, 4);
            placeholder.Erase (SKColors.Blue);

            using var background = new PictureBox {
                Width = 40, Height = 40,
                WaitOnLoad = false,
                InitialImage = new Majorsilence.Forms.Drawing.Bitmap (placeholder),
            };

            background.ImageLocation = file;
            Assert.Equal (SKColors.Blue, background.SKImage!.GetPixel (1, 1));
            background.CancelAsync ();
        } finally {
            File.Delete (file);
        }
    }

    // ── DataGridView combo cells ────────────────────────────────────────────────────────────────────

    private sealed class EditableGrid : DataGridView
    {
        internal ComboBox? OpenEditor ()
        {
            BeginEdit (true);
            return Controls.OfType<ComboBox> ().FirstOrDefault ();
        }
    }

    private static EditableGrid ComboGrid (out Form form, bool autoComplete, bool sorted)
    {
        HeadlessRenderer.Use ();
        form = new Form { Width = 460, Height = 300 };
        var grid = new EditableGrid { Width = 380, Height = 200, AllowUserToAddRows = false };

        var column = new DataGridViewComboBoxColumn { HeaderText = "Pick", Width = 120, AutoComplete = autoComplete, Sorted = sorted };
        column.Items.Add ("zeta");
        column.Items.Add ("alpha");
        grid.Columns.Add (column);

        var row = new DataGridViewRow ();
        row.Cells.Add (new DataGridViewComboBoxCell { Value = "zeta" });
        grid.Rows.Add (row);

        form.Controls.Add (grid);
        form.Show ();
        PaintSurface.RenderOnForm (grid, 1f).Dispose ();
        grid.SelectedRowIndex = 0;
        grid.SelectedColumnIndex = 0;
        return grid;
    }

    [Fact]
    public void The_combo_columns_AutoComplete_and_Sorted_reach_the_editor ()
    {
        var plain = ComboGrid (out var plain_form, autoComplete: false, sorted: false);

        using (plain_form) {
            var editor = plain.OpenEditor ();
            Assert.NotNull (editor);
            Assert.Equal (ComboBoxStyle.DropDownList, editor!.DropDownStyle);
            Assert.Equal (AutoCompleteMode.None, editor.AutoCompleteMode);
            Assert.False (editor.Sorted);
            Assert.Equal ("zeta", editor.Items[0]);
        }

        var fancy = ComboGrid (out var fancy_form, autoComplete: true, sorted: true);

        using (fancy_form) {
            var editor = fancy.OpenEditor ();
            Assert.NotNull (editor);

            // Autocomplete means the editor takes typed text, and completes it from its own list.
            Assert.Equal (ComboBoxStyle.DropDown, editor!.DropDownStyle);
            Assert.Equal (AutoCompleteMode.SuggestAppend, editor.AutoCompleteMode);
            Assert.Equal (AutoCompleteSource.ListItems, editor.AutoCompleteSource);

            // Sorted reorders the list the editor shows.
            Assert.True (editor.Sorted);
            Assert.Equal ("alpha", editor.Items[0]);
        }
    }

    [Fact]
    public void A_cells_DisplayStyleForCurrentCellOnly_wins_over_its_columns ()
    {
        var grid = ComboGrid (out var form, autoComplete: false, sorted: false);

        using (form) {
            var column = (DataGridViewComboBoxColumn) grid.Columns[0];
            var cell = (DataGridViewComboBoxCell) grid.Rows[0].Cells[0];

            grid.Rows.Add (new DataGridViewRow ());
            grid.SelectedRowIndex = 1;

            // The column says "every cell", so the non-current row still draws its button.
            column.DisplayStyleForCurrentCellOnly = false;
            var with_button = ButtonInk (grid);

            // The cell overrides that for itself.
            cell.DisplayStyleForCurrentCellOnly = true;
            var without = ButtonInk (grid);

            Assert.True (without < with_button, $"button ink {without} should be less than {with_button}");
        }
    }

    // How much ink the first row's cell carries, which the drop-down button adds to.
    private static int ButtonInk (DataGridView grid)
    {
        using var bitmap = PaintSurface.RenderOnForm (grid, 1f);
        var bounds = grid.GetCellBounds (0, 0);
        var background = bitmap.GetPixel (bounds.Left + 2, bounds.Top + 2);
        var ink = 0;

        for (var y = bounds.Top + 1; y < Math.Min (bitmap.Height, bounds.Bottom - 1); y++)
            for (var x = bounds.Left + 1; x < Math.Min (bitmap.Width, bounds.Right - 1); x++)
                if (bitmap.GetPixel (x, y) != background)
                    ink++;

        return ink;
    }

    // ── ErrorProvider ───────────────────────────────────────────────────────────────────────────────

    [Fact]
    public void The_error_providers_Icon_replaces_the_drawn_glyph ()
    {
        HeadlessRenderer.Use ();
        using var form = new Form { Size = new Size (300, 200) };
        var panel = new Panel { Bounds = new Rectangle (0, 0, 280, 160) };
        var box = new TextBox { Bounds = new Rectangle (20, 20, 100, 24) };
        panel.Controls.Add (box);
        form.Controls.Add (panel);
        form.Show ();

        using var provider = new ErrorProvider ();
        provider.SetError (box, "wrong");

        var drawn = new SKBitmap (16, 16);
        drawn.Erase (SKColors.Cyan);

        using (var before = PaintSurface.Render (panel))
            Assert.Equal (0, CountIn (before, SKColors.Cyan));

        provider.Icon = new Majorsilence.Forms.Drawing.Icon (drawn);

        using (var after = PaintSurface.Render (panel))
            Assert.True (CountIn (after, SKColors.Cyan) > 0, "the supplied icon was not drawn");
    }

    // ── Graphics page transform ─────────────────────────────────────────────────────────────────────

    [Fact]
    public void PageUnit_and_PageScale_scale_what_is_drawn ()
    {
        using var surface = new SKBitmap (200, 200, SKColorType.Rgba8888, SKAlphaType.Unpremul);
        using var canvas = new SKCanvas (surface);
        canvas.Clear (SKColors.White);

        using var graphics = Majorsilence.Forms.Drawing.Graphics.FromImage (new Majorsilence.Forms.Drawing.Bitmap (surface));
        using var brush = new Majorsilence.Forms.Drawing.SolidBrush (Color.Red);

        Assert.Equal (Majorsilence.Forms.Drawing.GraphicsUnit.Pixel, graphics.PageUnit);
        Assert.Equal (1f, graphics.PageTransformScale);

        // A point is 96/72 of a pixel, so a 10-unit square covers more than 10 pixels.
        graphics.PageUnit = Majorsilence.Forms.Drawing.GraphicsUnit.Point;
        Assert.Equal (96f / 72f, graphics.PageTransformScale, 4);

        // And PageScale multiplies on top, without compounding the unit twice.
        graphics.PageScale = 3f;
        Assert.Equal (96f / 72f * 3f, graphics.PageTransformScale, 4);

        graphics.PageUnit = Majorsilence.Forms.Drawing.GraphicsUnit.Inch;
        Assert.Equal (96f * 3f, graphics.PageTransformScale, 4);

        // Back to pixels at scale 1 and the transform is the identity again.
        graphics.PageUnit = Majorsilence.Forms.Drawing.GraphicsUnit.Pixel;
        graphics.PageScale = 1f;
        Assert.Equal (1f, graphics.PageTransformScale);

        // A 10x10 fill at 2x covers 20x20 device pixels.
        graphics.PageScale = 2f;
        graphics.FillRectangle (brush, 0, 0, 10, 10);

        Assert.Equal (SKColors.Red, surface.GetPixel (18, 18));
        Assert.NotEqual (SKColors.Red, surface.GetPixel (21, 21));
    }

    private static int CountIn (SKBitmap bitmap, SKColor colour)
    {
        var count = 0;

        for (var y = 0; y < bitmap.Height; y++)
            for (var x = 0; x < bitmap.Width; x++) {
                var p = bitmap.GetPixel (x, y);

                if (p.Red == colour.Red && p.Green == colour.Green && p.Blue == colour.Blue)
                    count++;
            }

        return count;
    }
}
