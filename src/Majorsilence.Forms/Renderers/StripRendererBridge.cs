using System.Drawing;
using SkiaSharp;

namespace Majorsilence.Forms.Renderers
{
    /// <summary>
    /// Routes each part the strip renderers paint through the strip's <see cref="ToolStripRenderer"/>
    /// first, so a custom renderer's <c>OnRender*</c> overrides run and its <c>Render*</c> events fire.
    /// </summary>
    /// <remarks>
    /// <para>TSM-48. <see cref="ToolStripRenderer"/> had every upstream <c>Draw*</c> method, each
    /// raising its event and calling its hook, and nothing in the assembly called any of them: the strip
    /// renderers painted directly, so <c>ToolStrip.Renderer</c>, <c>RenderMode</c>, a custom renderer
    /// subclass and all nineteen <c>Render*</c> events were inert together -- silently, since the
    /// subclass compiled and was assigned.</para>
    /// <para>The contract here differs from upstream's in one deliberate way. Upstream's BASE renderer
    /// draws text, arrows and images itself, and backgrounds come from the System/Professional
    /// subclasses; a custom renderer that does not call <c>base</c> gets no default painting. Here the
    /// built-in painting stays where it is and remains the default, and a custom renderer suppresses a
    /// part by setting <c>Handled</c> on the args -- a flag ADDED to this layer's item and arrow args
    /// for the purpose; upstream's carry none, and the first draft of this file wrongly said they
    /// did. Colour and rectangle changes an override makes (<c>TextColor</c>, <c>ArrowRectangle</c>...)
    /// are honoured by the default painting that follows, which is the common WinForms idiom of
    /// overriding <c>OnRenderItemText</c> to recolour and then calling <c>base</c>.</para>
    /// <para>Resolution follows upstream: an assigned <c>Renderer</c> wins; otherwise <c>RenderMode</c>
    /// picks the System or Professional shell, and <c>ManagerRenderMode</c> defers to
    /// <see cref="ToolStripManager"/>. Every strip therefore routes through some renderer, as
    /// upstream's do, and the events fire for all of them.</para>
    /// </remarks>
    internal static class StripRendererBridge
    {
        private static readonly ToolStripRenderer professional = new ToolStripProfessionalRenderer ();
        private static readonly ToolStripRenderer system = new ToolStripSystemRenderer ();

        internal static ToolStripRenderer? Resolve (ToolBar control)
        {
            if (control is not ToolStrip strip)
                return null;

            if (strip.Renderer is { } own)
                return own;

            return strip.RenderMode switch {
                ToolStripRenderMode.System => system,
                ToolStripRenderMode.Professional => professional,
                _ => ToolStripManager.Renderer ?? (ToolStripManager.RenderMode == ToolStripManagerRenderMode.System ? system : professional),
            };
        }

        // ---- strip-level parts. No Handled on these args, so the default painting always follows.

        internal static void Background (ToolBar control, PaintEventArgs e)
        {
            if (Resolve (control) is { } r && control is ToolStrip strip)
                r.DrawToolStripBackground (new ToolStripRenderEventArgs (e.Graphics, strip, control.ClientRectangle, control.GetEffectiveBackgroundColor ().ToDrawingColor ()));
        }

        internal static void Border (ToolBar control, PaintEventArgs e)
        {
            if (Resolve (control) is { } r && control is ToolStrip strip)
                r.DrawToolStripBorder (new ToolStripRenderEventArgs (e.Graphics, strip, control.ClientRectangle, control.GetEffectiveBackgroundColor ().ToDrawingColor ()));
        }

        internal static void Grip (ToolBar control, Rectangle deviceBand, PaintEventArgs e)
        {
            if (Resolve (control) is { } r && control is ToolStrip strip)
                r.DrawGrip (new ToolStripGripRenderEventArgs (e.Graphics, strip) { GripBounds = deviceBand, GripStyle = strip.GripStyle });
        }

        // ---- item backgrounds, dispatched on the item's type as upstream's items do in OnPaint.
        // Returns true when the renderer marked the part handled and the default fill must be skipped.

