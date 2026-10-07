using System.Drawing;
using Majorsilence.Forms.Headless;
using Xunit;

namespace Majorsilence.Forms.Tests
{
    // A scrolled panel keeps its children shifted by the scroll offset (the same as WinForms: a child's Top is
    // relative to the visible area). ScrollControlIntoView read each child's Top as an unshifted content position
    // and took the scroll offset off it again, so the further down the panel was scrolled the further above the
    // viewport a visible field looked, and focusing it scrolled the content back up -- by enough to put a field
    // under the pointer somewhere else between the press and the release of a tap.
    [Collection ("Headless")]
    public class ScrollControlIntoViewScrolledTests
    {
        public ScrollControlIntoViewScrolledTests () => HeadlessRenderer.Use ();

        private static (Form Form, Panel Panel, List<TextBox> Boxes) ScrolledForm (int rows, int scrollTo)
        {
            var form = new Form { ClientSize = new Size (360, 400) };
            var panel = new Panel { Dock = DockStyle.Fill, AutoScroll = true };
            var boxes = new List<TextBox> ();
            for (var i = 0; i < rows; i++) {
                var box = new TextBox { Location = new Point (8, 8 + i * 48), Size = new Size (300, 40) };
                boxes.Add (box);
                panel.Controls.Add (box);
            }

            form.Controls.Add (panel);
            form.Show ();
            HeadlessRenderer.CapturePng (form, 360, 400);
            panel.AutoScrollPosition = new Point (0, -scrollTo);
            HeadlessRenderer.CapturePng (form, 360, 400);
            return (form, panel, boxes);
        }

        [Fact]
        public void FocusingAFieldThatIsAlreadyFullyVisible_DoesNotMoveTheContent ()
        {
            var (form, panel, boxes) = ScrolledForm (rows: 30, scrollTo: 900);
            using var _ = form;
            var before = panel.AutoScrollPosition;
            var visible = boxes.Where (b => b.Top >= 0 && b.Bottom <= panel.ClientSize.Height).ToList ();
            Assert.True (before.Y < -800, "the panel did not scroll, so this proves nothing");

            visible[^1].Focus ();

            Assert.Equal (before, panel.AutoScrollPosition);
        }

        [Fact]
        public void FocusingAFieldBelowTheFold_ScrollsJustFarEnoughToShowIt_FromAScrolledPosition ()
        {
            var (form, panel, boxes) = ScrolledForm (rows: 30, scrollTo: 300);
            using var _ = form;
            var below = boxes.First (b => b.Top > panel.ClientSize.Height);

            below.Focus ();

            Assert.True (below.Bottom <= panel.ClientSize.Height, $"the field is still below the viewport (bottom {below.Bottom})");
            Assert.True (below.Bottom > panel.ClientSize.Height - 48, $"it scrolled further than it needed to (bottom {below.Bottom})");
        }

        [Fact]
        public void FocusingAFieldAboveTheTop_ScrollsBackJustFarEnoughToShowIt ()
        {
            var (form, panel, boxes) = ScrolledForm (rows: 30, scrollTo: 900);
            using var _ = form;
            var above = boxes.Last (b => b.Bottom < 0);

            above.Focus ();

            Assert.True (above.Top >= 0, $"the field is still above the viewport (top {above.Top})");
            Assert.True (above.Top < 48, $"it scrolled further than it needed to (top {above.Top})");
        }

        [Fact]
        public void ATapOnAComboBox_WhileTheFormIsScrolled_OpensIt ()
        {
            var (form, panel, boxes) = ScrolledForm (rows: 30, scrollTo: 500);
            using var _ = form;
            var combo = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Location = new Point (8, 8 + 30 * 48), Size = new Size (300, 40) };
            combo.Items.Add ("a");
            combo.Items.Add ("b");
            panel.Controls.Add (combo);
            HeadlessRenderer.CapturePng (form, 360, 400);
            panel.AutoScrollPosition = new Point (0, -100000);
            HeadlessRenderer.CapturePng (form, 360, 400);

            int x = combo.Width / 2, y = combo.Height / 2;
            for (Control? c = combo; c is not null; c = c.Parent) {
                x += c.Left;
                y += c.Top;
            }

            HeadlessRenderer.Click (form, x, y);

            Assert.True (combo.DroppedDown);
        }
    }
}
