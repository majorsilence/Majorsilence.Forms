using System.Linq;
using System.Xml.XPath;
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
        public void Find_ByXPath_LocatesElements ()
        {
            using var form = BuildForm (out _, out _);
            HeadlessRenderer.CapturePng (form, 300, 200);
            var session = new AutomationSession (form);

            // By tag (control type) + attribute predicate.
            var btn = session.Find (By.XPath ("//Button[@id='okButton']"));
            Assert.NotNull (btn);
            Assert.Equal ("okButton", btn!.AutomationId);

            // Attribute-only and descendant forms.
            Assert.NotNull (session.Find (By.XPath ("//*[@name='OK']")));
            Assert.NotNull (session.Find (By.XPath ("//TextBox")));

            // No match returns null; multiple matches come back in document order.
            Assert.Null (session.Find (By.XPath ("//Button[@id='missing']")));
            Assert.Equal (2, session.FindAll (By.XPath ("//Button | //TextBox")).Count);
        }

        [Fact]
        public void GetPageSource_RendersTreeAsXml ()
        {
            using var form = BuildForm (out _, out _);
            HeadlessRenderer.CapturePng (form, 300, 200);
            var session = new AutomationSession (form);

            var xml = session.GetPageSource ();

            Assert.Contains ("<Button", xml);
            Assert.Contains ("id=\"okButton\"", xml);
            Assert.Contains ("<TextBox", xml);
            // The source is well-formed and queryable by the same XPath used to find elements.
            var doc = System.Xml.Linq.XDocument.Parse (xml);
            Assert.NotEmpty (doc.XPathSelectElements ("//Button[@id='okButton']"));
        }

        [Fact]
        public void Focused_ReflectsKeyboardFocus ()
        {
            using var form = BuildForm (out _, out var textbox);
            HeadlessRenderer.CapturePng (form, 300, 200);

            // No control focused yet.
            Assert.DoesNotContain (AutomationProvider.BuildTree (form).Self (), e => e.Focused);

            textbox.Select ();

            var focused = AutomationProvider.BuildTree (form).Self ().Where (e => e.Focused).ToList ();
            var one = Assert.Single (focused);
            Assert.Equal ("nameBox", one.AutomationId);
        }

        [Fact]
        public void Observer_RaisesFocusChanged ()
        {
            using var form = BuildForm (out var button, out var textbox);
            HeadlessRenderer.CapturePng (form, 300, 200);

            using var observer = new AutomationObserver (form);
            AutomationElement? lastFocus = null;
            observer.FocusChanged += (_, el) => lastFocus = el;

            textbox.Select ();
            Assert.Equal ("nameBox", lastFocus?.AutomationId);

            button.Select ();
            Assert.Equal ("okButton", lastFocus?.AutomationId);
        }

        [Fact]
        public void Observer_RaisesValueChanged_ForFocusedControl ()
        {
            using var form = BuildForm (out _, out _);
            var check = new CheckBox { Name = "agree", Text = "Agree", Left = 10, Top = 90, Width = 120, Height = 24 };
            form.Controls.Add (check);
            HeadlessRenderer.CapturePng (form, 300, 200);

            using var observer = new AutomationObserver (form);
            AutomationElement? changed = null;
            observer.ValueChanged += (_, el) => changed = el;

            check.Select ();          // value tracking follows focus
            check.Checked = true;

            Assert.Equal ("agree", changed?.AutomationId);
            Assert.Equal ("true", changed?.Value);
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
