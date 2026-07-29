using Majorsilence.Forms;

namespace PointOfSale.Client.Controls;

/// <summary>
/// DataGridView.AutoGenerateColumns is unreliable to depend on for a fixed set of screens (its
/// column order/formatting isn't something we control) — every bound grid in this app defines its
/// columns explicitly and disables auto-generation, then rebinds DataSource on every refresh since
/// there's no live property-change notification in this framework's binding.
/// </summary>
public static class GridColumns
{
    public static DataGridViewColumn AddBound(DataGridView grid, string propertyName, string headerText, int width)
    {
        var column = grid.Columns.Add(propertyName, headerText);
        column.DataPropertyName = propertyName;
        column.Width = width;
        return column;
    }

    public static void Rebind<T>(DataGridView grid, IList<T> items)
    {
        grid.DataSource = null;
        grid.DataSource = items;
    }
}
