using System.Drawing;
using SkiaSharp;
using Topten.RichTextKit;

namespace Majorsilence.Forms
{
    /// <summary>
    /// A collection of extension methods to facilitate text drawing operations.
    /// </summary>
    public static class SkiaTextExtensions
    {
        private static readonly TextPaintOptions _defaultPaintOptions = new TextPaintOptions { Edging = SKFontEdging.SubpixelAntialias };

        /// <summary>
        /// Computes the Y coordinate at which a text block of the given measured height should be
        /// painted so that it is vertically aligned within <paramref name="bounds"/>, never pushing
        /// the top of the text above <paramref name="bounds"/>.Top.
        ///
        /// The clamp matters when the text's natural line height exceeds the control height: a common
        /// WinForms designer default is a control a couple of pixels shorter than its font's line box
        /// (e.g. a 12pt selected item in a ~28px DropDownList), and RichTextKit can report a more
        /// generous, leading-inclusive line height than GDI. Without the clamp, centering (or
        /// bottom-aligning) produces a negative offset that paints the glyph caps above the top edge,
        /// where canvas.Clip(bounds) shaves them off — the visible symptom being a selected item whose
        /// letters are sliced along the top. Real WinForms/GDI+ never top-clips: it keeps the caps and
        /// lets the descenders overflow the bottom. Clamping to bounds.Top reproduces that — the text
        /// top-aligns when it cannot fit, so the caps stay fully visible and only the bottom overflows
        /// (and is clipped, as before).
        /// </summary>
        internal static int VerticalTextOrigin (SKTextAlign vertical, Rectangle bounds, int measuredHeight)
        {
            // NOTE: GetVerticalAlign maps Top→Left, Center→Center, Bottom→Right (see TextMeasurer).
            if (vertical == SKTextAlign.Right)                                   // bottom-aligned
                return System.Math.Max (bounds.Top, bounds.Bottom - measuredHeight);
            if (vertical == SKTextAlign.Center)                                 // middle-aligned
                return bounds.Top + System.Math.Max (0, (bounds.Height - measuredHeight) / 2);
            return bounds.Top;                                                  // top-aligned
        }

        /// <summary>
        /// Draws a string of text.
        /// </summary>
        public static void DrawText (this SKCanvas canvas, string text, Rectangle bounds, Control control, ContentAlignment alignment, int selectionStart = -1, int selectionEnd = -1, SKColor? selectionColor = null, int? maxLines = null, bool ellipsis = false)
            => canvas.DrawText (text, control.CurrentStyle.GetFont (), control.LogicalToDeviceUnits (control.CurrentStyle.GetFontSize ()), bounds, control.Enabled ? control.CurrentStyle.GetForegroundColor () : Theme.ForegroundDisabledColor, alignment, selectionStart, selectionEnd, selectionColor, maxLines, ellipsis);

        /// <summary>
        /// Draws a string of text.
        /// </summary>
        public static void DrawText (this SKCanvas canvas, string text, SKTypeface font, int fontSize, Rectangle bounds, SKColor color, ContentAlignment alignment, int selectionStart = -1, int selectionEnd = -1, SKColor? selectionColor = null, int? maxLines = null, bool ellipsis = false)
        {
            if (string.IsNullOrWhiteSpace (text))
                return;

            // Layout height is deliberately unconstrained (bounds.Height is not passed through): a
            // control shorter than one line's natural height (a common WinForms designer default --
            // an 8.25pt-font Label is typically 13px tall, a couple pixels short of that font's own
            // line height) must still draw its text, matching real WinForms/GDI+ (which draws and
            // lets it overflow slightly rather than refusing to draw at all). RichTextKit's TextBlock
            // treats MaxHeight as a hard layout budget: if even the first line doesn't fit, it lays
            // out zero lines rather than the first line and would-be overflow, so passing the
            // control's real (short) height here silently produced completely invisible text. The
            // canvas.Clip(bounds) call below still constrains what's actually visible to bounds, so
            // nothing paints outside the control regardless.
            var tb = TextMeasurer.CreateTextBlock (text, font, fontSize, new Size (bounds.Width, int.MaxValue), TextMeasurer.GetTextAlign (alignment), color, maxLines, ellipsis);
            var location = bounds.Location;
            var vertical = TextMeasurer.GetVerticalAlign (alignment);

            location.Y = VerticalTextOrigin (vertical, bounds, (int)tb.MeasuredHeight);

            TextPaintOptions options;
            if (selectionStart >= 0 && selectionEnd >= 0 && selectionStart != selectionEnd) {
                options = new TextPaintOptions {
                    Edging = SKFontEdging.SubpixelAntialias,
                    Selection = new TextRange (selectionStart, selectionEnd),
                    SelectionColor = selectionColor ?? Theme.TextSelectionBackgroundColor
                };
            } else {
                options = _defaultPaintOptions;
            }

            canvas.Save ();
            canvas.Clip (bounds);
            tb.Paint (canvas, new SKPoint (location.X, location.Y), options);
            canvas.Restore ();
        }

