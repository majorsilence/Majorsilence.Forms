using System;
using System.Drawing;
using Majorsilence.Forms.Drawing.Drawing2D;
using Majorsilence.Forms.Drawing.Text;
using SkiaSharp;

#pragma warning disable CA1416  // WinForms compat layer — intentionally uses Windows-only System.Drawing APIs

namespace Majorsilence.Forms.Drawing
{
    /// <summary>
    /// WinForms compatibility: wraps an <see cref="SKCanvas"/> to provide a GDI-like drawing surface.
    /// Use <see cref="Control.CreateGraphics"/> for text measurement; for painting use <see cref="PaintEventArgs.Canvas"/>.
    /// </summary>
    public sealed partial class Graphics : IDisposable, Majorsilence.Forms.Drawing.IDeviceContext
    {
        private readonly Control? _control;
        private readonly SKCanvas? _canvas;

        // The surface this Graphics draws onto. Exposed to the Forms layer so PaintEventArgs can be
        // built from a Graphics, which is the shape WinForms code constructs it in.
        internal SKCanvas? Canvas => _canvas;
        private readonly bool _ownsCanvas;

        // The image FromImage was created over, if any. An SKCanvas does NOT keep its backing SKBitmap
        // alive, so without this a caller who does not hold the image themselves gets the bitmap
        // collected out from under native code -- which aborts the process rather than throwing.
        private readonly Majorsilence.Forms.Drawing.Image? _sourceImage;
        private bool _disposed;

        // The canvas matrix as it stood when this Graphics was created. GDI+ keeps the device origin
        // (for a control, its client origin) OUT of Graphics.Transform -- that starts as identity and
        // means "the world transform the caller has applied". Skia makes no such separation: whatever
        // translation put the canvas at the control's origin is already in SKCanvas.TotalMatrix. So the
        // world transform is tracked RELATIVE to this baseline. Without it, assigning Transform would
        // wipe out the control's origin and move the drawing to the top-left of the whole surface.
        private readonly SKMatrix _baseline = SKMatrix.Identity;

        internal Graphics (Control? control = null) { _control = control; }

        internal Graphics (SKCanvas canvas, Control? control = null)
        {
            _canvas = canvas;
            _control = control;
            _baseline = canvas.TotalMatrix;
        }

        private Graphics (SKCanvas canvas, bool ownsCanvas, Majorsilence.Forms.Drawing.Image? sourceImage = null)
        {
            _canvas = canvas;
            _ownsCanvas = ownsCanvas;
            _sourceImage = sourceImage;
            _baseline = canvas.TotalMatrix;
        }

        /// <summary>Creates a Graphics object for drawing on the specified Majorsilence.Forms.Drawing.Image.
        /// Drawing goes directly into the image's backing bitmap, matching System.Drawing semantics.</summary>
        public static Graphics FromImage (Majorsilence.Forms.Drawing.Image image)
        {
            ArgumentNullException.ThrowIfNull (image);
            var backing = image.GetSKBitmap () ?? throw new ArgumentException ("Image has no backing bitmap.", nameof (image));
            return new Graphics (new SKCanvas (backing), ownsCanvas: true, sourceImage: image);
        }

        /// <summary>Creates a Graphics object for the specified window handle. Returns a no-op instance in Majorsilence.Forms.</summary>
        public static Graphics FromHwnd (IntPtr hwnd) => new Graphics ();

        /// <summary>Creates a Graphics object from a device context handle. Returns a no-op instance in Majorsilence.Forms.</summary>
        public static Graphics FromHdc (IntPtr hdc) => new Graphics ();

        // --- Text measurement ---

        /// <summary>Measures the size of the specified string using the given font and size.</summary>
        public SizeF MeasureString (string text, SKTypeface font, int fontSize = -1)
        {
            if (string.IsNullOrEmpty (text)) return SizeF.Empty;
            var sz = fontSize <= 0
                ? TextMeasurer.MeasureText (text, font, Theme.FontSize)
                : TextMeasurer.MeasureText (text, font, fontSize);
            return new SizeF (sz.Width, sz.Height);
        }

        /// <summary>Measures the string constrained to a maximum width.</summary>
        /// <remarks>
        /// A <paramref name="maxWidth"/> of zero or less means UNBOUNDED, not "wrap at zero". That is
        /// GDI+'s convention, and callers rely on it: <c>Graphics.MeasureString (text, font)</c> is
        /// itself specified as passing a layout area of <c>SizeF (0, 0)</c>, so any library that
        /// funnels its measurement through one constrained overload passes 0 to mean "no limit".
        /// Taking the 0 literally wrapped every string to one grapheme per line, which turned
        /// auto-sized controls into tall narrow slivers rather than merely mis-measuring them.
        /// </remarks>
        public SizeF MeasureString (string text, SKTypeface font, int maxWidth, int fontSize = -1)
        {
            if (string.IsNullOrEmpty (text)) return SizeF.Empty;
            var proposed = new System.Drawing.Size (maxWidth > 0 ? maxWidth : int.MaxValue, int.MaxValue);
            var sz = fontSize <= 0
                ? TextMeasurer.MeasureText (text, font, Theme.FontSize, proposed)
                : TextMeasurer.MeasureText (text, font, fontSize, proposed);
            return new SizeF (sz.Width, sz.Height);
        }

        /// <summary>Measures the string using the control's own font.</summary>
        public SizeF MeasureString (string text, Control control)
        {
            if (string.IsNullOrEmpty (text)) return SizeF.Empty;
            var sz = TextMeasurer.MeasureText (text, control);
            return new SizeF (sz.Width, sz.Height);
        }

        /// <summary>Measures the string with a Majorsilence.Forms.Drawing.Font (maps to SKTypeface at the font's size).</summary>
        public SizeF MeasureString (string text, Majorsilence.Forms.Drawing.Font font)
        {
            if (string.IsNullOrEmpty (text) || font is null) return SizeF.Empty;
            var face = TypefaceCache.Resolve (font);
            return MeasureString (text, face, (int)Math.Round (font.PixelSize));
        }

        /// <summary>Measures the string with a Majorsilence.Forms.Drawing.Font, constrained to a size.</summary>
        public SizeF MeasureString (string text, Majorsilence.Forms.Drawing.Font font, SizeF layoutArea)
        {
            if (string.IsNullOrEmpty (text) || font is null) return SizeF.Empty;
            var face = TypefaceCache.Resolve (font);
            return MeasureString (text, face, (int)layoutArea.Width, (int)Math.Round (font.PixelSize));
        }

        /// <summary>Measures the string with a Majorsilence.Forms.Drawing.Font and StringFormat.</summary>
        /// <remarks>
        /// Honours <c>HotkeyPrefix</c> and <see cref="Majorsilence.Forms.Drawing.StringFormatFlags.DirectionVertical"/>
        /// (which swaps the reported width and height, matching the rotated run
        /// <see cref="DrawString(string, Majorsilence.Forms.Drawing.Font, Majorsilence.Forms.Drawing.Brush, RectangleF, Majorsilence.Forms.Drawing.StringFormat?)"/>
        /// draws for it). Trimming is still ignored.
        /// </remarks>
        public SizeF MeasureString (string text, Majorsilence.Forms.Drawing.Font font, Majorsilence.Forms.Drawing.StringFormat? format)
        {
            var single = MeasureString (WithoutHotkeyPrefix (text, format), font);
            return IsVertical (format) ? new SizeF (single.Height, single.Width) : single;
        }

        /// <summary>Measures the string with a Majorsilence.Forms.Drawing.Font, constrained to int width.</summary>
        /// <inheritdoc cref="MeasureString(string, Majorsilence.Forms.Drawing.Font, Majorsilence.Forms.Drawing.StringFormat)"/>
        public SizeF MeasureString (string text, Majorsilence.Forms.Drawing.Font font, int width, Majorsilence.Forms.Drawing.StringFormat? format)
        {
            text = WithoutHotkeyPrefix (text, format);
            if (string.IsNullOrEmpty (text) || font is null) return SizeF.Empty;

            // Vertical text here is a single rotated run, not multi-column CJK stacking (see
            // DrawStringVertical), so there is no second column for it to wrap into -- the width
            // constraint that drives wrapping for horizontal text does not apply.
            if (IsVertical (format)) {
                var single = MeasureString (text, font);
                return new SizeF (single.Height, single.Width);
            }

            var face = TypefaceCache.Resolve (font);
            return MeasureString (text, face, width, (int)Math.Round (font.PixelSize));
        }

        /// <summary>Measures the string with a Majorsilence.Forms.Drawing.Font, constrained to SizeF.</summary>
        /// <inheritdoc cref="MeasureString(string, Majorsilence.Forms.Drawing.Font, Majorsilence.Forms.Drawing.StringFormat)"/>
        public SizeF MeasureString (string text, Majorsilence.Forms.Drawing.Font font, SizeF layoutArea, Majorsilence.Forms.Drawing.StringFormat? format)
        {
            var display = WithoutHotkeyPrefix (text, format);
            if (IsVertical (format)) {
                var single = MeasureString (display, font);
                return new SizeF (single.Height, single.Width);
            }
            return MeasureString (display, font, layoutArea);
        }

        // Vertical here means "rotate the run 90 degrees", the way RDL's WritingMode="tb-rl" and every
        // chart library render a sideways axis title -- see the remarks on DrawStringVertical.
        private static bool IsVertical (Majorsilence.Forms.Drawing.StringFormat? format) =>
            format is not null && format.FormatFlags.HasFlag (Majorsilence.Forms.Drawing.StringFormatFlags.DirectionVertical);

        // The text that will actually be drawn, per the format's hotkey handling. Measuring the raw string
        // would reserve room for ampersands that never render, so a button sized from its own caption comes
        // out wider than its text -- and a centred caption sits off-centre by the same amount.
        private static string WithoutHotkeyPrefix (string text, Majorsilence.Forms.Drawing.StringFormat? format) =>
            format is null || format.HotkeyPrefix == Majorsilence.Forms.Drawing.Text.HotkeyPrefix.None
                ? text
                : Mnemonics.Strip (text);

        /// <summary>
        /// Returns one <see cref="Majorsilence.Forms.Drawing.Region"/> per character range previously
        /// supplied to <see cref="Majorsilence.Forms.Drawing.StringFormat.SetMeasurableCharacterRanges"/>,
        /// describing where that range lands when <paramref name="text"/> is laid out inside
        /// <paramref name="layoutRect"/>.
        /// </summary>
        /// <remarks>
        /// A range that wraps across lines produces a region containing one rectangle per line, the same
        /// as GDI+. Wrapping is greedy word wrap against <c>layoutRect.Width</c>; a width of zero (or a
        /// <see cref="Majorsilence.Forms.Drawing.StringFormat"/> with no ranges set) means no wrapping.
        /// Text is positioned from the left edge of <paramref name="layoutRect"/> — horizontal
        /// <c>StringFormat.Alignment</c> is not applied to the measurement.
        /// </remarks>
        public Majorsilence.Forms.Drawing.Region[] MeasureCharacterRanges (
            string text,
            Majorsilence.Forms.Drawing.Font font,
            RectangleF layoutRect,
            Majorsilence.Forms.Drawing.StringFormat? stringFormat)
        {
            var ranges = stringFormat?.MeasurableCharacterRanges;
            if (ranges is null || ranges.Length == 0)
                return [];

            var result = new Majorsilence.Forms.Drawing.Region[ranges.Length];
            if (string.IsNullOrEmpty (text) || font is null) {
                for (var i = 0; i < ranges.Length; i++)
                    result[i] = EmptyRegion ();
                return result;
            }

            var skFont = font.GetSKFont ();
            var metrics = skFont.Metrics;
            var lineHeight = metrics.Descent - metrics.Ascent;
            var lines = LayoutLines (text, skFont, layoutRect.Width);

            for (var i = 0; i < ranges.Length; i++) {
                var region = EmptyRegion ();
                var rangeStart = ranges[i].First;
                var rangeEnd = rangeStart + ranges[i].Length;

                foreach (var (start, length, index) in lines) {
                    // Clip this range against the slice of text that landed on this line.
                    var segStart = Math.Max (rangeStart, start);
                    var segEnd = Math.Min (rangeEnd, start + length);
                    if (segEnd <= segStart)
                        continue;

                    var x = layoutRect.X + skFont.MeasureText (text.AsSpan (start, segStart - start));
                    var width = skFont.MeasureText (text.AsSpan (segStart, segEnd - segStart));
                    region.Union (new RectangleF (x, layoutRect.Y + index * lineHeight, width, lineHeight));
                }

                result[i] = region;
            }

            return result;
        }

        private static Majorsilence.Forms.Drawing.Region EmptyRegion ()
        {
            var region = new Majorsilence.Forms.Drawing.Region ();
            region.MakeEmpty ();
            return region;
        }

        /// <summary>
        /// Splits <paramref name="text"/> into laid-out lines, honoring explicit newlines and greedy
        /// word wrapping at <paramref name="maxWidth"/>. Each entry keeps its start index into the
        /// original string so character ranges can be mapped back onto it.
        /// </summary>
        private static List<(int Start, int Length, int Index)> LayoutLines (string text, SKFont font, float maxWidth)
        {
            var lines = new List<(int, int, int)> ();
            var lineIndex = 0;

            for (var paragraphStart = 0; paragraphStart <= text.Length;) {
                var newline = text.IndexOf ('\n', paragraphStart);
                var paragraphEnd = newline < 0 ? text.Length : newline;
                // A trailing \r belongs to the break, not to the measured text.
                var contentEnd = paragraphEnd > paragraphStart && text[paragraphEnd - 1] == '\r' ? paragraphEnd - 1 : paragraphEnd;

                if (maxWidth <= 0) {
                    lines.Add ((paragraphStart, contentEnd - paragraphStart, lineIndex++));
                } else {
                    var cursor = paragraphStart;
                    while (cursor <= contentEnd) {
                        var take = FitRun (text, cursor, contentEnd, font, maxWidth);
                        lines.Add ((cursor, take, lineIndex++));
                        cursor += take;
                        if (take == 0)
                            break;      // nothing fits at all — avoid spinning forever
                        // Consume the space the wrap happened on, so it isn't re-measured next line.
                        while (cursor < contentEnd && text[cursor] == ' ')
                            cursor++;
                    }
                }

                if (newline < 0)
                    break;
                paragraphStart = paragraphEnd + 1;
            }

            return lines;
        }

        /// <summary>
        /// Returns how many characters starting at <paramref name="start"/> fit within
        /// <paramref name="maxWidth"/>, breaking on the last space that fits. Falls back to a hard
        /// character break when a single word is itself wider than the line.
        /// </summary>
        private static int FitRun (string text, int start, int end, SKFont font, float maxWidth)
        {
            if (start >= end)
                return 0;

            var lastBreak = -1;
            for (var i = start; i < end; i++) {
                var width = font.MeasureText (text.AsSpan (start, i - start + 1));
                if (width > maxWidth) {
                    if (lastBreak >= 0)
                        return lastBreak - start;        // break at the last space that fit
                    return Math.Max (1, i - start);      // single over-long word: hard break
                }
                if (text[i] == ' ')
                    lastBreak = i;
            }
            return end - start;
        }

        // --- Transform stubs ---

        /// <summary>Gets the dots-per-inch (always 96 in Majorsilence.Forms).</summary>
        public float DpiX => 96f;

        /// <inheritdoc cref="DpiX"/>
        public float DpiY => 96f;

