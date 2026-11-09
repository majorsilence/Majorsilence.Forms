using System.Drawing;
using Xunit;

namespace Majorsilence.Forms.Tests
{
    // A column given a fresh DataGridViewCellStyle that only sets alignment or a format -- the common shape in
    // migrated code -- leaves BackColor empty, to be inherited. The editing text box took that empty colour and
    // forced it opaque, which is black: typing into the cell showed a black box.
    [Collection ("Headless")]
    public class DataGridViewEditingControlColorTests
    {
        private static DataGridView Grid ()
        {
            var grid = new DataGridView { Width = 400, Height = 300 };
            grid.Columns.Add ("Payment", 100);
            grid.Columns["Payment"]!.DefaultCellStyle = new DataGridViewCellStyle { Alignment = DataGridViewContentAlignment.MiddleRight };
            grid.Rows.Add ("5.07");
            return grid;
        }

        [Fact]
        public void An_unset_back_colour_does_not_make_the_editor_black ()
        {
            using var grid = Grid ();
            grid.BeginEdit (0, 0);

            var editor = Assert.IsAssignableFrom<Control> (grid.EditingControl);
            Assert.NotEqual (Color.FromArgb (255, 0, 0, 0).ToArgb (), editor.BackColor.ToArgb ());
            Assert.Equal (255, editor.BackColor.A);
        }

        [Fact]
        public void A_back_colour_the_cell_sets_is_still_applied ()
        {
            using var grid = Grid ();
            grid.Columns["Payment"]!.DefaultCellStyle.BackColor = Color.LightYellow;
            grid.BeginEdit (0, 0);

            var editor = Assert.IsAssignableFrom<Control> (grid.EditingControl);
            Assert.Equal (Color.LightYellow.ToArgb (), editor.BackColor.ToArgb ());
        }
    }
}
