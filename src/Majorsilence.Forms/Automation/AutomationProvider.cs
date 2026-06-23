using System.Collections.Generic;
using System.Drawing;

namespace Majorsilence.Forms.Automation
{
    /// <summary>
    /// Builds the <see cref="AutomationElement"/> tree for a window by walking its control hierarchy.
    /// Backend-neutral: it reads the same logical control bounds/state the renderers use, so it works
    /// identically on the headless and real backends.
    /// </summary>
    public static class AutomationProvider
    {
        /// <summary>Builds a fresh snapshot of the window's automation tree, rooted at a synthetic window node.</summary>
        public static AutomationElement BuildTree (WindowBase window)
        {
            System.ArgumentNullException.ThrowIfNull (window);

            var adapter = window.adapter;
            var origin = adapter.Bounds.Location;

            var children = BuildChildren (adapter, origin);

            var title = window is Form form ? form.Text : string.Empty;

            return new AutomationElement (
                source: adapter,
                automationId: string.Empty,
                name: title ?? string.Empty,
                role: "window",
                controlType: window.GetType ().Name,
                value: null,
                enabled: true,
                visible: true,
                bounds: new Rectangle (origin, adapter.Size),
                children: children);
        }

        private static List<AutomationElement> BuildChildren (Control parent, Point parentOrigin)
        {
            var list = new List<AutomationElement> ();

            foreach (var c in parent.Controls.GetAllControls (includeImplicit: true)) {
                if (!c.Visible)
                    continue;

                var origin = new Point (parentOrigin.X + c.Bounds.X, parentOrigin.Y + c.Bounds.Y);
                list.Add (BuildNode (c, origin));
            }

            return list;
        }

        private static AutomationElement BuildNode (Control c, Point origin)
        {
            var children = BuildChildren (c, origin);

            return new AutomationElement (
                source: c,
                automationId: c.Name ?? string.Empty,
                name: AccessibleNameOf (c),
                role: RoleOf (c),
                controlType: c.GetType ().Name,
                value: ValueOf (c),
                enabled: c.Enabled,
                visible: c.Visible,
                bounds: new Rectangle (origin, c.Size),
                children: children);
        }

        private static string AccessibleNameOf (Control c)
        {
            if (!string.IsNullOrEmpty (c.AccessibleName))
                return c.AccessibleName!;
            if (!string.IsNullOrEmpty (c.Text))
                return c.Text;
            return c.Name ?? string.Empty;
        }

        // Maps a control to a coarse role. Honors an explicitly-set AccessibleRole, otherwise infers
        // from the control type. Names are lower-case and roughly follow ARIA/WinForms conventions.
        private static string RoleOf (Control c)
        {
            if (c.AccessibleRole != AccessibleRole.Default)
                return c.AccessibleRole.ToString ().ToLowerInvariant ();

            return c switch {
                Button => "button",
                CheckBox => "checkbox",
                RadioButton => "radio",
                TextBox => "textbox",
                ComboBox => "combobox",
                ListBox => "list",
                Label => "label",
                TabControl => "tablist",
                TabStrip => "tablist",
                ProgressBar => "progressbar",
                ScrollBar => "scrollbar",
                Panel => "group",
                _ => c.GetType ().Name.ToLowerInvariant ()
            };
        }

        // Value-bearing controls report their value so tests/automation can assert on it.
        private static string? ValueOf (Control c) => c switch {
            CheckBox cb => cb.Checked ? "true" : "false",
            RadioButton rb => rb.Checked ? "true" : "false",
            TextBox tb => tb.Text,
            ComboBox cbo => cbo.Text,
            _ => null
        };
    }
}
