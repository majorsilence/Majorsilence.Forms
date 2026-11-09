using System.Collections.Concurrent;
using System.Drawing;
using SkiaSharp;
using Topten.RichTextKit;

namespace Majorsilence.Forms
{
    /// <summary>
    /// Provides functions for measuring text and related operations.
    /// </summary>
    public static class TextMeasurer
    {
        // Per typeface: the extra advance per glyph, per pixel of font size, that the font engine already gives a
        // SYNTHESIZED bold -- or null when the typeface is not one. Keyed by instance; typefaces are cached and reused.
        private static readonly System.Collections.Concurrent.ConcurrentDictionary<SKTypeface, float?> synthetic_bold = new ();

        /// <summary>
        /// The letter spacing, in pixels, that brings a SYNTHESIZED bold -- a bold asked of a family with no
        /// bold face (Microsoft Sans Serif, the classic designer font, is the common case) -- up to GDI's width.
        /// GDI widens every glyph of an emboldened face by about a pixel at UI sizes; DirectWrite and FreeType
        /// widen it by much less (about a third of a pixel at 16px). Designer layouts were measured in GDI, so a
        /// bold caption came out narrower than the space it was laid out for: a label that wraps to "Postal/Zip"
        /// upstream stayed on one line here and ran under the textbox placed after it. Only the difference is
        /// added, and a real bold face (its own font file) is left alone.
        /// </summary>
        internal static float GdiBoldSpacing (SKTypeface font, int fontSize)
        {
            if (fontSize <= 0 || synthetic_bold.GetOrAdd (font, EngineBoldAdvance) is not { } engine_per_px)
                return 0f;

            var gdi = Math.Max (1f, fontSize / 16f);
            return Math.Max (0f, gdi - (engine_per_px * fontSize));
        }

        // A bold typeface served from the same font file as its family's regular face is synthesized. For one,
        // returns how much wider than regular the engine already draws each glyph, per pixel of font size.
        private static float? EngineBoldAdvance (SKTypeface font)
        {
            if (font.FontWeight < (int) SKFontStyleWeight.SemiBold)
                return null;

            using var regular = SKFontManager.Default.MatchFamily (font.FamilyName, SKFontStyle.Normal);

            if (regular is null || regular.FontWeight >= (int) SKFontStyleWeight.SemiBold || !SameFontFile (font, regular))
                return null;

            const string sample = "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789";
            const float size = 64f;
            using var bold_font = new SKFont (font, size);
            using var regular_font = new SKFont (regular, size);

            return Math.Max (0f, bold_font.MeasureText (sample) - regular_font.MeasureText (sample)) / sample.Length / size;
        }

        private static bool SameFontFile (SKTypeface a, SKTypeface b)
        {
            using var sa = a.OpenStream (out var index_a);
            using var sb = b.OpenStream (out var index_b);

            if (sa is null || sb is null || index_a != index_b || sa.Length != sb.Length)
                return false;

            // Same length and collection index; compare the leading bytes (the table directory) to be sure.
            var head = Math.Min (sa.Length, 4096);
            var ba = new byte[head];
            var bb = new byte[head];
            sa.Read (ba, head);
            sb.Read (bb, head);

            return ba.AsSpan ().SequenceEqual (bb);
        }

        // Every TextBlock in the library is created here (TextBoxDocument included), and a TextBlock
        // resolves its family name through FontMapper.Default at layout time. Installing from here
        // rather than relying on Theme's static constructor is what makes that dependable: Theme is
        // not otherwise touched on a pure measuring path, so a caller that measured text before
        // anything read a theme value silently got RichTextKit's built-in mapper -- losing both the
        // typeface cache this exists for and PrivateFontCollection resolution.
        static TextMeasurer () => CachingFontMapper.Install ();

        // All parameters that uniquely determine a TextBlock's content and layout.
        private readonly record struct TextBlockKey (
            string Text,
            SKTypeface Font,       // compared by SKObject identity (handle equality)
            int FontSize,
            int MaxWidth,
            int MaxHeight,
            TextAlignment Alignment,
            uint Color,            // SKColor.Value
            int MaxLines,          // -1 represents null
            bool Ellipsis,
            int MnemonicIndex,
            int Rasterisation,     // -1 for the default; see CreateTextBlock
            int Decoration,        // DecorationUnderline / DecorationStrikeout over the whole text
            TextDirection Direction = TextDirection.Auto);  // the paragraph's reading order; see CreateTextBlock

