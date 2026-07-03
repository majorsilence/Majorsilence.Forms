using System.Drawing;
using Majorsilence.Forms.SpellCheck;
using SkiaSharp;
using Topten.RichTextKit;

namespace Majorsilence.Forms.Renderers
{
    /// <summary>
    /// Represents a class that can render a TextBox.
    /// </summary>
    public class TextBoxRenderer : Renderer<TextBox>
    {
        // Wavy underline geometry, in logical pixels - a shallow sine-like zig-zag reminiscent of the
        // squiggle most word processors and browsers use for spelling errors.
        private const float SquiggleAmplitude = 1.5f;
        private const float SquigglePeriod = 4f;
        private static readonly SKColor SquiggleColor = new SKColor (0xE8, 0x1E, 0x1E);

        /// <inheritdoc/>
        protected override void Render (TextBox control, PaintEventArgs e)
        {
            var text = control.Text.Length > 0 ? control.Text : control.Placeholder;

            // Bail early if we don't need to draw anything
            if (text.Length == 0 && !control.Selected)
                return;

            var block = GetTextBlock (control);

            UpdateScrollBars (control, block);

            e.Canvas.Save ();
            e.Canvas.Clip (control.PaddedClientRectangle);

            if (text.Length > 0)
                e.Canvas.DrawTextBlock (block, GetTextOrigin (control), GetTextSelection (control));

            // Only tokenizes/looks up misspellings when a checker is attached to this control (the
            // attachment dictionary lookup is the only cost otherwise) - see TextBoxSpellCheck.
            if (control.Text.Length > 0 && TextBoxSpellCheck.GetSpellChecker (control) != null)
                DrawMisspellingSquiggles (control, block, e.Canvas);

            if (control.Selected) {
                var caret = TextMeasurer.GetCursorLocation (block, GetTextOrigin (control), GetCursorIndex (control), GetCurrentFontSize (control));
                e.Canvas.DrawRectangle (caret, Theme.ForegroundColor);
            }

            e.Canvas.Restore ();
        }

        // Draws a wavy red underline beneath each misspelled word currently in the TextBox's text.
        private static void DrawMisspellingSquiggles (TextBox control, TextBlock block, SKCanvas canvas)
        {
            var ranges = TextBoxSpellCheck.GetMisspelledRanges (control);

            if (ranges.Count == 0)
                return;

            var origin = control.TextOrigin;

            foreach (var range in ranges) {
                Rectangle startRect, endRect;

                try {
                    startRect = TextMeasurer.GetCursorLocation (block, origin, range.Start, control.CurrentFontSize);
                    endRect = TextMeasurer.GetCursorLocation (block, origin, range.End, control.CurrentFontSize);
                } catch (ArgumentOutOfRangeException) {
                    continue;
                }

                // A word that wraps across lines would have a start Y above the end Y; skip drawing a
                // (visually wrong) single straight span in that rare case rather than drawing garbage.
                if (startRect.IsEmpty || endRect.IsEmpty || startRect.Top != endRect.Top)
                    continue;

                var left = startRect.Left;
                var right = endRect.Left;

                if (right <= left)
                    continue;

                var baseline = startRect.Bottom;

                using var path = BuildSquigglePath (left, right, baseline);
                canvas.DrawPath (path, SquiggleColor, thickness: 1);
            }
        }

        // Builds a horizontal zig-zag SKPath between x=left and x=right at the given baseline y.
        private static SKPath BuildSquigglePath (float left, float right, float baseline)
        {
            var path = new SKPath ();
            path.MoveTo (left, baseline);

            var x = left;
            var up = true;

            while (x < right) {
                var nextX = Math.Min (x + SquigglePeriod, right);
                var y = baseline + (up ? -SquiggleAmplitude : SquiggleAmplitude);

                path.LineTo (nextX, y);

                x = nextX;
                up = !up;
            }

            return path;
        }

        /// <summary>
        /// Gets the TextBox's font size.
        /// </summary>
        protected int GetCurrentFontSize (TextBox control) => control.CurrentFontSize;

        /// <summary>
        /// Gets the current index of the TextBox cursor.
        /// </summary>
        protected int GetCursorIndex (TextBox control) => control.document.CursorIndex;

        /// <summary>
        /// Gets the TextBox's text block.
        /// </summary>
        protected TextBlock GetTextBlock (TextBox control) => control.document.GetTextBlock ();

        /// <summary>
        /// Gets the TextBox's text origin.
        /// </summary>
        protected Point GetTextOrigin (TextBox control) => control.TextOrigin;

        /// <summary>
        /// Gets the TextBox's text seleection.
        /// </summary>
        protected TextSelection GetTextSelection (TextBox control) => control.document.GetTextSelection ();

        /// <summary>
        /// Updates the TextBox's scroll bars.
        /// </summary>
        protected void UpdateScrollBars (TextBox control, TextBlock block) => control.UpdateScrollBars (block);
    }
}
