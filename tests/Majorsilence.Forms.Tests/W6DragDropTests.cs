using System.Drawing;
using Majorsilence.Forms.Headless;
using Xunit;

namespace Majorsilence.Forms.Tests;

/// <summary>
/// W6 mechanisms, sixth chunk: the in-process drag-and-drop pipeline behind DoDragDrop, its
/// consumers (ToolStripItem drag events and Alt-drag reordering, ListView.InsertionMark and
/// ListViewItem.Position, RichTextBox.EnableAutoDragDrop), MenuItem owner draw, and the DataGridView
/// divider hit-test in device coordinates (RC-8).
/// </summary>
public class W6DragDropTests
{
    // Window-device coordinates of a point inside a control, the way the backend reports them.
    private static Point WindowPoint (Control control, int x, int y)
    {
        var p = new Point (x, y);

        for (var c = control; c.Parent is not null; c = c.Parent)
            p.Offset (c.Left, c.Top);

        return new Point (control.LogicalToDeviceUnits (p.X), control.LogicalToDeviceUnits (p.Y));
    }

    private static (Form form, Panel source, Panel target) DragScene ()
    {
        HeadlessRenderer.Use ();
        var form = new Form { Size = new Size (400, 300) };
        var source = new Panel { Bounds = new Rectangle (10, 10, 100, 100) };
        var target = new Panel { Bounds = new Rectangle (200, 10, 150, 150), AllowDrop = true };
        form.Controls.Add (source);
        form.Controls.Add (target);
        form.Show ();
        return (form, source, target);
    }

    [Fact]
    public void DoDragDrop_blocks_until_the_drop_and_returns_the_effect_the_target_chose ()
    {
        var (form, source, target) = DragScene ();
        using var _ = form;
        var entered = 0;
        var over = 0;
        string? dropped = null;
        var feedback = new List<DragDropEffects> ();
        var queries = 0;

        target.DragEnter += (_, e) => { entered++; Assert.Equal (DragDropEffects.None, e.Effect); e.Effect = DragDropEffects.Copy; };
        target.DragOver += (_, e) => { over++; Assert.Equal (DragDropEffects.Copy, e.Effect); };
        target.DragDrop += (_, e) => {
            dropped = e.Data!.GetData (DataFormats.Text.Name) as string;
            Assert.True (e.Data.GetDataPresent (typeof (string)));
            Assert.Equal (DragDropEffects.Copy, e.Effect);
        };
        source.GiveFeedback += (_, e) => feedback.Add (e.Effect);
        source.QueryContinueDrag += (_, _) => queries++;

        var inside = WindowPoint (target, 20, 20);
        var further = WindowPoint (target, 40, 40);

        // The pointer events arrive from the message loop DoDragDrop runs, as they do in a real drag.
        Application.RunOnUIThread (() => {
            form.HandlePointerMoved (MouseButtons.Left, inside.X, inside.Y, Keys.None);
            form.HandlePointerMoved (MouseButtons.Left, further.X, further.Y, Keys.None);
            form.HandlePointerReleased (MouseButtons.Left, further.X, further.Y, Keys.None);
        });

        var effect = source.DoDragDrop ("hello", DragDropEffects.Copy | DragDropEffects.Move);

        Assert.Equal (DragDropEffects.Copy, effect);
        Assert.Equal (1, entered);
        Assert.Equal (2, over);             // the second move, and the release's own position
        Assert.Equal ("hello", dropped);
        Assert.Contains (DragDropEffects.Copy, feedback);
        Assert.True (queries >= 3);
        Assert.Null (DragDropSession.Active);
    }

    [Fact]
    public void Leaving_a_target_raises_DragLeave_and_a_target_that_sets_no_effect_gets_no_drop ()
    {
        var (form, source, target) = DragScene ();
        using var _ = form;
        var left = 0;
        var dropped = 0;
        target.DragLeave += (_, _) => left++;
        target.DragDrop += (_, _) => dropped++;

        var session = DragDropSession.Begin (source, "x", DragDropEffects.Move);
        var inside = WindowPoint (target, 5, 5);
        var outside = WindowPoint (source, 5, 5);

        form.HandlePointerMoved (MouseButtons.Left, inside.X, inside.Y, Keys.None);
        form.HandlePointerMoved (MouseButtons.Left, outside.X, outside.Y, Keys.None);
        Assert.Equal (1, left);

        form.HandlePointerMoved (MouseButtons.Left, inside.X, inside.Y, Keys.None);
        form.HandlePointerReleased (MouseButtons.Left, inside.X, inside.Y, Keys.None);

        Assert.Equal (0, dropped);          // Effect stayed None: nothing to drop
        Assert.Equal (2, left);             // a refused drop leaves the target
        Assert.Equal (DragDropEffects.None, session.Result);
    }

