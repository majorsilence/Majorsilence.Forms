using System.Drawing;
using Majorsilence.Forms.Headless;
using Xunit;

namespace Majorsilence.Forms.Tests;

/// <summary>
/// W6 mechanisms, eighth chunk: the strip layout cluster -- overflow (CanOverflow, OverflowButton,
/// ToolStripItem.Overflow/Placement), LayoutStyle/Orientation/Stretch/TextDirection,
/// ToolStripManager.Merge with MergeAction/MergeIndex/AllowMerge, and the rafting panel's rows and
/// renderer-routed backgrounds.
/// </summary>
public class W6StripLayoutTests
{
    private static ToolStrip Strip (out Form form, int width, params string[] texts)
    {
        HeadlessRenderer.Use ();
        form = new Form { Size = new Size (700, 300) };
        var strip = new ToolStrip { Bounds = new Rectangle (0, 0, width, 30), Dock = DockStyle.None, GripStyle = ToolStripGripStyle.Hidden };
        foreach (var text in texts)
            strip.Items.Add (new ToolStripButton (text));
        form.Controls.Add (strip);
        form.Show ();
        return strip;
    }

    private static ToolStripItem Item (ToolStrip strip, int index) => (ToolStripItem)strip.Items[index];

    // ── overflow ────────────────────────────────────────────────────────────────────────────────────

    [Fact]
    public void Items_past_the_edge_move_to_the_overflow_button_and_come_back_when_there_is_room ()
    {
        var strip = Strip (out var form, 150, "Button one", "Button two", "Button three", "Button four", "Button five");
        using var _ = form;
        var added = 0;
        var removed = 0;
        strip.ItemAdded += (_, _) => added++;
        strip.ItemRemoved += (_, _) => removed++;

        using var narrow = PaintSurface.Render (strip);

        var overflowed = strip.Items.Cast<ToolStripItem> ().Where (i => i.IsOnOverflow).ToList ();
        var main = strip.Items.Cast<ToolStripItem> ().Where (i => i.Placement == ToolStripItemPlacement.Main).ToList ();

        Assert.NotEmpty (overflowed);
        Assert.NotEmpty (main);
        Assert.Equal (5, strip.Items.Count);   // still the strip's items, as upstream keeps them
        Assert.Equal (overflowed, strip.OverflowButton.DropDownItems.Cast<ToolStripItem> ().ToList ());
        Assert.All (main, i => Assert.True (i.Bounds.Right <= strip.OverflowButton.Bounds.Left));
        Assert.True (strip.OverflowButton.Bounds.Right <= strip.Width);
        Assert.Equal (0, added + removed);      // the layout's moves are not the application's changes

        // Trailing items overflow first, so the main run is the leading run.
        Assert.Equal (Item (strip, 0), main[0]);
        Assert.Equal (Item (strip, 4), overflowed[^1]);

        strip.Width = 600;
        using var wide = PaintSurface.Render (strip);

        Assert.All (strip.Items.Cast<ToolStripItem> (), i => Assert.Equal (ToolStripItemPlacement.Main, i.Placement));
        Assert.Empty (strip.OverflowButton.DropDownItems);
        Assert.Equal (Rectangle.Empty, strip.OverflowButton.Bounds);
        Assert.Equal (0, added + removed);
    }

    [Fact]
    public void Overflow_Always_and_Never_and_CanOverflow_off_are_honoured ()
    {
        var strip = Strip (out var form, 150, "Button one", "Button two", "Button three", "Button four");
        using var _ = form;
        Item (strip, 0).Overflow = ToolStripItemOverflow.Always;
        Item (strip, 3).Overflow = ToolStripItemOverflow.Never;

        using var narrow = PaintSurface.Render (strip);
        Assert.True (Item (strip, 0).IsOnOverflow);
        Assert.Equal (ToolStripItemPlacement.Main, Item (strip, 3).Placement);

        strip.CanOverflow = false;
        using var clipped = PaintSurface.Render (strip);
        Assert.All (strip.Items.Cast<ToolStripItem> (), i => Assert.Equal (ToolStripItemPlacement.Main, i.Placement));
        Assert.Empty (strip.OverflowButton.DropDownItems);
    }

