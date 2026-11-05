using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using Majorsilence.Forms.Headless;
using Majorsilence.Forms.Telerik;
using Xunit;

namespace Majorsilence.Forms.Tests
{
    // RadDock floating windows, against Telerik's (UI for WinForms 2026.2.520), measured by the wfdockfloat probe on a
    // 984 x 661 dock holding one document: FloatWindow on a new tool window whose DefaultFloatingSize is 800 x 400 put
    // it in a sizable floating form of exactly that size, centred on the dock's screen rectangle, owned by the dock's
    // form, not in the taskbar, maximize box but no minimize box, captioned with the window's text; the window sat
    // in the form's strip at (1, 2), 2 narrower and 4 shorter than it. Events: DockStateChanging (Floating),
    // FloatingWindowCreated, DockStateChanged, DockWindowAdded, ActiveWindowChanging, ActiveWindowChanged.
    [Collection ("Headless")]
    public class RadDockFloatingTests
    {
        private static (Form form, RadDock dock, DocumentWindow doc) Build ()
        {
            HeadlessRenderer.Use ();

            var form = new Form { StartPosition = FormStartPosition.Manual, Location = new Point (100, 100), Size = new Size (1000, 720) };
            var dock = new RadDock { Bounds = new Rectangle (0, 0, 984, 661) };
            var container = new DocumentContainer ();
            var strip = new DocumentTabStrip ();
            var doc = new DocumentWindow ("Doc") { Name = "doc", DockState = DockState.TabbedDocument };

            strip.Controls.Add (doc);
            container.Controls.Add (strip);
            dock.Controls.Add (container);
            dock.MainDocumentContainer = container;
            form.Controls.Add (dock);
            form.Show ();
            HeadlessRenderer.CapturePng (form);

            return (form, dock, doc);
        }

        private static ToolWindow Tool (string name, Size? floating = null)
        {
            var tool = new ToolWindow (name) { Name = name };
            tool.Controls.Add (new Label { Text = "content " + name, Dock = DockStyle.Fill });

            if (floating is { } size)
                tool.DefaultFloatingSize = size;

            return tool;
        }

        private static List<string> Log (RadDock dock)
        {
            var log = new List<string> ();
            dock.DockStateChanging += (s, e) => log.Add ("DockStateChanging " + e.NewWindow?.Name + " " + e.NewDockState);
            dock.DockStateChanged += (s, e) => log.Add ("DockStateChanged " + e.DockWindow?.Name);
            dock.FloatingWindowCreated += (s, e) => log.Add ("FloatingWindowCreated");
            dock.DockWindowAdded += (s, e) => log.Add ("DockWindowAdded " + e.DockWindow?.Name);
            dock.DockWindowClosing += (s, e) => log.Add ("DockWindowClosing " + e.NewWindow?.Name);
            dock.DockWindowClosed += (s, e) => log.Add ("DockWindowClosed " + e.DockWindow?.Name);
            dock.ActiveWindowChanging += (s, e) => log.Add ("ActiveWindowChanging " + e.NewWindow?.Name);
            dock.ActiveWindowChanged += (s, e) => log.Add ("ActiveWindowChanged " + e.DockWindow?.Name);
            return log;
        }

        // Where Telerik puts a floating form of the given size: centred on the dock's screen rectangle.
        private static Point Centred (RadDock dock, FloatingWindow host, Size size)
        {
            var screen = dock.RectangleToScreen (new Rectangle (0, 0, dock.Width, dock.Height));
            var scale = host.DesktopScaling;
            return new Point (screen.X + (screen.Width - (int) System.Math.Round (size.Width * scale)) / 2,
                screen.Y + (screen.Height - (int) System.Math.Round (size.Height * scale)) / 2);
        }

        [Fact]
        public void FloatWindow_shows_a_new_tool_window_in_a_floating_form_of_its_DefaultFloatingSize_centred_on_the_dock ()
        {
            var (form, dock, _) = Build ();
            try {
                var tool = Tool ("t1", new Size (800, 400));

                dock.FloatWindow (tool);

                Assert.Equal (DockState.Floating, tool.DockState);
                Assert.Equal (DockState.Docked, tool.PreviousDockState);
                var host = Assert.Single (dock.FloatingWindows);
                Assert.Same (host, tool.FloatingParent);
                Assert.True (tool.IsInFloatingMode);
                Assert.True (host.Visible);
                Assert.Equal (new Size (800, 400), host.Size);
                Assert.Equal (Centred (dock, host, new Size (800, 400)), host.Location);
                Assert.True (tool.Visible);
                Assert.Contains (tool, dock.DockWindows);
                Assert.Equal (new[] { tool }, dock.GetWindows (DockState.Floating).ToArray ());
                Assert.Same (tool, dock.ActiveWindow);
                Assert.Single (dock.ActiveFloatingWindows);
            } finally {
                form.Close ();
            }
        }