        /// <summary>Gets the bounding rectangle of the current clipping region.</summary>
        /// <remarks>
        /// Reports the canvas' actual clip. It used to report the control's bounds -- or Empty when the
        /// Graphics wrapped a bitmap rather than a control, as double-buffered painting does -- which
        /// broke the save-and-restore idiom <c>var last = g.ClipBounds; g.SetClip(...); g.SetClip(last);</c>:
        /// restoring an empty rectangle clipped away everything drawn from that point on.
        /// </remarks>
        public RectangleF ClipBounds {
            get {
                if (_canvas is { } c) {
                    // DeviceClipBounds is exact, where LocalClipBounds is outset by a pixel to be
                    // conservative about antialiasing; it ignores the transform, so it is only usable
                    // while there is none.
                    if (c.TotalMatrix.IsIdentity) {
                        var device = c.DeviceClipBounds;
                        return new RectangleF (device.Left, device.Top, device.Width, device.Height);
                    }

                    var bounds = c.LocalClipBounds;
                    return new RectangleF (bounds.Left, bounds.Top, bounds.Width, bounds.Height);
                }

                return _control is not null
                    ? new RectangleF (0, 0, _control.Width, _control.Height)
                    : RectangleF.Empty;
            }
        }

        /// <summary>Gets the visible clip bounds. Alias for ClipBounds in Majorsilence.Forms.</summary>
        public RectangleF VisibleClipBounds => ClipBounds;

        /// <summary>Gets whether the visible clip region is empty. Always false in Majorsilence.Forms.</summary>
        public bool IsVisibleClipEmpty => false;

        /// <summary>Gets or sets the unit of measure for page coordinates. Stub in Majorsilence.Forms — always Pixel.</summary>
        public Majorsilence.Forms.Drawing.GraphicsUnit PageUnit { get; set; } = Majorsilence.Forms.Drawing.GraphicsUnit.Pixel;

        /// <summary>Gets or sets the scaling factor for page-to-world coordinates. Stub in Majorsilence.Forms.</summary>
        public float PageScale { get; set; } = 1f;

        /// <summary>
        /// Saves the current graphics state (transform and clip) and returns a token that
        /// <see cref="Restore"/> can rewind to.
        /// </summary>
        public Majorsilence.Forms.Drawing.Drawing2D.GraphicsState Save ()
            => new Majorsilence.Forms.Drawing.Drawing2D.GraphicsState (_canvas?.Save () ?? 0);

        /// <summary>Restores the graphics state to a token previously returned by <see cref="Save"/>.</summary>
        public void Restore (Majorsilence.Forms.Drawing.Drawing2D.GraphicsState state)
        {
            if (_canvas is null)
                return;
            if (state is null)
                _canvas.Restore ();
            else
                _canvas.RestoreToCount (state.Count);
        }

        /// <summary>
        /// Opens a new graphics container: saves the current transform and clip so that
        /// <see cref="EndContainer"/> restores them exactly. Containers share the underlying canvas
        /// state stack with <see cref="Save"/>/<see cref="Restore"/>, so the two nest freely.
        /// </summary>
        public Majorsilence.Forms.Drawing.Drawing2D.GraphicsContainer BeginContainer ()
            => new Majorsilence.Forms.Drawing.Drawing2D.GraphicsContainer (_canvas?.Save () ?? 0);

        /// <summary>
        /// Opens a new graphics container that also maps <paramref name="srcrect"/> onto
        /// <paramref name="dstrect"/> — drawing inside the container uses <paramref name="srcrect"/>'s
        /// coordinate space and lands in <paramref name="dstrect"/>, clipped to it.
        /// </summary>
        public Majorsilence.Forms.Drawing.Drawing2D.GraphicsContainer BeginContainer (
            RectangleF dstrect, RectangleF srcrect, Majorsilence.Forms.Drawing.GraphicsUnit unit)
        {
            var container = BeginContainer ();
            if (_canvas is null || srcrect.Width == 0 || srcrect.Height == 0)
                return container;

            _canvas.ClipRect (new SKRect (dstrect.Left, dstrect.Top, dstrect.Right, dstrect.Bottom));
            _canvas.Translate (dstrect.Left, dstrect.Top);
            _canvas.Scale (dstrect.Width / srcrect.Width, dstrect.Height / srcrect.Height);
            _canvas.Translate (-srcrect.Left, -srcrect.Top);
            return container;
        }

        /// <inheritdoc cref="BeginContainer(RectangleF, RectangleF, Majorsilence.Forms.Drawing.GraphicsUnit)"/>
        public Majorsilence.Forms.Drawing.Drawing2D.GraphicsContainer BeginContainer (
            Rectangle dstrect, Rectangle srcrect, Majorsilence.Forms.Drawing.GraphicsUnit unit)
            => BeginContainer ((RectangleF)dstrect, (RectangleF)srcrect, unit);

        /// <summary>Closes a container opened by <see cref="BeginContainer()"/>, restoring the saved state.</summary>
        public void EndContainer (Majorsilence.Forms.Drawing.Drawing2D.GraphicsContainer container)
        {
            if (_canvas is null)
                return;
            if (container is null)
                _canvas.Restore ();
            else
                _canvas.RestoreToCount (container.Count);
        }

        /// <summary>Gets or sets the smoothing mode. Stub in Majorsilence.Forms (always anti-aliased).</summary>
        public SmoothingMode SmoothingMode { get; set; } = SmoothingMode.Default;

        /// <summary>Gets or sets the interpolation mode. Stub in Majorsilence.Forms.</summary>
        public InterpolationMode InterpolationMode { get; set; } = InterpolationMode.Default;

        /// <summary>Gets or sets the text rendering hint. Stub in Majorsilence.Forms.</summary>
        public Majorsilence.Forms.Drawing.Text.TextRenderingHint TextRenderingHint { get; set; } = Majorsilence.Forms.Drawing.Text.TextRenderingHint.SystemDefault;

        /// <summary>Gets or sets the compositing quality. Stub in Majorsilence.Forms.</summary>
        public Majorsilence.Forms.Drawing.Drawing2D.CompositingQuality CompositingQuality { get; set; } = Majorsilence.Forms.Drawing.Drawing2D.CompositingQuality.Default;

        /// <summary>Gets or sets the pixel offset mode. Stub in Majorsilence.Forms.</summary>
        public Majorsilence.Forms.Drawing.Drawing2D.PixelOffsetMode PixelOffsetMode { get; set; } = Majorsilence.Forms.Drawing.Drawing2D.PixelOffsetMode.Default;

        /// <summary>Gets or sets the compositing mode. Stub in Majorsilence.Forms.</summary>
        public Majorsilence.Forms.Drawing.Drawing2D.CompositingMode CompositingMode { get; set; } = Majorsilence.Forms.Drawing.Drawing2D.CompositingMode.SourceOver;

        /// <summary>Applies a scale transform.</summary>
        public void ScaleTransform (float sx, float sy) => _canvas?.Scale (sx, sy);

        /// <summary>Translates the coordinate origin.</summary>
        public void TranslateTransform (float dx, float dy) => _canvas?.Translate (dx, dy);

        /// <summary>Resets the world transform to identity.</summary>
        /// <remarks>
        /// Restores the baseline rather than calling SKCanvas.ResetMatrix, which would reset to the
        /// identity of the whole surface and so discard the control's own origin.
        /// </remarks>
        public void ResetTransform () => _canvas?.SetMatrix (_baseline);

        // --- Clipping ---

        /// <summary>
        /// The canvas save count captured before any clip was applied, i.e. the depth to unwind to in
        /// order to get back to an unclipped surface. Null until the first clip operation.
        /// </summary>
        private int? _clipBaseline;

        /// <summary>
        /// Unwinds any clip previously applied through this Graphics, leaving the canvas as it was
        /// before the first clip, and re-arms the baseline for the next one.
        /// </summary>
        /// <remarks>
        /// Skia's clip is cumulative -- <c>ClipRect</c> can only ever narrow, never widen -- whereas
        /// <c>Graphics.SetClip</c> replaces. Bridging the two needs save/restore: the baseline save
        /// records the unclipped state, and replacing the clip means restoring to it first.
        /// </remarks>
        private void RestoreClipBaseline ()
        {
            if (_canvas is null)
                return;

            // Only unwind when the canvas is still at the depth where the baseline was armed. Skia's
            // Restore pops MATRIX and clip together, for every frame above the target -- so firing a
            // baseline recorded at some other nesting level does not merely replace a clip, it pops
            // frames that belong to someone else. The paint pipeline saves around every child it
            // paints, and a themed renderer replaces the clip from inside those frames (save old clip,
            // clip to the rounded border path, draw, put the old clip back): a stale baseline from a
            // sibling's paint would unwind the child's whole canvas state -- observed as text drawn
            // with the translation popped and the clip resurrected from an icon's 32x32 glyph scope,
            // i.e. quick-rejected into nothing. A baseline at a foreign depth is abandoned instead:
            // within one scope (constant depth) replace still works, which is the GDI+ contract the
            // save-old/set-new/restore-old idiom actually exercises.
            if (_clipBaseline is { } depth && _canvas.SaveCount == _clipBaselineArmedAt)
                RestoreKeepingMatrix (depth);

            _clipBaseline = _canvas.Save ();
            _clipBaselineArmedAt = _canvas.SaveCount;
        }

        /// <summary>
        /// Unwinds the canvas to <paramref name="depth"/> while leaving the current transform in place.
        /// </summary>
        /// <remarks>
        /// Skia's Restore pops the MATRIX along with the clip, because both live in the same saved
        /// frame. In System.Drawing they are independent: changing the clip must not move the drawing,
        /// and <see cref="Transform"/> survives any number of SetClip calls. Since the clip is emulated
        /// with save/restore frames, the matrix has to be carried across by hand.
        ///
        /// Without this, replacing a clip silently reset the world transform -- and the failure was
        /// invisible in the common case, because code that clips usually is not also translating. Where
        /// both were in play (an SVG renderer, which restores the clip after every single element while
        /// a nested &lt;g transform&gt; is active) the first shape inside each group landed correctly and
        /// every later sibling drew at the untransformed origin.
        /// </remarks>
        private void RestoreKeepingMatrix (int depth)
        {
            if (_canvas is null)
                return;

            var matrix = _canvas.TotalMatrix;
            _canvas.RestoreToCount (depth);
            _canvas.SetMatrix (matrix);
        }

        // The canvas save-count right after the baseline save: the guard that tells "our save is the
        // top of the stack" apart from "someone saved (or restored) around us since".
        private int _clipBaselineArmedAt;

        /// <summary>Sets the clipping region to the given rectangle, replacing any current clip.</summary>
        public void SetClip (Rectangle rect) => SetClip ((RectangleF)rect);

        /// <summary>Resets the clipping region, so that drawing is unrestricted again.</summary>
        public void ResetClip ()
        {
            if (_canvas is null)
                return;

            if (_clipBaseline is { } depth && _canvas.SaveCount == _clipBaselineArmedAt) {
                RestoreKeepingMatrix (depth);
                _clipBaseline = null;
            } else {
                _clipBaseline = null;   // stale baseline: abandon rather than pop foreign frames
            }
        }

        /// <summary>Gets whether the current clipping region is empty.</summary>
        public bool IsClipEmpty => _canvas?.LocalClipBounds.IsEmpty ?? false;

        /// <summary>Translates the clipping region by the specified amounts.</summary>
        /// <remarks>
        /// Moves the clip alone. It used to translate the canvas, which moved every subsequent drawing
        /// operation along with it.
        /// </remarks>
        public void TranslateClip (float dx, float dy)
        {
            if (_canvas is null)
                return;

            var bounds = _canvas.LocalClipBounds;
            SetClip (new RectangleF (bounds.Left + dx, bounds.Top + dy, bounds.Width, bounds.Height));
        }

        /// <inheritdoc cref="TranslateClip(float, float)"/>
        public void TranslateClip (int dx, int dy) => TranslateClip ((float)dx, dy);

        /// <summary>Gets or sets the rendering origin, used to align dithering and hatch brushes.</summary>
        /// <remarks>
        /// Stored and round-tripped. Skia positions shaders by their own local matrix rather than by a
        /// device-wide origin, so hatch alignment does not follow this value.
        /// </remarks>
        public Point RenderingOrigin { get; set; }

        /// <summary>Gets or sets the gamma-correction value used when rendering text (0..12).</summary>
        /// <remarks>Stored and round-tripped; Skia's text rendering exposes no equivalent knob.</remarks>
        public int TextContrast { get; set; } = 4;

        /// <summary>
        /// Forces pending drawing to execute. A no-op: this canvas is not a batched device context, so
        /// there is never queued work to flush.
        /// </summary>
        public void Flush () { }

        /// <inheritdoc cref="Flush()"/>
        public void Flush (Majorsilence.Forms.Drawing.Drawing2D.FlushIntention intention) { }

        /// <summary>
        /// Returns the nearest color representable on this surface. Every surface here is 32bpp, so the
        /// color comes back unchanged rather than quantized to a palette.
        /// </summary>
        public Color GetNearestColor (Color color) => color;

        /// <summary>Transforms an array of points between two coordinate spaces.</summary>
        /// <remarks>
        /// World and page space coincide here (no page transform or page unit is applied to the canvas),
        /// so this applies the canvas's current transform going to device space and its inverse coming
        /// back, and is the identity between world and page.
        /// </remarks>
        public void TransformPoints (Majorsilence.Forms.Drawing.Drawing2D.CoordinateSpace destSpace,
            Majorsilence.Forms.Drawing.Drawing2D.CoordinateSpace srcSpace, PointF[] pts)
        {
            if (pts is null || _canvas is null || destSpace == srcSpace)
                return;

            var deviceIsDestination = destSpace == Majorsilence.Forms.Drawing.Drawing2D.CoordinateSpace.Device;
            var deviceIsSource = srcSpace == Majorsilence.Forms.Drawing.Drawing2D.CoordinateSpace.Device;
            if (!deviceIsDestination && !deviceIsSource)
                return;     // world <-> page is the identity here

            var matrix = _canvas.TotalMatrix;
            if (deviceIsSource && !matrix.TryInvert (out matrix))
                return;

            for (var i = 0; i < pts.Length; i++) {
                var mapped = matrix.MapPoint (new SKPoint (pts[i].X, pts[i].Y));
                pts[i] = new PointF (mapped.X, mapped.Y);
            }
        }

        /// <inheritdoc cref="TransformPoints(Majorsilence.Forms.Drawing.Drawing2D.CoordinateSpace, Majorsilence.Forms.Drawing.Drawing2D.CoordinateSpace, PointF[])"/>
        public void TransformPoints (Majorsilence.Forms.Drawing.Drawing2D.CoordinateSpace destSpace,
            Majorsilence.Forms.Drawing.Drawing2D.CoordinateSpace srcSpace, Point[] pts)
        {
            if (pts is null)
                return;
            var asFloat = Array.ConvertAll (pts, p => new PointF (p.X, p.Y));
            TransformPoints (destSpace, srcSpace, asFloat);
            for (var i = 0; i < pts.Length; i++)
                pts[i] = new Point ((int)Math.Round (asFloat[i].X), (int)Math.Round (asFloat[i].Y));
        }

        /// <summary>Fills the interior of a region using the specified brush.</summary>
        public void FillRegion (Majorsilence.Forms.Drawing.Brush brush, Majorsilence.Forms.Drawing.Region region)
        {
            if (_canvas is null || brush is null || region is null)
                return;

            using var paint = RentFillPaint (brush);
            _canvas.DrawRegion (region.GetSKRegion (), paint);
        }

        /// <summary>Draws a closed cardinal spline through the specified points.</summary>
        public void DrawClosedCurve (Majorsilence.Forms.Drawing.Pen pen, PointF[] points)
            => DrawClosedCurve (pen, points, 0.5f, Majorsilence.Forms.Drawing.Drawing2D.FillMode.Alternate);

        /// <inheritdoc cref="DrawClosedCurve(Majorsilence.Forms.Drawing.Pen, PointF[])"/>
        public void DrawClosedCurve (Majorsilence.Forms.Drawing.Pen pen, PointF[] points, float tension,
            Majorsilence.Forms.Drawing.Drawing2D.FillMode fillmode)
        {
            if (_canvas is null || pen is null || points is null || points.Length < 2)
                return;

            using var path = new Majorsilence.Forms.Drawing.Drawing2D.GraphicsPath (fillmode);
            path.AddClosedCurve (points, tension);
            DrawPath (pen, path);
        }

