using System;
using System.Collections.Generic;
using System.Drawing;
using SkiaSharp;
using Topten.RichTextKit;

namespace Majorsilence.Forms
{
    /// <summary>
    /// A text box's text laid out one paragraph at a time, so that an edit lays out only the paragraphs it touches (#690).
    /// </summary>
    /// <remarks>
    /// <para>A single <c>TextBlock</c> over the whole document is thrown away by every edit: shaping, line breaking and the
    /// colorizer over all of it, about 0.4 s a keystroke in 450,000 characters. Here each paragraph -- the text up to and
    /// including its line feed -- has a block of its own, and the next layout keeps every block whose text (and, with a
    /// colorizer, spans) is unchanged. An edit that stays in one paragraph lays out that one.</para>
    /// <para>A block is addressed by code point, the layout by code point over the whole text: a piece starts at the sum of
    /// the code points before it. A paragraph ends in its line feed (a CRLF is one code point, and never straddles two
    /// pieces), which RichTextKit lays out as the end of a line and not as a line of its own, so the lines, heights and
    /// carets of the pieces add up to those of one block over the same text; the tests compare the two.</para>
    /// <para>A box that is not multiline is one piece, which is the old single block.</para>
    /// </remarks>
    internal sealed class TextBoxLayout
    {
        /// <summary>Everything a piece is laid out from other than its own text and spans. A layout is only reused for pieces
        /// laid out from the same key.</summary>
        internal readonly record struct Key (int FontSize, SKTypeface? Typeface, Size MaxSize, TextAlignment Alignment, SKColor Color,
                                            bool Multiline, bool Gdi, bool Placeholder, bool Colorized);

        private readonly string source;
        private readonly TextBlock[] blocks;
        private readonly TextSpanStyle[]?[] spans;      // what each piece was laid out with, relative to its start
        private readonly int[] starts;                  // UTF-16 index of each piece, then the text's length
        private readonly int[] cp_starts;               // code point of each piece, then the total
        private readonly float[] tops;                  // y of each piece, then the height of the lot
        private readonly int[] first_lines;             // the first line of each piece, then the count

        private TextBoxLayout (string source, Key key, TextBlock[] blocks, TextSpanStyle[]?[] spans, int[] starts, TextIndexMap index_map)
        {
            this.source = source;
            this.blocks = blocks;
            this.spans = spans;
            this.starts = starts;
            LaidOutFrom = key;
            IndexMap = index_map;

            var count = blocks.Length;

            cp_starts = new int[count + 1];
            tops = new float[count + 1];
            first_lines = new int[count + 1];

            for (var i = 0; i < count; i++) {
                var block = blocks[i];

                cp_starts[i] = index_map.ToCodePoint (starts[i]);
                tops[i + 1] = tops[i] + block.MeasuredHeight;
                first_lines[i + 1] = first_lines[i] + block.Lines.Count;
                MeasuredWidth = Math.Max (MeasuredWidth, block.MeasuredWidth);
            }

            cp_starts[count] = index_map.ToCodePoint (starts[count]);
        }

        internal Key LaidOutFrom { get; }

        /// <summary>Converts between the text's UTF-16 indices and the code points the blocks count.</summary>
        internal TextIndexMap IndexMap { get; }

        internal float MeasuredWidth { get; }

        internal float MeasuredHeight => tops[blocks.Length];

        internal int LineCount => first_lines[blocks.Length];

        internal int PieceCount => blocks.Length;

        /// <summary>The block of the only piece: the whole text's, in a layout of one.</summary>
        internal TextBlock FirstBlock => blocks[0];

        /// <summary>The block of the whole text, once something asked for it; see TextBoxDocument.GetTextBlock.</summary>
        internal TextBlock? WholeBlock { get; set; }

