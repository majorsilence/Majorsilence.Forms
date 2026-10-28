using Majorsilence.Forms.Headless;
using Majorsilence.Forms.Telerik;
using Xunit;

namespace Majorsilence.Forms.Tests;

// RadToggleSwitch.ReadOnly and ToggleStateMode.None, RadPageViewPage.ImageAlignment and
// RadElement.AccessibleRole -- designer members Telerik code uses that the compat layer did not have.
// Behaviour as measured on UI.for.WinForms 2026.2.520.
[Collection ("Headless")]
public class TelerikToggleSwitchPageViewElementMembersTests
{
    private static RadToggleSwitch Switch (out Form form)
    {
        HeadlessRenderer.Use ();
        form = new Form { Width = 300, Height = 200 };
        var toggle = new RadToggleSwitch { Width = 90, Height = 30 };
        form.Controls.Add (toggle);
        form.Show ();
        return toggle;
    }

    private static void Click (RadToggleSwitch toggle)
    {
        toggle.RaiseMouseDown (new MouseEventArgs (MouseButtons.Left, 1, 10, 10, 0));
        toggle.RaiseMouseUp (new MouseEventArgs (MouseButtons.Left, 1, 10, 10, 0));
        toggle.RaiseClick (new MouseEventArgs (MouseButtons.Left, 1, 10, 10, 0));
    }

    private static void Space (RadToggleSwitch toggle)
        => toggle.RaiseKeyUp (new KeyEventArgs (Keys.Space));

    [Fact]
    public void A_read_only_switch_ignores_the_mouse_and_keyboard_but_not_code ()
    {
        var toggle = Switch (out var form);

        using (form) {
            Assert.False (toggle.ReadOnly);

            var clicks = 0;
            toggle.Click += (_, _) => clicks++;
            toggle.ReadOnly = true;

            Click (toggle);
            Assert.False (toggle.Value);
            Space (toggle);
            Assert.False (toggle.Value);
            Assert.Equal (2, clicks);  // the clicks still happen; only their toggle is skipped

            toggle.Value = true;
            Assert.True (toggle.Value);

            // Press mode reads it too.
            toggle.ToggleStateMode = ToggleStateMode.Press;
            Click (toggle);
            Assert.True (toggle.Value);

            toggle.ReadOnly = false;
            Click (toggle);
            Assert.False (toggle.Value);
        }
    }

    [Fact]
    public void ToggleStateMode_None_leaves_toggling_to_code ()
    {
        var toggle = Switch (out var form);

        using (form) {
            toggle.ToggleStateMode = ToggleStateMode.None;

            Click (toggle);
            Assert.False (toggle.Value);
            Space (toggle);
            Assert.False (toggle.Value);

            toggle.Value = true;
            Assert.True (toggle.Value);

            toggle.ToggleStateMode = ToggleStateMode.Click;
            Click (toggle);
            Assert.False (toggle.Value);
        }
    }

    [Fact]
    public void ToggleStateMode_has_Teleriks_values_for_None_and_Click ()
    {
        Assert.Equal (0, (int) ToggleStateMode.None);
        Assert.Equal (1, (int) ToggleStateMode.Click);
    }

    [Fact]
    public void A_pages_ImageAlignment_is_its_tab_items ()
    {
        var page = new RadPageViewPage { Text = "Fuel" };

        Assert.Equal (ContentAlignment.MiddleLeft, page.ImageAlignment);
        Assert.Same (page, page.Item.Page);

        page.ImageAlignment = ContentAlignment.MiddleCenter;
        Assert.Equal (ContentAlignment.MiddleCenter, page.Item.ImageAlignment);

        page.Item.ImageAlignment = ContentAlignment.TopLeft;
        Assert.Equal (ContentAlignment.TopLeft, page.ImageAlignment);

        // Set before the page joins a view, it is still the item's once it has.
        using var view = new RadPageView ();
        var early = new RadPageViewPage { Text = "x", ImageAlignment = ContentAlignment.BottomRight };
        view.Pages.Add (early);
        Assert.Equal (ContentAlignment.BottomRight, early.Item.ImageAlignment);
    }

    [Fact]
    public void An_elements_AccessibleRole_is_its_own ()
    {
        using var grid = new RadGridView ();

        Assert.Equal (AccessibleRole.StaticText, grid.RootElement.AccessibleRole);
        Assert.Equal (AccessibleRole.StaticText, new RadElement ().AccessibleRole);

        grid.RootElement.AccessibleRole = AccessibleRole.Grouping;

        Assert.Equal (AccessibleRole.Grouping, grid.RootElement.AccessibleRole);
        Assert.Equal (AccessibleRole.Default, grid.AccessibleRole);
    }
}