        /// <inheritdoc cref="DrawClosedCurve(Majorsilence.Forms.Drawing.Pen, PointF[])"/>
        public void DrawClosedCurve (Majorsilence.Forms.Drawing.Pen pen, Point[] points)
            => DrawClosedCurve (pen, Array.ConvertAll (points ?? [], p => new PointF (p.X, p.Y)));

        /// <summary>Fills the interior of a closed cardinal spline through the specified points.</summary>
        public void FillClosedCurve (Majorsilence.Forms.Drawing.Brush brush, PointF[] points)
            => FillClosedCurve (brush, points, Majorsilence.Forms.Drawing.Drawing2D.FillMode.Alternate);

        /// <inheritdoc cref="FillClosedCurve(Majorsilence.Forms.Drawing.Brush, PointF[])"/>
        public void FillClosedCurve (Majorsilence.Forms.Drawing.Brush brush, PointF[] points,
            Majorsilence.Forms.Drawing.Drawing2D.FillMode fillmode, float tension = 0.5f)
        {
            if (_canvas is null || brush is null || points is null || points.Length < 2)
                return;

            using var path = new Majorsilence.Forms.Drawing.Drawing2D.GraphicsPath (fillmode);
            path.AddClosedCurve (points, tension);
            FillPath (brush, path);
        }

        /// <inheritdoc cref="FillClosedCurve(Majorsilence.Forms.Drawing.Brush, PointF[])"/>
        public void FillClosedCurve (Majorsilence.Forms.Drawing.Brush brush, Point[] points)
            => FillClosedCurve (brush, Array.ConvertAll (points ?? [], p => new PointF (p.X, p.Y)));

        /// <summary>Draws the image at its native size, clipped to the specified rectangle.</summary>
        public void DrawImageUnscaledAndClipped (Majorsilence.Forms.Drawing.Image image, Rectangle rect)
        {
            if (_canvas is null || image?.GetSKBitmap () is not { } bitmap)
                return;

            _canvas.Save ();
            _canvas.ClipRect (new SKRect (rect.Left, rect.Top, rect.Right, rect.Bottom));
            _canvas.DrawBitmap (bitmap, rect.Left, rect.Top);
            _canvas.Restore ();
        }

        /// <summary>Callback invoked during a long-running <c>DrawImage</c> to allow cancellation.</summary>
        public delegate bool DrawImageAbort (IntPtr callbackdata);

        // ---------------------------------------------------------------------------------------
        // Overload completion (Phase 7 of docs/gdi-gap-plan.md).
        //
        // Every member below delegates to an existing implementation. They are not sugar: GDI+ has a
        // very wide overload surface and *.Designer.cs emits integer literals, so a missing shape is a
        // compile error in exactly the generated files a migration cannot hand-edit. The overload
        // scanner in tools/Majorsilence.Forms.GdiDiff found these; it could not see them before,
        // because a name-level check reports "DrawImage exists" and stops there.
        // ---------------------------------------------------------------------------------------

        // -- shapes: integer and RectangleF variants --

        /// <inheritdoc cref="DrawArc(Majorsilence.Forms.Drawing.Pen, float, float, float, float, float, float)"/>
        public void DrawArc (Majorsilence.Forms.Drawing.Pen pen, int x, int y, int width, int height, int startAngle, int sweepAngle)
            => DrawArc (pen, (float)x, y, width, height, startAngle, sweepAngle);

        /// <inheritdoc cref="DrawArc(Majorsilence.Forms.Drawing.Pen, float, float, float, float, float, float)"/>
        public void DrawArc (Majorsilence.Forms.Drawing.Pen pen, RectangleF rect, float startAngle, float sweepAngle)
            => DrawArc (pen, rect.X, rect.Y, rect.Width, rect.Height, startAngle, sweepAngle);

        /// <inheritdoc cref="DrawBezier(Majorsilence.Forms.Drawing.Pen, PointF, PointF, PointF, PointF)"/>
        public void DrawBezier (Majorsilence.Forms.Drawing.Pen pen, float x1, float y1, float x2, float y2,
            float x3, float y3, float x4, float y4)
            => DrawBezier (pen, new PointF (x1, y1), new PointF (x2, y2), new PointF (x3, y3), new PointF (x4, y4));

        /// <inheritdoc cref="DrawEllipse(Majorsilence.Forms.Drawing.Pen, float, float, float, float)"/>
        public void DrawEllipse (Majorsilence.Forms.Drawing.Pen pen, int x, int y, int width, int height)
            => DrawEllipse (pen, (float)x, y, width, height);

        /// <inheritdoc cref="FillEllipse(Majorsilence.Forms.Drawing.Brush, float, float, float, float)"/>
        public void FillEllipse (Majorsilence.Forms.Drawing.Brush brush, int x, int y, int width, int height)
            => FillEllipse (brush, (float)x, y, width, height);

        /// <summary>Draws a pie section defined by an ellipse and two radial lines.</summary>
        public void DrawPie (Majorsilence.Forms.Drawing.Pen pen, float x, float y, float width, float height,
            float startAngle, float sweepAngle)
        {
            if (_canvas is null || pen is null)
                return;
            using var path = new Majorsilence.Forms.Drawing.Drawing2D.GraphicsPath ();
            path.AddPie (x, y, width, height, startAngle, sweepAngle);
            DrawPath (pen, path);
        }

        /// <inheritdoc cref="DrawPie(Majorsilence.Forms.Drawing.Pen, float, float, float, float, float, float)"/>
        public void DrawPie (Majorsilence.Forms.Drawing.Pen pen, int x, int y, int width, int height, int startAngle, int sweepAngle)
            => DrawPie (pen, (float)x, y, width, height, startAngle, sweepAngle);

        /// <inheritdoc cref="DrawPie(Majorsilence.Forms.Drawing.Pen, float, float, float, float, float, float)"/>
        public void DrawPie (Majorsilence.Forms.Drawing.Pen pen, RectangleF rect, float startAngle, float sweepAngle)
            => DrawPie (pen, rect.X, rect.Y, rect.Width, rect.Height, startAngle, sweepAngle);

        /// <inheritdoc cref="FillPie(Majorsilence.Forms.Drawing.Brush, float, float, float, float, float, float)"/>
        public void FillPie (Majorsilence.Forms.Drawing.Brush brush, int x, int y, int width, int height, int startAngle, int sweepAngle)
            => FillPie (brush, (float)x, y, width, height, startAngle, sweepAngle);

        /// <summary>Fills a polygon using the specified fill mode.</summary>
        public void FillPolygon (Majorsilence.Forms.Drawing.Brush brush, PointF[] points,
            Majorsilence.Forms.Drawing.Drawing2D.FillMode fillMode)
        {
            if (_canvas is null || brush is null || points is null || points.Length < 2)
                return;
            using var path = new Majorsilence.Forms.Drawing.Drawing2D.GraphicsPath (fillMode);
            path.AddPolygon (points);
            FillPath (brush, path);
        }

        /// <inheritdoc cref="FillPolygon(Majorsilence.Forms.Drawing.Brush, PointF[], Majorsilence.Forms.Drawing.Drawing2D.FillMode)"/>
        public void FillPolygon (Majorsilence.Forms.Drawing.Brush brush, Point[] points,
            Majorsilence.Forms.Drawing.Drawing2D.FillMode fillMode)
            => FillPolygon (brush, Array.ConvertAll (points ?? [], p => new PointF (p.X, p.Y)), fillMode);

        /// <inheritdoc cref="FillPolygon(Majorsilence.Forms.Drawing.Brush, PointF[], Majorsilence.Forms.Drawing.Drawing2D.FillMode)"/>
        /// <remarks>Span overload, as upstream has: points are commonly built on the stack, and a span
        /// does not implicitly convert to the array the other overloads take.</remarks>
        public void FillPolygon (Majorsilence.Forms.Drawing.Brush brush, ReadOnlySpan<PointF> points,
            Majorsilence.Forms.Drawing.Drawing2D.FillMode fillMode = Majorsilence.Forms.Drawing.Drawing2D.FillMode.Alternate)
            => FillPolygon (brush, points.ToArray (), fillMode);

        /// <inheritdoc cref="FillPolygon(Majorsilence.Forms.Drawing.Brush, ReadOnlySpan{PointF}, Majorsilence.Forms.Drawing.Drawing2D.FillMode)"/>
        public void FillPolygon (Majorsilence.Forms.Drawing.Brush brush, ReadOnlySpan<Point> points,
            Majorsilence.Forms.Drawing.Drawing2D.FillMode fillMode = Majorsilence.Forms.Drawing.Drawing2D.FillMode.Alternate)
            => FillPolygon (brush, points.ToArray (), fillMode);

        // -- curves --

        /// <inheritdoc cref="DrawCurve(Majorsilence.Forms.Drawing.Pen, PointF[])"/>
        public void DrawCurve (Majorsilence.Forms.Drawing.Pen pen, PointF[] points, float tension)
            => DrawCurve (pen, points, 0, (points?.Length ?? 1) - 1, tension);

        /// <inheritdoc cref="DrawCurve(Majorsilence.Forms.Drawing.Pen, PointF[])"/>
        public void DrawCurve (Majorsilence.Forms.Drawing.Pen pen, PointF[] points, int offset, int numberOfSegments)
            => DrawCurve (pen, points, offset, numberOfSegments, 0.5f);

        /// <inheritdoc cref="DrawCurve(Majorsilence.Forms.Drawing.Pen, PointF[])"/>
        public void DrawCurve (Majorsilence.Forms.Drawing.Pen pen, Point[] points, float tension)
            => DrawCurve (pen, Array.ConvertAll (points ?? [], p => new PointF (p.X, p.Y)), tension);

        /// <inheritdoc cref="DrawCurve(Majorsilence.Forms.Drawing.Pen, PointF[])"/>
        public void DrawCurve (Majorsilence.Forms.Drawing.Pen pen, Point[] points, int offset, int numberOfSegments, float tension)
            => DrawCurve (pen, Array.ConvertAll (points ?? [], p => new PointF (p.X, p.Y)), offset, numberOfSegments, tension);

        /// <inheritdoc cref="DrawClosedCurve(Majorsilence.Forms.Drawing.Pen, PointF[])"/>
        public void DrawClosedCurve (Majorsilence.Forms.Drawing.Pen pen, Point[] points, float tension,
            Majorsilence.Forms.Drawing.Drawing2D.FillMode fillmode)
            => DrawClosedCurve (pen, Array.ConvertAll (points ?? [], p => new PointF (p.X, p.Y)), tension, fillmode);

        /// <inheritdoc cref="FillClosedCurve(Majorsilence.Forms.Drawing.Brush, PointF[])"/>
        public void FillClosedCurve (Majorsilence.Forms.Drawing.Brush brush, Point[] points,
            Majorsilence.Forms.Drawing.Drawing2D.FillMode fillmode)
            => FillClosedCurve (brush, Array.ConvertAll (points ?? [], p => new PointF (p.X, p.Y)), fillmode);

        /// <inheritdoc cref="FillClosedCurve(Majorsilence.Forms.Drawing.Brush, PointF[])"/>
        public void FillClosedCurve (Majorsilence.Forms.Drawing.Brush brush, Point[] points,
            Majorsilence.Forms.Drawing.Drawing2D.FillMode fillmode, float tension)
            => FillClosedCurve (brush, Array.ConvertAll (points ?? [], p => new PointF (p.X, p.Y)), fillmode, tension);

        // -- images --

        /// <inheritdoc cref="DrawImage(Majorsilence.Forms.Drawing.Image, int, int)"/>
        public void DrawImage (Majorsilence.Forms.Drawing.Image image, float x, float y)
            => DrawImage (image, x, y, image?.Width ?? 0, image?.Height ?? 0);

        /// <inheritdoc cref="DrawImage(Majorsilence.Forms.Drawing.Image, int, int)"/>
        public void DrawImage (Majorsilence.Forms.Drawing.Image image, PointF point)
            => DrawImage (image, point.X, point.Y);

        /// <inheritdoc cref="DrawImage(Majorsilence.Forms.Drawing.Image, int, int)"/>
        public void DrawImage (Majorsilence.Forms.Drawing.Image image, Point point)
            => DrawImage (image, point.X, point.Y);

        /// <inheritdoc cref="DrawImage(Majorsilence.Forms.Drawing.Image, Rectangle, Rectangle, Majorsilence.Forms.Drawing.GraphicsUnit)"/>
        public void DrawImage (Majorsilence.Forms.Drawing.Image image, float x, float y, RectangleF srcRect,
            Majorsilence.Forms.Drawing.GraphicsUnit srcUnit)
            => DrawImage (image, new RectangleF (x, y, srcRect.Width, srcRect.Height), srcRect, srcUnit);

        /// <inheritdoc cref="DrawImage(Majorsilence.Forms.Drawing.Image, Rectangle, Rectangle, Majorsilence.Forms.Drawing.GraphicsUnit)"/>
        public void DrawImage (Majorsilence.Forms.Drawing.Image image, int x, int y, Rectangle srcRect,
            Majorsilence.Forms.Drawing.GraphicsUnit srcUnit)
            => DrawImage (image, new Rectangle (x, y, srcRect.Width, srcRect.Height), srcRect, srcUnit);

        /// <inheritdoc cref="DrawImage(Majorsilence.Forms.Drawing.Image, Rectangle, Rectangle, Majorsilence.Forms.Drawing.GraphicsUnit)"/>
        public void DrawImage (Majorsilence.Forms.Drawing.Image image, Rectangle destRect, float srcX, float srcY,
            float srcWidth, float srcHeight, Majorsilence.Forms.Drawing.GraphicsUnit srcUnit)
            => DrawImage (image, (RectangleF)destRect, new RectangleF (srcX, srcY, srcWidth, srcHeight), srcUnit);

        /// <inheritdoc cref="DrawImage(Majorsilence.Forms.Drawing.Image, Rectangle, Rectangle, Majorsilence.Forms.Drawing.GraphicsUnit)"/>
        public void DrawImage (Majorsilence.Forms.Drawing.Image image, Rectangle destRect, int srcX, int srcY,
            int srcWidth, int srcHeight, Majorsilence.Forms.Drawing.GraphicsUnit srcUnit)
            => DrawImage (image, destRect, new Rectangle (srcX, srcY, srcWidth, srcHeight), srcUnit);

        /// <summary>
        /// Draws an image into the parallelogram defined by three destination points (upper-left,
        /// upper-right, lower-left).
        /// </summary>
        /// <remarks>
        /// The affine transform implied by the three points is applied to the canvas, so rotation and
        /// skew render correctly rather than being reduced to the bounding box.
        /// </remarks>
        public void DrawImage (Majorsilence.Forms.Drawing.Image image, PointF[] destPoints)
            => DrawImageParallelogram (image, destPoints, null, null);

        /// <summary>
        /// Shared implementation of the three-point (parallelogram) DrawImage family, honouring the
        /// source rectangle and the color adjustments when given.
        /// </summary>
        /// <remarks>
        /// The <paramref name="imageAttrs"/> part matters more than a color-remapping corner case
        /// suggests: this is the overload GDI+ callers use to composite a layer at partial opacity,
        /// by handing it an alpha-scaling <c>ColorMatrix</c>. While the attributes were dropped, such
        /// a layer drew fully opaque -- so an element asking for <c>opacity="0"</c> rendered as a
        /// solid block of its fill colour instead of vanishing.
        /// </remarks>
        private void DrawImageParallelogram (Majorsilence.Forms.Drawing.Image image, PointF[] destPoints,
            RectangleF? srcRect, Majorsilence.Forms.Drawing.Imaging.ImageAttributes? imageAttrs)
        {
            if (_canvas is null || image?.GetSKBitmap () is not { } bitmap || destPoints is null || destPoints.Length < 3)
                return;

            // Lookup-style adjustments (transparent color key, remap table) cannot be expressed as a
            // channel filter, so they are baked into a copy first; the matrix and gamma ride on the paint.
            using var adjusted = imageAttrs?.ApplyPixelAdjustments (bitmap);
            var source = adjusted ?? bitmap;

            using var colorFilter = imageAttrs?.ToSKColorFilter ();
            using var paint = colorFilter is null ? null : new SKPaint { ColorFilter = colorFilter, IsAntialias = true };

            var src = srcRect is { Width: > 0, Height: > 0 } r
                ? new SKRect (r.Left, r.Top, r.Right, r.Bottom)
                : new SKRect (0, 0, source.Width, source.Height);

            // Map the source rectangle onto the parallelogram: the two edge vectors from the
            // upper-left corner give the matrix columns directly.
            var origin = destPoints[0];
            var xAxis = new PointF (destPoints[1].X - origin.X, destPoints[1].Y - origin.Y);
            var yAxis = new PointF (destPoints[2].X - origin.X, destPoints[2].Y - origin.Y);
            var width = Math.Max (1f, src.Width);
            var height = Math.Max (1f, src.Height);

            var matrix = new SKMatrix (
                xAxis.X / width, yAxis.X / height, origin.X,
                xAxis.Y / width, yAxis.Y / height, origin.Y,
                0, 0, 1);

            _canvas.Save ();
            _canvas.Concat (matrix);
            _canvas.DrawBitmap (source, src, new SKRect (0, 0, width, height), paint);
            _canvas.Restore ();
        }

