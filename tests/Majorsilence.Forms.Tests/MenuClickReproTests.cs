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

            // file.Bounds is local to strip; WindowPoint walks the rest of the chain, including the
            // form's client area, which is what puts the strip below the caption.
            var loc = WindowPoint.In (strip, file.Bounds.X + 5, file.Bounds.Y + 5);

            // Through HeadlessRenderer, not the window's handlers directly: those take DEVICE pixels, so
            // feeding them logical coordinates lands the click at 1/scale of where it was aimed -- at
            // MF_HEADLESS_SCALE=2 that is up in the title bar rather than on the "File" item.
            HeadlessRenderer.MouseDown (form, loc.X, loc.Y);
            HeadlessRenderer.MouseUp (form, loc.X, loc.Y);
            Majorsilence.Forms.Backends.Platform.Backend.DoEvents ();

            // Read the outcome, then close BEFORE asserting: a failing assert would otherwise leak this
            // shown form in Application.OpenForms, where a later test's parameterless ShowDialog picks it
            // as modal owner and pumps forever -- turning one red test into a run that hangs until the CI
            // job times out.
            var opened = file.IsDropDownOpened;
            form.Close ();

            Assert.True (opened, "A real click on a MenuStrip top-level item should open its drop down and leave it open.");
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
