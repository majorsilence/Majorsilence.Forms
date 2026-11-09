using System;
using System.Drawing;
using System.Linq;
using Majorsilence.Forms.Headless;
using Xunit;

namespace Majorsilence.Forms.Tests
{
    // A ported app's StatusStrip lays its items out as upstream's Table layout does. Measured with a WinForms probe
    // (Microsoft Sans Serif 12pt as the app font, the strip in SystemFonts.MenuFont): two labels "Ready0" and "Ready1" on a
    // 300px strip sit at (1, 3, 45, 17) and (46, 3, 45, 17) -- their own text width, a (0, 3, 0, 2) margin, the strip's
    // (1, 0, 14, 0) padding -- and a Spring label takes what the others leave. MF laid every item out 120px wide, 4px
    // apart and the strip's full height. An app that chose no font keeps that.
    [Collection ("Headless")]
    public class StatusStripUpstreamLayoutTests
    {
        private static void Ported () => Application.SetDefaultFont (new Majorsilence.Forms.Drawing.Font ("Microsoft Sans Serif", 12f));

        private static ToolStripStatusLabel Label (string text, bool spring = false) => new ToolStripStatusLabel { Text = text, Spring = spring };

        // A strip added as InitializeComponent adds one, onto a panel of the given size, then painted.
        private static (Form form, StatusStrip strip) Show (Action<StatusStrip>? properties, int width, int height, params ToolStripItem[] items)
        {
            HeadlessRenderer.Use ();
            var form = new Form { FormBorderStyle = FormBorderStyle.None, ClientSize = new Size (400, 300) };
            var panel = new Panel { Size = new Size (width, height) };
            var strip = new StatusStrip ();

            form.Controls.Add (panel);
            panel.SuspendLayout ();
            strip.SuspendLayout ();

            foreach (var item in items)
                strip.Items.Add (item);

            properties?.Invoke (strip);
            panel.Controls.Add (strip);
            strip.ResumeLayout (false);
            strip.PerformLayout ();
            panel.ResumeLayout (false);
            panel.PerformLayout ();

            form.Show ();
            HeadlessRenderer.CapturePng (form, 400, 300);

            return (form, strip);
        }

        private static (Form form, StatusStrip strip) Show (params ToolStripItem[] items) => Show (null, 300, 120, items);

        [Fact]
        public void A_status_label_is_its_text_wide_inside_upstream_s_margin ()
        {
            Rectangle first, second;
            Padding margin, padding;

            try {
                Ported ();
                var (form, strip) = Show (Label ("Ready0"), Label ("Ready1"));

                using (form) {
                    first = strip.Items[0].Bounds;
                    second = strip.Items[1].Bounds;
                    margin = strip.Items[0].Margin;
                    padding = strip.Items[0].Padding;
                }
            } finally {
                Application.SetDefaultFont (null!);
            }

            Assert.Equal (new Padding (0, 3, 0, 2), margin);
            Assert.Equal (Padding.Empty, padding);

            // Inside the strip's 1px left padding, 3px down, the 22px strip less the margin tall, the next straight after.
            Assert.Equal (new Point (1, 3), first.Location);
            Assert.Equal (17, first.Height);
            Assert.Equal (first.Right, second.Left);
            Assert.True (first.Width < 70, $"width {first.Width}");

            // "Ready0" in Segoe UI 9pt with GDI's padding.
            if (OperatingSystem.IsWindows ())
                Assert.Equal (new Rectangle (1, 3, 45, 17), first);
        }

        [Fact]
        public void A_spring_label_takes_what_the_others_leave ()
        {
            Rectangle left, spring, right;

            try {
                Ported ();
                var (form, strip) = Show (Label ("Left"), Label ("Spring", spring: true), Label ("Right"));

                using (form) {
                    left = strip.Items[0].Bounds;
                    spring = strip.Items[1].Bounds;
                    right = strip.Items[2].Bounds;
                }
            } finally {
                Application.SetDefaultFont (null!);
            }

            // The display rectangle is 1..286: the right label ends at the 14px padding before the sizing grip.
            Assert.Equal (left.Right, spring.Left);
            Assert.Equal (spring.Right, right.Left);
            Assert.Equal (286, right.Right);

            if (OperatingSystem.IsWindows ())
                Assert.Equal (223, spring.Width);
        }

