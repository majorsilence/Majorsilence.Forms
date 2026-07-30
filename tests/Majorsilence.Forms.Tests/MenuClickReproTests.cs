using Majorsilence.Forms;
using Majorsilence.Forms.Headless;
using Xunit;

namespace Majorsilence.Forms.Tests
{
    // Regression coverage for a real, 100%-reproducible-on-Avalonia bug: clicking a MenuStrip's
    // top-level item opened its drop down and then immediately closed it again. Root cause: showing a
    // brand-new popup window deactivates the parent (near-instant), but a brand-new window's real
    // OS/compositor-confirmed Activated notification is reliably slower than a single posted dispatcher
    // tick -- so Application.ScheduleClosePopupsOnDeactivate's "did one of our windows activate before
    // my posted check runs" race always lost, deterministically, not intermittently. That determinism
    // (not flakiness) is what points at a structural ordering mismatch rather than a coincidental race:
    // "notify the old window it lost focus" is synchronous; "confirm the new window really has focus"
    // requires a real compositor/OS round-trip. Fixed by WindowBase.Show/ShowDialog calling
    // Application.NotifyWindowActivated proactively -- the moment we ask the backend to show one of our
    // own windows -- instead of waiting for that window's own (potentially much later) Activated event
    // to confirm it after the fact.
    //
    // Existing MenuStrip/ToolStrip coverage (e.g. StripHierarchyTests.MenuStripItem_WithChildren_
    // StillOpensItsSubmenu) calls ShowDropDown() directly, so it never exercises the mouse-down/click/
    // mouse-up pipeline this bug lives in at all. The Headless backend's own Show() implementation
    // ALSO happens to fire OnBackendActivated synchronously (HeadlessWindowHost.Show() calls it
    // directly) -- unlike a real backend, where that confirmation is asynchronous. That means Headless
    // cannot actually distinguish "the fix's proactive call ran" from "Headless's own synchronous
    // activation would have covered it anyway": both tests below pass with or without the
    // WindowBase.Show/ShowDialog fix present, verified by temporarily reverting it locally. They still
    // pin the correct *observable* behavior (a real click opens and keeps a MenuStrip's drop down open;
    // showing a new window while a popup/menu is open must not close it), which is worth keeping, but
    // neither is a substitute for verifying the fix against a real backend where Activated genuinely is
    // asynchronous -- which is exactly the gap that let this bug ship in the first place.
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
        public void ShowingANewWindow_WhileAPopupIsOpen_DoesNotCloseThePopup ()
        {
            HeadlessRenderer.Use ();

            var form = new Form ();
            form.Show ();

            using var popup = new PopupWindow (form);
            Application.ActivePopupWindow = popup;

            // The parent deactivating as a side effect of the popup taking focus -- this alone always
            // used to schedule a close (Application.ScheduleClosePopupsOnDeactivate).
            form.OnBackendDeactivated ();

            popup.Show (10, 10);

            Majorsilence.Forms.Backends.Platform.Backend.DoEvents ();

            Assert.Same (popup, Application.ActivePopupWindow);

            form.Close ();
        }
    }
}
