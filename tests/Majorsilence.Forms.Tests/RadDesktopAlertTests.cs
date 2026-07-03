using System.Drawing;
using Majorsilence.Forms.Telerik;
using Xunit;

namespace Majorsilence.Forms.Tests
{
    // These run on the Headless backend (see AssemblyInfo.cs). RadDesktopAlert's popup is a real (if
    // invisible-to-any-OS) Form, and its Timer is backed by HeadlessPlatformBackend's HeadlessTimer,
    // which posts its Tick callback through the backend's action queue rather than firing inline — tests
    // that exercise auto-close pump that queue with Application.DoEvents () after the delay elapses.
    public class RadDesktopAlertTests
    {
        [Fact]
        public void CaptionText_and_ContentText_round_trip_through_the_popup_labels ()
        {
            using var alert = new RadDesktopAlert {
                CaptionText = "Saved",
                ContentText = "Your changes were saved.",
            };

            Assert.Equal ("Saved", alert.CaptionText);
            Assert.Equal ("Your changes were saved.", alert.ContentText);
            Assert.Equal ("Saved", alert.Popup.CaptionLabel.Text);
            Assert.Equal ("Your changes were saved.", alert.Popup.ContentLabel.Text);
        }

        [Fact]
        public void AutoCloseDelay_defaults_to_five_seconds ()
        {
            using var alert = new RadDesktopAlert ();
            Assert.Equal (5000, alert.AutoCloseDelay);
        }

        [Fact]
        public void Show_makes_the_popup_visible_and_positions_it_bottom_right ()
        {
            using var alert = new RadDesktopAlert { AutoCloseDelay = 0 };

            alert.Show ();

            Assert.True (alert.Popup.Visible);

            var workingArea = Screen.PrimaryScreen!.WorkingArea;
            Assert.True (alert.Popup.Location.X <= workingArea.Right - alert.Popup.Size.Width);
            Assert.True (alert.Popup.Location.Y <= workingArea.Bottom - alert.Popup.Size.Height);

            alert.Close ();
        }

        [Fact]
        public void Show_stacks_a_second_alert_above_the_first ()
        {
            using var first = new RadDesktopAlert { AutoCloseDelay = 0 };
            using var second = new RadDesktopAlert { AutoCloseDelay = 0 };

            first.Show ();
            second.Show ();

            // Second alert stacks above (smaller Y) the first, at the same X (both flush to the corner).
            Assert.Equal (first.Popup.Location.X, second.Popup.Location.X);
            Assert.True (second.Popup.Location.Y < first.Popup.Location.Y);

            first.Close ();
            second.Close ();
        }

        [Fact]
        public void Show_auto_closes_the_popup_after_AutoCloseDelay_elapses ()
        {
            using var alert = new RadDesktopAlert { AutoCloseDelay = 500 };

            alert.Show ();
            Assert.True (alert.Popup.Visible);

            System.Threading.Thread.Sleep (700);
            Application.DoEvents ();

            Assert.False (alert.Popup.Visible);
        }

        [Fact]
        public void CloseButton_click_closes_the_popup_before_the_delay_elapses ()
        {
            using var alert = new RadDesktopAlert { AutoCloseDelay = 60000 };

            alert.Show ();
            Assert.True (alert.Popup.Visible);

            alert.Popup.CloseButton.PerformClick ();

            Assert.False (alert.Popup.Visible);
        }

        [Fact]
        public void AlertElement_BorderColor_feeds_the_popup_window_border ()
        {
            using var alert = new RadDesktopAlert { AutoCloseDelay = 0 };
            alert.Popup.AlertElement.BorderColor = Color.Red;

            alert.Show ();

            var expected = new SkiaSharp.SKColor (Color.Red.R, Color.Red.G, Color.Red.B, Color.Red.A);
            Assert.Equal (expected, alert.Popup.Style.Border.GetColor ());
            Assert.True (alert.Popup.Style.Border.GetWidth () > 0);

            alert.Close ();
        }

        [Fact]
        public void AlertElement_BackColor_feeds_the_popup_window_background ()
        {
            using var alert = new RadDesktopAlert { AutoCloseDelay = 0 };
            alert.Popup.AlertElement.BackColor = Color.White;

            alert.Show ();

            Assert.Equal (Color.White.ToArgb (), alert.Popup.Style.BackColor.ToArgb ());

            alert.Close ();
        }

        [Fact]
        public void TextElement_ForeColor_feeds_the_caption_label_color ()
        {
            using var alert = new RadDesktopAlert { AutoCloseDelay = 0 };
            alert.Popup.AlertElement.CaptionElement.TextAndButtonsElement.TextElement.ForeColor = Color.Red;

            Assert.Equal (Color.Red.ToArgb (), alert.Popup.CaptionLabel.ForeColor.ToArgb ());
        }

        [Fact]
        public void CaptionGrip_BackColor_feeds_the_caption_bar_background ()
        {
            using var alert = new RadDesktopAlert { AutoCloseDelay = 0 };
            alert.Popup.AlertElement.CaptionElement.CaptionGrip.BackColor = Color.Blue;

            alert.Show ();

            Assert.Equal (Color.Blue.ToArgb (), alert.Popup.CaptionLabel.BackColor.ToArgb ());
            Assert.Equal (Color.Blue.ToArgb (), alert.Popup.CaptionPanel.BackColor.ToArgb ());

            alert.Close ();
        }

        [Fact]
        public void GradientStyle_is_a_settable_no_op ()
        {
            using var alert = new RadDesktopAlert ();

            alert.Popup.AlertElement.GradientStyle = GradientStyles.Linear;
            alert.Popup.AlertElement.CaptionElement.CaptionGrip.GradientStyle = GradientStyles.Glass;

            Assert.Equal (GradientStyles.Linear, alert.Popup.AlertElement.GradientStyle);
            Assert.Equal (GradientStyles.Glass, alert.Popup.AlertElement.CaptionElement.CaptionGrip.GradientStyle);
        }

        [Fact]
        public void Popup_is_a_borderless_topmost_form_not_shown_in_the_taskbar ()
        {
            using var alert = new RadDesktopAlert ();

            Assert.Equal (FormBorderStyle.None, alert.Popup.FormBorderStyle);
            Assert.True (alert.Popup.TopMost);
            Assert.False (alert.Popup.ShowInTaskbar);
        }

        [Fact]
        public void Dispose_closes_the_popup_and_stops_the_timer ()
        {
            var alert = new RadDesktopAlert { AutoCloseDelay = 60000 };
            alert.Show ();

            alert.Dispose ();

            Assert.False (alert.Popup.Visible);
        }
    }
}
