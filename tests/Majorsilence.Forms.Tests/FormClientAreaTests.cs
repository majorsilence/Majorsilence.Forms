using System.Drawing;
using Majorsilence.Forms.Headless;
using Xunit;

namespace Majorsilence.Forms.Tests;

// The client area excludes the caption this library draws (finding FRM-06, P0).
//
// Form.Controls used to hand out the ROOT adapter's collection, which spans the whole window
// including the title bar, and ClientSize reported that same whole-window figure. So on every
// platform but macOS -- the only one that uses system decorations -- a designer form built as
// ClientSize = (800, 450) got 450 pixels of which the top caption-height sat behind the caption:
// the first row of controls was hidden and an Anchor = Bottom row hung off the bottom by the same
// amount. Upstream's caption is non-client and (0, 0) is below it.
//
// Every test here forces custom chrome, because the platform that has none is the one this machine
// runs and a test written without it passes vacuously (see the caption-button regression).
[Collection ("Headless")]
public class FormClientAreaTests
{
    private static Form CustomChromeForm ()
    {
        HeadlessRenderer.Use ();
        var form = new Form { Size = new Size (400, 300) };
        form.UseSystemDecorations = false;
        return form;
    }

    [Fact]
    public void ClientSize_excludes_the_caption ()
    {
        using var form = CustomChromeForm ();
        form.Show ();

        var caption = form.TitleBar.PreferredHeight;

        Assert.True (caption > 0, "this test is meaningless without custom chrome");
        Assert.Equal (form.Size.Height - caption, form.ClientSize.Height);
        Assert.Equal (form.Size.Width, form.ClientSize.Width);

        form.Close ();
    }

    [Fact]
    public void Setting_ClientSize_gives_that_much_usable_height ()
    {
        // The designer's own shape: ClientSize is what InitializeComponent assigns.
        using var form = CustomChromeForm ();
        form.ClientSize = new Size (320, 240);
        form.Show ();

        Assert.Equal (new Size (320, 240), form.ClientSize);
        Assert.Equal (240 + form.TitleBar.PreferredHeight, form.Size.Height);

        form.Close ();
    }

    [Fact]
    public void ClientRectangle_matches_ClientSize ()
    {
        using var form = CustomChromeForm ();
        form.Show ();

        Assert.Equal (form.ClientSize, form.ClientRectangle.Size);
        Assert.Equal (Point.Empty, form.ClientRectangle.Location);

        form.Close ();
    }

    [Fact]
    public void A_child_at_the_origin_sits_below_the_caption_not_behind_it ()
    {
        using var form = CustomChromeForm ();
        var button = new Button { Text = "Top", Location = new Point (0, 0), Size = new Size (80, 24) };
        form.Controls.Add (button);
        form.Show ();

        // PointToScreen returns DEVICE pixels while PreferredHeight is logical -- the asymmetry
        // BACKLOG.md calls the root of most HiDPI failures. Compared in device space so this means the
        // same thing under MF_HEADLESS_SCALE=2, where it caught exactly that mistake in a first draft.
        var formTop = form.PointToScreen (Point.Empty).Y;
        var buttonTop = button.PointToScreen (Point.Empty).Y;
        var expected = (int) (form.TitleBar.PreferredHeight * form.Scaling);

        Assert.Equal (expected, buttonTop - formTop);

        form.Close ();
    }

    [Fact]
    public void A_bottom_anchored_child_stays_inside_the_window ()
    {
        // The other half of the same bug: the window was a caption too short for what it was told to
        // hold, so the bottom row fell off it.
        using var form = CustomChromeForm ();
        form.ClientSize = new Size (300, 200);

        form.Show ();

        // Positioned and anchored after the form is realised. Anchor distances are captured when the
        // control is parented, and a child added while its container is still unsized captures
        // degenerate ones -- a separate hazard with its own coverage in AnchorLayoutEarlyCaptureTests.
        // What is being tested here is that the client area is the right SIZE, not when anchors latch.
        var footer = new Button { Text = "OK", Size = new Size (80, 24) };
        form.Controls.Add (footer);
        footer.Location = new Point (10, form.ClientSize.Height - footer.Height);
        footer.Anchor = AnchorStyles.Bottom | AnchorStyles.Left;
        form.PerformLayout ();

        Assert.True (footer.Bottom <= form.ClientSize.Height,
            $"footer bottom {footer.Bottom} must fit inside the {form.ClientSize.Height}px client area");

        form.Close ();
    }

    [Fact]
    public void The_title_bar_is_not_in_the_collection_application_code_sees ()
    {
        using var form = CustomChromeForm ();
        form.Controls.Add (new Button { Text = "Only me", Size = new Size (60, 24) });

        Assert.Single (form.Controls);
        Assert.DoesNotContain (form.TitleBar, form.Controls);
    }

    [Fact]
    public void A_docked_child_fills_the_client_area_and_not_the_caption ()
    {
        using var form = CustomChromeForm ();
        var fill = new Panel { Dock = DockStyle.Fill };
        form.Controls.Add (fill);
        form.Show ();
        form.PerformLayout ();

        Assert.Equal (form.ClientSize.Height, fill.Height);
        Assert.Equal (form.ClientSize.Width, fill.Width);

        form.Close ();
    }

    [Fact]
    public void A_system_decorated_form_has_no_caption_to_exclude ()
    {
        // macOS's shape: the OS draws the caption, so the client area is the whole backend surface and
        // nothing should be subtracted.
        HeadlessRenderer.Use ();
        using var form = new Form { Size = new Size (400, 300) };
        form.UseSystemDecorations = true;
        form.Show ();

        Assert.Equal (form.Size.Height, form.ClientSize.Height);

        form.Close ();
    }
}
