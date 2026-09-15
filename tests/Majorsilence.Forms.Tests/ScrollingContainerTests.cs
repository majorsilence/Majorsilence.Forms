using System;
using System.Collections.Generic;
using System.Drawing;
using Majorsilence.Forms.Headless;
using SkiaSharp;
using Xunit;

namespace Majorsilence.Forms.Tests
{
    // W5.25 -- scrolling containers and the two panels that go with them.
    //
    //   LAY-22  TableLayoutPanel's whole paint region was commented out: CellBorderStyle reserved the
    //           gap between cells and nothing ever drew in it, and CellPaint was never raised
    //   LAY-28  Panel.BorderStyle validated and invalidated, and PanelRenderer.Render was an empty
    //           method body -- the border drew nothing and never inset the client area
    //   LAY-30  AutoScrollMargin was an auto-property while the canvas calculation read a different
    //           field of the same name that nothing wrote; ScrollControlIntoView ignored the margin
    //           and the horizontal axis, and nothing called it when focus moved
    //
    //   LAY-29  ScrollableControl.DisplayRectangle carried neither the scroll offset nor the content
    //           size, so anything converting between content and client coordinates read (0,0)
    [Collection ("Headless")]
    public class ScrollingContainerTests
    {
        // ---------------- LAY-28: Panel.BorderStyle

        [Fact]
        public void A_panel_border_is_drawn ()
        {
            // The finding's own test. PanelRenderer.Render was an empty method body, so
            // `panel1.BorderStyle = FixedSingle` -- the standard way to group controls visually
            // without a GroupBox -- showed nothing at all.
            using var plain = new Panel { Size = new Size (50, 50) };
            using var framed = new Panel { Size = new Size (50, 50), BorderStyle = BorderStyle.FixedSingle };

            Assert.Equal (0, EdgeInk (plain));
            Assert.True (EdgeInk (framed) > 0, "a FixedSingle panel should draw its outer ring");
        }

        [Fact]
        public void A_panel_border_insets_the_client_area ()
        {
            // The other half, and the one nobody sees until a Dock = Fill child sits 1-2px out from
            // where Windows puts it: Win32 shrinks the client rectangle for WS_BORDER/WS_EX_CLIENTEDGE.
            using var panel = new Panel { Size = new Size (50, 50) };

            Assert.Equal (50, panel.DisplayRectangle.Width);

            panel.BorderStyle = BorderStyle.FixedSingle;
            Assert.Equal (48, panel.DisplayRectangle.Width);

            panel.BorderStyle = BorderStyle.Fixed3D;
            Assert.Equal (46, panel.DisplayRectangle.Width);

            panel.BorderStyle = BorderStyle.None;
            Assert.Equal (50, panel.DisplayRectangle.Width);
        }

        [Fact]
        public void A_docked_child_is_placed_inside_the_panel_border ()
        {
            // What the inset is FOR: a Fill child must not be painted over by the frame.
            using var panel = new Panel { Size = new Size (50, 50), BorderStyle = BorderStyle.FixedSingle };
            var child = new Panel { Dock = DockStyle.Fill };
            panel.Controls.Add (child);
            panel.PerformLayout ();

            Assert.Equal (1, child.Left);
            Assert.Equal (1, child.Top);
            Assert.Equal (48, child.Width);
        }

        [Fact]
        public void A_panel_border_counts_toward_the_preferred_size ()
        {
            using var plain = new Panel { AutoSize = true };
            using var framed = new Panel { AutoSize = true, BorderStyle = BorderStyle.Fixed3D };

            Assert.True (framed.PreferredSize.Width > plain.PreferredSize.Width,
                $"a bordered panel should want to be wider ({framed.PreferredSize.Width} vs {plain.PreferredSize.Width})");
        }

        // ---------------- LAY-22: TableLayoutPanel cell borders and CellPaint

        [Fact]
        public void Cell_borders_are_drawn ()
        {
            // The finding's own test. The layout engine honoured CellBorderStyle and reserved the gap;
            // nothing drew in it, so a grid-looking form migrated as a grid of floating controls
            // separated by mysterious whitespace.
            using var plain = Table (TableLayoutPanelCellBorderStyle.None);
            using var single = Table (TableLayoutPanelCellBorderStyle.Single);

            Assert.Equal (0, Ink (plain));
            Assert.True (Ink (single) > 0, "a Single cell border style should draw grid lines");
        }

