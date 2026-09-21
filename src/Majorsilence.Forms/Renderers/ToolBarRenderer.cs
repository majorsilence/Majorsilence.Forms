using System.Drawing;

namespace Majorsilence.Forms.Renderers
{
    /// <summary>
    /// Represents a class that can render a ToolBar.
    /// </summary>
    public class ToolBarRenderer : Renderer<ToolBar>
    {
        /// <inheritdoc/>
        protected override void Render (ToolBar control, PaintEventArgs e)
        {
            RenderGrip (control, e);

            foreach (var item in control.Items) {
                // See ToolBar.LayoutItems: a hidden item has no box, so painting it drew it at
                // whatever bounds it last had (TSM-04).
                if (!item.Visible)
                    continue;

                if (item is MenuSeparatorItem msi)
                    RenderMenuSeparatorItem (control, msi, e);
                else
                    RenderItem (control, item, e);
            }
        }

        /// <summary>
        /// Renders the strip's drag grip into the band <see cref="ToolBar.GripBandWidth"/> reserved.
        /// </summary>
        /// <remarks>
        /// Drawn as the dotted vertical rule a toolbar grip conventionally is. The band's width comes
        /// from the control, not from a constant here, so the space layout kept free and the space
        /// painted are the same number (TSM-43).
        /// </remarks>
        protected virtual void RenderGrip (ToolBar control, PaintEventArgs e)
        {
            var band = control.GripBandWidth;

            if (band <= 0)
                return;

            // Every number here is LOGICAL, because this canvas is: RenderItem fills item.Bounds --
            // which are logical -- directly. Converting the offsets to device while measuring them
            // from a logical edge is TSM-41's bug, and writing it here made the grip drift out of its
            // own band and vanish entirely at MF_HEADLESS_SCALE=2.
            var margin = control is ToolStrip strip ? strip.GripMargin : new Padding (2);
            var bounds = control.GripBandBounds;
            var x = bounds.Left + margin.Left + ToolBar.GripRuleWidth / 2;
            var top = bounds.Top + margin.Top + 2;
            var bottom = bounds.Bottom - margin.Bottom - 2;
            var step = 3;
            var dot = 1;

            for (var y = top; y < bottom; y += step)
                e.Canvas.FillRectangle (new Rectangle (x, y, dot, dot), Theme.BorderMidColor);
        }

