using System;
using System.Drawing;
using Majorsilence.Forms.Headless;
using Xunit;

namespace Majorsilence.Forms.Tests;

// AutoScaleMode and AutoScaleDimensions are read, reported and then deliberately not acted on: no
// mode scales a container's children. The metric behind CurrentAutoScaleDimensions still has to be
// right, because callers read it, so the first pair of tests guard its units and its linearity; the
// rest pin the decision that nothing is scaled by it.
//
// Font mode used to scale, added under FRM-17 on the premise that a designer's recorded dimensions
// are a correction worth applying. That premise holds on Windows, where the recorded and the measured
// numbers come out of the same font stack, and not here: designer files record (7, 15) from Segoe UI
// 9pt while the default face here measures (6.50, 11). The resulting 11/15 squashed every Font-mode
// container to 73% of its authored height -- and only Font-mode ones, so a Font-mode UserControl
// inside a Dpi-mode form slid up and shrank away from the label beside it, which stayed put.
//
// These tests assert RELATIONSHIPS rather than pixel literals wherever they can: a ratio derived from
// CurrentAutoScaleDimensions, or a comparison between two fonts. W5.17 is the reason. There, the same
// units defect sat in three places and a suite full of absolute assertions stayed green through all of
// them, because no test tied one path's number to another's.
[Collection ("Headless")]
public class AutoScaleTests
{
    private static Form CustomChromeForm ()
    {
        HeadlessRenderer.Use ();
        var form = new Form ();
        form.UseSystemDecorations = false;   // macOS is the only platform without chrome; see the matrix
        form.ClientSize = new Size (400, 300);
        return form;
    }

    // Half the current dimensions: the shape a designer file has when it was recorded against a font
    // other than the one in use, and so the largest ratio anything here could be tempted to apply.
    private static SizeF HalfOf (SizeF current) => new SizeF (current.Width / 2f, current.Height / 2f);

    [Fact]
    public void Font_dimensions_are_in_the_range_Windows_records ()
    {
        // Not a transcribed number -- a units guard. Designer files carry dimensions measured by GDI:
        // (6, 13) for the old Tahoma 8.25pt default, (7, 15) for Segoe UI 9pt. Callers compare this
        // metric against those, and the two ways it can fail are both factor-sized: measuring at the
        // POINT size reads about a quarter small, and measuring in device pixels on a scaled display
        // reads scale-times large. The bound is what matters, not the value.
        using var form = new Form ();

        var dimensions = form.CurrentAutoScaleDimensions;

        Assert.InRange (dimensions.Width, 4f, 12f);
        Assert.InRange (dimensions.Height, 10f, 24f);
    }

    [Fact]
    public void Doubling_the_font_doubles_the_dimensions ()
    {
        using var form = new Form ();

        form.Font = new Majorsilence.Forms.Drawing.Font ("Arial", 10f);
        var small = form.CurrentAutoScaleDimensions;

        form.Font = new Majorsilence.Forms.Drawing.Font ("Arial", 20f);
        var large = form.CurrentAutoScaleDimensions;

        // The metric has to be linear in the font size, or it is not reporting a font measurement.
        Assert.InRange (large.Width / small.Width, 1.8f, 2.2f);
        Assert.InRange (large.Height / small.Height, 1.8f, 2.2f);
    }

    [Fact]
    public void Font_mode_reports_the_font_dimensions_and_scales_nothing ()
    {
        // The Dpi test below is the same assertion for the other mode, and both halves are asserted
        // together on purpose: reporting honestly and scaling nothing are separate promises, and a
        // change that quietly dropped the first would leave the second looking fine.
        using var form = CustomChromeForm ();
        var button = new Button { Bounds = new Rectangle (100, 100, 75, 23) };
        form.Controls.Add (button);

        // The designer's shape: dimensions recorded against a font half the size of the one in use.
        form.AutoScaleDimensions = HalfOf (form.CurrentAutoScaleDimensions);

        Assert.True (form.CurrentAutoScaleDimensions.Width > 0);

        form.Show ();

        Assert.Equal (new Rectangle (100, 100, 75, 23), button.Bounds);

        form.Close ();
    }

    [Fact]
    public void The_client_area_keeps_its_designed_size ()
    {
        // The counterpart to the children staying put. A form that grew its client area while leaving
        // its children alone would be just as wrong as one that did the reverse.
        using var form = CustomChromeForm ();
        form.AutoScaleDimensions = HalfOf (form.CurrentAutoScaleDimensions);

        form.Show ();

        Assert.Equal (new Size (400, 300), form.ClientSize);

        form.Close ();
    }

    [Fact]
    public void A_form_built_in_code_is_untouched ()
    {
        // AutoScaleDimensions is empty unless a designer file recorded it. Nothing scales either way
        // now, but this stays as the floor: whatever else changes, a form that recorded nothing must
        // never acquire a ratio from somewhere.
        using var form = CustomChromeForm ();
        var button = new Button { Bounds = new Rectangle (10, 20, 75, 23) };
        form.Controls.Add (button);

        form.Show ();

        Assert.Equal (new Rectangle (10, 20, 75, 23), button.Bounds);
        Assert.Equal (new Size (400, 300), form.ClientSize);

        form.Close ();
    }

