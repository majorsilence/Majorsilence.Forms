using System.Drawing;
using Majorsilence.Forms.Headless;
using Xunit;

namespace Majorsilence.Forms.Tests
{
    // StackPanel is a Majorsilence extension (WinForms has no stack panel), so these tests pin the
    // behaviour the type documents rather than porting upstream's. The mechanism under test is that
    // every layout pass derives each child's cross-axis size from the panel's current content width:
    // a resize can never leave a child at a width it had before (the anchor-snapshot staleness a
    // FlowLayoutPanel with Anchor = Left | Right shows).
    [Collection ("Headless")]
    public class StackPanelTests
    {
        public StackPanelTests () => HeadlessRenderer.Use ();

        private static StackPanel Column (int width, int height = 600, int rows = 3, Action<StackPanel>? configure = null)
        {
            var panel = new StackPanel { Size = new Size (width, height) };
            configure?.Invoke (panel);
            for (var i = 0; i < rows; i++)
                panel.Controls.Add (new TextBox { Height = 40, Margin = Padding.Empty });
            panel.PerformLayout ();
            return panel;
        }

        // Scroll bars are children that only count as visible once the panel is on a shown window, so the scrolling
        // tests put the panel on one, as an app does.
        private static (Form Form, StackPanel Panel) Shown (int width, int height, int rows)
        {
            var form = new Form { ClientSize = new Size (width, height) };
            var panel = new StackPanel { Dock = DockStyle.Fill, AutoScroll = true };
            for (var i = 0; i < rows; i++)
                panel.Controls.Add (new TextBox { Height = 40, Margin = Padding.Empty });
            form.Controls.Add (panel);
            form.Show ();
            panel.PerformLayout ();
            return (form, panel);
        }

        [Fact]
        public void Ctor_Default ()
        {
            using var panel = new StackPanel ();

            Assert.Equal (Orientation.Vertical, panel.Orientation);
            Assert.Equal (0, panel.Spacing);
            Assert.Equal (0, panel.MaximumContentWidth);
            Assert.Equal (StackAlignment.Center, panel.ContentAlignment);
            Assert.Equal (StackAlignment.Stretch, panel.ChildAlignment);
        }

        [Fact]
        public void Vertical_StacksChildrenAtTheFullWidth_WithMarginAndSpacing ()
        {
            using var panel = Column (300, rows: 3, configure: p => p.Spacing = 6);
            foreach (Control c in panel.Controls)
                c.Margin = new Padding (4, 2, 4, 2);
            panel.PerformLayout ();

            var first = panel.Controls[0];
            var second = panel.Controls[1];

            Assert.Equal ((4, 2, 292), (first.Left, first.Top, first.Width));
            // 2 top margin + 40 high + 2 bottom margin + 6 spacing, then the next child's own 2 top margin
            Assert.Equal (2 + 40 + 2 + 6 + 2, second.Top);
            Assert.All (panel.Controls.Cast<Control> (), c => Assert.Equal (292, c.Width));
        }

        [Fact]
        public void MaximumContentWidth_CapsTheColumn_AndCentresItWhenThePanelIsWider ()
        {
            using var wide = Column (900, configure: p => p.MaximumContentWidth = 560);
            using var narrow = Column (360, configure: p => p.MaximumContentWidth = 560);

            Assert.Equal ((170, 560), (wide.Controls[0].Left, wide.Controls[0].Width));
            Assert.Equal ((0, 360), (narrow.Controls[0].Left, narrow.Controls[0].Width));
        }

        [Theory]
        [InlineData (StackAlignment.Start, 0)]
        [InlineData (StackAlignment.Center, 170)]
        [InlineData (StackAlignment.End, 340)]
        public void ContentAlignment_PlacesTheCappedColumn (StackAlignment alignment, int expectedLeft)
        {
            using var panel = Column (900, configure: p => { p.MaximumContentWidth = 560; p.ContentAlignment = alignment; });

            Assert.Equal (expectedLeft, panel.Controls[0].Left);
        }