        [Fact]
        public void The_floating_form_is_sizable_owned_out_of_the_taskbar_and_captioned_with_the_window ()
        {
            var (form, dock, _) = Build ();
            try {
                var tool = Tool ("t1", new Size (800, 400));
                tool.Text = "Tracking";

                dock.FloatWindow (tool);

                var host = tool.FloatingParent!;
                Assert.Equal (FormBorderStyle.Sizable, host.FormBorderStyle);
                Assert.Same (form, host.Owner);
                Assert.False (host.ShowInTaskbar);
                Assert.True (host.MaximizeBox);
                Assert.False (host.MinimizeBox);
                Assert.Equal ("Tracking", host.Text);
                Assert.Same (dock, host.DockManager);
            } finally {
                form.Close ();
            }
        }

        [Fact]
        public void A_window_that_sets_no_floating_size_floats_at_300_by_300 ()
        {
            var (form, dock, _) = Build ();
            try {
                var tool = Tool ("t1");
                Assert.Equal (new Size (300, 300), tool.DefaultFloatingSize);
                Assert.Equal (new Size (300, 300), new DocumentWindow ().DefaultFloatingSize);

                dock.FloatWindow (tool);

                var host = tool.FloatingParent!;
                Assert.Equal (new Size (300, 300), host.Size);
                Assert.Equal (Centred (dock, host, new Size (300, 300)), host.Location);
            } finally {
                form.Close ();
            }
        }

        [Fact]
        public void The_window_sits_in_the_floating_strip_inset_1_at_the_sides_and_2_at_the_top_and_bottom ()
        {
            var (form, dock, _) = Build ();
            try {
                var tool = Tool ("t1", new Size (800, 400));

                dock.FloatWindow (tool);

                var strip = (ToolTabStrip) tool.Parent!;
                strip.PerformLayout ();
                Assert.False (strip.CaptionVisible);
                Assert.Equal (new Rectangle (1, 2, strip.Width - 2, strip.Height - 4), tool.Bounds);
            } finally {
                form.Close ();
            }
        }

        [Fact]
        public void FloatWindow_raises_Telerik_s_events_in_Telerik_s_order ()
        {
            var (form, dock, _) = Build ();
            try {
                var log = Log (dock);

                dock.FloatWindow (Tool ("t1", new Size (800, 400)));

                Assert.Equal (new[] {
                    "DockStateChanging t1 Floating", "FloatingWindowCreated", "DockStateChanged t1", "DockWindowAdded t1",
                    "ActiveWindowChanging t1", "ActiveWindowChanged t1",
                }, log);
            } finally {
                form.Close ();
            }
        }

        [Fact]
        public void FloatWindow_at_bounds_puts_the_form_there ()
        {
            var (form, dock, _) = Build ();
            try {
                var tool = Tool ("t1", new Size (800, 400));

                dock.FloatWindow (tool, new Rectangle (200, 150, 500, 300));

                Assert.Equal (new Point (200, 150), tool.FloatingParent!.Location);
                Assert.Equal (new Size (500, 300), tool.FloatingParent.Size);
            } finally {
                form.Close ();
            }
        }

        [Fact]
        public void Floating_a_docked_window_hides_its_strip_and_the_document_takes_the_room ()
        {
            var (form, dock, doc) = Build ();
            try {
                var tool = Tool ("t1", new Size (300, 200));
                dock.DockWindow (tool, DockPosition.Left);
                var strip = (ToolTabStrip) tool.Parent!;
                Assert.Equal (new Rectangle (5, 5, 200, 651), strip.Bounds);
                Assert.Equal (new Rectangle (209, 5, 770, 651), dock.MainDocumentContainer!.Bounds);

                var log = Log (dock);
                dock.FloatWindow (tool);

                Assert.False (strip.Visible);
                Assert.Same (dock, strip.Parent);
                Assert.Equal (new Rectangle (5, 5, 974, 651), dock.MainDocumentContainer.Bounds);
                Assert.Equal (new[] {
                    "DockStateChanging t1 Floating", "FloatingWindowCreated", "DockStateChanged t1",
                    "ActiveWindowChanging t1", "ActiveWindowChanged t1",
                }, log);
            } finally {
                form.Close ();
            }
        }

