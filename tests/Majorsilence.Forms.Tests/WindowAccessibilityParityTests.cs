using Majorsilence.Forms.Headless;
using Xunit;

namespace Majorsilence.Forms.Tests
{
    // The accessibility members a WinForms Form inherits from Control. Like Control's, these are a
    // DESCRIBED surface rather than a live one -- nothing is published to a platform accessibility API yet
    // -- so what can be checked is that they are window-owned (a screen reader addresses the window, not
    // its internal adapter) and that they hold what they are told.
    public class WindowAccessibilityParityTests
    {
        [Fact]
        public void AccessibilityObject_is_created_once_and_is_the_windows_own ()
        {
            HeadlessRenderer.Use ();

            using var form = new Form ();

            Assert.NotNull (form.AccessibilityObject);
            Assert.Same (form.AccessibilityObject, form.AccessibilityObject);
        }

        [Fact]
        public void CreateAccessibilityInstance_is_the_override_point ()
        {
            HeadlessRenderer.Use ();

            using var form = new CustomAccessibleForm ();

            Assert.IsType<CustomAccessibleObject> (form.AccessibilityObject);
        }

        [Fact]
        public void The_descriptive_properties_round_trip ()
        {
            HeadlessRenderer.Use ();

            using var form = new Form ();

            Assert.Equal (AccessibleRole.Default, form.AccessibleRole);
            Assert.True (form.IsAccessible);

            form.AccessibleRole = AccessibleRole.Dialog;
            form.AccessibleDefaultActionDescription = "close";
            form.IsAccessible = false;

            Assert.Equal (AccessibleRole.Dialog, form.AccessibleRole);
            Assert.Equal ("close", form.AccessibleDefaultActionDescription);
            Assert.False (form.IsAccessible);
        }

        [Fact]
        public void PrintPreviewDialog_keeps_its_own_default_role_without_shadowing ()
        {
            HeadlessRenderer.Use ();

            // It used to declare its own AccessibleRole purely to change the default, which shadowed the
            // inherited one. The default now comes from its constructor, so there is one property.
            using var dialog = new PrintPreviewDialog ();

            Assert.Equal (AccessibleRole.Client, dialog.AccessibleRole);
        }

        [Fact]
        public void AccessibilityNotifyClients_stays_callable ()
        {
            HeadlessRenderer.Use ();

            using var form = new Form ();

            form.AccessibilityNotifyClients (AccessibleEvents.Focus, 0);   // a no-op, as on Control
        }

        private sealed class CustomAccessibleForm : Form
        {
            protected override AccessibleObject CreateAccessibilityInstance () => new CustomAccessibleObject ();
        }

        private sealed class CustomAccessibleObject : AccessibleObject { }
    }
}
