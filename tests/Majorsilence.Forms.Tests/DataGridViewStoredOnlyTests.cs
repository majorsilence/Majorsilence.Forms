using System.Drawing;
using Majorsilence.Forms.Headless;
using Xunit;

namespace Majorsilence.Forms.Tests
{
    // W6.2 — the DataGridView slice. Five entries:
    //
    //   ScrollBars              the FOURTH member of this family found dead, after TextBox (TXT-26),
    //                           RichTextBox (TXT-29) and ListBox's twin. A grid told to scroll
    //                           vertically only still grew a horizontal bar.
    //   HideSelection           not an upstream DataGridView member at all, so there is no upstream
    //                           default to match; it behaves like its three working siblings rather
    //                           than silently doing nothing.
    //   LinkColor,
    //   VisitedLinkColor,
    //   LinkVisited             a DataGridViewLinkCell was painted exactly like a text cell -- the
    //                           whole visible difference between the two types was missing.
    [Collection ("Headless")]
    public class DataGridViewStoredOnlyTests
    {
        private static DataGridView Wide (out Form form)
        {
            HeadlessRenderer.Use ();

            var grid = new DataGridView { Width = 200, Height = 100 };

            for (var c = 0; c < 6; c++)
                grid.Columns.Add (new DataGridViewTextBoxColumn { HeaderText = $"col{c}", Width = 90 });

            for (var r = 0; r < 20; r++)
                grid.Rows.Add ();

            form = new Form { Width = 300, Height = 200 };
            form.Controls.Add (grid);
            form.Show ();
            PaintSurface.Render (grid).Dispose ();

            return grid;
        }

        // ---------------- ScrollBars

        [Fact]
        public void Both_bars_appear_when_the_content_overflows_both_ways ()
        {
            // PREMISE for everything below: without it, "no bar" is also what a grid that cannot
            // scroll at all looks like.
            using var grid = Wide (out var form);

            try {
                Assert.True (grid.VerticalScrollBarVisible, "premise: 20 rows in a short grid need a vertical bar");
                Assert.True (grid.HorizontalScrollBarVisible, "premise: 6 wide columns need a horizontal bar");
            } finally {
                form.Close ();
            }
        }

        [Theory]
        [InlineData (ScrollBars.None, false, false)]
        [InlineData (ScrollBars.Vertical, true, false)]
        [InlineData (ScrollBars.Horizontal, false, true)]
        [InlineData (ScrollBars.Both, true, true)]
        public void ScrollBars_says_which_bars_are_allowed (ScrollBars allowed, bool vertical, bool horizontal)
        {
            using var grid = Wide (out var form);

            try {
                grid.ScrollBars = allowed;
                PaintSurface.Render (grid).Dispose ();

                Assert.Equal (vertical, grid.VerticalScrollBarVisible);
                Assert.Equal (horizontal, grid.HorizontalScrollBarVisible);
            } finally {
                form.Close ();
            }
        }

        [Fact]
        public void ScrollBars_allows_but_does_not_force ()
        {
            // GUARD: the property says which bars are ALLOWED; the content still says which are
            // NEEDED. An implementation that showed a bar because it was permitted would put one on
            // every grid in existence, since Both is the default.
            HeadlessRenderer.Use ();

            using var form = new Form { Width = 400, Height = 300 };
            var grid = new DataGridView { Width = 300, Height = 200, ScrollBars = ScrollBars.Both };
            grid.Columns.Add (new DataGridViewTextBoxColumn { HeaderText = "one", Width = 80 });
            grid.Rows.Add ();
            form.Controls.Add (grid);
            form.Show ();
            PaintSurface.Render (grid).Dispose ();

            try {
                Assert.False (grid.VerticalScrollBarVisible);
                Assert.False (grid.HorizontalScrollBarVisible);
            } finally {
                form.Close ();
            }
        }

        // ---------------- HideSelection

        [Fact]
        public void HideSelection_defaults_to_showing_the_selection ()
        {
            // No upstream default to match -- upstream's DataGridView has no HideSelection at all --
            // so false is chosen as the less surprising of the two for a member upstream does not
            // define, and pinned here so it is a decision rather than an accident.
            using var grid = new DataGridView ();

            Assert.False (grid.HideSelection);
        }

