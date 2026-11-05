using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text;
using Majorsilence.Forms.Headless;
using Majorsilence.Forms.Telerik;
using Xunit;

namespace Majorsilence.Forms.Tests
{
    // RadDock auto-hide, against Telerik's (UI for WinForms 2026.2.520), measured by the wfdockfloat probe on a 984 x 661
    // dock holding one document: AutoHideWindow on a tool window docked left gave it a tab in a 26 wide band at (5, 5)
    // and the document container moved to (31, 5, 948, 651); docked at the bottom, a 25 high band at (5, 631) and the
    // container (5, 5, 974, 626). ShowAutoHidePopup slid it out in a borderless popup, from the left 2 inside the
    // band's right edge, AutoHideSize long (the strip's 200 when it had none) and as tall as the room; the window in it
    // at (3, 26), 5 narrower and 28 shorter. The pin (DockState = Docked) put it back.
    [Collection ("Headless")]
    public class RadDockAutoHideTests
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

        private static ToolWindow Tool (string name, string? text = null)
        {
            var tool = new ToolWindow (text ?? name) { Name = name };
            tool.Controls.Add (new Label { Text = "content " + name, Dock = DockStyle.Fill });
            return tool;
        }

        private static List<string> Log (RadDock dock)
        {
            var log = new List<string> ();
            dock.DockStateChanging += (s, e) => log.Add ("DockStateChanging " + e.NewWindow?.Name + " " + e.NewDockState);
            dock.DockStateChanged += (s, e) => log.Add ("DockStateChanged " + e.DockWindow?.Name);
            dock.ActiveWindowChanging += (s, e) => log.Add ("ActiveWindowChanging " + e.NewWindow?.Name);
            dock.ActiveWindowChanged += (s, e) => log.Add ("ActiveWindowChanged " + e.DockWindow?.Name);
            dock.AutoHideWindowDisplaying += (s, e) => log.Add ("AutoHideWindowDisplaying " + e.NewWindow?.Name + " " + e.DisplayReason);
            dock.AutoHideWindowDisplayed += (s, e) => log.Add ("AutoHideWindowDisplayed " + e.DockWindow?.Name);
            dock.AutoHideWindowHiding += (s, e) => log.Add ("AutoHideWindowHiding " + e.NewWindow?.Name);
            dock.AutoHideWindowHidden += (s, e) => log.Add ("AutoHideWindowHidden " + e.DockWindow?.Name);
            dock.DockWindowClosing += (s, e) => log.Add ("DockWindowClosing " + e.NewWindow?.Name);
            dock.DockWindowClosed += (s, e) => log.Add ("DockWindowClosed " + e.DockWindow?.Name);
            return log;
        }

        private static Rectangle OnScreen (RadDock dock, Rectangle r) => new (dock.PointToScreen (r.Location), r.Size);

        [Fact]
        public void AutoHideWindow_on_the_left_leaves_a_26_wide_band_and_the_document_takes_the_rest ()
        {
            var (form, dock, _) = Build ();
            try {
                var tool = Tool ("t1");
                dock.DockWindow (tool, DockPosition.Left);
                var strip = (ToolTabStrip) tool.Parent!;
                var log = Log (dock);

                dock.AutoHideWindow (tool);

                Assert.Equal (DockState.AutoHide, tool.DockState);
                Assert.False (tool.Visible);
                Assert.False (strip.Visible);
                Assert.Equal (new Size (200, 200), tool.AutoHideSize);
                Assert.Equal (new Rectangle (31, 5, 948, 651), dock.MainDocumentContainer!.Bounds);
                Assert.Equal (new[] { tool }, dock.AutoHideTabsOn (AutoHidePosition.Left));
                var (bands, _) = dock.AutoHideLayout ();
                Assert.Equal (new Rectangle (5, 5, 26, 651), bands[AutoHidePosition.Left]);
                Assert.Equal (new[] { tool }, dock.GetWindows (DockState.AutoHide).ToArray ());
                Assert.Equal (new[] { "DockStateChanging t1 AutoHide", "DockStateChanged t1" }, log);
            } finally {
                form.Close ();
            }
        }

