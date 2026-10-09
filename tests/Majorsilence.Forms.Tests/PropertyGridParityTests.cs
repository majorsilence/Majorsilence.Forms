using System.ComponentModel;
using System.Drawing;
using System.Linq;
using Majorsilence.Forms.Headless;
using Xunit;

namespace Majorsilence.Forms.Tests;

// The property grid beside WinForms' in ReportDesigner: rows ordered by label ignoring case, an
// expandable value's parts as child rows (Page Margins had no expander), and -- once the app has chosen
// a font -- upstream's row height and font.
[Collection ("Headless")]
public class PropertyGridParityTests
{
    [TypeConverter (typeof (ExpandableObjectConverter))]
    public sealed class Margins
    {
        public int Left { get; set; } = 5;
        public int Top { get; set; } = 7;
        public override string ToString () => $"{Left}, {Top}";
    }

    public sealed class Report
    {
        [Browsable (false)] public int Setter_calls { get; private set; }
        private Margins margins = new ();
        private Point origin = new (1, 2);

        public string alpha { get; set; } = "a";
        public string BodyColumns { get; set; } = "";
        public string BodyColumnSpacing { get; set; } = "";
        public string Zeta { get; set; } = "";

        public Margins PageMargins { get => margins; set { margins = value; Setter_calls++; } }

        // A struct: editing a part has to rebuild it and write it back.
        public Point Origin { get => origin; set { origin = value; Setter_calls++; } }
    }

    private static PropertyGrid Grid (out Form form, object selected)
    {
        HeadlessRenderer.Use ();
        form = new Form { Size = new Size (460, 420) };
        var grid = new PropertyGrid { Bounds = new Rectangle (0, 0, 380, 360), PropertySort = PropertySort.Alphabetical };
        form.Controls.Add (grid);
        form.Show ();
        grid.SelectedObject = selected;
        return grid;
    }

    private static void ClickValue (PropertyGrid grid, GridItem item)
    {
        var index = grid.VisibleRows.ToList ().IndexOf (item);
        var row = grid.DeviceToLogicalUnits (grid.RowBounds (index));
        grid.RaiseMouseDown (new MouseEventArgs (MouseButtons.Left, 1, row.Left + (row.Width * 3 / 4), row.Top + (row.Height / 2), 0));
    }

    [Fact]
    public void Rows_are_ordered_by_label_ignoring_case ()
    {
        var grid = Grid (out var form, new Report ());

        using (form) {
            var labels = grid.Roots.Select (r => r.Label).ToArray ();
            Assert.Equal (new[] { "alpha", "BodyColumns", "BodyColumnSpacing", "Origin", "PageMargins", "Zeta" }, labels);
        }
    }

    [Fact]
    public void An_expandable_value_has_its_parts_as_collapsed_child_rows ()
    {
        var grid = Grid (out var form, new Report ());

        using (form) {
            var margins = grid.Roots.Single (r => r.Label == "PageMargins");
            Assert.Equal (new[] { "Left", "Top" }, margins.GridItems.Select (i => i.Label).ToArray ());
            Assert.False (margins.Expanded);
            Assert.DoesNotContain (grid.VisibleRows, r => r.Label == "Left");

            margins.Expanded = true;

            var rows = grid.VisibleRows.Select (r => r.Label).ToList ();
            Assert.Equal (rows.IndexOf ("PageMargins") + 1, rows.IndexOf ("Left"));
        }
    }

    [Fact]
    public void Editing_a_part_writes_it_through_the_owning_property ()
    {
        var report = new Report ();
        var grid = Grid (out var form, report);

        using (form) {
            var margins = grid.Roots.Single (r => r.Label == "PageMargins");
            margins.Expanded = true;

            ClickValue (grid, margins.GridItems.Single (i => i.Label == "Top"));
            var editor = Assert.IsType<TextBox> (grid.EditingControl);
            editor.Text = "12";
            grid.EndEdit (commit: true);

            Assert.Equal (12, report.PageMargins.Top);
            Assert.Equal (1, report.Setter_calls);   // the owner's setter ran, as for a top-level edit
        }
    }

    [Fact]
    public void Editing_a_part_of_a_struct_rebuilds_it ()
    {
        var report = new Report ();
        var grid = Grid (out var form, report);

        using (form) {
            var origin = grid.Roots.Single (r => r.Label == "Origin");
            origin.Expanded = true;

            ClickValue (grid, origin.GridItems.Single (i => i.Label == "X"));
            var editor = Assert.IsType<TextBox> (grid.EditingControl);
            editor.Text = "9";
            grid.EndEdit (commit: true);

            Assert.Equal (new Point (9, 2), report.Origin);
        }
    }

    [Fact]
    public void With_a_chosen_font_rows_are_upstream_s_height ()
    {
        try {
            Application.SetDefaultFont (new Majorsilence.Forms.Drawing.Font ("Segoe UI", 9f));
            var grid = Grid (out var form, new Report ());

            using (form) {
                Assert.Equal (grid.Font.Height + 3, grid.RowHeight);
                Assert.Equal (grid.ViewBounds.Width / 2, grid.ScaledNameColumnWidth);   // upstream's default split
            }
        } finally {
            Application.SetDefaultFont (null!);
        }
    }

    [Fact]
    public void Without_a_chosen_font_rows_keep_the_theme_height ()
    {
        var grid = Grid (out var form, new Report ());

        using (form)
            Assert.Equal (22, grid.RowHeight);
    }
}
