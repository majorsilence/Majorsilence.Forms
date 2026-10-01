using System;
using System.ComponentModel;
using Majorsilence.Forms.Headless;
using Xunit;

namespace Majorsilence.Forms.Tests
{
    // A binding to a source member that does not exist used to do nothing at all (#290): ReadValue
    // returned the moment SourceProperty came back null, so a typo in a view-model property name and a
    // value that simply had not changed yet were indistinguishable -- and on a trimmed build, a
    // member trimmed away looked exactly the same as both. These tests pin the fix: the failure is now
    // loud, at the point `Add` is called, and names the member -- the same shape the TARGET side
    // already has for its own missing-member case (BindingRuntimeTests.
    // Binding_a_property_that_does_not_exist_is_reported_not_ignored, BND-30).
    [Collection ("Headless")]
    public class BindingMissingMemberTests
    {
        private sealed class Person : INotifyPropertyChanged
        {
            public string? Name { get; set; }

#pragma warning disable CS0067 // never raised: nothing here needs a live source, only its shape
            public event PropertyChangedEventHandler? PropertyChanged;
#pragma warning restore CS0067
        }

        [Fact]
        public void A_missing_source_member_throws_and_names_it ()
        {
            HeadlessRenderer.Use ();

            using var box = new TextBox ();

            var exception = Assert.Throws<ArgumentException> (
                () => box.DataBindings.Add ("Text", new Person { Name = "Ada" }, "NoSuchProperty"));

            Assert.Contains ("NoSuchProperty", exception.Message);
            Assert.Contains ("DataSource", exception.Message);
        }

        [Fact]
        public void An_empty_data_member_still_means_bind_to_the_object_itself_not_a_missing_member ()
        {
            // BindingMemberInfo.BindingField is "" when the caller passes no dataMember -- the
            // documented "bind to the whole object" shape (a plain ComboBox.SelectedItem-style
            // binding), not a lookup failure, so this must not throw.
            HeadlessRenderer.Use ();

            using var box = new TextBox ();

            var exception = Record.Exception (() => box.DataBindings.Add ("Text", new Person { Name = "Ada" }, ""));

            Assert.Null (exception);
        }

        [Fact]
        public void A_source_member_that_does_resolve_is_unaffected ()
        {
            // Guards against an over-eager fix that throws whenever a source is merely present,
            // rather than only when the named member truly does not resolve.
            HeadlessRenderer.Use ();

            using var box = new TextBox ();
            box.DataBindings.Add ("Text", new Person { Name = "Ada" }, "Name");

            Assert.Equal ("Ada", box.Text);
        }
    }
}
