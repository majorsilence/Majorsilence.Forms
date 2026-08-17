using System.Drawing;
using Majorsilence.Forms.Headless;
using Xunit;

namespace Majorsilence.Forms.Tests
{
    /// <summary>
    /// Maps points to rows the way a click does.
    /// </summary>
    /// <remarks>
    /// <see cref="ListBox.GetIndexAtLocation"/> takes the logical coordinates a
    /// <see cref="MouseEventArgs"/> carries, but tests them against <see cref="ListBox.GetItemRectangle"/>,
    /// which is built from <c>ClientRectangle</c> and <c>ScaledItemHeight</c> and is therefore in device
    /// pixels. Comparing the two without converting picks the row at index/scale, so at scaling 2 clicking
    /// the second row selected the first — for a user and for automation alike. These assertions are written
    /// in logical units so they hold at any scaling, which is what makes them catch it.
    /// </remarks>
    public class ListBoxHitTestTests
    {
        private static Form BuildForm (out ListBox list, int itemCount = 5)
        {
            var form = new Form { UseSystemDecorations = true, Width = 300, Height = 240 };

            list = new ListBox { Name = "list", Left = 0, Top = 0, Width = 200, Height = 120 };
            for (var i = 0; i < itemCount; i++)
                list.Items.Add ($"Item {i}");

            form.Controls.Add (list);
            HeadlessRenderer.CapturePng (form, 300, 240);   // force a layout pass
            return form;
        }

        // The logical centre of a row, in the units a mouse event arrives in.
        private static Point RowCentre (ListBox list, int index) =>
            new (5, list.DeviceToLogicalUnits (list.ClientRectangle.Top)
                    + (index * list.ItemHeight) + (list.ItemHeight / 2));

        [Fact]
        public void A_point_inside_a_row_returns_that_row ()
        {
            using var form = BuildForm (out var list);

            for (var index = 0; index < 3; index++)
                Assert.Equal (index, list.GetIndexAtLocation (RowCentre (list, index)));
        }

        // Clicking through the real input pipeline is covered by AutomationListItemTests, which drives a
        // session at an item's own bounds and asserts the selection that results.

        [Fact]
        public void A_point_below_the_last_row_belongs_to_no_row ()
        {
            using var form = BuildForm (out var list, itemCount: 2);

            Assert.Equal (-1, list.GetIndexAtLocation (RowCentre (list, 4)));
        }
    }
}
