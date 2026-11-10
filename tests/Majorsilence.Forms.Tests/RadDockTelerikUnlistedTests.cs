using System.Drawing;
using System.IO;
using System.Linq;
using System.Text;
using Majorsilence.Forms.Headless;
using Majorsilence.Forms.Telerik;
using Xunit;

namespace Majorsilence.Forms.Tests
{
    // What a layout Telerik's own dock saved does to the windows the dock already holds.
    //
    // The document below came out of probes/wfdockxml: Telerik 2026.2.520 saved it from a dock holding a
    // document (docSummary), two docked tool windows (twFilter left, twNotes bottom, its Text changed to
    // "Notes for Asset : 00003") and one auto-hidden tool window (twSide). Loading it back there gives:
    //
    //   a named window          the saved Text, tab order and the state of the strip that names it --
    //                           TabbedDocument from a DocumentTabStrip, Docked from a ToolTabStrip
    //   an unnamed tool window  DockState.Hidden, Parent null, IsDisposed FALSE
    //   an unnamed document     DockState.Hidden, Parent null, IsDisposed TRUE
    //
    // The two kinds differing is the point: disposing a tool window too took the panel out of the form
    // for good, and an unnamed window in a strip the layout never mentions was not touched at all.
    [Collection ("Headless")]
    public class RadDockTelerikUnlistedTests
    {
        private const string Layout =
            "<?xml version=\"1.0\" encoding=\"utf-8\"?><RadDock IsCleanUpTarget=\"True\" Orientation=\"Vertical\" CausesValidation=\"False\" TabIndex=\"0\" TabStop=\"False" +
            "\"><SerializableAutoHideContainer><RightAutoHideGroups><Telerik.WinControls.UI.Docking.AutoHideGroup><Windows><Telerik.WinControls.UI.Docking.Dock" +
            "WindowPlaceholder AutoHideSize=\"200, 200\" DockWindowName=\"twSide\" DockWindowText=\"twSide\" PreviousDockState=\"Docked\" Location=\"0, 0\" Font=\"Micros" +
            "oft Sans Serif, 12pt\" /></Windows></Telerik.WinControls.UI.Docking.AutoHideGroup></RightAutoHideGroups></SerializableAutoHideContainer><Controls>" +
            "<Telerik.WinControls.UI.RadSplitContainer IsCleanUpTarget=\"True\" Orientation=\"Horizontal\" Padding=\"5, 5, 5, 5\" CausesValidation=\"False\" Location=" +
            "\"5, 5\" Name=\"\" Size=\"864, 410\" TabIndex=\"0\" TabStop=\"False\"><SizeInfo SplitterCorrection=\"0, 0\" AutoSizeScale=\"0, 0\" /><Controls><Telerik.WinCont" +
            "rols.UI.RadSplitContainer IsCleanUpTarget=\"True\" Orientation=\"Vertical\" Padding=\"5, 5, 5, 5\" Location=\"0, 0\" Name=\"\" Size=\"864, 206\" TabIndex=\"0\"" +
            " TabStop=\"False\"><SizeInfo SplitterCorrection=\"0, 0\" AutoSizeScale=\"0, 0\" /><Controls><Telerik.WinControls.UI.Docking.ToolTabStrip SelectedIndex=" +
            "\"0\" Size=\"200, 206\" Location=\"0, 0\" Name=\"DockTabStrip3\" TabIndex=\"1\" TabStop=\"False\"><SizeInfo SplitterCorrection=\"0, 0\" AutoSizeScale=\"0, 0\" />" +
            "<Controls><Telerik.WinControls.UI.Docking.ToolWindow Caption=\"\" PreviousDockState=\"Docked\" Name=\"twFilter\" Size=\"198, 178\" Location=\"1, 26\" Font=" +
            "\"Microsoft Sans Serif, 12pt\" Text=\"twFilter\" /></Controls></Telerik.WinControls.UI.Docking.ToolTabStrip><Telerik.WinControls.UI.Docking.DocumentC" +
            "ontainer IsMainDocumentContainer=\"True\" Orientation=\"Vertical\" Name=\"\"><SizeInfo SplitterCorrection=\"0, 0\" AutoSizeScale=\"0, 0\" SizeMode=\"Fill\" /" +
            "><Controls><Telerik.WinControls.UI.Docking.DocumentTabStrip SelectedIndex=\"0\" Size=\"660, 206\" Location=\"0, 0\" Name=\"DockTabStrip2\" TabIndex=\"0\" T" +
            "abStop=\"False\"><SizeInfo SplitterCorrection=\"0, 0\" AutoSizeScale=\"0, 0\" /><Controls><Telerik.WinControls.UI.Docking.DocumentWindow IsPinned=\"Fals" +
            "e\" PreviousDockState=\"TabbedDocument\" Name=\"docSummary\" Size=\"648, 167\" Location=\"6, 33\" Font=\"Microsoft Sans Serif, 12pt\" Text=\"Summary\" /></Con" +
            "trols></Telerik.WinControls.UI.Docking.DocumentTabStrip></Controls></Telerik.WinControls.UI.Docking.DocumentContainer></Controls></Telerik.WinCon" +
            "trols.UI.RadSplitContainer><Telerik.WinControls.UI.Docking.ToolTabStrip SelectedIndex=\"0\" Size=\"864, 200\" Location=\"0, 210\" CausesValidation=\"Fal" +
            "se\" Name=\"DockTabStrip4\" TabIndex=\"1\" TabStop=\"False\"><SizeInfo SplitterCorrection=\"0, 0\" AutoSizeScale=\"0, 0\" /><Controls><Telerik.WinControls.U" +
            "I.Docking.ToolWindow Caption=\"\" PreviousDockState=\"Docked\" Name=\"twNotes\" Size=\"862, 172\" Location=\"1, 26\" Font=\"Microsoft Sans Serif, 12pt\" Text" +
            "=\"Notes for Asset : 00003\" /></Controls></Telerik.WinControls.UI.Docking.ToolTabStrip></Controls></Telerik.WinControls.UI.RadSplitContainer><Tele" +
            "rik.WinControls.UI.Docking.ToolTabStrip Size=\"200, 410\" Location=\"669, 5\" Name=\"DockTabStrip5\" TabIndex=\"1\" TabStop=\"False\"><SizeInfo SplitterCor" +
            "rection=\"0, 0\" AutoSizeScale=\"0, 0\" /><Controls><Telerik.WinControls.UI.Docking.ToolWindow Caption=\"\" PreviousDockState=\"Docked\" Name=\"twSide\" Si" +
            "ze=\"273, 253\" Location=\"4, 4\" Font=\"Microsoft Sans Serif, 12pt\" Text=\"twSide\" /></Controls></Telerik.WinControls.UI.Docking.ToolTabStrip></Contro" +
            "ls></RadDock>";

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

