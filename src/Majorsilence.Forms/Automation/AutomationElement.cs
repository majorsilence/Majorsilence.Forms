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
            Control source,
            string automationId,
            string name,
            string role,
            string controlType,
            string? value,
            bool enabled,
            bool visible,
            Rectangle bounds,
            IReadOnlyList<AutomationElement> children)
        {
            Source = source;
            AutomationId = automationId;
            Name = name;
            Role = role;
            ControlType = controlType;
            Value = value;
            Enabled = enabled;
            Visible = visible;
            Bounds = bounds;
            Children = children;
        }

        // The underlying control this snapshot was built from (not exposed publicly to keep the model
        // a stable, backend-neutral contract).
        internal Control Source { get; }

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

        /// <summary>Whether the control is enabled.</summary>
        public bool Enabled { get; }

        /// <summary>Whether the control is visible.</summary>
        public bool Visible { get; }

        /// <summary>The element's bounds in window-client (logical) coordinates.</summary>
        public Rectangle Bounds { get; }

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
