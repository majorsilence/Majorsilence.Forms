using System.Drawing;
using Xunit;

namespace Majorsilence.Forms.Tests;

// W5.24: the layout engines were already a faithful port -- the audit's one encouraging non-finding --
// and what was broken was the wiring into them. Four separate disconnections, each silent:
// Panel.GetPreferredSize replacing the engine (LAY-25), Control.Scale bypassing ScaleControl (LAY-21),
// no GetPreferredSizeCore on the button family (LAY-34), and GroupBox.AutoSize shadowing the real one
// (LAY-26).
public class LayoutWiringTests
{
    // ── Panel: ask the engine, honour padding and the constraints ────────────────────────────────

    [Fact]
    public void An_autosize_panel_includes_its_padding ()
    {
        // The child sits at the display-rectangle origin, which is where a padded panel's layout puts
        // it -- the finding's own suggested assertion used (0, 0) and expected 70, but the engine
        // subtracts the container's padding offset from the anchored preferred size (upstream's
        // DefaultLayout does this too, because an anchored child's bounds already start inside the
        // padding), so a child forced to (0, 0) legitimately measures 60. Placing it where layout
        // would is both the real shape and the assertion that means something.
        using var padded = new Panel { Padding = new Padding (10) };
        padded.Controls.Add (new Panel { Bounds = new Rectangle (10, 10, 50, 50), Margin = Padding.Empty });

        using var bare = new Panel ();
        bare.Controls.Add (new Panel { Bounds = new Rectangle (10, 10, 50, 50), Margin = Padding.Empty });

        // The old hand-rolled scan never looked at Padding at all, so both of these were 60 and an
        // auto-sized panel drew its content flush against its own edge.
        Assert.Equal (new Size (70, 70), padded.GetPreferredSize (Size.Empty));
        Assert.Equal (new Size (60, 60), bare.GetPreferredSize (Size.Empty));
    }

    [Fact]
    public void A_wrapping_flow_panel_computes_a_height_for_the_width_it_is_offered ()
    {
        using var flow = new FlowLayoutPanel { AutoSize = true, WrapContents = true, Width = 100 };

        for (var i = 0; i < 3; i++)
            flow.Controls.Add (new Button { Bounds = new Rectangle (0, 0, 60, 20) });

        // proposedSize was discarded outright before this, so a wrapping panel could only ever report
        // one row however narrow it was told it would be. Asserting the two widths against EACH OTHER
        // rather than against a number: a stale implementation returns the same height for both, and an
        // absolute floor like ">= 60" is satisfied by the panel's own default height without wrapping
        // anything (which is exactly how this test passed against a deliberately broken build once).
        var narrow = flow.GetPreferredSize (new Size (100, 0));
        var wide = flow.GetPreferredSize (new Size (1000, 0));

        Assert.True (narrow.Height > wide.Height,
            $"wrapping into 100px should be taller than laying out in 1000px; got {narrow} vs {wide}");
    }

    [Fact]
    public void Preferred_size_is_clamped_by_MaximumSize ()
    {
        using var panel = new Panel { MaximumSize = new Size (40, 40) };
        panel.Controls.Add (new Panel { Bounds = new Rectangle (0, 0, 500, 500) });

        // Overriding the PUBLIC GetPreferredSize bypassed ApplySizeConstraints entirely, so
        // MinimumSize/MaximumSize never reached PreferredSize. Overriding the core gets it for free.
        // Note this one guards the shape of the fix rather than proving it: any core override picks the
        // clamping up, so it stays green against a core that returns the wrong number. It is here to
        // catch a future change that moves this back onto the public method.
        var preferred = panel.GetPreferredSize (Size.Empty);

        Assert.True (preferred.Width <= 40 && preferred.Height <= 40,
            $"MaximumSize should clamp PreferredSize; got {preferred}");
    }

    // ── Scale: everything measured in the same pixels moves together ─────────────────────────────

    [Fact]
    public void Scaling_moves_padding_and_margin_with_the_bounds ()
    {
        using var panel = new Panel {
            Bounds = new Rectangle (10, 10, 100, 50),
            Padding = new Padding (8),
            Margin = new Padding (4),
        };

        panel.Scale (new SizeF (2f, 2f));

        Assert.Equal (new Padding (16), panel.Padding);
        Assert.Equal (new Padding (8), panel.Margin);
        Assert.Equal (new Size (200, 100), panel.Size);
    }

    [Fact]
    public void Scaling_moves_the_size_constraints ()
    {
        using var panel = new Panel {
            Bounds = new Rectangle (0, 0, 100, 100),
            MinimumSize = new Size (50, 50),
            MaximumSize = new Size (300, 300),
        };

        panel.Scale (new SizeF (2f, 2f));

        Assert.Equal (new Size (100, 100), panel.MinimumSize);
        Assert.Equal (new Size (600, 600), panel.MaximumSize);
    }

    [Fact]
    public void A_control_sitting_at_its_minimum_size_can_still_grow ()
    {
        // Pins the ordering inside ScaleControl: the old (unscaled) MaximumSize/MinimumSize has to be
        // lifted before the bounds are scaled, or the new bounds are computed and then clamped straight
        // back to the limit they were meant to outgrow.
        using var panel = new Panel {
            Bounds = new Rectangle (0, 0, 50, 50),
            MinimumSize = new Size (50, 50),
            MaximumSize = new Size (50, 50),
        };

        panel.Scale (new SizeF (2f, 2f));

        Assert.Equal (new Size (100, 100), panel.Size);
        Assert.Equal (new Size (100, 100), panel.MaximumSize);
    }

