using System.ComponentModel;
using System.Linq;
using Majorsilence.Forms.Headless;
using Majorsilence.Forms.Telerik;
using Xunit;

namespace Majorsilence.Forms.Tests;

// RadTreeView's Filter, TreeIndent, LineWidth and RadContextMenu, RadTreeNode.Selected, and the
// NodeElement a NodeFormatting handler sets a node's image through -- members Telerik code uses that the
// compat tree did not have.
[Collection ("Headless")]
public class TelerikTreeViewMembersTests
{
    private static RadTreeView Tree (out Form form)
    {
        HeadlessRenderer.Use ();

        var tree = new RadTreeView { Width = 260, Height = 300 };

        var finance = tree.Nodes.Add ("Finance");
        finance.Nodes.Add ("Invoices");
        finance.Nodes.Add ("Payroll");

        var planning = tree.Nodes.Add ("Planning");
        planning.Nodes.Add ("Zoning invoices");

        tree.Nodes.Add ("Mapping");
        tree.ExpandAll ();

        form = new Form { Width = 360, Height = 400 };
        form.Controls.Add (tree);
        form.Show ();

        return tree;
    }

    private static string Rows (RadTreeView tree) => string.Join (",", tree.LayoutItems ().Select (n => n.Text));

    [Fact]
    public void Filter_shows_the_matches_and_the_path_down_to_them ()
    {
        var tree = Tree (out var form);

        using (form) {
            Assert.Equal ("Finance,Invoices,Payroll,Planning,Zoning invoices,Mapping", Rows (tree));

            // Matched in any case; a parent with a match below it stays, one without goes.
            tree.Filter = "INVOICE";
            Assert.Equal ("Finance,Invoices,Planning,Zoning invoices", Rows (tree));

            tree.Filter = "Mapping";
            Assert.Equal ("Mapping", Rows (tree));

            tree.Filter = null;
            Assert.Equal ("Finance,Invoices,Payroll,Planning,Zoning invoices,Mapping", Rows (tree));

            tree.Filter = "";
            Assert.Equal ("Finance,Invoices,Payroll,Planning,Zoning invoices,Mapping", Rows (tree));
        }
    }

    private sealed class Item
    {
        public int Id { get; set; }
        public string Title { get; set; } = string.Empty;
    }

    [Fact]
    public void A_bound_tree_keeps_its_filter_when_it_rebuilds ()
    {
        HeadlessRenderer.Use ();

        var list = new BindingList<Item> { new () { Id = 1, Title = "Alpha" }, new () { Id = 2, Title = "Beta" } };

        using var form = new Form { Width = 360, Height = 400 };
        var tree = new RadTreeView { Width = 260, Height = 300, DisplayMember = "Title", DataSource = list, Filter = "a" };
        form.Controls.Add (tree);
        form.Show ();

        Assert.Equal ("Alpha,Beta", Rows (tree));

        tree.Filter = "alp";
        Assert.Equal ("Alpha", Rows (tree));

        list.Add (new Item { Id = 3, Title = "Alpine" });
        list.Add (new Item { Id = 4, Title = "Gamma" });
        Assert.Equal ("Alpha,Alpine", Rows (tree));
    }

    [Fact]
    public void Selected_selects_the_node_and_clearing_it_leaves_none ()
    {
        var tree = Tree (out var form);

        using (form) {
            var payroll = tree.FindNodes (n => n.Text == "Payroll").Single ();
            var mapping = tree.FindNodes (n => n.Text == "Mapping").Single ();

            Assert.False (payroll.Selected);

            payroll.Selected = true;
            Assert.Same (payroll, tree.SelectedNode);
            Assert.True (payroll.Selected);

            // Clearing another node's flag leaves the selection where it is.
            mapping.Selected = false;
            Assert.Same (payroll, tree.SelectedNode);

            payroll.Selected = false;
            Assert.Null (tree.SelectedNode);
            Assert.False (payroll.Selected);

            // A node outside any tree is never selected.
            var detached = new RadTreeNode ("loose") { Selected = true };
            Assert.False (detached.Selected);
        }
    }

