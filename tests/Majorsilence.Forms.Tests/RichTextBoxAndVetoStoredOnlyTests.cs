using System.Drawing;
using Majorsilence.Forms.Headless;
using Majorsilence.Forms.Telerik;
using Xunit;

namespace Majorsilence.Forms.Tests
{
    // W6.2 continued. Three entries, found by the triage rather than by reading:
    //
    //   RichTextBox.ScrollBars   TXT-26 all over again, one class down -- a `new` shadow over
    //                            ScrollControl.ScrollBars, whose setter is what shows the bars. No
    //                            RichTextBox ever displayed a scrollbar, however much text it held.
    //   RichTextBox.ZoomFactor   stored and read by nothing, so zooming did nothing at all.
    //   GridViewCellCancelEventArgs.Cancel
    //                            RadGridView raised CellBeginEdit and dropped the answer, so a
    //                            handler refusing an edit was ignored and the editor opened anyway.
    //                            One of the two real entries in the triage's Cancel/Handled bucket.
    [Collection ("Headless")]
    public class RichTextBoxAndVetoStoredOnlyTests
    {
        // ---------------- RichTextBox.ScrollBars

        [Fact]
        public void A_fresh_control_agrees_with_its_own_default ()
        {
            // The shadow's most direct symptom: the property said Both while the bars followed the
            // base's None, so the control disagreed with itself before anyone touched it.
            using var box = new RichTextBox ();

            Assert.Equal (RichTextBoxScrollBars.Both, box.ScrollBars);
            Assert.Equal (ScrollBars.Both, ((ScrollControl)box).ScrollBars);
        }

        [Theory]
        [InlineData (RichTextBoxScrollBars.None, ScrollBars.None)]
        [InlineData (RichTextBoxScrollBars.Horizontal, ScrollBars.Horizontal)]
        [InlineData (RichTextBoxScrollBars.Vertical, ScrollBars.Vertical)]
        [InlineData (RichTextBoxScrollBars.Both, ScrollBars.Both)]
        [InlineData (RichTextBoxScrollBars.ForcedHorizontal, ScrollBars.Horizontal)]
        [InlineData (RichTextBoxScrollBars.ForcedVertical, ScrollBars.Vertical)]
        [InlineData (RichTextBoxScrollBars.ForcedBoth, ScrollBars.Both)]
        public void Setting_ScrollBars_reaches_the_bars (RichTextBoxScrollBars asked, ScrollBars expected)
        {
            using var box = new RichTextBox ();

            box.ScrollBars = asked;

            Assert.Equal (expected, ((ScrollControl)box).ScrollBars);
        }

        [Fact]
        public void A_Forced_value_reads_back_as_itself ()
        {
            // The Forced* variants map onto the same pair of bars, so the getter must not answer from
            // the base or the distinction would be lost the moment it was set. It does not yet change
            // what is SHOWN -- there is no "show even when it fits" state -- which is recorded rather
            // than flattened silently.
            using var box = new RichTextBox ();

            box.ScrollBars = RichTextBoxScrollBars.ForcedVertical;

            Assert.Equal (RichTextBoxScrollBars.ForcedVertical, box.ScrollBars);
        }

        [Fact]
        public void Turning_the_bars_off_hides_them ()
        {
            // What the property is for, end to end: the vertical bar is visible with content that
            // overflows, and None takes it away.
            HeadlessRenderer.Use ();

            using var form = new Form { Width = 300, Height = 200 };
            var box = new RichTextBox { Width = 200, Height = 60, Multiline = true, WordWrap = false };
            box.Text = string.Join ("\n", System.Linq.Enumerable.Range (0, 40).Select (i => $"line {i}"));
            form.Controls.Add (box);
            form.Show ();
            PaintSurface.Render (box).Dispose ();

            try {
                Assert.True (box.VerticalScrollBar.Visible, "premise: the content overflows, so a bar is wanted");

                box.ScrollBars = RichTextBoxScrollBars.None;
                PaintSurface.Render (box).Dispose ();

                Assert.False (box.VerticalScrollBar.Visible);
            } finally {
                form.Close ();
            }
        }

        // ---------------- RichTextBox.ZoomFactor

        [Fact]
        public void ZoomFactor_scales_the_text ()
        {
            using var box = new RichTextBox { Width = 200, Height = 100, Text = "hello" };

            var normal = box.CurrentFontSize;

            box.ZoomFactor = 2.0f;

            Assert.Equal (normal * 2, box.CurrentFontSize);
        }

