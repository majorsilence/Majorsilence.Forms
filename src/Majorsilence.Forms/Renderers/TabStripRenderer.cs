using System.Drawing;

namespace Majorsilence.Forms.Renderers
{
    /// <summary>
    /// Represents a class that can render a TabStrip.
    /// </summary>
    public class TabStripRenderer : Renderer<TabStrip>
    {
        /// <inheritdoc/>
        protected override void Render (TabStrip control, PaintEventArgs e)
        {
            // An owner-drawn TabControl paints every tab through its DrawItem event instead, so the
            // strip contributes nothing of its own -- matching WinForms, where the built-in tab
            // painting is replaced wholesale rather than drawn underneath.
            var owner = control.Parent as TabControl;

            if (owner?.IsOwnerDrawn == true) {
                RenderOwnerDrawn (owner, control, e);
                return;
            }

            foreach (var item in control.Tabs)
                RenderItem (control, item, e);
        }

        private static void RenderOwnerDrawn (TabControl owner, TabStrip control, PaintEventArgs e)
        {
            for (var index = 0; index < control.Tabs.Count; index++) {
                var item = control.Tabs[index];

                var state = DrawItemState.Default;

                if (item.Selected)
                    state |= DrawItemState.Selected;
                if (!item.Enabled || !control.Enabled)
                    state |= DrawItemState.Disabled;
                if (item.Hovered)
                    state |= DrawItemState.HotLight;

                using var args = new DrawItemEventArgs (e.Graphics, owner.Font, item.Bounds, index, state);

                owner.RaiseDrawItem (args);
            }
        }

        /// <summary>
        /// Renders a TabStripItem.
        /// </summary>
        protected virtual void RenderItem (TabStrip control, TabStripItem item, PaintEventArgs e)
        {
            // Hover background
            if (item.Hovered && item.Enabled)
                e.Canvas.FillRectangle (item.Bounds, Theme.ControlLowColor);

            // Draw focus rectangle
            if (control.Selected && control.ShowFocusCues && control.Tabs.FocusedIndex == control.Tabs.IndexOf (item))
                e.Canvas.DrawFocusRectangle (item.Bounds, e.LogicalToDeviceUnits (1));

            // Draw with the strip's ambient effective font -- the same resolution
            // TabStripItem.GetPreferredSize measures with, so text always fits its tab. Selection
            // emphasis comes from the accent underline below rather than a bold variant.
            var font_color = !item.Enabled || !control.Enabled
                ? Theme.ForegroundDisabledColor
                : control.GetEffectiveForegroundColor ();
            var font = control.GetEffectiveFont ();
            var font_size = control.LogicalToDeviceUnits (control.GetEffectiveFontSize ());

            e.Canvas.DrawText (item.Text, font, font_size, item.Bounds, font_color, ContentAlignment.MiddleCenter);

            if (item.Selected) {
                var highlight_padding = e.LogicalToDeviceUnits (10);
                var highlight_height = e.LogicalToDeviceUnits (3);
                var highlight_bounds = new Rectangle (item.Bounds.Left + highlight_padding, item.Bounds.Bottom - highlight_height, item.Bounds.Width - (2 * highlight_padding), highlight_height);

                e.Canvas.FillRectangle (highlight_bounds, Theme.AccentColor2);
            }
        }
    }
}