        /// <summary>
        /// Lays <paramref name="text"/> out, keeping the blocks of <paramref name="previous"/> that still fit.
        /// </summary>
        /// <param name="text">What to lay out.</param>
        /// <param name="key">What the pieces are laid out from.</param>
        /// <param name="by_paragraph">Whether to cut the text into paragraphs, or lay it out as one piece.</param>
        /// <param name="styles">The colorizer's spans, valid and in order, or null with none.</param>
        /// <param name="previous">The last layout, whose blocks are reused where the text and spans match.</param>
        /// <param name="lay_out">Lays out one piece from its text and its spans (relative to it).</param>
        /// <param name="laid_out">Counts the pieces laid out.</param>
        internal static TextBoxLayout Build (string text, Key key, bool by_paragraph, IReadOnlyList<TextSpanStyle>? styles, TextBoxLayout? previous,
                                             Func<string, TextSpanStyle[]?, TextBlock> lay_out, ref int laid_out)
        {
            var starts = Split (text, by_paragraph);
            var count = starts.Length - 1;
            var blocks = new TextBlock[count];
            var spans = new TextSpanStyle[]?[count];
            var reuse = previous is not null && previous.LaidOutFrom.Equals (key) ? previous : null;
            var first = 0;
            var end = count;

            if (reuse is not null) {
                var old_end = reuse.blocks.Length;

                // The pieces before the edit are the same ones as before...
                while (first < count && first < old_end && reuse.Matches (first, text, starts[first], starts[first + 1], styles)) {
                    blocks[first] = reuse.blocks[first];
                    spans[first] = reuse.spans[first];
                    first++;
                }

                // ... and so are the ones after it, which only moved.
                while (end > first && old_end > first && reuse.Matches (old_end - 1, text, starts[end - 1], starts[end], styles)) {
                    end--;
                    old_end--;
                    blocks[end] = reuse.blocks[old_end];
                    spans[end] = reuse.spans[old_end];
                }
            }

            for (var i = first; i < end; i++) {
                var piece = text.Substring (starts[i], starts[i + 1] - starts[i]);

                spans[i] = styles is null ? null : Clip (styles, starts[i], starts[i + 1]);
                blocks[i] = lay_out (piece, spans[i]);
                laid_out++;
            }

            return new TextBoxLayout (text, key, blocks, spans, starts, TextIndexMap.For (text, foldLineEnds: key.Multiline));
        }

        // The UTF-16 index each paragraph starts at, then the text's length. The text is cut after each line feed; a CR LF is
        // never cut, and a lone CR is left inside its piece for RichTextKit to break at as it would in the whole text.
        private static int[] Split (string text, bool by_paragraph)
        {
            var cuts = new List<int> { 0 };

            if (by_paragraph) {
                var at = 0;

                while ((at = text.IndexOf ('\n', at)) >= 0) {
                    at++;

                    // No empty piece for the end of a text that ends in a line feed: one block over it has no line after it.
                    if (at < text.Length)
                        cuts.Add (at);
                }
            }

            cuts.Add (text.Length);

            return cuts.ToArray ();
        }

        // Whether piece i of this layout was laid out from what the new text has at [start, end): the same characters, and
        // the same spans over them.
        private bool Matches (int i, string text, int start, int end, IReadOnlyList<TextSpanStyle>? styles)
        {
            var length = starts[i + 1] - starts[i];

            if (length != end - start || !source.AsSpan (starts[i], length).SequenceEqual (text.AsSpan (start, length)))
                return false;

            var old = spans[i];

            if (styles is null || old is null)
                return styles is null && old is null;

            var at = FirstSpanAfter (styles, start);
            var n = 0;

            for (; at < styles.Count && styles[at].Start < end; at++, n++)
                if (n >= old.Length || old[n] != Clipped (styles[at], start, end))
                    return false;

            return n == old.Length;
        }

