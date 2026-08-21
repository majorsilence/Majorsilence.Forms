using System.Drawing;
using System.Linq;
using Majorsilence.Forms.Headless;
using Xunit;

namespace Majorsilence.Forms.Tests
{
    // Data binding is a COMPILE-compatibility surface here, not a working facility -- Binding.WriteValue is
    // an empty stub, so nothing moves a value yet. These tests therefore check what can be true today: that
    // the members exist, and that they are wired to the right OBJECTS, so implementing Binding later makes
    // them work rather than making them quietly wrong.
    public class WindowDataBindingParityTests
    {
        [Fact]
        public void A_windows_DataBindings_belong_to_the_window_not_to_its_adapter ()
        {
            HeadlessRenderer.Use ();

            using var form = new Form ();

            // The whole point of the collection: `form.DataBindings.Add ("Text", src, "Title")` is a
            // statement about the WINDOW's title. Handing back the adapter's collection would compile and
            // bind the adapter's Text instead -- a different property that nothing displays.
            Assert.Same (form, form.DataBindings.Control);
            Assert.Same (form, form.DataBindings.BindableComponent);
        }

        [Fact]
        public void The_collection_is_the_same_instance_each_time ()
        {
            HeadlessRenderer.Use ();

            using var form = new Form ();

            Assert.Same (form.DataBindings, form.DataBindings);
        }

        [Fact]
        public void Adding_a_binding_and_resetting_does_not_throw ()
        {
            HeadlessRenderer.Use ();

            using var form = new Form ();
            form.DataBindings.Add ("Text", new { Title = "hello" }, "Title");

            Assert.Single (form.DataBindings);

            form.ResetBindings ();   // no-ops today; must stay callable
        }

        [Fact]
        public void DataContext_is_inherited_by_the_windows_children ()
        {
            HeadlessRenderer.Use ();

            using var form = new Form { ClientSize = new Size (200, 150) };
            var panel = new Panel ();
            var nested = new Button ();
            panel.Controls.Add (nested);
            form.Controls.Add (panel);

            var source = new object ();
            form.DataContext = source;

            // Inheritance is the reason this property exists, and it is why the window's DataContext
            // forwards to the root adapter: a child with none of its own reads its parent's, and that
            // chain terminates there.
            Assert.Same (source, form.DataContext);
            Assert.Same (source, panel.DataContext);
            Assert.Same (source, nested.DataContext);
        }

        [Fact]
        public void DataContextChanged_reaches_a_Form_handler ()
        {
            HeadlessRenderer.Use ();

            using var form = new Form ();
            var changed = 0;
            form.DataContextChanged += (_, _) => changed++;

            form.DataContext = new object ();

            Assert.Equal (1, changed);
        }

        [Fact]
        public void The_IBindableComponent_binding_context_agrees_with_the_public_one ()
        {
            HeadlessRenderer.Use ();

            using var form = new Form ();

            // Form declares its own public, non-nullable BindingContext; the interface member routes to it
            // rather than to a second field that would drift apart from it.
            Assert.Same (form.BindingContext, ((IBindableComponent)form).BindingContext);

            var replacement = new BindingContext ();
            ((IBindableComponent)form).BindingContext = replacement;

            Assert.Same (replacement, form.BindingContext);
        }
    }
}