        [Fact]
        public void AResize_RelaysTheChildrenOutAtTheNewWidth_EveryTime ()
        {
            using var panel = Column (900, configure: p => p.MaximumContentWidth = 560);

            panel.Width = 400;
            Assert.Equal ((0, 400), (panel.Controls[0].Left, panel.Controls[0].Width));

            panel.Width = 700;
            Assert.Equal ((70, 560), (panel.Controls[0].Left, panel.Controls[0].Width));

            panel.Width = 360;
            Assert.Equal ((0, 360), (panel.Controls[0].Left, panel.Controls[0].Width));
        }

        [Fact]
        public void AHiddenChild_TakesNoRoom_AndGivesItBackWhenShown ()
        {
            using var panel = Column (300, rows: 3);
            var second = panel.Controls[1];
            var third = panel.Controls[2];
            var before = third.Top;

            second.Visible = false;
            Assert.Equal (before - 40, third.Top);

            second.Visible = true;
            Assert.Equal (before, third.Top);
        }

        [Theory]
        [InlineData (StackAlignment.Start, 0)]
        [InlineData (StackAlignment.Center, 100)]
        [InlineData (StackAlignment.End, 200)]
        public void APerChildAlignment_OverridesChildAlignment (StackAlignment alignment, int expectedLeft)
        {
            using var panel = Column (300, rows: 0);
            var button = new Button { Size = new Size (100, 30), Margin = Padding.Empty };
            panel.Controls.Add (button);

            panel.SetAlignment (button, alignment);
            panel.PerformLayout ();

            Assert.Equal (alignment, panel.GetAlignment (button));
            Assert.Equal ((expectedLeft, 100), (button.Left, button.Width));
        }

        [Fact]
        public void ChildAlignment_IsTheDefaultForChildrenWithoutTheirOwn ()
        {
            using var panel = Column (300, rows: 0, configure: p => p.ChildAlignment = StackAlignment.Start);
            var button = new Button { Size = new Size (100, 30), Margin = Padding.Empty };
            panel.Controls.Add (button);
            panel.PerformLayout ();

            Assert.Equal ((0, 100), (button.Left, button.Width));
            Assert.Equal (StackAlignment.Start, panel.GetAlignment (button));
        }

        [Fact]
        public void AWrappingLabel_GetsItsWrappedHeight_FromTheColumnWidth ()
        {
            const string text = "A fairly long caption that has to wrap onto more than one line on a phone, like New PIN (4 digits, leave blank to keep it)";
            using var phone = new StackPanel { Size = new Size (300, 600) };
            using var desk = new StackPanel { Size = new Size (900, 600) };
            var narrow = new Label { Text = text, AutoSize = true, Margin = Padding.Empty };
            var wide = new Label { Text = text, AutoSize = true, Margin = Padding.Empty };
            phone.Controls.Add (narrow);
            desk.Controls.Add (wide);
            phone.PerformLayout ();
            desk.PerformLayout ();

            Assert.Equal (300, narrow.Width);
            Assert.Equal (900, wide.Width);
            Assert.True (narrow.Height > wide.Height, $"narrow {narrow.Height} should be taller than wide {wide.Height}");
        }

        [Fact]
        public void Scrolling_IsKept_WhenLayoutRunsAgain ()
        {
            var (form, panel) = Shown (300, 200, 12);
            using var _ = form;
            panel.AutoScrollPosition = new Point (0, -150);
            var scrolledTop = panel.Controls[3].Top;
            var position = panel.AutoScrollPosition;

            panel.PerformLayout ();
            panel.PerformLayout ();

            Assert.True (position.Y < 0, "the panel did not scroll, so this proves nothing");
            Assert.Equal ((scrolledTop, position), (panel.Controls[3].Top, panel.AutoScrollPosition));
        }

        [Fact]
        public void WhenTheContentOverflows_ThereIsAVerticalScrollBar_AndNoHorizontalOne ()
        {
            var (form, panel) = Shown (300, 200, 12);
            using var _ = form;

            Assert.True (panel.VScroll);
            Assert.False (panel.HScroll);
            Assert.All (panel.Controls.Cast<Control> ().Where (c => c is TextBox), c => Assert.True (c.Right <= panel.ClientSize.Width));
        }