        [Fact]
        public void Auto_hidden_at_the_bottom_the_band_is_25_high ()
        {
            var (form, dock, _) = Build ();
            try {
                var tool = Tool ("t1");
                dock.DockWindow (tool, DockPosition.Bottom);
                tool.AutoHideSize = new Size (754, 130);

                tool.DockState = DockState.AutoHide;

                Assert.Equal (new Size (754, 130), tool.AutoHideSize);
                Assert.Equal (new Rectangle (5, 5, 974, 626), dock.MainDocumentContainer!.Bounds);
                Assert.Equal (new Rectangle (5, 631, 974, 25), dock.AutoHideLayout ().bands[AutoHidePosition.Bottom]);
            } finally {
                form.Close ();
            }
        }

        [Fact]
        public void Side_bands_run_between_the_top_and_bottom_ones_and_those_between_the_sides ()
        {
            var (form, dock, _) = Build ();
            try {
                var left = Tool ("t1", "Search Window");
                var bottom = Tool ("t2", "Filter for : Customers");
                dock.DockWindow (left, DockPosition.Left);
                dock.DockWindow (bottom, DockPosition.Bottom);

                dock.AutoHideWindow (left);
                dock.AutoHideWindow (bottom);

                var (bands, content) = dock.AutoHideLayout ();
                Assert.Equal (new Rectangle (5, 5, 26, 626), bands[AutoHidePosition.Left]);
                Assert.Equal (new Rectangle (31, 631, 948, 25), bands[AutoHidePosition.Bottom]);
                Assert.Equal (new Rectangle (31, 5, 948, 626), content);

                var tabs = dock.AutoHideTabs ().ToDictionary (t => t.window.Name, t => t.rect);
                Assert.Equal (new Point (6, 6), tabs["t1"].Location);
                Assert.Equal (24, tabs["t1"].Width);
                Assert.Equal (new Point (32, 632), tabs["t2"].Location);
                Assert.Equal (24, tabs["t2"].Height);

                // Segoe UI 8.25, as Telerik measured them (94 and 120 long), where the font is installed.
                if (OperatingSystem.IsWindows ()) {
                    Assert.Equal (94, tabs["t1"].Height);
                    Assert.Equal (120, tabs["t2"].Width);
                } else {
                    Assert.True (tabs["t2"].Width > tabs["t1"].Height);
                }
            } finally {
                form.Close ();
            }
        }

        [Fact]
        public void A_strip_auto_hides_whole_and_the_last_hidden_tab_comes_first ()
        {
            var (form, dock, _) = Build ();
            try {
                var a = Tool ("t1");
                var b = Tool ("t2");
                dock.DockWindow (a, DockPosition.Left);
                dock.DockWindow (b, a, DockPosition.Fill);

                dock.AutoHideWindow (a);

                Assert.Equal (DockState.AutoHide, b.DockState);
                Assert.Equal (new[] { b, a }, dock.AutoHideTabsOn (AutoHidePosition.Left));

                var tabs = dock.AutoHideTabs ();
                Assert.Equal (tabs[0].rect.Bottom, tabs[1].rect.Top);
            } finally {
                form.Close ();
            }
        }

        [Fact]
        public void ShowAutoHidePopup_slides_the_window_out_beside_the_left_band ()
        {
            var (form, dock, doc) = Build ();
            try {
                var tool = Tool ("t1");
                dock.DockWindow (tool, DockPosition.Left);
                dock.AutoHideWindow (tool);
                var log = Log (dock);

                dock.ShowAutoHidePopup (tool);

                var popup = dock.AutoHidePopup;
                Assert.True (popup.Visible);
                Assert.Same (tool, popup.ActiveWindow);
                Assert.Equal (FormBorderStyle.None, popup.FormBorderStyle);
                Assert.Same (form, popup.Owner);
                Assert.Equal (DockStyle.Left, popup.AutoHideDock);
                Assert.Equal (OnScreen (dock, new Rectangle (29, 5, 200, 651)), new Rectangle (popup.Location, popup.Size));
                Assert.True (tool.Visible);
                popup.ToolStrip.PerformLayout ();
                Assert.Equal (new Rectangle (3, 26, popup.ToolStrip.Width - 8, popup.ToolStrip.Height - 28), tool.Bounds);
                Assert.Same (tool, dock.ActiveWindow);
                Assert.Equal (new[] {
                    "AutoHideWindowDisplaying t1 TabItemClicked", "ActiveWindowChanging t1", "ActiveWindowChanged t1", "AutoHideWindowDisplayed t1",
                }, log);

                log.Clear ();
                dock.CloseAutoHidePopup ();

                Assert.False (popup.Visible);
                Assert.False (tool.Visible);
                Assert.Same (doc, dock.ActiveWindow);
                Assert.Equal (new[] { "AutoHideWindowHiding t1", "ActiveWindowChanging doc", "ActiveWindowChanged doc", "AutoHideWindowHidden t1" }, log);
            } finally {
                form.Close ();
            }
        }