    [Fact]
    public void Escape_cancels_through_QueryContinueDrag_which_the_source_may_overrule ()
    {
        var (form, source, target) = DragScene ();
        using var _ = form;
        var left = 0;
        target.DragEnter += (_, e) => e.Effect = DragDropEffects.Move;
        target.DragLeave += (_, _) => left++;
        var inside = WindowPoint (target, 5, 5);

        var cancelled = DragDropSession.Begin (source, "x", DragDropEffects.Move);
        form.HandlePointerMoved (MouseButtons.Left, inside.X, inside.Y, Keys.None);
        Assert.True (form.HandleKeyDown (Keys.Escape));
        Assert.Equal (1, left);
        Assert.Equal (DragDropEffects.None, cancelled.Result);

        source.QueryContinueDrag += (_, e) => { if (e.EscapePressed) e.Action = DragAction.Continue; };
        var kept = DragDropSession.Begin (source, "x", DragDropEffects.Move);
        form.HandlePointerMoved (MouseButtons.Left, inside.X, inside.Y, Keys.None);
        form.HandleKeyDown (Keys.Escape);
        Assert.False (kept.Completion.IsCompleted);
        Assert.Same (kept, DragDropSession.Active);

        form.HandlePointerReleased (MouseButtons.Left, inside.X, inside.Y, Keys.None);
        Assert.Equal (DragDropEffects.Move, kept.Result);
    }

    [Fact]
    public void The_form_is_the_target_when_no_control_under_the_pointer_accepts_drops ()
    {
        var (form, source, target) = DragScene ();
        using var _ = form;
        target.AllowDrop = false;
        form.AllowDrop = true;
        var entered = 0;
        var dropped = 0;
        form.DragEnter += (_, e) => { entered++; e.Effect = DragDropEffects.Link; };
        form.DragDrop += (_, _) => dropped++;

        var session = DragDropSession.Begin (source, "x", DragDropEffects.Link);
        var at = WindowPoint (target, 5, 5);
        form.HandlePointerMoved (MouseButtons.Left, at.X, at.Y, Keys.None);
        form.HandlePointerReleased (MouseButtons.Left, at.X, at.Y, Keys.None);

        Assert.Equal ((1, 1), (entered, dropped));
        Assert.Equal (DragDropEffects.Link, session.Result);
    }

    // ── ToolStripItem ───────────────────────────────────────────────────────────────────────────────

    private static (Form form, ToolStrip strip, ToolStripButton a, ToolStripButton b, ToolStripButton c, Panel source) StripScene ()
    {
        HeadlessRenderer.Use ();
        var form = new Form { Size = new Size (500, 300) };
        var strip = new ToolStrip { Bounds = new Rectangle (0, 0, 480, 30), AllowDrop = true, Dock = DockStyle.None };
        var a = new ToolStripButton ("A");
        var b = new ToolStripButton ("B");
        var c = new ToolStripButton ("C");
        strip.Items.Add (a);
        strip.Items.Add (b);
        strip.Items.Add (c);
        var source = new Panel { Bounds = new Rectangle (10, 100, 100, 100) };
        form.Controls.Add (strip);
        form.Controls.Add (source);
        form.Show ();
        using var _ = PaintSurface.Render (strip);   // lays the items out
        return (form, strip, a, b, c, source);
    }

    private static Point ItemCentre (ToolStripButton item)
        => WindowPoint ((Control)item.OwnerControl!, item.Bounds.Left + item.Bounds.Width / 2, item.Bounds.Top + item.Bounds.Height / 2);

    [Fact]
    public void A_strip_forwards_the_drag_to_the_item_under_the_pointer_that_accepts_drops ()
    {
        var (form, strip, a, b, _, source) = StripScene ();
        using var _f = form;
        b.AllowDrop = true;
        var log = new List<string> ();
        b.DragEnter += (_, e) => { log.Add ("enter"); e.Effect = DragDropEffects.Copy; };
        b.DragOver += (_, _) => log.Add ("over");
        b.DragLeave += (_, _) => log.Add ("leave");
        b.DragDrop += (_, e) => log.Add ("drop:" + e.Data!.GetData (DataFormats.Text.Name));
        a.DragEnter += (_, _) => log.Add ("A-enter");   // A does not AllowDrop: never offered

        var session = DragDropSession.Begin (source, "payload", DragDropEffects.Copy);
        var on_a = ItemCentre (a);
        var on_b = ItemCentre (b);

        form.HandlePointerMoved (MouseButtons.Left, on_a.X, on_a.Y, Keys.None);
        form.HandlePointerMoved (MouseButtons.Left, on_b.X, on_b.Y, Keys.None);
        form.HandlePointerMoved (MouseButtons.Left, on_b.X + 2, on_b.Y, Keys.None);
        form.HandlePointerMoved (MouseButtons.Left, on_a.X, on_a.Y, Keys.None);
        form.HandlePointerMoved (MouseButtons.Left, on_b.X, on_b.Y, Keys.None);
        form.HandlePointerReleased (MouseButtons.Left, on_b.X, on_b.Y, Keys.None);

        Assert.Equal (["enter", "over", "leave", "enter", "over", "drop:payload"], log);
        Assert.Equal (DragDropEffects.Copy, session.Result);
    }

