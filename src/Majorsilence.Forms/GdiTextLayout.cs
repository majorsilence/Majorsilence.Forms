using System.Collections.Concurrent;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using SkiaSharp;
using Topten.RichTextKit;
using HbBlob = HarfBuzzSharp.Blob;
using HbBuffer = HarfBuzzSharp.Buffer;
using HbFace = HarfBuzzSharp.Face;
using HbFont = HarfBuzzSharp.Font;

namespace Majorsilence.Forms
{
    /// <summary>
    /// Lays text out on GDI's pixel grid, as upstream's <c>TextRenderer</c> does, for an app that chose its default
    /// font (<see cref="SystemFonts.HasDefaultFontOverride"/>, the ported mode every upstream-look change is gated on).
    /// </summary>
    /// <remarks>
    /// <para>
    /// RichTextKit shapes with HarfBuzz at the font's design advances, scaled linearly: 8.25pt Microsoft Sans Serif
    /// "Clear Fields" came out 58.74px wide. Upstream draws through GDI (<c>DrawTextEx</c>), which advances the pen by
    /// each glyph's HINTED advance, a whole number of pixels, and kerns by whole pixels: the same caption is 54px. A
    /// caption laid out in the designer to fit wrapped or clipped here, and every width ran about 5 to 9 per cent wide.
    /// </para>
    /// <para>
    /// Measured with a WinForms probe (.NET 10, <c>TextRenderer.MeasureText</c> with <c>NoPadding</c> on every prefix of
    /// ten captions) for Microsoft Sans Serif 8.25pt and 12pt and Segoe UI 9pt at 96, 144 and 192 DPI: each pen
    /// position is the sum of the glyphs' hinted advances plus, for each pair, the OpenType kerning HarfBuzz finds,
    /// rounded to whole pixels at that size. 90 of 90 captions matched to the pixel. Skia's own advance for the glyph,
    /// asked of an aliased font with linear metrics off, is that hinted advance on Windows (95 of 95 printable ASCII
    /// glyphs at every size, synthesized bold included, against <c>GetCharWidth32</c>).
    /// </para>
    /// <para>
    /// RichTextKit offers no seam for advances: one <c>TextShaper</c> per typeface shapes with a private HarfBuzz font
    /// at a fixed scale of 512 and multiplies by <c>FontSize / 512</c>. So the shaper's font is swapped, once, for a
    /// HarfBuzz sub-font whose advance callbacks place each glyph on the grid while a GDI layout is in progress on the
    /// calling thread (<see cref="Enter"/>, which carries the pixel size the shaper does not know). Outside one the
    /// sub-font answers exactly as its parent did. The callback sees the run's glyphs in order, so it adds each pair's
    /// whole-pixel kerning and subtracts what HarfBuzz's own kerning will add. Positions are in 512ths of the size, so
    /// they cannot hit every whole pixel exactly: each pen position is rounded down to the 512th below it, at most
    /// <c>size / 512</c> px short, so a caption never measures wider than GDI's and never wraps where GDI does not.
    /// <see cref="TextMeasurer"/> rounds the measured width back to the whole pixel.
    /// </para>
    /// </remarks>
    internal static class GdiTextLayout
    {
        // HarfBuzz units per pixel of font size in RichTextKit's TextShaper (its overScale).
        private const int ShaperScale = 512;

        // The pixel size of the GDI layout in progress on this thread, or 0 when there is none.
        [System.ThreadStatic]
        private static int scope_size;

        /// <summary>Whether text is laid out on GDI's grid: the app chose its default font.</summary>
        internal static bool Applies => SystemFonts.HasDefaultFontOverride;

        /// <summary>
        /// Starts a GDI layout of text at <paramref name="fontSize"/> pixels on this thread, when <see cref="Applies"/>.
        /// Every TextBlock laid out before the returned scope is disposed is placed on GDI's grid.
        /// </summary>
        internal static Scope Enter (int fontSize) => new (Applies && fontSize > 0 && fontSize < 4096 ? fontSize : 0);