        /// <inheritdoc cref="DrawImage(Majorsilence.Forms.Drawing.Image, PointF[])"/>
        public void DrawImage (Majorsilence.Forms.Drawing.Image image, Point[] destPoints)
            => DrawImage (image, ToPointF (destPoints));

        private static PointF[] ToPointF (Point[]? points)
            => Array.ConvertAll (points ?? [], p => new PointF (p.X, p.Y));

        /// <inheritdoc cref="DrawImage(Majorsilence.Forms.Drawing.Image, PointF[])"/>
        /// <remarks>The unit is accepted for API compatibility; coordinates are treated as pixels.</remarks>
        public void DrawImage (Majorsilence.Forms.Drawing.Image image, PointF[] destPoints, RectangleF srcRect,
            Majorsilence.Forms.Drawing.GraphicsUnit srcUnit)
            => DrawImageParallelogram (image, destPoints, srcRect, null);

        /// <inheritdoc cref="DrawImage(Majorsilence.Forms.Drawing.Image, PointF[], RectangleF, Majorsilence.Forms.Drawing.GraphicsUnit)"/>
        public void DrawImage (Majorsilence.Forms.Drawing.Image image, PointF[] destPoints, RectangleF srcRect,
            Majorsilence.Forms.Drawing.GraphicsUnit srcUnit, Majorsilence.Forms.Drawing.Imaging.ImageAttributes? imageAttr)
            => DrawImageParallelogram (image, destPoints, srcRect, imageAttr);

        /// <inheritdoc cref="DrawImage(Majorsilence.Forms.Drawing.Image, PointF[], RectangleF, Majorsilence.Forms.Drawing.GraphicsUnit)"/>
        public void DrawImage (Majorsilence.Forms.Drawing.Image image, Point[] destPoints, Rectangle srcRect,
            Majorsilence.Forms.Drawing.GraphicsUnit srcUnit)
            => DrawImageParallelogram (image, ToPointF (destPoints), srcRect, null);

        /// <inheritdoc cref="DrawImage(Majorsilence.Forms.Drawing.Image, PointF[], RectangleF, Majorsilence.Forms.Drawing.GraphicsUnit)"/>
        public void DrawImage (Majorsilence.Forms.Drawing.Image image, Point[] destPoints, Rectangle srcRect,
            Majorsilence.Forms.Drawing.GraphicsUnit srcUnit, Majorsilence.Forms.Drawing.Imaging.ImageAttributes? imageAttr)
            => DrawImageParallelogram (image, ToPointF (destPoints), srcRect, imageAttr);

        // The abort-callback shapes. GDI+ polls the callback during a long draw; nothing here is
        // interruptible (a Skia bitmap draw is a single call), so the callback is accepted and never
        // invoked rather than being left absent and failing to compile.

        /// <inheritdoc cref="DrawImage(Majorsilence.Forms.Drawing.Image, PointF[], RectangleF, Majorsilence.Forms.Drawing.GraphicsUnit)"/>
        public void DrawImage (Majorsilence.Forms.Drawing.Image image, PointF[] destPoints, RectangleF srcRect,
            Majorsilence.Forms.Drawing.GraphicsUnit srcUnit, Majorsilence.Forms.Drawing.Imaging.ImageAttributes? imageAttr,
            DrawImageAbort? callback)
            => DrawImageParallelogram (image, destPoints, srcRect, imageAttr);

        /// <inheritdoc cref="DrawImage(Majorsilence.Forms.Drawing.Image, PointF[], RectangleF, Majorsilence.Forms.Drawing.GraphicsUnit)"/>
        public void DrawImage (Majorsilence.Forms.Drawing.Image image, PointF[] destPoints, RectangleF srcRect,
            Majorsilence.Forms.Drawing.GraphicsUnit srcUnit, Majorsilence.Forms.Drawing.Imaging.ImageAttributes? imageAttr,
            DrawImageAbort? callback, int callbackData)
            => DrawImageParallelogram (image, destPoints, srcRect, imageAttr);

        /// <inheritdoc cref="DrawImage(Majorsilence.Forms.Drawing.Image, PointF[], RectangleF, Majorsilence.Forms.Drawing.GraphicsUnit)"/>
        public void DrawImage (Majorsilence.Forms.Drawing.Image image, Point[] destPoints, Rectangle srcRect,
            Majorsilence.Forms.Drawing.GraphicsUnit srcUnit, Majorsilence.Forms.Drawing.Imaging.ImageAttributes? imageAttr,
            DrawImageAbort? callback)
            => DrawImageParallelogram (image, ToPointF (destPoints), srcRect, imageAttr);

        /// <inheritdoc cref="DrawImage(Majorsilence.Forms.Drawing.Image, PointF[], RectangleF, Majorsilence.Forms.Drawing.GraphicsUnit)"/>
        public void DrawImage (Majorsilence.Forms.Drawing.Image image, Point[] destPoints, Rectangle srcRect,
            Majorsilence.Forms.Drawing.GraphicsUnit srcUnit, Majorsilence.Forms.Drawing.Imaging.ImageAttributes? imageAttr,
            DrawImageAbort? callback, int callbackData)
            => DrawImageParallelogram (image, ToPointF (destPoints), srcRect, imageAttr);

        /// <inheritdoc cref="DrawImage(Majorsilence.Forms.Drawing.Image, Rectangle, int, int, int, int, Majorsilence.Forms.Drawing.GraphicsUnit)"/>
        public void DrawImage (Majorsilence.Forms.Drawing.Image image, Rectangle destRect, int srcX, int srcY,
            int srcWidth, int srcHeight, Majorsilence.Forms.Drawing.GraphicsUnit srcUnit,
            Majorsilence.Forms.Drawing.Imaging.ImageAttributes? imageAttr, DrawImageAbort? callback)
            => DrawImage (image, destRect, srcX, srcY, srcWidth, srcHeight, srcUnit);

        /// <inheritdoc cref="DrawImage(Majorsilence.Forms.Drawing.Image, Rectangle, int, int, int, int, Majorsilence.Forms.Drawing.GraphicsUnit)"/>
        public void DrawImage (Majorsilence.Forms.Drawing.Image image, Rectangle destRect, int srcX, int srcY,
            int srcWidth, int srcHeight, Majorsilence.Forms.Drawing.GraphicsUnit srcUnit,
            Majorsilence.Forms.Drawing.Imaging.ImageAttributes? imageAttr, DrawImageAbort? callback, IntPtr callbackData)
            => DrawImage (image, destRect, srcX, srcY, srcWidth, srcHeight, srcUnit);

        /// <inheritdoc cref="DrawImage(Majorsilence.Forms.Drawing.Image, Rectangle, float, float, float, float, Majorsilence.Forms.Drawing.GraphicsUnit)"/>
        public void DrawImage (Majorsilence.Forms.Drawing.Image image, Rectangle destRect, float srcX, float srcY,
            float srcWidth, float srcHeight, Majorsilence.Forms.Drawing.GraphicsUnit srcUnit,
            Majorsilence.Forms.Drawing.Imaging.ImageAttributes? imageAttr, DrawImageAbort? callback)
            => DrawImage (image, destRect, srcX, srcY, srcWidth, srcHeight, srcUnit);

        /// <inheritdoc cref="DrawImage(Majorsilence.Forms.Drawing.Image, Rectangle, float, float, float, float, Majorsilence.Forms.Drawing.GraphicsUnit)"/>
        public void DrawImage (Majorsilence.Forms.Drawing.Image image, Rectangle destRect, float srcX, float srcY,
            float srcWidth, float srcHeight, Majorsilence.Forms.Drawing.GraphicsUnit srcUnit,
            Majorsilence.Forms.Drawing.Imaging.ImageAttributes? imageAttr, DrawImageAbort? callback, IntPtr callbackData)
            => DrawImage (image, destRect, srcX, srcY, srcWidth, srcHeight, srcUnit);

        /// <inheritdoc cref="DrawImageUnscaled(Majorsilence.Forms.Drawing.Image, int, int)"/>
        public void DrawImageUnscaled (Majorsilence.Forms.Drawing.Image image, Point point)
            => DrawImageUnscaled (image, point.X, point.Y);

        /// <inheritdoc cref="DrawImageUnscaled(Majorsilence.Forms.Drawing.Image, int, int)"/>
        /// <remarks>The width and height are ignored, as in System.Drawing: "unscaled" means the image's own size.</remarks>
        public void DrawImageUnscaled (Majorsilence.Forms.Drawing.Image image, int x, int y, int width, int height)
            => DrawImageUnscaled (image, x, y);

        // -- clipping --

        /// <inheritdoc cref="SetClip(Rectangle)"/>
        public void SetClip (Rectangle rect, Majorsilence.Forms.Drawing.Drawing2D.CombineMode combineMode)
            => SetClip ((RectangleF)rect, combineMode);

        /// <inheritdoc cref="SetClip(RectangleF)"/>
        /// <remarks>
        /// Honours Replace, Intersect and Exclude. Union, Xor and Complement have no Skia equivalent and
        /// fall back to Replace.
        /// </remarks>
        public void SetClip (RectangleF rect, Majorsilence.Forms.Drawing.Drawing2D.CombineMode combineMode)
        {
            switch (combineMode) {
            case Majorsilence.Forms.Drawing.Drawing2D.CombineMode.Intersect:
                IntersectClip (rect);
                break;
            case Majorsilence.Forms.Drawing.Drawing2D.CombineMode.Exclude:
                ExcludeClip (Rectangle.Round (rect));
                break;
            default:
                SetClip (rect);
                break;
            }
        }

        /// <inheritdoc cref="SetClip(Majorsilence.Forms.Drawing.Drawing2D.GraphicsPath)"/>
        public void SetClip (Majorsilence.Forms.Drawing.Drawing2D.GraphicsPath path,
            Majorsilence.Forms.Drawing.Drawing2D.CombineMode combineMode)
            => SetClip (path);

        /// <inheritdoc cref="SetClip(Majorsilence.Forms.Drawing.Region)"/>
        public void SetClip (Majorsilence.Forms.Drawing.Region region,
            Majorsilence.Forms.Drawing.Drawing2D.CombineMode combineMode)
        {
            if (region is not null)
                SetClip (region.GetBounds (this), combineMode);
        }

        /// <summary>Sets this Graphics' clip to that of another Graphics.</summary>
        /// <remarks>
        /// Applies the source's current clip bounds. Skia exposes the clip as a bounding rectangle
        /// rather than as a transferable region object, so a non-rectangular source clip is applied as
        /// its bounds.
        /// </remarks>
        public void SetClip (Graphics graphics)
        {
            if (graphics?._canvas is null)
                return;
            var bounds = graphics._canvas.LocalClipBounds;
            SetClip (new RectangleF (bounds.Left, bounds.Top, bounds.Width, bounds.Height));
        }

        /// <inheritdoc cref="SetClip(Graphics)"/>
        public void SetClip (Graphics graphics, Majorsilence.Forms.Drawing.Drawing2D.CombineMode combineMode)
            => SetClip (graphics);

        /// <inheritdoc cref="IntersectClip(Rectangle)"/>
        public void IntersectClip (Majorsilence.Forms.Drawing.Region region) => SetClip (region);

        /// <summary>Returns whether the specified point is inside the visible clip region.</summary>
        public bool IsVisible (PointF point) => IsVisible (new RectangleF (point.X, point.Y, 1, 1));

        /// <inheritdoc cref="IsVisible(PointF)"/>
        public bool IsVisible (float x, float y) => IsVisible (new PointF (x, y));

        /// <inheritdoc cref="IsVisible(PointF)"/>
        public bool IsVisible (int x, int y) => IsVisible (new PointF (x, y));

        /// <inheritdoc cref="IsVisible(RectangleF)"/>
        public bool IsVisible (float x, float y, float width, float height)
            => IsVisible (new RectangleF (x, y, width, height));

        /// <inheritdoc cref="IsVisible(RectangleF)"/>
        public bool IsVisible (int x, int y, int width, int height)
            => IsVisible (new RectangleF (x, y, width, height));

        // -- transforms and measurement --

        /// <inheritdoc cref="ScaleTransform(float, float)"/>
        /// <remarks>
        /// The canvas can only prepend, so an <see cref="Majorsilence.Forms.Drawing.Drawing2D.MatrixOrder.Append"/>
        /// request is composed through the world <see cref="Transform"/> instead. Prepend is
        /// System.Drawing's default and stays on the direct canvas path.
        /// </remarks>
        public void ScaleTransform (float sx, float sy, Majorsilence.Forms.Drawing.Drawing2D.MatrixOrder order)
        {
            if (order == Majorsilence.Forms.Drawing.Drawing2D.MatrixOrder.Prepend) {
                ScaleTransform (sx, sy);
                return;
            }

            using var m = new Majorsilence.Forms.Drawing.Drawing2D.Matrix ();
            m.Scale (sx, sy);
            MultiplyTransform (m, order);
        }

        /// <inheritdoc cref="TranslateTransform(float, float)"/>
        /// <remarks>See <see cref="ScaleTransform(float, float, Majorsilence.Forms.Drawing.Drawing2D.MatrixOrder)"/> for the order parameter.</remarks>
        public void TranslateTransform (float dx, float dy, Majorsilence.Forms.Drawing.Drawing2D.MatrixOrder order)
        {
            if (order == Majorsilence.Forms.Drawing.Drawing2D.MatrixOrder.Prepend) {
                TranslateTransform (dx, dy);
                return;
            }

            using var m = new Majorsilence.Forms.Drawing.Drawing2D.Matrix ();
            m.Translate (dx, dy);
            MultiplyTransform (m, order);
        }

        /// <inheritdoc cref="MeasureString(string, Majorsilence.Forms.Drawing.Font)"/>
        public SizeF MeasureString (string text, Majorsilence.Forms.Drawing.Font font, int width)
            => MeasureString (text, font, width, null);

        /// <inheritdoc cref="MeasureString(string, Majorsilence.Forms.Drawing.Font)"/>
        /// <remarks>The origin does not affect the measured size; it is accepted for API compatibility.</remarks>
        public SizeF MeasureString (string text, Majorsilence.Forms.Drawing.Font font, PointF origin,
            Majorsilence.Forms.Drawing.StringFormat? stringFormat)
            => MeasureString (text, font, stringFormat);

        /// <summary>Measures the string and reports how much of it fit.</summary>
        public SizeF MeasureString (string text, Majorsilence.Forms.Drawing.Font font, SizeF layoutArea,
            Majorsilence.Forms.Drawing.StringFormat? stringFormat, out int charactersFitted, out int linesFilled)
        {
            var size = MeasureString (text, font, layoutArea, stringFormat);

            // Without a real line-breaking pass here, report the honest best case: everything fit on the
            // measured number of lines. MeasureCharacterRanges is the member that does real wrapping.
            charactersFitted = text?.Length ?? 0;
            var lineHeight = font is null ? 0f : MeasureString ("X", font).Height;
            linesFilled = lineHeight <= 0 ? 1 : Math.Max (1, (int)Math.Round (size.Height / lineHeight));
            return size;
        }

