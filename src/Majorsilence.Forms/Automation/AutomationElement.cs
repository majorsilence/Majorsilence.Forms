using System.Collections.Generic;
using System.Drawing;
using System.Linq;

namespace Majorsilence.Forms.Automation
{
    /// <summary>
    /// An immutable snapshot of a control in the accessibility / automation tree. This is the shared
    /// model consumed by in-process UI tests (<see cref="AutomationSession"/>), by the WebDriver server
    /// (Selenium-compatible automation), and — once bridged per platform — by OS screen readers.
    /// </summary>
    public sealed class AutomationElement
    {
        internal AutomationElement (
            object source,
            string automationId,
            string name,
            string role,
            string controlType,
            string? value,
            IReadOnlyDictionary<string, string> state,
            bool enabled,
            bool visible,
            bool focused,
            Rectangle bounds,
            IReadOnlyList<AutomationElement> children,
            AutomationLiveSetting liveSetting = AutomationLiveSetting.Off)
        {
            Source = source;
            AutomationId = automationId;
            Name = name;
            Role = role;
            ControlType = controlType;
            Value = value;
            State = state;
            Enabled = enabled;
            Visible = visible;
            Focused = focused;
            Bounds = bounds;
            Children = children;
            LiveSetting = liveSetting;
        }

        // The underlying control this snapshot was built from (not exposed publicly to keep the model
        // a stable, backend-neutral contract).
        // object, not Control: a menu or toolbar item is a MenuItem, which is not a Control, and those
        // items are part of the tree (see AutomationProvider.BuildItems). Consumers pattern-match on the
        // type they care about.
        internal object Source { get; }

        /// <summary>Stable automation id (the control's <see cref="Control.Name"/>), or empty.</summary>
        public string AutomationId { get; }

        /// <summary>Accessible name: <c>AccessibleName</c> if set, otherwise the control's text, otherwise its name.</summary>
        public string Name { get; }

        /// <summary>Coarse role string (e.g. <c>button</c>, <c>textbox</c>, <c>checkbox</c>, <c>window</c>).</summary>
        public string Role { get; }

        /// <summary>The concrete control type name (e.g. <c>Button</c>, <c>RadButton</c>).</summary>
        public string ControlType { get; }

        /// <summary>The element's value for value-bearing controls (text box content, checked state, …), or null.</summary>
        public string? Value { get; }

        /// <summary>
        /// Extra state a custom-painted control published via <see cref="IAutomationStateProvider.AutomationState"/>
        /// (register item F19) -- the level a status widget is showing, for example. Empty for every
        /// built-in control, which has no need for it: role, name and value already cover what those
        /// report. Each entry becomes a <c>state-{key}</c> attribute in the automation XML page source.
        /// </summary>
        public IReadOnlyDictionary<string, string> State { get; }

        /// <summary>Whether the control is enabled.</summary>
        public bool Enabled { get; }

        /// <summary>Whether the control is visible.</summary>
        public bool Visible { get; }

        /// <summary>Whether the control currently has keyboard focus (a per-snapshot reading of live state).</summary>
        public bool Focused { get; }

        /// <summary>The element's bounds in window-client (logical) coordinates.</summary>
        public Rectangle Bounds { get; }

        /// <summary>
        /// How assistive technology announces changes to this element: a <see cref="Label"/>'s or
        /// <see cref="ToolStripStatusLabel"/>'s <c>LiveSetting</c>, <see cref="AutomationLiveSetting.Off"/>
        /// for everything else. What the UI Automation bridge reports as LiveSetting and what the browser
        /// accessibility DOM announces a text change at.
        /// </summary>
        public AutomationLiveSetting LiveSetting { get; }

        private string? help_text;
        private bool help_text_read;

        /// <summary>
        /// The element's help text: its accessible object's <see cref="AccessibleObject.Help"/>, which raises
        /// <see cref="Control.QueryAccessibilityHelp"/> for a control, <see cref="ToolStripItem.QueryAccessibilityHelp"/>
        /// for a menu or tool strip item, and <see cref="WindowBase.QueryAccessibilityHelp"/> for the window
        /// root -- or null. What the UI Automation bridge reports as HelpText and the browser accessibility
        /// DOM as <c>aria-description</c>.
        /// </summary>
        /// <remarks>Read when first asked for, not when the snapshot is built, and then kept: upstream raises
        /// the event when a client asks for help, so building a tree to find one element does not raise it
        /// for every control in the window. Read it on the UI thread, since it runs application handlers.</remarks>
        public string? HelpText {
            get {
                if (!help_text_read) {
                    help_text = Source switch {
                        // The root's source is the adapter standing in for the window; the window is
                        // what a client addresses, so its accessible object answers.
                        ControlAdapter adapter => adapter.ParentForm.AccessibilityObject.Help,
                        Control control => control.AccessibilityObject.Help,
                        ToolStripItem item => item.AccessibilityObject.Help,
                        _ => null,
                    };
                    help_text_read = true;
                }

                return help_text;
            }
        }

        /// <summary>The child elements, in z-order.</summary>
        public IReadOnlyList<AutomationElement> Children { get; }

        /// <summary>The center of <see cref="Bounds"/> — where a synthesized click is delivered.</summary>
        public Point ClickPoint => new (Bounds.X + Bounds.Width / 2, Bounds.Y + Bounds.Height / 2);

        /// <summary>Enumerates this element's descendants depth-first (not including this element).</summary>
        public IEnumerable<AutomationElement> Descendants ()
        {
            foreach (var child in Children) {
                yield return child;
                foreach (var d in child.Descendants ())
                    yield return d;
            }
        }

        /// <summary>This element followed by all its descendants, depth-first.</summary>
        public IEnumerable<AutomationElement> Self () => Enumerable.Repeat (this, 1).Concat (Descendants ());

        /// <inheritdoc/>
        public override string ToString () =>
            $"{Role} \"{Name}\"{(AutomationId.Length > 0 ? $" #{AutomationId}" : string.Empty)} [{ControlType}] {Bounds}";
    }
}
