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
    
        [Fact]
        public void A_forms_controls_are_direct_children_of_the_window_in_the_tree ()
        {
            // The form's client area is an implementation detail of keeping the caption out of the
            // client region (FRM-06); a UI Automation client must not have to navigate through it, and
            // WinForms -- which has no such node -- does not make one. Regression for the Windows-only
            // UiaTreeTests.SiblingNavigation_RoundTrips, which asserts exactly this and cannot run here.
            Majorsilence.Forms.Headless.HeadlessRenderer.Use ();

            using var form = new Form { Size = new System.Drawing.Size (300, 200) };
            form.UseSystemDecorations = false;   // the shape CI tests
            var button = new Button { Name = "okButton", Text = "OK", Size = new System.Drawing.Size (80, 24) };
            form.Controls.Add (button);
            form.Show ();

            var root = AutomationProvider.BuildTree (form);

            Assert.Contains (root.Children, c => c.AutomationId == "okButton");
            Assert.DoesNotContain (root.Children, c => c.ControlType.Contains ("ClientArea"));

            form.Close ();
        }

        // ---- IAutomationStateProvider (register item F19): a custom-painted control publishing its own
        // value and extra state -- the issue's own example, "the level a status widget is showing",
        // standing in for alert-buddy's real beacon indicator. BeaconIndicator itself is declared below,
        // outside this class, so WebDriverServerTests can drive it through the real HTTP server too. ----

        [Fact]
        public void A_custom_control_can_publish_role_name_value_and_state ()
        {
            using var form = new Form { UseSystemDecorations = true };
            var beacon = new BeaconIndicator {
                Name = "workshopBeacon",
                AccessibleName = "Workshop beacon",
                AccessibleRole = AccessibleRole.StatusBar,
                Level = 3,
                Status = "alarm",
                Left = 10,
                Top = 10,
                Width = 60,
                Height = 60,
            };
            form.Controls.Add (beacon);
            HeadlessRenderer.CapturePng (form, 300, 200);

            var node = Assert.Single (AutomationProvider.BuildTree (form).Self (), e => e.AutomationId == "workshopBeacon");

            // Role and name: the existing AccessibleRole/AccessibleName properties already cover these for
            // any control, custom-painted or not -- IAutomationStateProvider adds nothing for either.
            Assert.Equal ("statusbar", node.Role);
            Assert.Equal ("Workshop beacon", node.Name);
            // Value and extra state: what IAutomationStateProvider actually adds.
            Assert.Equal ("3", node.Value);
            Assert.Equal ("3", node.State["level"]);
            Assert.Equal ("alarm", node.State["status"]);
        }

        [Fact]
        public void A_control_not_implementing_IAutomationStateProvider_has_empty_state ()
        {
            using var form = BuildForm (out _, out _);
            HeadlessRenderer.CapturePng (form, 300, 200);

            var btn = Assert.Single (AutomationProvider.BuildTree (form).Self (), e => e.AutomationId == "okButton");

            Assert.Empty (btn.State);
        }

        [Fact]
        public void The_automation_XML_shows_a_custom_controls_state ()
        {
            using var form = new Form { UseSystemDecorations = true };
            var beacon = new BeaconIndicator {
                Name = "workshopBeacon",
                Level = 2,
                Status = "warning",
                Left = 10,
                Top = 10,
                Width = 60,
                Height = 60,
            };
            form.Controls.Add (beacon);
            HeadlessRenderer.CapturePng (form, 300, 200);
            var session = new AutomationSession (form);

            var xml = session.GetPageSource ();

            Assert.Contains ("state-level=\"2\"", xml);
            Assert.Contains ("state-status=\"warning\"", xml);

            // Independently queryable by XPath, the same as every other attribute in the page source --
            // not just present as text somewhere in the document.
            var doc = System.Xml.Linq.XDocument.Parse (xml);
            Assert.NotEmpty (doc.XPathSelectElements ("//BeaconIndicator[@state-level='2']"));
        }

}

    // A minimal custom-painted control publishing its own value and extra state (register item F19) --
    // the issue's own example, "the level a status widget is showing", standing in for alert-buddy's real
    // beacon indicator. Nothing here needs to actually paint anything: BuildTree never renders, it only
    // reads the same logical state a renderer would. Shared (not nested) so WebDriverServerTests can drive
    // it through the real HTTP server too, not just AutomationTests' own in-process tree building.
    internal sealed class BeaconIndicator : Control, IAutomationStateProvider
    {
        public int Level { get; set; }
        public string Status { get; set; } = "warning";

        public string? AutomationValue => Level.ToString (System.Globalization.CultureInfo.InvariantCulture);

        public System.Collections.Generic.IReadOnlyDictionary<string, string> AutomationState => new System.Collections.Generic.Dictionary<string, string> {
            ["level"] = Level.ToString (System.Globalization.CultureInfo.InvariantCulture),
            ["status"] = Status,
        };
    }
}
