using System;
using System.Collections.Generic;
using SkiaSharp;
using Topten.RichTextKit;
using Topten.RichTextKit.Utils;

namespace Majorsilence.Forms
{
    /// <summary>
    /// Appends a text's runs to a RichTextKit <see cref="TextBlock"/> in a way RichTextKit lays out in time that grows
    /// with the text's length, not with its square (#683).
    /// </summary>
    /// <remarks>
    /// <para>
    /// Three things in RichTextKit 0.4.167 make a long block quadratic (0.6.171 fixes the first; the others remain):
    /// </para>
    /// <list type="bullet">
    /// <item><c>Buffer&lt;T&gt;.GrowBuffer</c> computes a doubled capacity and then allocates exactly the length asked
    /// for, so every <c>AddText</c> copies the whole code-point buffer, and every shaping call copies the whole glyph
    /// buffers. A colorized document is thousands of runs.</item>
    /// <item>Adjacent runs that can share a shaping call are shaped as one font run and then split apart with
    /// <c>FontRun.Split</c>, which shifts every coordinate after the split point.</item>
    /// <item>Line breaking splits that font run again at every line, shifting the rest of the document each time.</item>
    /// </list>
    /// <para>
    /// A 3 MB report's source in a <see cref="RichTextBox"/> never finished laying out. So, for long text only, the
    /// buffers are sized once up front, and the text is cut into segments (each paragraph, and long unbroken stretches
    /// at a space) that RichTextKit cannot shape together: a split then only ever shifts the rest of one segment.
    /// What keeps two segments out of one shaping call is a <see cref="Style.LetterSpacing"/> that differs by
    /// <see cref="LetterSpacingNudge"/> on alternate segments, the one style property RichTextKit compares there that
    /// moves nothing a person can see (it is added to each glyph's advance). Short text, every label and button,
    /// is appended exactly as it always was.
    /// </para>
    /// </remarks>
    // A struct, passed by reference: every label's layout goes through one, and the allocation gate counts its bytes.
    internal struct TextBlockAppender
    {
        // Below this many characters the quadratic terms cost nothing measurable, and the text is laid out exactly as
        // it was before this class existed.
        internal const int SegmentingThreshold = 4096;

        // The longest stretch shaped as one run when a paragraph has no line end: it is cut after the first space past
        // this length. A paragraph with no space at all stays one run.
        internal const int MaxSegmentLength = 1024;

        // Added to alternate segments' letter spacing. A hundred-thousandth of a pixel per glyph is a hundredth of a
        // pixel across a thousand-character line; float resolves it for any spacing below a hundred pixels.
        internal const float LetterSpacingNudge = 1e-5f;

        private readonly TextBlock block;

        // UTF-16 offsets at which a new segment starts, ascending; null when the text is short enough not to segment.
        private readonly List<int>? boundaries;

        // The UTF-16 offset of the next character appended, and the index of the first boundary after it.
        private int position;
        private int next_boundary;

        // The last style varied and its variant: consecutive pieces of one run share a style.
        private Style? varied;
        private Style? variant;

        /// <summary>
        /// Starts appending <paramref name="text"/>, piece by piece and in order, to <paramref name="block"/>, which
        /// came from <see cref="CreateBlock"/> with the same text's length.
        /// </summary>
        internal TextBlockAppender (TextBlock block, string text)
        {
            this.block = block;
            boundaries = text.Length >= SegmentingThreshold ? FindBoundaries (text) : null;
        }

        /// <summary>
        /// A new block for a text of <paramref name="length"/> characters: for a long text, one whose buffers already
        /// hold all of it.
        /// </summary>
        internal static TextBlock CreateBlock (int length)
            => length >= SegmentingThreshold ? new PresizedTextBlock (length) : new TextBlock ();

        /// <summary>
        /// Appends the next <paramref name="text"/> of the text this appender was made for, in <paramref name="style"/>.
        /// </summary>
        internal void Add (ReadOnlySpan<char> text, Style style)
        {
            if (boundaries is null) {
                block.AddText (text, style);
                return;
            }

            while (text.Length > 0) {
                while (next_boundary < boundaries.Count && boundaries[next_boundary] <= position)
                    next_boundary++;

                // next_boundary is the number of boundaries at or before position: the segment's index.
                var end = next_boundary < boundaries.Count ? boundaries[next_boundary] : int.MaxValue;
                var length = (int)Math.Min (text.Length, (long)end - position);

                block.AddText (text.Slice (0, length), (next_boundary & 1) == 0 ? style : VariantOf (style));

                text = text.Slice (length);
                position += length;
            }
        }

        private Style VariantOf (Style style)
        {
            if (!ReferenceEquals (varied, style)) {
                varied = style;
                variant = style.Modify (letterSpacing: style.LetterSpacing + LetterSpacingNudge);
            }

            return variant!;
        }