        [Fact]
        public void Two_spring_labels_share_the_space_equally ()
        {
            Rectangle a, s1, s2;

            try {
                Ported ();
                var (form, strip) = Show (Label ("A"), Label ("S1", spring: true), Label ("S2", spring: true));

                using (form) {
                    a = strip.Items[0].Bounds;
                    s1 = strip.Items[1].Bounds;
                    s2 = strip.Items[2].Bounds;
                }
            } finally {
                Application.SetDefaultFont (null!);
            }

            Assert.Equal (286, s2.Right);
            Assert.True (Math.Abs (s1.Width - s2.Width) <= 1, $"{s1.Width} and {s2.Width}");

            if (OperatingSystem.IsWindows ())
                Assert.Equal ((15, 135, 135), (a.Width, s1.Width, s2.Width));
        }

        // Upstream moves an item that runs into the sizing grip just outside the strip and does not show it.
        [Fact]
        public void An_item_that_runs_into_the_sizing_grip_is_not_shown ()
        {
            ToolStripItemPlacement[] placements;
            Rectangle[] bounds;
            Rectangle grip;
            int width;

            try {
                Ported ();
                var (form, strip) = Show (Label ("First long label"), Label ("Second long label"), Label ("Third long label"), Label ("Fourth"));

                using (form) {
                    placements = strip.Items.Cast<ToolStripItem> ().Select (i => i.Placement).ToArray ();
                    bounds = strip.Items.Cast<ToolStripItem> ().Select (i => i.Bounds).ToArray ();
                    grip = strip.SizeGripBounds;
                    width = strip.Width;
                }
            } finally {
                Application.SetDefaultFont (null!);
            }

            // A shown item stops at or before the grip and starts inside the strip; a dropped one is parked past the
            // strip's right edge. How far the labels run is their text width, so only Windows, which has Microsoft Sans
            // Serif and Segoe UI, measures which item is the one that runs into the grip.
            for (var i = 0; i < placements.Length; i++)
                if (placements[i] == ToolStripItemPlacement.Main)
                    Assert.True (bounds[i].Right <= grip.Left && bounds[i].X < width, $"item {i} shown at {bounds[i]}");
                else
                    Assert.Equal (302, bounds[i].X);

            if (OperatingSystem.IsWindows ()) {
                Assert.Equal (new[] { ToolStripItemPlacement.Main, ToolStripItemPlacement.Main, ToolStripItemPlacement.Main, ToolStripItemPlacement.None }, placements);
                Assert.Equal (new Point (302, 23), bounds[3].Location);
            }
        }

        // Upstream drops an item that starts at or past the strip's end as well, which no sizing grip reaches: a row
        // laid out in a font wider than the strip was measured for pushes a later item clean off it, touching neither
        // the grip nor the item before it. Measured on WinForms with AutoSize off, so no font takes part: on a 300px
        // strip with a grip, an item ending at 287 or 288 is shown and one ending at 289 is not; without a grip an item
        // starting at 296 is shown and one starting at 300 is not, however far it runs.
        [Theory]
        [InlineData (true, 400, 10, false, false)]  // the wide item runs into the grip; the next starts past the strip
        [InlineData (true, 280, 7, true, true)]     // ends at 288, on the grip's left edge
        [InlineData (true, 280, 8, true, false)]    // ends at 289, into the grip
        [InlineData (false, 400, 10, true, false)]  // no grip: the wide item stays, the next starts past the strip
        [InlineData (false, 295, 10, true, true)]   // no grip: starts at 296, inside
        [InlineData (false, 299, 10, true, false)]  // no grip: starts at 300, the strip's end
        public void An_item_that_starts_past_the_strip_is_not_shown (bool sizing_grip, int first, int second, bool first_shown, bool second_shown)
        {
            ToolStripItemPlacement[] placements;

            try {
                Ported ();
                var (form, strip) = Show (s => s.SizingGrip = sizing_grip, 300, 120,
                    new ToolStripStatusLabel { Text = "A", AutoSize = false, Width = first },
                    new ToolStripStatusLabel { Text = "B", AutoSize = false, Width = second });

                using (form)
                    placements = strip.Items.Cast<ToolStripItem> ().Select (i => i.Placement).ToArray ();
            } finally {
                Application.SetDefaultFont (null!);
            }

            var shown = new[] { first_shown, second_shown }.Select (b => b ? ToolStripItemPlacement.Main : ToolStripItemPlacement.None);

            Assert.Equal (shown, placements);
        }