    [Fact]
    public void AutoScaleMode_None_is_untouched ()
    {
        using var form = CustomChromeForm ();
        var button = new Button { Bounds = new Rectangle (100, 100, 75, 23) };
        form.Controls.Add (button);

        // Record real dimensions FIRST, then switch the mode off -- otherwise the assertion passes
        // for the wrong reason, since None reports empty CurrentAutoScaleDimensions and there would be
        // no ratio to ignore in the first place.
        form.AutoScaleDimensions = HalfOf (form.CurrentAutoScaleDimensions);
        form.AutoScaleMode = AutoScaleMode.None;

        form.Show ();

        Assert.Equal (new Rectangle (100, 100, 75, 23), button.Bounds);

        form.Close ();
    }

    [Fact]
    public void Dpi_mode_reports_the_device_dpi_and_scales_nothing ()
    {
        // A recorded, reasoned decision rather than an oversight, pinned here so it cannot be
        // "fixed" by accident: Bounds are logical and the backend already applies the display's
        // factor, so a dpi/96 ratio on top of it would scale every form twice. CurrentAutoScaleDimensions
        // still answers honestly, which is why this asserts both halves together.
        using var form = CustomChromeForm ();
        var button = new Button { Bounds = new Rectangle (100, 100, 75, 23) };
        form.Controls.Add (button);

        form.AutoScaleMode = AutoScaleMode.Dpi;
        form.AutoScaleDimensions = new SizeF (96f, 96f);

        Assert.True (form.CurrentAutoScaleDimensions.Width > 0);

        form.Show ();

        Assert.Equal (new Rectangle (100, 100, 75, 23), button.Bounds);

        form.Close ();
    }

    [Fact]
    public void A_second_pass_changes_nothing_either ()
    {
        using var form = CustomChromeForm ();
        var button = new Button { Bounds = new Rectangle (100, 100, 75, 23) };
        form.Controls.Add (button);

        var recorded = HalfOf (form.CurrentAutoScaleDimensions);
        form.AutoScaleDimensions = recorded;
        form.Show ();

        form.PerformAutoScale ();

        Assert.Equal (new Rectangle (100, 100, 75, 23), button.Bounds);

        // And the recorded dimensions are left as the designer wrote them, rather than being
        // overwritten with ours -- nothing was scaled to, so there is nothing new to record.
        Assert.Equal (recorded, form.AutoScaleDimensions);

        form.Close ();
    }

    [Fact]
    public void A_container_control_leaves_its_children_where_they_were_placed ()
    {
        using var container = new ContainerControl { Width = 300, Height = 200 };
        var button = new Button { Bounds = new Rectangle (40, 60, 75, 23) };
        container.Controls.Add (button);

        container.AutoScaleDimensions = HalfOf (container.CurrentAutoScaleDimensions);

        container.PerformLayout ();
        container.PerformLayout ();

        Assert.Equal (new Rectangle (40, 60, 75, 23), button.Bounds);
    }

    [Fact]
    public void A_user_control_leaves_its_children_where_they_were_placed ()
    {
        // The nested case is the one that went visibly wrong: this control inside an AutoScaleMode.Dpi
        // form was the only thing on it that moved.
        using var control = new UserControl { Width = 300, Height = 200 };
        var button = new Button { Bounds = new Rectangle (40, 60, 75, 23) };
        control.Controls.Add (button);

        control.AutoScaleDimensions = HalfOf (control.CurrentAutoScaleDimensions);

        control.PerformLayout ();

        Assert.Equal (new Rectangle (40, 60, 75, 23), button.Bounds);
    }

    [Fact]
    public void A_font_mode_user_control_stays_aligned_with_a_label_in_its_dpi_mode_parent ()
    {
        // The shape the defect was reported in, end to end. A designer puts a label and the control it
        // labels on the same row of a form; the form is AutoScaleMode.Dpi, the control is a
        // UserControl that carries its own AutoScaleMode.Font. Only one of the two was scaled, so they
        // came apart -- the picker ended up at (112, 11) against a label still at its designed (16, 18),
        // and 9px narrower besides.
        using var form = CustomChromeForm ();
        form.AutoScaleMode = AutoScaleMode.Dpi;
        form.AutoScaleDimensions = new SizeF (96f, 96f);

        var label = new Label { Location = new Point (16, 18), Size = new Size (81, 23), Text = "Year:" };

        var picker = new UserControl { Location = new Point (121, 16), Size = new Size (132, 31) };
        picker.AutoScaleMode = AutoScaleMode.Font;
        picker.AutoScaleDimensions = new SizeF (7f, 15f);   // what a Windows designer records
        picker.Controls.Add (new DateTimePicker { Dock = DockStyle.Fill });

        form.Controls.Add (label);
        form.Controls.Add (picker);
        form.Show ();

        Assert.Equal (new Rectangle (16, 18, 81, 23), label.Bounds);
        Assert.Equal (new Rectangle (121, 16, 132, 31), picker.Bounds);

        form.Close ();
    }

    [Fact]
    public void A_font_assigned_later_does_not_move_anything ()
    {
        // Application.SetDefaultFont, and any app that themes its fonts at startup, land here. The
        // text in the button is drawn with the new font; the button itself stays where it was put.
        using var container = new ContainerControl { Width = 300, Height = 200 };
        var button = new Button { Bounds = new Rectangle (40, 60, 80, 24) };
        container.Controls.Add (button);

        container.Font = new Majorsilence.Forms.Drawing.Font ("Arial", 10f);
        container.AutoScaleDimensions = container.CurrentAutoScaleDimensions;
        container.PerformLayout ();

        container.Font = new Majorsilence.Forms.Drawing.Font ("Arial", 20f);
        container.PerformLayout ();

        Assert.Equal (new Rectangle (40, 60, 80, 24), button.Bounds);
    }
}
