using System.Drawing;
using SkiaSharp;

namespace Majorsilence.Forms.Renderers
{
    /// <summary>
    /// Represents a class that can render a Menu.
    /// </summary>
    public class MenuRenderer : Renderer<Menu>
    {
        /// <inheritdoc/>
        protected override void Render (Menu control, PaintEventArgs e)
        {
            foreach (var item in control.Items) {
                if (!item.Visible)
                    continue;

                if (item is MenuSeparatorItem msi)
                    RenderMenuSeparatorItem (control, msi, e);
                else if (item is ToolStripSeparator tss) {
                    // See ToolBarRenderer.Render: a ToolStripSeparator on a MenuStrip was a blank
                    // item that highlighted on hover (TSM-23).
                    StripRendererBridge.Separator (control, tss, vertical: true, e);
                    RenderMenuSeparatorItem (control, tss, e);
                } else if (item is MdiControlItem mdi)
                    RenderMdiControlItem (control, mdi, e);
                else
                    RenderItem (control, item, e);
            }
        }

        // A maximized MDI child's icon or caption button, merged into the menu bar. The glyphs are drawn
        // in the menu's text colour: ControlPaint's are white, for an accent-coloured caption.
        private static void RenderMdiControlItem (Menu control, MdiControlItem item, PaintEventArgs e)
        {
            var hovered = item.Hovered || item.IsDropDownOpened;
            var item_style = hovered ? Menu.DefaultItemHoverStyle : Menu.DefaultItemStyle;
            e.Canvas.FillRectangle (item.DeviceBounds, item_style.TryGetBackgroundColor () ?? control.GetEffectiveBackgroundColor ());

            var bounds = item.DeviceBounds;

            if (item.ControlKind == MdiControlItem.Kind.System) {
                var image = item.Child.Image ?? item.Child.MdiParent?.Image;
                if (image?.ToSKBitmap () is { } bitmap) {
                    using (bitmap) {
                        // The small icon, as upstream's caption shows it: an .ico decodes to its largest frame.
                        var size = Math.Min (e.LogicalToDeviceUnits (16), Math.Min (bounds.Width, bounds.Height));
                        var x = bounds.X + (bounds.Width - size) / 2;
                        var y = bounds.Y + (bounds.Height - size) / 2;
                        using var small = bitmap.Resize (new SKSizeI (size, size), new SKSamplingOptions (SKCubicResampler.Mitchell));
                        if (small is not null)
                            e.Canvas.DrawBitmap (small, x, y);
                    }
                }
                return;
            }

            if (control.UpstreamFont is not null) {
                DrawUpstreamCaptionGlyph (e, item.ControlKind, bounds, item_style.GetForegroundColor ());
                return;
            }

            var glyph = e.LogicalToDeviceUnits (10);
            var box = new Rectangle (bounds.X + (bounds.Width - glyph) / 2, bounds.Y + (bounds.Height - glyph) / 2, glyph, glyph);
            var color = item_style.GetForegroundColor ();

            switch (item.ControlKind) {
                case MdiControlItem.Kind.Minimize:
                    e.Canvas.DrawLine (box.X, box.Bottom - 1, box.Right, box.Bottom - 1, color);
                    break;
                case MdiControlItem.Kind.Restore:
                    var offset = e.LogicalToDeviceUnits (2);
                    e.Canvas.DrawRectangle (new Rectangle (box.X, box.Y + offset, box.Width - offset, box.Height - offset), color);
                    e.Canvas.DrawLine (box.X + offset, box.Y + offset, box.X + offset, box.Y, color);
                    e.Canvas.DrawLine (box.X + offset, box.Y, box.Right, box.Y, color);
                    e.Canvas.DrawLine (box.Right, box.Y, box.Right, box.Bottom - offset, color);
                    e.Canvas.DrawLine (box.Right, box.Bottom - offset, box.Right - offset, box.Bottom - offset, color);
                    break;
                case MdiControlItem.Kind.Close:
                    e.Canvas.DrawLine (box.X, box.Y, box.Right, box.Bottom, color);
                    e.Canvas.DrawLine (box.X, box.Bottom, box.Right, box.Y, color);
                    break;
            }
        }

        // Upstream's caption glyphs in a 24px cell, as WinForms draws them on a maximized MDI child's merged
        // buttons (measured from it): a 7 x 2 bar; two boxes with 2px tops, the front one 8px wide over
        // the back one; an X of 2px strokes, 10px square. Solid pixels in the text colour -- the 1px
        // outlines drawn for the theme read as a lighter, different set beside WinForms'.
        private static void DrawUpstreamCaptionGlyph (PaintEventArgs e, MdiControlItem.Kind kind, Rectangle cell, SKColor color)
        {
            var px = (int) Math.Max (1, Math.Round (e.Scaling));
            var ox = cell.X + (cell.Width - 24 * px) / 2;
            var oy = cell.Y + (cell.Height - 24 * px) / 2;

            using var paint = new SKPaint { Color = color };

            // A run of whole cell pixels: x, y, width, height in the 24px cell.
            void Fill (int x, int y, int w, int h) => e.Canvas.DrawRect (SKRect.Create (ox + x * px, oy + y * px, w * px, h * px), paint);

            switch (kind) {
                case MdiControlItem.Kind.Minimize:
                    Fill (8, 15, 7, 2);
                    break;

                case MdiControlItem.Kind.Restore:
                    // Back window: its top and right edge, and the corners that show past the front one.
                    Fill (9, 6, 9, 2);
                    Fill (17, 8, 1, 5);
                    Fill (9, 8, 1, 2);
                    Fill (15, 12, 2, 1);

                    // Front window.
                    Fill (7, 10, 8, 2);
                    Fill (7, 12, 1, 5);
                    Fill (14, 12, 1, 5);
                    Fill (7, 16, 8, 1);
                    break;

                case MdiControlItem.Kind.Close:
                    for (var i = 0; i < 10; i++) {
                        Fill (8 + i, 7 + i, 2, 1);
                        Fill (16 - i, 7 + i, 2, 1);
                    }
                    break;
            }
        }

