using System;
using System.Drawing;
using Majorsilence.Forms.Headless;
using Majorsilence.Forms.Renderers;
using Xunit;

namespace Majorsilence.Forms.Tests
{
    // An item's preferred size is measured in device pixels and stored in logical units. Rounded to
    // the nearest unit, it could come back up to half a unit short -- 1.3 device pixels at a phone's
    // 2.625 -- and the text, drawn into a box narrower than itself, wrapped its last letter onto a
    // second line ("Bookmar" over "k"). Converted up, the box always holds the text.
    [Collection ("Headless")]
    public sealed class ToolStripScaledTextTests : IDisposable
    {
        private readonly double original = Application.UiScale;

        public ToolStripScaledTextTests () => HeadlessRenderer.Use ();

        public void Dispose () => Application.UiScale = original;

        [Theory]
        [InlineData (2.625)]
        [InlineData (1.75)]
        [InlineData (1.25)]
        public void An_item_laid_out_at_a_fractional_scale_is_never_narrower_than_its_text (double scale)
        {
            Application.UiScale = scale;
            var strip = new ToolStrip { Dock = DockStyle.Top };
            foreach (var text in new[] { "Bookmark", "Listen", "Search", "‹ Library", "Import…", "Contents", "Notes" })
                strip.Items.Add (new ToolStripButton (text));
            using var form = new Form { Width = 1200, Height = 300 };
            form.Controls.Add (strip);
            form.Show ();

            var renderer = RenderManager.GetRenderer<ToolBarRenderer> (strip)!;
            Assert.True (strip.LogicalToDeviceUnits (100) > 100, "the fixture must actually be scaled");
            foreach (var item in strip.Items) {
                var device = renderer.GetPreferredItemSize (strip, item, Size.Empty);
                var logical = item.GetPreferredSize (Size.Empty);
                Assert.True (strip.LogicalToDeviceUnits (logical.Width) >= device.Width,
                    $"'{item.Text}' at {scale}: {logical.Width} logical is {strip.LogicalToDeviceUnits (logical.Width)} device, short of {device.Width}");
                Assert.True (strip.LogicalToDeviceUnits (logical.Height) >= device.Height, $"'{item.Text}' at {scale}: too short");
            }
        }
    }
}
