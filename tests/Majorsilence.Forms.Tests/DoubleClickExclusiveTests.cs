using System.Drawing;
using Majorsilence.Forms.Headless;
using Xunit;

namespace Majorsilence.Forms.Tests;

// EVT-01 (the last open P0): the release that completes a double-click raised DoubleClick AND Click, so
// every double-click ran a control's Click handler twice. Upstream WmMouseUp raises one or the other,
// keyed on ControlStyles.StandardDoubleClick -- which Button, CheckBox and RadioButton turn off, so on
// those a double-click really is two Clicks (Control.cs WmMouseUp; Button.cs:39, CheckBox.cs:45).
// Driven through the window's pointer path, two clicks well inside the double-click time.
[Collection ("Headless")]
public class DoubleClickExclusiveTests
{
    private static Form Host (Control control)
    {
        HeadlessRenderer.Use ();
        var form = new Form { Width = 300, Height = 200 };
        control.SetBounds (20, 20, 120, 40);
        form.Controls.Add (control);
        form.Show ();
        return form;
    }

    private static void DoubleClick (Form form, Control control)
    {
        var at = control.GetPositionInForm ();
        HeadlessRenderer.Click (form, at.X + 10, at.Y + 10);
        HeadlessRenderer.Click (form, at.X + 10, at.Y + 10);
    }

    // A control that keeps both standard styles -- the default.
    private sealed class Plain : Control { }

    private sealed class NoDoubleClick : Control
    {
        public NoDoubleClick () => SetStyle (ControlStyles.StandardDoubleClick, false);
    }

    [Fact]
    public void A_double_click_is_one_Click_then_one_DoubleClick ()
    {
        var control = new Plain ();
        using var form = Host (control);
        using var recorder = EventRecorder.For (control, "Click", "MouseClick", "DoubleClick", "MouseDoubleClick");

        DoubleClick (form, control);

        recorder.AssertSequence ("Click", "MouseClick", "DoubleClick", "MouseDoubleClick");
    }

    [Fact]
    public void Without_StandardDoubleClick_the_second_release_is_a_second_Click ()
    {
        var control = new NoDoubleClick ();
        using var form = Host (control);
        using var recorder = EventRecorder.For (control, "Click", "DoubleClick");

        DoubleClick (form, control);

        recorder.AssertSequence ("Click", "Click");
    }

    [Fact]
    public void A_double_clicked_Button_clicks_twice_as_in_WinForms ()
    {
        var button = new Button ();
        using var form = Host (button);
        using var recorder = EventRecorder.For (button, "Click", "DoubleClick");

        DoubleClick (form, button);

        recorder.AssertSequence ("Click", "Click");
    }

    [Fact]
    public void A_double_clicked_CheckBox_toggles_twice ()
    {
        var box = new CheckBox ();
        using var form = Host (box);
        var doubles = 0;
        box.DoubleClick += (_, _) => doubles++;

        var at = box.GetPositionInForm ();
        HeadlessRenderer.Click (form, at.X + 10, at.Y + 10);
        Assert.True (box.Checked);
        HeadlessRenderer.Click (form, at.X + 10, at.Y + 10);

        Assert.False (box.Checked);
        Assert.Equal (0, doubles);
    }

    [Fact]
    public void A_double_clicked_RadioButton_raises_no_DoubleClick ()
    {
        var radio = new RadioButton ();
        using var form = Host (radio);
        using var recorder = EventRecorder.For (radio, "Click", "DoubleClick");

        DoubleClick (form, radio);

        recorder.AssertSequence ("Click", "Click");
    }

    [Fact]
    public void A_strip_item_clicks_twice_unless_it_is_DoubleClickEnabled ()
    {
        HeadlessRenderer.Use ();
        using var form = new Form { Width = 400, Height = 200 };
        var strip = new ToolStrip { Dock = DockStyle.Top };
        var item = new ToolStripButton ("One");
        strip.Items.Add (item);
        form.Controls.Add (strip);
        form.Show ();
        PaintSurface.Render (strip).Dispose ();

        var clicks = 0;
        var doubles = 0;
        item.Click += (_, _) => clicks++;
        item.DoubleClick += (_, _) => doubles++;

        var origin = strip.GetPositionInForm ();
        var b = item.Bounds;
        void Click () => HeadlessRenderer.Click (form, origin.X + b.Left + b.Width / 2, origin.Y + b.Top + b.Height / 2);

        // A real double-click is ~150ms apart. Back-to-back synthetic clicks fall inside MenuBase's 50ms
        // guard against one release being delivered twice (TryBeginLeafClick), which is not a double-click.
        void Pause () => System.Threading.Thread.Sleep (100);

        Click ();
        Pause ();
        Click ();
        Assert.Equal (2, clicks);   // upstream ToolStripItem.HandleMouseUp: a plain item clicks again
        Assert.Equal (0, doubles);

        // Far enough apart in time that the next pair starts a new gesture.
        System.Threading.Thread.Sleep (600);
        item.DoubleClickEnabled = true;
        Click ();
        Pause ();
        Click ();
        Assert.Equal (3, clicks);
        Assert.Equal (1, doubles);
    }

    // EVT-19: a click is a press and a release on the same control. The capture is let go by MouseUp
    // before Click is raised, so the click was hit-tested at the release point: a press on one button
    // and a release on another clicked the second, and a release on the form clicked the form.
    [Fact]
    public void A_press_on_one_control_and_a_release_on_another_clicks_neither ()
    {
        HeadlessRenderer.Use ();
        using var form = new Form { Width = 400, Height = 300 };
        var a = new Button { Left = 20, Top = 20, Width = 100, Height = 30 };
        var b = new Button { Left = 200, Top = 20, Width = 100, Height = 30 };
        form.Controls.Add (a);
        form.Controls.Add (b);
        form.Show ();

        var log = new System.Collections.Generic.List<string> ();
        a.Click += (_, _) => log.Add ("a");
        b.Click += (_, _) => log.Add ("b");
        form.Click += (_, _) => log.Add ("form");

        var pa = a.GetPositionInForm ();
        var pb = b.GetPositionInForm ();

        HeadlessRenderer.MouseDown (form, pa.X + 10, pa.Y + 10);
        HeadlessRenderer.MouseMove (form, pb.X + 10, pb.Y + 10, MouseButtons.Left);
        HeadlessRenderer.MouseUp (form, pb.X + 10, pb.Y + 10);

        HeadlessRenderer.MouseDown (form, pa.X + 10, pa.Y + 10);
        HeadlessRenderer.MouseMove (form, 350, 250, MouseButtons.Left);
        HeadlessRenderer.MouseUp (form, 350, 250);

        Assert.Empty (log);

        // And a press and release on the same button still clicks it.
        System.Threading.Thread.Sleep (600);
        HeadlessRenderer.Click (form, pb.X + 10, pb.Y + 10);
        Assert.Equal (new[] { "b" }, log);
    }
}