        /// <summary>Narrows the clipping region to its intersection with the given rectangle.</summary>
        public void IntersectClip (Rectangle rect) => IntersectClip ((RectangleF)rect);

        /// <inheritdoc cref="IntersectClip(Rectangle)"/>
        public void IntersectClip (RectangleF rect)
        {
            // Unlike SetClip this keeps the current clip, so it must not unwind to the baseline:
            // Skia's ClipRect already intersects.
            if (_clipBaseline is null && _canvas is not null) {
                _clipBaseline = _canvas.Save ();
                _clipBaselineArmedAt = _canvas.SaveCount;
            }
            _canvas?.ClipRect (new SKRect (rect.Left, rect.Top, rect.Right, rect.Bottom));
        }

        /// <summary>Gets or sets the clipping region.</summary>
        /// <remarks>
        /// Never null, as in System.Drawing: an unclipped surface reports an infinite region. It used to
        /// return null, which broke the standard save-and-restore idiom that custom painting is built on
        /// -- <c>var saved = g.Clip; g.SetClip(...); ...; g.Clip = saved;</c>. Combining against the null
        /// threw, and because that kind of drawing code often sits inside a broad catch, the rest of the
        /// paint was silently abandoned rather than reported: a control that drew its first few pieces
        /// and then simply stopped.
        /// </remarks>
        public Majorsilence.Forms.Drawing.Region Clip {
            // Read live from the canvas rather than cached, so that a clip applied between two reads is
            // reflected -- a stale snapshot here would restore the wrong clip.
            get => _canvas is { } c && !c.LocalClipBounds.IsEmpty
                ? new Majorsilence.Forms.Drawing.Region (Rectangle.Round (new RectangleF (
                    c.LocalClipBounds.Left, c.LocalClipBounds.Top, c.LocalClipBounds.Width, c.LocalClipBounds.Height)))
                : new Majorsilence.Forms.Drawing.Region ();
            set {
                if (value is not null)
                    SetClip (value);
            }
        }

        /// <summary>Excludes a rectangle from the clipping region.</summary>
        public void ExcludeClip (Rectangle rect)
        {
            if (_clipBaseline is null && _canvas is not null) {
                _clipBaseline = _canvas.Save ();
                _clipBaselineArmedAt = _canvas.SaveCount;
            }
            _canvas?.ClipRect (new SKRect (rect.Left, rect.Top, rect.Right, rect.Bottom),
                SKClipOperation.Difference);
        }

        /// <summary>Excludes a region from the clipping region, as its bounding rectangle.</summary>
        public void ExcludeClip (Majorsilence.Forms.Drawing.Region region)
        {
            if (region is not null)
                ExcludeClip (Rectangle.Round (region.GetBounds (this)));
        }

        /// <summary>Returns whether the specified point is within the clipping region. Always returns true in Majorsilence.Forms.</summary>
        public bool IsVisible (Point point) => true;

        /// <summary>Returns whether the specified rectangle is within the clipping region. Always returns true in Majorsilence.Forms.</summary>
        public bool IsVisible (Rectangle rect) => true;

        /// <summary>Returns whether the specified rectangle is within the clipping region. Always returns true in Majorsilence.Forms.</summary>
        public bool IsVisible (RectangleF rect) => true;

        /// <summary>Applies a matrix transform to the current world transform.</summary>
        public void MultiplyTransform (Majorsilence.Forms.Drawing.Drawing2D.Matrix matrix)
            => MultiplyTransform (matrix, Majorsilence.Forms.Drawing.Drawing2D.MatrixOrder.Prepend);

        /// <summary>Applies a matrix transform to the current world transform in the specified order.</summary>
        public void MultiplyTransform (Majorsilence.Forms.Drawing.Drawing2D.Matrix matrix,
            Majorsilence.Forms.Drawing.Drawing2D.MatrixOrder order)
        {
            if (_canvas is null || matrix is null) return;

            // Composed through Matrix rather than SKCanvas.Concat because Concat can only prepend;
            // MatrixOrder.Append needs the incoming matrix on the other side.
            var world = Transform;
            world.Multiply (matrix, order);
            Transform = world;
        }

        // --- Drawing operations (Skia-backed when canvas is available) ---

        /// <summary>Draws a Majorsilence.Forms.Drawing.Icon at the specified location. Converts to bitmap internally.</summary>
#pragma warning disable CA1416
        public void DrawIcon (Majorsilence.Forms.Drawing.Icon icon, int x, int y)
        {
            if (icon == null || _canvas == null) return;
            using var bmp = icon.ToBitmap ();
            using var skBmp = bmp.ToSKBitmap ();
            if (skBmp != null) _canvas.DrawBitmap (skBmp, new SKPoint (x, y));
        }

        /// <summary>Draws a Majorsilence.Forms.Drawing.Icon stretched to fill the destination rectangle.</summary>
        public void DrawIcon (Majorsilence.Forms.Drawing.Icon icon, Rectangle targetRect)
        {
            if (icon == null || _canvas == null) return;
            using var bmp = icon.ToBitmap ();
            using var skBmp = bmp.ToSKBitmap ();
            if (skBmp != null) _canvas.DrawBitmap (skBmp, new SKRect (targetRect.Left, targetRect.Top, targetRect.Right, targetRect.Bottom));
        }

        /// <summary>Draws an unscaled Majorsilence.Forms.Drawing.Icon at the specified location.</summary>
        public void DrawIconUnstretched (Majorsilence.Forms.Drawing.Icon icon, Rectangle targetRect) => DrawIcon (icon, targetRect.X, targetRect.Y);

        /// <summary>Returns the device context handle.</summary>
        /// <remarks>Zero: there is no GDI device context behind a Skia canvas. Reported truthfully
        /// rather than thrown, because callers pass the handle straight to a P/Invoke that is itself
        /// absent here, and a zero handle is what those APIs already have to check for. This pair is
        /// what satisfies <see cref="Majorsilence.Forms.Drawing.IDeviceContext"/>.</remarks>
        public IntPtr GetHdc () => IntPtr.Zero;

        /// <summary>Releases the device context handle. No-op in Majorsilence.Forms (stub).</summary>
        public void ReleaseHdc (IntPtr hdc) { }

        /// <summary>Releases the device context handle. No-op in Majorsilence.Forms (stub).</summary>
        public void ReleaseHdc () { }

        /// <summary>Copies the contents of the screen to this Graphics surface. Stub in Majorsilence.Forms.</summary>
        public void CopyFromScreen (int sourceX, int sourceY, int destinationX, int destinationY, Size blockRegionSize) { }

        /// <summary>Copies the contents of the screen to this Graphics surface. Stub in Majorsilence.Forms.</summary>
        public void CopyFromScreen (System.Drawing.Point upperLeftSource, System.Drawing.Point upperLeftDestination, Size blockRegionSize) { }

        /// <summary>Copies the contents of the screen to this Graphics surface. Stub in Majorsilence.Forms.</summary>
        /// <remarks>The raster-operation overload. Screen capture needs a platform screenshot API that no
        /// backend exposes yet, so like its siblings above this leaves the surface untouched rather than
        /// throwing -- a screenshot feature comes back blank instead of taking the app down.</remarks>
        public void CopyFromScreen (int sourceX, int sourceY, int destinationX, int destinationY,
            Size blockRegionSize, Majorsilence.Forms.Drawing.CopyPixelOperation copyPixelOperation) { }

        private static SKColor ToSKColor (System.Drawing.Color c) => new SKColor (c.R, c.G, c.B, c.A);

        /// <summary>
        /// Builds a fill <see cref="SKPaint"/> from a brush, honouring its actual type: a solid colour,
        /// or a gradient/hatch/texture shader. Previously every non-solid brush was collapsed to opaque
        /// black, which turned e.g. a soft <c>PathGradientBrush</c> glow into a solid black box. Anti-
        /// aliasing follows the current <see cref="SmoothingMode"/> so solid fills keep their prior look.
        /// Caller owns disposal.
        /// </summary>
        private SKPaint CreateFillPaint (Majorsilence.Forms.Drawing.Brush brush)
        {
            var paint = brush.CreatePaint ();
            paint.IsAntialias = SmoothingMode != Majorsilence.Forms.Drawing.Drawing2D.SmoothingMode.None;
            return paint;
        }

        // Reused across every SolidBrush fill call (FillRectangle, FillPolygon, DrawString's brush --
        // the overwhelming majority of fills) instead of allocating a fresh SKPaint via
        // Brush.CreatePaint() each time, same rationale and measurement as GetStrokePaint above.
        // Gradient/hatch/texture brushes are not handled here: their CreatePaint() builds an owned
        // Shader per call, which isn't safe to pool without a wider audit, so they still go through
        // CreateFillPaint's fresh-and-disposed path via RentFillPaint below.
        [ThreadStatic]
        private static SKPaint? t_fillPaint;

        private static SKPaint GetSolidFillPaint (Majorsilence.Forms.Drawing.SolidBrush brush, bool antialias)
        {
            var paint = t_fillPaint ??= new SKPaint ();
            paint.Color = ToSKColor (brush.Color);
            paint.Style = SKPaintStyle.Fill;
            paint.IsAntialias = antialias;
            return paint;
        }

        // A `using`-able handle around a fill paint that may be the shared pooled instance (SolidBrush)
        // or a fresh, Shader-owning one from CreateFillPaint (everything else) -- Dispose() only frees
        // the fresh one, so callers can `using var paint = RentFillPaint (brush);` exactly as they did
        // with CreateFillPaint and get the right disposal behaviour either way without knowing which.
        private readonly struct FillPaintHandle : IDisposable
        {
            private readonly SKPaint paint;
            private readonly bool ownsPaint;

            public FillPaintHandle (SKPaint paint, bool ownsPaint)
            {
                this.paint = paint;
                this.ownsPaint = ownsPaint;
            }

            public static implicit operator SKPaint (FillPaintHandle handle) => handle.paint;

            public void Dispose ()
            {
                if (ownsPaint)
                    paint.Dispose ();
            }
        }

        private FillPaintHandle RentFillPaint (Majorsilence.Forms.Drawing.Brush brush)
        {
            if (brush is Majorsilence.Forms.Drawing.SolidBrush solid) {
                var antialias = SmoothingMode != Majorsilence.Forms.Drawing.Drawing2D.SmoothingMode.None;
                return new FillPaintHandle (GetSolidFillPaint (solid, antialias), ownsPaint: false);
            }

            return new FillPaintHandle (CreateFillPaint (brush), ownsPaint: true);
        }

        private static float PenWidth (Majorsilence.Forms.Drawing.Pen pen) => pen.Width;
        private static SKColor PenColor (Majorsilence.Forms.Drawing.Pen pen) => ToSKColor (pen.Color);

        // Reused across every simple stroke draw call (DrawLine, DrawRectangle, DrawEllipse, etc.) on
        // this thread instead of allocating a fresh SKPaint each time. A benchmark
        // (benchmarks/Majorsilence.Forms.Benchmarks/PaintConstructionBenchmarks.cs) measured SKPaint
        // construction+disposal at ~12x the cost of just reassigning fields on an existing instance,
        // and a full canvas.DrawRect call at ~27% faster with zero allocation when pooled this way.
        // Safe to share because these call sites never nest (a paint is fully consumed by the single
        // draw call that follows before the method returns) and Skia copies SKPaint's plain scalar
        // fields into each draw command rather than holding a live reference to the object -- [ThreadStatic]
        // keeps it correct if painting ever happens on more than one thread (e.g. parallel headless tests).
        [ThreadStatic]
        private static SKPaint? t_strokePaint;

        private static SKPaint GetStrokePaint (Majorsilence.Forms.Drawing.Pen pen)
        {
            var paint = t_strokePaint ??= new SKPaint ();
            paint.Color = PenColor (pen);
            paint.Style = SKPaintStyle.Stroke;
            paint.StrokeWidth = PenWidth (pen);
            return paint;
        }

        /// <summary>Clears the canvas with the given color.</summary>
        public void Clear (System.Drawing.Color color) => _canvas?.Clear (ToSKColor (color));

        /// <summary>Fills a rectangle using a Majorsilence.Forms.Drawing.Brush.</summary>
        public void FillRectangle (Majorsilence.Forms.Drawing.Brush brush, Rectangle rect)
        {
            if (_canvas is null) return;
            using var paint = RentFillPaint (brush);
            _canvas.DrawRect (new SKRect (rect.Left, rect.Top, rect.Right, rect.Bottom), paint);
        }

        /// <summary>Fills a rectangle using a Majorsilence.Forms.Drawing.Brush.</summary>
        public void FillRectangle (Majorsilence.Forms.Drawing.Brush brush, RectangleF rect)
        {
            if (_canvas is null) return;
            using var paint = RentFillPaint (brush);
            _canvas.DrawRect (new SKRect (rect.Left, rect.Top, rect.Right, rect.Bottom), paint);
        }

        /// <summary>Fills a rectangle using a Majorsilence.Forms.Drawing.Brush.</summary>
        public void FillRectangle (Majorsilence.Forms.Drawing.Brush brush, float x, float y, float width, float height)
            => FillRectangle (brush, new RectangleF (x, y, width, height));

        /// <summary>Fills a rectangle using a Majorsilence.Forms.Drawing.Brush.</summary>
        public void FillRectangle (Majorsilence.Forms.Drawing.Brush brush, int x, int y, int width, int height)
            => FillRectangle (brush, new Rectangle (x, y, width, height));

        /// <summary>Draws a rectangle outline using a Majorsilence.Forms.Drawing.Pen.</summary>
        public void DrawRectangle (Majorsilence.Forms.Drawing.Pen pen, Rectangle rect)
        {
            if (_canvas is null) return;
            var paint = GetStrokePaint (pen);
            _canvas.DrawRect (new SKRect (rect.Left, rect.Top, rect.Right, rect.Bottom), paint);
        }

        /// <summary>Draws a rectangle outline using a Majorsilence.Forms.Drawing.Pen.</summary>
        public void DrawRectangle (Majorsilence.Forms.Drawing.Pen pen, int x, int y, int width, int height)
            => DrawRectangle (pen, new Rectangle (x, y, width, height));

        /// <summary>Draws a line using a Majorsilence.Forms.Drawing.Pen.</summary>
        public void DrawLine (Majorsilence.Forms.Drawing.Pen pen, Point p1, Point p2)
        {
            if (_canvas is null) return;
            var paint = GetStrokePaint (pen);
            _canvas.DrawLine (p1.X, p1.Y, p2.X, p2.Y, paint);
        }

        /// <summary>Draws a line using a Majorsilence.Forms.Drawing.Pen.</summary>
        public void DrawLine (Majorsilence.Forms.Drawing.Pen pen, int x1, int y1, int x2, int y2)
            => DrawLine (pen, new Point (x1, y1), new Point (x2, y2));

        /// <summary>Draws a line using a Majorsilence.Forms.Drawing.Pen.</summary>
        public void DrawLine (Majorsilence.Forms.Drawing.Pen pen, PointF p1, PointF p2)
        {
            if (_canvas is null) return;
            var paint = GetStrokePaint (pen);
            _canvas.DrawLine (p1.X, p1.Y, p2.X, p2.Y, paint);
        }

        /// <summary>Draws a line using a Majorsilence.Forms.Drawing.Pen and float coordinates.</summary>
        public void DrawLine (Majorsilence.Forms.Drawing.Pen pen, float x1, float y1, float x2, float y2)
            => DrawLine (pen, new PointF (x1, y1), new PointF (x2, y2));

        /// <summary>Draws a rectangle with float coordinates.</summary>
        public void DrawRectangle (Majorsilence.Forms.Drawing.Pen pen, float x, float y, float width, float height)
            => DrawRectangle (pen, new Rectangle ((int)x, (int)y, (int)width, (int)height));

