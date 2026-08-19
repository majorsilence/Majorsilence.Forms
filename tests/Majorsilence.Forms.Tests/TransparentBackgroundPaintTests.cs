using System;
using System.Drawing;
using System.Reflection;
using Majorsilence.Forms.Headless;
using Xunit;

namespace Majorsilence.Forms.Tests
{
    // Control.PaintTransparentBackground is reached only by reflection, from themed control libraries
    // painting their transparent-background controls -- Krypton's VisualControlBase does it on every paint
    // of every label, check box, radio button and group caption. Nothing in this library calls it, so
    // neither the compiler nor any ordinary test protects its name, its signature or what it draws.
    public class TransparentBackgroundPaintTests
    {
        // Byte-for-byte the lookup Krypton's VisualControlBase performs. If this stops finding the method,
        // Krypton silently falls back to filling SystemBrushes.Control and every themed caption in every
        // ported form turns into light text on a light rectangle.
        private static MethodInfo? ReflectedLookup () =>
            typeof (Control).GetMethod ("PaintTransparentBackground",
                BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.InvokeMethod,
                null, CallingConventions.HasThis,
                [typeof (PaintEventArgs), typeof (Rectangle), typeof (Majorsilence.Forms.Drawing.Region)],
                null);

        [Fact]
        public void The_method_themed_libraries_reflect_on_is_still_findable ()
        {
            Assert.NotNull (ReflectedLookup ());
        }

        [Fact]
        public void It_paints_the_pixels_the_parent_actually_drew_not_the_parents_BackColor ()
        {
            HeadlessRenderer.Use ();

            using var form = new Form { ClientSize = new Size (200, 200) };

            // The parent's BackColor and its painted pixels deliberately DISAGREE: a themed container
            // paints itself from a palette and leaves BackColor at the default, which is exactly how
            // filling the parent's BackColor came to look right in review and wrong on screen.
            var parent = new Panel { Bounds = new Rectangle (0, 0, 200, 200), BackColor = Color.Red };
            var child = new Panel { Bounds = new Rectangle (20, 20, 60, 60), BackColor = Color.Blue };
            parent.Controls.Add (child);
            form.Controls.Add (parent);

            form.Show ();
            HeadlessRenderer.CapturePng (form);   // gives both controls back buffers at their real sizes

            // Stand in for the parent having just painted itself green. Doing it by hand rather than from
            // a Paint handler because a COMPLETED paint leaves the child's own pixels blitted into the
            // parent's buffer -- during a real paint the parent's background pass has overwritten those
            // and the child has not been blitted back yet, which is the moment this method runs in.
            using (var parentCanvas = new SkiaSharp.SKCanvas (parent.GetBackBuffer ()))
                parentCanvas.Clear (new SkiaSharp.SKColor (0, 255, 0));   // green, deliberately not Red

            var buffer = child.GetBackBuffer ();
            using (var canvas = new SkiaSharp.SKCanvas (buffer)) {
                canvas.Clear (new SkiaSharp.SKColor (0, 0, 255));   // blue, so a no-op would be visible
                var info = new SkiaSharp.SKImageInfo (buffer.Width, buffer.Height);
                ReflectedLookup ()!.Invoke (child,
                    [new PaintEventArgs (info, canvas, 1.0), child.ClientRectangle, null!]);
                canvas.Flush ();
            }

            var painted = buffer.GetPixel (buffer.Width / 2, buffer.Height / 2);

            Assert.Equal (0, painted.Red);
            Assert.Equal (255, painted.Green);   // the parent's PAINTED green, not its Red BackColor
            Assert.Equal (0, painted.Blue);
        }
    }
}
