using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using Majorsilence.Forms.Automation;
using Majorsilence.Forms.Headless;
using Xunit;

namespace Majorsilence.Forms.Tests;

// The accessibility DOM's popups (combo box lists, menu drop-downs, context menus, tool tips) and its
// live region (docs/backends.md, "Accessibility DOM (browser)"). Host-neutral like the rest of
// AriaDomTests: the mapping, the owner links, the diff when a popup closes, and what is announced.
public partial class AriaDomTests
{
    // The form and the popups opened for it (or for its popups), in the order they were shown -- what the
    // browser mirror reads, limited to this test's windows so parallel tests' popups do not leak in.
    private static List<AriaNode> BuildWithPopups (Form form)
    {
        HeadlessRenderer.CapturePng (form, form.Width, form.Height);
        return AriaDom.Build (WindowsOf (form));
    }

    private static WindowBase[] WindowsOf (Form form) =>
        new WindowBase[] { form }.Concat (PopupWindow.ShownPopups.Where (p => BelongsTo (p, form))).ToArray ();

    private static bool BelongsTo (PopupWindow popup, Form form)
    {
        for (WindowBase w = popup; ; w = ((PopupWindow) w).ParentWindow) {
            if (ReferenceEquals (w, form))
                return true;
            if (w is not PopupWindow)
                return false;
        }
    }

    private static AriaNode NodeOf (IEnumerable<AriaNode> nodes, object source) =>
        Assert.Single (nodes, n => ReferenceEquals (n.Source, source));

    private static Form ComboForm (out ComboBox combo)
    {
        var form = new Form { Text = "Order", Width = 400, Height = 300 };
        var label = new Label { Name = "fruitLabel", Text = "Fruit", Left = 10, Top = 10, Width = 60 };
        combo = new ComboBox { Name = "fruit", AccessibleName = "Fruit", Left = 80, Top = 40, Width = 150, DropDownStyle = ComboBoxStyle.DropDownList };
        combo.Items.AddRange (new object[] { "Apple", "Banana", "Cherry" });
        combo.SelectedIndex = 1;
        form.Controls.Add (label);
        form.Controls.Add (combo);
        form.Show ();
        return form;
    }

    [Fact]
    public void An_open_combo_box_list_is_a_listbox_the_combo_box_controls ()
    {
        using var form = ComboForm (out var combo);
        var closed = BuildWithPopups (form);
        Assert.Equal ("false", NodeOf (closed, combo)["aria-expanded"]);
        Assert.Null (NodeOf (closed, combo)["aria-controls"]);

        combo.DroppedDown = true;
        var nodes = BuildWithPopups (form);
        var window = nodes[0];

        // The popup is inside the form's element, so it is inside the dialog when the form is one.
        var popup = Assert.Single (nodes, n => n["data-mf-popup"] == "listbox");
        Assert.Equal (window.Id, popup.ParentId);

        var list = Assert.Single (nodes, n => n.ParentId == popup.Id);
        Assert.Equal ("listbox", list.Role);
        Assert.Equal ("Fruit", list["aria-label"]);
        var options = nodes.Where (n => n.ParentId == list.Id).ToList ();
        Assert.Equal (new[] { "Apple", "Banana", "Cherry" }, options.Select (o => o.Text));
        Assert.Equal (new[] { "false", "true", "false" }, options.Select (o => o["aria-selected"]));

        var owner = NodeOf (nodes, combo);
        Assert.Equal ("true", owner["aria-expanded"]);
        Assert.Equal (list.ElementId, owner["aria-controls"]);

        // The selected option is where the keyboard is while the list is open.
        Assert.Same (options[1], AriaDom.ActiveNode (nodes));

        // Positioned where it is drawn: just under the combo box (ComboBox shows it at (1, Height)),
        // in coordinates relative to the form's element as the combo box's own are.
        Assert.Equal (new Point (owner.Bounds.X + 1, owner.Bounds.Bottom), popup.Bounds.Location);

        combo.DroppedDown = false;
        var after = BuildWithPopups (form);
        var ops = AriaDom.Diff (nodes.ToDictionary (n => n.Id), after);

        // One removal for the whole popup, and the combo box collapses and drops its link.
        Assert.Equal (popup.Id, Assert.Single (ops, o => o.IsRemove).Id);
        var collapsed = Assert.Single (ops, o => !o.IsRemove && ReferenceEquals (o.Node!.Source, combo)).Node!;
        Assert.Equal ("false", collapsed["aria-expanded"]);
        Assert.Null (collapsed["aria-controls"]);

        form.Close ();
    }

