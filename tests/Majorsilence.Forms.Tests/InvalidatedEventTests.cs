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

        [Fact]
        public void BecomingVisible_invalidates_so_the_revealed_surface_repaints ()
        {
            // Regression: found running ReportDesigner. Switching to a tab showed nothing until an
            // unrelated event forced a paint -- SetVisibleCore laid the newly-revealed subtree out
            // but never invalidated it. WinForms shows the window handle, which paints it; there is
            // no handle here, so it has to invalidate explicitly.
            using var parent = new Panel { Size = new Size (200, 200) };
            var child = new Panel { Size = new Size (100, 100), Visible = false };
            parent.Controls.Add (child);
            parent.CreateControl ();

            var invalidations = 0;
            child.Invalidated += (_, _) => invalidations++;

            child.Visible = true;

            Assert.True (invalidations > 0, "a control made visible must invalidate to repaint");
        }
    }
}