        [Theory]
        [InlineData (DockPosition.Bottom, 5, 501, 974, 130, 3, 29, 5, 31)]
        [InlineData (DockPosition.Right, 753, 5, 200, 651, 6, 26, 8, 28)]
        [InlineData (DockPosition.Top, 5, 30, 974, 200, 3, 26, 5, 31)]
        public void The_popup_slides_out_from_each_side_as_Telerik_s (DockPosition side, int x, int y, int width, int height, int insetX, int insetY, int lessWidth, int lessHeight)
        {
            var (form, dock, _) = Build ();
            try {
                var tool = Tool ("t1");
                dock.DockWindow (tool, side);

                if (side == DockPosition.Bottom)
                    tool.AutoHideSize = new Size (754, 130);

                dock.AutoHideWindow (tool);
                dock.ShowAutoHidePopup (tool);

                var popup = dock.AutoHidePopup;
                Assert.Equal (OnScreen (dock, new Rectangle (x, y, width, height)), new Rectangle (popup.Location, popup.Size));
                popup.ToolStrip.PerformLayout ();
                Assert.Equal (new Rectangle (insetX, insetY, width - lessWidth, height - lessHeight), tool.Bounds);
            } finally {
                form.Close ();
            }
        }

        [Fact]
        public void A_click_on_the_tab_slides_the_window_out_and_another_click_back_in ()
        {
            var (form, dock, _) = Build ();
            try {
                var tool = Tool ("t1");
                dock.DockWindow (tool, DockPosition.Left);
                dock.AutoHideWindow (tool);
                var tab = dock.AutoHideTabs ()[0].rect;

                Assert.True (dock.HandleAutoHideMouseDown (new Point (tab.X + 2, tab.Y + 2)));
                Assert.True (dock.AutoHidePopup.Visible);

                dock.HandleAutoHideMouseDown (new Point (tab.X + 2, tab.Y + 2));
                Assert.False (dock.AutoHidePopup.Visible);

                dock.HandleAutoHideMouseDown (new Point (tab.X + 2, tab.Y + 2));
                Assert.False (dock.HandleAutoHideMouseDown (new Point (500, 300)));
                Assert.False (dock.AutoHidePopup.Visible);
            } finally {
                form.Close ();
            }
        }

        [Fact]
        public void The_pin_docks_the_window_back_in_its_strip ()
        {
            var (form, dock, _) = Build ();
            try {
                var tool = Tool ("t1");
                dock.DockWindow (tool, DockPosition.Left);
                var strip = (ToolTabStrip) tool.Parent!;
                dock.AutoHideWindow (tool);
                dock.ShowAutoHidePopup (tool);
                var log = Log (dock);

                // The popup caption's pin, 16 before the close button that ends 1 short of the caption.
                var popup = dock.AutoHidePopup.ToolStrip;
                var pin = DockStrip.CaptionButtons (popup, tool).Single (b => b.button == DockStrip.CaptionButton.Pin).rect;
                Assert.Equal (new Rectangle (popup.Width - 37, 5, 14, 18), pin);
                Assert.True (DockStrip.ClickCaptionButton (popup, tool, new Point (pin.X + 2, pin.Y + 2)));

                Assert.Equal (DockState.Docked, tool.DockState);
                Assert.Same (strip, tool.Parent);
                Assert.True (strip.Visible);
                Assert.False (dock.AutoHidePopup.Visible);
                Assert.Empty (dock.AutoHideTabsOn (AutoHidePosition.Left));
                Assert.Equal (new Rectangle (209, 5, 770, 651), dock.MainDocumentContainer!.Bounds);
                Assert.Equal ("DockStateChanging t1 Docked", log[0]);
                Assert.Contains ("DockStateChanged t1", log);
            } finally {
                form.Close ();
            }
        }