        private static void Load (RadDock dock)
            => dock.LoadFromXml (new MemoryStream (Encoding.UTF8.GetBytes (Layout)));

        private static T Window<T> (RadDock dock, string name) where T : DockWindowBase
            => dock.DockWindows.Cast<DockWindowBase> ().OfType<T> ().Single (w => w.Name == name);

        [Fact]
        public void A_document_the_layout_names_comes_back_as_a_tabbed_document ()
        {
            var (form, dock) = Build ();
            Load (dock);

            var document = Window<DocumentWindow> (dock, "docSummary");
            Assert.Equal (DockState.TabbedDocument, document.DockState);
            Assert.Equal ("Summary", document.Text);

            form.Dispose ();
        }

        [Fact]
        public void A_tool_window_the_layout_does_not_name_is_hidden_and_kept ()
        {
            var (form, dock) = Build ();
            var extra = new ToolWindow { Name = "twExtra", Text = "twExtra" };
            dock.DockWindow (extra, DockPosition.Right);

            Load (dock);

            Assert.Equal (DockState.Hidden, extra.DockState);
            Assert.Null (extra.Parent);
            Assert.False (extra.IsDisposed);
            Assert.False (extra.Visible);

            form.Dispose ();
        }

        [Fact]
        public void A_tool_window_sharing_a_named_strip_is_hidden_and_kept ()
        {
            var (form, dock) = Build ();
            var filter = Window<ToolWindow> (dock, "twFilter");
            var extra = new ToolWindow { Name = "twExtra", Text = "twExtra" };
            dock.DockWindow (extra, filter, DockPosition.Fill);
            Assert.Same (filter.Parent, extra.Parent);

            Load (dock);

            Assert.Equal (DockState.Hidden, extra.DockState);
            Assert.Null (extra.Parent);
            Assert.False (extra.IsDisposed);
            Assert.Equal (DockState.Docked, filter.DockState);

            form.Dispose ();
        }

        [Fact]
        public void A_document_the_layout_does_not_name_is_dropped_and_disposed ()
        {
            var (form, dock) = Build ();
            var extra = new DocumentWindow { Name = "docExtra", Text = "Extra" };
            dock.DockWindow (extra, DockPosition.Fill);

            Load (dock);

            Assert.DoesNotContain ("docExtra", dock.DockWindows.Cast<DockWindowBase> ().Select (w => w.Name));
            Assert.True (extra.IsDisposed);

            form.Dispose ();
        }

        [Fact]
        public void The_windows_the_layout_names_keep_their_saved_text_and_state ()
        {
            var (form, dock) = Build ();
            Load (dock);

            Assert.Equal ("Notes for Asset : 00003", Window<ToolWindow> (dock, "twNotes").Text);
            Assert.Equal (DockState.Docked, Window<ToolWindow> (dock, "twNotes").DockState);
            Assert.Equal (DockState.AutoHide, Window<ToolWindow> (dock, "twSide").DockState);
            Assert.False (Window<ToolWindow> (dock, "twSide").Visible);

            form.Dispose ();
        }
    }
}
