using System.Data;
using Majorsilence.Forms.Headless;
using Xunit;

namespace Majorsilence.Forms.Tests;

// SelectedValue finds the row as WinForms does (CurrencyManager.Find): through the bound list's own search when it has one.
// A DataView compares as its DataTable does -- trailing spaces ignored, case-insensitive by default -- so a char column's
// padded 'MISCINV   ' is selected by "MISCINV". Comparing with Equals selected nothing and left SelectedValue null.
[Collection ("Headless")]
public class ListControlFindValueTests
{
    private static DataTable Templates ()
    {
        var table = new DataTable ();
        table.Columns.Add ("codename", typeof (string));
        table.Columns.Add ("DisplayName", typeof (string));
        table.Rows.Add ("MISCINV   ", "Miscellaneous Invoice");
        table.Rows.Add ("MISCINV2", "Misc Inv - Template 2");
        return table;
    }

    [Fact]
    public void ComboBox_selects_a_padded_char_value_as_the_DataTable_compares_it ()
    {
        HeadlessRenderer.Use ();
        using var combo = new ComboBox { DisplayMember = "DisplayName", ValueMember = "codename", DataSource = Templates () };

        combo.SelectedValue = "MISCINV";

        Assert.Equal (0, combo.SelectedIndex);
        Assert.Equal ("MISCINV   ", combo.SelectedValue);
    }

    [Fact]
    public void ComboBox_selects_by_value_case_insensitively_like_the_DataTable ()
    {
        HeadlessRenderer.Use ();
        using var combo = new ComboBox { DisplayMember = "DisplayName", ValueMember = "codename", DataSource = Templates () };

        combo.SelectedValue = "miscinv2";

        Assert.Equal (1, combo.SelectedIndex);
    }

    [Fact]
    public void ListBox_selects_a_padded_char_value_as_the_DataTable_compares_it ()
    {
        HeadlessRenderer.Use ();
        using var list = new ListBox { DisplayMember = "DisplayName", ValueMember = "codename", DataSource = Templates () };

        list.SelectedValue = "MISCINV";

        Assert.Equal (0, list.SelectedIndex);
    }

    [Fact]
    public void A_plain_list_still_matches_by_Equals ()
    {
        HeadlessRenderer.Use ();
        using var combo = new ComboBox { DataSource = new System.Collections.Generic.List<string> { "A", "B " } };

        combo.SelectedValue = "B";

        Assert.Equal (-1, combo.SelectedIndex);
    }
}
