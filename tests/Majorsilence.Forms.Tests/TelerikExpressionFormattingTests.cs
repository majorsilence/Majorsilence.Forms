using System.Data;
using Majorsilence.Forms.Headless;
using Majorsilence.Forms.Telerik;
using SkiaSharp;
using Xunit;

namespace Majorsilence.Forms.Tests;

// ExpressionFormattingObject, against what Telerik UI.for.WinForms 2026.2.520 measured with the same table:
// "Amount > 1 AND Name <> 'Charlie'" on the Name column colours Bravo's Name cell only, and the row rule
// "When < #2024-06-01#" colours the Bravo and Charlie rows.
[Collection ("Headless")]
public class TelerikExpressionFormattingTests
{
    private static RadGridView Grid (out Form form)
    {
        HeadlessRenderer.Use ();
        var table = new DataTable ();
        table.Columns.Add ("Name", typeof (string));
        table.Columns.Add ("Amount", typeof (int));
        table.Columns.Add ("When", typeof (DateTime));
        table.Rows.Add ("Bravo", 2, new DateTime (2024, 1, 1));
        table.Rows.Add ("Alpha", 1, new DateTime (2025, 1, 1));
        table.Rows.Add ("Charlie", 3, new DateTime (2023, 1, 1));

        var grid = new RadGridView { Width = 420, Height = 200, DataSource = table };
        form = new Form { Width = 520, Height = 300 };
        form.Controls.Add (grid);
        form.Show ();
        return grid;
    }

    private static readonly SKColor Gold = new (255, 215, 0);
    private static readonly SKColor Pink = new (255, 192, 203);

    private static List<string> Coloured (RadGridView grid, string column, SKColor colour)
    {
        PaintSurface.Render (grid).Dispose ();
        var core = (DataGridView) grid;
        var index = core.Columns[column]!.Index;
        var hits = new List<string> ();

        foreach (DataGridViewRow row in core.Rows)
            if (!RadGridView.IsStructuralRow (row) && row.Cells[index].Style.BackgroundColor == colour)
                hits.Add ((string) row.Cells[core.Columns["Name"]!.Index].Value!);

        return hits;
    }

    [Fact]
    public void A_cell_rule_colours_the_cells_whose_row_satisfies_the_expression ()
    {
        var grid = Grid (out var form);

        using (form) {
            grid.Columns["Name"]!.ConditionalFormattingObjectList.Add (
                new ExpressionFormattingObject ("r", "Amount > 1 AND Name <> 'Charlie'", false) { CellBackColor = System.Drawing.Color.Gold });

            Assert.Equal (new[] { "Bravo" }, Coloured (grid, "Name", Gold));
            Assert.Empty (Coloured (grid, "Amount", Gold));   // the cell, not the row
        }
    }

    [Fact]
    public void A_row_rule_colours_every_cell_of_the_rows_that_satisfy_it ()
    {
        var grid = Grid (out var form);

        using (form) {
            grid.Columns["Amount"]!.ConditionalFormattingObjectList.Add (
                new ExpressionFormattingObject ("r2", "When < #2024-06-01#", true) { RowBackColor = System.Drawing.Color.Pink });

            Assert.Equal (new[] { "Bravo", "Charlie" }, Coloured (grid, "Name", Pink));
            Assert.Equal (new[] { "Bravo", "Charlie" }, Coloured (grid, "When", Pink));
        }
    }

    [Fact]
    public void An_expression_that_cannot_be_evaluated_matches_nothing ()
    {
        var grid = Grid (out var form);

        using (form) {
            grid.Columns["Name"]!.ConditionalFormattingObjectList.Add (
                new ExpressionFormattingObject ("bad", "NoSuchColumn > 1", false) { CellBackColor = System.Drawing.Color.Gold });

            Assert.Empty (Coloured (grid, "Name", Gold));
        }
    }
}