        [Fact]
        public void Each_cell_border_style_draws_something_different ()
        {
            using var single = Table (TableLayoutPanelCellBorderStyle.Single);
            using var inset = Table (TableLayoutPanelCellBorderStyle.Inset);
            using var outset = Table (TableLayoutPanelCellBorderStyle.Outset);

            // Inset and Outset are the same lines with the light and dark swapped, which is the whole
            // difference between a cell that looks sunken and one that looks raised.
            Assert.NotEqual (Pixels (inset), Pixels (outset));
            Assert.NotEqual (Pixels (single), Pixels (inset));
        }

        [Fact]
        public void CellPaint_is_raised_once_per_cell ()
        {
            // The finding's own test. The event is declared (in RemainingMemberParity.cs) and was
            // never raised by anything, so a custom cell background simply never appeared.
            using var table = Table (TableLayoutPanelCellBorderStyle.Single);
            var cells = new List<(int Column, int Row)> ();
            table.CellPaint += (_, e) => cells.Add ((e.Column, e.Row));

            _ = Render (table);

            Assert.Equal (4, cells.Count);
            Assert.Contains ((0, 0), cells);
            Assert.Contains ((1, 1), cells);
        }

        [Fact]
        public void CellPaint_reports_the_cell_it_is_painting ()
        {
            using var table = Table (TableLayoutPanelCellBorderStyle.Single);
            var bounds = new Dictionary<(int, int), Rectangle> ();
            table.CellPaint += (_, e) => bounds[(e.Column, e.Row)] = e.CellBounds;

            _ = Render (table);

            // Column 1 is to the right of column 0, row 1 below row 0 -- relational, so it holds
            // whatever the panel's size works out to.
            Assert.True (bounds[(1, 0)].Left > bounds[(0, 0)].Left);
            Assert.True (bounds[(0, 1)].Top > bounds[(0, 0)].Top);
        }

        // ---------------- LAY-30: AutoScrollMargin and ScrollControlIntoView
        //
        // These own a shown Form rather than a detached Panel. A child control's Visible walks up to
        // its parent, so a detached panel's scrollbars report Visible = false -- and AutoScrollPosition
        // only applies to a scrollbar that is visible, so nothing scrolls and every assertion here
        // would read zero whatever the code did.

        [Fact]
        public void AutoScrollMargin_is_read_by_the_canvas_calculation ()
        {
            // It was an auto-property, while Recalculate read a private field of the same name that
            // nothing ever wrote: the margin was stored, reported back, and applied to nothing.
            using var host = new ScrollHost (childTop: 400);
            using var spaced = new ScrollHost (childTop: 400);
            spaced.Panel.AutoScrollMargin = new Size (0, 60);

            Assert.True (spaced.Panel.VerticalScroll.Maximum > host.Panel.VerticalScroll.Maximum,
                $"the margin should enlarge the scrollable canvas ({spaced.Panel.VerticalScroll.Maximum} vs {host.Panel.VerticalScroll.Maximum})");
        }

        [Fact]
        public void A_negative_AutoScrollMargin_is_rejected_by_the_property ()
        {
            // LAY-32's other half, which W5.25 missed: upstream's property throws on a negative
            // component. A negative margin silently accepted enlarges nothing and quietly mis-reports
            // itself back to the caller forever.
            using var host = new ScrollHost (childTop: 400);

            Assert.Throws<ArgumentOutOfRangeException> (() => host.Panel.AutoScrollMargin = new Size (-1, 0));
            Assert.Throws<ArgumentOutOfRangeException> (() => host.Panel.AutoScrollMargin = new Size (0, -1));
            Assert.Equal (Size.Empty, host.Panel.AutoScrollMargin);
        }

        [Fact]
        public void A_negative_SetAutoScrollMargin_is_clamped_rather_than_rejected ()
        {
            // The deliberate asymmetry, and upstream's: the property is what designer code assigns, so
            // a negative there is a bug worth surfacing; the method is the programmatic path and has
            // always been forgiving. Pinned because the obvious "tidy-up" is to make them agree.
            using var host = new ScrollHost (childTop: 400);

            host.Panel.SetAutoScrollMargin (-5, 12);

            Assert.Equal (new Size (0, 12), host.Panel.AutoScrollMargin);
        }

        [Fact]
        public void ScrollControlIntoView_brings_a_child_below_the_fold_into_view ()
        {
            // The finding's own test: tabbing or Focus()ing into a control below the fold used to
            // leave it invisible.
            using var host = new ScrollHost (childTop: 1000);

            host.Panel.ScrollControlIntoView (host.Child);

            Assert.NotEqual (0, host.Panel.AutoScrollPosition.Y);
            Assert.True (host.Child.Top < host.Panel.Height,
                $"the child should be inside the viewport once scrolled to (top {host.Child.Top}, viewport {host.Panel.Height})");
        }

