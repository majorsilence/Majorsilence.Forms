using System;
using System.Linq;
using System.Drawing;
using Majorsilence.Forms;
using Majorsilence.Forms.VisualStyles;
using Xunit;

namespace Majorsilence.Forms.Tests;

/// <summary>
/// The behavioural half of the parity work done for the Krypton Standard Toolkit port. Not the stubs --
/// those are covered by <see cref="NoOpStubBaselineTests"/> and described in COMPATIBILITY_MATRIX.md --
/// but the members whose shape or wiring changed, where a regression would be silent.
/// </summary>
public class KryptonPortParityTests
{
    // A node returned by any WinForms-shaped member has to be assignable to a TreeNode variable without a
    // cast. It was the other way round -- TreeNode derived from TreeViewItem -- so every one of these
    // needed a downcast, and a migration that needs casts inserted is not a recompile.
    [Fact]
    public void TreeMembers_ReturnNodesAssignableToTreeNode ()
    {
        using var tree = new TreeView ();

        TreeNode added = tree.Nodes.Add ("root");
        TreeNode child = added.Nodes.Add ("child");

        tree.SelectedNode = child;

        TreeNode? selected = tree.SelectedNode;
        TreeNode? parent = child.Parent;
        TreeNode indexed = tree.Nodes[0];

        Assert.Same (child, selected);
        Assert.Same (added, parent);
        Assert.Same (added, indexed);
    }

    // The rename turned `Add ((TreeViewItem)node)` -- an upcast that reached Collection<T>.Add -- into a
    // call that bound to the same method. Nothing caught it until the stack ran out mid-suite, so the
    // ctor that goes through AddRange is worth pinning directly.
    [Fact]
    public void TreeNode_WithChildren_DoesNotRecurse ()
    {
        var node = new TreeNode ("parent", new TreeNode ("a"), new TreeNode ("b"));

        Assert.Equal (2, node.Nodes.Count);
        Assert.Equal (new[] { "a", "b" }, node.Nodes.Select (n => n.Text));
        Assert.Equal (1, node.Nodes.IndexOf (node.Nodes[1]));
        Assert.True (node.Nodes.Contains (node.Nodes[0]));
    }

    // ListBox.ObjectCollection / ComboBox.ObjectCollection / SelectedObjectCollection are the type names
    // WinForms code writes when it re-exposes these properties; they returned base types before.
    [Fact]
    public void ListAndComboItems_AreTheNestedCollectionTypes ()
    {
        using var list = new ListBox ();
        using var combo = new ComboBox ();

        ListBox.ObjectCollection listItems = list.Items;
        ComboBox.ObjectCollection comboItems = combo.Items;
        ListBox.SelectedObjectCollection selected = list.SelectedItems;

        Assert.NotNull (listItems);
        Assert.NotNull (comboItems);
        Assert.NotNull (selected);

        // The combo's items are the popup list's items, not a second collection over the same values --
        // one would diverge from the other on the first change.
        combo.Items.Add ("one");
        Assert.Equal (1, combo.Items.Count);
        Assert.Same (comboItems, combo.Items);
    }

    [Fact]
    public void CheckedListBox_SubstitutesItsOwnItemCollection ()
    {
        using var checkedList = new CheckedListBox ();

        // CheckedListBox.Items is its own check-tracking wrapper; the collection the list box itself holds
        // is what CreateItemCollection substitutes, so that is what this pins.
        Assert.IsType<CheckedListBox.ObjectCollection> (((ListBox)checkedList).Items);
    }

    // ToolStripDropDownButton derived straight from ToolStripItem, so DropDown did not exist on it at all
    // and closing a menu through button.DropDown.Close (...) would not compile.
    [Fact]
    public void ToolStripDropDownButton_HasARealDropDown ()
    {
        var button = new ToolStripDropDownButton ("File");

        Assert.IsAssignableFrom<ToolStripDropDownItem> (button);
        Assert.False (button.HasDropDown);

        var dropDown = button.DropDown;

        Assert.NotNull (dropDown);
        Assert.True (button.HasDropDown);
        Assert.Same (dropDown, button.DropDown);
    }

