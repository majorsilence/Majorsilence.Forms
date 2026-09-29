using System.ComponentModel;
using System.Drawing;
using Majorsilence.Forms.Headless;
using Majorsilence.Forms.Telerik;
using SkiaSharp;
using Xunit;

namespace Majorsilence.Forms.Tests;

// W6 mechanisms, twentieth chunk (#176): RadPropertyGrid's item model and its seven events, which were
// declared `add { } remove { }` and dropped every handler, and RadPropertyStore as a selected object.
[Collection ("Headless")]
public class W6TelerikPropertyGridTests
{
    private sealed class Sample
    {
        [Category ("Appearance"), Description ("The caption.")]
        public string Title { get; set; } = "Untitled";

        [Category ("Behaviour")]
        public int Retries { get; set; } = 3;

        [Category ("Behaviour"), ReadOnly (true)]
        public string Identifier { get; set; } = "fixed";
    }

    private static RadPropertyGrid Grid (out Form form, object selected)
    {
        HeadlessRenderer.Use ();
        form = new Form { Size = new Size (460, 420) };
        var grid = new RadPropertyGrid { Bounds = new Rectangle (0, 0, 380, 360) };
        form.Controls.Add (grid);
        form.Show ();
        grid.SelectedObject = selected;
        return grid;
    }

    private static int RowOf (PropertyGrid grid, string name)
    {
        for (var i = 0; i < grid.VisibleRows.Count; i++)
            if (grid.VisibleRows[i].Name == name)
                return i;

        return -1;
    }

    private static void Click (PropertyGrid grid, string name, MouseButtons button = MouseButtons.Left, bool value = false)
    {
        // RowBounds is device, the mouse logical (RC-8).
        var row = grid.DeviceToLogicalUnits (grid.RowBounds (RowOf (grid, name)));
        var x = value ? row.Left + (row.Width * 3 / 4) : row.Left + 10;
        grid.RaiseMouseDown (new MouseEventArgs (button, 1, x, row.Top + (row.Height / 2), 0));
    }

    // ── the item model ──────────────────────────────────────────────────────────────────────────────

    [Fact]
    public void Items_and_Groups_are_the_properties_of_the_selected_object ()
    {
        var sample = new Sample ();
        var grid = Grid (out var form, sample);

        using (form) {
            Assert.Equal (new[] { "Title", "Identifier", "Retries" }, grid.Items.Select (i => i.Name));
            Assert.Equal (new[] { "Appearance", "Behaviour" }, grid.Groups.Select (g => g.Name));
            Assert.Equal (2, grid.Groups["Behaviour"].GridItems.Count);

            var retries = grid.Items["Retries"]!;
            Assert.Equal (3, retries.Value);
            Assert.Equal (typeof (int), retries.PropertyType);
            Assert.Equal ("Behaviour", retries.Category);
            Assert.Equal ("3", retries.FormattedValue);
            Assert.Equal ("The caption.", grid.Items["Title"]!.Description);
            Assert.True (grid.Items["Identifier"]!.ReadOnly);

            // Writing the item writes the object, and the item's original value stays what it was.
            var changed = new List<string> ();
            grid.ItemValueChanged += (_, e) => changed.Add (e.Item!.Name);

            retries.Value = 7;

            Assert.Equal (7, sample.Retries);
            Assert.Equal (7, retries.Value);
            Assert.Equal (3, retries.OriginalValue);
            Assert.Equal (new[] { "Retries" }, changed);

            // A new object is a new set of items.
            grid.SelectedObject = new Sample { Title = "Second" };
            Assert.Equal ("Second", grid.Items["Title"]!.Value);
        }
    }

    [Fact]
    public void Visible_hides_a_row_ReadOnly_locks_it_and_SelectedGridItem_is_the_core_selection ()
    {
        var grid = Grid (out var form, new Sample ());

        using (form) {
            var before = grid.VisibleRows.Count;

            grid.Items["Retries"]!.Visible = false;
            Assert.Equal (before - 1, grid.VisibleRows.Count);
            Assert.Equal (-1, RowOf (grid, "Retries"));

            grid.Items["Retries"]!.Visible = true;
            Assert.Equal (before, grid.VisibleRows.Count);

            // An item marked read-only, and then the whole grid, refuse the editor.
            grid.Items["Title"]!.ReadOnly = true;
            Assert.False (grid.IsEditable (RowOf (grid, "Title")));

            grid.Items["Title"]!.ReadOnly = false;
            Assert.True (grid.IsEditable (RowOf (grid, "Title")));

            grid.ReadOnly = true;
            Assert.False (grid.IsEditable (RowOf (grid, "Title")));
            grid.ReadOnly = false;

            // The row the user clicked is the Telerik item, and assigning an item selects its row.
            Click (grid, "Retries");
            Assert.Same (grid.Items["Retries"], grid.SelectedGridItem);

            grid.SelectedGridItem = grid.Items["Title"];
            Assert.Same (grid.VisibleRows[RowOf (grid, "Title")], ((PropertyGrid) grid).SelectedGridItem);
        }
    }

    // ── the events ──────────────────────────────────────────────────────────────────────────────────