        /// <summary>A GDI layout in progress; see <see cref="Enter"/>.</summary>
        internal readonly struct Scope : System.IDisposable
        {
            private readonly int outer;

            internal Scope (int size)
            {
                Size = size;
                outer = scope_size;

                if (size > 0)
                    scope_size = size;
            }

            /// <summary>The pixel size laid out on the grid, or 0 when this scope does nothing.</summary>
            internal int Size { get; }

            /// <summary>Whether this scope lays text out on GDI's grid.</summary>
            internal bool Active => Size > 0;

            /// <summary>Lays <paramref name="block"/> out now, while the scope is open; RichTextKit is lazy.</summary>
            internal void Complete (TextBlock block)
            {
                if (Active)
                    _ = block.MeasuredWidth;
            }

            /// <summary>A measured width on GDI's grid: the whole pixel it is a 512th or so short of.</summary>
            internal float Width (float measured) => Active ? (float) System.Math.Round (measured) : measured;

            public void Dispose ()
            {
                if (Size > 0)
                    scope_size = outer;
            }
        }

        private static readonly ConditionalWeakTable<SKTypeface, GridShaper> shapers = new ();
        private static readonly object prepare_lock = new ();

        /// <summary>
        /// Readies RichTextKit's shaper for <paramref name="face"/> to lay out on the grid. Called by the font mapper as
        /// each run's typeface is resolved; does nothing outside a GDI layout, or once done for the typeface.
        /// </summary>
        internal static void Prepare (SKTypeface face)
        {
            if (scope_size <= 0 || shapers.TryGetValue (face, out _))
                return;

            lock (prepare_lock) {
                if (shapers.TryGetValue (face, out _))
                    return;

                shapers.Add (face, GridShaper.Install (face));
            }
        }

        // RichTextKit's TextShaper is internal: its per-typeface factory and its HarfBuzz font are reached by name.
        // Either missing (another RichTextKit) leaves the text on RichTextKit's own advances.
        private static readonly System.Reflection.MethodInfo? shaper_for_typeface =
            System.Type.GetType ("Topten.RichTextKit.TextShaper, Topten.RichTextKit")?.GetMethod ("ForTypeface", System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic, null, [typeof (SKTypeface)], null);

        private static readonly System.Reflection.FieldInfo? shaper_font =
            System.Type.GetType ("Topten.RichTextKit.TextShaper, Topten.RichTextKit")?.GetField ("_font", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);

        // The grid layout of one typeface's shaper.
        private sealed class GridShaper
        {
            private readonly SKTypeface face;
            private readonly HbFont? parent;

            // Per pixel size: each glyph's hinted advance, or -1 until asked for.
            private readonly ConcurrentDictionary<int, int[]> advances = new ();

            private readonly System.Lazy<Kerning?> kerning;

            private GridShaper (SKTypeface face, HbFont? parent)
            {
                this.face = face;
                this.parent = parent;
                kerning = new (() => Kerning.For (face));
            }

            // Swaps the shaper's HarfBuzz font for a grid sub-font. Where RichTextKit's shaper has no such font (another
            // version), or the HarfBuzz library cannot be called, the text keeps RichTextKit's own advances.
            internal static GridShaper Install (SKTypeface face)
            {
                var shaper = shaper_font is null ? null : shaper_for_typeface?.Invoke (null, [face]);

                if (shaper is null || shaper_font!.GetValue (shaper) is not HbFont original)
                    return new GridShaper (face, null);

                var grid = new GridShaper (face, original);

                try {
                    // The sub-font inherits the parent's scale and answers every callback not set here through it. The
                    // parent is kept, not disposed: a thread shaping without a grid layout may still hold it.
                    var font = new HbFont (original);
                    Native.Install (font.Handle, GCHandle.ToIntPtr (GCHandle.Alloc (grid)));
                    shaper_font.SetValue (shaper, font);
                } catch (System.Exception e) when (e is System.DllNotFoundException or System.EntryPointNotFoundException) {
                }

                return grid;
            }

