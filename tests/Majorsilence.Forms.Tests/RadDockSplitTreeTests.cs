using System.Drawing;
using System.IO;
using System.Linq;
using System.Text;
using Majorsilence.Forms.Headless;
using Majorsilence.Forms.Telerik;
using Xunit;

namespace Majorsilence.Forms.Tests
{
    // How much room each panel of a dock gets, and what a saved layout does to it.
    //
    // Measured on UI for WinForms 2026.2.520 (probes/wfdocksplit), in a 900 x 420 dock holding a document
    // and three tool windows docked left, bottom and right, with the right one auto-hidden:
    //
    //   RadSplitContainer   (5, 5, 864, 410)     horizontal
    //     RadSplitContainer (0, 0, 864, 206)     vertical
    //       ToolTabStrip    (0, 0, 200, 206)     twFilter
    //       DocumentContainer (204, 0, 660, 206) the documents, SizeMode Fill
    //     ToolTabStrip      (0, 210, 864, 200)   twNotes
    //
    // The tool strip keeps the 200 of its AbsoluteSize and the documents take what is left. This dock gave
    // the two of them half the room each -- 430 and 430 -- because its documents sat in a bare
    // DocumentTabStrip, an Auto panel like the strip beside it, where Telerik's sit in a DocumentContainer
    // whose SizeMode is Fill.
    [Collection ("Headless")]
    public class RadDockSplitTreeTests
    {
        // The layout probes/wfdocksplit saved from that dock after setting every tool strip's AbsoluteSize
        // to 260 x 150 -- the case a layout saved from an untouched dock cannot show, since Telerik leaves
        // AbsoluteSize out of the document while it is still the default 200 x 200.
        private const string Resized =
            "<?xml version=\"1.0\" encoding=\"utf-8\"?><RadDock IsCleanUpTarget=\"True\" Orientation=\"Vertical\" CausesValidation=\"False\" TabIndex=\"0\" TabStop=\"False\"><Co"
            + "ntrols><Telerik.WinControls.UI.RadSplitContainer IsCleanUpTarget=\"True\" Orientation=\"Horizontal\" Padding=\"5, 5, 5, 5\" CausesValidation=\"False\" Locatio"
            + "n=\"5, 5\" Name=\"\" Size=\"626, 410\" TabIndex=\"0\" TabStop=\"False\"><SizeInfo SplitterCorrection=\"0, 0\" AutoSizeScale=\"0, 0\" /><Controls><Telerik.WinControl"
            + "s.UI.RadSplitContainer IsCleanUpTarget=\"True\" Orientation=\"Vertical\" Padding=\"5, 5, 5, 5\" Location=\"0, 0\" Name=\"\" Size=\"626, 256\" TabIndex=\"0\" TabStop"
            + "=\"False\"><SizeInfo SplitterCorrection=\"0, 0\" AutoSizeScale=\"0, 0\" /><Controls><Telerik.WinControls.UI.Docking.ToolTabStrip SelectedIndex=\"0\" Size=\"260"
            + ", 256\" Location=\"0, 0\" Name=\"DockTabStrip9\" TabIndex=\"1\" TabStop=\"False\"><SizeInfo SplitterCorrection=\"0, 0\" AutoSizeScale=\"0, 0\" AbsoluteSize=\"260, 1"
            + "50\" /><Controls><Telerik.WinControls.UI.Docking.ToolWindow Caption=\"\" PreviousDockState=\"Docked\" Name=\"twFilter\" Size=\"258, 228\" Location=\"1, 26\" Font"
            + "=\"Microsoft Sans Serif, 12pt\" Text=\"twFilter\" /></Controls></Telerik.WinControls.UI.Docking.ToolTabStrip><Telerik.WinControls.UI.Docking.DocumentConta"
            + "iner IsMainDocumentContainer=\"True\" Orientation=\"Vertical\" Name=\"\"><SizeInfo SplitterCorrection=\"0, 0\" AutoSizeScale=\"0, 0\" SizeMode=\"Fill\" /><Control"
            + "s><Telerik.WinControls.UI.Docking.DocumentTabStrip SelectedIndex=\"0\" Size=\"362, 256\" Location=\"0, 0\" Name=\"DockTabStrip8\" TabIndex=\"0\" TabStop=\"False\""
            + "><SizeInfo SplitterCorrection=\"0, 0\" AutoSizeScale=\"0, 0\" /><Controls><Telerik.WinControls.UI.Docking.DocumentWindow IsPinned=\"False\" PreviousDockStat"
            + "e=\"TabbedDocument\" Name=\"docSummary\" Size=\"350, 217\" Location=\"6, 33\" Font=\"Microsoft Sans Serif, 12pt\" Text=\"Summary\" /></Controls></Telerik.WinContr"
            + "ols.UI.Docking.DocumentTabStrip></Controls></Telerik.WinControls.UI.Docking.DocumentContainer></Controls></Telerik.WinControls.UI.RadSplitContainer><T"
            + "elerik.WinControls.UI.Docking.ToolTabStrip SelectedIndex=\"0\" Size=\"626, 150\" Location=\"0, 260\" CausesValidation=\"False\" Name=\"DockTabStrip10\" TabIndex"
            + "=\"1\" TabStop=\"False\"><SizeInfo SplitterCorrection=\"0, 0\" AutoSizeScale=\"0, 0\" AbsoluteSize=\"260, 150\" /><Controls><Telerik.WinControls.UI.Docking.Tool"
            + "Window Caption=\"\" PreviousDockState=\"Docked\" Name=\"twNotes\" Size=\"624, 122\" Location=\"1, 26\" Font=\"Microsoft Sans Serif, 12pt\" Text=\"twNotes\" /></Cont"
            + "rols></Telerik.WinControls.UI.Docking.ToolTabStrip></Controls></Telerik.WinControls.UI.RadSplitContainer><Telerik.WinControls.UI.Docking.ToolTabStrip "
            + "SelectedIndex=\"0\" Size=\"260, 410\" Location=\"635, 5\" Name=\"DockTabStrip11\" TabIndex=\"1\" TabStop=\"False\"><SizeInfo SplitterCorrection=\"0, 0\" AutoSizeSca"
            + "le=\"0, 0\" AbsoluteSize=\"260, 150\" /><Controls><Telerik.WinControls.UI.Docking.ToolWindow Caption=\"\" PreviousDockState=\"Docked\" Name=\"twSide\" Size=\"258"
            + ", 382\" Location=\"1, 26\" Font=\"Microsoft Sans Serif, 12pt\" Text=\"twSide\" /></Controls></Telerik.WinControls.UI.Docking.ToolTabStrip></Controls></RadDoc"
            + "k>";