        /// <summary>
        /// Renders a MenuItem.
        /// </summary>
        protected virtual void RenderItem (ToolBar control, MenuItem item, PaintEventArgs e)
        {
            // An item hosting a real control draws nothing of its own: the control is a child of the strip
            // and paints itself over this rectangle. Painting the item's own background and text as well
            // shows through any hosted control with a transparent background.
            if (item is ToolStripControlHost)
                return;

            // Background. A checked ToolStripButton draws with the pressed background, which is the
            // only way the user can see which mode a toggle button is in (TSM-06); hover still wins so
            // the item reacts under the pointer.
            var item_style = item.Hovered || item.IsDropDownOpened || item.Checked ? ToolBar.DefaultItemHoverStyle : ToolBar.DefaultItemStyle;
            var background_color = item_style.TryGetBackgroundColor () ?? control.GetEffectiveBackgroundColor ();
            e.Canvas.FillRectangle (item.Bounds, background_color);

            // A ToolStripLabel with IsLink draws as a hyperlink: the link colours and the underline
            // were all stored and read by nothing, so `new ToolStripLabel { IsLink = true }` was
            // indistinguishable from a plain caption (TSM-42). Disabled still wins -- a link you
            // cannot click should not be advertising itself.
            var link = item.Enabled ? item as ToolStripLabel : null;
            var is_link = link is { IsLink: true };

            var font_color = !item.Enabled ? Theme.ForegroundDisabledColor
                : is_link ? LinkColour (link!)
                : item_style.GetForegroundColor ();
            var font_size = e.LogicalToDeviceUnits (Theme.FontSize);
            var pad = e.LogicalToDeviceUnits (8);

            // ToolStripItem carries the image/text placement knobs; a plain MenuItem keeps the
            // historical image-left/text-right arrangement.
            var strip_item = item as ToolStripItem;
            var relation = strip_item?.TextImageRelation ?? TextImageRelation.ImageBeforeText;
            var text_align = strip_item?.TextAlign ?? ContentAlignment.MiddleLeft;

            // Content box: inside the horizontal padding, less the dropdown arrow's gutter so text
            // never runs underneath the glyph.
            var arrow_gutter = item.HasItems ? e.LogicalToDeviceUnits (16) + 4 : 0;
            var content = new Rectangle (
                item.Bounds.Left + pad,
                item.Bounds.Top,
                Math.Max (0, item.Bounds.Width - (pad * 2) - arrow_gutter),
                item.Bounds.Height);

            var image = item.ImageSK;
            var image_size = Size.Empty;

            if (image != null) {
                // ImageScaling.None means "draw it at its own size" -- the whole point of assigning a
                // large glyph to a large button. Anything else gets the standard strip icon box.
                // ToolStrip.ImageScalingSize is the box a scaled item image is drawn in. It was
                // stored and read by nothing, so every icon was drawn at a hard-coded 20 and a strip
                // that asked for 24px or 32px icons got 20px ones (TSM-44). Upstream's default is
                // 16x16, which is what the property already declares.
                var box = (control as ToolStrip)?.ImageScalingSize ?? new Size (20, 20);

                image_size = strip_item?.ImageScaling == ToolStripItemImageScaling.None
                    ? new Size (image.Width, image.Height)
                    : new Size (e.LogicalToDeviceUnits (box.Width), e.LogicalToDeviceUnits (box.Height));

                // Never overflow the content box, however big the source bitmap is.
                image_size.Width = Math.Min (image_size.Width, content.Width);
                image_size.Height = Math.Min (image_size.Height, content.Height);
            }

            var text_size = Size.Empty;

            if (!string.IsNullOrEmpty (item.Text)) {
                var measured = TextMeasurer.MeasureText (item.Text, Theme.UIFont, font_size);
                text_size = new Size ((int) Math.Ceiling (measured.Width), (int) Math.Ceiling (measured.Height));
            }

            var image_rect = Rectangle.Empty;
            var text_rect = content;
            var gap = image_size.IsEmpty || text_size.IsEmpty ? 0 : e.LogicalToDeviceUnits (4);

            switch (relation) {
                case TextImageRelation.ImageAboveText:
                case TextImageRelation.TextAboveImage: {
                    // Stack them and centre the pair vertically; each half centres horizontally.
                    var stack = image_size.Height + gap + text_size.Height;
                    var top = content.Top + Math.Max (0, (content.Height - stack) / 2);
                    var image_top = relation == TextImageRelation.ImageAboveText ? top : top + text_size.Height + gap;
                    var text_top = relation == TextImageRelation.ImageAboveText ? top + image_size.Height + gap : top;

                    if (!image_size.IsEmpty)
                        image_rect = new Rectangle (
                            content.Left + Math.Max (0, (content.Width - image_size.Width) / 2),
                            image_top, image_size.Width, image_size.Height);

                    text_rect = new Rectangle (content.Left, text_top, content.Width, Math.Max (0, text_size.Height));
                    break;
                }

                case TextImageRelation.TextBeforeImage: {
                    if (!image_size.IsEmpty)
                        image_rect = new Rectangle (
                            content.Right - image_size.Width,
                            content.Top + Math.Max (0, (content.Height - image_size.Height) / 2),
                            image_size.Width, image_size.Height);

                    text_rect = new Rectangle (content.Left, content.Top,
                        Math.Max (0, content.Width - image_size.Width - gap), content.Height);
                    break;
                }

                case TextImageRelation.Overlay: {
                    if (!image_size.IsEmpty)
                        image_rect = new Rectangle (
                            content.Left + Math.Max (0, (content.Width - image_size.Width) / 2),
                            content.Top + Math.Max (0, (content.Height - image_size.Height) / 2),
                            image_size.Width, image_size.Height);
                    break;
                }

                default: {
                    // ImageBeforeText: image against the leading edge, text to its right.
                    if (!image_size.IsEmpty)
                        image_rect = new Rectangle (
                            content.Left,
                            content.Top + Math.Max (0, (content.Height - image_size.Height) / 2),
                            image_size.Width, image_size.Height);

                    var offset = image_size.IsEmpty ? e.LogicalToDeviceUnits (4) : image_size.Width + gap;
                    text_rect = new Rectangle (content.Left + offset, content.Top,
                        Math.Max (0, content.Width - offset), content.Height);
                    break;
                }
            }

            if (image is not null && !image_rect.IsEmpty)
                e.Canvas.DrawBitmap (image, image_rect, !item.Enabled);

            if (!string.IsNullOrEmpty (item.Text)) {
                e.Canvas.DrawText (item.Text, Theme.UIFont, font_size, text_rect, font_color, text_align);

                // Drawn as a line under the text rather than as a font style, matching
                // LinkLabelRenderer -- the two link surfaces should not underline differently.
                if (is_link && LinkRendering.ShouldUnderline (link!.LinkBehavior, item.Hovered) && !text_size.IsEmpty) {
                    var run = LinkRendering.UnderlineRun (text_rect, text_size, text_align);
                    var thickness = Math.Max (1, e.LogicalToDeviceUnits (1));
                    var y = Math.Min (text_rect.Bottom, run.Y) - thickness;

                    e.Canvas.DrawLine (run.X, y, run.X + run.Width, y, font_color, thickness);
                }
            }

            // Dropdown Arrow. ToolStripDropDownButton.ShowDropDownArrow turns it off -- a toolbar
            // button that opens a menu but is drawn as a plain button, which is how icon-only
            // "more actions" buttons are usually styled. It was stored and read by nothing, so the
            // arrow was drawn whenever the item had a submenu whatever the property said.
            if (item.HasItems && ShowsDropDownArrow (item)) {
                var arrow_bounds = DrawingExtensions.CenterSquare (item.Bounds, 16);
                var arrow_area = new Rectangle (item.Bounds.Right - e.LogicalToDeviceUnits (16) - 4, arrow_bounds.Top, 16, 16);
                ControlPaint.DrawArrowGlyph (e, arrow_area, font_color, ArrowDirection.Down);
            }
        }

