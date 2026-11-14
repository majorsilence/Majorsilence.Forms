using System;
using System.Collections.Generic;

namespace Majorsilence.Forms
{
    /// <summary>
    /// Converts between the UTF-16 indices a <see cref="TextBox"/>'s text is addressed by and the code-point indices of
    /// the RichTextKit <c>TextBlock</c> laid out from it (#689).
    /// </summary>
    /// <remarks>
    /// RichTextKit's <c>Utf32Buffer</c> folds each surrogate pair, and each CR LF, into one code point, so every index
    /// past one is smaller there than in the string by the number folded before it. Handing one kind of index to an API
    /// of the other drifts a character per CRLF above the position. The units that are folded away -- a pair's low
    /// surrogate, a CRLF's LF -- are recorded once per layout, and an index converts by counting them.
    /// </remarks>
    internal sealed class TextIndexMap
    {
        /// <summary>The map of a text with nothing folded: both kinds of index are the same.</summary>
        internal static readonly TextIndexMap Identity = new TextIndexMap (Array.Empty<int> ());

        // The UTF-16 index of each unit folded into the one before it, ascending.
        private readonly int[] folded;

        // For each folded unit, the code-point index of the code point it was folded into: ascending, so ToUtf16 can
        // binary-search it.
        private readonly int[] folded_into;

        private TextIndexMap (int[] folded)
        {
            this.folded = folded;
            folded_into = new int[folded.Length];

            // The folded unit at position j has j folded units before it, so it sits j code points behind its UTF-16 index,
            // less the one it is part of.
            for (var j = 0; j < folded.Length; j++)
                folded_into[j] = folded[j] - j - 1;
        }

        /// <summary>Maps <paramref name="text"/> as RichTextKit lays it out in one <c>AddText</c>.</summary>
        /// <param name="text">The text laid out.</param>
        /// <param name="foldLineEnds">Whether a CRLF is one code point. A single line shows each of its line ends as a mark
        /// of its own before the text reaches RichTextKit (see TextMeasurer.OneLine), so there it is two.</param>
        internal static TextIndexMap For (string text, bool foldLineEnds = true)
        {
            List<int>? folded = null;

            for (var i = 0; i < text.Length - 1; i++) {
                var c = text[i];

                if ((foldLineEnds && c == '\r' && text[i + 1] == '\n') || (char.IsHighSurrogate (c) && char.IsLowSurrogate (text[i + 1]))) {
                    (folded ??= new List<int> ()).Add (i + 1);
                    i++;
                }
            }

            return folded is null ? Identity : new TextIndexMap (folded.ToArray ());
        }

        /// <summary>The code-point index of the character at the UTF-16 <paramref name="index"/>; an index inside a folded
        /// pair is the pair's start.</summary>
        internal int ToCodePoint (int index)
        {
            if (folded.Length == 0)
                return index;

            // The number of folded units at or before index.
            var lo = 0;
            var hi = folded.Length;

            while (lo < hi) {
                var mid = lo + (hi - lo) / 2;

                if (folded[mid] <= index)
                    lo = mid + 1;
                else
                    hi = mid;
            }

            return index - lo;
        }

        /// <summary>The UTF-16 index of the code point at <paramref name="codePoint"/>.</summary>
        internal int ToUtf16 (int codePoint)
        {
            if (folded.Length == 0)
                return codePoint;

            // The number of folded pairs that start before codePoint.
            var lo = 0;
            var hi = folded_into.Length;

            while (lo < hi) {
                var mid = lo + (hi - lo) / 2;

                if (folded_into[mid] < codePoint)
                    lo = mid + 1;
                else
                    hi = mid;
            }

            return codePoint + lo;
        }
    }
}