    private static Form MenuForm (out ToolStripMenuItem file, out ToolStripMenuItem recent, out ToolStripMenuItem wrap)
    {
        var form = new Form { Text = "Editor", Width = 500, Height = 300 };
        var strip = new MenuStrip ();
        file = new ToolStripMenuItem ("&File");
        recent = new ToolStripMenuItem ("&Recent");
        recent.DropDownItems.Add (new ToolStripMenuItem ("notes.txt"));
        wrap = new ToolStripMenuItem ("&Word wrap") { CheckOnClick = true, Checked = true };
        file.DropDownItems.Add (new ToolStripMenuItem ("&Open"));
        file.DropDownItems.Add (recent);
        file.DropDownItems.Add (new ToolStripSeparator ());
        file.DropDownItems.Add (wrap);
        file.DropDownItems.Add (new ToolStripMenuItem ("E&xit") { Enabled = false });
        strip.Items.Add (file);
        form.Controls.Add (strip);
        form.Show ();
        return form;
    }

    [Fact]
    public void Menu_drop_downs_are_menus_of_their_items_with_check_disabled_and_submenu_states ()
    {
        using var form = MenuForm (out var file, out var recent, out var wrap);
        var closed = BuildWithPopups (form);

        var bar = Assert.Single (closed, n => n.Role == "menubar");
        var file_node = NodeOf (closed, file);
        Assert.Equal (bar.Id, file_node.ParentId);
        Assert.Equal ("menuitem", file_node.Role);
        Assert.Equal ("File", file_node.Text);
        Assert.Equal ("menu", file_node["aria-haspopup"]);
        Assert.Equal ("false", file_node["aria-expanded"]);
        // A closed submenu is not in the DOM: the automation tree nests it under the item, ARIA does not.
        Assert.DoesNotContain (closed, n => n.ParentId == file_node.Id);

        file.ShowDropDown ();
        var nodes = BuildWithPopups (form);

        var popup = Assert.Single (nodes, n => n["data-mf-popup"] == "menu");
        Assert.Equal (nodes[0].Id, popup.ParentId);
        var menu = Assert.Single (nodes, n => n.ParentId == popup.Id);
        Assert.Equal ("menu", menu.Role);
        Assert.Equal ("File", menu["aria-label"]);

        var items = nodes.Where (n => n.ParentId == menu.Id).ToList ();
        Assert.Equal (new[] { "menuitem", "menuitem", "separator", "menuitemcheckbox", "menuitem" }, items.Select (i => i.Role));
        Assert.Equal (new[] { "Open", "Recent", null, "Word wrap", "Exit" }, items.Select (i => i.Text));
        Assert.Equal ("true", items[3]["aria-checked"]);
        Assert.Equal ("true", items[4]["aria-disabled"]);
        Assert.Null (items[0]["aria-disabled"]);
        Assert.Equal ("menu", items[1]["aria-haspopup"]);
        Assert.Equal ("false", items[1]["aria-expanded"]);

        var opened = NodeOf (nodes, file);
        Assert.Equal ("true", opened["aria-expanded"]);
        Assert.Equal (menu.ElementId, opened["aria-controls"]);

        // A submenu opens inside the menu it hangs off, and its item says so.
        recent.ShowDropDown ();
        var deeper = BuildWithPopups (form);
        var sub_popup = Assert.Single (deeper, n => n["data-mf-popup"] == "menu" && n.Id != popup.Id);
        Assert.Equal (popup.Id, sub_popup.ParentId);
        var sub_menu = Assert.Single (deeper, n => n.ParentId == sub_popup.Id);
        Assert.Equal ("Recent", sub_menu["aria-label"]);
        Assert.Equal ("notes.txt", Assert.Single (deeper, n => n.ParentId == sub_menu.Id).Text);
        Assert.Equal (sub_menu.ElementId, NodeOf (deeper, recent)["aria-controls"]);

        wrap.Checked = false;
        Assert.Equal ("false", NodeOf (BuildWithPopups (form), wrap)["aria-checked"]);

        // Closing the menu removes both popups with one removal: the submenu is inside the menu.
        file.HideDropDown ();
        var ops = AriaDom.Diff (deeper.ToDictionary (n => n.Id), BuildWithPopups (form));
        Assert.Equal (popup.Id, Assert.Single (ops, o => o.IsRemove).Id);

        form.Close ();
    }