    [Fact]
    public void An_item_that_starts_a_drag_gets_GiveFeedback_and_QueryContinueDrag ()
    {
        var (form, strip, a, _, _, source) = StripScene ();
        using var _f = form;
        source.AllowDrop = true;
        source.DragEnter += (_, e) => e.Effect = DragDropEffects.Move;
        var feedback = 0;
        var queries = 0;
        a.GiveFeedback += (_, _) => feedback++;
        a.QueryContinueDrag += (_, _) => queries++;
        var at = WindowPoint (source, 10, 10);

        Application.RunOnUIThread (() => {
            form.HandlePointerMoved (MouseButtons.Left, at.X, at.Y, Keys.None);
            form.HandlePointerReleased (MouseButtons.Left, at.X, at.Y, Keys.None);
        });

        Assert.Equal (DragDropEffects.Move, a.DoDragDrop ("x", DragDropEffects.Move));
        Assert.True (feedback >= 1);
        Assert.True (queries >= 2);
    }

    [Fact]
    public void Alt_dragging_an_item_reorders_the_strip_when_AllowItemReorder_is_set ()
    {
        var (form, strip, a, b, c, _) = StripScene ();
        using var _f = form;
        var on_a = a.Bounds.Location + new Size (a.Bounds.Width / 2, a.Bounds.Height / 2);

        // Reordering off: an Alt-drag is just a mouse drag.
        strip.RaiseMouseDown (new MouseEventArgs (MouseButtons.Left, 1, on_a.X, on_a.Y, Point.Empty, keyData: Keys.Alt));
        strip.RaiseMouseMove (new MouseEventArgs (MouseButtons.Left, 0, on_a.X + 20, on_a.Y, Point.Empty, keyData: Keys.Alt));
        Assert.Null (DragDropSession.Active);
        strip.RaiseMouseUp (new MouseEventArgs (MouseButtons.Left, 1, on_a.X + 20, on_a.Y, 0));

        strip.AllowItemReorder = true;
        strip.RaiseMouseDown (new MouseEventArgs (MouseButtons.Left, 1, on_a.X, on_a.Y, Point.Empty, keyData: Keys.Alt));
        strip.RaiseMouseMove (new MouseEventArgs (MouseButtons.Left, 0, on_a.X + 20, on_a.Y, Point.Empty, keyData: Keys.Alt));
        Assert.NotNull (DragDropSession.Active);

        var on_c = ItemCentre (c);
        form.HandlePointerMoved (MouseButtons.Left, on_c.X, on_c.Y, Keys.Alt);
        form.HandlePointerReleased (MouseButtons.Left, on_c.X, on_c.Y, Keys.Alt);

        Assert.Equal (["B", "A", "C"], strip.Items.Select (i => i.Text));
        Assert.Null (DragDropSession.Active);
    }

    // ── ListView insertion mark and item placement ──────────────────────────────────────────────────

    [Fact]
    public void The_insertion_mark_sits_beside_its_item_and_NearestIndex_answers_from_the_layout ()
    {
        HeadlessRenderer.Use ();
        using var list = new ListView { Size = new Size (300, 200), View = View.Details };
        list.Columns.Add ("A", 200);
        list.Items.Add ("one");
        list.Items.Add ("two");
        list.Items.Add ("three");
        using var laid_out = PaintSurface.Render (list);

        Assert.Equal (Rectangle.Empty, list.InsertionMark.Bounds);

        var second = list.Items[1].Bounds;
        Assert.Equal (1, list.InsertionMark.NearestIndex (new Point (second.Left + 5, second.Top + 2)));
        Assert.Equal (2, list.InsertionMark.NearestIndex (new Point (second.Left + 5, second.Bottom + 200)));

        list.InsertionMark.Index = 1;
        list.InsertionMark.Color = Color.Red;
        Assert.Equal (second.Top, list.InsertionMark.Bounds.Top);

        list.InsertionMark.AppearsAfterItem = true;
        Assert.Equal (second.Bottom, list.InsertionMark.Bounds.Bottom);

        using var painted = PaintSurface.Render (list);
        var mark = list.InsertionMark.DeviceBounds;
        Assert.Equal (Color.Red.ToSKColor (), painted.GetPixel (mark.Left + mark.Width / 2, mark.Top));
    }

