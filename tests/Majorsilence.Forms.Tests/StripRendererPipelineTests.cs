using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using Majorsilence.Forms.Headless;
using Xunit;

namespace Majorsilence.Forms.Tests
{
    // TSM-48. ToolStripRenderer had every upstream Draw* method, each raising its Render* event and
    // calling its OnRender* hook -- and nothing in the assembly called any of them. The strip
    // renderers painted directly, so ToolStrip.Renderer, RenderMode, a custom renderer subclass and
    // all nineteen Render* events were inert together. StripRendererBridge now routes every part
    // through the resolved renderer first.
    //
    // The contract, stated because it is NOT upstream's: the built-in painting remains the default,
    // and a custom renderer suppresses a part by setting Handled on the args (a flag added here;
    // upstream's base renderer does the default painting and an override suppresses it by not calling
    // base). Restyling needs no flag -- the default painting reads TextColor/ArrowRectangle back.
    [Collection ("Headless")]
    public class StripRendererPipelineTests
    {
        private sealed class Recorder : ToolStripRenderer
        {
            internal readonly List<string> Calls = new ();
            internal bool HandleButtons, HandleArrows;
            internal Color? RecolourText;

            protected override void OnRenderToolStripBackground (ToolStripRenderEventArgs e) => Calls.Add ("Background");
            protected override void OnRenderToolStripBorder (ToolStripRenderEventArgs e) => Calls.Add ("Border");
            protected override void OnRenderGrip (ToolStripGripRenderEventArgs e) => Calls.Add ("Grip");
            protected override void OnRenderImageMargin (ToolStripRenderEventArgs e) => Calls.Add ("ImageMargin");
            protected override void OnRenderSeparator (ToolStripSeparatorRenderEventArgs e) => Calls.Add ("Separator");
            protected override void OnRenderItemCheck (ToolStripItemImageRenderEventArgs e) => Calls.Add ("Check:" + e.Item.Text);
            protected override void OnRenderMenuItemBackground (ToolStripItemRenderEventArgs e) => Calls.Add ("MenuItemBackground:" + e.Item.Text);
            protected override void OnRenderLabelBackground (ToolStripItemRenderEventArgs e) => Calls.Add ("LabelBackground:" + e.Item.Text);
            protected override void OnRenderDropDownButtonBackground (ToolStripItemRenderEventArgs e) => Calls.Add ("DropDownButtonBackground:" + e.Item.Text);

            protected override void OnRenderButtonBackground (ToolStripItemRenderEventArgs e)
            {
                Calls.Add ("ButtonBackground:" + e.Item.Text);
                e.Handled = HandleButtons;
            }

            protected override void OnRenderItemText (ToolStripItemTextRenderEventArgs e)
            {
                Calls.Add ("ItemText:" + e.Text);
                if (RecolourText is { } c) e.TextColor = c;
            }

            protected override void OnRenderArrow (ToolStripArrowRenderEventArgs e)
            {
                Calls.Add ("Arrow:" + e.Item.Text);
                e.Handled = HandleArrows;
            }
        }

        private static ToolStrip Strip (Recorder? renderer, out Form form, out ToolStripButton button, out ToolStripDropDownButton drop)
        {
            HeadlessRenderer.Use ();

            button = new ToolStripButton { Text = "Open" };
            drop = new ToolStripDropDownButton { Text = "More" };
            drop.DropDownItems.Add (new ToolStripMenuItem ("Sub"));

            var strip = new ToolStrip { Width = 360, Height = 30 };
            strip.Items.Add (button);
            strip.Items.Add (new ToolStripLabel { Text = "Ready" });
            strip.Items.Add (drop);

            if (renderer is not null)
                strip.Renderer = renderer;

            form = new Form { Width = 460, Height = 200 };
            form.Controls.Add (strip);
            form.Show ();

            return strip;
        }

        [Fact]
        public void A_custom_renderer_is_consulted_for_every_part_of_a_strip ()
        {
            var r = new Recorder ();
            var strip = Strip (r, out var form, out _, out _);

            using (form) {
                PaintSurface.Render (strip).Dispose ();

                Assert.Contains ("Background", r.Calls);
                Assert.Contains ("Grip", r.Calls);
                Assert.Contains ("ButtonBackground:Open", r.Calls);
                Assert.Contains ("LabelBackground:Ready", r.Calls);
                Assert.Contains ("DropDownButtonBackground:More", r.Calls);
                Assert.Contains ("ItemText:Open", r.Calls);
                Assert.Contains ("Arrow:More", r.Calls);
                Assert.Contains ("Border", r.Calls);

                // Order matters to a renderer that paints a frame: background first, border last.
                Assert.Equal ("Background", r.Calls.First ());
                Assert.Equal ("Border", r.Calls.Last ());
            }
        }

        [Fact]
        public void The_Render_events_fire_as_well_as_the_hooks ()
        {
            var r = new Recorder ();
            var strip = Strip (r, out var form, out _, out _);

            using (form) {
                var texts = new List<string> ();
                r.RenderItemText += (_, e) => texts.Add (e.Text);

                PaintSurface.Render (strip).Dispose ();

                Assert.Contains ("Open", texts);
                Assert.Contains ("Ready", texts);
            }
        }

