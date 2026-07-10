using Xunit;

namespace Majorsilence.Forms.Tests
{
    // WinForms parity: BackColor is an AMBIENT property. A control whose own style chain never sets
    // a background (Label, CheckBox, Panel child...) takes its parent control's effective background
    // -- a white-on-dark panel keeps its dark background behind child label text. Without this, such
    // controls fell back to the theme color and painted opaque light boxes over dark panels.
    public class AmbientBackColorTests
    {
        [Fact]
        public void Label_inherits_parent_BackColor_when_unset ()
        {
            using var panel = new Panel { BackColor = System.Drawing.Color.FromArgb (23, 54, 96) };
            var label = new Label ();
            panel.Controls.Add (label);

            Assert.Equal (panel.BackColor.ToArgb (), label.BackColor.ToArgb ());
        }

        [Fact]
        public void Explicit_BackColor_still_wins ()
        {
            using var panel = new Panel { BackColor = System.Drawing.Color.FromArgb (23, 54, 96) };
            var label = new Label { BackColor = System.Drawing.Color.White };
            panel.Controls.Add (label);

            Assert.Equal (System.Drawing.Color.White.ToArgb (), label.BackColor.ToArgb ());
        }

        [Fact]
        public void Ambient_resolution_walks_nested_parents ()
        {
            using var outer = new Panel { BackColor = System.Drawing.Color.FromArgb (10, 20, 30) };
            var inner = new Panel ();
            var label = new Label ();
            outer.Controls.Add (inner);
            inner.Controls.Add (label);

            Assert.Equal (outer.BackColor.ToArgb (), label.BackColor.ToArgb ());
        }
    }
}