            internal int Advance (uint glyph)
            {
                var size = scope_size;

                if (size <= 0)
                    return Native.hb_font_get_glyph_h_advance (parent!.Handle, glyph);

                return (int) FloorDiv ((long) Hinted (size, glyph) * ShaperScale, size);
            }

            // The run's glyphs, in logical order, HarfBuzz's way: each at a stride in its glyph-info array, each advance
            // written at a stride into its glyph-position array. On the grid, each pen position is a whole pixel turned
            // into 512ths of the size and rounded down.
            internal void Advances (uint count, System.IntPtr first_glyph, uint glyph_stride, System.IntPtr first_advance, uint advance_stride)
            {
                var size = scope_size;

                if (size <= 0) {
                    Native.hb_font_get_glyph_h_advances (parent!.Handle, count, first_glyph, glyph_stride, first_advance, advance_stride);
                    return;
                }

                var glyphs = count <= 256 ? stackalloc uint[(int) count] : new uint[count];

                for (var i = 0; i < glyphs.Length; i++)
                    glyphs[i] = (uint) Marshal.ReadInt32 (first_glyph, (int) (i * glyph_stride));

                var table = HintedAdvances (size, glyphs);
                var kerns = kerning.Value;
                long pen = 0;
                long placed = 0;

                for (var i = 0; i < glyphs.Length; i++) {
                    var glyph = glyphs[i];
                    pen += glyph < table.Length ? table[glyph] : 0;

                    // GDI kerns by whole pixels; HarfBuzz will add its own kerning, in 512ths, after this.
                    var shaper_kern = 0;

                    if (kerns is not null && i + 1 < glyphs.Length) {
                        pen += kerns.Pixels (glyph, glyphs[i + 1], size);
                        shaper_kern = kerns.Pixels (glyph, glyphs[i + 1], 0);
                    }

                    var position = FloorDiv (pen * ShaperScale, size);
                    Marshal.WriteInt32 (first_advance, (int) (i * advance_stride), (int) (position - placed) - shaper_kern);
                    placed = position;
                }
            }

            private int Hinted (int size, uint glyph)
            {
                System.Span<uint> one = [glyph];
                var table = HintedAdvances (size, one);
                return glyph < table.Length ? table[glyph] : 0;
            }

            // The hinted advances at size, with every glyph in glyphs filled in.
            private int[] HintedAdvances (int size, System.ReadOnlySpan<uint> glyphs)
            {
                var table = advances.GetOrAdd (size, _ => {
                    var empty = new int[System.Math.Max (1, face.GlyphCount)];
                    empty.AsSpan ().Fill (-1);
                    return empty;
                });

                var missing = 0;

                foreach (var glyph in glyphs) {
                    if (glyph < table.Length && table[glyph] < 0)
                        missing++;
                }

                if (missing == 0)
                    return table;

                var ids = new ushort[missing];
                var n = 0;

                foreach (var glyph in glyphs) {
                    if (glyph < table.Length && table[glyph] < 0 && n < missing)
                        ids[n++] = (ushort) glyph;
                }

                // Aliased, with linear metrics off: the hinted advance GDI uses (see the class remarks).
                using var font = new SKFont (face, size) {
                    Edging = SKFontEdging.Alias,
                    Hinting = SKFontHinting.Full,
                    Subpixel = false,
                    LinearMetrics = false,
                };

                var widths = font.GetGlyphWidths (ids);

                for (var i = 0; i < ids.Length; i++)
                    table[ids[i]] = (int) System.Math.Round (widths[i]);

                return table;
            }
        }

        // A typeface's OpenType kerning, pair by pair, as HarfBuzz applies it to the first glyph of a pair.
        private sealed class Kerning
        {
            private readonly HbFont shaper_scale;
            private readonly ConcurrentDictionary<int, HbFont> by_size = new ();
            private readonly ConcurrentDictionary<long, int> pairs = new ();

