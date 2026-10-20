using System.Drawing;

namespace Majorsilence.Forms.Renderers
{
    /// <summary>
    /// Represents a class that can render a MenuDropDown.
    /// </summary>
    public class MenuDropDownRenderer : Renderer<MenuDropDown>
    {
        // A ported WinForms app (one that chose its font) gets upstream's drop-down metrics, as its menu bar
        // gets upstream's (see MenuUpstreamMetricsTests), in logical pixels (ToolStripDropDownMenu.ScaleConstants):
        // the image margin (24 plus its border pixel), the text's padding either side, the submenu arrow and the
        // space after it, the smallest item (a 16px check in its 2px padding), and a drop-down item's own
        // padding (ToolStripMenuItem's s_defaultDropDownPadding). Its items were sized from their bar padding
        // (14, 3) and a fixed 70px allowance, so each drop-down was taller and wider than upstream's and its text
        // further in. Apps that have not chosen a font keep the theme's metrics.
        private const int UpstreamImageMarginWidth = 25;
        private const int ThemeImageMarginWidth = 28;
        private const int TextPaddingLeft = 8;
        private const int TextPaddingRight = 9;
        private const int ArrowWidth = 10;
        private const int ArrowPaddingRight = 8;
        private const int MinimumItemHeight = 20;
        private static readonly Padding DropDownItemPadding = new Padding (0, 1, 0, 1);

        private static bool UpstreamMetrics => ControlPaint.UsesUpstreamGlyphs;

        private static int ImageMarginWidth => UpstreamMetrics ? UpstreamImageMarginWidth : ThemeImageMarginWidth;

        // An item's padding on a drop-down: upstream's drop-down default unless the application assigned one.
        private static Padding ItemPadding (MenuItem item)
            => UpstreamMetrics ? item.AssignedPadding ?? DropDownItemPadding : item.Padding;

        // Where an item's caption starts, from the item's left edge, in logical pixels.
        private static int TextInset (MenuDropDown control, MenuItem item)
            => UpstreamMetrics
                ? (ShowsImageMargin (control) ? UpstreamImageMarginWidth : 0) + TextPaddingLeft + ItemPadding (item).Left
                : ShowsImageMargin (control) ? ThemeImageMarginWidth : 6;

        /// <inheritdoc/>
        protected override void Render (MenuDropDown control, PaintEventArgs e)
        {
            // TSM-48: the drop-down's parts go through its ToolStripRenderer too. Only a
            // ToolStripSeparator can be offered to DrawSeparator -- its args require that type, and a
            // MenuSeparatorItem is not one.
            StripRendererBridge.Background (control, e);

            // The icon gutter, offered once for the whole drop-down as upstream does.
            if (ShowsImageMargin (control))
                StripRendererBridge.ImageMargin (control, new Rectangle (0, 0, e.LogicalToDeviceUnits (ImageMarginWidth), control.DeviceClientRectangle.Height), e);

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

            // BarBreak bars between the columns (W6 mechanisms).
            foreach (var bar in control.ColumnBars) {
                var x = e.LogicalToDeviceUnits (bar);
                e.Canvas.DrawLine (x, e.LogicalToDeviceUnits (2), x, control.DeviceClientRectangle.Height - e.LogicalToDeviceUnits (2), Theme.BorderMidColor);
            }

            StripRendererBridge.Border (control, e);
        }

        /// <summary>
        /// Renders a MenuItem.
        /// </summary>
        protected virtual void RenderItem (MenuDropDown control, MenuItem item, PaintEventArgs e)
        {
            // Owner draw (W6 mechanisms): the application paints the whole item, background included.
            if (item.RaiseDrawItem (item.DeviceBounds, e))
                return;

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
                // An indeterminate ToolStripMenuItem draws as one (TSM-24). RadioCheck is MenuItem's, so
                // the legacy MenuItem gets its bullet too; this tested for ToolStripMenuItem, which
                // inherits the property and is not the type that declares it (TSM-32).
                var state = item is ToolStripMenuItem tri ? tri.CheckState : CheckState.Checked;

                if (StripRendererBridge.Check (control, item, glyph_rect, e)) {
                    // the renderer drew the check itself
                } else if (item.RadioCheck)
                    ControlPaint.DrawRadioButton (e, glyph_rect.Location, state, !item.Enabled);
                else
                    ControlPaint.DrawCheckBox (e, glyph_rect, state, !item.Enabled);
            } else if (item.ImageSK != null) {
                var image_size = e.LogicalToDeviceUnits (16);
                var image_bounds = DrawingExtensions.CenterSquare (item.DeviceBounds, image_size);
                var image_rect = new Rectangle (item.DeviceBounds.Left + e.LogicalToDeviceUnits (6), image_bounds.Top, image_size, image_size);

                if (!StripRendererBridge.Image (control, item, image_rect, e))
                    e.Canvas.DrawBitmap (item.ImageSK, image_rect, !item.Enabled);
            }