    [Fact]
    public void An_image_set_on_the_node_element_is_the_one_the_node_is_drawn_with ()
    {
        var tree = Tree (out var form);

        using (form) {
            using var icon = new Majorsilence.Forms.Drawing.Bitmap (12, 12);

            tree.NodeFormatting += (_, e) => {
                Assert.Same (e.Node, e.NodeElement.Data);

                if (e.Node.Text == "Invoices")
                    e.NodeElement.ImageElement.Image = icon;
            };

            using var _ = PaintSurface.Render (tree);

            Assert.Same (icon, tree.FindNodes (n => n.Text == "Invoices").Single ().Image);
            Assert.Null (tree.FindNodes (n => n.Text == "Payroll").Single ().Image);
        }
    }

    [Fact]
    public void LineWidth_is_the_width_the_lines_are_drawn ()
    {
        var tree = Tree (out var form);

        using (form) {
            tree.ShowLines = true;

            Assert.Equal (1f, tree.LineWidth);
            var thin = LineInk (tree);

            tree.LineWidth = 3;
            var thick = LineInk (tree);

            Assert.True (thin > 0, "No line was drawn.");
            Assert.True (thick >= thin * 2, $"LineWidth = 3 drew {thick} pixels of line against {thin} at 1.");
        }
    }

    [Fact]
    public void TreeIndent_is_the_indent_of_each_level ()
    {
        var tree = Tree (out var form);

        using (form) {
            // Without lines the first ink on a leaf's row is its text.
            tree.ShowLines = false;

            var invoices = tree.FindNodes (n => n.Text == "Invoices").Single ();
            var before = TextLeft (tree, invoices);

            tree.TreeIndent = tree.Indent + 30;

            Assert.Equal (tree.Indent, tree.TreeIndent);
            Assert.Equal (before + tree.LogicalToDeviceUnits (30), TextLeft (tree, invoices));
        }
    }

    [Fact]
    public void RadContextMenu_is_the_menu_the_tree_shows ()
    {
        var tree = Tree (out var form);

        using (form) {
            var menu = new RadContextMenu ();

            tree.RadContextMenu = menu;
            Assert.Same (menu, tree.RadContextMenu);
            Assert.Same (menu, RadContextMenuManager.GetRadContextMenu (tree));

            tree.RadContextMenu = null;
            Assert.Null (RadContextMenuManager.GetRadContextMenu (tree));
        }
    }

    // The first column inside the border of a node's row that is not background: where its text starts, for a leaf.
    private static int TextLeft (RadTreeView tree, TreeNode node)
    {
        using var bitmap = PaintSurface.Render (tree);

        var y_from = node.Bounds.Top;
        var y_to = System.Math.Min (node.Bounds.Bottom, bitmap.Height);

        // Past the control's border, which is ink on every row.
        for (var x = tree.LogicalToDeviceUnits (3); x < bitmap.Width - 2; x++)
            for (var y = y_from; y < y_to; y++)
                if (bitmap.GetPixel (x, y) != bitmap.GetPixel (bitmap.Width - 2, y))
                    return x;

        return -1;
    }

    // Ink in the root gutter over the rows of the root's children -- only connectors are drawn there.
    private static int LineInk (RadTreeView tree)
    {
        using var bitmap = PaintSurface.Render (tree);

        var root = tree.Nodes[0]!;
        var centre = root.Bounds.Left + 2 + tree.LogicalToDeviceUnits (10) / 2;
        var ink = 0;

        foreach (var child in root.Nodes)
            for (var y = child.Bounds.Top; y < child.Bounds.Bottom && y < bitmap.Height; y++)
                for (var x = System.Math.Max (0, centre - 4); x <= centre + 4; x++)
                    if (bitmap.GetPixel (x, y) != bitmap.GetPixel (bitmap.Width - 2, y))
                        ink++;

        return ink;
    }
}