        /// <summary>Draws a rectangle with a RectangleF.</summary>
        public void DrawRectangle (Majorsilence.Forms.Drawing.Pen pen, RectangleF rect)
            => DrawRectangle (pen, new Rectangle ((int)rect.X, (int)rect.Y, (int)rect.Width, (int)rect.Height));

        /// <summary>Draws an arc.</summary>
        public void DrawArc (Majorsilence.Forms.Drawing.Pen pen, Rectangle rect, float startAngle, float sweepAngle)
        {
            if (_canvas is null) return;
            var paint = GetStrokePaint (pen);
            using var path = new SKPath ();
            path.AddArc (new SKRect (rect.Left, rect.Top, rect.Right, rect.Bottom), startAngle, sweepAngle);
            _canvas.DrawPath (path, paint);
        }

        /// <summary>Draws an arc with float coordinates.</summary>
        public void DrawArc (Majorsilence.Forms.Drawing.Pen pen, float x, float y, float width, float height, float startAngle, float sweepAngle)
            => DrawArc (pen, new Rectangle ((int)x, (int)y, (int)width, (int)height), startAngle, sweepAngle);

        /// <summary>Draws a pie section.</summary>
        public void DrawPie (Majorsilence.Forms.Drawing.Pen pen, Rectangle rect, float startAngle, float sweepAngle)
        {
            if (_canvas is null) return;
            var paint = GetStrokePaint (pen);
            using var path = new SKPath ();
            path.MoveTo (rect.X + rect.Width / 2f, rect.Y + rect.Height / 2f);
            path.AddArc (new SKRect (rect.Left, rect.Top, rect.Right, rect.Bottom), startAngle, sweepAngle);
            path.Close ();
            _canvas.DrawPath (path, paint);
        }

        /// <summary>Fills a pie section.</summary>
        public void FillPie (Majorsilence.Forms.Drawing.Brush brush, Rectangle rect, float startAngle, float sweepAngle)
        {
            if (_canvas is null) return;
            using var paint = RentFillPaint (brush);
            using var path = new SKPath ();
            path.MoveTo (rect.X + rect.Width / 2f, rect.Y + rect.Height / 2f);
            path.AddArc (new SKRect (rect.Left, rect.Top, rect.Right, rect.Bottom), startAngle, sweepAngle);
            path.Close ();
            _canvas.DrawPath (path, paint);
        }

        /// <summary>Fills a pie section with float coordinates.</summary>
        public void FillPie (Majorsilence.Forms.Drawing.Brush brush, float x, float y, float width, float height, float startAngle, float sweepAngle)
            => FillPie (brush, new Rectangle ((int)x, (int)y, (int)width, (int)height), startAngle, sweepAngle);

        /// <summary>Draws a cubic Bezier curve.</summary>
        public void DrawBezier (Majorsilence.Forms.Drawing.Pen pen, PointF pt1, PointF pt2, PointF pt3, PointF pt4)
        {
            if (_canvas is null) return;
            var paint = GetStrokePaint (pen);
            using var path = new SKPath ();
            path.MoveTo (pt1.X, pt1.Y);
            path.CubicTo (pt2.X, pt2.Y, pt3.X, pt3.Y, pt4.X, pt4.Y);
            _canvas.DrawPath (path, paint);
        }

        /// <summary>Draws a cubic Bezier curve using integer Point coordinates.</summary>
        public void DrawBezier (Majorsilence.Forms.Drawing.Pen pen, Point pt1, Point pt2, Point pt3, Point pt4)
            => DrawBezier (pen, new PointF (pt1.X, pt1.Y), new PointF (pt2.X, pt2.Y), new PointF (pt3.X, pt3.Y), new PointF (pt4.X, pt4.Y));

        /// <summary>Draws multiple cubic Bezier curves.</summary>
        public void DrawBeziers (Majorsilence.Forms.Drawing.Pen pen, PointF[] points)
        {
            if (_canvas is null || points.Length < 4) return;
            for (int i = 0; i + 3 < points.Length; i += 3)
                DrawBezier (pen, points[i], points[i + 1], points[i + 2], points[i + 3]);
        }

        /// <summary>Draws multiple cubic Bezier curves using integer Point coordinates.</summary>
        public void DrawBeziers (Majorsilence.Forms.Drawing.Pen pen, Point[] points)
        {
            if (_canvas is null || points.Length < 4) return;
            for (int i = 0; i + 3 < points.Length; i += 3)
                DrawBezier (pen, points[i], points[i + 1], points[i + 2], points[i + 3]);
        }

        /// <summary>Draws a cardinal spline curve through the specified points.</summary>
        public void DrawCurve (Majorsilence.Forms.Drawing.Pen pen, PointF[] points)
        {
            if (_canvas is null || points.Length < 2) return;
            var paint = GetStrokePaint (pen);
            using var path = new SKPath ();
            path.MoveTo (points[0].X, points[0].Y);
            for (int i = 1; i < points.Length; i++) path.LineTo (points[i].X, points[i].Y);
            _canvas.DrawPath (path, paint);
        }

        /// <summary>Draws a cardinal spline curve using integer Point coordinates.</summary>
        public void DrawCurve (Majorsilence.Forms.Drawing.Pen pen, Point[] points)
        {
            if (_canvas is null || points.Length < 2) return;
            var paint = GetStrokePaint (pen);
            using var path = new SKPath ();
            path.MoveTo (points[0].X, points[0].Y);
            for (int i = 1; i < points.Length; i++) path.LineTo (points[i].X, points[i].Y);
            _canvas.DrawPath (path, paint);
        }

        /// <summary>
        /// Draws a segment of a cardinal spline through <paramref name="points"/>, starting at
        /// <paramref name="offset"/> and spanning <paramref name="numberOfSegments"/> segments,
        /// with the curve's tightness controlled by <paramref name="tension"/> (0 = straight
        /// lines between points, matching System.Drawing.Graphics.DrawCurve's default of 0.5).
        /// Unlike the plain (Pen, PointF[]) overload above (which just connects points with
        /// straight lines), this is a real Catmull-Rom spline through the points.
        /// </summary>
        public void DrawCurve (Majorsilence.Forms.Drawing.Pen pen, PointF[] points, int offset, int numberOfSegments, float tension)
        {
            if (_canvas is null || points.Length < 2 || numberOfSegments < 1) return;

            int last = offset + numberOfSegments;
            if (last >= points.Length) last = points.Length - 1;
            if (offset < 0 || offset >= last) return;

            var paint = GetStrokePaint (pen);
            using var path = new SKPath ();
            path.MoveTo (points[offset].X, points[offset].Y);

            // Catmull-Rom, converted to tension-scaled tangents (matches GDI+'s interpretation
            // of the `tension` parameter): for each segment P1->P2, use the neighboring points
            // P0 and P3 (clamped to the array ends) to compute tangents, subdivided into a fixed
            // number of steps per segment for a smooth curve.
            const int stepsPerSegment = 24;
            for (int i = offset; i < last; i++)
            {
                var p0 = points[System.Math.Max (i - 1, 0)];
                var p1 = points[i];
                var p2 = points[i + 1];
                var p3 = points[System.Math.Min (i + 2, points.Length - 1)];

                for (int s = 1; s <= stepsPerSegment; s++)
                {
                    float t = s / (float)stepsPerSegment;
                    float t2 = t * t;
                    float t3 = t2 * t;

                    float m0x = tension * (p2.X - p0.X);
                    float m0y = tension * (p2.Y - p0.Y);
                    float m1x = tension * (p3.X - p1.X);
                    float m1y = tension * (p3.Y - p1.Y);

                    float h00 = 2 * t3 - 3 * t2 + 1;
                    float h10 = t3 - 2 * t2 + t;
                    float h01 = -2 * t3 + 3 * t2;
                    float h11 = t3 - t2;

                    float x = h00 * p1.X + h10 * m0x + h01 * p2.X + h11 * m1x;
                    float y = h00 * p1.Y + h10 * m0y + h01 * p2.Y + h11 * m1y;

                    path.LineTo (x, y);
                }
            }

            _canvas.DrawPath (path, paint);
        }

        /// <summary>Fills an ellipse using a Majorsilence.Forms.Drawing.Brush.</summary>
        public void FillEllipse (Majorsilence.Forms.Drawing.Brush brush, Rectangle rect)
        {
            if (_canvas is null) return;
            using var paint = RentFillPaint (brush);
            _canvas.DrawOval (new SKRect (rect.Left, rect.Top, rect.Right, rect.Bottom), paint);
        }

        /// <summary>Fills an ellipse using a Majorsilence.Forms.Drawing.Brush.</summary>
        public void FillEllipse (Majorsilence.Forms.Drawing.Brush brush, float x, float y, float width, float height)
            => FillEllipse (brush, new Rectangle ((int)x, (int)y, (int)width, (int)height));

        /// <summary>Draws an ellipse outline using a Majorsilence.Forms.Drawing.Pen.</summary>
        public void DrawEllipse (Majorsilence.Forms.Drawing.Pen pen, Rectangle rect)
        {
            if (_canvas is null) return;
            var paint = GetStrokePaint (pen);
            _canvas.DrawOval (new SKRect (rect.Left, rect.Top, rect.Right, rect.Bottom), paint);
        }

        /// <summary>Draws an ellipse outline using a Majorsilence.Forms.Drawing.Pen (RectangleF overload).</summary>
        public void DrawEllipse (Majorsilence.Forms.Drawing.Pen pen, RectangleF rect)
            => DrawEllipse (pen, new Rectangle ((int)rect.X, (int)rect.Y, (int)rect.Width, (int)rect.Height));

        /// <summary>Draws an ellipse outline using a Majorsilence.Forms.Drawing.Pen.</summary>
        public void DrawEllipse (Majorsilence.Forms.Drawing.Pen pen, float x, float y, float width, float height)
            => DrawEllipse (pen, new Rectangle ((int)x, (int)y, (int)width, (int)height));

        /// <summary>Fills an ellipse using a Majorsilence.Forms.Drawing.Brush (RectangleF overload).</summary>
        public void FillEllipse (Majorsilence.Forms.Drawing.Brush brush, RectangleF rect)
            => FillEllipse (brush, new Rectangle ((int)rect.X, (int)rect.Y, (int)rect.Width, (int)rect.Height));

        /// <summary>Fills a closed polygon.</summary>
        public void FillPolygon (Majorsilence.Forms.Drawing.Brush brush, Point[] points)
        {
            if (_canvas is null || points.Length < 2) return;
            var path = new SKPath ();
            path.MoveTo (points[0].X, points[0].Y);
            for (int i = 1; i < points.Length; i++) path.LineTo (points[i].X, points[i].Y);
            path.Close ();
            using var paint = RentFillPaint (brush);
            _canvas.DrawPath (path, paint);
        }

        /// <summary>Draws a closed polygon outline.</summary>
        public void DrawPolygon (Majorsilence.Forms.Drawing.Pen pen, Point[] points)
        {
            if (_canvas is null || points.Length < 2) return;
            var path = new SKPath ();
            path.MoveTo (points[0].X, points[0].Y);
            for (int i = 1; i < points.Length; i++) path.LineTo (points[i].X, points[i].Y);
            path.Close ();
            var paint = GetStrokePaint (pen);
            _canvas.DrawPath (path, paint);
        }

        /// <summary>Draws a closed polygon outline using PointF coordinates.</summary>
        public void DrawPolygon (Majorsilence.Forms.Drawing.Pen pen, PointF[] points)
        {
            if (_canvas is null || points.Length < 2) return;
            var path = new SKPath ();
            path.MoveTo (points[0].X, points[0].Y);
            for (int i = 1; i < points.Length; i++) path.LineTo (points[i].X, points[i].Y);
            path.Close ();
            var paint = GetStrokePaint (pen);
            _canvas.DrawPath (path, paint);
        }

        /// <summary>Fills a closed polygon using PointF coordinates.</summary>
        public void FillPolygon (Majorsilence.Forms.Drawing.Brush brush, PointF[] points)
        {
            if (_canvas is null || points.Length < 2) return;
            var path = new SKPath ();
            path.MoveTo (points[0].X, points[0].Y);
            for (int i = 1; i < points.Length; i++) path.LineTo (points[i].X, points[i].Y);
            path.Close ();
            using var paint = RentFillPaint (brush);
            _canvas.DrawPath (path, paint);
        }

        /// <summary>Draws an open polyline.</summary>
        public void DrawLines (Majorsilence.Forms.Drawing.Pen pen, Point[] points)
        {
            if (_canvas is null || points.Length < 2) return;
            var paint = GetStrokePaint (pen);
            for (int i = 1; i < points.Length; i++)
                _canvas.DrawLine (points[i - 1].X, points[i - 1].Y, points[i].X, points[i].Y, paint);
        }

        /// <summary>Draws an open polyline using floating-point coordinates.</summary>
        public void DrawLines (Majorsilence.Forms.Drawing.Pen pen, PointF[] points)
        {
            if (_canvas is null || points.Length < 2) return;
            var paint = GetStrokePaint (pen);
            for (int i = 1; i < points.Length; i++)
                _canvas.DrawLine (points[i - 1].X, points[i - 1].Y, points[i].X, points[i].Y, paint);
        }

        /// <summary>Draws a series of rectangles.</summary>
        public void DrawRectangles (Majorsilence.Forms.Drawing.Pen pen, Rectangle[] rects)
        {
            foreach (var r in rects) DrawRectangle (pen, r);
        }

        /// <summary>Draws a series of rectangles using floating-point coordinates.</summary>
        public void DrawRectangles (Majorsilence.Forms.Drawing.Pen pen, RectangleF[] rects)
        {
            foreach (var r in rects) DrawRectangle (pen, new Rectangle ((int)r.X, (int)r.Y, (int)r.Width, (int)r.Height));
        }

        /// <summary>Fills a series of rectangles.</summary>
        public void FillRectangles (Majorsilence.Forms.Drawing.Brush brush, Rectangle[] rects)
        {
            foreach (var r in rects) FillRectangle (brush, r);
        }

        /// <summary>Fills a series of rectangles using floating-point coordinates.</summary>
        public void FillRectangles (Majorsilence.Forms.Drawing.Brush brush, RectangleF[] rects)
        {
            foreach (var r in rects) FillRectangle (brush, r);
        }

        /// <summary>Draws a string with the given Majorsilence.Forms.Drawing.Font and Brush.</summary>
        public void DrawString (string text, Majorsilence.Forms.Drawing.Font font, Majorsilence.Forms.Drawing.Brush brush, float x, float y)
        {
            if (_canvas is null || string.IsNullOrEmpty (text)) return;

            // font.GetSKFont() already lazily builds and caches exactly this SKFont (same typeface
            // resolution TypefaceCache.Resolve used to redo here, same PixelSize); reusing it turns
            // one allocation per DrawString call into zero, for anything that draws the same Font
            // repeatedly (Binary Rain's per-glyph rain, any custom-painted label). It is owned by
            // the Font, not this call, so it must not be disposed here.
            // A solid brush -- overwhelmingly the case -- goes through the same RichTextKit path the
            // library's own renderers and MeasureString already use, because that path does FONT FALLBACK
            // and a bare SKFont does not. Drawing straight to SKCanvas.DrawText renders any codepoint the
            // chosen typeface lacks as tofu, while MeasureString (RichTextKit) measured it correctly --
            // so a CJK or emoji string laid out at the right size and then drew as a row of boxes. Found
            // with a Chinese control library, where every label was tofu.
            //
            // Routing both sides through one machinery is also what keeps them from disagreeing again.
            if (brush is Majorsilence.Forms.Drawing.SolidBrush solid) {
                // GDI+ DrawString(text, font, brush, x, y) is unbounded, so the layout width has to be
                // effectively infinite -- but not int.MaxValue, which overflows RichTextKit's own
                // arithmetic. This is far wider than any real surface and the clip inside DrawText keeps
                // it honest.
                const int Unbounded = 1 << 20;

                _canvas.DrawText (text, font.GetSKTypeface (), (int)System.Math.Round (font.PixelSize),
                    new Rectangle ((int)x, (int)y, Unbounded, Unbounded),
                    solid.Color.ToSKColor (), ContentAlignment.TopLeft);

                return;
            }

            // Gradient and texture brushes have no single colour to hand RichTextKit, so they keep the
            // direct path -- and with it the no-fallback limitation, which is worth knowing but affects
            // almost nothing: text painted with a gradient brush is rare, and Latin text is unaffected.
            var skFont = font.GetSKFont ();
            using var paint = brush.CreatePaint ();

            // (x, y) is the TOP-LEFT of the text, as in GDI+, but Skia draws from the baseline -- so the
            // baseline sits one ascent below the top, not one em. The two are close enough that the em was
            // not obviously wrong, but it disagreed with the line height MeasureString reports, so text
            // asked to centre vertically landed a few pixels low. Skia's ascent is negative (up from the
            // baseline), hence the negation.
            _canvas.DrawText (text, x, y - skFont.Metrics.Ascent, SKTextAlign.Left, skFont, paint);
        }

