using System.Collections.Generic;
using Xunit;

using Color = System.Drawing.Color;
using Rectangle = System.Drawing.Rectangle;

namespace Majorsilence.Forms.Tests
{
    /// <summary>
    /// Covers <see cref="ToolStripRenderer"/>, added for docs/winforms-gap-plan.md item 4.
    ///
    /// It was an empty abstract class — 41 of 41 members missing — so custom toolbar chrome was not
    /// merely unstyled but impossible to write: there was nothing to override. These tests exercise
    /// both extension routes an app actually uses (subclass and override, or hook the event) rather
    /// than asserting the members exist.
    /// </summary>
    public class ToolStripRendererTests
    {
        private sealed class RecordingRenderer : ToolStripRenderer
        {
            public readonly List<string> Calls = [];

            protected override void OnRenderToolStripBackground (ToolStripRenderEventArgs e) => Calls.Add ("background");
            protected override void OnRenderItemText (ToolStripItemTextRenderEventArgs e) => Calls.Add ($"text:{e.Text}");
            protected override void OnRenderSeparator (ToolStripSeparatorRenderEventArgs e) => Calls.Add ($"sep:{e.Vertical}");
            protected override void OnRenderArrow (ToolStripArrowRenderEventArgs e) => Calls.Add ("arrow");
        }

        private static Graphics NewGraphics () => Graphics.FromImage (new Majorsilence.Forms.Drawing.Bitmap (40, 40));

        [Fact]
        public void A_subclass_receives_the_paints_it_overrides ()
        {
            var renderer = new RecordingRenderer ();
            using var toolStrip = new ToolStrip ();
            using var g = NewGraphics ();

            renderer.DrawToolStripBackground (new ToolStripRenderEventArgs (g, toolStrip, Rectangle.Empty, Color.Empty));
            renderer.DrawArrow (new ToolStripArrowRenderEventArgs (g, null!, Rectangle.Empty, Color.Black, ArrowDirection.Down));

            Assert.Equal (["background", "arrow"], renderer.Calls);
        }

        [Fact]
        public void An_item_text_paint_carries_the_text_a_renderer_needs ()
        {
            var renderer = new RecordingRenderer ();
            using var g = NewGraphics ();
            using var font = new Majorsilence.Forms.Drawing.Font ("Arial", 9f);
            var item = new ToolStripButton { Text = "Save" };

            renderer.DrawItemText (new ToolStripItemTextRenderEventArgs (
                g, item, "Save", new Rectangle (0, 0, 40, 20), Color.Black, font, TextFormatFlags.Left));

            Assert.Equal (["text:Save"], renderer.Calls);
        }

        [Fact]
        public void A_caller_can_hook_the_render_event_without_subclassing ()
        {
            // The other half of the pattern: theming without deriving.
            var renderer = new RecordingRenderer ();
            using var toolStrip = new ToolStrip ();
            using var g = NewGraphics ();
            var hooked = 0;

            renderer.RenderToolStripBackground += (_, _) => hooked++;
            renderer.DrawToolStripBackground (new ToolStripRenderEventArgs (g, toolStrip, Rectangle.Empty, Color.Empty));

            Assert.Equal (1, hooked);
            // Both routes fire for the same paint — the event does not replace the override.
            Assert.Equal (["background"], renderer.Calls);
        }

        [Fact]
        public void A_separator_paint_reports_its_orientation ()
        {
            var renderer = new RecordingRenderer ();
            using var g = NewGraphics ();
            var separator = new ToolStripSeparator ();

            renderer.DrawSeparator (new ToolStripSeparatorRenderEventArgs (g, separator, vertical: true));

            Assert.Equal (["sep:True"], renderer.Calls);
        }

        [Fact]
        public void The_base_renderer_paints_nothing_by_itself ()
        {
            // Deliberate: this layer draws ToolStrips through its own theme, so a base that painted
            // would draw underneath a subclass that also paints. It must not throw either.
            var plain = new ToolStripProfessionalRenderer ();
            using var toolStrip = new ToolStrip ();
            using var g = NewGraphics ();

            plain.DrawToolStripBackground (new ToolStripRenderEventArgs (g, toolStrip, Rectangle.Empty, Color.Empty));
            plain.DrawToolStripBorder (new ToolStripRenderEventArgs (g, toolStrip, Rectangle.Empty, Color.Empty));
        }

        [Fact]
        public void CreateDisabledImage_greys_an_image_without_touching_the_original ()
        {
            using var source = new Majorsilence.Forms.Drawing.Bitmap (4, 4);
            source.SetPixel (1, 1, Color.Red);

            using var disabled = ToolStripRenderer.CreateDisabledImage (source)!;
            var pixel = new Majorsilence.Forms.Drawing.Bitmap (disabled).GetPixel (1, 1);

            Assert.Equal (pixel.R, pixel.G);
            Assert.Equal (pixel.G, pixel.B);                      // desaturated
            Assert.Equal (Color.Red.ToArgb (), source.GetPixel (1, 1).ToArgb ());   // original untouched
        }

        [Fact]
        public void CreateDisabledImage_passes_null_through ()
            => Assert.Null (ToolStripRenderer.CreateDisabledImage (null));
    }
}
