using System.Drawing;
using Majorsilence.Forms.Headless;
using Majorsilence.Forms.Telerik;
using Xunit;

namespace Majorsilence.Forms.Tests
{
    // A RadDock, a RadSplitContainer and a DocumentContainer share their room between the split panels they hold, as
    // Telerik's do. Measured on UI for WinForms 2026.2.520 (.NET 10), all in logical pixels:
    //   - a 1416 x 710 dock (vertical) with a Fill tool strip, an Auto one of AbsoluteSize 369 and a collapsed, hidden
    //     main document container: (5, 5, 1033, 700) and (1042, 5, 369, 700) -- a budget-template form's layout;
    //   - in a 1000 x 500 dock with no Fill panel: two Auto panels 493 each; AutoSizeScale 0.2 on the first, 690 and
    //     296; an Absolute 300 then two Auto panels, 300, 343 and 339; SplitterWidth 3, the second at 208;
    //   - beside the main document container (Fill), an Auto tool strip of AbsoluteSize 250 keeps 250 and the
    //     container takes 736, empty or not;
    //   - a horizontal 1570 x 717 dock with a collapsed main container and two Auto panels, AutoSizeScale 0.185633 on
    //     the first: 482 and 221 high;
    //   - nested: a dock holding a horizontal split container (AbsoluteSize 250) of two tool strips, the main
    //     document container of two document tab strips, and a tool strip: (5, 5, 250, 490) split 243 / 243 from
    //     (0, 0), the container (259, 5, 532, 490) split 264 / 264, and the strip (795, 5, 200, 490);
    //   - SizeInfo defaults to Auto, 200 x 200, no scale; the dock, split and document containers to Vertical.
    // Here the main document container filled the dock and every other strip kept its designer bounds, so a form whose
    // content sat in tool strips beside it came out overlapped, or blank where a strip had been hidden behind it.
    [Collection ("Headless")]
    public class RadDockSplitLayoutTests
    {
        private static ToolTabStrip Tool (string name, SplitPanelSizeMode? mode = null, float absolute = 0, float scale = 0, bool horizontal = false)
        {
            var strip = new ToolTabStrip ();
            strip.Controls.Add (new ToolWindow { Name = name, Text = name });

            if (mode is { } m)
                strip.SizeInfo.SizeMode = m;
            if (absolute > 0)
                strip.SizeInfo.AbsoluteSize = horizontal ? new SizeF (200, absolute) : new SizeF (absolute, 200);
            if (scale != 0)
                strip.SizeInfo.AutoSizeScale = horizontal ? new SizeF (0, scale) : new SizeF (scale, 0);

            return strip;
        }

        private static DocumentTabStrip Documents (string name)
        {
            var strip = new DocumentTabStrip ();
            strip.Controls.Add (new DocumentWindow { Name = name, Text = name });
            return strip;
        }

        // A dock of the given size holding the panels in order, laid out. The main document container is one of them when
        // given; otherwise a collapsed one is added and hidden.
        private static RadDock Dock (int width, int height, DocumentContainer? main, params Control[] panels)
        {
            HeadlessRenderer.Use ();

            var form = new Form { ClientSize = new Size (width + 20, height + 20) };
            var dock = new RadDock { Location = new Point (0, 0), Size = new Size (width, height) };

            foreach (var panel in panels)
                dock.Controls.Add (panel);

            var container = main ?? new DocumentContainer { Collapsed = true };
            if (main is null)
                dock.Controls.Add (container);

            dock.MainDocumentContainer = container;
            dock.MainDocumentContainerVisible = main is not null;
            form.Controls.Add (dock);
            form.Show ();
            dock.PerformLayout ();

            return dock;
        }

        [Fact]
        public void The_defaults_are_Telerik_s ()
        {
            var info = new SplitPanelSizeInfo ();

            Assert.Equal (SplitPanelSizeMode.Auto, info.SizeMode);
            Assert.Equal (new SizeF (200, 200), info.AbsoluteSize);
            Assert.Equal (SizeF.Empty, info.AutoSizeScale);
            Assert.Equal (Orientation.Vertical, new RadDock ().Orientation);
            Assert.Equal (Orientation.Vertical, new RadSplitContainer ().Orientation);
            Assert.Equal (Orientation.Vertical, new DocumentContainer ().Orientation);
        }

