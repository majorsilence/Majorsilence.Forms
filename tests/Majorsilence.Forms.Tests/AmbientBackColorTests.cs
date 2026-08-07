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

        // Regression: TrackBar pinned Theme.BackgroundColor in its own DefaultStyle, which short
        // circuits the ambient lookup above. The surface around the groove is most of the control, so
        // a track bar on a black media-player panel painted a wide light bar straight across it.
        [Fact]
        public void TrackBar_inherits_parent_BackColor_when_unset ()
        {
            using var panel = new Panel { BackColor = System.Drawing.Color.Black };
            var trackBar = new TrackBar ();
            panel.Controls.Add (trackBar);

            Assert.Equal (panel.BackColor.ToArgb (), trackBar.BackColor.ToArgb ());
        }

        [Fact]
        public void TrackBar_explicit_BackColor_still_wins ()
        {
            using var panel = new Panel { BackColor = System.Drawing.Color.Black };
            var trackBar = new TrackBar { BackColor = System.Drawing.Color.DarkRed };
            panel.Controls.Add (trackBar);

            Assert.Equal (System.Drawing.Color.DarkRed.ToArgb (), trackBar.BackColor.ToArgb ());
        }

        // The other half of the rule: an input surface is NOT ambient in WinForms -- its BackColor is
        // SystemColors.Window -- so these must keep pinning their own colour and stay light on a dark
        // container, which is why the fix above is per-control rather than a blanket change.
        [Fact]
        public void Input_surfaces_do_not_inherit_the_container_BackColor ()
        {
            using var panel = new Panel { BackColor = System.Drawing.Color.Black };
            var textBox = new TextBox ();
            var comboBox = new ComboBox ();
            panel.Controls.Add (textBox);
            panel.Controls.Add (comboBox);

            Assert.NotEqual (panel.BackColor.ToArgb (), textBox.BackColor.ToArgb ());
            Assert.NotEqual (panel.BackColor.ToArgb (), comboBox.BackColor.ToArgb ());
        }
    }
}