        [Fact]
        public void Closing_an_auto_hidden_window_takes_its_tab_away ()
        {
            var (form, dock, doc) = Build ();
            try {
                var tool = Tool ("t1");
                dock.DockWindow (tool, DockPosition.Left);
                dock.AutoHideWindow (tool);
                dock.ShowAutoHidePopup (tool);
                var log = Log (dock);

                tool.Close ();

                Assert.Equal (DockState.Hidden, tool.DockState);
                Assert.Equal (DockState.AutoHide, tool.PreviousDockState);
                Assert.False (dock.AutoHidePopup.Visible);
                Assert.Empty (dock.AutoHideTabsOn (AutoHidePosition.Left));
                Assert.Equal (new Rectangle (5, 5, 974, 651), dock.MainDocumentContainer!.Bounds);
                Assert.Same (doc, dock.ActiveWindow);
                Assert.Equal (new[] {
                    "DockStateChanging t1 Hidden", "DockWindowClosing t1", "DockStateChanged t1", "DockWindowClosed t1",
                    "ActiveWindowChanging doc", "ActiveWindowChanged doc",
                }, log);

                tool.Show ();

                Assert.Equal (DockState.AutoHide, tool.DockState);
                Assert.Equal (new[] { tool }, dock.AutoHideTabsOn (AutoHidePosition.Left));
            } finally {
                form.Close ();
            }
        }

        [Fact]
        public void A_window_never_docked_auto_hides_on_the_left_at_200 ()
        {
            var (form, dock, _) = Build ();
            try {
                var tool = Tool ("t1");

                dock.AutoHideWindow (tool);

                Assert.Equal (DockState.AutoHide, tool.DockState);
                Assert.Equal (new Size (200, 200), tool.AutoHideSize);
                Assert.Equal (new[] { tool }, dock.AutoHideTabsOn (AutoHidePosition.Left));
                Assert.Contains (tool, dock.DockWindows);
            } finally {
                form.Close ();
            }
        }

        [Fact]
        public void A_designer_auto_hide_group_goes_to_its_side ()
        {
            HeadlessRenderer.Use ();
            using var form = new Form { Size = new Size (1000, 720) };
            var dock = new RadDock { Bounds = new Rectangle (0, 0, 984, 661) };
            var container = new DocumentContainer ();
            container.Controls.Add (new DocumentTabStrip ());
            dock.Controls.Add (container);
            dock.MainDocumentContainer = container;
            var strip = new ToolTabStrip ();
            var tool = Tool ("search");
            strip.Controls.Add (tool);
            dock.Controls.Add (strip);
            form.Controls.Add (dock);

            ((System.ComponentModel.ISupportInitialize) dock).BeginInit ();
            dock.SerializableAutoHideContainer.BottomAutoHideGroups.Add (new AutoHideGroup (new DockWindowPlaceholder ("search")));
            ((System.ComponentModel.ISupportInitialize) dock).EndInit ();
            form.Show ();

            Assert.Equal (DockState.AutoHide, tool.DockState);
            Assert.Equal (new[] { tool }, dock.AutoHideTabsOn (AutoHidePosition.Bottom));
            form.Close ();
        }

        // ---------------- layout

        [Fact]
        public void A_saved_layout_brings_floating_and_auto_hidden_windows_back ()
        {
            var (form, dock, _) = Build ();
            var (form2, dock2, _) = Build ();
            try {
                var t1 = Tool ("t1");
                var t2 = Tool ("t2");
                dock.DockWindow (t1, DockPosition.Left);
                dock.DockWindow (t2, DockPosition.Right);
                dock.FloatWindow (t1, new Rectangle (250, 180, 640, 330));
                t2.AutoHideSize = new Size (240, 150);
                dock.AutoHideWindow (t2);

                using var stream = new MemoryStream ();
                dock.SaveToXml (stream);
                stream.Position = 0;

                var u1 = Tool ("t1");
                var u2 = Tool ("t2");
                dock2.DockWindow (u1, DockPosition.Left);
                dock2.DockWindow (u2, DockPosition.Right);
                dock2.LoadFromXml (stream);

                Assert.Equal (DockState.Floating, u1.DockState);
                Assert.Equal (new Point (250, 180), u1.FloatingParent!.Location);
                Assert.Equal (new Size (640, 330), u1.FloatingParent.Size);
                Assert.Equal (DockState.AutoHide, u2.DockState);
                Assert.Equal (new[] { u2 }, dock2.AutoHideTabsOn (AutoHidePosition.Right));
                Assert.Equal (new Size (240, 150), u2.AutoHideSize);
            } finally {
                form.Close ();
                form2.Close ();
            }
        }