        private static (Form form, RadDock dock) Build ()
        {
            HeadlessRenderer.Use ();

            var form = new Form { Size = new Size (960, 520) };
            var dock = new RadDock { Left = 0, Top = 0, Width = 900, Height = 420 };
            form.Controls.Add (dock);

            dock.DockWindow (new DocumentWindow { Name = "docSummary", Text = "Summary" }, DockPosition.Fill);
            dock.DockWindow (new ToolWindow { Name = "twFilter", Text = "twFilter" }, DockPosition.Left);
            dock.DockWindow (new ToolWindow { Name = "twNotes", Text = "twNotes" }, DockPosition.Bottom);
            dock.DockWindow (new ToolWindow { Name = "twSide", Text = "twSide" }, DockPosition.Right);

            form.Show ();
            HeadlessRenderer.CapturePng (form);
            return (form, dock);
        }

        // The panel of the given kind holding the named window. An AutoHideTabStrip is a ToolTabStrip by
        // type but holds the tabs of hidden windows rather than being one of the dock's split panels.
        private static T Panel<T> (Control root, string window) where T : Control
        {
            foreach (Control child in root.Controls) {
                if (child is T match and not AutoHideTabStrip && Holds (child, window))
                    return match;

                if (Panel<T> (child, window) is { } found)
                    return found;
            }

            return null!;

            static bool Holds (Control panel, string name)
                => panel.Controls.OfType<DockWindowBase> ().Any (w => w.Name == name)
                    || panel.Controls.OfType<Control> ().Any (c => Holds (c, name));
        }

        [Fact]
        public void A_tool_window_docked_beside_the_documents_keeps_the_200_Telerik_gives_it ()
        {
            var (form, dock) = Build ();
            dock.AutoHideWindow (dock.DockWindows.Cast<DockWindowBase> ().First (w => w.Name == "twSide"));
            form.PerformLayout ();
            HeadlessRenderer.CapturePng (form);

            Assert.Equal (new Rectangle (0, 0, 200, 206), Panel<ToolTabStrip> (dock, "twFilter").Bounds);
            Assert.Equal (new Rectangle (204, 0, 660, 206), Panel<DocumentContainer> (dock, "docSummary").Bounds);
            Assert.Equal (new Rectangle (0, 210, 864, 200), Panel<ToolTabStrip> (dock, "twNotes").Bounds);

            form.Dispose ();
        }

