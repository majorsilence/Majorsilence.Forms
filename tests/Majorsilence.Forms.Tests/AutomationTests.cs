using System.Linq;
using Majorsilence.Forms.Automation;
using Majorsilence.Forms.Headless;
using Xunit;

namespace Majorsilence.Forms.Tests
{
    // Exercises the in-process automation surface (the foundation for the WebDriver/Selenium server and
    // for screen-reader bridging): build the element tree, locate by id/name/role, and drive controls
    // through the same neutral input pipeline a real backend uses.
    public class AutomationTests
    {
        private static Form BuildForm (out Button button, out TextBox textbox)
        {
            var form = new Form { UseSystemDecorations = true };
            button = new Button { Name = "okButton", Text = "OK", Left = 10, Top = 10, Width = 100, Height = 30 };
            textbox = new TextBox { Name = "nameBox", Left = 10, Top = 50, Width = 200, Height = 30 };
            form.Controls.Add (button);
            form.Controls.Add (textbox);
            return form;
        }

        [Fact]
        public void BuildTree_ExposesControlsWithRolesAndIds ()
        {
            using var form = BuildForm (out _, out _);
            HeadlessRenderer.CapturePng (form, 300, 200);  // force a layout pass

            var root = AutomationProvider.BuildTree (form);

            Assert.Equal ("window", root.Role);
            var all = root.Self ().ToList ();

            var btn = Assert.Single (all, e => e.AutomationId == "okButton");
            Assert.Equal ("button", btn.Role);
            Assert.Equal ("OK", btn.Name);
            Assert.Equal ("Button", btn.ControlType);

            var tb = Assert.Single (all, e => e.AutomationId == "nameBox");
            Assert.Equal ("textbox", tb.Role);
        }

        [Fact]
        public void Find_ByIdNameRole_LocatesElement ()
        {
            using var form = BuildForm (out _, out _);
            HeadlessRenderer.CapturePng (form, 300, 200);
            var session = new AutomationSession (form);

            Assert.NotNull (session.Find (By.Id ("okButton")));
            Assert.NotNull (session.Find (By.Name ("OK")));
            Assert.NotNull (session.Find (By.Role ("textbox")));
            Assert.Null (session.Find (By.Id ("missing")));
        }

        [Fact]
        public void Click_RoutesToControl ()
        {
            using var form = BuildForm (out var button, out _);
            var clicks = 0;
            button.Click += (_, _) => clicks++;

            HeadlessRenderer.CapturePng (form, 300, 200);
            var session = new AutomationSession (form);

            session.Click (session.FindOrThrow (By.Id ("okButton")));

            Assert.Equal (1, clicks);
        }

        [Fact]
        public void SendKeys_TypesIntoTextBox ()
        {
            using var form = BuildForm (out _, out var textbox);
            HeadlessRenderer.CapturePng (form, 300, 200);
            var session = new AutomationSession (form);

            session.SendKeys (session.FindOrThrow (By.Id ("nameBox")), "Hello");

            Assert.Equal ("Hello", textbox.Text);
            // The value is reflected back through the automation snapshot.
            Assert.Equal ("Hello", session.GetText (session.FindOrThrow (By.Id ("nameBox"))));
        }
    }
}
