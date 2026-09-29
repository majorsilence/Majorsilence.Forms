using System.ComponentModel;
using System.Data;
using System.Drawing;
using Majorsilence.Forms.Headless;
using Majorsilence.Forms.Telerik;
using Xunit;

namespace Majorsilence.Forms.Tests;

// W6 mechanisms, twentieth chunk (#176): RadTreeView's five binding members and SpacingBetweenNodes,
// which were stored and never read -- a bound tree stayed empty.
[Collection ("Headless")]
public class W6TelerikTreeViewTests
{
    private static string Shape (IEnumerable<RadTreeNode> nodes)
        => string.Join (",", nodes.Select (n => n.Nodes.Count == 0 ? n.Text : n.Text + "(" + Shape (n.Nodes) + ")"));

    [Fact]
    public void A_self_referencing_table_builds_the_hierarchy ()
    {
        HeadlessRenderer.Use ();
        var table = new DataTable ();
        table.Columns.Add ("ID", typeof (int));
        table.Columns.Add ("ParentID", typeof (int));
        table.Columns.Add ("Name", typeof (string));
        table.Rows.Add (3, 1, "Invoices");        // listed before its parent
        table.Rows.Add (1, DBNull.Value, "Finance");
        table.Rows.Add (2, DBNull.Value, "Planning");
        table.Rows.Add (4, 3, "Overdue");
        table.Rows.Add (5, 99, "Orphan");         // parent not in the list: a root

        using var tree = new RadTreeView {
            DisplayMember = "Name",
            ValueMember = "ID",
            ChildMember = "ID",
            ParentMember = "ParentID",
            DataSource = table,
        };

        Assert.Equal ("Finance(Invoices(Overdue)),Planning,Orphan", Shape (tree.Nodes));

        var overdue = tree.FindNodes (n => n.Text == "Overdue").Single ();
        Assert.Equal (4, overdue.Value);
        Assert.Equal ("Overdue", ((DataRowView) overdue.DataBoundItem!)["Name"]);
        Assert.Equal ("Invoices", overdue.Parent!.Text);

        // The table announces its changes through its view, and the tree follows.
        table.Rows.Add (6, 2, "Zoning");
        Assert.Equal ("Finance(Invoices(Overdue)),Planning(Zoning),Orphan", Shape (tree.Nodes));
    }

    private sealed class Department
    {
        public int Id { get; set; }
        public int? ParentId { get; set; }
        public string Title { get; set; } = string.Empty;
    }

    [Fact]
    public void A_flat_list_binds_as_roots_and_a_binding_list_rebuilds_the_tree ()
    {
        HeadlessRenderer.Use ();
        var list = new BindingList<Department> {
            new () { Id = 1, Title = "Alpha" },
            new () { Id = 2, Title = "Beta" },
        };

        using var tree = new RadTreeView { DisplayMember = "Title", DataSource = list };

        Assert.Equal ("Alpha,Beta", Shape (tree.Nodes));
        Assert.Same (list[0], tree.Nodes[0]!.Value);   // no ValueMember: the item itself

        list.Add (new Department { Id = 3, Title = "Gamma" });
        Assert.Equal ("Alpha,Beta,Gamma", Shape (tree.Nodes));

        // Naming the key members turns the same list into a hierarchy.
        list[1].ParentId = 1;
        tree.ChildMember = "Id";
        tree.ParentMember = "ParentId";
        Assert.Equal ("Alpha(Beta),Gamma", Shape (tree.Nodes));
    }

    [Fact]
    public void A_parent_loop_is_broken_rather_than_lost ()
    {
        HeadlessRenderer.Use ();
        var list = new List<Department> {
            new () { Id = 1, ParentId = 2, Title = "A" },
            new () { Id = 2, ParentId = 1, Title = "B" },
            new () { Id = 3, ParentId = 3, Title = "Self" },
        };

        using var tree = new RadTreeView { DisplayMember = "Title", ChildMember = "Id", ParentMember = "ParentId", DataSource = list };

        var all = tree.FindNodes (_ => true).Select (n => n.Text).OrderBy (t => t, StringComparer.Ordinal);
        Assert.Equal (new[] { "A", "B", "Self" }, all);
        Assert.Equal (2, tree.Nodes.Count);   // the A-B loop hangs from one root, Self is its own
    }

    [Fact]
    public void SpacingBetweenNodes_adds_to_every_rows_height ()
    {
        HeadlessRenderer.Use ();
        using var form = new Form { Width = 300, Height = 300 };
        var tree = new RadTreeView { Width = 200, Height = 200 };
        form.Controls.Add (tree);
        var node = tree.Nodes.Add ("one");
        form.Show ();

        var before = node.GetPreferredSize (Size.Empty).Height;
        var row_before = tree.ScaledItemHeight;

        tree.SpacingBetweenNodes = 6;

        Assert.Equal (before + tree.LogicalToDeviceUnits (6), node.GetPreferredSize (Size.Empty).Height);
        Assert.Equal (row_before + tree.LogicalToDeviceUnits (6), tree.ScaledItemHeight);
    }
}