    [Fact]
    public void ItemFormatting_relabels_and_colours_the_row_it_is_raised_for ()
    {
        var grid = Grid (out var form, new Sample ());

        using (form) {
            var formatted = new List<string> ();

            grid.ItemFormatting += (_, e) => {
                formatted.Add (e.Item!.Name);

                if (e.Item.Name == "Retries") {
                    e.VisualElement.TextElement.Text = "Attempts";
                    e.VisualElement.BackColor = Color.Red;
                }
            };

            using var bitmap = PaintSurface.Render (grid);

            Assert.Contains ("Title", formatted);
            Assert.Contains ("Retries", formatted);

            // The row's background is the handler's colour, sampled at the right-hand end of the value
            // cell where no text reaches.
            var row = grid.RowBounds (RowOf (grid, "Retries"));
            Assert.Equal (SKColors.Red, bitmap.GetPixel (row.Right - 3, row.Top + (row.Height / 2)));

            var title = grid.RowBounds (RowOf (grid, "Title"));
            Assert.NotEqual (SKColors.Red, bitmap.GetPixel (title.Right - 3, title.Top + (title.Height / 2)));

            // The label the handler gave is the one drawn.
            Assert.Equal ("Attempts", grid.FormatRow (grid.VisibleRows[RowOf (grid, "Retries")], "Retries", "3").Label);
        }
    }

    [Fact]
    public void An_edit_raises_EditorInitialized_ItemValueChanged_and_Edited_in_order ()
    {
        var sample = new Sample ();
        var grid = Grid (out var form, sample);

        using (form) {
            var events = new List<string> ();
            grid.EditorInitialized += (_, e) => events.Add ("initialized:" + e.Item!.Name + ":" + (e.Editor is TextBox));
            grid.ItemValueChanged += (_, e) => events.Add ("changed:" + e.Item!.Name);
            grid.Edited += (_, e) => events.Add ("edited:" + e.Item!.Name);

            Click (grid, "Title", value: true);
            var editor = Assert.IsType<TextBox> (grid.EditingControl);
            editor.Text = "Renamed";
            grid.EndEdit (commit: true);

            Assert.Equal ("Renamed", sample.Title);
            Assert.Equal (new[] { "initialized:Title:True", "changed:Title", "edited:Title" }, events);

            // An edit abandoned changes nothing, and still closes with Edited.
            events.Clear ();
            Click (grid, "Title", value: true);
            grid.EndEdit (commit: false);
            Assert.Equal (new[] { "initialized:Title:True", "edited:Title" }, events);
        }
    }

    [Fact]
    public void EditorRequired_lets_a_handler_supply_the_editor ()
    {
        var sample = new Sample ();
        var grid = Grid (out var form, sample);

        using (form) {
            grid.EditorRequired += (_, e) => {
                if (e.Item!.Name == "Retries")
                    e.Editor = new NumericUpDown { Minimum = 0, Maximum = 10 };
                else if (e.Item.Name == "Title")
                    e.EditorType = typeof (MaskedTextBox);
            };

            Click (grid, "Retries", value: true);
            var number = Assert.IsType<NumericUpDown> (grid.EditingControl);
            Assert.Equal (3m, number.Value);   // seeded with the item's value

            number.Value = 9;
            grid.EndEdit (commit: true);
            Assert.Equal (9, sample.Retries);

            Click (grid, "Title", value: true);
            var masked = Assert.IsType<MaskedTextBox> (grid.EditingControl);
            Assert.Equal ("Untitled", masked.Text);
            grid.EndEdit (commit: false);
        }
    }

    [Fact]
    public void A_click_raises_ItemMouseClick_and_a_right_click_ContextMenuOpening_on_the_clicked_row ()
    {
        var grid = Grid (out var form, new Sample ());

        using (form) {
            var clicked = new List<string?> ();
            var menus = new List<string?> ();
            grid.ItemMouseClick += (s, _) => clicked.Add ((((RadPropertyGrid) s!).SelectedGridItem as PropertyGridItem)?.Name);
            grid.ContextMenuOpening += (s, _) => menus.Add ((((RadPropertyGrid) s!).SelectedGridItem as PropertyGridItem)?.Name);

            Click (grid, "Title");
            Click (grid, "Retries", MouseButtons.Right);

            Assert.Equal (new[] { "Title" }, clicked);
            Assert.Equal (new[] { "Retries" }, menus);

            // A category header is not an item.
            Click (grid, "Behaviour");
            Assert.Single (clicked);
        }
    }

    // ── RadPropertyStore ────────────────────────────────────────────────────────────────────────────

    [Fact]
    public void A_property_store_is_shown_and_edited_as_its_items ()
    {
        var store = new RadPropertyStore ();
        store.Add (new PropertyStoreItem (typeof (int), "Port", 8080, "The port to listen on.", "Network") { Label = "Listen port" });
        store.Add (new PropertyStoreItem (typeof (string), "Host", "localhost", "The host name.", "Network", isReadOnly: true));
        store.Add (new PropertyStoreItem (typeof (bool), "Verbose", false, true));

        var grid = Grid (out var form, store);

        using (form) {
            Assert.Equal (new[] { "Host", "Port", "Verbose" }, grid.Items.Select (i => i.Name).OrderBy (n => n, StringComparer.Ordinal));
            Assert.Equal ("Listen port", grid.Items["Port"]!.Label);
            Assert.Equal ("Network", grid.Items["Port"]!.Category);
            Assert.Equal ("The port to listen on.", grid.Items["Port"]!.Description);
            Assert.False (grid.IsEditable (RowOf (grid, "Host")));

            Click (grid, "Port", value: true);
            var editor = Assert.IsType<TextBox> (grid.EditingControl);
            editor.Text = "9090";
            grid.EndEdit (commit: true);

            Assert.Equal (9090, store["Port"]!.Value);

            // The default value is what a reset goes back to.
            var descriptor = TypeDescriptor.GetProperties (store)["Verbose"]!;
            store["Verbose"]!.Value = false;
            Assert.True (descriptor.CanResetValue (store));
            descriptor.ResetValue (store);
            Assert.Equal (true, store["Verbose"]!.Value);
        }
    }
}