        // What Telerik's own SaveToXml wrote for a window floated at (250, 180, 640, 330) and another auto-hidden on
        // the right (probe wfdockfloat, scenario L1).
        private const string TelerikLayout = @"<?xml version=""1.0"" encoding=""utf-8""?>
<RadDock Orientation=""Vertical"" TabIndex=""0"" TabStop=""False"">
  <SerializableAutoHideContainer>
    <RightAutoHideGroups>
      <Telerik.WinControls.UI.Docking.AutoHideGroup>
        <Windows>
          <Telerik.WinControls.UI.Docking.DockWindowPlaceholder AutoHideSize=""200, 200"" DockWindowName=""t2"" DockWindowText=""t2"" PreviousDockState=""Docked"" Location=""0, 0"" Font=""Microsoft Sans Serif, 12pt"" />
        </Windows>
      </Telerik.WinControls.UI.Docking.AutoHideGroup>
    </RightAutoHideGroups>
  </SerializableAutoHideContainer>
  <SerializableFloatingWindows>
    <Telerik.WinControls.UI.Docking.SerializableFloatingWindow BackColor=""191, 219, 255"" Location=""250, 180"" ClientSize=""632, 300"" ZIndex=""1"" WindowState=""Normal"">
      <DockContainer Orientation=""Vertical"" Padding=""5, 5, 5, 5"" CausesValidation=""False"" Dock=""Fill"" Location=""0, 0"" Name="""" Size=""632, 300"" TabIndex=""0"" TabStop=""False"">
        <SizeInfo SplitterCorrection=""0, 0"" AutoSizeScale=""0, 0"" />
        <Controls>
          <Telerik.WinControls.UI.Docking.ToolTabStrip SelectedIndex=""0"" Size=""632, 300"" Location=""0, 0"" CausesValidation=""False"" Name=""DockTabStrip66"" TabIndex=""0"" TabStop=""False"">
            <SizeInfo SplitterCorrection=""0, 0"" AutoSizeScale=""0, 0"" />
            <Controls>
              <Telerik.WinControls.UI.Docking.DockWindowPlaceholder DockWindowName=""t1"" DockWindowText=""t1"" PreviousDockState=""Docked"" Size=""624, 292"" Location=""4, 4"" Font=""Microsoft Sans Serif, 12pt"" />
            </Controls>
          </Telerik.WinControls.UI.Docking.ToolTabStrip>
        </Controls>
      </DockContainer>
    </Telerik.WinControls.UI.Docking.SerializableFloatingWindow>
  </SerializableFloatingWindows>
  <Controls>
    <Telerik.WinControls.UI.Docking.ToolTabStrip Size=""200, 651"" Location=""5, 5"" Name=""DockTabStrip64"" TabIndex=""1"" TabStop=""False"">
      <SizeInfo SplitterCorrection=""0, 0"" AutoSizeScale=""0, 0"" />
      <Controls>
        <Telerik.WinControls.UI.Docking.ToolWindow Caption="""" DefaultFloatingSize=""800, 400"" PreviousDockState=""Docked"" Name=""t1"" Size=""630, 296"" Location=""1, 2"" Font=""Microsoft Sans Serif, 12pt"" Text=""t1"" />
      </Controls>
    </Telerik.WinControls.UI.Docking.ToolTabStrip>
    <Telerik.WinControls.UI.Docking.DocumentContainer IsMainDocumentContainer=""True"" Orientation=""Vertical"" Name="""">
      <SizeInfo SplitterCorrection=""0, 0"" AutoSizeScale=""0, 0"" SizeMode=""Fill"" />
      <Controls>
        <Telerik.WinControls.UI.Docking.DocumentTabStrip SelectedIndex=""0"" Size=""948, 651"" Location=""0, 0"" Name=""DockTabStrip63"" TabIndex=""0"" TabStop=""False"">
          <SizeInfo SplitterCorrection=""0, 0"" AutoSizeScale=""0, 0"" />
          <Controls>
            <Telerik.WinControls.UI.Docking.DocumentWindow IsPinned=""False"" PreviousDockState=""TabbedDocument"" Name=""doc"" Size=""936, 612"" Location=""6, 33"" Font=""Microsoft Sans Serif, 12pt"" Text=""Doc"" />
          </Controls>
        </Telerik.WinControls.UI.Docking.DocumentTabStrip>
      </Controls>
    </Telerik.WinControls.UI.Docking.DocumentContainer>
    <Telerik.WinControls.UI.Docking.ToolTabStrip Size=""200, 651"" Location=""753, 5"" Name=""DockTabStrip65"" TabIndex=""2"" TabStop=""False"">
      <SizeInfo SplitterCorrection=""0, 0"" AutoSizeScale=""0, 0"" />
      <Controls>
        <Telerik.WinControls.UI.Docking.ToolWindow Caption="""" PreviousDockState=""Docked"" Name=""t2"" Size=""273, 253"" Location=""4, 4"" Font=""Microsoft Sans Serif, 12pt"" Text=""t2"" />
      </Controls>
    </Telerik.WinControls.UI.Docking.ToolTabStrip>
  </Controls>
</RadDock>";

