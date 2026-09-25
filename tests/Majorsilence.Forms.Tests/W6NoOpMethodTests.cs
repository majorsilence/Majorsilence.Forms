using System.ComponentModel;
using System.Drawing;
using Majorsilence.Forms.Headless;
using SkiaSharp;
using Xunit;

namespace Majorsilence.Forms.Tests;

// W6 mechanisms, sixteenth chunk: public methods with empty bodies -- a seam the stored-only and
// unraised-event baselines do not cover. A method that accepts its arguments and does nothing reads
// as working code at the call site, which is worse than one that is missing.
[Collection ("Headless")]
public class W6NoOpMethodTests
{
    // ── Control.DrawToBitmap ────────────────────────────────────────────────────────────────────────

    [Fact]
    public void DrawToBitmap_paints_the_control_and_its_children ()
    {
        HeadlessRenderer.Use ();
        using var form = new Form { Width = 300, Height = 200 };
        var panel = new Panel { Bounds = new Rectangle (0, 0, 120, 60) };
        panel.Style.BackgroundColor = SKColors.Red;

        var child = new Panel { Bounds = new Rectangle (10, 10, 30, 20) };
        child.Style.BackgroundColor = SKColors.Blue;
        panel.Controls.Add (child);

        form.Controls.Add (panel);
        form.Show ();

        // The bitmap is in DEVICE pixels, which is what the paint pipeline places children in.
        using var surface = new SKBitmap (panel.ScaledWidth, panel.ScaledHeight, SKColorType.Rgba8888, SKAlphaType.Premul);
        using var bitmap = new Majorsilence.Forms.Drawing.Bitmap (surface);

        panel.DrawToBitmap (bitmap, new Rectangle (0, 0, panel.ScaledWidth, panel.ScaledHeight));

        // The control's own background, and the child painted over it.
        Assert.Equal (SKColors.Red, surface.GetPixel (panel.LogicalToDeviceUnits (100), panel.LogicalToDeviceUnits (50)));
        Assert.Equal (SKColors.Blue, surface.GetPixel (panel.LogicalToDeviceUnits (20), panel.LogicalToDeviceUnits (15)));
    }

    [Fact]
    public void DrawToBitmap_clips_to_the_target_bounds ()
    {
        HeadlessRenderer.Use ();
        using var form = new Form { Width = 300, Height = 200 };
        var panel = new Panel { Bounds = new Rectangle (0, 0, 80, 40) };
        panel.Style.BackgroundColor = SKColors.Lime;
        form.Controls.Add (panel);
        form.Show ();

        using var surface = new SKBitmap (panel.ScaledWidth, panel.ScaledHeight, SKColorType.Rgba8888, SKAlphaType.Premul);
        using var bitmap = new Majorsilence.Forms.Drawing.Bitmap (surface);

        // Only the left half is asked for, so the right half is untouched.
        panel.DrawToBitmap (bitmap, new Rectangle (0, 0, panel.ScaledWidth / 2, panel.ScaledHeight));

        Assert.Equal (SKColors.Lime, surface.GetPixel (panel.LogicalToDeviceUnits (10), panel.LogicalToDeviceUnits (20)));
        Assert.NotEqual (SKColors.Lime, surface.GetPixel (panel.LogicalToDeviceUnits (70), panel.LogicalToDeviceUnits (20)));

        // An empty request paints nothing at all.
        using var untouched = new SKBitmap (panel.ScaledWidth, panel.ScaledHeight, SKColorType.Rgba8888, SKAlphaType.Premul);
        using var empty_target = new Majorsilence.Forms.Drawing.Bitmap (untouched);

        panel.DrawToBitmap (empty_target, Rectangle.Empty);
        Assert.NotEqual (SKColors.Lime, untouched.GetPixel (panel.LogicalToDeviceUnits (10), panel.LogicalToDeviceUnits (20)));
    }

    // ── RichTextBox redo ────────────────────────────────────────────────────────────────────────────

    [Fact]
    public void Redo_puts_back_what_Undo_reverted ()
    {
        HeadlessRenderer.Use ();
        using var box = new RichTextBox { Width = 200, Height = 80 };

        Assert.False (box.CanUndo);
        Assert.False (box.CanRedo);
        Assert.Equal (string.Empty, box.RedoActionName);

        box.AppendText ("hello");
        box.RaiseKeyPress (new KeyPressEventArgs ('!'));

        Assert.True (box.CanUndo);
        Assert.Equal ("Undo", box.UndoActionName);

        var typed = box.Text;
        box.Undo ();
        var reverted = box.Text;

        Assert.NotEqual (typed, reverted);
        Assert.True (box.CanRedo);
        Assert.Equal ("Undo", box.RedoActionName);

        box.Redo ();
        Assert.Equal (typed, box.Text);

        // The buffer toggles, so a second redo goes back again.
        box.Redo ();
        Assert.Equal (reverted, box.Text);
    }

