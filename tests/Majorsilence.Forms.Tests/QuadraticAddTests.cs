using System.Drawing;
using Majorsilence.Forms.Headless;
using Xunit;

namespace Majorsilence.Forms.Tests
{
    // #691: filling a DataGridView or a panel one control at a time did work proportional to everything already
    // held, on every add.
    [Collection ("Headless")]
    public class QuadraticAddTests
    {
        private static DataGridView NewGrid ()
        {
            HeadlessRenderer.Use ();
            var grid = new DataGridView { Size = new Size (300, 300) };
            grid.Columns.Add ("a", "A");
            grid.Columns.Add ("b", "B");
            return grid;
        }

        [Fact]
        public void Filling_a_grid_does_not_walk_every_row_per_add ()
        {
            using var form = new Form ();
            var grid = NewGrid ();
            form.Controls.Add (grid);

            for (var i = 0; i < 500; i++)
                grid.Rows.Add ("r" + i, "v" + i);

            Assert.Equal (0, grid.VisibleRowWalks);
            // The range still covers every row that takes space (the new row included, as AllowUserToAddRows is on).
            Assert.Equal (grid.VisibleRowCountByWalking () - 1, grid.VerticalScrollMaximum);
        }

        // The count is kept as rows come, go and are hidden; every way the collection changes has to keep it equal
        // to the definition (a row takes space when it has height), or the scroll range drifts.
        [Fact]
        public void The_visible_row_count_matches_a_walk_through_every_kind_of_change ()
        {
            using var form = new Form ();
            var grid = NewGrid ();
            form.Controls.Add (grid);

            void Agrees (string step)
            {
                var expected = grid.VisibleRowCountByWalking ();
                Assert.True (expected >= 0, step);
                Assert.Equal (expected, grid.Rows.Count - grid.Rows.HiddenCount + (grid.ShowsNewRow && grid.NewRowPlaceholder.Visible ? 1 : 0));
            }

            for (var i = 0; i < 20; i++)
                grid.Rows.Add ("r" + i, "v");
            Agrees ("add");

            grid.Rows[3].Visible = false;
            grid.Rows[4].Visible = false;
            Agrees ("hide");

            grid.Rows[3].Visible = false;
            Agrees ("hide again");

            grid.Rows[4].Visible = true;
            Agrees ("show");

            grid.Rows.RemoveAt (3);
            Agrees ("remove a hidden row");

            grid.Rows.RemoveAt (0);
            Agrees ("remove a visible row");

            var hidden = new DataGridViewRow { Visible = false };
            grid.Rows.Insert (2, hidden);
            Agrees ("insert a hidden row");

            grid.Rows[5] = new DataGridViewRow { Visible = false };
            Agrees ("replace with a hidden row");

            grid.Rows[2] = new DataGridViewRow ();
            Agrees ("replace a hidden row with a visible one");

            grid.AllowUserToAddRows = true;
            Agrees ("new row shown");

            grid.NewRowPlaceholder.Visible = false;
            Agrees ("new row hidden");

            grid.AllowUserToAddRows = false;
            grid.Rows.Clear ();
            Agrees ("clear");
            Assert.Equal (0, grid.Rows.HiddenCount);
        }

        // Filling a panel with auto-sized labels measured every label's text on every add. The measurement is kept
        // until an input to it changes; this is the other half, that it does change.
        [Fact]
        public void A_label_remeasures_when_its_text_font_or_padding_changes ()
        {
            HeadlessRenderer.Use ();
            using var form = new Form ();
            var label = new Label { Text = "short", AutoSize = true };
            form.Controls.Add (label);

            var short_size = label.GetPreferredSize (Size.Empty);

            label.Text = "a much longer caption than before";
            var long_size = label.GetPreferredSize (Size.Empty);
            Assert.True (long_size.Width > short_size.Width);

            label.Text = "short";
            Assert.Equal (short_size, label.GetPreferredSize (Size.Empty));

            label.Padding = new Padding (10);
            Assert.Equal (short_size.Width + 20, label.GetPreferredSize (Size.Empty).Width);

            label.Padding = Padding.Empty;
            label.Font = new Majorsilence.Forms.Drawing.Font (label.Font.FontFamily, label.Font.Size * 2);
            Assert.True (label.GetPreferredSize (Size.Empty).Height > short_size.Height);
        }

        [Fact]
        public void A_label_whose_ancestor_font_changes_is_remeasured ()
        {
            HeadlessRenderer.Use ();
            using var form = new Form ();
            var panel = new Panel ();
            var label = new Label { Text = "inherited", AutoSize = true };
            panel.Controls.Add (label);
            form.Controls.Add (panel);

            var before = label.GetPreferredSize (Size.Empty);
            panel.Font = new Majorsilence.Forms.Drawing.Font (panel.Font.FontFamily, panel.Font.Size * 2);

            Assert.True (label.GetPreferredSize (Size.Empty).Width > before.Width);
        }
    }
}