    [Fact]
    public void A_highlighted_menu_item_is_the_active_descendant ()
    {
        using var form = MenuForm (out var file, out _, out _);
        file.ShowDropDown ();
        var open = file.DropDownItems[0];

        file.OpenDropDown!.SelectItemFromKeyboard (open);
        var nodes = BuildWithPopups (form);

        Assert.Same (open, AriaDom.ActiveNode (nodes)!.Source);

        form.Close ();
    }

    [Fact]
    public void A_context_menu_is_a_menu ()
    {
        using var form = new Form { Text = "Canvas", Width = 400, Height = 300 };
        var target = new Panel { Name = "canvas", Left = 0, Top = 0, Width = 300, Height = 200 };
        form.Controls.Add (target);
        form.Show ();

        var context = new ContextMenuStrip ();
        context.Items.Add (new ToolStripMenuItem ("Cu&t"));
        context.Items.Add (new ToolStripMenuItem ("&Paste") { Enabled = false });
        context.Show (target, new Point (20, 30));

        var nodes = BuildWithPopups (form);
        var popup = Assert.Single (nodes, n => n["data-mf-popup"] == "menu");
        var menu = Assert.Single (nodes, n => n.ParentId == popup.Id);
        Assert.Equal ("menu", menu.Role);
        Assert.Equal (new[] { "Cut", "Paste" }, nodes.Where (n => n.ParentId == menu.Id).Select (n => n.Text));
        Assert.Equal ("true", nodes.Single (n => n.Text == "Paste")["aria-disabled"]);

        context.Close ();
        Assert.DoesNotContain (BuildWithPopups (form), n => n["data-mf-popup"] is not null);

        form.Close ();
    }

    [Fact]
    public void A_tool_tip_is_a_tooltip_that_describes_its_control ()
    {
        using var form = new Form { Text = "Tips", Width = 400, Height = 300 };
        var save = new Button { Name = "save", Text = "Save", Left = 10, Top = 10 };
        form.Controls.Add (save);
        form.Show ();
        using var tip = new ToolTip { ShowAlways = true };

        tip.Show ("Saves the file", save);
        var nodes = BuildWithPopups (form);

        var tooltip = Assert.Single (nodes, n => n.Role == "tooltip");
        Assert.Equal ("Saves the file", tooltip.Text);
        Assert.Equal ("tooltip", tooltip["data-mf-popup"]);
        // Where the tip is drawn, the size ToolTip measured for its text.
        Assert.True (tooltip.Bounds.Width > 0 && tooltip.Bounds.Height > 0, tooltip.ToString ());
        // The tip's own label is not mirrored again inside it.
        Assert.DoesNotContain (nodes, n => n.ParentId == tooltip.Id);
        Assert.Equal (tooltip.ElementId, NodeOf (nodes, save)["aria-describedby"]);

        tip.Hide (save);
        var after = BuildWithPopups (form);
        Assert.DoesNotContain (after, n => n.Role == "tooltip");
        Assert.Null (NodeOf (after, save)["aria-describedby"]);

        form.Close ();
    }

    [Fact]
    public void Tool_strip_items_take_their_strips_meaning ()
    {
        using var form = new Form { Text = "Strips", Width = 500, Height = 300 };
        var tools = new ToolStrip ();
        var bold = new ToolStripButton ("Bold") { CheckOnClick = true, Checked = true };
        tools.Items.Add (bold);
        tools.Items.Add (new ToolStripLabel ("Zoom"));
        var status = new StatusStrip ();
        var ready = new ToolStripStatusLabel { Text = "Ready" };
        status.Items.Add (ready);
        form.Controls.Add (tools);
        form.Controls.Add (status);
        form.Show ();

        var nodes = BuildWithPopups (form);

        var bold_node = NodeOf (nodes, bold);
        Assert.Equal ("button", bold_node.Role);
        Assert.Equal ("true", bold_node["aria-pressed"]);
        Assert.Null (nodes.Single (n => n.Text == "Zoom").Role);

        var status_node = Assert.Single (nodes, n => n.Role == "status");
        // The live region speaks the status bar's changes; its own implicit politeness would repeat them.
        Assert.Equal ("off", status_node["aria-live"]);
        var ready_node = NodeOf (nodes, ready);
        Assert.Null (ready_node.Role);
        Assert.Equal ("Ready", ready_node.Text);
        Assert.Equal (AriaAnnounce.TextChange, ready_node.Announce);

        form.Close ();
    }
}
