using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using Majorsilence.Forms;
using Xunit;

// CA1859 wants these locals typed as BindingSource rather than the interface. Here the interface type
// is the assertion: designer code holds the object as ISupportInitialize and calls through it, so
// narrowing to the concrete type would leave the tests passing even if the interface were dropped.
#pragma warning disable CA1859

namespace Majorsilence.Forms.Tests
{
    /// <summary>
    /// BindingSource must implement ISupportInitialize: every generated designer file wraps its
    /// property assignments in ((ISupportInitialize)bindingSource).BeginInit()/EndInit(), so a
    /// BindingSource that does not implement it throws InvalidCastException before the form is shown.
    /// </summary>
    public class BindingSourceInitializeTests
    {
        [Fact]
        public void BindingSource_is_castable_to_ISupportInitialize ()
        {
            var source = new BindingSource ();

            // The designer's exact cast. Without the interface this is InvalidCastException.
            var init = (ISupportInitialize)source;
            init.BeginInit ();
            init.EndInit ();
        }

        [Fact]
        public void EndInit_resolves_the_source_and_member_assigned_between_the_calls ()
        {
            var set = new DataSet ();
            var table = set.Tables.Add ("people");
            table.Columns.Add ("name", typeof (string));
            table.Rows.Add ("ada");

            var source = new BindingSource ();
            var init = (ISupportInitialize)source;

            init.BeginInit ();
            source.DataSource = set;      // resolves to nothing on its own -- member not set yet
            source.DataMember = "people";
            init.EndInit ();

            Assert.Single (source.List);
            Assert.Equal (0, source.Position);
        }

        [Fact]
        public void Resolution_is_suspended_until_EndInit ()
        {
            var set = new DataSet ();
            var table = set.Tables.Add ("people");
            table.Columns.Add ("name", typeof (string));
            table.Rows.Add ("ada");

            var source = new BindingSource ();
            var init = (ISupportInitialize)source;

            init.BeginInit ();
            source.DataSource = set;
            source.DataMember = "people";

            // Still suspended: the list must not have been resolved by either setter.
            Assert.Empty (source.List);

            init.EndInit ();
            Assert.Single (source.List);
        }

        [Fact]
        public void IsInitialized_tracks_the_init_span_and_Initialized_fires_once ()
        {
            var source = new BindingSource ();
            var init = (ISupportInitialize)source;
            int fired = 0;
            source.Initialized += (_, _) => fired++;

            Assert.True (source.IsInitialized);

            init.BeginInit ();
            Assert.False (source.IsInitialized);
            Assert.Equal (0, fired);

            init.EndInit ();
            Assert.True (source.IsInitialized);
            Assert.Equal (1, fired);

            // A stray EndInit with no matching BeginInit is ignored rather than re-raising.
            init.EndInit ();
            Assert.Equal (1, fired);
        }

        [Fact]
        public void Assignment_outside_an_init_span_still_resolves_immediately ()
        {
            var source = new BindingSource ();
            source.DataSource = new List<string> { "ada", "grace" };

            Assert.Equal (2, source.List.Count);
        }

        [Fact]
        public void BindingSource_reports_initialization_through_ISupportInitializeNotification ()
        {
            var source = new BindingSource ();
            var notify = (ISupportInitializeNotification)source;
            bool raised = false;
            notify.Initialized += (_, _) => raised = true;

            Assert.True (notify.IsInitialized);
            source.BeginInit ();
            Assert.False (notify.IsInitialized);
            source.EndInit ();

            Assert.True (notify.IsInitialized);
            Assert.True (raised);
        }
    }
}

#pragma warning restore CA1859
