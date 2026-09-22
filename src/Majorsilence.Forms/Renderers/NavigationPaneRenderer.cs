using System.Drawing;
using SkiaSharp;

namespace Majorsilence.Forms.Renderers;

/// <summary>
/// Represents a class that can render a NavigationPane.
/// </summary>
public class NavigationPaneRenderer : Renderer<NavigationPane>
{
    /// <inheritdoc/>
    protected override void Render (NavigationPane control, PaintEventArgs e)
    {
        foreach (var item in control.Items)
            RenderItem (control, item, e);
    }

    /// <summary>
    /// Renders a NavigationPaneItem.
    /// </summary>
    protected virtual void RenderItem (NavigationPane control, NavigationPaneItem item, PaintEventArgs e)
    {
        // NavigationPaneItem.Bounds is LOGICAL and this canvas is DEVICE -- the same boundary as
        // TabStripRenderer, and for the same reason: the item has no owner to convert through
        // (TSM-41).
        var bounds = control.LogicalToDeviceUnits (item.Bounds);

        if (item.Hovered && item.Enabled)
            e.Canvas.FillRectangle (bounds, Theme.ControlLowColor);

        // Draw focus rectangle
        if (control.Selected && control.ShowFocusCues && control.Items.FocusedIndex == control.Items.IndexOf (item))
            e.Canvas.DrawFocusRectangle (bounds, e.LogicalToDeviceUnits (1));

        var font_color = !item.Enabled ? Theme.ForegroundDisabledColor : Theme.ForegroundColor;
        var font = item.Selected || item.Hovered ? Theme.UIFontBold : Theme.UIFont;
        var font_size = e.LogicalToDeviceUnits (Theme.FontSize);

        e.Canvas.DrawText (item.Text, font, font_size, bounds, font_color, ContentAlignment.MiddleCenter);

        if (item.ImageSK is SKBitmap image) {
            var image_rect = bounds.CenterSquare (e.LogicalToDeviceUnits (20));
            e.Canvas.DrawBitmap (image, image_rect, !item.Enabled);
        }

        if (item.Selected) {
            var highlight_width = e.LogicalToDeviceUnits (2);
            var highlight_padding = e.LogicalToDeviceUnits (3);
            var highlight_bounds = new Rectangle (bounds.Left - highlight_width, bounds.Top + highlight_padding, highlight_width, bounds.Height - (2 * highlight_padding));

            e.Canvas.FillRectangle (highlight_bounds, Theme.AccentColor2);
        }
    }
}
