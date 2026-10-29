using Majorsilence.Forms.Telerik;
using Xunit;

namespace Majorsilence.Forms.Tests;

// What Telerik's designer writes for a RadContextMenu: a RadContextMenuManager component, the menu's items
// listed as a RadItem array, and each RadMenuItem's Name and UseCompatibleTextRendering.
public class TelerikMenuItemMembersTests
{
    [Fact]
    public void Menu_items_listed_as_RadItems_are_the_menus_items ()
    {
        var menu = new RadContextMenu ();
        var add = new RadMenuItem ("New Asset");
        var edit = new RadMenuItem ("Edit Asset");

        // The designer's cMnu.Items.AddRange (New RadItem() {add, edit}).
        menu.Items.AddRange (new RadItem[] { add, edit });

        Assert.Equal (new object[] { add, edit }, menu.Items);

        RadItem single = new RadMenuItem ("Delete");
        menu.Items.Add (single);
        Assert.IsType<RadMenuItem> (menu.Items[2]);
        Assert.Equal ("Delete", ((RadMenuItem) menu.Items[2]).Text);

        menu.Items.Insert (0, (RadItem) new RadMenuItem ("First"));
        Assert.Equal ("First", ((RadMenuItem) menu.Items[0]).Text);

        // Anything else is stored as it is.
        var label = new RadLabelElement ();
        menu.Items.Add (label);
        Assert.Same (label, menu.Items[4]);
    }

    [Fact]
    public void A_menu_item_keeps_its_designer_name ()
    {
        var item = new RadMenuItem { Name = "rdmnuItemAssetNew", UseCompatibleTextRendering = false };

        Assert.Equal ("rdmnuItemAssetNew", item.Name);
        Assert.False (item.UseCompatibleTextRendering);
        Assert.True (new RadMenuItem ().UseCompatibleTextRendering);
    }

    [Fact]
    public void The_designer_can_create_a_context_menu_manager ()
    {
        using var manager = new RadContextMenuManager ();
        using var control = new Control ();
        var menu = new RadContextMenu ();

        RadContextMenuManager.SetRadContextMenu (control, menu);
        Assert.Same (menu, RadContextMenuManager.GetRadContextMenu (control));

        RadContextMenuManager.SetRadContextMenu (control, null);
        Assert.Null (RadContextMenuManager.GetRadContextMenu (control));
    }
}
