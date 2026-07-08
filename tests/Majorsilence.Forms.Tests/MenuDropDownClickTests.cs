using System.Drawing;
using Xunit;

namespace Majorsilence.Forms.Tests
{
    // Reproduces the real crash-adjacent bug: in a migrated app (TownSuite frmMainAR) the top-level
    // menus open, but clicking a leaf item in the dropdown never fires its Click handler, so no form
    // opens. These drive the click path directly (bypassing popup-window plumbing) to isolate whether
    // the OnClick -> item.Click chain is intact.
    public class MenuDropDownClickTests
    {
        // Exposes the protected OnClick so a test can drive it at a point, exactly as the window's
        // click routing would once the pointer release reaches the dropdown control.
        private sealed class TestableMenuDropDown : MenuDropDown
        {
            public TestableMenuDropDown (MenuItem root) : base (root) { }
            public void ClickAt (Point p) => OnClick (new MouseEventArgs (MouseButtons.Left, 1, p.X, p.Y, Point.Empty));
        }

        [Fact]
        public void Clicking_a_leaf_item_raises_its_Click ()
        {
            var root = new MenuItem ("File");
            var open = new MenuItem ("Open");
            root.Items.Add (open);
            open.SetBounds (0, 0, 100, 24);

            var raised = false;
            open.Click += (_, _) => raised = true;

            using var dropdown = new TestableMenuDropDown (root);
            dropdown.ClickAt (new Point (10, 10));

            Assert.True (raised, "Clicking a leaf dropdown item must raise its Click event.");
        }

        [Fact]
        public void Clicking_a_ToolStripMenuItem_leaf_raises_its_Click ()
        {
            // The exact type the migrated designer code uses (Compat.MenuItem : ToolStripMenuItem).
            var root = new ToolStripMenuItem ("Customers");
            var records = new ToolStripMenuItem ("Records");
            root.DropDownItems.Add (records);
            records.SetBounds (0, 0, 200, 24);

            var raised = false;
            records.Click += (_, _) => raised = true;

            using var dropdown = new TestableMenuDropDown (root);
            dropdown.ClickAt (new Point (10, 10));

            Assert.True (raised, "Clicking a ToolStripMenuItem leaf must raise its Click event.");
        }
    }
}
