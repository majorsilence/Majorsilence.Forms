using System.Drawing;
using Majorsilence.Forms.Headless;
using Majorsilence.Forms.Telerik;
using SkiaSharp;
using Xunit;

namespace Majorsilence.Forms.Tests;

// W6 mechanisms, twenty-fourth chunk (#176): RadWaitingBar, RadToggleSwitch, RadDropDownList.NullText
// and RadPageView.DefaultPage -- stored members that decided how the controls look and start.
[Collection ("Headless")]
public class W6TelerikControlsTests
{
    private static T Shown<T> (T control, out Form form) where T : Control
    {
        HeadlessRenderer.Use ();
        form = new Form { Width = 400, Height = 300 };
        form.Controls.Add (control);
        form.Show ();
        return control;
    }

    // ── RadWaitingBar ───────────────────────────────────────────────────────────────────────────────

    [Fact]
    public void The_waiting_bar_moves_only_between_StartWaiting_and_StopWaiting ()
    {
        var bar = Shown (new RadWaitingBar { Width = 200, Height = 20 }, out var form);

        using (form) {
            // Idle until asked: a bar a form shows at rest is not busy.
            Assert.False (bar.IsWaiting);
            Assert.False (bar.MarqueeTimerRunning);

            bar.StartWaiting ();
            Assert.True (bar.IsWaiting);
            Assert.True (bar.MarqueeTimerRunning);

            bar.StopWaiting ();
            Assert.False (bar.MarqueeTimerRunning);
        }
    }

    [Fact]
    public void WaitingStep_and_WaitingIndicatorSize_shape_the_indicator ()
    {
        var bar = Shown (new RadWaitingBar { Width = 200, Height = 20 }, out var form);

        using (form) {
            bar.WaitingStep = 3;
            var before = bar.MarqueePosition;
            bar.AdvanceMarquee ();
            Assert.Equal (before + (3f / ProgressBar.MarqueeSteps), bar.MarqueePosition, 3);

            Assert.Equal ((int) (200 * 0.3f), bar.MarqueeBlockWidth (200));

            bar.WaitingIndicatorSize = new Size (40, 10);
            Assert.Equal (bar.LogicalToDeviceUnits (40), bar.MarqueeBlockWidth (200));
        }
    }

    // ── RadToggleSwitch ─────────────────────────────────────────────────────────────────────────────

    private static int Ink (SKBitmap bitmap, Rectangle area, SKColor background)
    {
        var ink = 0;

        for (var y = area.Top; y < area.Bottom; y++)
            for (var x = area.Left; x < area.Right; x++)
                if (x >= 0 && y >= 0 && x < bitmap.Width && y < bitmap.Height && bitmap.GetPixel (x, y) != background)
                    ink++;

        return ink;
    }

    [Fact]
    public void The_switch_draws_a_thumb_at_its_end_and_the_matching_caption_in_the_free_half ()
    {
        var toggle = Shown (new RadToggleSwitch { Width = 90, Height = 30, OnText = "Yes", OffText = "No" }, out var form);

        using (form) {
            var (track, off_thumb) = toggle.SwitchGeometry ();
            Assert.True (off_thumb.Left < track.Left + (track.Width / 2), "off: the thumb sits at the left");

            // The caption is drawn in the half the thumb has left, over the track colour.
            using (var off = PaintSurface.Render (toggle)) {
                var free = new Rectangle (off_thumb.Right + 2, track.Top + 2, track.Right - off_thumb.Right - 4, track.Height - 4);
                var track_colour = off.GetPixel (track.Right - 3, track.Top + (track.Height / 2));
                Assert.True (Ink (off, free, track_colour) > 0, "the off caption was not drawn");
            }

            toggle.Value = true;
            var (_, on_thumb) = toggle.SwitchGeometry ();
            Assert.True (on_thumb.Left > track.Left + (track.Width / 2), "on: the thumb sits at the right");

            // With no caption there is nothing in the free half but the track.
            toggle.OnText = string.Empty;

            using var on = PaintSurface.Render (toggle);

            // Past the track's rounded end, whose curve blends into the background.
            var radius = track.Height / 2;
            var empty = new Rectangle (track.Left + radius, track.Top + 2, on_thumb.Left - track.Left - radius - 2, track.Height - 4);
            var accent = on.GetPixel (track.Left + (track.Height / 2) + 2, track.Top + (track.Height / 2));
            Assert.Equal (0, Ink (on, empty, accent));
        }
    }