        [Fact]
        public void Closing_a_floating_window_hides_it_and_keeps_the_form_for_Show ()
        {
            var (form, dock, doc) = Build ();
            try {
                var tool = Tool ("t1", new Size (800, 400));
                dock.FloatWindow (tool);
                var host = tool.FloatingParent!;
                var location = host.Location;
                var log = Log (dock);

                tool.Close ();

                Assert.Equal (DockState.Hidden, tool.DockState);
                Assert.Equal (DockState.Floating, tool.PreviousDockState);
                Assert.Null (tool.Parent);
                Assert.False (host.Visible);
                Assert.False (host.IsDisposed);
                Assert.Single (dock.FloatingWindows);
                Assert.Empty (dock.ActiveFloatingWindows);
                Assert.Contains (tool, dock.DockWindows);
                Assert.Same (doc, dock.ActiveWindow);
                Assert.Equal (new[] {
                    "DockStateChanging t1 Hidden", "DockWindowClosing t1", "DockStateChanged t1", "DockWindowClosed t1",
                    "ActiveWindowChanging doc", "ActiveWindowChanged doc",
                }, log);

                log.Clear ();
                tool.Show ();

                Assert.Equal (DockState.Floating, tool.DockState);
                Assert.Same (host, tool.FloatingParent);
                Assert.True (host.Visible);
                Assert.Equal (location, host.Location);
                Assert.DoesNotContain ("FloatingWindowCreated", log);
            } finally {
                form.Close ();
            }
        }

        [Fact]
        public void Closing_the_floating_form_closes_its_windows_and_hides_it ()
        {
            var (form, dock, _) = Build ();
            try {
                var tool = Tool ("t1", new Size (800, 400));
                dock.FloatWindow (tool);
                var host = tool.FloatingParent!;
                var log = Log (dock);

                host.Close ();

                Assert.Equal (DockState.Hidden, tool.DockState);
                Assert.False (host.Visible);
                Assert.False (host.IsDisposed);
                Assert.Equal (new[] {
                    "DockStateChanging t1 Hidden", "DockWindowClosing t1", "DockStateChanged t1", "DockWindowClosed t1",
                    "ActiveWindowChanging doc", "ActiveWindowChanged doc",
                }, log);
            } finally {
                form.Close ();
            }
        }

        [Fact]
        public void Docking_a_floating_window_puts_it_back_in_its_strip_and_floating_again_reuses_the_form ()
        {
            var (form, dock, doc) = Build ();
            try {
                var tool = Tool ("t1", new Size (400, 250));
                dock.DockWindow (tool, DockPosition.Left);
                var strip = (ToolTabStrip) tool.Parent!;
                dock.FloatWindow (tool);
                var host = tool.FloatingParent!;
                host.Location = new Point (150, 120);
                var log = Log (dock);

                tool.DockState = DockState.Docked;

                Assert.Same (strip, tool.Parent);
                Assert.True (strip.Visible);
                Assert.False (host.Visible);
                Assert.Equal (DockState.Docked, tool.PreviousDockState);
                Assert.Same (tool, dock.ActiveWindow);
                Assert.Equal (new Rectangle (209, 5, 770, 651), dock.MainDocumentContainer!.Bounds);
                Assert.Equal (new[] { "DockStateChanging t1 Docked", "DockStateChanged t1", "ActiveWindowChanging t1", "ActiveWindowChanged t1" }, log);

                log.Clear ();
                tool.DockState = DockState.Floating;

                Assert.Same (host, tool.FloatingParent);
                Assert.Equal (new Point (150, 120), host.Location);
                Assert.Equal (new[] { "DockStateChanging t1 Floating", "DockStateChanged t1" }, log);
            } finally {
                form.Close ();
            }
        }

        [Fact]
        public void DockWindow_takes_a_floating_window_to_the_side_it_names ()
        {
            var (form, dock, doc) = Build ();
            try {
                var tool = Tool ("t1", new Size (800, 400));
                dock.FloatWindow (tool);
                var host = tool.FloatingParent!;
                var log = Log (dock);

                dock.DockWindow (tool, DockPosition.Right);

                Assert.Equal (DockState.Docked, tool.DockState);
                Assert.False (host.Visible);
                Assert.Equal (new Rectangle (779, 5, 200, 651), tool.Parent!.Bounds);
                Assert.Equal (new Rectangle (5, 5, 770, 651), dock.MainDocumentContainer!.Bounds);
                Assert.Equal (new[] { "DockStateChanging t1 Docked", "DockStateChanged t1", "ActiveWindowChanging doc", "ActiveWindowChanged doc" }, log);
            } finally {
                form.Close ();
            }
        }