        [Fact]
        public void A_saved_layout_puts_the_strips_back_at_the_sizes_it_holds ()
        {
            var (form, dock) = Build ();

            dock.LoadFromXml (new MemoryStream (Encoding.UTF8.GetBytes (Resized)));
            form.PerformLayout ();
            HeadlessRenderer.CapturePng (form);

            // Telerik, loading the same document into the same dock.
            Assert.Equal (new Rectangle (0, 0, 260, 256), Panel<ToolTabStrip> (dock, "twFilter").Bounds);
            Assert.Equal (new Rectangle (264, 0, 362, 256), Panel<DocumentContainer> (dock, "docSummary").Bounds);
            Assert.Equal (new Rectangle (0, 260, 626, 150), Panel<ToolTabStrip> (dock, "twNotes").Bounds);
            Assert.Equal (new Rectangle (635, 5, 260, 410), Panel<ToolTabStrip> (dock, "twSide").Bounds);

            form.Dispose ();
        }

        [Fact]
        public void The_sizes_a_layout_holds_reach_the_panels_SizeInfo_not_just_their_bounds ()
        {
            var (form, dock) = Build ();

            dock.LoadFromXml (new MemoryStream (Encoding.UTF8.GetBytes (Resized)));
            form.PerformLayout ();
            HeadlessRenderer.CapturePng (form);

            // What Telerik's own panels report afterwards, and what the next layout pass will go on reading.
            Assert.Equal (new SizeF (260, 150), Panel<ToolTabStrip> (dock, "twFilter").SizeInfo.AbsoluteSize);
            Assert.Equal (new SizeF (260, 150), Panel<ToolTabStrip> (dock, "twNotes").SizeInfo.AbsoluteSize);
            Assert.Equal (SplitPanelSizeMode.Fill, Panel<DocumentContainer> (dock, "docSummary").SizeInfo.SizeMode);

            form.Dispose ();
        }

        [Fact]
        public void A_panel_that_only_holds_the_filling_one_fills_its_own_container ()
        {
            HeadlessRenderer.Use ();

            // Telerik's shape, by hand, with the document container two levels down and every container Auto --
            // which is how Telerik writes them into a saved layout. Measured there at (5, 5, 200, 410) and
            // (209, 5, 686, 410): the strip keeps its AbsoluteSize and the container holding the documents takes
            // the rest, although nothing beside it says Fill.
            var form = new Form { Size = new Size (960, 520) };
            var dock = new RadDock { Left = 0, Top = 0, Width = 900, Height = 420 };

            var side = new ToolTabStrip ();
            side.Controls.Add (new ToolWindow { Name = "twSide", Text = "twSide" });

            var filter = new ToolTabStrip ();
            filter.Controls.Add (new ToolWindow { Name = "twFilter", Text = "twFilter" });

            var documents = new DocumentTabStrip ();
            documents.Controls.Add (new DocumentWindow { Name = "docSummary", Text = "Summary" });

            var container = new DocumentContainer ();
            container.SizeInfo.SizeMode = SplitPanelSizeMode.Fill;
            container.Controls.Add (documents);

            var inner = new RadSplitContainer { Orientation = Orientation.Vertical };
            inner.Controls.Add (filter);
            inner.Controls.Add (container);

            var notes = new ToolTabStrip ();
            notes.Controls.Add (new ToolWindow { Name = "twNotes", Text = "twNotes" });

            var outer = new RadSplitContainer { Orientation = Orientation.Horizontal };
            outer.Controls.Add (inner);
            outer.Controls.Add (notes);

            dock.Controls.Add (side);
            dock.Controls.Add (outer);
            form.Controls.Add (dock);
            form.Show ();
            HeadlessRenderer.CapturePng (form);

            Assert.Equal (new Rectangle (5, 5, 200, 410), side.Bounds);
            Assert.Equal (new Rectangle (209, 5, 686, 410), outer.Bounds);

            form.Dispose ();
        }
    }
}
