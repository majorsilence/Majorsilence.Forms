using System;
using System.Drawing;
using System.IO;
using System.Linq;
using Majorsilence.Forms.Headless;
using Xunit;

namespace Majorsilence.Forms.Tests;

// TSM-36: ToolStripPanel.Join (strip, row) joins the strip INTO that row, beside the strips already on
// it, as upstream does (ToolStripPanel.Join(ToolStrip, int) turns the row into a drop point and
// HorizontalRowManager.JoinRow inserts a cell there). Before, the layout stacked Controls one per row
// and never read the rows, so the row given to Join changed nothing on screen.
[Collection ("Headless")]
public class ToolStripPanelJoinTests
{
    private static ToolStrip NewStrip (string caption)
    {
        var strip = new ToolStrip { GripStyle = ToolStripGripStyle.Hidden, Name = caption + "Strip" };
        strip.Items.Add (new ToolStripButton (caption));
        return strip;
    }

    private static ToolStripPanel ShownPanel (out Form form)
    {
        HeadlessRenderer.Use ();
        form = new Form { Width = 600, Height = 300 };
        var panel = new ToolStripPanel { Name = "panel", Bounds = new Rectangle (0, 0, 560, 200), Dock = DockStyle.None };
        form.Controls.Add (panel);
        form.Show ();
        return panel;
    }

    [Fact]
    public void Joining_an_existing_row_puts_the_strip_beside_the_others_not_on_a_row_of_its_own ()
    {
        var panel = ShownPanel (out var form);

        using (form) {
            var a = NewStrip ("Alpha");
            var b = NewStrip ("Beta");
            panel.Join (a, 0);
            panel.Join (b, 0);
            panel.PerformLayout ();

            Assert.Single (panel.Rows);
            Assert.Equal (new[] { b, a }, panel.Rows[0].Controls);

            // Side by side on one line: same top, b at the front (upstream joins at the row's leading
            // edge), a starting where b ends.
            Assert.Equal (a.Top, b.Top);
            Assert.True (b.Left < a.Left, $"b ({b.Left}) should be before a ({a.Left})");
            Assert.Equal (b.Right, a.Left);
            Assert.True (a.Width > 0 && b.Width > 0);
        }
    }

    [Fact]
    public void Moving_a_strip_onto_another_row_removes_the_row_it_left_and_the_panel_shrinks ()
    {
        var panel = ShownPanel (out var form);

        using (form) {
            var a = NewStrip ("Alpha");
            var b = NewStrip ("Beta");
            panel.Join (a);
            panel.Join (b);
            panel.PerformLayout ();

            Assert.Equal (2, panel.Rows.Length);
            Assert.True (a.Bottom <= b.Top, "two rows stack");
            var twoRows = panel.GetPreferredSize (Size.Empty).Height;

            panel.Join (b, 0);
            panel.PerformLayout ();

            Assert.Single (panel.Rows);
            Assert.Equal (a.Top, b.Top);
            Assert.True (panel.GetPreferredSize (Size.Empty).Height < twoRows,
                "one row of two strips is thinner than two rows of one");
        }
    }

    [Fact]
    public void Joining_at_a_point_places_the_strip_by_position_along_the_row ()
    {
        var panel = ShownPanel (out var form);

        using (form) {
            var a = NewStrip ("Alpha");
            var b = NewStrip ("Beta");
            panel.Join (a, 0);
            panel.Join (b, 0);
            panel.PerformLayout ();

            // Past the end of the row's strips, on the row: after them.
            var c = NewStrip ("Gamma");
            var row = panel.Rows[0];
            panel.Join (c, new Point (a.Right + 20, row.Bounds.Top + 2));
            panel.PerformLayout ();

            Assert.Single (panel.Rows);
            Assert.Equal (new[] { b, a, c }, panel.Rows[0].Controls);
            Assert.Equal (a.Right, c.Left);

            // Below every row: a new row at the end.
            var d = NewStrip ("Delta");
            panel.Join (d, new Point (4, panel.Rows[0].Bounds.Bottom + 30));
            panel.PerformLayout ();

            Assert.Equal (2, panel.Rows.Length);
            Assert.True (d.Top >= a.Bottom);
        }
    }

    [Fact]
    public void A_strip_that_leaves_the_panel_leaves_its_row ()
    {
        var panel = ShownPanel (out var form);

        using (form) {
            var a = NewStrip ("Alpha");
            var b = NewStrip ("Beta");
            var c = NewStrip ("Gamma");
            panel.Join (a, 0);
            panel.Join (b, 0);
            panel.Join (c);

            panel.Controls.Remove (c);
            panel.Controls.Remove (a);

            Assert.Single (panel.Rows);
            Assert.Equal (new[] { b }, panel.Rows[0].Controls);
        }
    }

    [Fact]
    public void A_stretched_strip_last_on_a_row_takes_the_rest_of_it ()
    {
        var panel = ShownPanel (out var form);

        using (form) {
            var a = NewStrip ("Alpha");
            var menu = new MenuStrip ();
            menu.Items.Add (new ToolStripMenuItem ("File"));
            panel.Join (a, 0);
            panel.Join (menu, new Point (a.Right + 40, panel.Rows[0].Bounds.Top + 1));
            panel.PerformLayout ();

            Assert.True (menu.Stretch);
            Assert.Single (panel.Rows);
            Assert.Equal (a.Right, menu.Left);
            Assert.Equal (panel.DeviceClientRectangle.Right - panel.RowMargin.Right, menu.Right);
        }
    }

    [Fact]
    public void A_saved_shared_row_comes_back_shared ()
    {
        var file = Path.Combine (Path.GetTempPath (), "majorsilence-strip-rows-" + Guid.NewGuid ().ToString ("N") + ".txt");
        var previous = ToolStripManager.SettingsPath;
        ToolStripManager.SettingsPath = file;

        try {
            var panel = ShownPanel (out var form);

            using (form) {
                var a = NewStrip ("Alpha");
                var b = NewStrip ("Beta");
                var c = NewStrip ("Gamma");
                panel.Join (a);
                panel.Join (b, 0);   // b, a on row 0
                panel.Join (c);      // c on row 1
                panel.PerformLayout ();

                ToolStripManager.SaveSettings (form, "rows");

                // The user splits everything onto rows of their own, in another order.
                panel.Join (a, 5);
                panel.Join (c, 0);
                panel.PerformLayout ();
                Assert.NotEqual (panel.RowIndexOf (a), panel.RowIndexOf (b));

                ToolStripManager.LoadSettings (form, "rows");
                panel.PerformLayout ();

                Assert.Equal (2, panel.Rows.Length);
                Assert.Equal (new[] { b, a }, panel.Rows[0].Controls);
                Assert.Equal (new[] { c }, panel.Rows[1].Controls);
                Assert.Equal (a.Top, b.Top);
                Assert.True (c.Top >= a.Bottom);
            }
        } finally {
            ToolStripManager.SettingsPath = previous;

            try {
                File.Delete (file);
            } catch (IOException) {
            } catch (UnauthorizedAccessException) {
            }
        }
    }
}
