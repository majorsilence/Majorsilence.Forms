using System.Reflection;
using Majorsilence.Forms.Headless;
using Majorsilence.Forms.Telerik;
using Xunit;

namespace Majorsilence.Forms.Tests
{
    // W6.1, the Telerik dead-event sweep — the page-view and date-picker slice.
    //
    // All three events here were declared `add { } remove { }`, which is the worse of the two dead
    // shapes: the delegate is DISCARDED at the add site, so a handler is unreachable even by
    // reflection and a later `-=` is meaningless. A `#pragma warning disable CS0067` event at least
    // retains what it is given.
    [Collection ("Headless")]
    public class RadPageViewAndPickerEventTests
    {
        // ---------------- RadDateTimePicker.Opened

        [Fact]
        public void Opening_the_drop_down_raises_Opened ()
        {
            // An alias for the engine's DropDown, which became real in W5.20c. Handlers that populate
            // or constrain the calendar on open never ran.
            HeadlessRenderer.Use ();

            using var form = new Form { Width = 300, Height = 200 };
            var picker = new RadDateTimePicker { Width = 200, Height = 24 };
            form.Controls.Add (picker);
            form.Show ();

            try {
                var opened = 0;
                picker.Opened += (_, _) => opened++;

                picker.DroppedDown = true;

                Assert.Equal (1, opened);
            } finally {
                form.Close ();
            }
        }

        [Fact]
        public void Unsubscribing_from_Opened_really_unsubscribes ()
        {
            // The half an `add { } remove { }` event cannot do at all: with the delegate discarded,
            // `-=` has nothing to remove and the handler was never going to run anyway. This pins that
            // both directions are real.
            HeadlessRenderer.Use ();

            using var form = new Form { Width = 300, Height = 200 };
            var picker = new RadDateTimePicker { Width = 200, Height = 24 };
            form.Controls.Add (picker);
            form.Show ();

            try {
                var opened = 0;
                void Handler (object? sender, System.EventArgs e) => opened++;

                picker.Opened += Handler;
                picker.Opened -= Handler;

                picker.DroppedDown = true;

                Assert.Equal (0, opened);
            } finally {
                form.Close ();
            }
        }

        // ---------------- RadPageView.PageRemoving

        [Fact]
        public void Removing_a_page_raises_PageRemoving ()
        {
            using var view = new RadPageView ();
            var first = new RadPageViewPage { Text = "one" };
            view.Pages.Add (first);
            view.Pages.Add (new RadPageViewPage { Text = "two" });

            RadPageViewPage? announced = null;
            view.PageRemoving += (_, e) => announced = e.Page;

            view.Pages.RemoveAt (0);

            Assert.Same (first, announced);
            Assert.Single (view.Pages);
        }

        [Fact]
        public void A_handler_can_veto_the_removal ()
        {
            // The "save before closing this tab?" prompt every document UI is built around: the
            // handler never ran, so the veto never happened and the page always closed.
            using var view = new RadPageView ();
            view.Pages.Add (new RadPageViewPage { Text = "one" });
            view.Pages.Add (new RadPageViewPage { Text = "two" });

            view.PageRemoving += (_, e) => e.Cancel = true;

            view.Pages.RemoveAt (0);

            Assert.Equal (2, view.Pages.Count);
        }

        [Fact]
        public void Removal_still_works_with_no_handler ()
        {
            // GUARD, not proof: a seam that returned false by default would make every page
            // unremovable, which is a far worse bug than the one being fixed.
            using var view = new RadPageView ();
            view.Pages.Add (new RadPageViewPage { Text = "one" });
            view.Pages.Add (new RadPageViewPage { Text = "two" });

            view.Pages.RemoveAt (0);

            Assert.Single (view.Pages);
        }

        // ---------------- RadPageView.PageCollapsed

        [Fact]
        public void PageCollapsed_retains_a_handler_even_though_it_never_fires ()
        {
            // PageCollapsed cannot be raised here: Telerik raises it in RadPageView's ExplorerBar /
            // Outlook / Accordion modes, and this page view is a TabControl with no collapse concept
            // and no Mode. It is still worth being a REAL event -- `add { } remove { }` threw the
            // delegate away, so a consumer could not even detect the loss.
            //
            // Asserted through the backing field, because there is no other way to observe an event
            // that is deliberately never raised.
            using var view = new RadPageView ();

            var field = typeof (RadPageView).GetField ("PageCollapsed", BindingFlags.Instance | BindingFlags.NonPublic);

            Assert.NotNull (field);
            Assert.Null (field!.GetValue (view));

            view.PageCollapsed += (_, _) => { };

            Assert.NotNull (field.GetValue (view));
        }
    }
}
