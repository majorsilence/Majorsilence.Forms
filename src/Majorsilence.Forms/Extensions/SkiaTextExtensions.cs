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
        /// <summary>
        /// Narrows a wrapping text box to the whole lines that fit, as upstream's
        /// <c>TextFormatFlags.TextBoxControl</c> does: <c>ControlPaint.CreateTextFormatFlags</c> ORs
        /// <c>WordBreak | TextBoxControl</c> for Label and the whole button family, so a last line that would
        /// only be partly visible is not drawn and the lines that remain are aligned as the control asks.
        /// Returns the box and an alignment re-pinned to its top row, ready for the clip in the draw call to
        /// hide the rest. Text that fits, or a control shorter than a single line (which keeps the
        /// top-clamped path, see <see cref="VerticalTextOrigin"/>), comes back unchanged.
        /// </summary>
        internal static (Rectangle Bounds, ContentAlignment Alignment) WholeLineBox (string text, SKTypeface font, int fontSize, Rectangle bounds, ContentAlignment alignment)
        {
            var wrapped = TextMeasurer.MeasureText (Mnemonics.Strip (text), font, fontSize, new Size (bounds.Width, int.MaxValue)).Height;
            var line = TextMeasurer.MeasureText ("X", font, fontSize).Height;
            var whole_lines = line > 0 ? (int) (bounds.Height / line) : 0;

            if (wrapped <= bounds.Height || whole_lines < 1)
                return (bounds, alignment);

            var visible = (int) (whole_lines * line);
            var top = TextMeasurer.GetVerticalAlign (alignment) switch {
                SKTextAlign.Left => bounds.Top,
                SKTextAlign.Right => bounds.Bottom - visible,
                _ => bounds.Top + ((bounds.Height - visible) / 2),
            };

            var top_row = alignment switch {
                ContentAlignment.MiddleLeft or ContentAlignment.BottomLeft => ContentAlignment.TopLeft,
                ContentAlignment.MiddleRight or ContentAlignment.BottomRight => ContentAlignment.TopRight,
                ContentAlignment.MiddleCenter or ContentAlignment.BottomCenter => ContentAlignment.TopCenter,
                _ => alignment,
            };

            return (new Rectangle (bounds.X, top, bounds.Width, visible), top_row);
        }

        /// <summary>A control's text box narrowed to whole lines, using the control's effective font.</summary>
        internal static (Rectangle Bounds, ContentAlignment Alignment) WholeLineBox (Control control, string text, Rectangle bounds, ContentAlignment alignment)
            => WholeLineBox (text, control.GetEffectiveFont (), control.LogicalToDeviceUnits (control.GetEffectiveFontSize ()), bounds, alignment);

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
        /// <remarks>The control font's Underline and Strikeout are drawn, as upstream draws a control's text in
        /// an underlined or struck-out font.</remarks>
        public static void DrawText (this SKCanvas canvas, string text, Rectangle bounds, Control control, ContentAlignment alignment, int selectionStart = -1, int selectionEnd = -1, SKColor? selectionColor = null, int? maxLines = null, bool ellipsis = false)
            => DrawText (canvas, text, control.GetEffectiveFont (), control.LogicalToDeviceUnits (control.GetEffectiveFontSize ()), bounds, control.Enabled ? control.GetEffectiveForegroundColor () : Theme.ForegroundDisabledColor, alignment, selectionStart, selectionEnd, selectionColor, maxLines, ellipsis, paintOptions: null, TextMeasurer.DecorationOf (control.Font));

        /// <summary>
        /// Draws a string of text.
        /// </summary>
        public static void DrawText (this SKCanvas canvas, string text, SKTypeface font, int fontSize, Rectangle bounds, SKColor color, ContentAlignment alignment, int selectionStart = -1, int selectionEnd = -1, SKColor? selectionColor = null, int? maxLines = null, bool ellipsis = false)
            => DrawText (canvas, text, font, fontSize, bounds, color, alignment, selectionStart, selectionEnd, selectionColor, maxLines, ellipsis, paintOptions: null);

        /// <summary>
        /// Draws a string of text with caller-chosen glyph rasterisation; null means the library default.
        /// <c>Graphics.TextRenderingHint</c> reaches the text through here (GFX-08).
        /// </summary>
        internal static void DrawText (SKCanvas canvas, string text, SKTypeface font, int fontSize, Rectangle bounds, SKColor color, ContentAlignment alignment, int selectionStart, int selectionEnd, SKColor? selectionColor, int? maxLines, bool ellipsis, TextPaintOptions? paintOptions, int decoration = 0)
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
            var tb = TextMeasurer.CreateTextBlock (text, font, fontSize, new Size (bounds.Width, int.MaxValue), TextMeasurer.GetTextAlign (alignment), color, maxLines, ellipsis,
                rasterisation: paintOptions is null ? -1 : ((int)paintOptions.Edging << 4) | (int)paintOptions.Hinting, decoration: decoration);
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
                options = paintOptions ?? _defaultPaintOptions;
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
        /// <remarks>The access key is underlined only while the control's form shows keyboard cues
        /// (<see cref="Control.ShowKeyboardCues"/>).</remarks>
        public static void DrawMnemonicText (this SKCanvas canvas, string text, Rectangle bounds, Control control, ContentAlignment alignment, int? maxLines = null, bool ellipsis = false)
            => canvas.DrawMnemonicText (text, control.GetEffectiveFont (), control.LogicalToDeviceUnits (control.GetEffectiveFontSize ()), bounds, control.Enabled ? control.GetEffectiveForegroundColor () : Theme.ForegroundDisabledColor, alignment, maxLines, ellipsis, control.ShowKeyboardCues, TextMeasurer.DecorationOf (control.Font));

        /// <summary>
        /// Draws a string of text, interpreting WinForms mnemonic prefixes: an ampersand marks the
        /// following character as the access key (drawn underlined), and a doubled ampersand is a
        /// literal ampersand.
        /// </summary>
        public static void DrawMnemonicText (this SKCanvas canvas, string text, SKTypeface font, int fontSize, Rectangle bounds, SKColor color, ContentAlignment alignment, int? maxLines = null, bool ellipsis = false)
            => canvas.DrawMnemonicText (text, font, fontSize, bounds, color, alignment, maxLines, ellipsis, underline: true);

        // As above; with underline false the access key is drawn like the rest of the text, as upstream
        // draws it while keyboard cues are hidden. The ampersands are still interpreted.
        internal static void DrawMnemonicText (this SKCanvas canvas, string text, SKTypeface font, int fontSize, Rectangle bounds, SKColor color, ContentAlignment alignment, int? maxLines, bool ellipsis, bool underline, int decoration = 0)
        {
            if (string.IsNullOrWhiteSpace (text))
                return;

            var display = Mnemonics.Parse (text, out var mnemonicIndex);

            if (!underline)
                mnemonicIndex = -1;

            if (string.IsNullOrEmpty (display))
                return;

            // See the matching comment in DrawText above: height is deliberately unconstrained so a
            // control shorter than one line's natural text height still draws (clipped by
            // canvas.Clip(bounds) below, not by refusing to lay out any text at all).
            var tb = TextMeasurer.CreateTextBlock (display, font, fontSize, new Size (bounds.Width, int.MaxValue), TextMeasurer.GetTextAlign (alignment), color, maxLines, ellipsis, mnemonicIndex, decoration: decoration);
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

            if (!selection.IsEmpty () && selection.TextColor is { } text_colour)
                RepaintSelectedText (canvas, block, location, selection, text_colour);
        }

        // Draws the selected text again in the selection's text colour, clipped to the selection, as upstream's edit
        // control draws selected text in HighlightText: on a Highlight background the text's own (dark) colour all but
        // vanished. The block is repainted through a colour filter, so each glyph keeps its antialiasing.
        private static void RepaintSelectedText (SKCanvas canvas, TextBlock block, Point location, TextSelection selection, SKColor colour)
        {
            var start = Math.Min (selection.Start, selection.End);
            var end = Math.Max (selection.Start, selection.End);
            using var region = new SKPath ();

            foreach (var line in block.Lines) {
                var line_start = line.Start;
                var line_end = line.Start + line.Length;

                if (line_end <= start || line_start >= end)
                    continue;

                var from = block.GetCaretInfo (new CaretPosition (Math.Max (start, line_start))).CaretXCoord;
                var to = block.GetCaretInfo (new CaretPosition (Math.Min (end, line_end))).CaretXCoord;

                if (to < from)
                    (from, to) = (to, from);

                region.AddRect (new SKRect (location.X + from, location.Y + line.YCoord, location.X + to, location.Y + line.YCoord + line.Height));
            }

            if (region.IsEmpty)
                return;

            using var paint = new SKPaint { ColorFilter = SKColorFilter.CreateBlendMode (colour, SKBlendMode.SrcIn) };

            canvas.Save ();
            canvas.ClipPath (region, antialias: false);
            canvas.SaveLayer (paint);
            block.Paint (canvas, new SKPoint (location.X, location.Y), _defaultPaintOptions);
            canvas.Restore ();
            canvas.Restore ();
        }

        /// <summary>
        /// Draws a single line of text.
        /// </summary>
        public static void DrawTextLine (this SKCanvas canvas, string text, Rectangle bounds, Control control, ContentAlignment alignment, bool ellipsis = false)
            => canvas.DrawText (text, control.GetEffectiveFont (), control.LogicalToDeviceUnits (control.GetEffectiveFontSize ()), bounds, control.Enabled ? control.GetEffectiveForegroundColor () : Theme.ForegroundDisabledColor, alignment, maxLines: 1, ellipsis: ellipsis);
    }
}