        // LinkVisited picks the visited colour. ActiveLinkColor -- the colour WinForms uses while the
        // link is held down -- is deliberately NOT read: nothing in this layer tracks a pressed strip
        // item (MenuBase handles MouseMove and MouseLeave and no button state at all), so there is no
        // moment at which it could apply. Wiring it would be an unverifiable claim; it stays in the
        // baseline with that reason recorded in TSM-42.
        private static SkiaSharp.SKColor LinkColour (ToolStripLabel link)
            => (link.LinkVisited ? link.VisitedLinkColor : link.LinkColor).ToSKColor ();

        // Only ToolStripDropDownButton carries the flag; every other item type draws its arrow
        // whenever it has a submenu, which is what upstream does too.
        private static bool ShowsDropDownArrow (MenuItem item)
            => item is not ToolStripDropDownButton button || button.ShowDropDownArrow;

        /// <summary>
        /// Renders a MenuSeparatorItem.
        /// </summary>
        protected virtual void RenderMenuSeparatorItem (ToolBar control, MenuSeparatorItem item, PaintEventArgs e)
        {
            // Background
            e.Canvas.FillRectangle (item.Bounds, control.GetEffectiveBackgroundColor ());

            var center = item.Bounds.GetCenter ();
            var thickness = e.LogicalToDeviceUnits (1);
            var padding = e.LogicalToDeviceUnits (item.Padding);

            e.Canvas.DrawLine (center.X, item.Bounds.Top + padding.Top + thickness, center.X, item.Bounds.Bottom - padding.Bottom - thickness, item.Enabled ? Theme.ControlHighlightLowColor : Theme.ForegroundDisabledColor, thickness);
        }

        /// <summary>
        /// Gets the preferred size of a MenuItem.
        /// </summary>
        public virtual Size GetPreferredItemSize (ToolBar control, MenuItem item, Size proposedSize)
        {
            if (item is MenuSeparatorItem msi)
                return GetPreferredSeparatorItemSize (control, msi, proposedSize);

            var font_size = control.LogicalToDeviceUnits (Theme.FontSize);
            var measured = TextMeasurer.MeasureText (item.Text, Theme.UIFont, font_size);
            var text_width = (int) Math.Round (measured.Width);
            var text_height = (int) Math.Ceiling (measured.Height);

            var strip_item = item as ToolStripItem;
            var image_size = Size.Empty;

            if (item.ImageSK is not null)
                // Match RenderItem: an unscaled image occupies its natural size, not the 20px box.
                image_size = strip_item?.ImageScaling == ToolStripItemImageScaling.None
                    ? new Size (item.ImageSK.Width, item.ImageSK.Height)
                    : new Size (control.LogicalToDeviceUnits (20), control.LogicalToDeviceUnits (20));

            var stacked = strip_item?.TextImageRelation is TextImageRelation.ImageAboveText
                                                         or TextImageRelation.TextAboveImage;

            var width = control.LogicalToDeviceUnits (item.Padding.Horizontal);
            int height;

            if (stacked) {
                // Side by side in neither axis: as wide as the wider half, as tall as both plus a gap.
                width += Math.Max (text_width, image_size.Width);
                height = image_size.Height + text_height
                       + (image_size.IsEmpty || text_height == 0 ? 0 : control.LogicalToDeviceUnits (4));
            } else {
                width += text_width + image_size.Width;
                height = Math.Max (image_size.Height, text_height);
            }

            if (item.HasItems)
                width += control.LogicalToDeviceUnits (14);

            // Height was previously the item's current box, which made a strip's preferred height
            // depend on whatever it had already been given rather than on its content.
            return new Size (width, Math.Max (height + control.LogicalToDeviceUnits (item.Padding.Vertical), item.Bounds.Height));
        }

        /// <summary>
        /// Gets the preferred size of a MenuSeparatorItem.
        /// </summary>
        protected virtual Size GetPreferredSeparatorItemSize (ToolBar control, MenuSeparatorItem item, Size proposedSize)
        {
            var padding = control.LogicalToDeviceUnits (item.Padding.Horizontal);
            var thickness = control.LogicalToDeviceUnits (1);

            return new Size (thickness + padding, item.Bounds.Height);
        }
    }
}
