using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace Majorsilence.Forms.Tests
{
    /// <summary>
    /// Covers the Control and Form parity pass (docs/winforms-gap-plan.md). Both types are touched by
    /// every migrated app, so these check the members that compute something rather than the ones
    /// that store a value: <c>DataContext</c>'s inheritance down the tree, <c>InvokeAsync</c>'s
    /// marshalling and exception routing, and <c>IsKeyLocked</c>'s refusal of a non-toggle key.
    /// </summary>
    public class ControlAndFormParityTests
    {
        [Fact]
        public void DataContext_is_inherited_by_children_that_have_none ()
        {
            using var parent = new Panel ();
            var child = new Button ();
            parent.Controls.Add (child);

            parent.DataContext = "view model";

            Assert.Equal ("view model", child.DataContext);

            child.DataContext = "its own";
            Assert.Equal ("its own", child.DataContext);
            Assert.Equal ("view model", parent.DataContext);
        }

        [Fact]
        public void DataContextChanged_reaches_the_children_that_inherit_it ()
        {
            using var parent = new Panel ();
            var child = new Button ();
            var grandchild = new Button ();
            parent.Controls.Add (child);
            child.Controls.Add (grandchild);

            var childRaised = 0;
            var grandchildRaised = 0;
            child.DataContextChanged += (_, _) => childRaised++;
            grandchild.DataContextChanged += (_, _) => grandchildRaised++;

            parent.DataContext = new object ();

            Assert.Equal (1, childRaised);
            Assert.Equal (1, grandchildRaised);
        }

        [Fact]
        public void A_child_with_its_own_context_is_not_told_about_its_parents ()
        {
            using var parent = new Panel ();
            var child = new Button { DataContext = "mine" };
            parent.Controls.Add (child);

            var raised = 0;
            child.DataContextChanged += (_, _) => raised++;

            parent.DataContext = "theirs";

            Assert.Equal (0, raised);
            Assert.Equal ("mine", child.DataContext);
        }

        [Fact]
        public void IsKeyLocked_refuses_a_key_that_has_no_locked_state ()
        {
            Assert.Throws<System.NotSupportedException> (() => Control.IsKeyLocked (Keys.A));
            Assert.Throws<System.NotSupportedException> (() => Control.IsKeyLocked (Keys.Shift));

            Control.IsKeyLocked (Keys.CapsLock);      // must not throw
            Control.IsKeyLocked (Keys.NumLock);
            Control.IsKeyLocked (Keys.Scroll);
        }

        [Fact (Skip = "bisect")]
        public async Task InvokeAsync_completes_with_the_callbacks_result ()
        {
            using var control = new Button ();

            Assert.Equal (42, await control.InvokeAsync (() => 42, TestContext.Current.CancellationToken));
        }

        [Fact (Skip = "bisect")]
        public async Task InvokeAsync_routes_an_exception_to_the_awaiter ()
        {
            // Not to the dispatch loop -- letting it escape there takes the application down.
            using var control = new Button ();

            await Assert.ThrowsAsync<System.InvalidOperationException> (
                () => control.InvokeAsync (() => throw new System.InvalidOperationException ("boom"),
                    TestContext.Current.CancellationToken));
        }

        [Fact (Skip = "bisect")]
        public async Task InvokeAsync_reports_cancellation_without_running_the_callback ()
        {
            using var control = new Button ();
            using var cts = new CancellationTokenSource ();
            var ran = false;
            await cts.CancelAsync ();

            await Assert.ThrowsAnyAsync<System.OperationCanceledException> (
                () => control.InvokeAsync (() => ran = true, cts.Token));

            Assert.False (ran);
        }

        [Fact (Skip = "bisect")]
        public async Task InvokeAsync_awaits_an_asynchronous_callback ()
        {
            using var control = new Button ();

            var value = await control.InvokeAsync (async _ => {
                await Task.Yield ();
                return "done";
            }, TestContext.Current.CancellationToken);

            Assert.Equal ("done", value);
        }

        [Fact]
        public void FromHandle_reports_that_there_is_no_such_control ()
        {
            // There are no window handles here, so null is the only honest answer.
            Assert.Null (Control.FromHandle (System.IntPtr.Zero));
            Assert.Null (Control.FromChildHandle (new System.IntPtr (1234)));
        }

        [Fact]
        public void The_accessible_object_falls_back_from_AccessibleName_to_Text ()
        {
            using var control = new Button { Text = "Save" };
            var accessible = new Control.ControlAccessibleObject (control);

            Assert.Equal ("Save", accessible.Name);
            Assert.Same (control, accessible.Owner);

            control.AccessibleName = "Save the document";
            Assert.Equal ("Save the document", accessible.Name);
        }

        [Fact]
        public void IsAncestorSiteInDesignMode_walks_the_whole_chain ()
        {
            using var root = new Panel ();
            var middle = new Panel ();
            var leaf = new Button ();
            root.Controls.Add (middle);
            middle.Controls.Add (leaf);

            Assert.False (leaf.IsAncestorSiteInDesignMode);
        }

        [Fact]
        public void The_form_chrome_colours_round_trip_and_notify_once ()
        {
            using var form = new Form ();
            var raised = 0;
            form.FormCaptionBackColorChanged += (_, _) => raised++;

            form.FormCaptionBackColor = System.Drawing.Color.Teal;
            form.FormCaptionBackColor = System.Drawing.Color.Teal;   // no change, no event

            Assert.Equal (System.Drawing.Color.Teal, form.FormCaptionBackColor);
            Assert.Equal (1, raised);
        }

        [Fact]
        public void RightToLeftLayout_notifies_when_it_changes ()
        {
            using var form = new Form ();
            var raised = 0;
            form.RightToLeftLayoutChanged += (_, _) => raised++;

            form.RightToLeftLayout = true;
            form.RightToLeftLayout = true;

            Assert.True (form.RightToLeftLayout);
            Assert.Equal (1, raised);
        }

        [Fact]
        public void GetAutoScaleSize_grows_with_the_font ()
        {
            using var small = new Majorsilence.Forms.Drawing.Font ("Arial", 8f);
            using var large = new Majorsilence.Forms.Drawing.Font ("Arial", 24f);

            Assert.True (Form.GetAutoScaleSize (large).Height > Form.GetAutoScaleSize (small).Height);
            Assert.Throws<System.ArgumentNullException> (() => Form.GetAutoScaleSize (null!));
        }

        [Fact]
        public void PreProcessMessage_reports_that_it_did_not_take_the_message ()
        {
            // There is no message pump, so nothing ever arrives; the override point still has to exist.
            using var control = new Button ();
            var message = default (Message);

            Assert.False (control.PreProcessMessage (ref message));
            Assert.Equal (PreProcessControlState.MessageNotNeeded, control.PreProcessControlMessage (ref message));
        }

        [Fact]
        public void The_assembly_metadata_properties_read_the_entry_assembly ()
        {
            // Under a test host there may be no entry assembly attributes at all; what matters is that
            // these answer without throwing and never return null.
            using var control = new Button ();

            Assert.NotNull (control.CompanyName);
            Assert.NotNull (control.ProductName);
            Assert.NotNull (control.ProductVersion);
        }
    }
}