        /// <summary>Draws a string at the given PointF.</summary>
        public void DrawString (string text, Majorsilence.Forms.Drawing.Font font, Majorsilence.Forms.Drawing.Brush brush, PointF point)
            => DrawString (text, font, brush, point.X, point.Y);

        /// <summary>Draws a string at the given Point.</summary>
        public void DrawString (string text, Majorsilence.Forms.Drawing.Font font, Majorsilence.Forms.Drawing.Brush brush, Point point)
            => DrawString (text, font, brush, point.X, point.Y);

        /// <summary>Draws a string within the specified rectangle, word-wrapping to its width.</summary>
        /// <remarks>
        /// GDI+ draws this overload with <c>StringFormat.GenericDefault</c>, whose flags do NOT include
        /// <c>NoWrap</c> -- so the text wraps inside the layout rectangle and is clipped vertically.
        /// This used to clip to the rectangle and then call the unbounded point overload, so a
        /// paragraph, a wrapped cell value, a tooltip body or a report text box drew as ONE clipped
        /// line. It was made worse by <see cref="MeasureString(string, Majorsilence.Forms.Drawing.Font, SizeF)"/>
        /// wrapping correctly: an application that measured to size its box and then drew into it got a
        /// tall empty box with a single line at the top (finding GFX-06).
        /// </remarks>
        public void DrawString (string text, Majorsilence.Forms.Drawing.Font font, Majorsilence.Forms.Drawing.Brush brush, RectangleF bounds)
        {
            if (_canvas is null || string.IsNullOrEmpty (text)) return;

            // The canvas extension lays out to the rectangle's width and clips to it, which is exactly
            // the contract here. Solid brushes only, matching the point overload: a gradient or texture
            // brush has no single colour to hand RichTextKit and keeps the direct path below.
            if (brush is Majorsilence.Forms.Drawing.SolidBrush solid) {
                _canvas.DrawText (text, font.GetSKTypeface (), (int)System.Math.Round (font.PixelSize),
                    Rectangle.Round (bounds), solid.Color.ToSKColor (), ContentAlignment.TopLeft);

                return;
            }

            _canvas.Save ();
            _canvas.ClipRect (new SKRect (bounds.Left, bounds.Top, bounds.Right, bounds.Bottom));
            DrawString (text, font, brush, bounds.Left, bounds.Top);
            _canvas.Restore ();
        }

        /// <summary>
        /// Draws a string within the specified rectangle, honouring the format's Alignment and
        /// LineAlignment, and rotating for <see cref="Majorsilence.Forms.Drawing.StringFormatFlags.DirectionVertical"/>.
        /// Trimming and the remaining FormatFlags are still ignored.
        /// </summary>
        public void DrawString (string text, Majorsilence.Forms.Drawing.Font font, Majorsilence.Forms.Drawing.Brush brush, RectangleF bounds, Majorsilence.Forms.Drawing.StringFormat? format)
        {
            if (format is null) {
                DrawString (text, font, brush, bounds);
                return;
            }

            if (IsVertical (format)) {
                DrawStringVertical (text, font, brush, bounds, format);
                return;
            }

            // Hotkey prefixes are the format's business, as in GDI+: "&Cancel" has to render as "Cancel"
            // with an underlined C, not with a literal ampersand. Krypton's AccurateText sets this on every
            // piece of button, tab and menu text it draws, so leaving it unread showed the raw "&" suite-wide.
            var display = text;
            var mnemonic = -1;

            if (format.HotkeyPrefix != Majorsilence.Forms.Drawing.Text.HotkeyPrefix.None) {
                display = Mnemonics.Parse (text, out mnemonic);

                if (format.HotkeyPrefix != Majorsilence.Forms.Drawing.Text.HotkeyPrefix.Show)
                    mnemonic = -1;   // Hide: strip the prefix, but draw no underline.
            }

            // Measured on the DISPLAY text: sizing on the raw string would offset centred text by the
            // width of an ampersand that never appears.
            var origin = AlignTextInBounds (
                display, font, bounds,
                ToOffsetFactor (format.Alignment),
                ToOffsetFactor (format.LineAlignment));

            DrawStringClipped (display, font, brush, origin, bounds);

            if (mnemonic >= 0 && mnemonic < display.Length)
                DrawMnemonicUnderline (display, mnemonic, font, brush, origin, bounds);
        }

        /// <summary>
        /// Draws <paramref name="text"/> rotated -90 degrees (counter-clockwise) to fill a tall,
        /// narrow <paramref name="bounds"/> -- what RDL's <c>WritingMode="tb-rl"</c> calls for on
        /// axis and category titles.
        /// </summary>
        /// <remarks>
        /// This rotates the whole run as one line, the way SSRS and every chart library render a
        /// sideways axis title (read by tilting your head left) -- not the glyph-by-glyph vertical
        /// stacking real CJK typesetting uses, which is what plain <c>StringFormatFlags.DirectionVertical</c>
        /// means on Windows GDI+. RDL only ever pairs the flag with a single short Latin run in a box
        /// the report author already sized for a rotated line (see PageDrawing.DrawString), so that is
        /// what this renders.
        /// GDI+ also swaps which factor governs which axis for vertical text: <c>Alignment</c> positions
        /// the run along its reading direction (here, bottom-to-top) and <c>LineAlignment</c> positions
        /// it across the column's width.
        /// </remarks>
        private void DrawStringVertical (string text, Majorsilence.Forms.Drawing.Font font, Majorsilence.Forms.Drawing.Brush brush, RectangleF bounds, Majorsilence.Forms.Drawing.StringFormat format)
        {
            if (_canvas is null || string.IsNullOrEmpty (text))
                return;

            var display = format.HotkeyPrefix != Majorsilence.Forms.Drawing.Text.HotkeyPrefix.None
                ? Mnemonics.Parse (text, out _)
                : text;

            // Unrotated: size.Width is the run's length (becomes the box's height once rotated),
            // size.Height is its thickness (becomes the box's width).
            var size = MeasureString (display, font);

            var alongSlack = bounds.Height - size.Width;
            var acrossSlack = bounds.Width - size.Height;

            var pivotX = bounds.Left + acrossSlack * ToOffsetFactor (format.LineAlignment);
            var pivotY = bounds.Bottom - alongSlack * ToOffsetFactor (format.Alignment);

            _canvas.Save ();
            _canvas.ClipRect (new SKRect (bounds.Left, bounds.Top, bounds.Right, bounds.Bottom));
            _canvas.RotateDegrees (-90, pivotX, pivotY);
            DrawString (display, font, brush, pivotX, pivotY);
            _canvas.Restore ();
        }

        /// <summary>
        /// Underlines a single character of already-drawn text, the way GDI+ marks a hotkey.
        /// </summary>
        /// <remarks>
        /// Drawn as a rule rather than with an underlined font: only one character is underlined, so
        /// restyling the whole run would be wrong, and measuring the prefix is how its x position is found.
        /// Clipped to the same bounds as the text so a mnemonic in overflowing text does not escape the box.
        /// </remarks>
        internal void DrawMnemonicUnderline (string display, int index,
            Majorsilence.Forms.Drawing.Font font, Majorsilence.Forms.Drawing.Brush brush,
            PointF origin, RectangleF clip)
        {
            if (_canvas is null)
                return;

            var before = MeasureString (display[..index], font).Width;
            var width = MeasureString (display[index].ToString (), font).Width;

            if (width <= 0)
                return;

            // Just below the baseline, which DrawString places one ascent below the text's top edge.
            var y = origin.Y - font.GetSKFont ().Metrics.Ascent + 1f;

            using var paint = brush.CreatePaint ();
            paint.Style = SKPaintStyle.Fill;

            _canvas.Save ();
            _canvas.ClipRect (new SKRect (clip.Left, clip.Top, clip.Right, clip.Bottom));
            _canvas.DrawRect (origin.X + before, y, width, 1f, paint);
            _canvas.Restore ();
        }

        // Near -> 0 (no shift), Center -> half the slack, Far -> all of it.
        private static float ToOffsetFactor (Majorsilence.Forms.Drawing.StringAlignment alignment) => alignment switch {
            Majorsilence.Forms.Drawing.StringAlignment.Center => 0.5f,
            Majorsilence.Forms.Drawing.StringAlignment.Far => 1f,
            _ => 0f,
        };

        /// <summary>
        /// Positions <paramref name="text"/> inside <paramref name="bounds"/>, shifting it by the
        /// given fraction of the leftover space on each axis. A factor of 0 leaves it at the
        /// top-left, 0.5 centres it, 1 pushes it to the far edge.
        /// </summary>
        /// <remarks>
        /// Text longer than the box yields a negative offset, so it overhangs symmetrically the way
        /// GDI's DT_CENTER does, rather than being pinned to the near edge.
        /// </remarks>
        internal PointF AlignTextInBounds (string text, Majorsilence.Forms.Drawing.Font font, RectangleF bounds, float xFactor, float yFactor)
        {
            var size = MeasureString (text, font);
            return new PointF (
                bounds.X + ((bounds.Width - size.Width) * xFactor),
                bounds.Y + ((bounds.Height - size.Height) * yFactor));
        }

        /// <summary>
        /// Draws text laid out as a block inside <paramref name="bounds"/> — wrapping to its width,
        /// optionally limited to a number of lines and ellipsised when it does not fit.
        /// </summary>
        /// <remarks>
        /// The single-run path (<c>DrawStringClipped</c>) cannot express either, so
        /// <c>TextRenderer.DrawText</c> routes the <c>WordBreak</c> and ellipsis flags here.
        /// RichTextKit already implements both; nothing was reaching them.
        /// </remarks>
        internal void DrawTextBlock (string text, Majorsilence.Forms.Drawing.Font font,
            System.Drawing.Color color, Rectangle bounds, ContentAlignment alignment,
            int? maxLines, bool ellipsis)
        {
            if (_canvas is null || string.IsNullOrEmpty (text))
                return;

            _canvas.DrawText (text, font.GetSKTypeface (), (int)System.Math.Round (font.PixelSize),
                bounds, color.ToSKColor (), alignment, maxLines: maxLines, ellipsis: ellipsis);
        }

        /// <summary>
        /// Draws text at <paramref name="origin"/> while clipping to a separate rectangle, or to
        /// nothing when <paramref name="clip"/> is null.
        /// </summary>
        /// <remarks>
        /// The two have to be independent: GDI's alignment flags move text around inside the layout
        /// rectangle, but clipping still happens against that rectangle -- so the position the text
        /// is drawn at is not the corner of the box it is clipped to.
        /// </remarks>
        internal void DrawStringClipped (string text, Majorsilence.Forms.Drawing.Font font, Majorsilence.Forms.Drawing.Brush brush, PointF origin, RectangleF? clip)
        {
            if (_canvas is null || string.IsNullOrEmpty (text)) return;

            if (clip is null) {
                DrawString (text, font, brush, origin.X, origin.Y);
                return;
            }

            var rect = clip.Value;
            _canvas.Save ();
            _canvas.ClipRect (new SKRect (rect.Left, rect.Top, rect.Right, rect.Bottom));
            DrawString (text, font, brush, origin.X, origin.Y);
            _canvas.Restore ();
        }

        /// <summary>Draws a string within the specified rectangle.</summary>
        public void DrawString (string text, Majorsilence.Forms.Drawing.Font font, Majorsilence.Forms.Drawing.Brush brush, RectangleF bounds, object? format)
            => DrawString (text, font, brush, bounds);

        /// <summary>Draws a string at the given PointF (StringFormat is ignored).</summary>
        public void DrawString (string text, Majorsilence.Forms.Drawing.Font font, Majorsilence.Forms.Drawing.Brush brush, PointF point, Majorsilence.Forms.Drawing.StringFormat? format)
            => DrawString (text, font, brush, point.X, point.Y);

        /// <summary>Draws a string at the given float coordinates (StringFormat is ignored).</summary>
        public void DrawString (string text, Majorsilence.Forms.Drawing.Font font, Majorsilence.Forms.Drawing.Brush brush, float x, float y, Majorsilence.Forms.Drawing.StringFormat? format)
            => DrawString (text, font, brush, x, y);

        // The SKBitmap/SKColor overloads below (through DrawImage(SKBitmap, float, float, float, float))
        // are Skia-native convenience helpers, not part of the GDI+ surface real WinForms code calls by
        // name -- SKBitmap/SKColor mean nothing to ported code, which only ever has an Image/Pen/Brush to
        // hand. Kept internal (not deleted; nothing in this assembly calls them either, but they cost
        // nothing to keep for a future renderer fast path) rather than public: left public, they silently
        // widened this method's public overload set, so a real WinForms call using target-typed `new(...)`
        // -- `g.DrawRectangle(new(color), rect)`, `g.DrawLine(new(color), p1, p2)`, an image drawn through
        // a bitmap variable -- became ambiguous (CS0121) between the real overload and one of these,
        // despite nothing in the call ever mentioning Skia.

        /// <summary>Draws an SKBitmap image at the given rectangle.</summary>
        internal void DrawImage (SKBitmap image, Rectangle destRect)
        {
            if (_canvas is null || image is null) return;
            _canvas.DrawBitmap (image, new SKRect (destRect.Left, destRect.Top, destRect.Right, destRect.Bottom));
        }

        /// <summary>Draws an SKBitmap image at the given position.</summary>
        internal void DrawImage (SKBitmap image, int x, int y)
        {
            if (_canvas is null || image is null) return;
            _canvas.DrawBitmap (image, new SKPoint (x, y));
        }

        /// <summary>Draws a focus rectangle (stub — draws a dotted border).</summary>
        public void DrawFocusRectangle (Rectangle rect)
        {
            if (_canvas is null) return;
            using var paint = new SKPaint { Color = SKColors.Black, Style = SKPaintStyle.Stroke, StrokeWidth = 1 };
            paint.PathEffect = SKPathEffect.CreateDash ([1, 1], 0);
            _canvas.DrawRect (new SKRect (rect.Left, rect.Top, rect.Right, rect.Bottom), paint);
        }

        // --- SKColor overloads (internal usage) ---

        /// <summary>Fills a rectangle with the given SKColor.</summary>
        internal void FillRectangle (SKColor color, Rectangle rect)
        {
            if (_canvas is null) return;
            using var paint = new SKPaint { Color = color, Style = SKPaintStyle.Fill };
            _canvas.DrawRect (new SKRect (rect.Left, rect.Top, rect.Right, rect.Bottom), paint);
        }

        /// <summary>Draws a rectangle outline with the given SKColor.</summary>
        internal void DrawRectangle (SKColor color, Rectangle rect)
        {
            if (_canvas is null) return;
            using var paint = new SKPaint { Color = color, Style = SKPaintStyle.Stroke, StrokeWidth = 1 };
            _canvas.DrawRect (new SKRect (rect.Left, rect.Top, rect.Right, rect.Bottom), paint);
        }

        /// <summary>Draws a line with the given SKColor.</summary>
        internal void DrawLine (SKColor color, Point p1, Point p2)
        {
            if (_canvas is null) return;
            using var paint = new SKPaint { Color = color, Style = SKPaintStyle.Stroke, StrokeWidth = 1 };
            _canvas.DrawLine (p1.X, p1.Y, p2.X, p2.Y, paint);
        }