    [Fact]
    public void An_item_removed_while_overflowed_leaves_the_overflow_drop_down ()
    {
        var strip = Strip (out var form, 120, "Button one", "Button two", "Button three", "Button four");
        using var _ = form;
        using var narrow = PaintSurface.Render (strip);
        var last = Item (strip, 3);
        Assert.True (last.IsOnOverflow);

        strip.Items.Remove (last);

        Assert.DoesNotContain (last, strip.OverflowButton.DropDownItems);
        Assert.Equal (3, strip.Items.Count);
        using var again = PaintSurface.Render (strip);
        Assert.DoesNotContain (last, strip.Items);
    }

    // ── layout styles ───────────────────────────────────────────────────────────────────────────────

    [Fact]
    public void VerticalStackWithOverflow_stacks_items_top_to_bottom_and_pins_trailing_ones_to_the_bottom ()
    {
        var strip = Strip (out var form, 80, "One", "Two", "Three");
        using var _ = form;
        strip.Height = 300;
        strip.LayoutStyle = ToolStripLayoutStyle.VerticalStackWithOverflow;
        Item (strip, 2).Alignment = ToolStripItemAlignment.Right;

        Assert.Equal (Orientation.Vertical, strip.Orientation);
        using var painted = PaintSurface.Render (strip);

        var one = Item (strip, 0).Bounds;
        var two = Item (strip, 1).Bounds;
        var three = Item (strip, 2).Bounds;
        Assert.Equal (one.Left, two.Left);
        Assert.Equal (one.Bottom, two.Top);
        Assert.True (one.Width >= two.Width && one.Width > one.Height, "items should be full-width rows");
        Assert.True (three.Top > two.Bottom, "a trailing item sits at the bottom");
        Assert.True (three.Bottom >= strip.Height - 2, $"the trailing item should touch the bottom edge ({three.Bottom} of {strip.Height})");

        // StackWithOverflow follows the docked edge.
        strip.LayoutStyle = ToolStripLayoutStyle.StackWithOverflow;
        Assert.Equal (Orientation.Horizontal, strip.Orientation);
        strip.Dock = DockStyle.Left;
        Assert.Equal (Orientation.Vertical, strip.Orientation);
    }

    [Fact]
    public void Flow_wraps_items_into_rows_without_overflow ()
    {
        var strip = Strip (out var form, 150, "Button one", "Button two", "Button three", "Button four");
        using var _ = form;
        strip.Height = 80;
        strip.LayoutStyle = ToolStripLayoutStyle.Flow;

        using var painted = PaintSurface.Render (strip);

        Assert.True (Item (strip, 3).Bounds.Top > Item (strip, 0).Bounds.Top, "the last item should be on a later row");
        Assert.All (strip.Items.Cast<ToolStripItem> (), i => Assert.Equal (ToolStripItemPlacement.Main, i.Placement));
        Assert.All (strip.Items.Cast<ToolStripItem> (), i => Assert.True (i.Bounds.Right <= strip.Width));
    }

    [Fact]
    public void TextDirection_measures_and_draws_vertical_text ()
    {
        var strip = Strip (out var form, 300, "Alignment");
        using var _ = form;
        var item = Item (strip, 0);
        var horizontal = item.GetPreferredSize (Size.Empty);
        using var flat = PaintSurface.Render (strip);

        item.TextDirection = ToolStripTextDirection.Vertical90;
        var vertical = item.GetPreferredSize (Size.Empty);
        using var rotated = PaintSurface.Render (strip);

        Assert.True (vertical.Width < horizontal.Width, "vertical text should be narrower");
        Assert.True (vertical.Height > horizontal.Height, "vertical text should be taller");

        static int Differences (SkiaSharp.SKBitmap a, SkiaSharp.SKBitmap b)
        {
            var n = 0;
            for (var y = 0; y < a.Height; y++)
                for (var x = 0; x < a.Width; x++)
                    if (a.GetPixel (x, y) != b.GetPixel (x, y))
                        n++;
            return n;
        }

        Assert.True (Differences (flat, rotated) > 0, "the rotated caption should paint differently");

        // Inherit takes the strip's direction.
        item.TextDirection = ToolStripTextDirection.Inherit;
        strip.TextDirection = ToolStripTextDirection.Vertical270;
        Assert.Equal (ToolStripTextDirection.Vertical270, strip.TextDirectionFor (item));
        Assert.True (item.GetPreferredSize (Size.Empty).Height > horizontal.Height);
    }

