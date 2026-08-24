using System.Linq;
using Majorsilence.Forms.Headless;
using Xunit;

namespace Majorsilence.Forms.Tests
{
    // WinForms' DataGridView adds its scrollbars to Controls as ordinary children, and real code relies on
    // it: a themed control library scans grid.Controls for them so it can mirror their state onto its own
    // skinned scrollbars. Ours kept them as implicit chrome, which the public collection does not show, so
    // that scan found nothing and the library NREd while the grid painted.
    public class DataGridViewScrollBarChildrenTests
    {
        [Fact]
        public void A_grids_scrollbars_are_in_its_Controls_collection ()
        {
            HeadlessRenderer.Use ();

            using var grid = new DataGridView ();

            Assert.Contains (grid.Controls.Cast<Control> (), c => c is VScrollBar);
            Assert.Contains (grid.Controls.Cast<Control> (), c => c is HScrollBar);
        }

        [Fact]
        public void They_are_the_types_WinForms_uses_so_an_exact_type_scan_finds_them ()
        {
            HeadlessRenderer.Use ();

            using var grid = new DataGridView ();

            // The idiom in the wild is `item.GetType () == typeof (VScrollBar)`, which an exact-type
            // comparison fails for a VerticalScrollBar base instance -- so the concrete type matters, not
            // just the assignability.
            Assert.Contains (grid.Controls.Cast<Control> (), c => c.GetType () == typeof (VScrollBar));
            Assert.Contains (grid.Controls.Cast<Control> (), c => c.GetType () == typeof (HScrollBar));
        }

        [Fact]
        public void A_plain_scrollable_panel_still_reports_no_children ()
        {
            HeadlessRenderer.Use ();

            // The counterpart guarantee, and the reason this was not done as a blanket change: in WinForms
            // `new Panel ().Controls` is empty -- ScrollableControl's scrollbars are not children there
            // either. Exposing every control's chrome broke 27 tests that correctly assert this.
            using var panel = new Panel ();

            Assert.Empty (panel.Controls);
        }
    }
}