        [Fact]
        public void AMixedForm_OnAPhoneWidth_FillsTheViewport_AndNeverGrowsAHorizontalScrollBar ()
        {
            // Wrapping labels beside fields and a cap wider than the window: once any layout pass saw a
            // content width wider than the viewport, the next pass must not take that as the width to fill.
            using var form = new Form { ClientSize = new Size (360, 700) };
            var panel = new StackPanel { Dock = DockStyle.Fill, AutoScroll = true, MaximumContentWidth = 560, Spacing = 10, Padding = new Padding (8) };
            for (var i = 0; i < 24; i++) {
                Control c = i % 2 == 0
                    ? new Label { Text = "A fairly long caption that has to wrap onto a second line on a phone, like New PIN (4 digits)", AutoSize = true }
                    : new TextBox { Height = 48 };
                c.Margin = Padding.Empty;
                panel.Controls.Add (c);
            }

            form.Controls.Add (panel);
            form.Show ();
            HeadlessRenderer.CapturePng (form, 360, 700);

            Assert.True (panel.VScroll);
            Assert.False (panel.HScroll);
            Assert.All (panel.Controls.Cast<Control> (), c => Assert.True (c.Right <= panel.ClientSize.Width, $"{c.GetType ().Name} reaches {c.Right} in a {panel.ClientSize.Width} wide panel"));
        }

        [Fact]
        public void AnAutoSizedStack_TakesTheHeightItsChildrenNeed ()
        {
            using var form = new Form { ClientSize = new Size (400, 600) };
            var panel = new StackPanel { Width = 300, AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink };
            form.Controls.Add (panel);
            form.Show ();
            panel.Controls.Add (new TextBox { Height = 40, Margin = Padding.Empty });
            panel.PerformLayout ();
            var one = panel.Height;

            panel.Controls.Add (new TextBox { Height = 40, Margin = Padding.Empty });
            panel.PerformLayout ();

            Assert.Equal ((40, 80), (one, panel.Height));
        }

        [Fact]
        public void TheNaturalSize_IsTheWidestChild_WhenNoWidthIsProposed ()
        {
            using var panel = new StackPanel ();
            panel.Controls.Add (new Button { Size = new Size (120, 30), Margin = Padding.Empty });
            panel.Controls.Add (new Button { Size = new Size (200, 30), Margin = Padding.Empty });

            Assert.Equal (new Size (200, 60), panel.GetPreferredSize (Size.Empty));
            Assert.Equal (new Size (150, 60), panel.GetPreferredSize (new Size (150, 0)));
        }

        [Fact]
        public void Horizontal_StacksLeftToRight_AndStretchesTheHeight ()
        {
            using var panel = new StackPanel { Size = new Size (600, 80), Orientation = Orientation.Horizontal, Spacing = 5 };
            panel.Controls.Add (new Button { Width = 100, Margin = Padding.Empty });
            panel.Controls.Add (new Button { Width = 120, Margin = Padding.Empty });
            panel.PerformLayout ();

            Assert.Equal ((0, 100, 80), (panel.Controls[0].Left, panel.Controls[0].Width, panel.Controls[0].Height));
            Assert.Equal ((105, 120, 80), (panel.Controls[1].Left, panel.Controls[1].Width, panel.Controls[1].Height));
        }

        [Fact]
        public void Padding_InsetsTheColumn ()
        {
            using var panel = Column (300, rows: 1, configure: p => p.Padding = new Padding (10, 20, 10, 0));

            Assert.Equal ((10, 20, 280), (panel.Controls[0].Left, panel.Controls[0].Top, panel.Controls[0].Width));
        }

        [Fact]
        public void TheValidation_RejectsNegativeSizes_AndUndefinedEnums ()
        {
            using var panel = new StackPanel ();

            Assert.Throws<ArgumentOutOfRangeException> (() => panel.Spacing = -1);
            Assert.Throws<ArgumentOutOfRangeException> (() => panel.MaximumContentWidth = -1);
            Assert.Throws<System.ComponentModel.InvalidEnumArgumentException> (() => panel.Orientation = (Orientation)99);
            Assert.Throws<System.ComponentModel.InvalidEnumArgumentException> (() => panel.ChildAlignment = (StackAlignment)99);
        }

        [Fact]
        public void ItExtendsOnlyItsOwnChildren ()
        {
            using var panel = new StackPanel ();
            var inside = new Button ();
            var outside = new Button ();
            panel.Controls.Add (inside);

            var provider = (System.ComponentModel.IExtenderProvider)panel;

            Assert.True (provider.CanExtend (inside));
            Assert.False (provider.CanExtend (outside));
        }
    }
}
