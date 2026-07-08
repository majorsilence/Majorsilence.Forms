using Xunit;

namespace Majorsilence.Forms.Tests
{
    // WinForms parity: ControlAdded/ControlRemoved use ControlEventHandler(object, ControlEventArgs),
    // exposing e.Control -- migrated handlers (Handles panel.ControlAdded / e.Control) depend on that
    // shape. The fork previously typed these events as EventHandler<EventArgs<Control>>.
    public class ControlAddedRemovedTests
    {
        [Fact]
        public void ControlAdded_and_ControlRemoved_pass_the_control_via_ControlEventArgs ()
        {
            using var parent = new Panel ();
            var child = new Panel ();

            Control? added = null;
            Control? removed = null;
            parent.ControlAdded += (s, e) => added = e.Control;
            parent.ControlRemoved += (s, e) => removed = e.Control;

            parent.Controls.Add (child);
            Assert.Same (child, added);

            parent.Controls.Remove (child);
            Assert.Same (child, removed);
        }
    }
}
