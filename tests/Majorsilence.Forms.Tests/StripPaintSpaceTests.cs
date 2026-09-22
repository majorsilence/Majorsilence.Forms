using System.Drawing;
using Majorsilence.Forms.Headless;
using Xunit;

namespace Majorsilence.Forms.Tests
{
    // TSM-41, generalised into a gate. The first fix covered ToolBarRenderer and MenuDropDownRenderer
    // and the finding was marked CLOSED on that basis. It was not: MenuRenderer, RibbonRenderer,
    // StatusStripRenderer, TabStripRenderer and NavigationPaneRenderer were all handing a LOGICAL item
    // box to a DEVICE canvas in exactly the same way -- a MenuStrip item measured 50x26 in an 800x52
    // bitmap where its device box was 100x52.
    //
    // Fixing renderers one at a time is how the next one gets missed, so this asserts the property
    // for every item-hosting control there is a fixture for, at whatever scale the run uses. A
    // renderer added later that paints the logical box fails the moment it is listed here.
    //
    // MEASURING THE PAINTED BOX. Each control needs a lever that fills the item's own rectangle and
    // nothing else, so the difference between two renders is exactly that rectangle -- no font,
    // padding or chrome in the way:
    //
    //   ToolStrip / MenuStrip / TabStrip / NavigationPane   hover, which fills the item background
    //   StatusStrip                                         a ToolStripProgressBar driven Minimum ->
    //                                                       Maximum, which its renderer fills across
    //                                                       the item's width
    //
    // StatusStrip needs the second because its renderer paints no item background at all -- status
    // panels are not interactive, so hovering one changes nothing. It also needs a second item beside
    // the bar: as the only item the bar is spring-sized and the fill measured nothing, which is what
    // defeated the first attempt at covering it.
    [Collection ("Headless")]
    public class StripPaintSpaceTests
    {
        public static TheoryData<string> Hosts => new () {
            "ToolStrip", "MenuStrip", "StatusStrip", "TabStrip", "NavigationPane",
        };

        [Theory]
        [MemberData (nameof (Hosts))]
        public void An_items_painted_rectangle_is_its_logical_box_scaled (string kind)
        {
            HeadlessRenderer.Use ();

            using var form = new Form { Width = 500, Height = 320 };
            var probe = Build (kind, form);

            form.Show ();
            PaintSurface.Render (probe.Control).Dispose ();

            var scale = probe.Control.LogicalToDeviceUnits (10) / 10;
            var painted = probe.Paint ();
            var logical = probe.LogicalBounds ();

            // Stated as "the logical box times the scale" rather than compared against a device-bounds
            // helper: comparing against the helper moves both sides at once if the seam is reverted,
            // and the test would still pass. This is the assertion that actually catches a regression.
            //
            // Within 2px, because a probe measures where pixels CHANGED and an edge the renderer draws
            // identically in both frames does not count -- the StatusStrip bar's outermost columns are
            // overdrawn by the panel's own chrome, so its fill measures 118 of a 120-wide item. The
            // tolerance is irrelevant to what this gate is for: the bug it catches is off by a FACTOR
            // of the scale, 120 against 60, not by a pixel or two.
            // Clipped to the control, because nothing can be painted outside it. This was added for
            // NavigationPane, which used to lay its items out 151 logical units wide inside an 80-wide
            // pane -- that is fixed now (it laid out against the DEVICE client rectangle while the
            // layout engine writes LOGICAL bounds; see NavigationPaneLayoutTests). The clamp stays
            // because it is the correct statement of what a paint probe can measure, not because any
            // host still needs it.
            var expected = Rectangle.Intersect (
                new Rectangle (logical.Left * scale, logical.Top * scale, logical.Width * scale, logical.Height * scale),
                new Rectangle (0, 0, probe.Control.LogicalToDeviceUnits (probe.Control.Width),
                    probe.Control.LogicalToDeviceUnits (probe.Control.Height)));

            Close (expected.Width, painted.Width, "width");
            Close (expected.Height, painted.Height, "height");
            Close (expected.Left, painted.Left, "left");
            Close (expected.Top, painted.Top, "top");
        }

        private static void Close (int expected, int actual, string what)
            => Assert.True (System.Math.Abs (expected - actual) <= 2,
                $"{what}: expected {expected} (logical box x scale), measured {actual}");

        private sealed class Probe
        {
            internal required Control Control { get; init; }
            internal required System.Func<Rectangle> LogicalBounds { get; init; }
            internal required System.Func<Rectangle> Paint { get; init; }
        }