        [Fact]
        public void ScrollControlIntoView_keeps_AutoScrollMargin_clear ()
        {
            using var tight = new ScrollHost (childTop: 1000);
            using var spaced = new ScrollHost (childTop: 1000);
            spaced.Panel.AutoScrollMargin = new Size (0, 40);

            tight.Panel.ScrollControlIntoView (tight.Child);
            spaced.Panel.ScrollControlIntoView (spaced.Child);

            // A margin means scrolling FURTHER, so the field does not end up flush against the edge.
            Assert.True (-spaced.Panel.AutoScrollPosition.Y > -tight.Panel.AutoScrollPosition.Y,
                $"the margin should be left clear below the control ({-spaced.Panel.AutoScrollPosition.Y} vs {-tight.Panel.AutoScrollPosition.Y})");
        }

        [Fact]
        public void ScrollControlIntoView_handles_the_horizontal_axis ()
        {
            // Only the vertical axis was handled, so a wide form left the focused field off to the
            // right however far down the panel had scrolled to reach it.
            using var host = new ScrollHost (childTop: 10, childLeft: 900);

            host.Panel.ScrollControlIntoView (host.Child);

            Assert.NotEqual (0, host.Panel.AutoScrollPosition.X);
        }

        [Fact]
        public void Focusing_a_child_below_the_fold_scrolls_it_into_view ()
        {
            // The behaviour ScrollControlIntoView exists FOR, and the half nothing called: upstream
            // drives it from ContainerControl when the active control changes, which is what makes
            // tabbing into a field below the fold of a long data-entry form bring it on screen.
            using var host = new ScrollHost (childTop: 1000, selectable: true);

            host.Child.Focus ();

            Assert.NotEqual (0, host.Panel.AutoScrollPosition.Y);
        }

        [Fact]
        public void ScrollControlIntoView_leaves_a_visible_child_alone ()
        {
            // GUARD, not proof: a control already in view must not be scrolled to, or every focus
            // change would jerk the panel about.
            using var host = new ScrollHost (childTop: 10);

            host.Panel.ScrollControlIntoView (host.Child);

            Assert.Equal (0, host.Panel.AutoScrollPosition.Y);
        }

        // A form holding one AutoScroll panel with one child, shown so the scrollbars are really
        // visible and the scroll position really applies.
        private sealed class ScrollHost : IDisposable
        {
            private readonly Form form;

            internal ScrollHost (int childTop, int childLeft = 10, bool selectable = false)
            {
                HeadlessRenderer.Use ();

                form = new Form { Width = 400, Height = 320 };
                Panel = new Panel { Size = new Size (200, 200), AutoScroll = true };
                // A plain Panel is not selectable, so the focus test needs something that can take
                // focus for the focus-change path to run at all.
                Child = selectable
                    ? new SelectablePanel { Left = childLeft, Top = childTop, Size = new Size (50, 20) }
                    : new Panel { Left = childLeft, Top = childTop, Size = new Size (50, 20) };
                Panel.Controls.Add (Child);
                form.Controls.Add (Panel);
                form.Show ();
                Panel.PerformLayout ();
            }

            internal Panel Panel { get; }

            internal Panel Child { get; }

            public void Dispose () => form.Close ();
        }

        private sealed class SelectablePanel : Panel
        {
            internal SelectablePanel ()
            {
                SetControlBehavior (ControlBehaviors.Selectable, true);
                TabStop = true;
            }
        }

        // ---------------- LAY-29: DisplayRectangle is the content coordinate space

        [Fact]
        public void DisplayRectangle_carries_the_scroll_offset ()
        {
            // The finding's own test. The origin is the NEGATIVE scroll position upstream, which is
            // the space every anchor delta in the layout engine is expressed in; reporting (0,0)
            // whatever the scroll position mis-placed anything that converted between content and
            // client coordinates by exactly the scroll amount.
            using var host = new ScrollHost (childTop: 1000);

            Assert.Equal (0, host.Panel.DisplayRectangle.Y);

            host.Panel.AutoScrollPosition = new Point (0, 50);

            Assert.Equal (-50, host.Panel.DisplayRectangle.Y);
            Assert.Equal (-50, host.Panel.AutoScrollPosition.Y);
        }

