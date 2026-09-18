using System.Drawing;
using Majorsilence.Forms.Headless;
using Xunit;

namespace Majorsilence.Forms.Tests
{
    // W6.2, the stored-only sweep (RC-7) -- the border slice (LST-56). Eight controls exposed a
    // BorderStyle that was stored and never read again, so a designer or an application that turned
    // a frame off (or on) changed nothing at all.
    //
    // The fix is not a new border model: ControlStyle.Border has always existed, and TextBoxBase has
    // always mapped its BorderStyle onto it. That mapping moved to Control.ApplyBorderStyle and the
    // six *Control* subclasses in the baseline now share it: ListBox, ListView, TreeView, ToolBar,
    // SplitContainer, Splitter.
    //
    // Border width is what ClientRectangle insets by, so a wired BorderStyle is observable without
    // sampling pixels -- but the paint test below samples them anyway, because an inset that no
    // renderer honours would still be a stub.
    //
    // Two entries are deliberately NOT wired, and are recorded in docs/behaviour-gap/lists.md:
    // StatusBarPanel.BorderStyle (a Component, not a Control -- no Style to write) and
    // ToolStripStatusLabel.BorderStyle/BorderSides (a ToolStripItem, same reason, and its per-side
    // border needs the strip item renderer rather than a control style).
    [Collection ("Headless")]
    public class ControlBorderStyleTests
    {
        private static T Shown<T> (T control, out Form form) where T : Control
        {
            HeadlessRenderer.Use ();

            control.Width = 200;
            control.Height = 120;

            form = new Form { Width = 300, Height = 220 };
            form.Controls.Add (control);
            form.Show ();

            return control;
        }

        // The three controls upstream gives a border by default. Their constructors apply the declared
        // default so the property, not the theme, is the source of truth -- though only ListView
        // actually changes appearance because of it: ListBox and TreeView already set
        // Border.Width = 1 in their own DefaultStyle and merely agreed with Fixed3D by accident.
        [Theory]
        [InlineData (typeof (ListBox))]
        [InlineData (typeof (ListView))]
        [InlineData (typeof (TreeView))]
        public void Controls_that_default_to_Fixed3D_inset_their_client_rectangle (System.Type type)
        {
            var control = Shown ((Control)System.Activator.CreateInstance (type)!, out var form);

            using (form) {
                Assert.Equal (BorderStyle.Fixed3D, BorderStyleOf (control));

                Assert.Equal (Framed (control), control.ClientRectangle);

                SetBorderStyle (control, BorderStyle.None);

                var bounds = control.ScaledBounds;
                Assert.Equal (new Rectangle (0, 0, bounds.Width, bounds.Height), control.ClientRectangle);
            }
        }

        // The three that default to None. Turning a border ON has to work too -- a setter that only
        // ever cleared the width would pass the test above.
        [Theory]
        [InlineData (typeof (ToolBar))]
        [InlineData (typeof (SplitContainer))]
        [InlineData (typeof (Splitter))]
        public void Controls_that_default_to_None_gain_an_inset_when_a_border_is_set (System.Type type)
        {
            var control = Shown ((Control)System.Activator.CreateInstance (type)!, out var form);

            using (form) {
                Assert.Equal (BorderStyle.None, BorderStyleOf (control));

                // Not Framed (control): a ToolBar already carries a themed 1px rule along its bottom
                // edge, so "no border style" is not the same as "no inset anywhere" for every control
                // here. What all three share is that nothing is inset at the top-left yet.
                var bare = control.ClientRectangle;
                Assert.Equal (0, bare.X);
                Assert.Equal (0, bare.Y);
                Assert.NotEqual (Framed (control), bare);

                SetBorderStyle (control, BorderStyle.FixedSingle);

                Assert.Equal (Framed (control), control.ClientRectangle);
            }
        }

        // The backend has no sunken-edge primitive, so FixedSingle and Fixed3D both draw the themed
        // 1px frame. What matters for parity is that neither of them is silently treated as None.
        //
        // This starts from None rather than from the default: ListBox and TreeView already set
        // Border.Width = 1 in their own DefaultStyle, so a control left at its Fixed3D default is
        // framed whether or not the property is wired, and asserting on that would prove nothing.
        [Fact]
        public void FixedSingle_and_Fixed3D_both_draw_a_frame ()
        {
            var box = Shown (new ListBox (), out var form);

            using (form) {
                SetBorderStyle (box, BorderStyle.None);
                var bare = box.ClientRectangle;
                Assert.Equal (0, bare.X);

                SetBorderStyle (box, BorderStyle.FixedSingle);
                var single = box.ClientRectangle;

                SetBorderStyle (box, BorderStyle.None);
                SetBorderStyle (box, BorderStyle.Fixed3D);
                var raised = box.ClientRectangle;

                Assert.Equal (Framed (box), single);
                Assert.Equal (single, raised);
                Assert.NotEqual (bare, raised);
            }
        }

        // The inset would be worth nothing if no renderer drew into it, so this one looks at the
        // pixels: with a border there is ink along the control's edge, and with None there is not.
        [Fact]
        public void A_border_puts_ink_on_the_controls_edge ()
        {
            var box = Shown (new ListBox (), out var form);

            using (form) {
                SetBorderStyle (box, BorderStyle.None);
                var bare = EdgeColours (box);

                SetBorderStyle (box, BorderStyle.FixedSingle);
                var bordered = EdgeColours (box);

                Assert.NotEqual (bare, bordered);
            }
        }

        private static string EdgeColours (Control control)
        {
            using var bitmap = PaintSurface.Render (control);

            var sb = new System.Text.StringBuilder ();

            for (var x = 0; x < bitmap.Width; x++)
                sb.Append (bitmap.GetPixel (x, 0));

            for (var y = 0; y < bitmap.Height; y++)
                sb.Append (bitmap.GetPixel (0, y));

            return sb.ToString ();
        }

        // The client rectangle a control has when all four sides carry the themed 1px frame. Border
        // widths are not scaled (see the TODO on Control.ClientRectangle), so this holds under
        // MF_HEADLESS_SCALE=2 as well -- only the bounds it is subtracted from grow.
        private static Rectangle Framed (Control control)
        {
            var bounds = control.ScaledBounds;

            return new Rectangle (1, 1, bounds.Width - 2, bounds.Height - 2);
        }

        // BorderStyle is declared separately on each of the six -- they share the mapping, not the
        // property -- so the tests reach it the same way a designer serialiser would.
        private static BorderStyle BorderStyleOf (Control control)
            => (BorderStyle)control.GetType ().GetProperty ("BorderStyle")!.GetValue (control)!;

        private static void SetBorderStyle (Control control, BorderStyle value)
            => control.GetType ().GetProperty ("BorderStyle")!.SetValue (control, value);
    }
}