    // The drop-down must be a view onto its item, not a second menu beside it. It was created without
    // an owner, so items added through DropDownItems went into a collection the strip never rendered
    // and DropDown.Close() closed an orphan while the real menu stayed open -- both silently.
    [Fact]
    public void ToolStripDropDownButton_DropDownIsAViewOntoTheItem ()
    {
        var button = new ToolStripDropDownButton ("File");

        button.DropDownItems.Add (new ToolStripMenuItem ("Open"));

        // The item's own Items is what the strip renders and automation walks, so the added item must
        // land there -- and the drop-down's Items must be that same collection, not a copy.
        Assert.Single (button.Items);
        Assert.Same (button.Items, button.DropDown.Items);
        Assert.True (button.HasDropDownItems);

        // Never opened: the facade agrees.
        Assert.False (button.DropDown.Visible);
        Assert.False (button.Pressed);
    }

    [Fact]
    public void ToolStripDropDownButton_CloseClosesTheRealMenu ()
    {
        using var form = new Form ();
        using var strip = new ToolStrip ();
        var button = new ToolStripDropDownButton ("File");

        button.DropDownItems.Add (new ToolStripMenuItem ("Open"));
        strip.Items.Add (button);
        form.Controls.Add (strip);
        form.Show ();

        button.ShowDropDown ();
        Assert.True (button.DropDown.Visible);
        Assert.True (button.Pressed);

        // Krypton's focus-lost path: close the shown menu through the drop-down facade.
        button.DropDown.Close (ToolStripDropDownCloseReason.AppFocusChange);
        Assert.False (button.DropDown.Visible);
        Assert.False (button.Pressed);
    }

    // A cell style set on the grid has to read back as a DataGridViewCellStyle -- the conversion existed
    // one way only, so a derived grid could not re-expose DefaultCellStyle with the WinForms type.
    [Fact]
    public void GridCellStyle_RoundTripsThroughDataGridViewCellStyle ()
    {
        using var grid = new DataGridView ();

        grid.DefaultCellStyle = new DataGridViewCellStyle {
            BackColor = Color.Red,
            ForeColor = Color.White,
            Alignment = DataGridViewContentAlignment.MiddleRight,
            WrapMode = DataGridViewTriState.True,
        };

        DataGridViewCellStyle read = grid.DefaultCellStyle;

        // Compared by ARGB: the round trip goes through a Skia colour, so a named Color comes back as its
        // numeric equivalent and Color.Equals distinguishes the two.
        Assert.Equal (Color.Red.ToArgb (), read.BackColor.ToArgb ());
        Assert.Equal (Color.White.ToArgb (), read.ForeColor.ToArgb ());
        Assert.Equal (DataGridViewContentAlignment.MiddleRight, read.Alignment);
        Assert.Equal (DataGridViewTriState.True, read.WrapMode);
    }

    [Fact]
    public void DataGridViewColumn_IsAComponentAndNotifiesOnDispose ()
    {
        var column = new DataGridViewColumn ("Header");
        var disposed = 0;

        Assert.IsAssignableFrom<System.ComponentModel.IComponent> (column);

        column.Disposed += (_, _) => disposed++;
        column.Dispose ();

        Assert.Equal (1, disposed);
    }

    // AddStrip really slices the sheet: adding it whole would draw every glyph as the entire strip, which
    // is a visible fault rather than a missing feature.
    [Fact]
    public void ImageCollection_AddStrip_SlicesTheSheetIntoFrames ()
    {
        using var images = new ImageList { ImageSize = new Size (8, 8) };
        using var strip = new Majorsilence.Forms.Drawing.Bitmap (8 * 4, 8);

        var first = images.Images.AddStrip (strip);

        Assert.Equal (0, first);
        Assert.Equal (4, images.Images.Count);
    }