        [Fact]
        public void A_window_not_allowed_to_float_stays_and_floating_twice_changes_nothing ()
        {
            var (form, dock, _) = Build ();
            try {
                var fixedTool = Tool ("t0");
                fixedTool.AllowedDockState = AllowedDockState.Docked;
                dock.FloatWindow (fixedTool);
                Assert.Equal (DockState.Docked, fixedTool.DockState);
                Assert.Empty (dock.FloatingWindows);

                var tool = Tool ("t1", new Size (800, 400));
                dock.FloatWindow (tool);
                var log = Log (dock);
                dock.FloatWindow (tool);

                Assert.Single (dock.FloatingWindows);
                Assert.Empty (log);
            } finally {
                form.Close ();
            }
        }

        [Fact]
        public void A_handler_can_cancel_floating ()
        {
            var (form, dock, _) = Build ();
            try {
                var tool = Tool ("t1");
                dock.DockWindow (tool, DockPosition.Left);
                dock.DockStateChanging += (s, e) => e.Cancel = true;

                dock.FloatWindow (tool);

                Assert.Equal (DockState.Docked, tool.DockState);
                Assert.IsType<ToolTabStrip> (tool.Parent);
                Assert.Empty (dock.FloatingWindows);
            } finally {
                form.Close ();
            }
        }

        [Fact]
        public void A_document_floats_too ()
        {
            var (form, dock, doc) = Build ();
            try {
                doc.DefaultFloatingSize = new Size (500, 300);

                dock.FloatWindow (doc);

                Assert.Equal (DockState.Floating, doc.DockState);
                Assert.Equal (new Size (500, 300), doc.FloatingParent!.Size);
                Assert.Contains (doc, dock.DockWindows);
            } finally {
                form.Close ();
            }
        }

        [Fact]
        public void Two_windows_floated_together_share_one_form_as_tabs ()
        {
            var (form, dock, _) = Build ();
            try {
                var a = Tool ("a");
                var b = Tool ("b");
                dock.DockWindow (a, DockPosition.Left);
                dock.DockWindow (b, a, DockPosition.Fill);

                dock.FloatWindows (new DockWindowBase[] { a, b }, new Rectangle (300, 200, 500, 300));

                var host = Assert.Single (dock.FloatingWindows);
                Assert.Same (host, a.FloatingParent);
                Assert.Same (host, b.FloatingParent);
                Assert.Same (a, dock.ActiveWindow);
                Assert.True (a.Visible);
                Assert.False (b.Visible);
            } finally {
                form.Close ();
            }
        }

        [Fact]
        public void Disposing_the_dock_disposes_its_floating_forms ()
        {
            var (form, dock, _) = Build ();
            var tool = Tool ("t1");
            dock.FloatWindow (tool);
            var host = tool.FloatingParent!;

            form.Close ();
            dock.Dispose ();

            Assert.True (host.IsDisposed);
        }

        // Telerik's DockWindow(window, position), measured: a strip of its own, 200 long; Left first and Right last
        // along the side-by-side dock; Bottom and Top turn the dock, wrapping what it held in a split container that
        // fills between them.
        [Fact]
        public void DockWindow_to_a_side_gives_a_tool_window_its_own_strip_there ()
        {
            var (form, dock, _) = Build ();
            try {
                var a = Tool ("a");
                var b = Tool ("b");
                var c = Tool ("c");
                var e = Tool ("e");

                dock.DockWindow (a, DockPosition.Left);
                dock.DockWindow (b, DockPosition.Right);
                dock.DockWindow (c, DockPosition.Bottom);
                dock.DockWindow (e, DockPosition.Top);
                dock.PerformLayout ();

                Assert.Equal (Orientation.Horizontal, dock.Orientation);
                Assert.Equal (new Rectangle (5, 5, 974, 200), e.Parent!.Bounds);
                Assert.Equal (new Rectangle (5, 456, 974, 200), c.Parent!.Bounds);
                var wrapper = Assert.IsType<RadSplitContainer> (a.Parent!.Parent);
                Assert.Equal (new Rectangle (5, 209, 974, 243), wrapper.Bounds);
                Assert.Equal (new Rectangle (0, 0, 200, 243), a.Parent.Bounds);
                Assert.Equal (new Rectangle (774, 0, 200, 243), b.Parent!.Bounds);
                Assert.Equal (new Rectangle (204, 0, 566, 243), dock.MainDocumentContainer!.Bounds);
            } finally {
                form.Close ();
            }
        }
    }
}