        // Paint-safe TextBlock cache. TextBlock layout is idempotent; Paint() is read-only (painting one block on
        // several threads at once is covered by TextBlockConcurrentPaintTests). Reusing cached instances
        // eliminates repeated RichTextKit layout on every frame.
        private static readonly GenerationCache<TextBlockKey, TextBlock> _blocks = new ();

        // MeasureText's answers. Measuring needs only the size, and keeping the whole block for it (its lines, runs
        // and glyph buffers, about 4 KB) kept every measured string's layout alive long enough to be promoted by
        // the garbage collector: a report measuring new values spent a third of its time collecting
        // (ConcurrentMeasureBenchmarks). Nothing else asks for a block with MeasureText's key (default alignment
        // and colour, no line limit), so no caller loses a block it used to get from the cache.
        private static readonly GenerationCache<TextBlockKey, SKSize> _sizes = new ();

        // A font's Underline and Strikeout, which the typeface (family, weight, slant) does not carry.
        internal const int DecorationUnderline = 1;
        internal const int DecorationStrikeout = 2;

        internal static int DecorationOf (Majorsilence.Forms.Drawing.Font? font)
            => font is null ? 0 : (font.Underline ? DecorationUnderline : 0) | (font.Strikeout ? DecorationStrikeout : 0);

        // direction is the paragraph's reading order: Auto takes it from the first strong character (the theme's way),
        // LTR or RTL fixes it, as upstream's DrawTextEx without or with DT_RTLREADING does (see ReadingDirectionOf).
        internal static TextBlock CreateTextBlock (string text, SKTypeface font, int fontSize, Size maxSize, TextAlignment alignment = TextAlignment.Auto, SKColor color = new SKColor (), int? maxLines = null, bool ellipsis = false, int mnemonicIndex = -1, int rasterisation = -1, int decoration = 0, TextDirection direction = TextDirection.Auto)
        {
            // rasterisation keys the cache only. RichTextKit builds each run's SKTextBlob on the FIRST
            // paint, with that paint's edging and hinting, and reuses it for every later paint -- so a
            // block shared between an aliased and an antialiased draw of the same text rendered with
            // whichever came first (Graphics.TextRenderingHint, GFX-08).
            // One vectorised scan instead of three Replace passes on every call, cache hits included.
            if (maxLines == 1 && text.AsSpan ().IndexOfAny ('\n', '\r') >= 0) {
                text = text.Replace ("\n\r", "»");
                text = text.Replace ("\n", "»");
                text = text.Replace ("\r", "»");
            }

            var key = new TextBlockKey (text, font, fontSize, maxSize.Width, maxSize.Height, alignment, (uint)color, maxLines ?? -1, ellipsis, mnemonicIndex, rasterisation, decoration, direction);

            if (_blocks.TryGet (key, out var cached))
                return cached;

            return _blocks.Add (key, LayOut (text, font, fontSize, maxSize, alignment, color, maxLines, ellipsis, mnemonicIndex, decoration, direction));
        }