    [Fact]
    public void ThumbTickness_sets_the_thumbs_width ()
    {
        var toggle = Shown (new RadToggleSwitch { Width = 90, Height = 30 }, out var form);

        using (form) {
            var (_, square) = toggle.SwitchGeometry ();
            Assert.Equal (square.Height, square.Width);

            toggle.ThumbTickness = 12;
            var (_, narrow) = toggle.SwitchGeometry ();
            Assert.Equal (toggle.LogicalToDeviceUnits (12), narrow.Width);
        }
    }

    [Fact]
    public void Press_mode_toggles_on_the_press_and_the_click_does_not_toggle_it_back ()
    {
        var toggle = Shown (new RadToggleSwitch { Width = 90, Height = 30 }, out var form);

        using (form) {
            var clicks = 0;
            toggle.Click += (_, _) => clicks++;

            // A press, a release and the click they make -- the harness raises the click itself.
            void Press () => toggle.RaiseMouseDown (new MouseEventArgs (MouseButtons.Left, 1, 10, 10, 0));
            void ReleaseAndClick ()
            {
                toggle.RaiseMouseUp (new MouseEventArgs (MouseButtons.Left, 1, 10, 10, 0));
                toggle.RaiseClick (new MouseEventArgs (MouseButtons.Left, 1, 10, 10, 0));
            }

            Press ();
            Assert.False (toggle.Value);  // Click mode, the default: a press alone does nothing
            ReleaseAndClick ();
            Assert.True (toggle.Value);   // the click toggled it

            toggle.ToggleStateMode = ToggleStateMode.Press;

            Press ();
            Assert.False (toggle.Value);  // the press toggled it

            ReleaseAndClick ();
            Assert.False (toggle.Value);  // and the click that followed did not toggle it back
            Assert.Equal (2, clicks);     // the click event still happened
        }
    }

    // ── RadDropDownList.NullText ────────────────────────────────────────────────────────────────────

    [Fact]
    public void NullText_shows_while_nothing_is_selected ()
    {
        var list = Shown (new RadDropDownList { Width = 160, Height = 24, DropDownStyle = RadDropDownStyle.DropDownList }, out var form);

        using (form) {
            list.Items.Add ("First");

            using (var empty = PaintSurface.Render (list)) {
                list.NullText = "Choose one";

                using var with_text = PaintSurface.Render (list);
                var area = new Rectangle (4, 4, (list.ClientRectangle.Width / 2) - 4, list.ClientRectangle.Height - 8);
                var background = empty.GetPixel (area.Left, area.Top);
                Assert.Equal (0, Ink (empty, area, background));
                Assert.True (Ink (with_text, area, background) > 0, "the null text was not drawn");
            }

            // The editable style shows it as its placeholder.
            list.DropDownStyle = RadDropDownStyle.DropDown;
            Assert.Equal ("Choose one", list.EditRegion.PlaceholderText);
        }
    }

    // ── RadPageView.DefaultPage ─────────────────────────────────────────────────────────────────────

    [Fact]
    public void DefaultPage_selects_its_page_whether_set_before_or_after_the_pages ()
    {
        HeadlessRenderer.Use ();
        using var form = new Form { Width = 400, Height = 300 };

        var first = new RadPageViewPage { Text = "First" };
        var second = new RadPageViewPage { Text = "Second" };

        // The designer's order: the property, then the pages.
        var view = new RadPageView { Width = 300, Height = 200, DefaultPage = second };
        view.Pages.Add (first);
        view.Pages.Add (second);
        form.Controls.Add (view);
        form.Show ();

        Assert.Same (second, view.SelectedPage);

        view.DefaultPage = first;
        Assert.Same (first, view.SelectedPage);
    }
}