        [Fact]
        public void HideSelection_drops_the_highlight_when_focus_is_elsewhere ()
        {
            using var grid = Wide (out var form);

            try {
                grid.Rows[0].Selected = true;

                Assert.False (grid.Focused);
                Assert.True (grid.IsRowPaintedSelected (0), "premise: the row paints selected by default");

                grid.HideSelection = true;

                Assert.False (grid.IsRowPaintedSelected (0));
            } finally {
                form.Close ();
            }
        }

        [Fact]
        public void HideSelection_covers_the_cell_modes_too ()
        {
            // Both painted-selection helpers are gated, not just the row one: a grid in cell-selection
            // mode would otherwise drop the row band and keep the cell highlight.
            using var grid = Wide (out var form);

            try {
                grid.SelectionMode = DataGridViewSelectionMode.CellSelect;
                grid.Rows[0].Cells[0].Selected = true;

                Assert.True (grid.IsCellPaintedSelected (0, 0), "premise: the cell paints selected by default");

                grid.HideSelection = true;

                Assert.False (grid.IsCellPaintedSelected (0, 0));
            } finally {
                form.Close ();
            }
        }

        // ---------------- link cells

        private static DataGridView WithLink (out Form form, out DataGridViewLinkCell cell)
        {
            HeadlessRenderer.Use ();

            var grid = new DataGridView { Width = 200, Height = 80 };
            grid.Columns.Add (new DataGridViewTextBoxColumn { HeaderText = "link", Width = 150 });
            grid.Rows.Add ();

            cell = new DataGridViewLinkCell { Value = "open" };
            grid.Rows[0].Cells[0] = cell;

            form = new Form { Width = 300, Height = 180 };
            form.Controls.Add (grid);
            form.Show ();
            PaintSurface.Render (grid).Dispose ();

            return grid;
        }

        // The pixels of the first cell, as a comparable string.
        private static string Cell (DataGridView grid)
        {
            using var bitmap = PaintSurface.Render (grid);

            // GetCellDisplayRectangle answers in LOGICAL units (W6.3 put every public rectangle member
            // there); the bitmap is device. Converting is the difference between sampling the cell and
            // sampling a quarter of it -- caught by the MF_HEADLESS_SCALE=2 gate, again.
            var logical = grid.GetCellDisplayRectangle (0, 0, cutOverflow: true);
            var bounds = new Rectangle (
                grid.LogicalToDeviceUnits (logical.X), grid.LogicalToDeviceUnits (logical.Y),
                grid.LogicalToDeviceUnits (logical.Width), grid.LogicalToDeviceUnits (logical.Height));

            var builder = new System.Text.StringBuilder ();

            for (var y = System.Math.Max (0, bounds.Top); y < bounds.Bottom && y < bitmap.Height; y++)
                for (var x = System.Math.Max (0, bounds.Left); x < bounds.Right && x < bitmap.Width; x++)
                    builder.Append (bitmap.GetPixel (x, y).ToString ()).Append (';');

            return builder.ToString ();
        }

        [Fact]
        public void A_link_cell_paints_in_its_link_colour ()
        {
            using var grid = WithLink (out var form, out var cell);

            try {
                var original = Cell (grid);

                cell.LinkColor = Color.Magenta;

                Assert.NotEqual (original, Cell (grid));
            } finally {
                form.Close ();
            }
        }

        [Fact]
        public void A_visited_link_paints_in_the_visited_colour ()
        {
            using var grid = WithLink (out var form, out var cell);

            try {
                cell.LinkColor = Color.Blue;
                cell.VisitedLinkColor = Color.Purple;

                var unvisited = Cell (grid);

                cell.LinkVisited = true;

                Assert.NotEqual (unvisited, Cell (grid));
            } finally {
                form.Close ();
            }
        }

        [Fact]
        public void An_explicit_cell_colour_still_wins ()
        {
            // GUARD, and the reason the link colour is applied last: an application that has coloured
            // a particular cell means it. Upstream's link colours are a default for the type, not an
            // override of the style.
            using var grid = WithLink (out var form, out var cell);

            try {
                cell.Style.ForeColor = Color.Green;

                var green = Cell (grid);

                cell.LinkColor = Color.Magenta;

                Assert.Equal (green, Cell (grid));
            } finally {
                form.Close ();
            }
        }
    }
}
