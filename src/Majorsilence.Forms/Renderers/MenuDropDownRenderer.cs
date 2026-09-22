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
            // TSM-48: the drop-down's parts go through its ToolStripRenderer too. Only a
            // ToolStripSeparator can be offered to DrawSeparator -- its args require that type, and a
            // MenuSeparatorItem is not one.
            StripRendererBridge.Background (control, e);

            // The icon gutter, offered once for the whole drop-down as upstream does.
            if (ShowsImageMargin (control))
                StripRendererBridge.ImageMargin (control, new Rectangle (0, 0, e.LogicalToDeviceUnits (28), control.ClientRectangle.Height), e);

            foreach (var item in control.Items) {
                if (!item.Visible)
                    continue;

                if (item is MenuSeparatorItem msi)
                    RenderMenuSeparatorItem (control, msi, e);
                else if (item is ToolStripSeparator tss) {
                    StripRendererBridge.Separator (control, tss, vertical: false, e);
                    RenderMenuSeparatorItem (control, tss, e);
                } else
                    RenderItem (control, item, e);
            }

            StripRendererBridge.Border (control, e);
        }

        /// <summary>
        /// Renders a MenuItem.
        /// </summary>
        protected virtual void RenderItem (MenuDropDown control, MenuItem item, PaintEventArgs e)
        {
            // Background
            var item_style = item.Hovered || item.IsDropDownOpened ? MenuDropDown.DefaultItemHoverStyle : MenuDropDown.DefaultItemStyle;
            var background_color = item_style.GetBackgroundColor ();

            if (!StripRendererBridge.ItemBackground (control, item, e))
                e.Canvas.FillRectangle (item.DeviceBounds, background_color);

            // A check mark goes in the image gutter, which is the 28px inset the text starts after.
            // Nothing drew one before, so a checked menu item was indistinguishable from an unchecked
            // one -- the user could not see the mode they were in (TSM-06). An item with both a check
            // and an image gets the check: that is the state, where the image is decoration.
            if (item.Checked) {
                var glyph = e.LogicalToDeviceUnits (12);
                var centred = DrawingExtensions.CenterSquare (item.DeviceBounds, glyph);
                var glyph_rect = new Rectangle (item.DeviceBounds.Left + e.LogicalToDeviceUnits (8), centred.Top, glyph, glyph);

                // ControlPaint.DrawMenuGlyph is one of the Graphics-based WinForms-compat stubs and
                // paints nothing; the check box and radio painters are the real ones this framework
                // draws every other check with. Upstream's menu tick has no box around it, which is a
                // cosmetic difference from what a Win32 menu draws and the same glyph the rest of this
                // toolkit uses for the same meaning.
                if (StripRendererBridge.Check (control, item, glyph_rect, e)) {
                    // the renderer drew the check itself
                } else if (item is ToolStripMenuItem { RadioCheck: true })
                    ControlPaint.DrawRadioButton (e, glyph_rect.Location, CheckState.Checked, !item.Enabled);
                else
                    ControlPaint.DrawCheckBox (e, glyph_rect, CheckState.Checked, !item.Enabled);
            } else if (item.ImageSK != null) {
                var image_size = e.LogicalToDeviceUnits (16);
                var image_bounds = DrawingExtensions.CenterSquare (item.DeviceBounds, image_size);
                var image_rect = new Rectangle (item.DeviceBounds.Left + e.LogicalToDeviceUnits (6), image_bounds.Top, image_size, image_size);

                if (!StripRendererBridge.Image (control, item, image_rect, e))
                    e.Canvas.DrawBitmap (item.ImageSK, image_rect, !item.Enabled);
            }

            // Text
            var font_color = item.Enabled ? item_style.GetForegroundColor () : Theme.ForegroundDisabledColor;
            var font_size = e.LogicalToDeviceUnits (Theme.FontSize);
            var bounds = item.DeviceBounds;

            // ShowImageMargin collapses the gutter the icons sit in, which is what a text-only context
            // menu asks for. Both declarations of it -- ContextMenuStrip's and
            // ToolStripDropDownMenu's -- were stored and read by nothing, so every drop-down carried
            // 28 units of empty space (TSM-45). This was wired once before and reverted: the indent is
            // device and the box it was measured against was logical, so at scaling 2 the caption
            // landed past the item's right edge whatever the property said. TSM-41 is what made it
            // expressible.
            bounds.X += e.LogicalToDeviceUnits (ShowsImageMargin (control) ? 28 : 6);
            if (StripRendererBridge.Text (control, item, item.Text, bounds, font_color, e) is { } tp) {
                bounds = tp.rect;
                font_color = tp.colour;
                e.Canvas.DrawMnemonicText (tp.text, Theme.UIFont, font_size, bounds, font_color, ContentAlignment.MiddleLeft);
            }

            // Shortcut text, right-aligned in the gutter the submenu arrow also uses. Drawn only when
            // the item has no submenu, as upstream does -- an item cannot both open a menu and carry
            // an accelerator, and drawing both would overlap them.
            if (item is ToolStripMenuItem menu_item && !item.HasItems) {
                var shortcut = menu_item.ShortcutDisplayText;

                if (!string.IsNullOrEmpty (shortcut)) {
                    var shortcut_bounds = item.DeviceBounds;
                    shortcut_bounds.Width -= e.LogicalToDeviceUnits (12);

                    // Same colour as the caption, so a disabled item's shortcut greys out with it.
                    e.Canvas.DrawText (shortcut, Theme.UIFont, font_size, shortcut_bounds, font_color,
                        ContentAlignment.MiddleRight, maxLines: 1);
                }
            }

            // Dropdown Arrow
            if (item.HasItems) {
                var arrow_bounds = DrawingExtensions.CenterSquare (item.DeviceBounds, 16);
                var arrow_area = new Rectangle (item.DeviceBounds.Right - e.LogicalToDeviceUnits (16) - 4, arrow_bounds.Top, 16, 16);

                if (StripRendererBridge.Arrow (control, item, arrow_area, font_color, ArrowDirection.Right, e) is { } ap)
                    ControlPaint.DrawArrowGlyph (e, ap.rect, ap.colour, ap.direction);
            }
        }

        /// <summary>
        /// Renders a ToolStripSeparator as a visual divider.
        /// </summary>
        protected virtual void RenderMenuSeparatorItem (MenuDropDown control, ToolStripSeparator item, PaintEventArgs e)
        {
            e.Canvas.FillRectangle (item.DeviceBounds, Theme.ControlLowColor);

            var center = item.DeviceBounds.GetCenter ();
            var thickness = e.LogicalToDeviceUnits (1);
            var padding = e.LogicalToDeviceUnits (item.Padding);

            e.Canvas.DrawLine (item.DeviceBounds.X + padding.Top, center.Y, item.DeviceBounds.Right - padding.Right, center.Y, Theme.ControlHighlightLowColor, thickness);
        }

        /// <summary>
        /// Renders a MenuSeparatorItem.
        /// </summary>
        protected virtual void RenderMenuSeparatorItem (MenuDropDown control, MenuSeparatorItem item, PaintEventArgs e)
        {
            // Background
            e.Canvas.FillRectangle (item.DeviceBounds, Theme.ControlLowColor);

            var center = item.DeviceBounds.GetCenter ();
            var thickness = e.LogicalToDeviceUnits (1);
            var padding = e.LogicalToDeviceUnits (item.Padding);

            e.Canvas.DrawLine (item.DeviceBounds.X + padding.Top, center.Y, item.DeviceBounds.Right - padding.Right, center.Y, item.Enabled ? Theme.ControlHighlightLowColor : Theme.ForegroundDisabledColor, thickness);
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

            return new Size (item.DeviceBounds.Width, thickness + padding);
        }

        /// <summary>
        /// Gets the preferred size of a MenuSeparatorItem.
        /// </summary>
        protected virtual Size GetPreferredSeparatorItemSize (MenuDropDown control, MenuSeparatorItem item, Size proposedSize)
        {
            var padding = control.LogicalToDeviceUnits (item.Padding.Vertical);
            var thickness = control.LogicalToDeviceUnits (1);

            return new Size (item.DeviceBounds.Width, thickness + padding);
        }

        /// <summary>
        /// Whether this drop-down reserves the image gutter.
        /// </summary>
        /// <remarks>
        /// Declared separately on <see cref="ContextMenuStrip"/> and
        /// <see cref="ToolStripDropDownMenu"/> -- two properties for one piece of state, so both are
        /// read here and either one turning it off collapses the gutter. Anything else (a plain
        /// <see cref="MenuDropDown"/>, a submenu) keeps it, as it always has.
        /// </remarks>
        private static bool ShowsImageMargin (MenuDropDown control)
            => control switch {
                ContextMenuStrip menu => menu.ShowImageMargin,
                ToolStripDropDownMenu drop => drop.ShowImageMargin,
                _ => true
            };
    }
}