        private static TextBlock LayOut (string text, SKTypeface font, int fontSize, Size maxSize, TextAlignment alignment, SKColor color, int? maxLines, bool ellipsis, int mnemonicIndex, int decoration = 0, TextDirection direction = TextDirection.Auto)
        {
            // In a ported app the text is laid out on GDI's pixel grid, as upstream's TextRenderer lays it out.
            using var gdi = GdiTextLayout.Enter (fontSize);

            var tb = new TextBlock {
                MaxWidth = maxLines.GetValueOrDefault () == 1 ? (float?)null : maxSize.Width,
                MaxHeight = maxSize.Height == int.MaxValue ? (int?)null : maxSize.Height,
                Alignment = alignment,
                MaxLines = maxLines,
                EllipsisEnabled = ellipsis,
                BaseDirection = direction,
            };

            // The typeface's slant has to travel with its weight: RichTextKit re-resolves the face
            // from the style's family name, weight and FontItalic, so without it every italic font
            // drew upright -- a report chart's italic category labels among them.
            var face = StyleOf (font);

            // On the grid a synthesized bold already has GDI's emboldened advances.
            var spacing = gdi.Active ? 0f : GdiBoldSpacing (font, fontSize);

            var styleNormal = new Style {
                FontFamily = face.Family,
                FontSize = fontSize,
                TextColor = color,
                FontWeight = face.Weight,
                FontItalic = face.Italic,
                LetterSpacing = spacing,
                Underline = (decoration & DecorationUnderline) != 0 ? UnderlineStyle.Solid : UnderlineStyle.None,
                StrikeThrough = (decoration & DecorationStrikeout) != 0 ? StrikeThroughStyle.Solid : StrikeThroughStyle.None
            };

            // Underline the mnemonic character (the access key) by splitting the text into a
            // normal run, a single underlined run for that character, and a trailing normal run.
            if (mnemonicIndex >= 0 && mnemonicIndex < text.Length) {
                var styleMnemonic = new Style {
                    FontFamily = face.Family,
                    FontSize = fontSize,
                    TextColor = color,
                    FontWeight = face.Weight,
                    FontItalic = face.Italic,
                    LetterSpacing = spacing,
                    Underline = UnderlineStyle.Solid,
                    StrikeThrough = styleNormal.StrikeThrough
                };

                if (mnemonicIndex > 0)
                    AddTextWithEmojiPresentation (tb, text.AsSpan (0, mnemonicIndex), styleNormal);

                // The mnemonic character itself is never an emoji base character in practice (an access key is
                // a plain letter or digit), so it is added as-is, exactly as before.
                tb.AddText (text.AsSpan (mnemonicIndex, 1), styleMnemonic);

                if (mnemonicIndex + 1 < text.Length)
                    AddTextWithEmojiPresentation (tb, text.AsSpan (mnemonicIndex + 1), styleNormal);
            } else {
                AddTextWithEmojiPresentation (tb, text, styleNormal);
            }

            // Above, for a single line, we did not limit the width, and we need to fix it here
            // - If it fits, we need to set MaxWidth so Left/Center/Right alignment will work
            // - If it doesn't fit and we need an ellipsis, set MaxWidth to trigger the ellipsis
            if (maxLines.GetValueOrDefault () == 1) {
                if (tb.MeasuredWidth <= maxSize.Width || ellipsis)
                    tb.MaxWidth = maxSize.Width;
            } else if (maxLines.GetValueOrDefault () == 0) {
                if (tb.Lines.LastOrDefault ()?.Runs.Count > 1 && !ellipsis)
                    tb.MaxHeight *= 2;
            }

            gdi.Complete (tb);

            return tb;
        }

        // A typeface's family name, weight and slant, as a block's Style takes them. Each is a native call on the
        // typeface, the name a native string as well, and every block laid out asked for all three: a few per cent of
        // measuring a new string (ConcurrentMeasureBenchmarks). A typeface's style never changes.
        private sealed class TypefaceStyle (string family, int weight, bool italic)
        {
            internal string Family { get; } = family;
            internal int Weight { get; } = weight;
            internal bool Italic { get; } = italic;
        }

        private static readonly System.Runtime.CompilerServices.ConditionalWeakTable<SKTypeface, TypefaceStyle> typeface_styles = new ();

        private static TypefaceStyle StyleOf (SKTypeface font)
            => typeface_styles.GetValue (font, static f => new TypefaceStyle (f.FamilyName, f.FontWeight, f.FontSlant != SKFontStyleSlant.Upright));

        // For tests: the number of cached blocks and sizes, both generations of each.
        internal static int CachedBlockCount => _blocks.Count + _sizes.Count;

        // For tests: the number of cached blocks alone.
        internal static int CachedLayoutCount => _blocks.Count;

        // Resolved emoji-capable typeface family names, keyed by base codepoint. A font-manager query,
        // amortised the same way FontSubstitution's own fallback cache is (see there): this is asked
        // for every VARIATION SELECTOR-16 sequence in every string laid out.
        private static readonly System.Collections.Concurrent.ConcurrentDictionary<int, string?> emojiFamilyByCodepoint = new ();