        [Fact]
        public void A_Fill_strip_takes_what_an_Auto_strip_beside_it_leaves ()
        {
            var fill = Tool ("template", SplitPanelSizeMode.Fill, 1033);
            var import = Tool ("import", absolute: 369);

            using var form = Dock (1416, 710, null, fill, import).FindForm ();

            Assert.Equal (new Rectangle (5, 5, 1033, 700), fill.Bounds);
            Assert.Equal (new Rectangle (1042, 5, 369, 700), import.Bounds);
        }

        [Fact]
        public void Auto_panels_share_the_room_by_their_scale_and_the_last_takes_the_rest ()
        {
            var a = Tool ("a");
            var b = Tool ("b");
            using (Dock (1000, 500, null, a, b).FindForm ()) {
                Assert.Equal (new Rectangle (5, 5, 493, 490), a.Bounds);
                Assert.Equal (new Rectangle (502, 5, 493, 490), b.Bounds);
            }

            var c = Tool ("c", scale: 0.2f);
            var d = Tool ("d");
            using (Dock (1000, 500, null, c, d).FindForm ()) {
                Assert.Equal (690, c.Width);
                Assert.Equal (new Rectangle (699, 5, 296, 490), d.Bounds);
            }
        }

        [Fact]
        public void An_Absolute_panel_keeps_its_size_and_the_Auto_ones_share_the_rest ()
        {
            var a = Tool ("a", SplitPanelSizeMode.Absolute, 300);
            var b = Tool ("b");
            var c = Tool ("c");

            using var form = Dock (1000, 500, null, a, b, c).FindForm ();

            Assert.Equal (300, a.Width);
            Assert.Equal (new Rectangle (309, 5, 343, 490), b.Bounds);
            Assert.Equal (new Rectangle (656, 5, 339, 490), c.Bounds);
        }

        [Fact]
        public void The_splitter_width_spaces_the_panels ()
        {
            // Measured beside the main document container: two Auto strips of the default 200, then the container.
            var a = Tool ("a");
            var b = Tool ("b");
            var main = new DocumentContainer ();
            var dock = Dock (1000, 500, main, a, b, main);

            using var form = dock.FindForm ();
            dock.SplitterWidth = 3;

            Assert.Equal (new Rectangle (5, 5, 200, 490), a.Bounds);
            Assert.Equal (208, b.Left);
        }

        [Fact]
        public void The_main_document_container_fills_beside_an_Auto_strip_even_empty ()
        {
            var main = new DocumentContainer ();
            var tool = Tool ("tool", absolute: 250);

            using var form = Dock (1000, 500, main, main, tool).FindForm ();

            Assert.True (main.Visible);
            Assert.Equal (new Rectangle (5, 5, 736, 490), main.Bounds);
            Assert.Equal (new Rectangle (745, 5, 250, 490), tool.Bounds);
        }

        [Fact]
        public void A_horizontal_dock_stacks_its_panels ()
        {
            var top = Tool ("top", absolute: 428, scale: 0.185633f, horizontal: true);
            var bottom = Tool ("bottom");
            var dock = Dock (1570, 717, null, top, bottom);

            using var form = dock.FindForm ();
            dock.Orientation = Orientation.Horizontal;

            Assert.Equal (new Rectangle (5, 5, 1560, 482), top.Bounds);
            Assert.Equal (new Rectangle (5, 491, 1560, 221), bottom.Bounds);
        }

        [Fact]
        public void Nested_containers_share_their_own_room ()
        {
            var a = Tool ("a");
            var b = Tool ("b", scale: 0.2f, horizontal: true);
            var split = new RadSplitContainer { Orientation = Orientation.Horizontal };
            split.SizeInfo.AbsoluteSize = new SizeF (250, 200);
            split.Controls.Add (a);
            split.Controls.Add (b);

            var main = new DocumentContainer ();
            var d1 = Documents ("d1");
            var d2 = Documents ("d2");
            main.Controls.Add (d1);
            main.Controls.Add (d2);

            var c = Tool ("c");

            using var form = Dock (1000, 500, main, split, main, c).FindForm ();

            Assert.Equal (new Rectangle (5, 5, 250, 490), split.Bounds);
            Assert.Equal (new Rectangle (0, 0, 250, 243), a.Bounds);
            Assert.Equal (new Rectangle (0, 247, 250, 243), b.Bounds);
            Assert.Equal (new Rectangle (259, 5, 532, 490), main.Bounds);
            Assert.Equal (new Rectangle (0, 0, 264, 490), d1.Bounds);
            Assert.Equal (new Rectangle (268, 0, 264, 490), d2.Bounds);
            Assert.Equal (new Rectangle (795, 5, 200, 490), c.Bounds);
        }
    }
}
