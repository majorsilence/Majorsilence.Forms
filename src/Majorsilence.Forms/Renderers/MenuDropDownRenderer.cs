using System.Drawing;

namespace Majorsilence.Forms.Renderers
{
    /// <summary>
    /// Represents a class that can render a MenuDropDown.
    /// </summary>
    public class MenuDropDownRenderer : Renderer<MenuDropDown>
    {
        /// <inheritdoc/>
        protected override void Render (MenuDropDown control, PaintEventArgs e)
        {
            foreach (var item in control.Items) {
                if (!item.Visible)
                    continue;

                if (item is MenuSeparatorItem msi)
                    RenderMenuSeparatorItem (control, msi, e);
                else if (item is ToolStripSeparator tss)
                    RenderMenuSeparatorItem (control, tss, e);
                else
                    RenderItem (control, item, e);
            }
        }

        /// <summary>
        /// Renders a MenuItem.
        /// </summary>
        protected virtual void RenderItem (MenuDropDown control, MenuItem item, PaintEventArgs e)
        {
            // Background
            var background_color = item.Hovered || item.IsDropDownOpened ? Theme.ControlHighlightLowColor : Theme.ControlLowColor;
            e.Canvas.FillRectangle (item.Bounds, background_color);

            // A check mark goes in the image gutter, which is the 28px inset the text starts after.
            // Nothing drew one before, so a checked menu item was indistinguishable from an unchecked
            // one -- the user could not see the mode they were in (TSM-06). An item with both a check
            // and an image gets the check: that is the state, where the image is decoration.
            if (item.Checked) {
                var glyph = e.LogicalToDeviceUnits (12);
                var centred = DrawingExtensions.CenterSquare (item.Bounds, glyph);
                var glyph_rect = new Rectangle (item.Bounds.Left + e.LogicalToDeviceUnits (8), centred.Top, glyph, glyph);

                // ControlPaint.DrawMenuGlyph is one of the Graphics-based WinForms-compat stubs and
                // paints nothing; the check box and radio painters are the real ones this framework
                // draws every other check with. Upstream's menu tick has no box around it, which is a
                // cosmetic difference from what a Win32 menu draws and the same glyph the rest of this
                // toolkit uses for the same meaning.
                if (item is ToolStripMenuItem { RadioCheck: true })
                    ControlPaint.DrawRadioButton (e, glyph_rect.Location, CheckState.Checked, !item.Enabled);
                else
                    ControlPaint.DrawCheckBox (e, glyph_rect, CheckState.Checked, !item.Enabled);
            } else if (item.ImageSK != null) {
                var image_size = e.LogicalToDeviceUnits (16);
                var image_bounds = DrawingExtensions.CenterSquare (item.Bounds, image_size);
                var image_rect = new Rectangle (item.Bounds.Left + e.LogicalToDeviceUnits (6), image_bounds.Top, image_size, image_size);
                e.Canvas.DrawBitmap (item.ImageSK, image_rect, !item.Enabled);
            }

            // Text
            var font_color = item.Enabled ? Theme.ForegroundColor : Theme.ForegroundDisabledColor;
            var font_size = e.LogicalToDeviceUnits (Theme.FontSize);
            var bounds = item.Bounds;
            bounds.X += e.LogicalToDeviceUnits (28);
            e.Canvas.DrawMnemonicText (item.Text, Theme.UIFont, font_size, bounds, font_color, ContentAlignment.MiddleLeft);

            // Dropdown Arrow
            if (item.HasItems) {
                var arrow_bounds = DrawingExtensions.CenterSquare (item.Bounds, 16);
                var arrow_area = new Rectangle (item.Bounds.Right - e.LogicalToDeviceUnits (16) - 4, arrow_bounds.Top, 16, 16);
                ControlPaint.DrawArrowGlyph (e, arrow_area, font_color, ArrowDirection.Right);
            }
        }

        /// <summary>
        /// Renders a ToolStripSeparator as a visual divider.
        /// </summary>
        protected virtual void RenderMenuSeparatorItem (MenuDropDown control, ToolStripSeparator item, PaintEventArgs e)
        {
            e.Canvas.FillRectangle (item.Bounds, Theme.ControlLowColor);

            var center = item.Bounds.GetCenter ();
            var thickness = e.LogicalToDeviceUnits (1);
            var padding = e.LogicalToDeviceUnits (item.Padding);

            e.Canvas.DrawLine (item.Bounds.X + padding.Top, center.Y, item.Bounds.Right - padding.Right, center.Y, Theme.ControlHighlightLowColor, thickness);
        }

        /// <summary>
        /// Renders a MenuSeparatorItem.
        /// </summary>
        protected virtual void RenderMenuSeparatorItem (MenuDropDown control, MenuSeparatorItem item, PaintEventArgs e)
        {
            // Background
            e.Canvas.FillRectangle (item.Bounds, Theme.ControlLowColor);

            var center = item.Bounds.GetCenter ();
            var thickness = e.LogicalToDeviceUnits (1);
            var padding = e.LogicalToDeviceUnits (item.Padding);

            e.Canvas.DrawLine (item.Bounds.X + padding.Top, center.Y, item.Bounds.Right - padding.Right, center.Y, item.Enabled ? Theme.ControlHighlightLowColor : Theme.ForegroundDisabledColor, thickness);
        }

        /// <summary>
        /// Gets the preferred size of a MenuItem.
        /// </summary>
        public virtual Size GetPreferredItemSize (MenuDropDown control, MenuItem item, Size proposedSize)
        {
            if (item is MenuSeparatorItem msi)
                return GetPreferredSeparatorItemSize (control, msi, proposedSize);

            if (item is ToolStripSeparator tss)
                return GetPreferredSeparatorItemSize (control, tss, proposedSize);

            var padding = control.LogicalToDeviceUnits (item.Padding);
            var font_size = control.LogicalToDeviceUnits (Theme.FontSize);
            var text_size = TextMeasurer.MeasureText (Mnemonics.Strip (item.Text), Theme.UIFont, font_size);

            return new Size ((int)Math.Round (text_size.Width, 0, MidpointRounding.AwayFromZero) + padding.Horizontal + control.LogicalToDeviceUnits (70), (int)Math.Round (text_size.Height, 0, MidpointRounding.AwayFromZero) + control.LogicalToDeviceUnits (8));
        }

        /// <summary>
        /// Gets the preferred size of a ToolStripSeparator.
        /// </summary>
        protected virtual Size GetPreferredSeparatorItemSize (MenuDropDown control, ToolStripSeparator item, Size proposedSize)
        {
            var padding = control.LogicalToDeviceUnits (item.Padding.Vertical);
            var thickness = control.LogicalToDeviceUnits (1);

            return new Size (item.Bounds.Width, thickness + padding);
        }

        /// <summary>
        /// Gets the preferred size of a MenuSeparatorItem.
        /// </summary>
        protected virtual Size GetPreferredSeparatorItemSize (MenuDropDown control, MenuSeparatorItem item, Size proposedSize)
        {
            var padding = control.LogicalToDeviceUnits (item.Padding.Vertical);
            var thickness = control.LogicalToDeviceUnits (1);

            return new Size (item.Bounds.Width, thickness + padding);
        }
    }
}
