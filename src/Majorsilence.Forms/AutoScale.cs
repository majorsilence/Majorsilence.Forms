using System;
using System.Drawing;

namespace Majorsilence.Forms
{
    /// <summary>
    /// The one definition of what <see cref="AutoScaleMode"/> means in this layer: the metric behind
    /// <c>CurrentAutoScaleDimensions</c>, and the rules for when there is nothing to scale.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <see cref="Form"/>, <see cref="ContainerControl"/> and <see cref="UserControl"/> each declare
    /// their own <c>AutoScaleMode</c>/<c>AutoScaleDimensions</c> pair. Upstream can put this on
    /// <c>ContainerControl</c> and have the other two inherit it; here they are siblings
    /// (<c>Form : WindowBase</c>, and <c>UserControl</c>/<c>ContainerControl</c> are both
    /// <c>Panel</c>s), so there is no common base to hang it on. Keeping the metric and the no-op
    /// rules here is what stops three copies of them drifting apart -- the shape RC-6 catalogues.
    /// </para>
    /// </remarks>
    internal static class AutoScaleEngine
    {
        // The reference run and the divide-by-its-length mirror the shape of upstream's average
        // character width. The ABSOLUTE number matters here as much as the ratio, which is unusual:
        // designer files carry dimensions measured on Windows -- Segoe UI 9pt records (7, 15), the
        // older Tahoma 8.25pt default records (6, 13) -- so a metric off by a unit factor would
        // rescale every migrated form by that factor. Measuring at the font's PIXEL size is what
        // keeps it in the same range; a point-sized measurement reads about a quarter small, which is
        // the units defect W5.17 found in three separate places.
        private const string ReferenceGlyphs = "AaBbYyZz";

        /// <summary>The font-mode dimensions of a font: average glyph width, and line height.</summary>
        internal static SizeF FontDimensions (Majorsilence.Forms.Drawing.Font? font)
        {
            if (font is null)
                return SizeF.Empty;

            var typeface = TypefaceCache.Resolve (font);
            var measured = TextMeasurer.MeasureText (
                ReferenceGlyphs, typeface, (int)Math.Round (font.PixelSize));

            return new SizeF (measured.Width / ReferenceGlyphs.Length, measured.Height);
        }

        /// <summary>The dimensions a container is currently laid out at, for the given mode.</summary>
        internal static SizeF CurrentDimensions (
            AutoScaleMode mode, Majorsilence.Forms.Drawing.Font? font, int deviceDpi)
            => mode switch {
                AutoScaleMode.Font => FontDimensions (font),
                AutoScaleMode.Dpi => new SizeF (deviceDpi, deviceDpi),
                _ => SizeF.Empty,
            };

        /// <summary>
        /// Works out the factor a container should be scaled by, or returns <c>false</c> when there is
        /// nothing to do.
        /// </summary>
        /// <remarks>
        /// Every reason to do nothing lives here rather than at the three call sites, because "did not
        /// scale" and "scaled by a factor derived from a meaningless number" are indistinguishable
        /// afterwards -- and the second one moves every control on the form.
        /// </remarks>
        internal static bool TryGetFactor (
            AutoScaleMode mode, SizeF recorded, SizeF current, out SizeF factor)
        {
            factor = new SizeF (1f, 1f);

            if (mode is AutoScaleMode.None or AutoScaleMode.Inherit)
                return false;

            // Dpi mode is deliberately inert, and this is the one place that decision lives. Upstream's
            // logical coordinates ARE device pixels, so scaling by dpi/96 is what makes a form the right
            // physical size on a scaled display. Here Bounds are logical and the backend already
            // applies the display's factor on the way to the screen -- Control.DeviceDpi is derived
            // from that same factor -- so applying the ratio again would scale every form twice on any
            // HiDPI display, which is the compounding RC-8 describes. Font mode is what designer files
            // overwhelmingly record and what FRM-17 is about; Dpi mode keeps reporting honest
            // CurrentAutoScaleDimensions and changes nothing.
            if (mode is AutoScaleMode.Dpi)
                return false;

            // An unrecorded AutoScaleDimensions is the common case for a form built in code, and there
            // is no ratio to be had from it. Guarding NaN/infinity too: they arrive from a zero-sized
            // measurement rather than from a caller, and a NaN factor silently zeroes every bound.
            if (!IsUsable (recorded) || !IsUsable (current))
                return false;

            factor = new SizeF (current.Width / recorded.Width, current.Height / recorded.Height);

            // Two fonts that agree mean no change at all, rather than a small arbitrary one: rounding
            // a factor of 1.002 still moves children by a pixel in whichever direction the rounding
            // falls, and that is a worse answer than leaving the designer's own numbers alone.
            return !IsEffectivelyOne (factor);
        }

        private static bool IsUsable (SizeF size)
            => size.Width > 0 && size.Height > 0
               && !float.IsNaN (size.Width) && !float.IsNaN (size.Height)
               && !float.IsInfinity (size.Width) && !float.IsInfinity (size.Height);

        private static bool IsEffectivelyOne (SizeF factor)
            => Math.Abs (factor.Width - 1f) < 0.001f && Math.Abs (factor.Height - 1f) < 0.001f;

        /// <summary>
        /// Scales a container control to the difference between its recorded and current dimensions,
        /// and records the new dimensions so a second call is a no-op.
        /// </summary>
        internal static void Perform (Control container, AutoScaleMode mode, ref SizeF recorded)
        {
            var current = CurrentDimensions (mode, container.Font, container.DeviceDpi);

            if (!TryGetFactor (mode, recorded, current, out var factor))
                return;

            container.Scale (factor);
            recorded = current;
        }
    }
}