        // Handled on a background means the renderer painted it: the strip's own fill is skipped, so a
        // hovered button shows no built-in highlight.
        [Fact]
        public void Handled_suppresses_the_built_in_item_background ()
        {
            var r = new Recorder ();
            var strip = Strip (r, out var form, out var button, out _);

            using (form) {
                var unhandled = HoverInk (strip, button);

                r.HandleButtons = true;
                var handled = HoverInk (strip, button);

                Assert.True (unhandled > 0, "fixture: hovering never changed any pixels");
                Assert.True (handled < unhandled / 4,
                    $"Handled did not suppress the built-in hover fill: {handled} vs {unhandled}");
            }
        }

        // The common WinForms idiom: override OnRenderItemText to recolour, let the default draw.
        [Fact]
        public void A_TextColor_set_by_the_renderer_is_what_gets_drawn ()
        {
            var r = new Recorder { RecolourText = Color.Red };
            var strip = Strip (r, out var form, out var button, out _);

            using (form) {
                using var bitmap = PaintSurface.Render (strip);
                var b = button.DeviceBounds;
                var red = 0;

                for (var y = b.Top; y < b.Bottom; y++)
                    for (var x = b.Left; x < b.Right; x++) {
                        var p = bitmap.GetPixel (x, y);
                        if (p.Red > 180 && p.Green < 90 && p.Blue < 90) red++;
                    }

                Assert.True (red > 0, "No red pixels: the renderer's TextColor was ignored.");
            }
        }

        [Fact]
        public void Handled_suppresses_the_built_in_arrow ()
        {
            var r = new Recorder ();
            var strip = Strip (r, out var form, out _, out var drop);

            using (form) {
                var with_arrow = ArrowInk (strip, drop);

                r.HandleArrows = true;
                var without = ArrowInk (strip, drop);

                Assert.True (with_arrow > without, $"Handled did not suppress the arrow glyph: {without} vs {with_arrow}");
            }
        }

        // No renderer assigned: upstream resolves one from RenderMode / ToolStripManager, so every strip
        // routes through something and the events fire without any assignment. A strip's own Renderer
        // still reads null, which StripHierarchyTests asserts.
        [Fact]
        public void With_no_Renderer_assigned_the_manager_renderer_is_used ()
        {
            var r = new Recorder ();
            var strip = Strip (null, out var form, out _, out _);

            using (form) {
                Assert.Null (strip.Renderer);
                Assert.Equal (ToolStripRenderMode.ManagerRenderMode, strip.RenderMode);

                var previous = ToolStripManager.Renderer;
                ToolStripManager.Renderer = r;

                try {
                    PaintSurface.Render (strip).Dispose ();
                    Assert.Contains ("ButtonBackground:Open", r.Calls);
                } finally {
                    ToolStripManager.Renderer = previous;
                }
            }
        }

        [Fact]
        public void A_drop_down_routes_its_parts_too ()
        {
            HeadlessRenderer.Use ();

            var r = new Recorder ();
            var menu = new ContextMenuStrip { Renderer = r };
            menu.Items.Add (new ToolStripMenuItem ("Paste") { Checked = true });
            menu.Items.Add (new ToolStripSeparator ());
            var more = new ToolStripMenuItem ("More");
            more.DropDownItems.Add (new ToolStripMenuItem ("Deeper"));
            menu.Items.Add (more);

            using var form = new Form { Width = 400, Height = 300 };
            var host = new Panel { Left = 10, Top = 10, Width = 100, Height = 40 };
            form.Controls.Add (host);
            form.Show ();
            menu.Show (host, new Point (10, 10));

            try {
                PaintSurface.Render (menu).Dispose ();

                Assert.Contains ("Background", r.Calls);
                Assert.Contains ("ImageMargin", r.Calls);
                Assert.Contains ("MenuItemBackground:Paste", r.Calls);
                Assert.Contains ("Check:Paste", r.Calls);
                Assert.Contains ("ItemText:Paste", r.Calls);
                Assert.Contains ("Separator", r.Calls);
                Assert.Contains ("Arrow:More", r.Calls);
                Assert.Contains ("Border", r.Calls);
            } finally {
                Application.ClosePopups ();
                form.Close ();
            }
        }

        private static int HoverInk (ToolStrip strip, ToolStripItem item)
        {
            using var cold = PaintSurface.Render (strip);
            item.Hovered = true;
            using var hot = PaintSurface.Render (strip);
            item.Hovered = false;

            var b = item.DeviceBounds;
            var n = 0;

            for (var y = b.Top; y < b.Bottom && y < hot.Height; y++)
                for (var x = b.Left; x < b.Right && x < hot.Width; x++)
                    if (hot.GetPixel (x, y) != cold.GetPixel (x, y)) n++;

            return n;
        }

        // Ink in the item's rightmost 20 device pixels, where the drop-down arrow lives.
        private static int ArrowInk (ToolStrip strip, ToolStripItem item)
        {
            using var bitmap = PaintSurface.Render (strip);
            var b = item.DeviceBounds;
            var bg = bitmap.GetPixel (b.Left + 1, b.Top + 1);
            var n = 0;

            for (var y = b.Top; y < b.Bottom && y < bitmap.Height; y++)
                for (var x = System.Math.Max (b.Left, b.Right - strip.LogicalToDeviceUnits (20)); x < b.Right && x < bitmap.Width; x++)
                    if (bitmap.GetPixel (x, y) != bg) n++;

            return n;
        }
    }
}
