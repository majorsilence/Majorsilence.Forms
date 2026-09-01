using System.Collections.Generic;
using System.ComponentModel;
using Xunit;

namespace Majorsilence.Forms.Tests
{
    /// <summary>
    /// Covers the Application, MenuItem and BindingSource parity pass (docs/winforms-gap-plan.md).
    ///
    /// BindingSource is the part worth guarding: its <c>Supports*</c> properties have to answer from
    /// the bound list rather than returning a constant, because whether sorting works depends on what
    /// was bound — a <c>List&lt;T&gt;</c> cannot sort itself and a <c>BindingList&lt;T&gt;</c> can.
    /// </summary>
    public class AppMenuBindingParityTests
    {
        private sealed class Row
        {
            public string Name { get; set; } = string.Empty;
        }

        [Fact]
        public void Mnemonic_reads_the_marked_character ()
        {
            Assert.Equal ('S', new MenuItem ("&Save").Mnemonic);
            Assert.Equal ('x', new MenuItem ("E&xit").Mnemonic);
            Assert.Equal ('\0', new MenuItem ("Save").Mnemonic);
        }

        [Fact]
        public void Mnemonic_ignores_an_escaped_ampersand ()
        {
            // "&&" prints one ampersand; it does not mark the next character.
            Assert.Equal ('\0', new MenuItem ("Save && Close").Mnemonic);
            Assert.Equal ('C', new MenuItem ("Save && &Close").Mnemonic);
        }

        [Fact]
        public void Index_reports_and_moves_the_item_within_its_parent ()
        {
            var parent = new MenuItem ("File");
            var first = new MenuItem ("One");
            var second = new MenuItem ("Two");
            parent.Items.Add (first);
            parent.Items.Add (second);

            Assert.Equal (0, first.Index);
            Assert.Equal (1, second.Index);

            second.Index = 0;

            Assert.Equal (0, second.Index);
            Assert.Equal (1, first.Index);
        }

        [Fact]
        public void An_unparented_item_has_no_index ()
            => Assert.Equal (-1, new MenuItem ("Orphan").Index);

        [Fact]
        public void IsParent_reports_whether_there_is_a_submenu ()
        {
            var item = new MenuItem ("File");

            Assert.False (item.IsParent);

            item.Items.Add (new MenuItem ("Open"));

            Assert.True (item.IsParent);
        }

        [Fact]
        public void CloneMenu_copies_the_state_and_the_submenu ()
        {
            var source = new MenuItem ("&File") {
                Checked = true,
                RadioCheck = true,
                Shortcut = Shortcut.CtrlS,
                MergeOrder = 3,
                MergeType = MenuMerge.MergeItems,
            };
            source.Items.Add (new MenuItem ("Open"));

            var clone = source.CloneMenu ();

            Assert.NotSame (source, clone);
            Assert.Equal ("&File", clone.Text);
            Assert.True (clone.Checked);
            Assert.True (clone.RadioCheck);
            Assert.Equal (Shortcut.CtrlS, clone.Shortcut);
            Assert.Equal (3, clone.MergeOrder);
            Assert.Equal (MenuMerge.MergeItems, clone.MergeType);
            Assert.Equal (1, clone.Items.Count);
            Assert.NotSame (source.Items[0], clone.Items[0]);
        }

        [Fact]
        public void MergeMenu_honours_the_sources_merge_type ()
        {
            var target = new MenuItem ("File");
            target.Items.Add (new MenuItem ("Existing"));

            var removed = new MenuItem ("Ignored") { MergeType = MenuMerge.Remove };
            target.MergeMenu (removed);
            Assert.Equal (1, target.Items.Count);          // Remove contributes nothing

            var replacement = new MenuItem ("Fresh") { MergeType = MenuMerge.Replace };
            target.MergeMenu (replacement);
            Assert.Equal (1, target.Items.Count);          // Replace cleared, then added
            Assert.Equal ("Fresh", target.Items[0].Text);
        }

        [Fact]
        public void PerformClick_raises_the_items_Click_event ()
        {
            var item = new MenuItem ("Save");
            var clicked = 0;
            item.Click += (_, _) => clicked++;

            item.PerformClick ();

            Assert.Equal (1, clicked);
        }

        [Fact]
        public void PerformSelect_raises_the_Select_event ()
        {
            var item = new MenuItem ("Save");
            var selected = 0;
            item.Select += (_, _) => selected++;

            item.PerformSelect ();

            Assert.Equal (1, selected);
        }

