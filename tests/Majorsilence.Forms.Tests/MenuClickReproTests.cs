using Majorsilence.Forms;
using Majorsilence.Forms.Headless;
using Xunit;

namespace Majorsilence.Forms.Tests
{
    // Regression coverage for a real, reported bug: clicking a MenuStrip's top-level item flashed its
    // drop down open and then immediately closed it again.
    //
    // Root cause, confirmed with real diagnostic logging against a real Avalonia window on a real
    // desktop (Linux, Mutter/XWayland) while the reporter clicked it: the mechanism deciding whether to
    // close a popup on the parent window's Deactivated event (Application.ScheduleClosePopupsOnDeactivate)
    // used to compare an activation-generation counter's value before and after scheduling a delayed
    // close, on the theory that "the parent deactivates near-instantly; the new popup's own Activated
    // arrives afterward and cancels the close." The logged sequence showed the OPPOSITE, consistently:
    // the popup's own Activated fired ~20-30ms BEFORE its parent's Deactivated arrived. Since nothing
    // activated AFTER the schedule captured its snapshot, the counter-comparison always saw "unchanged"
    // and closed the popup -- explaining why it was reported as happening on every click rather than
    // intermittently. (An earlier attempted fix along the same before/after-counter lines -- calling the
    // activation proactively from WindowBase.Show instead of waiting for the real event -- didn't help,
    // for the same underlying reason: it also ran before the schedule captured its snapshot in this
    // ordering, so there was still nothing left to change afterward.)
    //
    // Fixed by checking CURRENT state instead of a before/after delta: WindowBase.IsActive reflects
    // whether the backend currently considers a window active, checked directly on
    // Application.ActivePopupWindow at decision time -- both synchronously (covers the observed ordering,
    // where the popup is already active by the time the parent's Deactivated arrives) and via a
    // one-tick-later posted recheck (covers the reverse ordering, giving a delayed real Activated event
    // a chance to arrive first). Order-independent by construction, since it never compares "before" to
    // "after" -- only "is it active right now."
    //
    // Existing MenuStrip/ToolStrip coverage (e.g. StripHierarchyTests.MenuStripItem_WithChildren_
    // StillOpensItsSubmenu) calls ShowDropDown() directly, so it never exercises the mouse-down/click/
    // mouse-up pipeline this bug lives in, or the deactivate-driven close path, at all.
    public class MenuClickReproTests
    {
        [Fact]
        public void RealClick_OnMenuStripItem_OpensAndStaysOpen ()
        {
            HeadlessRenderer.Use ();

            var form = new Form ();
            var strip = new MenuStrip ();
            var file = new ToolStripMenuItem ("File");
            file.DropDownItems.Add (new ToolStripMenuItem ("Open"));
            strip.Items.Add (file);
            form.Controls.Add (strip);
            form.Show ();
            HeadlessRenderer.CapturePng (form, 500, 200);

            // file.Bounds is local to strip; strip.Bounds is local to the form (which also hosts an
            // implicit, docked FormTitleBar above it) -- add both to get form-relative coordinates.
            var loc = new System.Drawing.Point (strip.Bounds.X + file.Bounds.X + 5, strip.Bounds.Y + file.Bounds.Y + 5);

            form.HandlePointerPressed (MouseButtons.Left, loc.X, loc.Y, Keys.None);
            form.HandlePointerReleased (MouseButtons.Left, loc.X, loc.Y, Keys.None);
            Majorsilence.Forms.Backends.Platform.Backend.DoEvents ();

            Assert.True (file.IsDropDownOpened, "A real click on a MenuStrip top-level item should open its drop down and leave it open.");

            form.Close ();
        }

        [Fact]
        public void PopupAlreadyActive_WhenParentDeactivates_IsNotClosed ()
        {
            HeadlessRenderer.Use ();

            var form = new Form ();
            form.Show ();

            using var popup = new PopupWindow (form);
            Application.ActivePopupWindow = popup;

            // The popup's own Activated already arrived (this is the ordering observed on real
            // Avalonia/Linux) -- before the parent's Deactivated does.
            popup.OnBackendActivated ();
            form.OnBackendDeactivated ();

            Majorsilence.Forms.Backends.Platform.Backend.DoEvents ();

            Assert.Same (popup, Application.ActivePopupWindow);

            form.Close ();
        }

        [Fact]
        public void PopupNeverActivates_WhenParentDeactivates_IsClosed ()
        {
            HeadlessRenderer.Use ();

            var form = new Form ();
            form.Show ();

            using var popup = new PopupWindow (form);
            Application.ActivePopupWindow = popup;

            // The popup never activates at all -- simulates focus genuinely moving to a different
            // application while a popup happened to be open. This must still close it.
            form.OnBackendDeactivated ();

            Majorsilence.Forms.Backends.Platform.Backend.DoEvents ();

            Assert.Null (Application.ActivePopupWindow);

            form.Close ();
        }
    }
}
