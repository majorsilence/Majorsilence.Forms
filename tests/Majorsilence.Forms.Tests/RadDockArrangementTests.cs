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
    // Loading a layout REBUILDS the dock's split tree. Telerik's loader deserialises the saved tree and matches
    // windows into it rather than moving the panels the dock has, so a layout that holds a window somewhere else
    // puts it back there -- and the panels the dock had are thrown away, named panels included.
    //
    // Measured on UI for WinForms 2026.2.520 (probes/wfdocksplit, scenarios C and F). The document below is the one
    // Telerik saved there: a dock 900 x 420 holding docSummary, twFilter LEFT, twNotes BOTTOM (its Text changed to
    // "Notes for Asset : 00003") and twSide auto-hidden on the right.
    //
    // Loading it into a dock whose twFilter is docked RIGHT gave back the saved tree exactly:
    //
    //   RadSplitContainer   (5, 5, 864, 410)     Horizontal
    //     RadSplitContainer (0, 0, 864, 206)     Vertical
    //       ToolTabStrip    (0, 0, 200, 206)     DockTabStrip3, twFilter -- FIRST, where the dock had it last
    //       DocumentContainer (204, 0, 660, 206) Fill
    //         DocumentTabStrip (0, 0, 660, 206)  DockTabStrip2, docSummary
    //     ToolTabStrip      (0, 210, 864, 200)   DockTabStrip4, twNotes
    //   ToolTabStrip                             DockTabStrip5, empty (twSide auto-hidden), laid out nowhere
    //
    // down to the strip NAMES, which come from the document and not from the dock. Scenario F then settled that this
    // is a rebuild and not a reconcile: loading a layout back into the very dock it was saved from, where every name
    // in the document matches a live panel, still replaced six of the seven panels and left the originals disposed.
    [Collection ("Headless")]
    public class RadDockArrangementTests
    {
        private const string Layout =
            "<?xml version=\"1.0\" encoding=\"utf-8\"?><RadDock IsCleanUpTarget=\"True\" Orientation=\"Vertical\" CausesValidation=\"False\" TabIndex=\"0\" TabStop=\"False\"><Se" +
            "rializableAutoHideContainer><RightAutoHideGroups><Telerik.WinControls.UI.Docking.AutoHideGroup><Windows><Telerik.WinControls.UI.Docking.DockWindowPlac" +
            "eholder AutoHideSize=\"200, 200\" DockWindowName=\"twSide\" DockWindowText=\"twSide\" PreviousDockState=\"Docked\" Location=\"0, 0\" Font=\"Microsoft Sans Serif," +
            " 12pt\" /></Windows></Telerik.WinControls.UI.Docking.AutoHideGroup></RightAutoHideGroups></SerializableAutoHideContainer><Controls><Telerik.WinControls" +
            ".UI.RadSplitContainer IsCleanUpTarget=\"True\" Orientation=\"Horizontal\" Padding=\"5, 5, 5, 5\" CausesValidation=\"False\" Location=\"5, 5\" Name=\"\" Size=\"864," +
            " 410\" TabIndex=\"0\" TabStop=\"False\"><SizeInfo SplitterCorrection=\"0, 0\" AutoSizeScale=\"0, 0\" /><Controls><Telerik.WinControls.UI.RadSplitContainer IsCl" +
            "eanUpTarget=\"True\" Orientation=\"Vertical\" Padding=\"5, 5, 5, 5\" CausesValidation=\"False\" Location=\"0, 0\" Name=\"\" Size=\"864, 206\" TabIndex=\"0\" TabStop=\"" +
            "False\"><SizeInfo SplitterCorrection=\"0, 0\" AutoSizeScale=\"0, 0\" /><Controls><Telerik.WinControls.UI.Docking.ToolTabStrip SelectedIndex=\"0\" Size=\"200, " +
            "206\" Location=\"0, 0\" Name=\"DockTabStrip3\" TabIndex=\"1\" TabStop=\"False\"><SizeInfo SplitterCorrection=\"0, 0\" AutoSizeScale=\"0, 0\" /><Controls><Telerik.W" +
            "inControls.UI.Docking.ToolWindow Caption=\"\" PreviousDockState=\"Docked\" Name=\"twFilter\" Size=\"198, 178\" Location=\"1, 26\" Font=\"Microsoft Sans Serif, 12" +
            "pt\" Text=\"twFilter\" /></Controls></Telerik.WinControls.UI.Docking.ToolTabStrip><Telerik.WinControls.UI.Docking.DocumentContainer IsMainDocumentContain" +
            "er=\"True\" Orientation=\"Vertical\" CausesValidation=\"False\" Name=\"\"><SizeInfo SplitterCorrection=\"0, 0\" AutoSizeScale=\"0, 0\" SizeMode=\"Fill\" /><Controls" +
            "><Telerik.WinControls.UI.Docking.DocumentTabStrip SelectedIndex=\"0\" Size=\"660, 206\" Location=\"0, 0\" CausesValidation=\"False\" Name=\"DockTabStrip2\" TabI" +
            "ndex=\"0\" TabStop=\"False\"><SizeInfo SplitterCorrection=\"0, 0\" AutoSizeScale=\"0, 0\" /><Controls><Telerik.WinControls.UI.Docking.DocumentWindow IsPinned=" +
            "\"False\" PreviousDockState=\"TabbedDocument\" Name=\"docSummary\" Size=\"648, 167\" Location=\"6, 33\" Font=\"Microsoft Sans Serif, 12pt\" Text=\"Summary\" /></Con" +
            "trols></Telerik.WinControls.UI.Docking.DocumentTabStrip></Controls></Telerik.WinControls.UI.Docking.DocumentContainer></Controls></Telerik.WinControls" +
            ".UI.RadSplitContainer><Telerik.WinControls.UI.Docking.ToolTabStrip SelectedIndex=\"0\" Size=\"864, 200\" Location=\"0, 210\" Name=\"DockTabStrip4\" TabIndex=\"" +
            "1\" TabStop=\"False\"><SizeInfo SplitterCorrection=\"0, 0\" AutoSizeScale=\"0, 0\" /><Controls><Telerik.WinControls.UI.Docking.ToolWindow Caption=\"\" Previous" +
            "DockState=\"Docked\" Name=\"twNotes\" Size=\"862, 172\" Location=\"1, 26\" Font=\"Microsoft Sans Serif, 12pt\" Text=\"Notes for Asset : 00003\" /></Controls></Tel" +
            "erik.WinControls.UI.Docking.ToolTabStrip></Controls></Telerik.WinControls.UI.RadSplitContainer><Telerik.WinControls.UI.Docking.ToolTabStrip Size=\"200," +
            " 410\" Location=\"669, 5\" Name=\"DockTabStrip5\" TabIndex=\"1\" TabStop=\"False\"><SizeInfo SplitterCorrection=\"0, 0\" AutoSizeScale=\"0, 0\" /><Controls><Teleri" +
            "k.WinControls.UI.Docking.ToolWindow Caption=\"\" PreviousDockState=\"Docked\" Name=\"twSide\" Size=\"273, 253\" Location=\"4, 4\" Font=\"Microsoft Sans Serif, 12" +
            "pt\" Text=\"twSide\" /></Controls></Telerik.WinControls.UI.Docking.ToolTabStrip></Controls></RadDock>";
        // The dock Telerik's scenario C built: twFilter docked RIGHT, where the layout holds it LEFT.
        private static (Form form, RadDock dock) Build (DockPosition filter = DockPosition.Right, DockPosition notes = DockPosition.Bottom)
        {
            HeadlessRenderer.Use ();

            var form = new Form { Size = new Size (960, 520) };
            var dock = new RadDock { Left = 0, Top = 0, Width = 900, Height = 420 };
            form.Controls.Add (dock);

            dock.DockWindow (new DocumentWindow { Name = "docSummary", Text = "Summary" }, DockPosition.Fill);
            dock.DockWindow (new ToolWindow { Name = "twFilter", Text = "twFilter" }, filter);
            dock.DockWindow (new ToolWindow { Name = "twNotes", Text = "twNotes" }, notes);
            dock.DockWindow (new ToolWindow { Name = "twSide", Text = "twSide" }, DockPosition.Right);

            form.Show ();
            HeadlessRenderer.CapturePng (form);
            return (form, dock);
        }

        private static void Load (Form form, RadDock dock)
        {
            dock.LoadFromXml (new MemoryStream (Encoding.UTF8.GetBytes (Layout)));
            form.PerformLayout ();
            HeadlessRenderer.CapturePng (form);
        }

        private static T Window<T> (RadDock dock, string name) where T : DockWindowBase
            => dock.DockWindows.Cast<DockWindowBase> ().OfType<T> ().Single (w => w.Name == name);

        // Every panel of the dock's split tree, in tree order. An AutoHideTabStrip is a ToolTabStrip by type but
        // holds the tabs of hidden windows rather than taking part in the split, so it is left out.
        private static List<Majorsilence.Forms.Control> Panels (Majorsilence.Forms.Control root)
        {
            var found = new List<Majorsilence.Forms.Control> ();

            foreach (Majorsilence.Forms.Control child in root.Controls) {
                if (child is not AutoHideTabStrip && SplitLayout.SizeInfoOf (child) is not null)
                    found.Add (child);

                found.AddRange (Panels (child));
            }

            return found;
        }

        // The headline. twFilter is docked RIGHT, so its strip sits AFTER the documents' container; the layout holds
        // it LEFT, so it has to come back before it. Telerik: the strip at (0, 0, 200, 206) and the container at
        // (204, 0, 660, 206). Matching windows into the strips they already sit in -- what this did before -- gave
        // the strip its saved SIZE but left it on the right.
        [Fact]
        public void A_layout_that_holds_a_window_on_the_other_side_puts_it_back_there ()
        {
            var (form, dock) = Build ();

            var before = (ToolTabStrip) Window<ToolWindow> (dock, "twFilter").Parent!;
            var documents_before = dock.MainDocumentContainer!;
            Assert.True (before.Bounds.Left > documents_before.Bounds.Left, "twFilter starts to the RIGHT of the documents");

            Load (form, dock);

            var strip = (ToolTabStrip) Window<ToolWindow> (dock, "twFilter").Parent!;
            var documents = dock.MainDocumentContainer!;

            Assert.Equal (new Rectangle (0, 0, 200, 206), strip.Bounds);
            Assert.Equal (new Rectangle (204, 0, 660, 206), documents.Bounds);
            Assert.Same (strip.Parent, documents.Parent);
            Assert.Equal (0, strip.Parent!.Controls.GetChildIndex (strip));
            Assert.Equal (1, strip.Parent.Controls.GetChildIndex (documents));

            form.Dispose ();
        }

        // The NESTING, which is the part of the arrangement the sizes alone can never reach. This dock is built
        // with twNotes docked RIGHT, so nothing in it is stacked top to bottom: every panel sits side by side in one
        // flat row and the dock holds no Horizontal container at all. The document nests two -- a Horizontal
        // container holding a Vertical one and twNotes' strip below it -- so the shape below can only come from
        // deserialising the saved tree.
        //
        // Starting instead from the dock of the other tests, which already nests the same way, this test passed
        // without any of the change: worth saying, since a shape test that the old code satisfies proves nothing.
        [Fact]
        public void The_whole_tree_comes_back_in_the_shape_the_document_holds ()
        {
            var (form, dock) = Build (notes: DockPosition.Right);

            Assert.DoesNotContain (Panels (dock).OfType<RadSplitContainer> (), c => c.Orientation == Orientation.Horizontal);
            Assert.True (Window<ToolWindow> (dock, "twNotes").Parent!.Bounds.Top == 5, "twNotes starts beside the documents, not under them");

            Load (form, dock);

            var outer = Assert.IsType<RadSplitContainer> (dock.Controls.OfType<Majorsilence.Forms.Control> ().First (c => SplitLayout.SizeInfoOf (c) is not null));
            Assert.Equal (new Rectangle (5, 5, 864, 410), outer.Bounds);
            Assert.Equal (Orientation.Horizontal, outer.Orientation);

            var inner = Assert.IsType<RadSplitContainer> (outer.Controls.OfType<Majorsilence.Forms.Control> ().First ());
            Assert.Equal (new Rectangle (0, 0, 864, 206), inner.Bounds);
            Assert.Equal (Orientation.Vertical, inner.Orientation);

            Assert.Equal (new Rectangle (0, 210, 864, 200), Window<ToolWindow> (dock, "twNotes").Parent!.Bounds);
            Assert.Equal (new Rectangle (0, 0, 660, 206), Window<DocumentWindow> (dock, "docSummary").Parent!.Bounds);

            form.Dispose ();
        }

        // The names come from the document too, which is how the rebuild shows itself most plainly: the dock's own
        // strips were never called these.
        [Fact]
        public void The_strips_come_back_under_the_names_the_document_gave_them ()
        {
            var (form, dock) = Build ();

            Assert.DoesNotContain ("DockTabStrip3", Panels (dock).Select (p => p.Name));

            Load (form, dock);

            Assert.Equal ("DockTabStrip3", Window<ToolWindow> (dock, "twFilter").Parent!.Name);
            Assert.Equal ("DockTabStrip2", Window<DocumentWindow> (dock, "docSummary").Parent!.Name);
            Assert.Equal ("DockTabStrip4", Window<ToolWindow> (dock, "twNotes").Parent!.Name);

            form.Dispose ();
        }

        // Measured in scenario F: the panels the dock had are disposed, not reused, even where a name matches.
        [Fact]
        public void The_panels_the_dock_had_are_disposed_rather_than_moved ()
        {
            var (form, dock) = Build ();
            var before = Panels (dock);
            Assert.NotEmpty (before);

            Load (form, dock);

            var after = Panels (dock);
            Assert.All (before, panel => Assert.True (panel.IsDisposed, panel.GetType ().Name + " should be disposed"));
            Assert.All (after, panel => Assert.DoesNotContain (panel, before));
            Assert.NotEmpty (after);

            form.Dispose ();
        }

        // A window the rebuild cannot place has to come OUT of the panel being disposed, not go down with it: a
        // control disposes its children, and a tool window Telerik keeps alive would otherwise be gone for good --
        // the application still holding a reference to a disposed panel.
        [Fact]
        public void A_window_the_document_cannot_name_is_taken_out_of_a_discarded_strip_alive ()
        {
            var (form, dock) = Build ();

            // No Name, so no document can ever match it.
            var nameless = new ToolWindow { Text = "scratch" };
            dock.DockWindow (nameless, DockPosition.Bottom);
            var strip = nameless.Parent!;

            Load (form, dock);

            Assert.True (strip.IsDisposed);
            Assert.False (nameless.IsDisposed);
            Assert.Null (nameless.Parent);

            form.Dispose ();
        }

        // The dock's own Orientation is the document's as well, and it decides whether the dock lays its panels out
        // side by side or one above the other -- so a rebuilt tree read into a dock left turned the other way would
        // come out at right angles to the layout it was saved from.
        [Fact]
        public void The_dock_takes_the_orientation_the_document_holds ()
        {
            var (form, dock) = Build ();
            dock.Orientation = Orientation.Horizontal;
            HeadlessRenderer.CapturePng (form);

            Load (form, dock);

            Assert.Equal (Orientation.Vertical, dock.Orientation);
            Assert.Equal (new Rectangle (5, 5, 864, 410), Panels (dock)[0].Bounds);

            form.Dispose ();
        }

        // The rebuilt container marked IsMainDocumentContainer takes over: the one the dock had is among the panels
        // being disposed, and a dock still pointing at it would lay its documents out inside a dead panel.
        [Fact]
        public void The_main_document_container_is_the_rebuilt_one ()
        {
            var (form, dock) = Build ();
            var before = dock.MainDocumentContainer;

            Load (form, dock);

            Assert.NotNull (dock.MainDocumentContainer);
            Assert.NotSame (before, dock.MainDocumentContainer);
            Assert.False (dock.MainDocumentContainer!.IsDisposed);
            Assert.Same (dock.MainDocumentContainer, Window<DocumentWindow> (dock, "docSummary").Parent!.Parent);
            Assert.Equal (SplitPanelSizeMode.Fill, dock.MainDocumentContainer.SizeInfo.SizeMode);

            form.Dispose ();
        }
    }
}