    private sealed class ScaleControlSpy : Panel
    {
        public int Calls { get; private set; }
        public SizeF LastFactor { get; private set; }

        protected override void ScaleControl (SizeF factor, BoundsSpecified specified)
        {
            Calls++;
            LastFactor = factor;
            base.ScaleControl (factor, specified);
        }
    }

    [Fact]
    public void An_overridden_ScaleControl_is_actually_called ()
    {
        // The documented WinForms DPI hook. It had no caller anywhere in the repo, so an override
        // compiled, looked right, and never ran.
        using var spy = new ScaleControlSpy { Bounds = new Rectangle (0, 0, 100, 100) };

        spy.Scale (new SizeF (1.5f, 1.5f));

        Assert.Equal (1, spy.Calls);
        Assert.Equal (new SizeF (1.5f, 1.5f), spy.LastFactor);
        Assert.Equal (new Size (150, 150), spy.Size);
    }

    [Fact]
    public void Scaling_a_container_reaches_its_children ()
    {
        using var panel = new Panel { Bounds = new Rectangle (0, 0, 200, 200) };
        var child = new ScaleControlSpy { Bounds = new Rectangle (10, 20, 50, 60), Padding = new Padding (2) };
        panel.Controls.Add (child);

        panel.Scale (new SizeF (2f, 2f));

        Assert.Equal (1, child.Calls);
        Assert.Equal (new Padding (4), child.Padding);
        Assert.Equal (new Rectangle (20, 40, 100, 120), child.Bounds);
    }

    // ── Buttons: measure the caption ─────────────────────────────────────────────────────────────

    [Fact]
    public void A_button_measures_its_caption ()
    {
        using var shortCaption = new Button { Text = "OK" };
        using var longCaption = new Button { Text = "A very long caption indeed" };

        // Both used to report the designer's size, so these were equal -- which is why AutoSize on a
        // button did nothing at all.
        Assert.True (longCaption.PreferredSize.Width > shortCaption.PreferredSize.Width,
            $"long {longCaption.PreferredSize} should be wider than short {shortCaption.PreferredSize}");
        Assert.True (longCaption.PreferredSize.Width > new Button ().Width,
            "a caption longer than the default button should want more than the default width");
    }

    [Fact]
    public void A_bigger_font_wants_a_bigger_button ()
    {
        using var small = new Button { Text = "Caption", Font = new Majorsilence.Forms.Drawing.Font ("Arial", 8f) };
        using var large = new Button { Text = "Caption", Font = new Majorsilence.Forms.Drawing.Font ("Arial", 24f) };

        Assert.True (large.PreferredSize.Width > small.PreferredSize.Width);
        Assert.True (large.PreferredSize.Height > small.PreferredSize.Height);
    }

    [Fact]
    public void A_check_box_reserves_room_for_its_glyph ()
    {
        using var button = new Button { Text = "Enabled" };
        using var check = new CheckBox { Text = "Enabled" };
        using var radio = new RadioButton { Text = "Enabled" };

        // The glyph column is the renderer's own GlyphSize (13) + GlyphTextPadding (5) + the pixel
        // TextImageLayoutEngine adds. Asserting the DIFFERENCE, not just "wider": the default sizes of
        // these controls differ anyway, so a plain inequality holds even when nothing is measured.
        var glyphColumn = check.PreferredSize.Width - button.PreferredSize.Width;

        Assert.InRange (glyphColumn, 15, 25);
        Assert.Equal (glyphColumn, radio.PreferredSize.Width - button.PreferredSize.Width);
        Assert.Equal (button.PreferredSize.Height, check.PreferredSize.Height);
    }

    [Fact]
    public void A_buttons_padding_widens_its_preferred_size ()
    {
        using var bare = new Button { Text = "Caption" };
        using var padded = new Button { Text = "Caption", Padding = new Padding (12) };

        Assert.Equal (bare.PreferredSize.Width + 24, padded.PreferredSize.Width);
    }

    // ── GroupBox ─────────────────────────────────────────────────────────────────────────────────

    [Fact]
    public void GroupBox_AutoSize_is_the_real_AutoSize ()
    {
        using var group = new GroupBox ();

        group.AutoSize = true;

        // It was a `new`-shadowed auto-property, so the two halves of the same feature disagreed:
        // gb.AutoSize said true while the layout state the engine reads said false.
        Assert.True (((Control)group).AutoSize);
        Assert.True (group.AutoSize);
    }

    [Fact]
    public void A_group_box_leaves_room_for_its_caption_above_its_children ()
    {
        // The caption band is font-dependent (CaptionHeight), so the assertion is between two group
        // boxes rather than against a constant: the same children under a bigger caption font need a
        // taller box. Against a stale implementation both report the designer's size and are equal.
        using var small = new GroupBox { Text = "Options", Font = new Majorsilence.Forms.Drawing.Font ("Arial", 8f) };
        using var large = new GroupBox { Text = "Options", Font = new Majorsilence.Forms.Drawing.Font ("Arial", 24f) };

        foreach (var group in new[] { small, large })
            group.Controls.Add (new Panel { Bounds = new Rectangle (0, 0, 60, 40), Margin = Padding.Empty });

        var smallPreferred = small.GetPreferredSize (Size.Empty);
        var largePreferred = large.GetPreferredSize (Size.Empty);

        Assert.True (largePreferred.Height > smallPreferred.Height,
            $"a bigger caption font needs a taller group box; got {smallPreferred} vs {largePreferred}");
        Assert.True (smallPreferred.Height > 40,
            $"the caption band has to be added above the children; got {smallPreferred}");
    }
}
