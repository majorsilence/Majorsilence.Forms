using System.Collections.Generic;
using System.Drawing;
using Majorsilence.Forms.Headless;
using Majorsilence.Forms.Telerik;
using Majorsilence.Forms.Telerik.Primitives;
using SkiaSharp;
using Xunit;

namespace Majorsilence.Forms.Tests
{
    // A tool strip's element tree, as Telerik's (UI for WinForms 2026.2.520): RootRadElement > SplitPanelElement >
    // FillPrimitive, BorderPrimitive. Code flattens a strip's chrome by walking it, giving the fill primitive one colour
    // and collapsing the border primitive; the tree was an empty stub, so that code found nothing to change.
    [Collection ("Headless")]
    public class ToolTabStripChromeTests
    {
        // The walk such code does: every fill primitive one colour, every border primitive collapsed.
        private static (int Fills, int Borders) Flatten (RadElement element, Color colour)
        {
            var fills = 0;
            var borders = 0;

            if (element is FillPrimitive fill) {
                fill.NumberOfColors = 1;
                fill.BackColor = colour;
                fills++;
            } else if (element is BorderPrimitive) {
                element.Visibility = ElementVisibility.Collapsed;
                borders++;
            }

            foreach (var child in element.Children) {
                var (f, b) = Flatten (child, colour);
                fills += f;
                borders += b;
            }

            return (fills, borders);
        }

        [Fact]
        public void The_root_element_holds_the_panel_element_with_its_fill_and_border_primitives ()
        {
            using var strip = new ToolTabStrip ();

            Assert.IsType<RootRadElement> (strip.RootElement);
            var panel = Assert.IsType<SplitPanelElement> (strip.RootElement.GetChildAt (0));
            Assert.IsType<FillPrimitive> (panel.GetChildAt (0));
            Assert.IsType<BorderPrimitive> (panel.GetChildAt (1));
            Assert.Same (strip.RootElement, strip.RootElement);
        }

        [Fact]
        public void A_strip_fills_with_its_fill_primitive_s_colour ()
        {
            HeadlessRenderer.Use ();
            using var form = new Form { FormBorderStyle = FormBorderStyle.None, ClientSize = new Size (200, 120) };
            var strip = new ToolTabStrip { Location = new Point (0, 0), Size = new Size (200, 120), CaptionVisible = false };
            form.Controls.Add (strip);
            form.Show ();

            var colour = Color.FromArgb (0x20, 0xA0, 0x40);

            using (var before = SKBitmap.Decode (HeadlessRenderer.CapturePng (form, 200, 120)))
                Assert.NotEqual (new SKColor (0x20, 0xA0, 0x40), before.GetPixel (form.LogicalToDeviceUnits (100), form.LogicalToDeviceUnits (100)));

            Assert.Equal ((1, 1), Flatten (strip.RootElement, colour));

            using var after = SKBitmap.Decode (HeadlessRenderer.CapturePng (form, 200, 120));
            Assert.Equal (new SKColor (0x20, 0xA0, 0x40), after.GetPixel (form.LogicalToDeviceUnits (100), form.LogicalToDeviceUnits (100)));
            Assert.Equal (1, ((FillPrimitive) strip.RootElement.GetChildAt (0).GetChildAt (0)).NumberOfColors);
        }
    }
}