        // Where segments start: after each line end, and after the first space once a stretch with no line end has run
        // past MaxSegmentLength. Never between CR and LF (RichTextKit folds a CRLF only within one AddText), and never
        // before a combining mark, which shapes with the character before it.
        internal static List<int> FindBoundaries (string text)
        {
            var result = new List<int> ();
            var start = 0;

            for (var i = 0; i < text.Length; i++) {
                var c = text[i];
                bool cut;

                if (c == '\n' || c == '\u2028' || c == '\u2029')
                    cut = true;
                else if (c == '\r')
                    cut = i + 1 >= text.Length || text[i + 1] != '\n';
                else
                    cut = (c == ' ' || c == '\t') && i - start >= MaxSegmentLength;

                if (cut && i + 1 < text.Length && !IsCombiningMark (text[i + 1])) {
                    result.Add (i + 1);
                    start = i + 1;
                }
            }

            return result;
        }

        private static bool IsCombiningMark (char c)
            => char.GetUnicodeCategory (c) is System.Globalization.UnicodeCategory.NonSpacingMark
                or System.Globalization.UnicodeCategory.SpacingCombiningMark
                or System.Globalization.UnicodeCategory.EnclosingMark;

        /// <summary>Whether RichTextKit's shaping buffers were found to size up front; see <c>ShapingBuffers</c>.</summary>
        internal static bool ShapingBuffersFound => ShapingBuffers.Found;

        // A block whose code-point buffer, and shaping buffers, are sized for its whole text before any of it is added,
        // so RichTextKit's exact-size growth never runs (see the class remarks).
        private sealed class PresizedTextBlock : TextBlock
        {
            internal PresizedTextBlock (int length)
            {
                // Each piece's code points are at most its UTF-16 length (a surrogate pair is one, a folded CRLF one),
                // so the whole text fits. Buffer.Clear keeps the array.
                _codePoints.Length = length;
                _codePoints.Clear ();

                ShapingBuffers.Presize (this, length);
            }
        }

        // RichTextKit's shaping buffers are private to TextBlock, of a type internal to it, so they are reached by name,
        // as GdiTextLayout reaches the shaper. Either missing (another RichTextKit) leaves them to grow as they did.
        private static class ShapingBuffers
        {
            private static readonly System.Reflection.FieldInfo? buffers =
                typeof (TextBlock).GetField ("_textShapingBuffers", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);

            [System.Diagnostics.CodeAnalysis.DynamicallyAccessedMembers (System.Diagnostics.CodeAnalysis.DynamicallyAccessedMemberTypes.PublicFields)]
            private static readonly Type? set_type = Type.GetType ("Topten.RichTextKit.TextShaper+ResultBufferSet, Topten.RichTextKit");

            private static readonly System.Reflection.FieldInfo? glyphs = set_type?.GetField ("GlyphIndicies");
            private static readonly System.Reflection.FieldInfo? positions = set_type?.GetField ("GlyphPositions");
            private static readonly System.Reflection.FieldInfo? clusters = set_type?.GetField ("Clusters");
            private static readonly System.Reflection.FieldInfo? x_coords = set_type?.GetField ("CodePointXCoords");

            // Whether every buffer was found, as the type Presize casts it to. Each one missing is skipped silently and
            // only brings back the slow growth, so a test asserts this rather than leave a RichTextKit upgrade that moved
            // them to show up as a timing failure.
            internal static bool Found
                => buffers?.FieldType == set_type
                    && glyphs?.FieldType == typeof (Buffer<ushort>)
                    && positions?.FieldType == typeof (Buffer<SKPoint>)
                    && clusters?.FieldType == typeof (Buffer<int>)
                    && x_coords?.FieldType == typeof (Buffer<float>);

            internal static void Presize (TextBlock block, int length)
            {
                if (buffers?.GetValue (block) is not { } set)
                    return;

                // A script that shapes a code point into more than one glyph (a split vowel) needs room past one glyph
                // per code point; a quarter more covers it, and running out only brings back the old growth.
                var glyph_count = length + length / 4 + 256;

                Presize (glyphs?.GetValue (set) as Buffer<ushort>, glyph_count);
                Presize (positions?.GetValue (set) as Buffer<SKPoint>, glyph_count);
                Presize (clusters?.GetValue (set) as Buffer<int>, glyph_count);
                Presize (x_coords?.GetValue (set) as Buffer<float>, length + 256);
            }

            private static void Presize<T> (Buffer<T>? buffer, int length)
            {
                if (buffer is null)
                    return;

                buffer.Length = length;
                buffer.Clear ();
            }
        }
    }
}
