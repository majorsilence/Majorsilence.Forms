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
            // TSM-48: every part goes through the strip's ToolStripRenderer first. See
            // StripRendererBridge for the contract.
            StripRendererBridge.Background (control, e);

            // The legacy bar's Divider: a rule along the top edge (W6 mechanisms).
            if (control.LegacyChrome && control.Divider) {
                var client = control.ClientRectangle;
                e.Canvas.DrawLine (client.Left, client.Top, client.Right, client.Top, Theme.BorderLowColor, Math.Max (1, e.LogicalToDeviceUnits (1)));
            }

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

            StripRendererBridge.Border (control, e);
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

            StripRendererBridge.Grip (control, control.LogicalToDeviceUnits (new Rectangle (control.GripBandBounds.Left, control.GripBandBounds.Top, band, control.GripBandBounds.Height)), e);

            // DEVICE throughout, like the rest of this canvas. GripBandBounds is logical because
            // LayoutItems measures against it, so it is converted here -- the one place that needs the
            // other space. (This block was written in logical units when TSM-41 was believed to say
            // the canvas was logical; it agreed with the items only because they were wrong the same
            // way.)
            var margin = control is ToolStrip strip ? strip.GripMargin : new Padding (2);
            var bounds = control.GripBandBounds;
            var x = e.LogicalToDeviceUnits (bounds.Left + margin.Left + ToolBar.GripRuleWidth / 2);
            var top = e.LogicalToDeviceUnits (bounds.Top + margin.Top + 2);
            var bottom = e.LogicalToDeviceUnits (bounds.Bottom - margin.Bottom - 2);
            var step = Math.Max (2, e.LogicalToDeviceUnits (3));
            var dot = Math.Max (1, e.LogicalToDeviceUnits (1));

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

            // A partially pushed legacy button (PartialPush: an indeterminate toggle) shows a lighter
            // pressed state than a pushed one (W6 mechanisms).
            var legacy = control.ButtonFor (item);

            if (legacy is { PartialPush: true } && !item.Checked && !item.Hovered)
                background_color = Theme.ControlMidColor;
            // The renderer sees the item first; Handled means it painted the background itself.
            if (!StripRendererBridge.ItemBackground (control, item, e))
                e.Canvas.FillRectangle (item.DeviceBounds, background_color);

            // ToolBarAppearance.Normal: each button carries a raised 3D border; Flat carries none.
            if (legacy is not null && control.LegacyChrome && control.Appearance == ToolBarAppearance.Normal)
                RenderRaisedBorder (item.DeviceBounds, e, sunken: item.Checked || legacy.PartialPush);

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
                item.DeviceBounds.Left + pad,
                item.DeviceBounds.Top,
                Math.Max (0, item.DeviceBounds.Width - (pad * 2) - arrow_gutter),
                item.DeviceBounds.Height);

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

            if (image is not null && !image_rect.IsEmpty && !StripRendererBridge.Image (control, item, image_rect, e))
                e.Canvas.DrawBitmap (image, image_rect, !item.Enabled);

            // The renderer may recolour, move or take over the text. Null back means it took over.
            var text_parts = string.IsNullOrEmpty (item.Text) ? null
                : StripRendererBridge.Text (control, item, item.Text, text_rect, font_color, e);

            if (text_parts is { } tp) {
                text_rect = tp.rect;
                font_color = tp.colour;
                e.Canvas.DrawText (tp.text, Theme.UIFont, font_size, text_rect, font_color, text_align);

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
            // A legacy DropDownButton draws its arrow when the bar's DropDownArrows says so (W6).
            var legacy_arrow = legacy is { Style: ToolBarButtonStyle.DropDownButton } && control.DropDownArrows;

            if ((item.HasItems && ShowsDropDownArrow (item)) || legacy_arrow) {
                var arrow_bounds = DrawingExtensions.CenterSquare (item.DeviceBounds, 16);
                var arrow_area = new Rectangle (item.DeviceBounds.Right - e.LogicalToDeviceUnits (16) - 4, arrow_bounds.Top, 16, 16);

                if (StripRendererBridge.Arrow (control, item, arrow_area, font_color, ArrowDirection.Down, e) is { } ap)
                    ControlPaint.DrawArrowGlyph (e, ap.rect, ap.colour, ap.direction);
            }
        }

        // The 3D edge of a ToolBarAppearance.Normal button: light top-left and dark bottom-right when
        // raised, the reverse when pushed.
        private static void RenderRaisedBorder (Rectangle bounds, PaintEventArgs e, bool sunken)
        {
            var top_left = sunken ? Theme.BorderMidColor : Theme.ControlHighColor;
            var bottom_right = sunken ? Theme.ControlHighColor : Theme.BorderMidColor;

            e.Canvas.DrawLine (bounds.Left, bounds.Top, bounds.Right - 1, bounds.Top, top_left);
            e.Canvas.DrawLine (bounds.Left, bounds.Top, bounds.Left, bounds.Bottom - 1, top_left);
            e.Canvas.DrawLine (bounds.Left, bounds.Bottom - 1, bounds.Right - 1, bounds.Bottom - 1, bottom_right);
            e.Canvas.DrawLine (bounds.Right - 1, bounds.Top, bounds.Right - 1, bounds.Bottom - 1, bottom_right);
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
            e.Canvas.FillRectangle (item.DeviceBounds, control.GetEffectiveBackgroundColor ());

            var center = item.DeviceBounds.GetCenter ();
            var thickness = e.LogicalToDeviceUnits (1);
            var padding = e.LogicalToDeviceUnits (item.Padding);

            e.Canvas.DrawLine (center.X, item.DeviceBounds.Top + padding.Top + thickness, center.X, item.DeviceBounds.Bottom - padding.Bottom - thickness, item.Enabled ? Theme.ControlHighlightLowColor : Theme.ForegroundDisabledColor, thickness);
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

            if (item.ImageSK is not null) {
                // Match RenderItem exactly: an unscaled image occupies its natural size, and a scaled
                // one the strip's ImageScalingSize. This read a hard-coded 20 while RenderItem was
                // changed to honour the property (TSM-44), so measure and paint disagreed about how
                // much room an icon needs -- a 32px strip measured items as though its icons were 20.
                var box = (control as ToolStrip)?.ImageScalingSize ?? new Size (20, 20);

                image_size = strip_item?.ImageScaling == ToolStripItemImageScaling.None
                    ? new Size (item.ImageSK.Width, item.ImageSK.Height)
                    : new Size (control.LogicalToDeviceUnits (box.Width), control.LogicalToDeviceUnits (box.Height));
            }

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
            var size = new Size (width, Math.Max (height + control.LogicalToDeviceUnits (item.Padding.Vertical), item.DeviceBounds.Height));

            // A legacy button is never smaller than the bar's ButtonSize, and reserves its arrow gutter (W6).
            if (control.ButtonFor (item) is { } legacy) {
                var minimum = control.LogicalToDeviceUnits (control.ButtonSize);
                var arrow = legacy.Style == ToolBarButtonStyle.DropDownButton && control.DropDownArrows ? control.LogicalToDeviceUnits (16) : 0;

                size = new Size (Math.Max (size.Width + arrow, minimum.Width), Math.Max (size.Height, minimum.Height));
            }

            return size;
        }

        /// <summary>
        /// Gets the preferred size of a MenuSeparatorItem.
        /// </summary>
        protected virtual Size GetPreferredSeparatorItemSize (ToolBar control, MenuSeparatorItem item, Size proposedSize)
        {
            var padding = control.LogicalToDeviceUnits (item.Padding.Horizontal);
            var thickness = control.LogicalToDeviceUnits (1);

            return new Size (thickness + padding, item.DeviceBounds.Height);
        }
    }
}
