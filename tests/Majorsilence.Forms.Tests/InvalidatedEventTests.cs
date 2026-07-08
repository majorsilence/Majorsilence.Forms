using System.Drawing;
using Xunit;

namespace Majorsilence.Forms.Tests
{
    // WinForms parity: Control.Invalidated is InvalidateEventHandler(object, InvalidateEventArgs) exposing
    // e.InvalidRect -- migrated handlers depend on that shape. The fork previously typed it as
    // EventHandler<EventArgs<Rectangle>>.
    public class InvalidatedEventTests
    {
        [Fact]
        public void Invalidated_passes_InvalidRect_via_InvalidateEventArgs ()
        {
            using var control = new Panel ();
            control.CreateControl (); // Invalidate is a no-op before the handle is created.

            Rectangle? got = null;
            control.Invalidated += (s, e) => got = e.InvalidRect;

            var rect = new Rectangle (1, 2, 3, 4);
            control.Invalidate (rect);

            Assert.Equal (rect, got);
        }
    }
}