        internal static bool ItemBackground (ToolBar control, MenuItem item, PaintEventArgs e)
        {
            if (Resolve (control) is not { } r || item is not ToolStripItem strip_item)
                return false;

            var args = new ToolStripItemRenderEventArgs (e.Graphics, strip_item);

            switch (strip_item) {
                case ToolStripSplitButton: r.DrawSplitButton (args); break;
                case ToolStripDropDownButton: r.DrawDropDownButtonBackground (args); break;
                case ToolStripMenuItem when control is MenuDropDown: r.DrawMenuItemBackground (args); break;
                case ToolStripStatusLabel: r.DrawToolStripStatusLabelBackground (args); break;
                case ToolStripLabel: r.DrawLabelBackground (args); break;
                case ToolStripButton: r.DrawButtonBackground (args); break;
                default: r.DrawItemBackground (args); break;
            }

            return args.Handled;
        }

        internal static bool Image (ToolBar control, MenuItem item, Rectangle deviceRect, PaintEventArgs e)
        {
            if (Resolve (control) is not { } r || item is not ToolStripItem strip_item)
                return false;

            var args = item.Image is { } image
                ? new ToolStripItemImageRenderEventArgs (e.Graphics, strip_item, image, deviceRect)
                : new ToolStripItemImageRenderEventArgs (e.Graphics, strip_item, deviceRect);

            r.DrawItemImage (args);
            return args.Handled;
        }

        internal static bool Check (ToolBar control, MenuItem item, Rectangle deviceRect, PaintEventArgs e)
        {
            if (Resolve (control) is not { } r || item is not ToolStripItem strip_item)
                return false;

            var args = new ToolStripItemImageRenderEventArgs (e.Graphics, strip_item, deviceRect);
            r.DrawItemCheck (args);
            return args.Handled;
        }

        internal static void ImageMargin (ToolBar control, Rectangle deviceMargin, PaintEventArgs e)
        {
            if (Resolve (control) is { } r && control is ToolStrip strip)
                r.DrawImageMargin (new ToolStripRenderEventArgs (e.Graphics, strip, deviceMargin, control.GetEffectiveBackgroundColor ().ToDrawingColor ()));
        }

        /// <summary>The text parts after the renderer has seen them; null when handled and nothing should be drawn.</summary>
        internal static (string text, Rectangle rect, SKColor colour)? Text (ToolBar control, MenuItem item, string text, Rectangle deviceRect, SKColor colour, PaintEventArgs e)
        {
            if (Resolve (control) is not { } r || item is not ToolStripItem strip_item)
                return (text, deviceRect, colour);

            var args = new ToolStripItemTextRenderEventArgs (e.Graphics, strip_item, text, deviceRect,
                colour.ToDrawingColor (), strip_item.Font ?? Control.DefaultFont, TextFormatFlags.Default);

            r.DrawItemText (args);

            return args.Handled ? null : (args.Text, args.TextRectangle, args.TextColor.ToSKColor ());
        }

        /// <summary>The arrow parts after the renderer has seen them; null when handled.</summary>
        internal static (Rectangle rect, SKColor colour, ArrowDirection direction)? Arrow (ToolBar control, MenuItem item, Rectangle deviceRect, SKColor colour, ArrowDirection direction, PaintEventArgs e)
        {
            if (Resolve (control) is not { } r || item is not ToolStripItem strip_item)
                return (deviceRect, colour, direction);

            var args = new ToolStripArrowRenderEventArgs (e.Graphics, strip_item, deviceRect, colour.ToDrawingColor (), direction);
            r.DrawArrow (args);

            return args.Handled ? null : (args.ArrowRectangle, args.ArrowColor.ToSKColor (), args.Direction);
        }

        internal static bool Separator (ToolBar control, ToolStripSeparator separator, bool vertical, PaintEventArgs e)
        {
            if (Resolve (control) is not { } r)
                return false;

            var args = new ToolStripSeparatorRenderEventArgs (e.Graphics, separator, vertical);
            r.DrawSeparator (args);
            return false; // separator args carry no Handled
        }
    }
}