    [Fact]
    public void ImageCollection_AddStrip_RejectsAMisalignedSheet ()
    {
        using var images = new ImageList { ImageSize = new Size (8, 8) };
        using var strip = new Majorsilence.Forms.Drawing.Bitmap (9, 8);

        Assert.Throws<ArgumentException> (() => images.Images.AddStrip (strip));
    }

    [Fact]
    public void Padding_AddAndSubtract_ComposeEdgewise ()
    {
        var a = new Padding (1, 2, 3, 4);
        var b = new Padding (10, 20, 30, 40);

        Assert.Equal (new Padding (11, 22, 33, 44), Padding.Add (a, b));
        Assert.Equal (new Padding (9, 18, 27, 36), Padding.Subtract (b, a));
        Assert.Equal (Padding.Add (a, b), a + b);
        Assert.Equal (Padding.Subtract (b, a), b - a);
    }

    // ClientSize was read-only, so the normal way a dialog sizes itself to its content did not compile.
    [Fact]
    public void Control_ClientSize_SetGrowsBySizeOfBorder ()
    {
        using var panel = new Panel { Size = new Size (100, 50) };

        var border = new Size (panel.Width - panel.ClientSize.Width, panel.Height - panel.ClientSize.Height);

        panel.ClientSize = new Size (200, 120);

        Assert.Equal (new Size (200, 120), panel.ClientSize);
        Assert.Equal (new Size (200 + border.Width, 120 + border.Height), panel.Size);
    }

    // A form's handle being destroyed is the last notification it sends, so code tracking the set of live
    // forms keys on it rather than on Closed.
    [Fact]
    public void Form_HandleDestroyed_IsRaisedWhenTheFormCloses ()
    {
        using var form = new Form ();
        var order = new System.Collections.Generic.List<string> ();

        form.Closed += (_, _) => order.Add ("closed");
        form.HandleDestroyed += (_, _) => order.Add ("handleDestroyed");

        form.Show ();
        form.Close ();

        Assert.Equal (new[] { "closed", "handleDestroyed" }, order);
    }

    // ItemSelectionChanged was an EventHandler with empty accessors: it carried none of the information
    // the event exists to carry, and dropped its handlers besides.
    [Fact]
    public void ListView_ItemSelectionChanged_ReportsDeselectionThenSelection ()
    {
        using var view = new ListView ();
        var first = view.Items.Add ("first");
        var second = view.Items.Add ("second");
        var seen = new System.Collections.Generic.List<(string Text, bool Selected)> ();

        view.ItemSelectionChanged += (_, e) => seen.Add ((e.Item.Text, e.IsSelected));

        // WinForms order: the per-item changes land before SelectedIndexChanged, so a handler of the
        // latter reads a selection that has already settled.
        view.SelectedIndexChanged += (_, _) =>
            Assert.Equal (view.SelectedItem?.Text == "first" ? 1 : 3, seen.Count);

        view.SelectedItem = first;
        view.SelectedItem = second;

        Assert.Equal (
            new[] { ("first", true), ("first", false), ("second", true) },
            seen);
    }

    [Fact]
    public void MaskedTextBox_CutCopyMaskFormat_IsAMaskFormat ()
    {
        using var box = new MaskedTextBox { CutCopyMaskFormat = MaskFormat.ExcludePromptAndLiterals };

        Assert.Equal (MaskFormat.ExcludePromptAndLiterals, box.CutCopyMaskFormat);
    }

    [Fact]
    public void DomainUpDown_Items_IsTheNestedCollectionType ()
    {
        using var updown = new DomainUpDown ();

        DomainUpDown.DomainUpDownItemCollection items = updown.Items;
        items.Add ("alpha");
        items.Add ("beta");

        // Selection is matched by value, not by reference: the items are objects now, so `==` would have
        // become a reference comparison that fails for two equal strings in different instances.
        updown.SelectedItem = new string ("beta".ToCharArray ());

        Assert.Equal (1, updown.SelectedIndex);
        Assert.Equal ("beta", updown.Text);
    }

