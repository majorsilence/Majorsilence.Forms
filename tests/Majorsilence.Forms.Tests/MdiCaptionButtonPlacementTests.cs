using Xunit;

namespace Majorsilence.Forms.Tests
{
    /// <summary>
    /// macOS puts window controls at the leading edge in close/minimize/zoom order; Windows puts them
    /// at the trailing edge in minimize/maximize/close order. An MDI child caption is drawn by the
    /// framework rather than the OS, so it has to follow the host convention itself.
    ///
    /// These exercise the geometry that <c>PaintCaptionButtons</c> and <c>HitCaptionButton</c> both
    /// read, so a drawn glyph and its clickable box cannot disagree.
    /// </summary>
    public class MdiCaptionButtonPlacementTests
    {
        private const int Border = MdiChildWindow.FrameBorder;
        private const int Bw = MdiChildWindow.ButtonWidth;
        private const int Width = 600;

        private static MdiChildWindow Frame (bool maximizeBox = true, bool minimizeBox = true)
        {
            var parent = new Form { IsMdiContainer = true };
            var child = new Form { MdiParent = parent, MaximizeBox = maximizeBox, MinimizeBox = minimizeBox };
            return new MdiChildWindow (parent.MdiClientControl!, child);
        }

        private static T WithLayout<T> (bool onLeft, Func<T> body)
        {
            var original = MdiChildWindow.CaptionButtonsOnLeft;
            MdiChildWindow.CaptionButtonsOnLeft = onLeft;
            try { return body (); } finally { MdiChildWindow.CaptionButtonsOnLeft = original; }
        }

        [Fact]
        public void Mac_layout_orders_the_controls_close_minimize_zoom_from_the_left ()
        {
            var order = WithLayout (true, () => Frame ().CaptionButtonOrder ());

            Assert.Equal (
                new[] { MdiChildWindow.CaptionHit.Close, MdiChildWindow.CaptionHit.Minimize, MdiChildWindow.CaptionHit.Maximize },
                order);
        }

        [Fact]
        public void Windows_layout_keeps_minimize_maximize_close ()
        {
            var order = WithLayout (false, () => Frame ().CaptionButtonOrder ());

            Assert.Equal (
                new[] { MdiChildWindow.CaptionHit.Minimize, MdiChildWindow.CaptionHit.Maximize, MdiChildWindow.CaptionHit.Close },
                order);
        }

        [Fact]
        public void Mac_layout_packs_the_buttons_against_the_left_edge ()
        {
            WithLayout (true, () => {
                // Close is first, hard against the frame border; the rest follow rightwards.
                Assert.Equal (Border, MdiChildWindow.CaptionButtonX (0, 3, Bw, Width, Border));
                Assert.Equal (Border + Bw, MdiChildWindow.CaptionButtonX (1, 3, Bw, Width, Border));
                Assert.Equal (Border + (2 * Bw), MdiChildWindow.CaptionButtonX (2, 3, Bw, Width, Border));
                return 0;
            });
        }

        [Fact]
        public void Windows_layout_packs_the_buttons_against_the_right_edge ()
        {
            WithLayout (false, () => {
                // Close is last and ends flush with the right border.
                Assert.Equal (Width - Border - Bw, MdiChildWindow.CaptionButtonX (2, 3, Bw, Width, Border));
                Assert.Equal (Width - Border - (3 * Bw), MdiChildWindow.CaptionButtonX (0, 3, Bw, Width, Border));
                return 0;
            });
        }

        [Fact]
        public void The_two_layouts_occupy_opposite_edges ()
        {
            var macClose = WithLayout (true, () => MdiChildWindow.CaptionButtonX (0, 3, Bw, Width, Border));
            var winClose = WithLayout (false, () => MdiChildWindow.CaptionButtonX (2, 3, Bw, Width, Border));

            Assert.True (macClose < Width / 2, $"mac close should be on the left half, was x={macClose}");
            Assert.True (winClose > Width / 2, $"windows close should be on the right half, was x={winClose}");
        }

        [Theory]
        [InlineData (true)]
        [InlineData (false)]
        public void Hidden_boxes_drop_out_and_close_stays_on_its_edge (bool onLeft)
        {
            var order = WithLayout (onLeft, () => Frame (maximizeBox: false, minimizeBox: false).CaptionButtonOrder ());

            // Only Close remains, and it still sits on the platform's edge.
            Assert.Equal (new[] { MdiChildWindow.CaptionHit.Close }, order);

            var x = WithLayout (onLeft, () => MdiChildWindow.CaptionButtonX (0, 1, Bw, Width, Border));
            Assert.Equal (onLeft ? Border : Width - Border - Bw, x);
        }

        [Fact]
        public void Defaults_to_the_host_platform_convention ()
        {
            Assert.Equal (OperatingSystem.IsMacOS (), MdiChildWindow.CaptionButtonsOnLeft);
            Assert.Equal (OperatingSystem.IsMacOS (), MdiChildWindow.MacStyleCaption);
        }

        [Fact]
        public void Mac_button_cells_match_traffic_light_spacing ()
        {
            // 12pt discs about 8pt apart: a 20pt cell reproduces macOS' spacing, where Windows' square
            // glyph buttons are wider. Hit testing and painting both scale from this one number, so a
            // narrower cell must not leave dead space between the discs.
            var macSlot = WithLayout (true, () => MdiChildWindow.CaptionButtonSlot);
            var winSlot = WithLayout (false, () => MdiChildWindow.CaptionButtonSlot);

            Assert.Equal (20, macSlot);
            Assert.Equal (MdiChildWindow.ButtonWidth, winSlot);

            // Cells are contiguous: button i+1 starts exactly where button i ends.
            WithLayout (true, () => {
                var first = MdiChildWindow.CaptionButtonX (0, 3, macSlot, Width, Border);
                var second = MdiChildWindow.CaptionButtonX (1, 3, macSlot, Width, Border);
                Assert.Equal (first + macSlot, second);
                return 0;
            });
        }

        [Fact]
        public void Mac_layout_leaves_room_for_a_centred_title_on_both_sides ()
        {
            // The title is inset by the button run on BOTH sides so it reads as centred in the window
            // and cannot collide with the traffic lights. Three 20pt cells plus the frame border must
            // still leave the majority of a normal caption for the text.
            var run = 3 * WithLayout (true, () => MdiChildWindow.CaptionButtonSlot);
            var inset = Border + run + 4;

            Assert.True (Width - (2 * inset) > Width / 2,
                $"insets of {inset} leave only {Width - (2 * inset)}px of {Width} for the title");
        }
    }
}