        /// <summary>
        /// Renders a MenuItem.
        /// </summary>
        protected virtual void RenderItem (Menu control, MenuItem item, PaintEventArgs e)
        {
            // Background
            var item_style = item.Hovered || item.IsDropDownOpened ? Menu.DefaultItemHoverStyle : Menu.DefaultItemStyle;
            var background_color = item_style.TryGetBackgroundColor () ?? control.GetEffectiveBackgroundColor ();
            e.Canvas.FillRectangle (item.DeviceBounds, background_color);

            // Text
            var font_color = item.Enabled ? item_style.GetForegroundColor () : Theme.ForegroundDisabledColor;
            var font_size = e.LogicalToDeviceUnits (control.ItemFontSize);

            e.Canvas.DrawMnemonicText (item.Text, control.ItemTypeface, font_size, item.DeviceBounds, font_color, ContentAlignment.MiddleCenter, maxLines: null, ellipsis: false, underline: control.ShowKeyboardCues);
        }

        /// <summary>
        /// Renders a MenuSeparatorItem.
        /// </summary>
        protected virtual void RenderMenuSeparatorItem (Menu control, MenuSeparatorItem item, PaintEventArgs e)
            => DrawSeparatorRule (control, item, e);

        /// <summary>
        /// Renders a ToolStripSeparator as the same vertical rule a MenuSeparatorItem draws.
        /// </summary>
        protected virtual void RenderMenuSeparatorItem (Menu control, ToolStripSeparator item, PaintEventArgs e)
            => DrawSeparatorRule (control, item, e);

        private static void DrawSeparatorRule (Menu control, MenuItem item, PaintEventArgs e)
        {
            // Background
            e.Canvas.FillRectangle (item.DeviceBounds, control.GetEffectiveBackgroundColor ());

            var center = item.DeviceBounds.GetCenter ();
            var thickness = e.LogicalToDeviceUnits (1);
            var padding = e.LogicalToDeviceUnits (item.Padding);

            e.Canvas.DrawLine (center.X, item.DeviceBounds.Top + padding.Top, center.X, item.DeviceBounds.Bottom - padding.Bottom, item.Enabled ? Theme.ControlHighlightLowColor : Theme.ForegroundDisabledColor, thickness);
        }

        /// <summary>
        /// Gets the preferred size of a MenuItem.
        /// </summary>
        public virtual Size GetPreferredItemSize (Menu control, MenuItem item, Size proposedSize)
        {
            if (item is MenuSeparatorItem msi)
                return GetPreferredSeparatorItemSize (control, msi, proposedSize);

            if (item is ToolStripSeparator tss)
                return GetPreferredSeparatorItemSize (control, tss, proposedSize);

            if (item is MdiControlItem mdi)
                return new Size (control.LogicalToDeviceUnits (mdi.LogicalWidth), item.DeviceBounds.Height);

            // Upstream's metrics (Menu.UpstreamFont): the text as GDI measures it -- with its padding of
            // a sixth of the line height on each side -- inside the item padding and a 2px border.
            if (control.UpstreamFont is { } font) {
                var measured = TextMeasurer.MeasureText (Mnemonics.Strip (item.Text), control.ItemTypeface, control.LogicalToDeviceUnits (control.ItemFontSize));
                var gdi_padding = 2 * (int) Math.Ceiling (font.Height / 6.0);
                var extra = control.LogicalToDeviceUnits (gdi_padding + item.Padding.Horizontal + 4);

                return new Size ((int) Math.Ceiling (measured.Width) + extra, item.DeviceBounds.Height);
            }

            var padding = control.LogicalToDeviceUnits (item.Padding.Horizontal);
            var font_size = control.LogicalToDeviceUnits (Theme.FontSize);
            var text_size = (int)Math.Round (TextMeasurer.MeasureText (Mnemonics.Strip (item.Text), Theme.UIFont, font_size).Width);

            return new Size (text_size + padding, item.DeviceBounds.Height);
        }

        /// <summary>
        /// Gets the preferred size of a MenuSeparatorItem.
        /// </summary>
        protected virtual Size GetPreferredSeparatorItemSize (Menu control, MenuSeparatorItem item, Size proposedSize)
            => SeparatorSize (control, item);

        /// <summary>
        /// Gets the preferred size of a ToolStripSeparator: the rule plus its padding, as a MenuSeparatorItem.
        /// </summary>
        protected virtual Size GetPreferredSeparatorItemSize (Menu control, ToolStripSeparator item, Size proposedSize)
            => SeparatorSize (control, item);

        private static Size SeparatorSize (Menu control, MenuItem item)
        {
            var padding = control.LogicalToDeviceUnits (item.Padding.Horizontal);
            var thickness = control.LogicalToDeviceUnits (1);

            return new Size (thickness + padding, item.DeviceBounds.Height);
        }
    }
}