        // A character followed by VARIATION SELECTOR-16 (U+FE0F) asks for its emoji presentation, but
        // RichTextKit resolves one typeface per Style/run (FontMapper.TypefaceFromStyle takes the style,
        // not the text), so it cannot see that request -- the requested UI font is used throughout, and a
        // base character the UI font already happens to have (WARNING SIGN's plain triangle is common in
        // ordinary text fonts) draws as that font's own glyph, ignoring the selector entirely. Splitting a
        // selector sequence into its own run, styled with a font family resolved to actually have the
        // colour glyph, is what a real HarfBuzz-based shaper would do internally; this does the same thing
        // one level up, at the run-building step this project owns. Found and evidenced on
        // majorsilence/alert-buddy's tracker (#271/#281): the warning sign drew as a monochrome outline
        // rather than the coloured triangle. Filed as #271 in this tracker as well.
        private static void AddTextWithEmojiPresentation (TextBlock tb, ReadOnlySpan<char> text, Style style)
        {
            var runStart = 0;
            var i = 0;

            while (i < text.Length) {
                var isPair = char.IsHighSurrogate (text[i]) && i + 1 < text.Length && char.IsLowSurrogate (text[i + 1]);
                var length = isPair ? 2 : 1;
                var selectorAt = i + length;

                if (selectorAt < text.Length && text[selectorAt] == '️') {
                    var codepoint = isPair ? char.ConvertToUtf32 (text[i], text[i + 1]) : text[i];
                    var emojiFamily = emojiFamilyByCodepoint.GetOrAdd (codepoint,
                        cp => SKFontManager.Default.MatchCharacter (null, ["und-Zsye"], cp)?.FamilyName);

                    // No emoji-capable face on the system either: leave the sequence for the surrounding
                    // run, same as if it had not been recognised at all, rather than force a pointless split.
                    if (emojiFamily is not null) {
                        if (i > runStart)
                            tb.AddText (text.Slice (runStart, i - runStart), style);

                        tb.AddText (text.Slice (i, length + 1), new Style {
                            FontFamily = emojiFamily,
                            FontSize = style.FontSize,
                            TextColor = style.TextColor,
                            FontWeight = style.FontWeight,
                            Underline = style.Underline,
                            LetterSpacing = style.LetterSpacing,
                        });

                        i += length + 1;
                        runStart = i;
                        continue;
                    }
                }

                i += length;
            }

            if (runStart == 0)
                tb.AddText (text, style); // No selector sequence found: exactly the unmodified original call.
            else if (runStart < text.Length)
                tb.AddText (text.Slice (runStart), style);
        }

        /// <summary>
        /// Clears the TextBlock layout cache. Call when the theme changes so stale
        /// color/font entries are not reused.
        /// </summary>
        internal static void ClearTextBlockCache ()
        {
            _blocks.Clear ();
            _sizes.Clear ();
        }

        /// <summary>
        /// Finds the next word separator in the specified text.
        /// </summary>
        public static int FindNextSeparator (string text, int start, bool forward)
        {
            var len = text.Length;

            if (forward) {
                for (var i = start + 1; i < len; i++) {
                    if (IsWordSeparator (text[i]))
                        return i + 1;
                }

                return len;
            } else {
                for (var i = start - 1; i > 0; i--) {
                    if (IsWordSeparator (text[i - 1]))
                        return i;
                }

                return 0;
            }
        }