    // A container can report a degenerate DisplayRectangle transiently (a themed form's root panel
    // does, mid-construction). Anchoring against it collapsed every anchored child to zero width, and
    // the next re-init laundered the collapse into the stored anchor deltas -- permanently. The layout
    // engine now skips the anchor pass against a degenerate rectangle, so the collapse never happens
    // and the deltas stay true.
    [Fact]
    public void AnchoredChild_SurvivesATransientlyCollapsedParent ()
    {
        using var parent = new Panel { Size = new Size (400, 100) };
        var child = new Panel {
            Bounds = new Rectangle (10, 10, 380, 30),
            Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right,
        };
        parent.Controls.Add (child);
        parent.PerformLayout ();

        parent.Size = new Size (0, 0);        // the transient collapse
        parent.PerformLayout ();
        parent.Size = new Size (500, 100);    // the recovery
        parent.PerformLayout ();

        // Designed 380 wide with 10 left / 10 right in a 400-wide parent; at 500 the anchors give
        // 480. Before the fix this came back 0: the child had been shrunk against the empty rectangle
        // and its recorded distances rewritten from the shrunken bounds.
        Assert.Equal (480, child.Width);
        Assert.Equal (10, child.Left);
    }

    [Fact]
    public void SystemInformation_ReportsTheWin32Constants ()
    {
        Assert.Equal (120, SystemInformation.MouseWheelScrollDelta);   // WHEEL_DELTA
        Assert.Equal (400, SystemInformation.MenuShowDelay);
        Assert.False (SystemInformation.HighContrast);
        Assert.Equal (Application.UserInteractive, SystemInformation.UserInteractive);
    }

    // Absent is the answer callers branch on, and the useful direction to be wrong in: code testing for
    // layered windows picks the plain effect over the alpha-blended one, which is the one that draws.
    [Fact]
    public void OSFeature_ReportsEveryFeatureAbsent ()
    {
        Assert.Null (OSFeature.Feature.GetVersionPresent (OSFeature.LayeredWindows));
        Assert.Null (OSFeature.Feature.GetVersionPresent (OSFeature.Themes));
        Assert.False (OSFeature.Feature.IsPresent (OSFeature.LayeredWindows));
        Assert.False (OSFeature.IsPresent (SystemParameter.DropShadow));
        Assert.False (VisualStyleInformation.IsEnabledByUser);
        Assert.Equal (string.Empty, VisualStyleInformation.ColorScheme);
    }

    [Fact]
    public void InputLanguage_AnswersFromTheCulture ()
    {
        var current = InputLanguage.CurrentInputLanguage;

        Assert.NotNull (current);
        Assert.Equal (System.Globalization.CultureInfo.CurrentCulture.Name, current!.Culture.Name);
        Assert.Equal (current.Culture.EnglishName, current.LayoutName);
        Assert.Equal (IntPtr.Zero, current.Handle);

        Assert.NotEmpty (InputLanguage.InstalledInputLanguages);
        Assert.NotNull (InputLanguage.FromCulture (System.Globalization.CultureInfo.CurrentCulture));
    }

    [Fact]
    public void ToolStripRenderEventArgs_KeepsTheGraphicsItWasGiven ()
    {
        using var strip = new ToolStrip { Size = new Size (120, 24) };
        using var graphics = strip.CreateGraphics ();

        var args = new ToolStripRenderEventArgs (graphics, strip);

        // Graphics was left at its default before, so every renderer reading it got null from an
        // argument the caller had supplied.
        Assert.Same (graphics, args.Graphics);
        Assert.Same (strip, args.ToolStrip);
        Assert.Equal (new Rectangle (Point.Empty, strip.Size), args.AffectedBounds);
    }
}
