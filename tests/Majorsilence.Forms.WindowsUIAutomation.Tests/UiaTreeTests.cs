using System.Windows.Automation;
using Majorsilence.Forms.Automation;
using Majorsilence.Forms.Headless;
using Xunit;

namespace Majorsilence.Forms.WindowsUIAutomation.Tests
{
    // Windows-only: exercises the bridge's pure tree-navigation and role-mapping logic (UiaTree / UiaMappings)
    // against a headless window. The COM provider round-trip (WM_GETOBJECT → a real screen reader) is a
    // separate manual desktop check, documented in docs/automation.md.
    public class UiaTreeTests
    {
        private static Form BuildForm (out Button button, out TextBox textbox)
        {
            Majorsilence.Forms.Backends.Platform.Backend = new Majorsilence.Forms.Headless.HeadlessPlatformBackend ();
            var form = new Form { UseSystemDecorations = true };
            button = new Button { Name = "okButton", Text = "OK", Left = 10, Top = 10, Width = 100, Height = 30 };
            textbox = new TextBox { Name = "nameBox", Left = 10, Top = 50, Width = 200, Height = 30 };
            form.Controls.Add (button);
            form.Controls.Add (textbox);
            HeadlessRenderer.CapturePng (form, 300, 200);   // force a layout pass
            return form;
        }

        // Finds the child-index path of the first element with the given automation id.
        private static int[] PathOf (Majorsilence.Forms.Automation.AutomationElement root, string automationId)
        {
            var path = new List<int> ();
            Assert.True (Walk (root, automationId, path), $"no element with id '{automationId}'");
            return path.ToArray ();
        }

        private static bool Walk (Majorsilence.Forms.Automation.AutomationElement node, string id, List<int> path)
        {
            for (var i = 0; i < node.Children.Count; i++) {
                path.Add (i);
                if (node.Children[i].AutomationId == id || Walk (node.Children[i], id, path))
                    return true;
                path.RemoveAt (path.Count - 1);
            }
            return false;
        }

        [Fact]
        public void Follow_ResolvesElementAtPath ()
        {
            using var form = BuildForm (out _, out _);
            var root = AutomationProvider.BuildTree (form);

            var path = PathOf (root, "okButton");
            Assert.Equal ("okButton", UiaTree.Follow (root, path)?.AutomationId);
            Assert.Null (UiaTree.Follow (root, [999]));   // out-of-range => null
        }

        [Fact]
        public void SiblingNavigation_RoundTrips ()
        {
            using var form = BuildForm (out _, out _);
            var root = AutomationProvider.BuildTree (form);
            var buttonPath = PathOf (root, "okButton");

            // okButton is a direct child of the window, so its parent is the root (empty path).
            Assert.Empty (UiaTree.Parent (buttonPath)!);

            var next = UiaTree.NextSibling (root, buttonPath);
            Assert.NotNull (next);
            // PreviousSibling of the next sibling lands back on the original element.
            Assert.Equal (buttonPath, UiaTree.PreviousSibling (root, next!));
        }

        [Fact]
        public void FirstAndLastChild_PointToEnds ()
        {
            using var form = BuildForm (out _, out _);
            var root = AutomationProvider.BuildTree (form);

            var first = UiaTree.FirstChild (root, []);
            var last = UiaTree.LastChild (root, []);
            Assert.NotNull (first);
            Assert.NotNull (last);
            Assert.Equal (0, first![0]);
            Assert.Equal (root.Children.Count - 1, last![0]);
        }

        [Fact]
        public void HitTest_FindsControlAtPoint ()
        {
            using var form = BuildForm (out _, out _);
            var root = AutomationProvider.BuildTree (form);
            var button = UiaTree.Follow (root, PathOf (root, "okButton"))!;
            var center = new Point (button.Bounds.X + button.Bounds.Width / 2, button.Bounds.Y + button.Bounds.Height / 2);

            var hit = UiaTree.HitTest (root, center);
            Assert.Equal ("okButton", UiaTree.Follow (root, hit)?.AutomationId);
        }

        [Fact]
        public void FindFocused_LocatesFocusedControl ()
        {
            using var form = BuildForm (out _, out var textbox);
            Assert.Null (UiaTree.FindFocused (AutomationProvider.BuildTree (form)));

            textbox.Select ();

            var root = AutomationProvider.BuildTree (form);
            var path = UiaTree.FindFocused (root);
            Assert.NotNull (path);
            Assert.Equal ("nameBox", UiaTree.Follow (root, path!)?.AutomationId);
        }

        [Theory]
        [InlineData ("button")]
        [InlineData ("checkbox")]
        [InlineData ("textbox")]
        [InlineData ("window")]
        public void Mappings_KnownRolesGetSpecificControlTypes (string role)
        {
            Assert.NotEqual (ControlType.Custom.Id, UiaMappings.ControlTypeId (role));
        }

        [Fact]
        public void Mappings_RolesAndPatterns ()
        {
            Assert.Equal (ControlType.Button.Id, UiaMappings.ControlTypeId ("button"));
            Assert.Equal (ControlType.Edit.Id, UiaMappings.ControlTypeId ("textbox"));
            Assert.Equal (ControlType.Custom.Id, UiaMappings.ControlTypeId ("somethingUnknown"));

            Assert.True (UiaMappings.IsInvokable ("button"));
            Assert.False (UiaMappings.IsInvokable ("label"));
            Assert.True (UiaMappings.IsValueEditable ("textbox"));
            Assert.True (UiaMappings.IsTogglable ("checkbox"));
            Assert.True (UiaMappings.IsFocusable ("combobox"));
        }
    }
}