        /// <summary>
        /// Gets the cursor location at the specified code point.
        /// </summary>
        public static Rectangle GetCursorLocation (string text, Rectangle bounds, SKTypeface font, int fontSize, Size maxSize, ContentAlignment alignment, int codePoint, int? maxLines = null)
        {
            // If there isn't any text the cursor height will be 0, so put a dummy character here
            if (string.IsNullOrWhiteSpace (text))
                text = "l";

            var tb = CreateTextBlock (text, font, fontSize, bounds.Size, GetTextAlign (alignment), maxLines: maxLines);

            try {
                var caret_rect = tb.GetCaretInfo (new CaretPosition (codePoint)).CaretRectangle;

                // We need to offset the caret to client bounds
                var vertical = GetVerticalAlign (alignment);

                if (vertical == SKTextAlign.Left)
                    return new Rectangle (bounds.X + (int)caret_rect.Left, bounds.Y + (int)caret_rect.Top, (int)caret_rect.Width, (int)caret_rect.Height);
                else if (vertical == SKTextAlign.Right)
                    return new Rectangle (bounds.X + (int)caret_rect.Left, bounds.Y + bounds.Bottom - (int)caret_rect.Height, (int)caret_rect.Width, (int)caret_rect.Height);

                // Centered
                return new Rectangle (bounds.X + (int)caret_rect.Left, bounds.Y + (int)caret_rect.Top + ((bounds.Height - (int)caret_rect.Height) / 2), (int)caret_rect.Width, (int)caret_rect.Height);
            } catch (ArgumentOutOfRangeException) {
                return Rectangle.Empty;
            }
        }

        /// <summary>
        /// Gets the cursor location at the specified code point.
        /// </summary>
        public static Rectangle GetCursorLocation (TextBlock block, Point location, int codePoint, int fontSize)
            => GetCursorLocation (block, location, codePoint, fontSize, emptyLineHeight: 0);

        /// <summary>
        /// Gets the cursor location at the specified code point. With no text there is nothing to measure, so
        /// <paramref name="emptyLineHeight"/> (the height one line of this text would have) sizes the cursor the way a
        /// cursor on text is sized; zero falls back to a guess from <paramref name="fontSize"/>.
        /// </summary>
        public static Rectangle GetCursorLocation (TextBlock block, Point location, int codePoint, int fontSize, int emptyLineHeight)
        {
            // If there isn't any text the cursor height will be 0, so size it from one line of text, or failing that from the font size
            if (block.MeasuredHeight == 0)
                return new Rectangle (location.X, location.Y + 1, 0, emptyLineHeight > 0 ? emptyLineHeight - 2 : fontSize + 2);

            try {
                var caret_rect = block.GetCaretInfo (new CaretPosition (codePoint)).CaretRectangle;

                return new Rectangle (location.X + (int)caret_rect.Left, location.Y + (int)caret_rect.Top + 1, (int)caret_rect.Width, (int)caret_rect.Height - 2);
            } catch (ArgumentOutOfRangeException) {
                return Rectangle.Empty;
            }
        }

        /// <summary>
        /// Gets the maximum carent index of the specified text.
        /// </summary>
        public static int GetMaxCaretIndex (string text)
        {
            var tb = new TextBlock ();

            tb.AddText (text, new Style ());

            return tb.CaretIndicies[tb.CaretIndicies.Count - 1];
        }

        /// <summary>
        /// The reading order a control's text is laid out in. A ported app's control reads as upstream's does: right to
        /// left when <see cref="Control.RightToLeft"/> is Yes (<c>DT_RTLREADING</c>), else left to right whatever its
        /// first letter. Elsewhere the first strong character decides, as it always has.
        /// </summary>
        /// <remarks>
        /// Measured on WinForms (.NET 10), 8.25pt labels: with RightToLeft.Yes ":Cheque Total" shows "Cheque Total:" at the
        /// right (the colon, a neutral before the first letter, takes the paragraph's direction), "-5.00" shows "5.00-" and
        /// "#1 Fee" shows "Fee #1", each pixel for pixel the reordered caption drawn left to right. With RightToLeft.No a
        /// caption beginning in Hebrew (":שלום") is still a left-to-right paragraph: the colon stays at the left.
        /// </remarks>
        internal static TextDirection ReadingDirectionOf (Control control)
        {
            if (!ControlPaint.UsesUpstreamGlyphs)
                return TextDirection.Auto;

            return control.RightToLeft == RightToLeft.Yes ? TextDirection.RTL : TextDirection.LTR;
        }