            // Text
            var font_color = item.Enabled ? item_style.GetForegroundColor () : Theme.ForegroundDisabledColor;
            var (item_typeface, item_font_size) = StripItemFont.Resolve (item, control);
            var font_size = e.LogicalToDeviceUnits (item_font_size);
            var bounds = item.DeviceBounds;

            // ShowImageMargin collapses the gutter the icons sit in, which is what a text-only context
            // menu asks for. Both declarations of it -- ContextMenuStrip's and
            // ToolStripDropDownMenu's -- were stored and read by nothing, so every drop-down carried
            // 28 units of empty space (TSM-45). This was wired once before and reverted: the indent is
            // device and the box it was measured against was logical, so at scaling 2 the caption
            // landed past the item's right edge whatever the property said. TSM-41 is what made it
            // expressible.
            bounds.X += e.LogicalToDeviceUnits (TextInset (control, item));
            if (StripRendererBridge.Text (control, item, item.Text, bounds, font_color, e) is { } tp) {
                bounds = tp.rect;
                font_color = tp.colour;
                e.Canvas.DrawMnemonicText (tp.text, item_typeface, font_size, bounds, font_color, ContentAlignment.MiddleLeft, maxLines: null, ellipsis: false, underline: control.ShowKeyboardCues);
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
                    e.Canvas.DrawText (shortcut, item_typeface, font_size, shortcut_bounds, font_color,
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

            var padding = control.LogicalToDeviceUnits (ItemPadding (item));
            var (item_typeface, item_font_size) = StripItemFont.Resolve (item, control);
            var font_size = control.LogicalToDeviceUnits (item_font_size);
            var text_size = TextMeasurer.MeasureText (Mnemonics.Strip (item.Text), item_typeface, font_size);
            var text_width = (int)Math.Round (text_size.Width, 0, MidpointRounding.AwayFromZero);
            var text_height = (int)Math.Round (text_size.Height, 0, MidpointRounding.AwayFromZero);

            if (!UpstreamMetrics)
                return new Size (text_width + padding.Horizontal + control.LogicalToDeviceUnits (70), text_height + control.LogicalToDeviceUnits (8));

            // A shortcut is laid out after the caption, a tab's width further on, as upstream measures it.
            if (item is ToolStripMenuItem { ShortcutDisplayText: { Length: > 0 } shortcut }) {
                var shortcut_size = TextMeasurer.MeasureText (shortcut, item_typeface, font_size);
                text_width += control.LogicalToDeviceUnits (ShortcutGap) + (int)Math.Ceiling (shortcut_size.Width);
                text_height = Math.Max (text_height, (int)Math.Round (shortcut_size.Height, 0, MidpointRounding.AwayFromZero));
            }

            // Upstream: the image margin, the text in its padding, then room for the submenu arrow -- kept on
            // every item, so the column lines up whether or not an item has a submenu.
            var margin = ShowsImageMargin (control) ? ImageMarginWidth : 0;
            var chrome = control.LogicalToDeviceUnits (margin + TextPaddingLeft + TextPaddingRight + ArrowWidth + ArrowPaddingRight);
            var height = Math.Max (text_height + control.LogicalToDeviceUnits (2), control.LogicalToDeviceUnits (MinimumItemHeight));

            return new Size (text_width + chrome + padding.Horizontal, height + padding.Vertical);
        }

        // The space between a caption and its shortcut: a tab, as upstream measures it with TextRenderer.
        private const int ShortcutGap = 24;

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
        // Either margin keeps the gutter: with the image margin off and the check margin on, upstream
        // still reserves the column for check marks (W6.2 sweep).
        internal static bool ShowsImageMargin (MenuDropDown control)
            => control switch {
                ContextMenuStrip menu => menu.ShowImageMargin || menu.ShowCheckMargin,
                ToolStripDropDownMenu drop => drop.ShowImageMargin || drop.ShowCheckMargin,
                _ => true
            };
    }
}