        [Fact]
        public void DisplayRectangle_carries_the_content_size ()
        {
            // The other half: the size is the scrollable content extent, larger than the visible area.
            using var host = new ScrollHost (childTop: 1000);

            // Compared against the panel's own (logical) Height, not ClientRectangle: the client
            // rectangle is in DEVICE pixels, so under MF_HEADLESS_SCALE=2 it is twice the number
            // DisplayRectangle reports and the comparison stops meaning anything.
            Assert.True (host.Panel.DisplayRectangle.Height > host.Panel.Height,
                $"the display rectangle should span the content ({host.Panel.DisplayRectangle.Height} vs panel {host.Panel.Height})");
            Assert.True (host.Panel.DisplayRectangle.Height >= 1000,
                $"the content runs to at least the last child ({host.Panel.DisplayRectangle.Height})");
        }

        [Fact]
        public void An_unscrolled_panel_reports_its_client_area ()
        {
            // GUARD, not proof: a panel that is not scrolling has no content beyond what is shown, and
            // must keep reporting the plain client area -- every non-scrolling container in the
            // framework lays its children out against this.
            using var host = new ScrollHost (childTop: 10);

            Assert.Equal (0, host.Panel.DisplayRectangle.X);
            Assert.Equal (0, host.Panel.DisplayRectangle.Y);
            // Logical units both sides -- see DisplayRectangle_carries_the_content_size.
            Assert.Equal (host.Panel.Height, host.Panel.DisplayRectangle.Height);
        }

        [Fact]
        public void A_scrolled_child_keeps_its_place_in_content_coordinates ()
        {
            // What the coordinate space is FOR: subtracting the display-rect origin from a child's
            // bounds gives its position in the content, which is the number the application knows.
            using var host = new ScrollHost (childTop: 1000);

            host.Panel.AutoScrollPosition = new Point (0, 50);

            var display = host.Panel.DisplayRectangle;

            Assert.Equal (1000, host.Child.Top - display.Y);
        }

        // ---------------- helpers

        private static TableLayoutPanel Table (TableLayoutPanelCellBorderStyle style)
        {
            var table = new TableLayoutPanel {
                Size = new Size (80, 60),
                ColumnCount = 2,
                RowCount = 2,
                CellBorderStyle = style,
            };

            table.ColumnStyles.Add (new ColumnStyle (SizeType.Percent, 50));
            table.ColumnStyles.Add (new ColumnStyle (SizeType.Percent, 50));
            table.RowStyles.Add (new RowStyle (SizeType.Percent, 50));
            table.RowStyles.Add (new RowStyle (SizeType.Percent, 50));
            table.PerformLayout ();

            return table;
        }

        private static SKBitmap Render (Control control)
        {
            HeadlessRenderer.Use ();

            // Scale 1 explicitly: these compare ink between renders and probe in bitmap pixels, so a
            // doubled bitmap under MF_HEADLESS_SCALE=2 would move the probes.
            return PaintSurface.RenderOnForm (control, 1f);
        }

        private static string Pixels (Control control)
        {
            using var bitmap = Render (control);
            var sb = new System.Text.StringBuilder ();

            for (var y = 0; y < bitmap.Height; y++)
                for (var x = 0; x < bitmap.Width; x++)
                    sb.Append (bitmap.GetPixel (x, y));

            return sb.ToString ();
        }

        private static int Ink (Control control)
        {
            using var bitmap = Render (control);
            var background = Background (bitmap);
            var ink = 0;

            for (var y = 0; y < bitmap.Height; y++)
                for (var x = 0; x < bitmap.Width; x++)
                    if (bitmap.GetPixel (x, y) != background)
                        ink++;

            return ink;
        }

        // Ink on the outermost ring -- where a border is and nothing else goes.
        private static int EdgeInk (Control control)
        {
            using var bitmap = Render (control);
            var background = Background (bitmap);
            var ink = 0;

            for (var x = 0; x < bitmap.Width; x++) {
                if (bitmap.GetPixel (x, 0) != background)
                    ink++;

                if (bitmap.GetPixel (x, bitmap.Height - 1) != background)
                    ink++;
            }

            for (var y = 1; y < bitmap.Height - 1; y++) {
                if (bitmap.GetPixel (0, y) != background)
                    ink++;

                if (bitmap.GetPixel (bitmap.Width - 1, y) != background)
                    ink++;
            }

            return ink;
        }

        private static SKColor Background (SKBitmap bitmap)
        {
            var counts = new Dictionary<SKColor, int> ();

            for (var y = 0; y < bitmap.Height; y++)
                for (var x = 0; x < bitmap.Width; x++) {
                    var pixel = bitmap.GetPixel (x, y);
                    counts[pixel] = counts.TryGetValue (pixel, out var n) ? n + 1 : 1;
                }

            var background = SKColors.Transparent;
            var best = -1;

            foreach (var entry in counts)
                if (entry.Value > best) {
                    best = entry.Value;
                    background = entry.Key;
                }

            return background;
        }
    }
}