        [Fact]
        public void BindingSource_reports_what_the_bound_list_actually_supports ()
        {
            // A plain List<T> notifies nothing and sorts nothing; saying otherwise would make a grid
            // wait for change events that never arrive.
            var plain = new BindingSource { DataSource = new List<Row> { new () { Name = "a" } } };

            Assert.False (plain.SupportsChangeNotification);
            Assert.False (plain.SupportsSorting);
            Assert.False (plain.SupportsFiltering);
            Assert.False (plain.IsSorted);
        }

        [Fact]
        public void A_BindingList_reports_change_notification ()
        {
            var bindable = new BindingSource { DataSource = new BindingList<Row> { new () { Name = "a" } } };

            Assert.True (bindable.SupportsChangeNotification);
        }

        [Fact]
        public void ApplySort_records_the_sort_even_when_the_list_cannot_perform_it ()
        {
            var source = new BindingSource { DataSource = new List<Row> { new () { Name = "b" }, new () { Name = "a" } } };
            var property = TypeDescriptor.GetProperties (typeof (Row))[nameof (Row.Name)]!;

            source.ApplySort (property, ListSortDirection.Descending);

            Assert.Equal ("Name DESC", source.Sort);
            Assert.False (source.IsSorted);       // the list still has not sorted, and says so

            source.RemoveSort ();
            Assert.Null (source.Sort);
        }

        [Fact]
        public void ApplySort_rejects_a_null_property ()
        {
            var source = new BindingSource ();

            Assert.Throws<System.ArgumentNullException> (
                () => source.ApplySort ((PropertyDescriptor)null!, ListSortDirection.Ascending));
        }

        [Fact]
        public void Suspend_and_resume_binding_track_their_state ()
        {
            var source = new BindingSource ();

            Assert.False (source.IsBindingSuspended);

            source.SuspendBinding ();
            Assert.True (source.IsBindingSuspended);

            source.ResumeBinding ();
            Assert.False (source.IsBindingSuspended);
        }

        [Fact]
        public void RemoveFilter_clears_the_filter ()
        {
            var source = new BindingSource { DataSource = new List<Row> (), Filter = "Name = 'a'" };

            source.RemoveFilter ();

            Assert.Null (source.Filter);
        }

        [Fact]
        public void List_exposes_the_bound_items ()
        {
            var rows = new List<Row> { new () { Name = "a" }, new () { Name = "b" } };
            var source = new BindingSource { DataSource = rows };

            Assert.Equal (2, source.List.Count);

            // Inverted with W4.1 (BND-01): the manager now wraps the BindingSource itself, exactly as
            // upstream (`new CurrencyManager(this)`), because the BindingSource is the list identity
            // that survives a DataSource re-resolve. Asserting it wrapped the INNER list pinned the
            // design that orphaned every binding attached before the data arrived.
            Assert.Same (source, source.CurrencyManager.List);
            Assert.Equal (2, source.CurrencyManager.List!.Count);
        }

        [Fact]
        public void SetColorMode_is_what_ColorMode_reports ()
        {
            // It used to discard its argument, which left ColorMode with nothing to report.
            var original = Application.ColorMode;

            try {
                Application.SetColorMode (SystemColorMode.Dark);

                Assert.Equal (SystemColorMode.Dark, Application.ColorMode);
                Assert.True (Application.IsDarkModeEnabled);

                Application.SetColorMode (SystemColorMode.Classic);
                Assert.False (Application.IsDarkModeEnabled);
            } finally {
                Application.SetColorMode (original);
            }
        }

        [Fact]
        public void OnThreadException_reaches_a_registered_handler ()
        {
            System.Exception? seen = null;
            void Handler (object sender, System.Threading.ThreadExceptionEventArgs e) => seen = e.Exception;

            Application.ThreadException += Handler;

            try {
                Application.OnThreadException (new System.InvalidOperationException ("boom"));
            } finally {
                Application.ThreadException -= Handler;
            }

            Assert.IsType<System.InvalidOperationException> (seen);
        }

        [Fact]
        public void RaiseIdle_reaches_a_registered_handler ()
        {
            var raised = 0;
            void Handler (object? sender, System.EventArgs e) => raised++;

            Application.Idle += Handler;

            try {
                Application.RaiseIdle (System.EventArgs.Empty);
            } finally {
                Application.Idle -= Handler;
            }

            Assert.Equal (1, raised);
        }

        [Fact]
        public void SetSuspendState_reports_that_the_request_was_refused ()
        {
            // False is how WinForms reports a refusal, so a caller that checks behaves correctly.
            Assert.False (Application.SetSuspendState (PowerState.Suspend, force: false, disableWakeEvent: false));
        }
    }
}