    // ── PropertyGrid.ResetSelectedProperty ──────────────────────────────────────────────────────────

    private sealed class Settings
    {
        [DefaultValue ("Untitled")]
        public string Title { get; set; } = "Untitled";
    }

    [Fact]
    public void ResetSelectedProperty_puts_a_property_back_to_its_default ()
    {
        HeadlessRenderer.Use ();
        using var form = new Form { Size = new Size (420, 360) };
        var target = new Settings { Title = "Changed" };
        var grid = new PropertyGrid { Bounds = new Rectangle (0, 0, 360, 300), SelectedObject = target };
        form.Controls.Add (grid);
        form.Show ();

        var changes = new List<PropertyValueChangedEventArgs> ();
        grid.PropertyValueChanged += (_, e) => changes.Add (e);

        // Nothing selected: nothing happens and nothing is announced.
        grid.ResetSelectedProperty ();
        Assert.Equal ("Changed", target.Title);
        Assert.Empty (changes);

        var title = grid.VisibleRows.First (i => i.Label == "Title");
        title.Select ();

        grid.ResetSelectedProperty ();

        Assert.Equal ("Untitled", target.Title);
        Assert.Equal ("Untitled", grid.SelectedGridItem!.Value);

        var change = Assert.Single (changes);
        Assert.Equal ("Changed", change.OldValue);
    }

    // ── ColumnHeader.AutoResize ─────────────────────────────────────────────────────────────────────

    [Fact]
    public void A_column_header_resizes_itself_to_its_text_or_its_contents ()
    {
        HeadlessRenderer.Use ();
        using var form = new Form { Width = 460, Height = 300 };
        var list = new ListView { View = View.Details, Width = 380, Height = 200 };
        list.Columns.Add ("A", 300);
        list.Items.Add (new ListViewItem ("a much longer cell value than the header"));
        form.Controls.Add (list);
        form.Show ();
        PaintSurface.RenderOnForm (list, 1f).Dispose ();

        var header = list.Columns[0];

        header.AutoResize (ColumnHeaderAutoResizeStyle.HeaderSize);
        var to_header = header.Width;
        Assert.True (to_header < 300, $"header width {to_header} should have shrunk to its text");

        header.AutoResize (ColumnHeaderAutoResizeStyle.ColumnContent);
        Assert.True (header.Width > to_header, $"content width {header.Width} should exceed the header's {to_header}");

        // None leaves it alone, as upstream does.
        var before = header.Width;
        header.AutoResize (ColumnHeaderAutoResizeStyle.None);
        Assert.Equal (before, header.Width);
    }

    // ── ImageCollection.SetKeyName ──────────────────────────────────────────────────────────────────

    [Fact]
    public void SetKeyName_renames_an_image_without_moving_it ()
    {
        using var list = new ImageList ();
        var red = new SKBitmap (8, 8);
        red.Erase (SKColors.Red);
        var blue = new SKBitmap (8, 8);
        blue.Erase (SKColors.Blue);

        list.Images.Add ("first", red);
        list.Images.Add ("second", blue);

        list.Images.SetKeyName (1, "renamed");

        // The image answers to the new key, keeps its index, and the old key is gone.
        Assert.True (list.Images.ContainsKey ("renamed"));
        Assert.False (list.Images.ContainsKey ("second"));
        Assert.Equal (SKColors.Blue, list.Images["renamed"].GetPixel (1, 1));
        Assert.Equal (SKColors.Blue, list.Images[1].GetPixel (1, 1));
        Assert.Equal (SKColors.Red, list.Images[0].GetPixel (1, 1));

        Assert.Throws<ArgumentOutOfRangeException> (() => list.Images.SetKeyName (5, "nope"));
    }

    // ── Clipboard images ────────────────────────────────────────────────────────────────────────────

    [Fact]
    public void An_image_put_on_the_clipboard_comes_back_off_it ()
    {
        Clipboard.Clear ();
        Assert.False (Clipboard.ContainsImage ());
        Assert.Null (Clipboard.GetImage ());

        var pixels = new SKBitmap (4, 4);
        pixels.Erase (SKColors.Magenta);
        using var image = new Majorsilence.Forms.Drawing.Bitmap (pixels);

        Clipboard.SetImage (image);

        Assert.True (Clipboard.ContainsImage ());
        Assert.Same (image, Clipboard.GetImage ());

        Clipboard.Clear ();
        Assert.False (Clipboard.ContainsImage ());
    }
}