        // The spans of [start, end), cut to it and made relative to start.
        private static TextSpanStyle[] Clip (IReadOnlyList<TextSpanStyle> styles, int start, int end)
        {
            var at = FirstSpanAfter (styles, start);
            var count = 0;

            for (var i = at; i < styles.Count && styles[i].Start < end; i++)
                count++;

            if (count == 0)
                return Array.Empty<TextSpanStyle> ();

            var clipped = new TextSpanStyle[count];

            for (var i = 0; i < count; i++)
                clipped[i] = Clipped (styles[at + i], start, end);

            return clipped;
        }

        private static TextSpanStyle Clipped (TextSpanStyle span, int start, int end)
        {
            var from = Math.Max (span.Start, start);
            var to = Math.Min (span.Start + span.Length, end);

            return span with { Start = from - start, Length = to - from };
        }

        // The index of the first span that ends after start. The spans are in order and do not overlap, so their ends are too.
        private static int FirstSpanAfter (IReadOnlyList<TextSpanStyle> styles, int start)
        {
            var lo = 0;
            var hi = styles.Count;

            while (lo < hi) {
                var mid = lo + (hi - lo) / 2;

                if (styles[mid].Start + styles[mid].Length <= start)
                    lo = mid + 1;
                else
                    hi = mid;
            }

            return lo;
        }

        // The piece a code point is in: the one that starts at or before it, so a code point at a break is at the start of the
        // next piece, as it is the start of the next line in one block. The end of the text is in the last piece.
        private int PieceOfCodePoint (int code_point)
        {
            var lo = 0;
            var hi = blocks.Length;

            while (hi - lo > 1) {
                var mid = lo + (hi - lo) / 2;

                if (cp_starts[mid] <= code_point)
                    lo = mid;
                else
                    hi = mid;
            }

            return lo;
        }

        private int PieceAtY (float y)
        {
            var lo = 0;
            var hi = blocks.Length;

            while (hi - lo > 1) {
                var mid = lo + (hi - lo) / 2;

                if (tops[mid] <= y)
                    lo = mid;
                else
                    hi = mid;
            }

            return lo;
        }

        /// <summary>The caret at a code point, with its rectangle and line in the layout's coordinates.</summary>
        internal CaretInfo GetCaretInfo (int code_point)
        {
            var piece = PieceOfCodePoint (code_point);
            var info = blocks[piece].GetCaretInfo (new CaretPosition (code_point - cp_starts[piece]));
            var rect = info.CaretRectangle;

            rect.Offset (0, tops[piece]);
            info.CaretRectangle = rect;
            info.CodePointIndex += cp_starts[piece];
            info.LineIndex += first_lines[piece];

            return info;
        }

        /// <summary>The cursor rectangle at a code point, in device pixels from <paramref name="location"/>.</summary>
        internal Rectangle GetCursorLocation (Point location, int code_point, int font_size, int empty_line_height)
        {
            // No text, so nothing to measure the cursor from.
            if (MeasuredHeight == 0)
                return TextMeasurer.EmptyCursor (location, font_size, empty_line_height);

            try {
                return TextMeasurer.CursorRectangle (GetCaretInfo (code_point).CaretRectangle, location);
            } catch (ArgumentOutOfRangeException) {
                return Rectangle.Empty;
            }
        }

        /// <summary>The code point nearest a point of the layout.</summary>
        internal int HitTest (float x, float y)
        {
            var piece = PieceAtY (y);

            return cp_starts[piece] + blocks[piece].HitTest (x, y - tops[piece]).ClosestCodePointIndex;
        }

        /// <summary>The first caret stop: the start of the text.</summary>
        internal int FirstStop => cp_starts[0] + (blocks[0].CaretIndicies.Count == 0 ? 0 : blocks[0].CaretIndicies[0]);

        /// <summary>The last caret stop: the end of the text.</summary>
        internal int LastStop {
            get {
                var last = blocks.Length - 1;
                var stops = blocks[last].CaretIndicies;

                return cp_starts[last] + (stops.Count == 0 ? 0 : stops[stops.Count - 1]);
            }
        }

