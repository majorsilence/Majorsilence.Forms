using System.Linq;
using Majorsilence.Forms.Automation;
using Majorsilence.Forms.Headless;
using Xunit;

namespace Majorsilence.Forms.Tests
{
    /// <summary>
    /// Menu and toolbar items appear in the automation tree and can be driven through a session.
    /// </summary>
    /// <remarks>
    /// A <see cref="MenuItem"/> is not a <see cref="Control"/>, so a tree built only from the control
    /// hierarchy stopped at the strip: an automated test could see a ToolStrip and click nothing on it.
    /// Driving a docking sample this way found a MenuStrip and a ToolStrip and no way to open a document.
    /// </remarks>
    public class AutomationStripItemTests
    {
        private static Form BuildForm (out ToolStripButton button, out ToolStripMenuItem file, out ToolStripMenuItem open)
        {
            var form = new Form { UseSystemDecorations = true, Width = 400, Height = 300 };

            var menu = new MenuStrip { Name = "mainMenu" };
            file = new ToolStripMenuItem { Name = "fileMenu", Text = "&File" };
            open = new ToolStripMenuItem { Name = "openItem", Text = "&Open" };
            file.Items.Add (open);
            menu.Items.Add (file);

            var strip = new ToolStrip { Name = "mainToolBar" };
            button = new ToolStripButton { Name = "newButton", Text = "New" };
            strip.Items.Add (button);

            form.Controls.Add (menu);
            form.Controls.Add (strip);
            return form;
        }

        [Fact]
        public void The_tree_contains_the_strips_items ()
        {
            using var form = BuildForm (out _, out _, out _);
            HeadlessRenderer.CapturePng (form, 400, 300);  // force a layout pass

            var all = AutomationProvider.BuildTree (form).Self ().ToList ();

            var button = Assert.Single (all, e => e.AutomationId == "newButton");
            Assert.Equal ("menuitem", button.Role);
            Assert.Equal ("ToolStripButton", button.ControlType);
            Assert.Equal ("New", button.Name);
            Assert.True (button.Enabled);

            var file = Assert.Single (all, e => e.AutomationId == "fileMenu");
            Assert.Equal ("File", file.Name);           // the mnemonic marker is not part of the name
        }

        [Fact]
        public void Submenu_items_are_nested_under_their_parent ()
        {
            using var form = BuildForm (out _, out _, out _);
            HeadlessRenderer.CapturePng (form, 400, 300);

            var file = Assert.Single (AutomationProvider.BuildTree (form).Self (), e => e.AutomationId == "fileMenu");

            Assert.Single (file.Children, c => c.AutomationId == "openItem");
        }

        [Fact]
        public void An_items_bounds_are_on_screen_and_its_own_size ()
        {
            using var form = BuildForm (out var button, out _, out _);
            HeadlessRenderer.CapturePng (form, 400, 300);

            var element = Assert.Single (AutomationProvider.BuildTree (form).Self (), e => e.AutomationId == "newButton");

            // Not the zero rectangle a caller would click blindly, and offset by the strip it sits in.
            Assert.True (element.Bounds.Width > 0, $"width was {element.Bounds.Width}");
            Assert.True (element.Bounds.Height > 0, $"height was {element.Bounds.Height}");
            Assert.Equal (button.Bounds.Width, element.Bounds.Width);
            Assert.True (element.Bounds.Top >= 0, $"top was {element.Bounds.Top}");
        }

        [Fact]
        public void Clicking_an_item_through_the_session_raises_its_Click ()
        {
            using var form = BuildForm (out var button, out _, out _);
            HeadlessRenderer.CapturePng (form, 400, 300);

            var clicked = 0;
            button.Click += (_, _) => clicked++;

            var session = new AutomationSession (form);
            var element = session.Find (By.Id ("newButton"));

            Assert.NotNull (element);
            session.Click (element!);

            Assert.Equal (1, clicked);
        }

        [Fact]
        public void Items_are_findable_by_role_name_and_type ()
        {
            using var form = BuildForm (out _, out _, out _);
            HeadlessRenderer.CapturePng (form, 400, 300);

            var session = new AutomationSession (form);

            Assert.NotEmpty (session.FindAll (By.Role ("menuitem")));
            Assert.Equal ("newButton", session.Find (By.Type ("ToolStripButton"))?.AutomationId);
            Assert.Equal ("newButton", session.Find (By.Name ("New"))?.AutomationId);
        }

        [Fact]
        public void An_invisible_item_stays_out_of_the_tree ()
        {
            using var form = BuildForm (out var button, out _, out _);
            button.Visible = false;
            HeadlessRenderer.CapturePng (form, 400, 300);

            Assert.DoesNotContain (AutomationProvider.BuildTree (form).Self (), e => e.AutomationId == "newButton");
        }
    }
}