        [Fact]
        public void ZoomFactor_scales_down_too ()
        {
            using var box = new RichTextBox { Width = 200, Height = 100, Text = "hello" };

            var normal = box.CurrentFontSize;

            box.ZoomFactor = 0.5f;

            Assert.True (box.CurrentFontSize < normal, $"not smaller: {normal} -> {box.CurrentFontSize}");
        }

        [Fact]
        public void The_default_zoom_changes_nothing ()
        {
            // GUARD: the multiply is on the path every caret and measurement reads, so a default that
            // did not come out exactly 1:1 would shift text in every RichTextBox in existence.
            using var plain = new TextBox { Width = 200, Height = 100, Text = "hello" };
            using var rich = new RichTextBox { Width = 200, Height = 100, Text = "hello" };

            Assert.Equal (1.0f, rich.ZoomFactor);
            Assert.Equal (plain.CurrentFontSize, rich.CurrentFontSize);
        }

        [Fact]
        public void An_out_of_range_zoom_is_rejected ()
        {
            // Upstream's bounds, exclusive at both ends. Stored silently before, so a zoom of 0 was
            // accepted and would now be a font size of nothing.
            using var box = new RichTextBox ();

            Assert.Throws<System.ArgumentOutOfRangeException> (() => box.ZoomFactor = 0f);
            Assert.Throws<System.ArgumentOutOfRangeException> (() => box.ZoomFactor = 0.015625f);
            Assert.Throws<System.ArgumentOutOfRangeException> (() => box.ZoomFactor = 64f);
            Assert.Throws<System.ArgumentOutOfRangeException> (() => box.ZoomFactor = -1f);
        }

        [Fact]
        public void A_font_size_never_collapses_to_zero ()
        {
            // GUARD: the smallest legal zoom still has to leave something measurable, or the caret
            // arithmetic divides into nothing.
            using var box = new RichTextBox { Width = 200, Height = 100, Text = "hello" };

            box.ZoomFactor = 0.02f;

            Assert.True (box.CurrentFontSize >= 1);
        }

        // ---------------- the CellBeginEdit veto

        // Shown and laid out: BeginEdit returns early on an empty cell rectangle, so an unrendered
        // grid never reaches the event at all and every test below would pass without proving a thing.
        private static RadGridView Populated (out Form form)
        {
            HeadlessRenderer.Use ();

            var grid = new RadGridView { Width = 300, Height = 120 };
            grid.Columns.Add (new GridViewTextBoxColumn ("Name") { HeaderText = "Name", Width = 150 });
            grid.Rows.Add ();
            grid.Rows[0].Cells["Name"].Value = "Alice";

            form = new Form { Width = 400, Height = 250 };
            form.Controls.Add (grid);
            form.Show ();
            PaintSurface.Render (grid).Dispose ();

            return grid;
        }

        [Fact]
        public void A_handler_can_refuse_a_cell_edit ()
        {
            // The half only a propagated Cancel can do. Before this the args were built, raised, and
            // the answer dropped -- so a read-only-at-runtime cell opened its editor regardless.
            using var grid = Populated (out var form);

            try {
                grid.CellBeginEdit += (_, e) => e.Cancel = true;

                grid.BeginEdit (0, 0);

                Assert.False (grid.IsCurrentCellInEditMode);
            } finally {
                form.Close ();
            }
        }

        [Fact]
        public void Without_a_veto_the_edit_begins ()
        {
            // PREMISE: without it, "no editor" is also what a grid that cannot edit at all looks like.
            using var grid = Populated (out var form);

            try {
                var raised = 0;
                grid.CellBeginEdit += (_, _) => raised++;

                grid.BeginEdit (0, 0);

                Assert.True (grid.IsCurrentCellInEditMode);
                Assert.Equal (1, raised);
            } finally {
                form.Close ();
            }
        }

        [Fact]
        public void The_event_still_carries_its_cell ()
        {
            // GUARD on the rewrite: the args are now built only when someone is listening, so the
            // cell coordinates must still be filled in on that path.
            using var grid = Populated (out var form);

            try {
                GridViewCellCancelEventArgs? seen = null;
                grid.CellBeginEdit += (_, e) => seen = e;

                grid.BeginEdit (0, 0);

                Assert.NotNull (seen);
                Assert.Equal (0, seen!.RowIndex);
                Assert.Equal (0, seen.ColumnIndex);
            } finally {
                form.Close ();
            }
        }
    }
}