        [Fact]
        public void A_layout_Telerik_saved_floats_and_auto_hides_its_windows_again ()
        {
            var (form, dock, _) = Build ();
            try {
                var t1 = Tool ("t1");
                var t2 = Tool ("t2");
                dock.DockWindow (t1, DockPosition.Left);
                dock.DockWindow (t2, DockPosition.Right);

                dock.LoadFromXml (new MemoryStream (Encoding.UTF8.GetBytes (TelerikLayout)));

                Assert.Equal (DockState.Floating, t1.DockState);
                Assert.Equal (new Point (250, 180), t1.FloatingParent!.Location);
                Assert.Equal (new Size (632, 300), t1.FloatingParent.ClientSize);
                Assert.Equal (DockState.AutoHide, t2.DockState);
                Assert.Equal (new[] { t2 }, dock.AutoHideTabsOn (AutoHidePosition.Right));
                Assert.Equal (new Rectangle (5, 5, 948, 651), dock.MainDocumentContainer!.Bounds);
            } finally {
                form.Close ();
            }
        }

        // ---------------- docked caption: ported mode only

        [Fact]
        public void A_ported_app_s_docked_strip_has_Telerik_s_caption_with_pin_and_close ()
        {
            Size client;
            Rectangle bounds;
            List<(DockStrip.CaptionButton button, Rectangle rect)> buttons;

            Application.SetDefaultFont (new Majorsilence.Forms.Drawing.Font ("Microsoft Sans Serif", 12f));
            try {
                var (form, dock, _) = Build ();
                try {
                    var tool = Tool ("t1");
                    dock.DockWindow (tool, DockPosition.Left);
                    var strip = (ToolTabStrip) tool.Parent!;
                    strip.PerformLayout ();
                    client = strip.Size;
                    bounds = tool.Bounds;
                    buttons = DockStrip.CaptionButtons (strip, tool);
                } finally {
                    form.Close ();
                }
            } finally {
                Application.SetDefaultFont (null!);
            }

            Assert.Equal (new Size (200, 651), client);
            Assert.Equal (new Rectangle (1, 26, 198, 623), bounds);
            Assert.Equal (new Rectangle (180, 3, 18, 20), buttons.Single (b => b.button == DockStrip.CaptionButton.Close).rect);
            Assert.Equal (new Rectangle (164, 3, 14, 20), buttons.Single (b => b.button == DockStrip.CaptionButton.Pin).rect);
        }

        [Fact]
        public void An_app_that_never_chose_a_font_keeps_the_old_docked_caption ()
        {
            var (form, dock, _) = Build ();
            try {
                var tool = Tool ("t1");
                dock.DockWindow (tool, DockPosition.Left);
                var strip = (ToolTabStrip) tool.Parent!;
                strip.PerformLayout ();

                Assert.Equal (new Rectangle (0, DockStrip.CaptionHeight, 200, 651 - DockStrip.CaptionHeight), tool.Bounds);
                Assert.Empty (DockStrip.CaptionButtons (strip, tool));
            } finally {
                form.Close ();
            }
        }

        [Fact]
        public void The_pin_on_a_ported_docked_caption_auto_hides_the_window ()
        {
            Application.SetDefaultFont (new Majorsilence.Forms.Drawing.Font ("Microsoft Sans Serif", 12f));
            try {
                var (form, dock, _) = Build ();
                try {
                    var tool = Tool ("t1");
                    dock.DockWindow (tool, DockPosition.Left);
                    var strip = (ToolTabStrip) tool.Parent!;

                    Assert.True (DockStrip.ClickCaptionButton (strip, tool, new Point (170, 10)));

                    Assert.Equal (DockState.AutoHide, tool.DockState);

                    tool.DockState = DockState.Docked;
                    Assert.True (DockStrip.ClickCaptionButton (strip, tool, new Point (188, 10)));
                    Assert.Equal (DockState.Hidden, tool.DockState);
                } finally {
                    form.Close ();
                }
            } finally {
                Application.SetDefaultFont (null!);
            }
        }
    }
}
