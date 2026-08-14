using System.Linq;
using Majorsilence.Forms.Automation;
using Majorsilence.Forms.Headless;
using Xunit;

namespace Majorsilence.Forms.Tests
{
    /// <summary>
    /// A ListBox's items appear in the automation tree and can be driven through a session.
    /// </summary>
    /// <remarks>
    /// A list item is not a <see cref="Control"/> — the ListBox paints its Items itself — so a tree built
    /// only from the control hierarchy stopped at the list: a caller could find the list and read nothing
    /// in it, and apps had to mirror list contents into a label to make them assertable at all.
    /// </remarks>
    public class AutomationListItemTests
    {
        private static Form BuildForm (out ListBox list, int itemCount = 3)
        {
            var form = new Form { UseSystemDecorations = true, Width = 400, Height = 300 };

            list = new ListBox { Name = "wordList", Left = 10, Top = 10, Width = 200, Height = 120 };
            var names = new [] { "Alpha", "Beta", "Gamma" };

            for (var i = 0; i < itemCount; i++)
                list.Items.Add (i < names.Length ? names [i] : $"Item {i}");

            form.Controls.Add (list);
            return form;
        }

        private static AutomationElement TheList (Form form) =>
            Assert.Single (AutomationProvider.BuildTree (form).Self (), e => e.AutomationId == "wordList");

        [Fact]
        public void The_tree_contains_the_lists_items ()
        {
            using var form = BuildForm (out _);
            HeadlessRenderer.CapturePng (form, 400, 300);  // force a layout pass

            var items = TheList (form).Children;

            Assert.Equal (new [] { "Alpha", "Beta", "Gamma" }, items.Select (i => i.Name));
            Assert.All (items, item => {
                Assert.Equal ("listitem", item.Role);
                Assert.Equal ("ListBoxItem", item.ControlType);
                Assert.True (item.Enabled);
                Assert.True (item.Visible);
                // Items have no Name of their own, so they carry no id: a synthetic index-based one would
                // shift under the caller as the list scrolls or grows.
                Assert.Equal (string.Empty, item.AutomationId);
            });
        }

        [Fact]
        public void An_items_bounds_are_on_screen_stacked_and_inside_the_list ()
        {
            using var form = BuildForm (out var list);
            HeadlessRenderer.CapturePng (form, 400, 300);

            var element = TheList (form);
            var items = element.Children;

            Assert.All (items, item => {
                // Not the zero rectangle a caller would click blindly.
                Assert.True (item.Bounds.Width > 0, $"width was {item.Bounds.Width}");
                Assert.True (item.Bounds.Height > 0, $"height was {item.Bounds.Height}");

                // Logical units, like every other Bounds in the tree — so no taller than one logical row,
                // which is what catches a device-pixel rectangle leaking through on a scaled display.
                Assert.True (item.Bounds.Height <= list.ItemHeight,
                    $"height {item.Bounds.Height} exceeds the logical ItemHeight {list.ItemHeight}");

                // Offset by the list it sits in, not reported in the list's own coordinate space.
                Assert.True (item.Bounds.Left >= element.Bounds.Left, "item starts left of its list");
                Assert.True (item.Bounds.Top >= element.Bounds.Top, "item starts above its list");
            });

            Assert.True (items [1].Bounds.Top > items [0].Bounds.Top, "items should stack downwards");
        }

        [Fact]
        public void The_list_reports_its_selected_item_as_its_value ()
        {
            using var form = BuildForm (out var list);
            HeadlessRenderer.CapturePng (form, 400, 300);

            Assert.Equal (string.Empty, TheList (form).Value);   // nothing selected yet

            list.SelectedIndex = 1;

            Assert.Equal ("Beta", TheList (form).Value);
        }

        [Fact]
        public void An_items_text_is_the_item_not_a_state_flag ()
        {
            using var form = BuildForm (out var list);
            HeadlessRenderer.CapturePng (form, 400, 300);

            list.SelectedIndex = 1;

            // GetText reads Value before Name, so an item carrying any value of its own would answer this
            // with something other than the text on screen — which is what a caller is asking for.
            var session = new AutomationSession (form);
            var beta = session.Find (By.Name ("Beta"));

            Assert.NotNull (beta);
            Assert.Null (beta!.Value);
            Assert.Equal ("Beta", session.GetText (beta));
        }

        [Fact]
        public void Items_are_findable_by_role_name_and_type ()
        {
            using var form = BuildForm (out _);
            HeadlessRenderer.CapturePng (form, 400, 300);

            var session = new AutomationSession (form);

            Assert.Equal (3, session.FindAll (By.Role ("listitem")).Count);
            Assert.Equal ("Beta", session.Find (By.Name ("Beta"))?.Name);
            Assert.Equal ("listitem", session.Find (By.Type ("ListBoxItem"))?.Role);
        }

        [Fact]
        public void Clicking_an_item_through_the_session_selects_it ()
        {
            using var form = BuildForm (out var list);
            HeadlessRenderer.CapturePng (form, 400, 300);

            var session = new AutomationSession (form);
            var beta = session.Find (By.Name ("Beta"));

            Assert.NotNull (beta);
            session.Click (beta!);

            // The click goes through the real input pipeline at the item's own rectangle, so this is also
            // what proves those bounds are correct and in the units the pointer handlers expect.
            Assert.Equal (1, list.SelectedIndex);
            Assert.Equal ("Beta", list.SelectedItem);
        }

        [Fact]
        public void Items_scrolled_out_of_view_stay_out_of_the_tree ()
        {
            // More items than the list can show, so the tree has to follow the scroll position rather than
            // reporting rectangles for rows that are not on screen.
            using var form = BuildForm (out var list, itemCount: 40);
            HeadlessRenderer.CapturePng (form, 400, 300);

            var visible = TheList (form).Children;

            Assert.True (visible.Count < list.Items.Count,
                $"all {list.Items.Count} items were in the tree; only the visible ones belong there");
            Assert.Contains (visible, item => item.Name == "Alpha");

            list.TopIndex = 20;
            HeadlessRenderer.CapturePng (form, 400, 300);

            var scrolled = TheList (form).Children;

            Assert.DoesNotContain (scrolled, item => item.Name == "Alpha");
            Assert.Contains (scrolled, item => item.Name == "Item 20");
        }

        [Fact]
        public void An_empty_list_contributes_no_items ()
        {
            using var form = BuildForm (out _, itemCount: 0);
            HeadlessRenderer.CapturePng (form, 400, 300);

            Assert.Empty (TheList (form).Children);
        }
    }
}
