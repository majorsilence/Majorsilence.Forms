using System;
using System.Drawing;
using Majorsilence.Forms.Headless;
using Xunit;

namespace Majorsilence.Forms.Tests;

// W3.6 / finding FRM-17: AutoScaleMode and AutoScaleDimensions were stored and read by nothing, so a
// designer file's recorded font dimensions -- which every designer file carries -- corrected nothing.
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

    // Half the current dimensions, so the expected factor is exactly 2 without naming a font metric.
    private static SizeF HalfOf (SizeF current) => new SizeF (current.Width / 2f, current.Height / 2f);

    private static Rectangle Scaled (Rectangle bounds, float factor)
    {
        // Mirrors Control.ScaleCore's own rounding rather than assuming width * factor: it scales the
        // far edge and subtracts, so a control at an odd offset keeps its right edge aligned.
        var x = (int)Math.Round (bounds.Left * factor);
        var y = (int)Math.Round (bounds.Top * factor);

        return new Rectangle (
            x, y,
            (int)Math.Round ((bounds.Left + bounds.Width) * factor) - x,
            (int)Math.Round ((bounds.Top + bounds.Height) * factor) - y);
    }

    [Fact]
    public void Font_dimensions_are_in_the_range_Windows_records ()
    {
        // Not a transcribed number -- a units guard. Designer files carry dimensions measured by GDI:
        // (6, 13) for the old Tahoma 8.25pt default, (7, 15) for Segoe UI 9pt. The ratio between
        // recorded and current is only meaningful if this metric lands in the same range, and the two
        // ways it can fail are both factor-sized: measuring at the POINT size reads about a quarter
        // small, and measuring in device pixels on a scaled display reads scale-times large. Either
        // would rescale every migrated form by that factor, so the bound is what matters, not the value.
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

        // The metric has to be linear in the font size, or the ratio it produces is not a scale factor.
        Assert.InRange (large.Width / small.Width, 1.8f, 2.2f);
        Assert.InRange (large.Height / small.Height, 1.8f, 2.2f);
    }

    [Fact]
    public void A_form_scales_its_children_by_the_font_ratio ()
    {
        using var form = CustomChromeForm ();
        var button = new Button { Bounds = new Rectangle (100, 100, 75, 23) };
        form.Controls.Add (button);

        // The designer's shape: dimensions recorded with a font half the size of the one in use.
        form.AutoScaleDimensions = HalfOf (form.CurrentAutoScaleDimensions);

        form.Show ();

        Assert.Equal (Scaled (new Rectangle (100, 100, 75, 23), 2f), button.Bounds);

        form.Close ();
    }

    [Fact]
    public void The_client_area_grows_with_the_children ()
    {
        using var form = CustomChromeForm ();
        form.AutoScaleDimensions = HalfOf (form.CurrentAutoScaleDimensions);

        form.Show ();

        // A form whose children doubled and whose client area did not would clip half of them -- the
        // symptom FRM-17 describes, arrived at from the other direction.
        Assert.Equal (new Size (800, 600), form.ClientSize);

        form.Close ();
    }

    [Fact]
    public void A_form_built_in_code_is_untouched ()
    {
        // AutoScaleDimensions is empty unless a designer file recorded it, and there is no ratio to be
        // had from an empty one. This is what keeps the blast radius to forms that carry real numbers.
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
    public void Scaling_records_the_new_dimensions_so_a_second_pass_does_nothing ()
    {
        using var form = CustomChromeForm ();
        var button = new Button { Bounds = new Rectangle (100, 100, 75, 23) };
        form.Controls.Add (button);

        form.AutoScaleDimensions = HalfOf (form.CurrentAutoScaleDimensions);
        form.Show ();

        var afterFirst = button.Bounds;

        form.PerformAutoScale ();

        Assert.Equal (afterFirst, button.Bounds);
        Assert.Equal (form.CurrentAutoScaleDimensions, form.AutoScaleDimensions);

        form.Close ();
    }

    [Fact]
    public void A_container_control_scales_its_children_on_first_layout ()
    {
        using var container = new ContainerControl { Width = 300, Height = 200 };
        var button = new Button { Bounds = new Rectangle (40, 60, 75, 23) };
        container.Controls.Add (button);

        container.AutoScaleDimensions = HalfOf (container.CurrentAutoScaleDimensions);

        container.PerformLayout ();

        Assert.Equal (Scaled (new Rectangle (40, 60, 75, 23), 2f), button.Bounds);

        // Once, not once per layout: the second pass has nothing left to do because the first recorded
        // the dimensions it scaled to.
        var afterFirst = button.Bounds;
        container.PerformLayout ();

        Assert.Equal (afterFirst, button.Bounds);
    }

    [Fact]
    public void A_user_control_scales_its_children_on_first_layout ()
    {
        using var control = new UserControl { Width = 300, Height = 200 };
        var button = new Button { Bounds = new Rectangle (40, 60, 75, 23) };
        control.Controls.Add (button);

        control.AutoScaleDimensions = HalfOf (control.CurrentAutoScaleDimensions);

        control.PerformLayout ();

        Assert.Equal (Scaled (new Rectangle (40, 60, 75, 23), 2f), button.Bounds);
    }

    [Fact]
    public void A_font_assigned_later_rescales_by_the_difference ()
    {
        // Application.SetDefaultFont, and any app that themes its fonts at startup, land here: the
        // dimensions recorded by the previous pass are what the next one divides by, so the container
        // scales by the change rather than by the designer's original ratio a second time.
        using var container = new ContainerControl { Width = 300, Height = 200 };
        var button = new Button { Bounds = new Rectangle (40, 60, 80, 24) };
        container.Controls.Add (button);

        container.Font = new Majorsilence.Forms.Drawing.Font ("Arial", 10f);
        container.AutoScaleDimensions = container.CurrentAutoScaleDimensions;
        container.PerformLayout ();

        var beforeFontChange = button.Bounds;

        container.Font = new Majorsilence.Forms.Drawing.Font ("Arial", 20f);
        container.PerformLayout ();

        Assert.True (button.Bounds.Width > beforeFontChange.Width,
            "a font twice the size should have widened the button, not left it alone");
        Assert.InRange (
            (float)button.Bounds.Width / beforeFontChange.Width, 1.7f, 2.3f);
    }
}