        private static Probe Build (string kind, Form form)
        {
            switch (kind) {
                case "MenuStrip": {
                    var strip = new MenuStrip { Width = 300, Height = 26 };
                    var item = new ToolStripMenuItem ("File");
                    strip.Items.Add (item);
                    form.Controls.Add (strip);

                    return Hovered (strip, item);
                }

                case "StatusStrip": {
                    var strip = new StatusStrip { Width = 300, Height = 26 };
                    // A label beside the bar: alone, the bar springs to the whole strip and its fill
                    // measures nothing useful.
                    strip.Items.Add (new ToolStripStatusLabel { Text = "Ready" });
                    var bar = new ToolStripProgressBar { Minimum = 0, Maximum = 100, Value = 0 };
                    strip.Items.Add (bar);
                    form.Controls.Add (strip);

                    return new Probe {
                        Control = strip,
                        LogicalBounds = () => bar.Bounds,
                        Paint = () => {
                            bar.Value = bar.Minimum;
                            using var empty = PaintSurface.Render (strip);
                            bar.Value = bar.Maximum;
                            using var full = PaintSurface.Render (strip);

                            return Diff (empty, full);
                        },
                    };
                }

                case "TabStrip": {
                    var tabs = new TabControl { Width = 300, Height = 200 };
                    tabs.TabPages.Add (new TabPage { Text = "One" });
                    tabs.TabPages.Add (new TabPage { Text = "Two" });
                    form.Controls.Add (tabs);

                    var strip = tabs.TabStrip;
                    var item = strip.Tabs[1];

                    return new Probe {
                        Control = strip,
                        LogicalBounds = () => item.Bounds,
                        // TabStripItem.Hovered is computed from the collection's HoveredIndex, as
                        // NavigationPaneItem's is -- driven there rather than on the item.
                        Paint = () => {
                            using var cold = PaintSurface.Render (strip);
                            strip.Tabs.HoveredIndex = 1;
                            using var hot = PaintSurface.Render (strip);
                            strip.Tabs.HoveredIndex = -1;

                            return Diff (cold, hot);
                        },
                    };
                }

                case "NavigationPane": {
                    var pane = new NavigationPane { Width = 80, Height = 200 };
                    pane.Items.Add (new NavigationPaneItem (new SkiaSharp.SKBitmap (16, 16), "One"));
                    pane.Items.Add (new NavigationPaneItem (new SkiaSharp.SKBitmap (16, 16), "Two"));
                    form.Controls.Add (pane);

                    var item = pane.Items[1];

                    return new Probe {
                        Control = pane,
                        LogicalBounds = () => item.Bounds,
                        // NavigationPaneItem.Hovered is computed from the collection's HoveredIndex,
                        // so the hover is driven there rather than on the item.
                        Paint = () => {
                            using var cold = PaintSurface.Render (pane);
                            pane.Items.HoveredIndex = 1;
                            using var hot = PaintSurface.Render (pane);
                            pane.Items.HoveredIndex = -1;

                            return Diff (cold, hot);
                        },
                    };
                }

                default: {
                    var strip = new ToolStrip { Width = 300, Height = 30, GripVisible = false };
                    var item = new ToolStripButton { Text = "File" };
                    strip.Items.Add (item);
                    form.Controls.Add (strip);

                    return Hovered (strip, item);
                }
            }
        }

        private static Probe Hovered (Control control, MenuItem item)
            => new () {
                Control = control,
                LogicalBounds = () => item.Bounds,
                Paint = () => {
                    using var cold = PaintSurface.Render (control);
                    item.Hovered = true;
                    using var hot = PaintSurface.Render (control);
                    item.Hovered = false;

                    return Diff (cold, hot);
                },
            };

        private static Rectangle Diff (SkiaSharp.SKBitmap before, SkiaSharp.SKBitmap after)
        {
            int x0 = int.MaxValue, y0 = int.MaxValue, x1 = -1, y1 = -1;

            for (var y = 0; y < after.Height; y++)
                for (var x = 0; x < after.Width; x++)
                    if (after.GetPixel (x, y) != before.GetPixel (x, y)) {
                        if (x < x0) x0 = x;
                        if (y < y0) y0 = y;
                        if (x > x1) x1 = x;
                        if (y > y1) y1 = y;
                    }

            Assert.True (x1 >= 0, "The probe changed no pixels at all, so there is nothing to measure.");

            return Rectangle.FromLTRB (x0, y0, x1 + 1, y1 + 1);
        }
    }
}
