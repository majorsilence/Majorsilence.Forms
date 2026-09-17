using System.Drawing;
using Majorsilence.Forms.Headless;
using Xunit;

namespace Majorsilence.Forms.Tests
{
    // W6.2 — wiring the candidate list, batch 3.
    //
    //   DataGridViewRow.DividerHeight   extra space below a row, the usual way a grid separates groups
    //                                   of rows. Stored and read by nothing, so setting it did nothing.
    //   PictureBox.InitialImage         what the box shows WHILE an async load runs.
    //
    // PictureBox.ErrorImage was NOT wired: its moment is on the async failure path, which completes
    // through RunOnUiThread, and no fixture here can pump far enough to observe it -- five seconds of
    // DoEvents never sees IsErrored flip. A wiring that cannot be demonstrated is not a wiring.
    [Collection ("Headless")]
    public class W62WiringBatch3Tests
    {
        // ---------------- DataGridViewRow.DividerHeight

        private static DataGridView Grid (out Form form)
        {
            HeadlessRenderer.Use ();

            var grid = new DataGridView { Width = 260, Height = 200 };
            grid.Columns.Add (new DataGridViewTextBoxColumn { HeaderText = "A", Width = 120 });
            grid.Rows.Add ();
            grid.Rows.Add ();

            form = new Form { Width = 360, Height = 300 };
            form.Controls.Add (grid);
            form.Show ();
            PaintSurface.Render (grid).Dispose ();

            return grid;
        }

        [Fact]
        public void DividerHeight_adds_space_below_the_row ()
        {
            using var grid = Grid (out var form);

            try {
                var before = grid.GetRowDisplayRectangle (0, cutOverflow: false).Height;

                grid.Rows[0].DividerHeight = 10;
                PaintSurface.Render (grid).Dispose ();

                // LOGICAL units on both sides: GetRowDisplayRectangle answers in logical (W6.3 put every
                // public rectangle member there), so converting the 10 to device made this pass at
                // scale 1 and fail at MF_HEADLESS_SCALE=2.
                Assert.Equal (before + 10, grid.GetRowDisplayRectangle (0, cutOverflow: false).Height);
            } finally {
                form.Close ();
            }
        }

        [Fact]
        public void A_divider_pushes_the_next_row_down ()
        {
            // What the property is for: separating groups of rows. If it only grew the first row's own
            // rectangle without moving the next one, the two would overlap.
            using var grid = Grid (out var form);

            try {
                var before = grid.GetRowDisplayRectangle (1, cutOverflow: false).Top;

                grid.Rows[0].DividerHeight = 12;
                PaintSurface.Render (grid).Dispose ();

                Assert.True (grid.GetRowDisplayRectangle (1, cutOverflow: false).Top > before,
                    "the following row did not move down");
            } finally {
                form.Close ();
            }
        }

        [Fact]
        public void No_divider_leaves_the_layout_alone ()
        {
            // GUARD: every grid in existence has DividerHeight 0, and a change that shifted their rows
            // would be far worse than the gap being fixed.
            using var grid = Grid (out var form);

            try {
                var first = grid.GetRowDisplayRectangle (0, cutOverflow: false);
                var second = grid.GetRowDisplayRectangle (1, cutOverflow: false);

                Assert.Equal (0, grid.Rows[0].DividerHeight);
                Assert.Equal (first.Bottom, second.Top);
            } finally {
                form.Close ();
            }
        }

        [Fact]
        public void A_negative_divider_is_ignored ()
        {
            // GUARD: a negative would shrink the row into its neighbour.
            using var grid = Grid (out var form);

            try {
                var before = grid.GetRowDisplayRectangle (0, cutOverflow: false).Height;

                grid.Rows[0].DividerHeight = -20;
                PaintSurface.Render (grid).Dispose ();

                Assert.Equal (before, grid.GetRowDisplayRectangle (0, cutOverflow: false).Height);
            } finally {
                form.Close ();
            }
        }

        // ---------------- PictureBox.InitialImage / ErrorImage

        private static Majorsilence.Forms.Drawing.Bitmap Solid (Color colour)
        {
            var bitmap = new Majorsilence.Forms.Drawing.Bitmap (8, 8);

            using var canvas = Majorsilence.Forms.Drawing.Graphics.FromImage (bitmap);
            canvas.Clear (colour);

            return bitmap;
        }

        [Fact]
        public void An_async_load_shows_the_initial_image_while_it_runs ()
        {
            HeadlessRenderer.Use ();

            using var box = new PictureBox { Width = 60, Height = 60 };
            var placeholder = Solid (Color.Aqua);

            box.InitialImage = placeholder;
            box.LoadAsync ("/definitely/not/here.png");

            // Synchronously after starting: the placeholder is in place before the load completes.
            Assert.Same (placeholder, box.Image);
        }

        [Fact]
        public void A_load_with_no_images_set_is_unchanged ()
        {
            // GUARD: both are optional, so a box that sets neither must behave exactly as before --
            // which is every PictureBox that exists today.
            HeadlessRenderer.Use ();

            using var box = new PictureBox { Width = 60, Height = 60 };
            var original = Solid (Color.Lime);

            box.Image = original;
            box.LoadAsync ("/definitely/not/here.png");

            // No InitialImage set, so the load must leave the existing image alone -- which is every
            // PictureBox that exists today.
            Assert.Same (original, box.Image);
        }

        private static void PumpUntil (System.Func<bool> condition, int timeout_ms = 5_000)
        {
            var deadline = System.DateTime.UtcNow.AddMilliseconds (timeout_ms);

            while (System.DateTime.UtcNow < deadline) {
                Application.DoEvents ();

                if (condition ())
                    return;

                System.Threading.Thread.Sleep (10);
            }

            Application.DoEvents ();
        }
    }
}