            // The character each glyph is mapped from (the lowest one), or 0 for a glyph no character maps to.
            private readonly int[] characters;

            private Kerning (HbFont font, int[] characters)
            {
                shaper_scale = font;
                this.characters = characters;
            }

            // Null when the typeface has neither a GPOS nor a kern table, so nothing to kern.
            internal static Kerning? For (SKTypeface face)
            {
                using var stream = face.OpenStream (out var index);

                if (stream is null || stream.Length <= 0)
                    return null;

                var length = stream.Length;
                var data = Marshal.AllocHGlobal (length);
                var bytes = new byte[length];

                if (stream.Read (bytes, length) != length) {
                    Marshal.FreeHGlobal (data);
                    return null;
                }

                Marshal.Copy (bytes, 0, data, length);

                var blob = new HbBlob (data, length, HarfBuzzSharp.MemoryMode.ReadOnly, () => Marshal.FreeHGlobal (data));
                var hb_face = new HbFace (blob, index) { UnitsPerEm = face.UnitsPerEm };
                var tables = hb_face.Tables;

                if (System.Array.IndexOf (tables, HarfBuzzSharp.Tag.Parse ("GPOS")) < 0 && System.Array.IndexOf (tables, HarfBuzzSharp.Tag.Parse ("kern")) < 0)
                    return null;

                var font = new HbFont (hb_face);
                font.SetScale (ShaperScale, ShaperScale);
                font.SetFunctionsOpenType ();

                var characters = new int[System.Math.Max (1, face.GlyphCount)];
                var codepoints = new int[0x10000];
                for (var c = 0; c < codepoints.Length; c++)
                    codepoints[c] = c;

                using (var sk = new SKFont (face)) {
                    var glyphs = sk.GetGlyphs (codepoints);

                    for (var c = codepoints.Length - 1; c > 0; c--) {
                        var glyph = glyphs[c];
                        if (glyph != 0 && glyph < characters.Length && (c < 0xD800 || c > 0xDFFF))
                            characters[glyph] = c;
                    }
                }

                return new Kerning (font, characters);
            }

            // The kerning of first before second: whole pixels at size, or HarfBuzz units at the shaper's scale for 0.
            internal int Pixels (uint first, uint second, int size)
            {
                if (first >= characters.Length || second >= characters.Length || characters[first] == 0 || characters[second] == 0)
                    return 0;

                var key = ((long) size << 32) | (first << 16) | second;

                if (pairs.TryGetValue (key, out var known))
                    return known;

                // Shaped at the pixel size itself, HarfBuzz rounds the kerning to whole pixels, as GDI does.
                var font = size == 0 ? shaper_scale : by_size.GetOrAdd (size, s => {
                    var sized = new HbFont (shaper_scale);
                    sized.SetScale (s, s);
                    return sized;
                });

                using var buffer = new HbBuffer ();
                buffer.AddUtf32 ([characters[first], characters[second]]);
                buffer.GuessSegmentProperties ();
                font.Shape (buffer);

                var value = 0;
                var infos = buffer.GlyphInfos;
                var positions = buffer.GlyphPositions;

                for (var i = 0; i < infos.Length; i++) {
                    if (infos[i].Cluster == 0 && infos[i].Codepoint == first && infos.Length == 2) {
                        value = positions[i].XAdvance - font.GetHorizontalGlyphAdvance (first);
                        break;
                    }
                }

                return pairs.GetOrAdd (key, value);
            }
        }

        // HarfBuzz's advance callbacks, set on the native font functions. HarfBuzzSharp's own proxy for the batched one
        // reads the glyphs and writes the advances as if packed, ignoring HarfBuzz's strides (each is one field of a
        // 20-byte glyph record), so it would read the wrong glyphs and write over the glyph positions.
        private static class Native
        {
            private const string Library = "libHarfBuzzSharp";