        [Fact]
        public void A_right_to_left_strip_mirrors_its_row ()
        {
            Rectangle first, second;
            Padding padding;

            try {
                Ported ();
                var (form, strip) = Show (s => s.RightToLeft = RightToLeft.Yes, 300, 120, Label ("Ready0"), Label ("Ready1"));

                using (form) {
                    first = strip.Items[0].Bounds;
                    second = strip.Items[1].Bounds;
                    padding = strip.Padding;
                }
            } finally {
                Application.SetDefaultFont (null!);
            }

            Assert.Equal (new Padding (14, 0, 1, 0), padding);
            Assert.Equal (299, first.Right);
            Assert.Equal (first.Left, second.Right);
        }

        // Docked to a side, upstream's status strip runs down it: the items stacked, as wide as the strip.
        [Fact]
        public void A_status_strip_docked_left_is_vertical ()
        {
            Rectangle strip_bounds, first, second;
            Orientation orientation;

            try {
                Ported ();
                var (form, strip) = Show (s => s.Dock = DockStyle.Left, 300, 200, Label ("Ready0"), Label ("R1"));

                using (form) {
                    strip_bounds = strip.Bounds;
                    orientation = strip.Orientation;
                    first = strip.Items[0].Bounds;
                    second = strip.Items[1].Bounds;
                }
            } finally {
                Application.SetDefaultFont (null!);
            }

            Assert.Equal (Orientation.Vertical, orientation);
            Assert.Equal (200, strip_bounds.Height);
            Assert.True (strip_bounds.Width < 100, $"width {strip_bounds.Width}");

            // The (1, 3, 1, 22) padding and each label's (0, 3, 0, 2) margin.
            Assert.Equal (new Point (1, 6), first.Location);
            Assert.Equal (strip_bounds.Width - 2, first.Width);
            Assert.Equal (first.Bottom + 5, second.Top);

            if (OperatingSystem.IsWindows ())
                Assert.Equal ((47, 15, 26), (strip_bounds.Width, first.Height, second.Top));
        }

        // A progress bar's size is its own and makes the strip taller; a fixed-size label's height does not.
        [Fact]
        public void A_designer_progress_bar_makes_the_strip_taller_and_a_fixed_label_does_not ()
        {
            int with_bar, with_label;
            Rectangle bar;

            try {
                Ported ();
                var (form, strip) = Show (Label ("Busy"), new ToolStripProgressBar { Size = new Size (117, 19) }, Label ("End"));

                using (form) {
                    with_bar = strip.Height;
                    bar = strip.Items[1].Bounds;
                }

                var (form2, strip2) = Show (new ToolStripStatusLabel { Text = "Tall", AutoSize = false, Size = new Size (120, 30) }, Label ("Y"));

                using (form2)
                    with_label = strip2.Height;
            } finally {
                Application.SetDefaultFont (null!);
            }

            Assert.Equal (25, with_bar);
            Assert.Equal (new Size (117, 19), bar.Size);
            Assert.Equal (3, bar.Y);
            Assert.Equal (22, with_label);
        }

        [Fact]
        public void A_status_strip_prefers_its_items_size_whatever_its_own ()
        {
            Size preferred;

            try {
                Ported ();
                var (form, strip) = Show (s => { s.AutoSize = false; s.Height = 30; }, 300, 120, Label ("Ready0"), Label ("Ready1"));

                using (form)
                    preferred = strip.GetPreferredSize (Size.Empty);
            } finally {
                Application.SetDefaultFont (null!);
            }

            Assert.Equal (22, preferred.Height);

            if (OperatingSystem.IsWindows ())
                Assert.Equal (new Size (107, 22), preferred);
        }

        // An app that never chose a font keeps MF's layout: 120px items, 4px apart.
        [Fact]
        public void Without_a_chosen_font_a_status_strip_keeps_its_fixed_width_items ()
        {
            var (form, strip) = Show (Label ("Ready0"), Label ("Ready1"));

            using (form) {
                Assert.Equal (120, strip.Items[0].Bounds.Width);
                Assert.Equal (strip.Items[0].Bounds.Right + 4, strip.Items[1].Bounds.Left);
                Assert.Equal (Padding.Empty, strip.Items[0].Margin);
                Assert.NotEqual (ToolStripLayoutStyle.Table, strip.LayoutStyle);
            }
        }
    }
}