    [Fact]
    public void A_placed_item_stays_where_Position_put_it_when_AutoArrange_is_off ()
    {
        HeadlessRenderer.Use ();
        using var list = new ListView { Size = new Size (300, 300), View = View.LargeIcon };
        list.Items.Add ("a");
        var item = list.Items.Add ("b");
        using var arranged = PaintSurface.Render (list);
        var flow = item.Bounds.Location;
        Assert.Equal (flow, item.Position);

        list.AutoArrange = false;
        item.Position = new Point (120, 90);
        using var placed = PaintSurface.Render (list);

        Assert.NotEqual (flow, item.Bounds.Location);
        Assert.Equal (new Point (120, 90), item.Bounds.Location);
        Assert.Equal (item.Position, item.Bounds.Location);
    }

    // ── RichTextBox ─────────────────────────────────────────────────────────────────────────────────

    [Fact]
    public void EnableAutoDragDrop_inserts_dropped_text_at_the_selection ()
    {
        HeadlessRenderer.Use ();
        using var form = new Form { Size = new Size (400, 300) };
        var source = new Panel { Bounds = new Rectangle (10, 10, 50, 50) };
        var box = new RichTextBox { Bounds = new Rectangle (100, 10, 250, 100), Text = "start", EnableAutoDragDrop = true };
        form.Controls.Add (source);
        form.Controls.Add (box);
        form.Show ();
        Assert.True (box.AllowDrop);
        box.Select (box.Text.Length, 0);

        var session = DragDropSession.Begin (source, " end", DragDropEffects.Copy | DragDropEffects.Move);
        var at = WindowPoint (box, 20, 20);
        form.HandlePointerMoved (MouseButtons.Left, at.X, at.Y, Keys.None);
        form.HandlePointerReleased (MouseButtons.Left, at.X, at.Y, Keys.None);

        Assert.Equal (DragDropEffects.Copy, session.Result);
        Assert.Equal ("start end", box.Text);
    }

    // ── MenuItem owner draw ─────────────────────────────────────────────────────────────────────────

    [Fact]
    public void An_owner_drawn_menu_item_is_measured_and_painted_by_the_application ()
    {
        HeadlessRenderer.Use ();
        using var form = new Form { Size = new Size (300, 300) };
        using var menu = new MenuDropDown { Size = new Size (200, 100) };
        var plain = menu.Items.Add ("plain");
        var custom = menu.Items.Add ("custom");
        form.Controls.Add (menu);
        var drawn = new List<(int index, DrawItemState state)> ();
        custom.DrawItem += (_, e) => drawn.Add ((e.Index, e.State));
        custom.MeasureItem += (_, e) => e.ItemHeight = 44;

        var before = custom.GetPreferredSize (Size.Empty);
        custom.OwnerDraw = true;
        Assert.Equal (44, custom.GetPreferredSize (Size.Empty).Height);
        Assert.NotEqual (44, before.Height);

        using var _ = PaintSurface.Render (menu);

        // Exactly once per paint pass: every strip used to be painted twice (ScrollableControl.OnPaint
        // and MenuBase.OnPaint both ran the renderer) until the seventh W6 chunk.
        Assert.Equal ([(1, DrawItemState.None)], drawn);
        Assert.Equal (before.Height, plain.GetPreferredSize (Size.Empty).Height);
    }

    // ── DataGridView divider hit-test (RC-8) ────────────────────────────────────────────────────────

    [Fact]
    public void A_column_divider_drag_in_logical_coordinates_resizes_the_column ()
    {
        HeadlessRenderer.Use ();
        using var form = new Form { Size = new Size (500, 300) };
        var grid = new DataGridView { Size = new Size (450, 200), AllowUserToResizeColumns = true, RowHeadersVisible = false };
        grid.Columns.Add ("a", "A");
        grid.Columns.Add ("b", "B");
        grid.Columns[0].Width = 100;
        grid.Rows.Add ("x", "y");
        form.Controls.Add (grid);
        form.Show ();
        using var _ = PaintSurface.Render (grid);

        var divider_x = grid.DeviceToLogicalUnits (grid.GetColumnDeviceLeft (0)) + 100;
        var y = grid.ColumnHeadersHeight / 2;

        grid.RaiseMouseDown (new MouseEventArgs (MouseButtons.Left, 1, divider_x, y, 0));
        grid.RaiseMouseMove (new MouseEventArgs (MouseButtons.Left, 0, divider_x + 30, y, 0));
        grid.RaiseMouseUp (new MouseEventArgs (MouseButtons.Left, 1, divider_x + 30, y, 0));

        Assert.Equal (130, grid.Columns[0].Width);
    }
}
