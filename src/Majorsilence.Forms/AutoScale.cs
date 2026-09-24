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
        /// nothing to do -- which, by the decisions recorded below, is every mode.
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

            // None and Inherit ask for nothing, by definition.
            //
            // Dpi: upstream's logical coordinates ARE device pixels, so scaling by dpi/96 is what makes
            // a form the right physical size on a scaled display. Here Bounds are logical and the
            // backend already applies the display's factor on the way to the screen --
            // Control.DeviceDpi is derived from that same factor -- so applying the ratio again would
            // scale every form twice on any HiDPI display, the compounding RC-8 describes.
            //
            // Font: the ratio would be between a number a Windows designer recorded and a number
            // measured from whatever face this platform resolved for us, and those two are not
            // comparable. Designer files overwhelmingly record (7, 15), which is Segoe UI 9pt; the
            // default here resolves to sans-serif 8.25pt, which measures (6.50, 11) -- ascent 8.47,
            // descent 2.53, no leading at all. Scaling by 11/15 squashed every Font-mode container to
            // 73% of its authored height for a reason connected to neither the display nor the layout.
            //
            // It did so selectively, which is worse than doing it everywhere: a Font-mode UserControl
            // inside a Dpi-mode form was the only thing that moved, so a label stayed on its designed
            // row while the control it labelled slid up and shrank away from it.
            //
            // This reverses FRM-17, which added the scaling on the premise that a designer's recorded
            // dimensions are a correction worth applying. On Windows they are, because the recorded
            // and the measured numbers come out of the same font stack. Here they do not, so the
            // premise does not hold and the correction is noise. CurrentAutoScaleDimensions goes on
            // reporting honestly either way; it is only the bounds that are now left alone.
            return false;
        }


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