        /// <summary>
        /// Draws a string of text, interpreting WinForms mnemonic prefixes, resolving the font and
        /// colour from the control (matching the control-based <see cref="DrawText(SKCanvas, string, Rectangle, Control, ContentAlignment, int, int, SKColor?, int?, bool)"/> overload).
        /// </summary>
        public static void DrawMnemonicText (this SKCanvas canvas, string text, Rectangle bounds, Control control, ContentAlignment alignment, int? maxLines = null, bool ellipsis = false)
            => canvas.DrawMnemonicText (text, control.CurrentStyle.GetFont (), control.LogicalToDeviceUnits (control.CurrentStyle.GetFontSize ()), bounds, control.Enabled ? control.CurrentStyle.GetForegroundColor () : Theme.ForegroundDisabledColor, alignment, maxLines, ellipsis);

        /// <summary>
        /// Draws a string of text, interpreting WinForms mnemonic prefixes: an ampersand marks the
        /// following character as the access key (drawn underlined), and a doubled ampersand is a
        /// literal ampersand.
        /// </summary>
        public static void DrawMnemonicText (this SKCanvas canvas, string text, SKTypeface font, int fontSize, Rectangle bounds, SKColor color, ContentAlignment alignment, int? maxLines = null, bool ellipsis = false)
        {
            if (string.IsNullOrWhiteSpace (text))
                return;

            var display = Mnemonics.Parse (text, out var mnemonicIndex);

            if (string.IsNullOrEmpty (display))
                return;

            // See the matching comment in DrawText above: height is deliberately unconstrained so a
            // control shorter than one line's natural text height still draws (clipped by
            // canvas.Clip(bounds) below, not by refusing to lay out any text at all).
            var tb = TextMeasurer.CreateTextBlock (display, font, fontSize, new Size (bounds.Width, int.MaxValue), TextMeasurer.GetTextAlign (alignment), color, maxLines, ellipsis, mnemonicIndex);
            var location = bounds.Location;
            var vertical = TextMeasurer.GetVerticalAlign (alignment);

            location.Y = VerticalTextOrigin (vertical, bounds, (int)tb.MeasuredHeight);

            canvas.Save ();
            canvas.Clip (bounds);
            tb.Paint (canvas, new SKPoint (location.X, location.Y), _defaultPaintOptions);
            canvas.Restore ();
        }

        /// <summary>
        /// Draws a block of text.
        /// </summary>
        public static void DrawTextBlock (this SKCanvas canvas, TextBlock block, Point location, TextSelection selection)
        {
            TextPaintOptions options;

            if (!selection.IsEmpty ()) {
                options = new TextPaintOptions {
                    Edging = SKFontEdging.SubpixelAntialias,
                    Selection = new TextRange (selection.Start, selection.End),
                    SelectionColor = selection.Color
                };
            } else {
                options = _defaultPaintOptions;
            }

            block.Paint (canvas, new SKPoint (location.X, location.Y), options);
        }

        /// <summary>
        /// Draws a single line of text.
        /// </summary>
        public static void DrawTextLine (this SKCanvas canvas, string text, Rectangle bounds, Control control, ContentAlignment alignment, bool ellipsis = false)
            => canvas.DrawText (text, control.CurrentStyle.GetFont (), control.LogicalToDeviceUnits (control.CurrentStyle.GetFontSize ()), bounds, control.Enabled ? control.CurrentStyle.GetForegroundColor () : Theme.ForegroundDisabledColor, alignment, maxLines: 1, ellipsis: ellipsis);
    }
}