    // ── merge ───────────────────────────────────────────────────────────────────────────────────────

    private static ToolStripMenuItem Menu (string text, MergeAction action = MergeAction.Append, int index = -1)
        => new (text) { MergeAction = action, MergeIndex = index };

    [Fact]
    public void Merge_appends_inserts_replaces_and_removes_by_MergeAction_and_RevertMerge_restores ()
    {
        HeadlessRenderer.Use ();
        using var target = new MenuStrip ();
        using var source = new MenuStrip ();
        var file = Menu ("File");
        var edit = Menu ("Edit");
        var help = Menu ("Help");
        target.Items.Add (file);
        target.Items.Add (edit);
        target.Items.Add (help);

        var tools = Menu ("Tools");
        var view = Menu ("View", MergeAction.Insert, 1);
        var edit2 = Menu ("Edit", MergeAction.Replace);
        var help2 = Menu ("Help", MergeAction.Remove);
        source.Items.Add (tools);
        source.Items.Add (view);
        source.Items.Add (edit2);
        source.Items.Add (help2);

        Assert.True (ToolStripManager.Merge (source, target));

        Assert.Equal (["File", "View", "Edit", "Tools"], target.Items.Cast<MenuItem> ().Select (i => i.Text));
        Assert.Same (edit2, target.Items[2]);
        Assert.Equal ([help2], source.Items.Cast<MenuItem> ());   // a Remove stays home

        Assert.True (ToolStripManager.RevertMerge (target));

        Assert.Equal ([file, edit, help], target.Items.Cast<MenuItem> ());
        Assert.Equal ([tools, view, edit2, help2], source.Items.Cast<MenuItem> ());
        Assert.False (ToolStripManager.RevertMerge (target));

        source.AllowMerge = false;
        Assert.False (ToolStripManager.Merge (source, target));
        Assert.Equal (3, target.Items.Count);
    }

    [Fact]
    public void MatchOnly_merges_a_matching_item_drop_down ()
    {
        HeadlessRenderer.Use ();
        using var target = new MenuStrip ();
        using var source = new MenuStrip ();
        var target_file = Menu ("File");
        target_file.DropDownItems.Add (Menu ("New"));
        target_file.DropDownItems.Add (Menu ("Exit"));
        target.Items.Add (target_file);

        var source_file = Menu ("File", MergeAction.MatchOnly);
        var recent = Menu ("Recent", MergeAction.Insert, 1);
        source_file.DropDownItems.Add (recent);
        source.Items.Add (source_file);

        Assert.True (ToolStripManager.Merge (source, target));
        Assert.Equal (["New", "Recent", "Exit"], target_file.DropDownItems.Cast<MenuItem> ().Select (i => i.Text));
        Assert.Single (target.Items);           // the matching item itself did not move
        Assert.Empty (source_file.DropDownItems);

        Assert.True (ToolStripManager.RevertMerge (target, source));
        Assert.Equal (["New", "Exit"], target_file.DropDownItems.Cast<MenuItem> ().Select (i => i.Text));
        Assert.Equal ([recent], source_file.DropDownItems.Cast<MenuItem> ());
    }

    // ── rafting panel ───────────────────────────────────────────────────────────────────────────────