        /// <summary>Draws a string with the given SKColor.</summary>
        public void DrawString (string text, SKTypeface font, SKColor color, float x, float y)
        {
            if (_canvas is null || string.IsNullOrEmpty (text)) return;
            using var skFont = new SKFont (font, Theme.FontSize);
            using var paint = new SKPaint { Color = color };
            _canvas.DrawText (text, x, y + Theme.FontSize, SKTextAlign.Left, skFont, paint);
        }

        /// <summary>Draws a string within bounds with the given SKColor.</summary>
        public void DrawString (string text, SKTypeface font, SKColor color, RectangleF bounds)
        {
            if (_canvas is null || string.IsNullOrEmpty (text)) return;
            _canvas.Save ();
            _canvas.ClipRect (new SKRect (bounds.Left, bounds.Top, bounds.Right, bounds.Bottom));
            DrawString (text, font, color, bounds.Left, bounds.Top);
            _canvas.Restore ();
        }

        /// <summary>Draws an SKBitmap at its original size at the given position.</summary>
        internal void DrawImageUnscaled (SKBitmap image, int x, int y) => DrawImage (image, x, y);

        /// <summary>Draws an SKBitmap at its original size at the given point.</summary>
        internal void DrawImageUnscaled (SKBitmap image, Point point) => DrawImage (image, point.X, point.Y);

        /// <summary>Draws an SKBitmap at its original size, clipped to the given rectangle.</summary>
        internal void DrawImageUnscaled (SKBitmap image, Rectangle rect) => DrawImage (image, rect);

        /// <summary>Draws an SKBitmap clipped to the destination rectangle from a source rectangle.</summary>
        internal void DrawImage (SKBitmap image, Rectangle destRect, Rectangle srcRect, Majorsilence.Forms.Drawing.GraphicsUnit srcUnit)
        {
            if (_canvas is null || image is null) return;
            var src = new SKRect (srcRect.Left, srcRect.Top, srcRect.Right, srcRect.Bottom);
            var dst = new SKRect (destRect.Left, destRect.Top, destRect.Right, destRect.Bottom);
            _canvas.DrawBitmap (image, src, dst);
        }

        /// <summary>Draws an SKBitmap scaled to fill the destination rectangle.</summary>
        internal void DrawImage (SKBitmap image, float x, float y, float width, float height)
            => DrawImage (image, new Rectangle ((int)x, (int)y, (int)width, (int)height));

        /// <summary>Draws a Majorsilence.Forms.Drawing.Image at the specified location.</summary>
#pragma warning disable CA1416
        public void DrawImage (Majorsilence.Forms.Drawing.Image image, int x, int y)
        {
            using var bmp = image?.ToSKBitmap ();
            if (bmp != null) DrawImage (bmp, x, y);
        }

        /// <summary>Draws a Majorsilence.Forms.Drawing.Image at the specified location and size (int overload).</summary>
        public void DrawImage (Majorsilence.Forms.Drawing.Image image, int x, int y, int width, int height)
            => DrawImage (image, new Rectangle (x, y, width, height));

        /// <summary>Draws a Majorsilence.Forms.Drawing.Image scaled to fill the destination rectangle.</summary>
        public void DrawImage (Majorsilence.Forms.Drawing.Image image, Rectangle destRect)
        {
            image?.PrepareForDraw (destRect.Width, destRect.Height);

            using var bmp = image?.ToSKBitmap ();
            if (bmp != null) DrawImage (bmp, destRect);
        }

        /// <summary>Draws a Majorsilence.Forms.Drawing.Image scaled to fill the destination rectangle.</summary>
        public void DrawImage (Majorsilence.Forms.Drawing.Image image, RectangleF destRect)
            => DrawImage (image, Rectangle.Round (destRect));

        /// <summary>Draws a Majorsilence.Forms.Drawing.Image at (x,y) scaled to (width,height).</summary>
        public void DrawImage (Majorsilence.Forms.Drawing.Image image, float x, float y, float width, float height)
            => DrawImage (image, new Rectangle ((int)x, (int)y, (int)width, (int)height));

        /// <summary>Draws a Majorsilence.Forms.Drawing.Bitmap at the specified location.</summary>
        public void DrawImage (Majorsilence.Forms.Drawing.Bitmap bitmap, int x, int y) => DrawImage ((Majorsilence.Forms.Drawing.Image)bitmap, x, y);

        /// <summary>Draws a Majorsilence.Forms.Drawing.Bitmap scaled to fill the destination rectangle.</summary>
        public void DrawImage (Majorsilence.Forms.Drawing.Bitmap bitmap, Rectangle destRect) => DrawImage ((Majorsilence.Forms.Drawing.Image)bitmap, destRect);

        /// <summary>Draws a portion of a Majorsilence.Forms.Drawing.Image to the destination rectangle.</summary>
        public void DrawImage (Majorsilence.Forms.Drawing.Image image, Rectangle destRect, Rectangle srcRect, Majorsilence.Forms.Drawing.GraphicsUnit srcUnit)
        {
            image?.PrepareForDraw (destRect.Width, destRect.Height);

            using var bmp = image?.ToSKBitmap ();
            if (bmp != null) DrawImage (bmp, destRect, srcRect, srcUnit);
        }

        /// <summary>Draws a portion of a Majorsilence.Forms.Drawing.Image to the destination rectangle (float-rectangle overload, rounded to integer device rects).</summary>
        public void DrawImage (Majorsilence.Forms.Drawing.Image image, RectangleF destRect, RectangleF srcRect, Majorsilence.Forms.Drawing.GraphicsUnit srcUnit)
            => DrawImage (image, Rectangle.Round (destRect), Rectangle.Round (srcRect), srcUnit);

        /// <summary>Draws a Majorsilence.Forms.Drawing.Image unscaled at a point.</summary>
        public void DrawImageUnscaled (Majorsilence.Forms.Drawing.Image image, int x, int y) => DrawImage (image, x, y);

        /// <summary>Draws a Majorsilence.Forms.Drawing.Image unscaled at a point.</summary>
        public void DrawImageUnscaled (Majorsilence.Forms.Drawing.Image image, Rectangle rect) => DrawImage (image, rect);

        /// <summary>
        /// Draws a portion of an image into <paramref name="destRect"/>, applying the color
        /// adjustments described by <paramref name="imageAttrs"/> (color matrix, gamma, transparent
        /// color key, remap table). This is the GDI+ image color-remapping path: the matrix and gamma
        /// become an <c>SKColorFilter</c> on the draw paint, and the lookup-style adjustments are
        /// baked into a temporary copy of the source pixels first.
        /// </summary>
        public void DrawImage (Majorsilence.Forms.Drawing.Image image, Rectangle destRect,
            float srcX, float srcY, float srcWidth, float srcHeight,
            Majorsilence.Forms.Drawing.GraphicsUnit srcUnit,
            Majorsilence.Forms.Drawing.Imaging.ImageAttributes? imageAttrs)
        {
            if (_canvas is null || image is null)
                return;

            using var bmp = image.ToSKBitmap ();
            if (bmp is null)
                return;

            using var adjusted = imageAttrs?.ApplyPixelAdjustments (bmp);
            var source = adjusted ?? bmp;

            using var colorFilter = imageAttrs?.ToSKColorFilter ();
            using var paint = colorFilter is null ? null : new SKPaint { ColorFilter = colorFilter, IsAntialias = true };

            var src = new SKRect (srcX, srcY, srcX + srcWidth, srcY + srcHeight);
            var dst = new SKRect (destRect.Left, destRect.Top, destRect.Right, destRect.Bottom);
            _canvas.DrawBitmap (source, src, dst, paint);
        }

        /// <inheritdoc cref="DrawImage(Majorsilence.Forms.Drawing.Image, Rectangle, float, float, float, float, Majorsilence.Forms.Drawing.GraphicsUnit, Majorsilence.Forms.Drawing.Imaging.ImageAttributes)"/>
        public void DrawImage (Majorsilence.Forms.Drawing.Image image, Rectangle destRect,
            int srcX, int srcY, int srcWidth, int srcHeight,
            Majorsilence.Forms.Drawing.GraphicsUnit srcUnit,
            Majorsilence.Forms.Drawing.Imaging.ImageAttributes? imageAttrs)
            => DrawImage (image, destRect, (float)srcX, srcY, srcWidth, srcHeight, srcUnit, imageAttrs);

        /// <inheritdoc cref="DrawImage(Majorsilence.Forms.Drawing.Image, Rectangle, float, float, float, float, Majorsilence.Forms.Drawing.GraphicsUnit, Majorsilence.Forms.Drawing.Imaging.ImageAttributes)"/>
        public void DrawImage (Majorsilence.Forms.Drawing.Image image, Rectangle destRect, Rectangle srcRect,
            Majorsilence.Forms.Drawing.GraphicsUnit srcUnit,
            Majorsilence.Forms.Drawing.Imaging.ImageAttributes? imageAttrs)
            => DrawImage (image, destRect, (float)srcRect.X, srcRect.Y, srcRect.Width, srcRect.Height, srcUnit, imageAttrs);

        /// <summary>
        /// Draws the whole image into <paramref name="destRect"/> with the given color adjustments.
        /// </summary>
        public void DrawImage (Majorsilence.Forms.Drawing.Image image, Rectangle destRect,
            Majorsilence.Forms.Drawing.Imaging.ImageAttributes? imageAttrs)
            => DrawImage (image, destRect, 0f, 0f, image?.Width ?? 0, image?.Height ?? 0,
                Majorsilence.Forms.Drawing.GraphicsUnit.Pixel, imageAttrs);
#pragma warning restore CA1416

        /// <summary>Rotates the current transform by the specified angle in degrees.</summary>
        public void RotateTransform (float angle) => _canvas?.RotateDegrees (angle);

        /// <summary>Applies a rotation in degrees, composed in the specified order.</summary>
        /// <remarks>See <see cref="ScaleTransform(float, float, Majorsilence.Forms.Drawing.Drawing2D.MatrixOrder)"/>.</remarks>
        public void RotateTransform (float angle, Majorsilence.Forms.Drawing.Drawing2D.MatrixOrder order)
        {
            if (order == Majorsilence.Forms.Drawing.Drawing2D.MatrixOrder.Prepend) {
                RotateTransform (angle);
                return;
            }

            using var m = new Majorsilence.Forms.Drawing.Drawing2D.Matrix ();
            m.Rotate (angle);
            MultiplyTransform (m, order);
        }

        /// <summary>Gets or sets the current world transformation matrix.</summary>
        /// <remarks>
        /// Measured against <see cref="_baseline"/>, so this reports and accepts exactly the transform
        /// the caller applied, the way GDI+ does -- see the field's remarks. Assigning REPLACES the
        /// world transform rather than composing with it, which is also GDI+ behaviour and is what
        /// makes the save/modify/restore pattern work:
        /// <c>var saved = g.Transform; ...; g.Transform = saved;</c>
        /// That pattern is how every SVG renderer applies a nested <c>&lt;g transform&gt;</c>, and
        /// while the setter was a no-op such transforms silently vanished -- collapsing every
        /// translated group onto the same spot instead of failing visibly.
        /// </remarks>
#pragma warning disable CA1416
        public Majorsilence.Forms.Drawing.Drawing2D.Matrix Transform {
            get {
                if (_canvas is null || !_baseline.TryInvert (out var inverse))
                    return new Majorsilence.Forms.Drawing.Drawing2D.Matrix ();

                return new Majorsilence.Forms.Drawing.Drawing2D.Matrix (
                    inverse.PreConcat (_canvas.TotalMatrix));
            }
            set {
                if (_canvas is null) return;

                _canvas.SetMatrix (value is null
                    ? _baseline
                    : _baseline.PreConcat (value.ToSKMatrix ()));
            }
        }
#pragma warning restore CA1416

        /// <summary>Draws a Majorsilence.Forms.Drawing.Drawing2D.GraphicsPath outline using the specified pen.</summary>
#pragma warning disable CA1416
        public void DrawPath (Majorsilence.Forms.Drawing.Pen pen, Majorsilence.Forms.Drawing.Drawing2D.GraphicsPath path)
        {
            if (_canvas is null || path is null) return;

            // Stroke the path itself rather than a polyline rebuilt from PathPoints: replaying only the
            // points turns every curve into straight segments, which is very visible for a path built
            // by AddString or AddEllipse. Using the pen's own paint also picks up its dash pattern,
            // caps, join and brush, which the hand-rolled SKPaint here previously discarded.
            using var paint = pen.CreatePaint ();
            paint.IsAntialias = SmoothingMode != Majorsilence.Forms.Drawing.Drawing2D.SmoothingMode.None;

            _canvas.DrawPath (path.ToSKPath (), paint);
        }

        /// <summary>Fills the interior of a Majorsilence.Forms.Drawing.Drawing2D.GraphicsPath using the specified brush.</summary>
        public void FillPath (Majorsilence.Forms.Drawing.Brush brush, Majorsilence.Forms.Drawing.Drawing2D.GraphicsPath path)
        {
            if (_canvas is null || path is null) return;

            using var paint = RentFillPaint (brush);

            // As in DrawPath: fill the real path, so curves and the path's own fill mode survive.
            var skPath = path.ToSKPath ();
            skPath.FillType = path.FillMode == Majorsilence.Forms.Drawing.Drawing2D.FillMode.Winding
                ? SKPathFillType.Winding
                : SKPathFillType.EvenOdd;

            _canvas.DrawPath (skPath, paint);
        }
#pragma warning restore CA1416

        /// <summary>Sets the clipping region to a Majorsilence.Forms.Drawing.Drawing2D.GraphicsPath, replacing any current clip.</summary>
#pragma warning disable CA1416
        public void SetClip (Majorsilence.Forms.Drawing.Drawing2D.GraphicsPath path)
        {
            if (_canvas is null || path is null) return;

            using var skPath = new SKPath ();
            foreach (var point in path.PathPoints) {
                if (skPath.PointCount == 0)
                    skPath.MoveTo (point.X, point.Y);
                else
                    skPath.LineTo (point.X, point.Y);
            }

            skPath.Close ();
            RestoreClipBaseline ();
            _canvas.ClipPath (skPath);
        }
#pragma warning restore CA1416

        /// <summary>Sets the clipping region to a rectangle, replacing any current clip.</summary>
        public void SetClip (RectangleF rect)
        {
            if (_canvas is null)
                return;

            RestoreClipBaseline ();
            _canvas.ClipRect (new SKRect (rect.Left, rect.Top, rect.Right, rect.Bottom));
        }

        /// <summary>Sets the clipping region to an existing region, replacing any current clip.</summary>
        /// <remarks>
        /// A non-rectangular region is applied as its bounding rectangle: Skia clips to rectangles and
        /// paths, and <see cref="Majorsilence.Forms.Drawing.Region"/> does not expose its geometry.
        /// </remarks>
#pragma warning disable CA1416
        public void SetClip (Majorsilence.Forms.Drawing.Region region)
        {
            if (_canvas is null || region is null)
                return;

            if (region.IsInfinite (this))
                ResetClip ();
            else
                SetClip (region.GetBounds (this));
        }
#pragma warning restore CA1416

        /// <inheritdoc/>
        public void Dispose ()
        {
            if (!_disposed) {
                _disposed = true;

                // Clipping is implemented with a canvas save, so leave the canvas at the depth we found
                // it -- when it is borrowed rather than owned, the next user inherits whatever is left.
                ResetClip ();

                if (_ownsCanvas)
                    _canvas?.Dispose ();
            }
        }
    }

}

namespace Majorsilence.Forms
{
    public partial class Control
    {
        /// <summary>
        /// Creates a <see cref="Graphics"/> object for the control's drawing surface.
        /// Use for text measurement only; for actual drawing, use <see cref="PaintEventArgs.Canvas"/>.
        /// </summary>
        public Graphics CreateGraphics () => new Graphics (this);
    }
}
