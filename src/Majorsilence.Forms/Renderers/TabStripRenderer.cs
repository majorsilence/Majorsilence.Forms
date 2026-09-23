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
            // TabStripItem.Bounds is LOGICAL -- it is the hit-test space, like every other item box
            // here -- and this canvas is DEVICE. Converted once, which is the boundary TSM-41
            // established for the MenuItem-based renderers. TabStripItem has no owner reference, so it
            // cannot carry a DeviceBounds of its own the way MenuItem does and the control converts
            // instead.
            //
            // Deliberately NOT applied to the DrawItemEventArgs above: that rectangle is handed to
            // application owner-draw code, where the logical box is the contract.
            var bounds = control.LogicalToDeviceUnits (item.Bounds);

            // The part style for this tab's state: hover wins over selected (a hovered selected tab still
            // lights up), then the plain item style. A part with no background leaves the strip showing.
            var item_style = item.Hovered && item.Enabled ? TabStrip.DefaultItemHoverStyle
                : item.Selected ? TabStrip.DefaultSelectedItemStyle
                : TabStrip.DefaultItemStyle;

            if (item_style.TryGetBackgroundColor () is { } item_bg)
                e.Canvas.FillRectangle (bounds, item_bg);

            // Draw focus rectangle
            if (control.Selected && control.ShowFocusCues && control.Tabs.FocusedIndex == control.Tabs.IndexOf (item))
                e.Canvas.DrawFocusRectangle (bounds, e.LogicalToDeviceUnits (1));

            // Draw with the strip's ambient effective font -- the same resolution
            // TabStripItem.GetPreferredSize measures with, so text always fits its tab. Selection
            // emphasis comes from the accent underline below rather than a bold variant.
            var font_color = !item.Enabled || !control.Enabled
                ? Theme.ForegroundDisabledColor
                : item_style.TryGetForegroundColor () ?? control.GetEffectiveForegroundColor ();

            // TabControl.HotTrack: the hovered tab's text takes the hot-track colour (W6).
            if (item.Hovered && item.Enabled && control.OwnerTabControl is { HotTrack: true })
                font_color = SystemColors.HotTrack.ToSKColor ();
            var font = control.GetEffectiveFont ();
            var font_size = control.LogicalToDeviceUnits (control.GetEffectiveFontSize ());

            // An imaged tab draws the icon at its leading edge and gives the rest to the caption.
            // TabStripItem.GetPreferredSize reserved exactly this much room, so the text still lands
            // centred in what is left; a tab with no image is laid out and painted as before (LAY-14).
            var text_bounds = bounds;

            if (item.Image is { } image) {
                var size = new Size (control.LogicalToDeviceUnits (item.ImageSize.Width), control.LogicalToDeviceUnits (item.ImageSize.Height));
                var left = bounds.Left + control.LogicalToDeviceUnits (item.Padding.Left);
                var image_bounds = new Rectangle (left, bounds.Top + ((bounds.Height - size.Height) / 2), size.Width, size.Height);

                e.Canvas.DrawBitmap (image, image_bounds, !item.Enabled || !control.Enabled);

                text_bounds = Rectangle.FromLTRB (image_bounds.Right + control.LogicalToDeviceUnits (TabStripItem.IMAGE_TEXT_GAP),
                    bounds.Top, bounds.Right, bounds.Bottom);
            }

            e.Canvas.DrawText (item.Text, font, font_size, text_bounds, font_color, ContentAlignment.MiddleCenter);

            if (item.Selected) {
                var underline = TabStrip.DefaultSelectedItemStyle.Border.Bottom;
                var highlight_padding = e.LogicalToDeviceUnits (10);
                var highlight_height = e.LogicalToDeviceUnits (underline.GetWidth ());
                var highlight_bounds = new Rectangle (bounds.Left + highlight_padding, bounds.Bottom - highlight_height, bounds.Width - (2 * highlight_padding), highlight_height);

                if (highlight_height > 0)
                    e.Canvas.FillRectangle (highlight_bounds, underline.GetColor ());
            }
        }
    }
}