    [Fact]
    public void Panel_rows_honour_Stretch_RowMargin_and_the_row_Margin ()
    {
        HeadlessRenderer.Use ();
        using var form = new Form { Size = new Size (500, 300) };
        var panel = new ToolStripPanel { Bounds = new Rectangle (0, 0, 400, 120), Dock = DockStyle.None };
        var menu = new MenuStrip ();
        menu.Items.Add (new ToolStripMenuItem ("File"));
        var toolbar = new ToolStrip { GripStyle = ToolStripGripStyle.Hidden };
        toolbar.Items.Add (new ToolStripButton ("One"));
        toolbar.Items.Add (new ToolStripButton ("Two"));
        panel.Controls.Add (menu);
        panel.Controls.Add (toolbar);
        form.Controls.Add (panel);
        form.Show ();
        panel.PerformLayout ();

        var area = panel.ClientRectangle;
        Assert.True (menu.Stretch);
        Assert.Equal (area.Width - panel.RowMargin.Horizontal, menu.Width);
        Assert.False (toolbar.Stretch);
        Assert.True (toolbar.Width < menu.Width, $"a ToolStrip keeps its preferred width ({toolbar.Width} vs {menu.Width})");
        Assert.Equal (2, panel.Rows.Length);
        Assert.Equal (panel.Rows[0].Bounds.Bottom, panel.Rows[1].Bounds.Top);
        Assert.Equal (menu.Bottom, toolbar.Top);

        toolbar.Stretch = true;
        panel.PerformLayout ();
        Assert.Equal (menu.Width, toolbar.Width);

        panel.RowMargin = new Padding (10, 5, 0, 0);
        panel.Rows[1].Margin = new Padding (0, 4, 0, 0);
        panel.PerformLayout ();
        Assert.Equal (10, menu.Left);
        Assert.Equal (5, menu.Top);
        Assert.Equal (menu.Bottom + 4, toolbar.Top);
        Assert.Equal (area.Width - 10, menu.Width);
    }

    [Fact]
    public void Panel_and_content_panel_backgrounds_go_through_the_renderer ()
    {
        HeadlessRenderer.Use ();
        using var form = new Form { Size = new Size (300, 300) };
        var panel = new ToolStripPanel { Bounds = new Rectangle (0, 0, 100, 40), Dock = DockStyle.None };
        var content = new ToolStripContentPanel { Bounds = new Rectangle (0, 50, 100, 40), Dock = DockStyle.None };
        form.Controls.Add (panel);
        form.Controls.Add (content);
        var renderer = new ToolStripProfessionalRenderer ();
        var seen = new List<string> ();
        renderer.RenderToolStripPanelBackground += (_, e) => {
            seen.Add ("panel");
            Assert.Same (panel, e.ToolStripPanel);
            e.Graphics.Canvas!.Clear (SkiaSharp.SKColors.Red);
            e.Handled = true;
        };
        renderer.RenderToolStripContentPanelBackground += (_, e) => {
            seen.Add ("content");
            Assert.Same (content, e.ToolStripContentPanel);
            e.Graphics.Canvas!.Clear (SkiaSharp.SKColors.Blue);
            e.Handled = true;
        };

        panel.Renderer = renderer;
        content.Renderer = renderer;
        using var painted_panel = PaintSurface.Render (panel);
        using var painted_content = PaintSurface.Render (content);

        Assert.Equal (["panel", "content"], seen);
        Assert.Equal (SkiaSharp.SKColors.Red, painted_panel.GetPixel (10, 10));
        Assert.Equal (SkiaSharp.SKColors.Blue, painted_content.GetPixel (10, 10));

        // RenderMode picks the renderer when none is assigned: the manager's, which is asked too.
        panel.Renderer = null;
        var manager_seen = 0;
        var manager_renderer = new ToolStripProfessionalRenderer ();
        manager_renderer.RenderToolStripPanelBackground += (_, _) => manager_seen++;
        var previous = ToolStripManager.Renderer;
        ToolStripManager.Renderer = manager_renderer;

        try {
            using var via_manager = PaintSurface.Render (panel);
            Assert.Equal (1, manager_seen);
        } finally {
            ToolStripManager.Renderer = previous;
        }
    }
}