        /// <summary>
        /// The caret stop one step from a code point: a CRLF, a surrogate pair and a combining sequence are each one step.
        /// A caret inside one (which SetSelection can leave it) steps to the stop that starts it going left, and to the next
        /// going right.
        /// </summary>
        internal int StepCaret (int code_point, int step)
        {
            var piece = PieceOfCodePoint (code_point);
            var stops = blocks[piece].CaretIndicies;

            if (stops.Count == 0)
                return code_point;

            var local = code_point - cp_starts[piece];
            var lo = 0;
            var hi = stops.Count;

            // The number of stops at or before the caret.
            while (lo < hi) {
                var mid = lo + (hi - lo) / 2;

                if (stops[mid] <= local)
                    lo = mid + 1;
                else
                    hi = mid;
            }

            var at = lo - 1;

            if (step > 0)
                return cp_starts[piece] + stops[Math.Min (at + 1, stops.Count - 1)];

            var target = at >= 0 && stops[at] == local ? at - 1 : at;

            if (target >= 0)
                return cp_starts[piece] + stops[target];

            // Before the first stop of a piece: the one before the break, which is the one before the previous piece's last
            // stop (that stop is this piece's start).
            if (piece == 0)
                return cp_starts[0] + stops[0];

            var before = blocks[piece - 1].CaretIndicies;

            return cp_starts[piece - 1] + before[Math.Max (before.Count - 2, 0)];
        }

        /// <summary>The line a code point is on; the last line for the end of the text.</summary>
        internal int GetLineFromCodePoint (int code_point)
        {
            var piece = PieceOfCodePoint (code_point);
            var lines = blocks[piece].Lines;
            var local = code_point - cp_starts[piece];

            for (var i = 0; i < lines.Count; i++)
                if (local < lines[i].Start + lines[i].Length)
                    return first_lines[piece] + i;

            return first_lines[piece] + Math.Max (lines.Count - 1, 0);
        }

        /// <summary>The code point a line starts at, or -1 past the last line.</summary>
        internal int GetLineStart (int line)
        {
            if (line < 0 || line >= LineCount)
                return -1;

            var lo = 0;
            var hi = blocks.Length;

            // The last piece whose first line is at or before this one.
            while (hi - lo > 1) {
                var mid = lo + (hi - lo) / 2;

                if (first_lines[mid] <= line)
                    lo = mid;
                else
                    hi = mid;
            }

            // A piece with no lines (an empty text) has none to be asked for: LineCount is 0 there.
            return cp_starts[lo] + blocks[lo].Lines[line - first_lines[lo]].Start;
        }

        /// <summary>Draws the pieces that are in view, with the part of the selection that falls in each.</summary>
        /// <param name="canvas">Where to draw; its clip decides which pieces are in view.</param>
        /// <param name="origin">Where the layout's top left is on the canvas.</param>
        /// <param name="selection">The selection, in code points of the whole text.</param>
        internal void Paint (SKCanvas canvas, Point origin, TextSelection selection)
        {
            var clip = canvas.LocalClipBounds;
            var from = PieceAtY (clip.Top - origin.Y);
            var sel_start = Math.Min (selection.Start, selection.End);
            var sel_end = Math.Max (selection.Start, selection.End);

            for (var i = from; i < blocks.Length; i++) {
                // Past the bottom of what is shown; the pieces after it are lower still.
                if (origin.Y + tops[i] > clip.Bottom)
                    break;

                var local = TextSelection.Empty;

                if (!selection.IsEmpty ()) {
                    var lo = Math.Max (sel_start - cp_starts[i], 0);
                    var hi = Math.Min (sel_end - cp_starts[i], cp_starts[i + 1] - cp_starts[i]);

                    if (hi > lo)
                        local = new TextSelection (lo, hi, selection.Color) { TextColor = selection.TextColor };
                }

                canvas.DrawTextBlock (blocks[i], new SKPoint (origin.X, origin.Y + tops[i]), local);
            }
        }
    }
}