            [UnmanagedFunctionPointer (CallingConvention.Cdecl)]
            internal delegate int AdvanceFunc (System.IntPtr font, System.IntPtr fontData, uint glyph, System.IntPtr userData);

            [UnmanagedFunctionPointer (CallingConvention.Cdecl)]
            internal delegate void AdvancesFunc (System.IntPtr font, System.IntPtr fontData, uint count, System.IntPtr firstGlyph, uint glyphStride, System.IntPtr firstAdvance, uint advanceStride, System.IntPtr userData);

            // Held for the life of the process: native code keeps the function pointers.
            private static readonly AdvanceFunc advance = Advance;
            private static readonly AdvancesFunc advances = Advances;

            [DllImport (Library, CallingConvention = CallingConvention.Cdecl)]
            private static extern System.IntPtr hb_font_funcs_create ();

            [DllImport (Library, CallingConvention = CallingConvention.Cdecl)]
            private static extern void hb_font_funcs_set_glyph_h_advance_func (System.IntPtr ffuncs, AdvanceFunc func, System.IntPtr userData, System.IntPtr destroy);

            [DllImport (Library, CallingConvention = CallingConvention.Cdecl)]
            private static extern void hb_font_funcs_set_glyph_h_advances_func (System.IntPtr ffuncs, AdvancesFunc func, System.IntPtr userData, System.IntPtr destroy);

            [DllImport (Library, CallingConvention = CallingConvention.Cdecl)]
            private static extern void hb_font_funcs_make_immutable (System.IntPtr ffuncs);

            [DllImport (Library, CallingConvention = CallingConvention.Cdecl)]
            private static extern void hb_font_set_funcs (System.IntPtr font, System.IntPtr klass, System.IntPtr fontData, System.IntPtr destroy);

            [DllImport (Library, CallingConvention = CallingConvention.Cdecl)]
            internal static extern int hb_font_get_glyph_h_advance (System.IntPtr font, uint glyph);

            [DllImport (Library, CallingConvention = CallingConvention.Cdecl)]
            internal static extern void hb_font_get_glyph_h_advances (System.IntPtr font, uint count, System.IntPtr firstGlyph, uint glyphStride, System.IntPtr firstAdvance, uint advanceStride);

            // Gives font the grid's two advance callbacks; grid is a GCHandle to its GridShaper, never freed (the font
            // lives as long as RichTextKit's shaper, which is never released).
            internal static void Install (System.IntPtr font, System.IntPtr grid)
            {
                var functions = hb_font_funcs_create ();
                hb_font_funcs_set_glyph_h_advance_func (functions, advance, grid, System.IntPtr.Zero);
                hb_font_funcs_set_glyph_h_advances_func (functions, advances, grid, System.IntPtr.Zero);
                hb_font_funcs_make_immutable (functions);
                hb_font_set_funcs (font, functions, System.IntPtr.Zero, System.IntPtr.Zero);
            }

            private static GridShaper GridOf (System.IntPtr grid) => (GridShaper) GCHandle.FromIntPtr (grid).Target!;

            // An exception must not unwind into HarfBuzz.
            private static int Advance (System.IntPtr font, System.IntPtr fontData, uint glyph, System.IntPtr grid)
            {
                try {
                    return GridOf (grid).Advance (glyph);
                } catch {
                    return 0;
                }
            }

            private static void Advances (System.IntPtr font, System.IntPtr fontData, uint count, System.IntPtr firstGlyph, uint glyphStride, System.IntPtr firstAdvance, uint advanceStride, System.IntPtr grid)
            {
                try {
                    GridOf (grid).Advances (count, firstGlyph, glyphStride, firstAdvance, advanceStride);
                } catch {
                    for (var i = 0; i < count; i++)
                        Marshal.WriteInt32 (firstAdvance, (int) (i * advanceStride), 0);
                }
            }
        }

        private static long FloorDiv (long value, long divisor)
            => value >= 0 ? value / divisor : -((-value + divisor - 1) / divisor);
    }
}