        internal static TextAlignment GetTextAlign (ContentAlignment alignment)
        {
            switch (alignment) {
                case ContentAlignment.TopLeft:
                case ContentAlignment.MiddleLeft:
                case ContentAlignment.BottomLeft:
                    return TextAlignment.Left;
                case ContentAlignment.TopCenter:
                case ContentAlignment.MiddleCenter:
                case ContentAlignment.BottomCenter:
                    return TextAlignment.Center;
                default:
                    return TextAlignment.Right;
            }
        }

        internal static SKTextAlign GetVerticalAlign (ContentAlignment alignment)
        {
            // We are reusing Left, Center, Right as Top, Center, Bottom
            switch (alignment) {
                case ContentAlignment.TopLeft:
                case ContentAlignment.TopCenter:
                case ContentAlignment.TopRight:
                    return SKTextAlign.Left;
                case ContentAlignment.MiddleLeft:
                case ContentAlignment.MiddleCenter:
                case ContentAlignment.MiddleRight:
                    return SKTextAlign.Center;
                default:
                    return SKTextAlign.Right;
            }
        }

        /// <summary>
        /// Gets the character element closest to the specified point.
        /// </summary>
        public static HitTestResult HitTest (string text, Rectangle bounds, SKTypeface font, int fontSize, Size maxSize, ContentAlignment alignment, Point location, int? maxLines = null)
        {
            var tb = CreateTextBlock (text, font, fontSize, bounds.Size, GetTextAlign (alignment), maxLines: maxLines);

            // We need to offset the requested location to fit the desired bounds
            var x = location.X + bounds.X;
            var y = location.Y + bounds.Y;

            return tb.HitTest (x, y);
        }

        /// <summary>
        /// Determines if the specified character is a word separator.
        /// </summary>
        public static bool IsWordSeparator (char c)
        {
            switch (c) {
                case ' ':
                case '\t':
                case '(':
                case ')':
                case '\r':
                case '\n':
                    return true;
            }

            return false;
        }

        /// <summary>
        /// Represents the maximum size of a text area.
        /// </summary>
        public static Size MaxSize = new Size (int.MaxValue, int.MaxValue);

        // One line of text at the control's font, in LOGICAL pixels. MeasureText (text, control) measures at the
        // DEVICE font size, so a PreferredHeight built straight from it came out doubled at scale 2 and then laid a
        // logical box out at that device height (a ToolStripTextBox 42px tall in a 40px row on Windows).
        internal static int LogicalLineHeight (Control control)
            => control.DeviceToLogicalUnits ((int)Math.Ceiling (MeasureText ("Wg", control).Height));

        /// <summary>
        /// Measures the specified text using font characteristics from the provided control.
        /// </summary>
        public static SKSize MeasureText (string text, Control control)
            => MeasureText (text, control, new Size (1000, 1000));

        /// <summary>
        /// Measures the specified text using font characteristics from the provided control.
        /// </summary>
        public static SKSize MeasureText (string text, Control control, Size maxSize)
            => MeasureText (text, control.CurrentStyle.GetFont (), control.LogicalToDeviceUnits (control.CurrentStyle.GetFontSize ()), maxSize);

        /// <summary>
        /// Measures the specified text using the provided font characteristics.
        /// </summary>
        public static SKSize MeasureText (string text, SKTypeface font, int fontSize)
            => MeasureText (text, font, fontSize, new Size (1000, 1000));

        /// <summary>
        /// Measures the specified text using the provided font characteristics.
        /// </summary>
        public static SKSize MeasureText (string text, SKTypeface font, int fontSize, Size maxSize)
        {
            if (!text.HasValue ())
                return SKSize.Empty;

            // The key CreateTextBlock (text, font, fontSize, maxSize) would use; MeasureText's text never has its
            // line breaks replaced, as that is only done for a one-line block.
            var key = new TextBlockKey (text, font, fontSize, maxSize.Width, maxSize.Height, TextAlignment.Auto, 0, -1, false, -1, -1, 0);
            if (_sizes.TryGet (key, out var size))
                return size;

            var tb = LayOut (text, font, fontSize, maxSize, TextAlignment.Auto, new SKColor (), null, false, -1);

            using var gdi = GdiTextLayout.Enter (fontSize);

            return _sizes.Add (key, new SKSize (gdi.Width (tb.MeasuredWidth), tb.MeasuredHeight));
        }
    }
}
