using System;
using System.Drawing;
using System.Linq;
using Majorsilence.Forms.Theming.WinForms;
using Xunit;
using MF = Majorsilence.Forms;
using WF = System.Windows.Forms;

namespace Majorsilence.Forms.Theming.WinForms.Tests
{
    // Parity with the Majorsilence.Forms engine (docs/theming-parity-plan.md): each test applies a sheet to real
    // controls and checks a relationship (this pixel is that colour; this state differs from rest), not a threshold.
    public class WinFormsParityTests : IDisposable
    {
        private readonly WF.Form form = new () { ClientSize = new Size (400, 300) };

        public void Dispose ()
        {
            GC.SuppressFinalize (this);
            form.Dispose ();
            MF.Theme.SetBuiltInTheme (MF.BuiltInTheme.Light);
        }

        internal static Color Rgb (string hex)
            => Color.FromArgb (255, Convert.ToInt32 (hex.Substring (1, 2), 16), Convert.ToInt32 (hex.Substring (3, 2), 16), Convert.ToInt32 (hex.Substring (5, 2), 16));

        internal static void AssertColor (string expectedHex, Color actual)
            => Assert.Equal (Rgb (expectedHex).ToArgb (), actual.ToArgb ());

        private T Add<T> (T control) where T : WF.Control
        {
            form.Controls.Add (control);
            return control;
        }

        private void Apply (string css)
        {
            WinFormsCssTheme.Track (form);
            WinFormsCssTheme.Apply (css);
        }

        private static Bitmap Draw (WF.Control control)
        {
            _ = control.Handle;   // some controls adjust their size when the handle is created (ListBox.IntegralHeight)
            var bitmap = new Bitmap (control.Width, control.Height);
            control.DrawToBitmap (bitmap, new Rectangle (0, 0, control.Width, control.Height));
            return bitmap;
        }

        // ---- phase 1: selectors that had a seam but were reported as having none ---------------------

        [Fact]
        public void ProgressBarRule_RecoloursTheTrack ()
        {
            var bar = Add (new WF.ProgressBar { Size = new Size (200, 24), Location = new Point (10, 10), Minimum = 0, Maximum = 100, Value = 0 });
            Apply ("ProgressBar { background-color: #112233; }");

            using var bitmap = Draw (bar);

            AssertColor ("#112233", bitmap.GetPixel (bar.Width / 2, bar.Height / 2));
            Assert.DoesNotContain (WinFormsCssTheme.Diagnostics, d => d.Message.Contains ("'ProgressBar' has no", StringComparison.Ordinal));
        }

        [Fact]
        public void DateTimePickerRule_WinsOverTheTextBoxRule_AndThemesTheDropDown ()
        {
            var picker = Add (new WF.DateTimePicker ());
            var text = Add (new WF.TextBox ());
            Apply ("TextBox { background-color: #111111; color: #dddddd; } DateTimePicker { background-color: #222222; color: #eeeeee; }");

            AssertColor ("#222222", picker.BackColor);
            AssertColor ("#eeeeee", picker.ForeColor);
            AssertColor ("#222222", picker.CalendarMonthBackground);
            AssertColor ("#eeeeee", picker.CalendarForeColor);
            AssertColor ("#111111", text.BackColor);
        }

        [Fact]
        public void DateTimePickerWithoutItsOwnRule_StillFollowsTheTextBoxRule ()
        {
            var picker = Add (new WF.DateTimePicker ());
            Apply ("TextBox { background-color: #111111; }");

            AssertColor ("#111111", picker.BackColor);
            AssertColor ("#111111", picker.CalendarMonthBackground);
        }

        [Fact]
        public void StatusStripRule_WinsOverTheStatusBarRule ()
        {
            var status = Add (new WF.StatusStrip ());
            Apply ("StatusBar { background-color: #111111; } StatusStrip { background-color: #222222; }");

            var renderer = Assert.IsAssignableFrom<WF.ToolStripProfessionalRenderer> (WF.ToolStripManager.Renderer);
            AssertColor ("#222222", status.BackColor);
            AssertColor ("#222222", renderer.ColorTable.StatusStripGradientEnd);
        }

        [Fact]
        public void StatusStripWithOnlyAStatusBarRule_IsStillThemed ()
        {
            var status = Add (new WF.StatusStrip ());
            Apply ("StatusBar { background-color: #111111; }");

            AssertColor ("#111111", status.BackColor);
        }

        [Fact]
        public void StatusStripBorderTop_PaintsTheSeamAlongTheTopEdge ()
        {
            var status = Add (new WF.StatusStrip { Dock = WF.DockStyle.None, Size = new Size (200, 24), Location = new Point (0, 100) });
            status.Items.Add (new WF.ToolStripStatusLabel ("ready"));
            Apply ("StatusStrip { background-color: #222222; border-top-width: 2px; border-top-color: #ff0000; }");

            using var bitmap = Draw (status);

            AssertColor ("#ff0000", bitmap.GetPixel (bitmap.Width - 20, 0));
            AssertColor ("#ff0000", bitmap.GetPixel (bitmap.Width - 20, 1));
            AssertColor ("#222222", bitmap.GetPixel (bitmap.Width - 20, 6));
        }

        // ---- phase 3.2: border colours -----------------------------------------------------------------------

        private const string Red = "#ff0000";

        // The 1px frame of a control is the outermost ring of its bitmap.
        private static void AssertRing (string hex, Bitmap bitmap)
        {
            AssertColor (hex, bitmap.GetPixel (0, bitmap.Height / 2));
            AssertColor (hex, bitmap.GetPixel (bitmap.Width - 1, bitmap.Height / 2));
            AssertColor (hex, bitmap.GetPixel (bitmap.Width / 2, 0));
            AssertColor (hex, bitmap.GetPixel (bitmap.Width / 2, bitmap.Height - 1));
        }

        [Fact]
        public void TextBox_BorderColor_RepaintsTheFrame ()
        {
            var box = Add (new WF.TextBox { Size = new Size (100, 24), Location = new Point (10, 10), BorderStyle = WF.BorderStyle.Fixed3D });
            Apply ("TextBox { border-color: " + Red + "; }");

            using var bitmap = Draw (box);

            AssertRing (Red, bitmap);
            Assert.Equal (WF.BorderStyle.FixedSingle, box.BorderStyle);
            Assert.NotEqual (Rgb (Red).ToArgb (), bitmap.GetPixel (bitmap.Width / 2, bitmap.Height / 2).ToArgb ());
        }

        [Fact]
        public void TextBox_WithoutARule_TakesTheBorderLowToken ()
        {
            var box = Add (new WF.TextBox { Size = new Size (100, 24), Location = new Point (10, 10) });
            Apply (":root { --border-low-color: #3a3a40; }");

            using var bitmap = Draw (box);

            AssertRing ("#3a3a40", bitmap);
        }

        [Fact]
        public void BorderNone_KeepsTheControlBorderless ()
        {
            var box = Add (new WF.TextBox { Size = new Size (100, 24), Location = new Point (10, 10) });
            Apply ("TextBox { border: none; border-color: " + Red + "; }");

            using var bitmap = Draw (box);

            Assert.Equal (WF.BorderStyle.None, box.BorderStyle);
            Assert.NotEqual (Rgb (Red).ToArgb (), bitmap.GetPixel (0, bitmap.Height / 2).ToArgb ());
        }

        [Theory]
        [InlineData ("ListBox")]
        [InlineData ("ListView")]
        [InlineData ("TreeView")]
        public void ListsAndTrees_BorderColor_RepaintsTheFrame (string kind)
        {
            WF.Control control = kind switch {
                "ListBox" => new WF.ListBox { BorderStyle = WF.BorderStyle.Fixed3D },
                "ListView" => new WF.ListView { BorderStyle = WF.BorderStyle.Fixed3D },
                _ => new WF.TreeView { BorderStyle = WF.BorderStyle.Fixed3D },
            };
            control.Size = new Size (120, 80);
            control.Location = new Point (10, 10);
            Add (control);
            Apply (kind + " { border-color: " + Red + "; }");

            using var bitmap = Draw (control);

            AssertRing (Red, bitmap);
        }

        [Fact]
        public void Panel_BorderColor_NeedsABorderToPaint ()
        {
            var framed = Add (new WF.Panel { Size = new Size (80, 40), Location = new Point (10, 10), BorderStyle = WF.BorderStyle.FixedSingle });
            var plain = Add (new WF.Panel { Size = new Size (80, 40), Location = new Point (10, 60) });
            Apply ("Panel { border-color: " + Red + "; }");

            using var withBorder = Draw (framed);
            using var without = Draw (plain);

            AssertRing (Red, withBorder);
            Assert.Equal (WF.BorderStyle.None, plain.BorderStyle);
            Assert.NotEqual (Rgb (Red).ToArgb (), without.GetPixel (0, 20).ToArgb ());
        }

        [Fact]
        public void ComboBox_BorderColor_IsRepaintedOverTheFlatBorder ()
        {
            var combo = Add (new WF.ComboBox { Size = new Size (120, 24), Location = new Point (10, 10), DropDownStyle = WF.ComboBoxStyle.DropDownList });
            combo.Items.Add ("one");
            Apply ("ComboBox { border-color: " + Red + "; }");

            using var bitmap = Draw (combo);

            AssertColor (Red, bitmap.GetPixel (0, bitmap.Height / 2));
            AssertColor (Red, bitmap.GetPixel (bitmap.Width / 2, 0));
            AssertColor (Red, bitmap.GetPixel (bitmap.Width / 2, bitmap.Height - 1));
        }

        [Fact]
        public void NumericUpDown_BorderColor_IsRepaintedOverTheBorder ()
        {
            var numeric = Add (new WF.NumericUpDown { Size = new Size (100, 24), Location = new Point (10, 10) });
            Apply ("NumericUpDown { border-color: " + Red + "; }");

            using var bitmap = Draw (numeric);

            AssertColor (Red, bitmap.GetPixel (0, bitmap.Height / 2));
            AssertColor (Red, bitmap.GetPixel (bitmap.Width / 2, 0));
        }

        [Fact]
        public void GroupBox_BorderColor_RedrawsTheFrameAroundTheCaption ()
        {
            var group = Add (new WF.GroupBox { Text = "Group", Size = new Size (160, 80), Location = new Point (10, 10) });
            Apply ("GroupBox { border-color: " + Red + "; }");

            using var bitmap = Draw (group);

            AssertColor (Red, bitmap.GetPixel (0, bitmap.Height / 2));
            AssertColor (Red, bitmap.GetPixel (bitmap.Width - 1, bitmap.Height / 2));
            AssertColor (Red, bitmap.GetPixel (bitmap.Width / 2, bitmap.Height - 1));
        }

        [Fact]
        public void ProgressBar_BorderColor_PaintsTheFrame ()
        {
            var bar = Add (new WF.ProgressBar { Size = new Size (120, 20), Location = new Point (10, 10), Value = 50 });
            Apply ("ProgressBar { border-color: " + Red + "; }");

            using var bitmap = Draw (bar);

            AssertRing (Red, bitmap);
        }

        [Fact]
        public void PaintBordersOff_LeavesTheSystemBorders_AndSaysSo ()
        {
            var box = Add (new WF.TextBox { Size = new Size (100, 24), Location = new Point (10, 10), BorderStyle = WF.BorderStyle.Fixed3D });
            WinFormsCssTheme.PaintBorders = false;
            try {
                Apply ("TextBox { border-color: " + Red + "; }");

                using var bitmap = Draw (box);

                Assert.Equal (WF.BorderStyle.Fixed3D, box.BorderStyle);
                Assert.NotEqual (Rgb (Red).ToArgb (), bitmap.GetPixel (0, bitmap.Height / 2).ToArgb ());
                var warning = Assert.Single (WinFormsCssTheme.Diagnostics, d => d.Message.StartsWith ("'TextBox { border-color }'", StringComparison.Ordinal));
                Assert.Equal (MF.ThemeCssSeverity.Warning, warning.Severity);
                Assert.Contains ("PaintBorders is off", warning.Message, StringComparison.Ordinal);
            } finally {
                WinFormsCssTheme.PaintBorders = true;
            }
        }

        [Fact]
        public void TurningPaintBordersOff_PutsTheSystemFrameBack ()
        {
            var box = Add (new WF.TextBox { Size = new Size (100, 24), Location = new Point (10, 10), BorderStyle = WF.BorderStyle.Fixed3D });
            Apply ("TextBox { border-color: " + Red + "; }");
            Assert.Equal (WF.BorderStyle.FixedSingle, box.BorderStyle);

            WinFormsCssTheme.PaintBorders = false;
            try {
                WinFormsCssTheme.Apply ("TextBox { border-color: " + Red + "; }");

                using var bitmap = Draw (box);
                Assert.Equal (WF.BorderStyle.Fixed3D, box.BorderStyle);
                Assert.NotEqual (Rgb (Red).ToArgb (), bitmap.GetPixel (0, bitmap.Height / 2).ToArgb ());
            } finally {
                WinFormsCssTheme.PaintBorders = true;
            }
        }

        // An app's own text box that draws itself keeps its border.
        private sealed class OwnerPaintedTextBox : WF.TextBox
        {
            protected override void WndProc (ref WF.Message m) => base.WndProc (ref m);
        }

        [Fact]
        public void ControlThatPaintsItself_KeepsItsBorder_AndIsReportedOnce ()
        {
            var own = Add (new OwnerPaintedTextBox { Size = new Size (100, 24), Location = new Point (10, 10), BorderStyle = WF.BorderStyle.Fixed3D });
            var other = Add (new OwnerPaintedTextBox { Size = new Size (100, 24), Location = new Point (10, 50), BorderStyle = WF.BorderStyle.Fixed3D });
            Apply ("TextBox { border-color: " + Red + "; }");

            using var bitmap = Draw (own);

            Assert.Equal (WF.BorderStyle.Fixed3D, own.BorderStyle);
            Assert.Equal (WF.BorderStyle.Fixed3D, other.BorderStyle);
            Assert.NotEqual (Rgb (Red).ToArgb (), bitmap.GetPixel (0, bitmap.Height / 2).ToArgb ());
            Assert.Single (WinFormsCssTheme.Diagnostics, d => d.Message.Contains ("OwnerPaintedTextBox' paints itself", StringComparison.Ordinal));
        }

        [Fact]
        public void BorderPainters_AreReleasedWithTheirControls ()
        {
            static int UserObjects () => (int) GetGuiResources (System.Diagnostics.Process.GetCurrentProcess ().Handle, 1);

            WinFormsCssTheme.Track (form);
            WinFormsCssTheme.Apply ("TextBox { border-color: " + Red + "; }");

            // Warm up, then create and destroy a few hundred themed controls.
            for (var i = 0; i < 20; i++) {
                var warm = new WF.TextBox ();
                form.Controls.Add (warm);
                _ = warm.Handle;
                form.Controls.Remove (warm);
                warm.Dispose ();
            }

            GC.Collect ();
            var before = UserObjects ();

            for (var i = 0; i < 300; i++) {
                var box = new WF.TextBox ();
                form.Controls.Add (box);
                _ = box.Handle;
                form.Controls.Remove (box);
                box.Dispose ();
            }

            GC.Collect ();
            GC.WaitForPendingFinalizers ();
            var after = UserObjects ();

            Assert.True (after - before < 40, $"{after - before} USER objects leaked by 300 themed text boxes");
        }

        [System.Runtime.InteropServices.DllImport ("user32.dll")]
        private static extern uint GetGuiResources (IntPtr process, uint flags);

        // ---- phase 2: states ---------------------------------------------------------------------------------

        private static void Raise (WF.Control control, string method, EventArgs args)
            => control.GetType ().GetMethod (method, System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Public)!.Invoke (control, new object[] { args });

        private static void Press (WF.Control control) => Raise (control, "OnMouseDown", new WF.MouseEventArgs (WF.MouseButtons.Left, 1, 1, 1, 0));

        private static void Release (WF.Control control) => Raise (control, "OnMouseUp", new WF.MouseEventArgs (WF.MouseButtons.Left, 1, 1, 1, 0));

        [Fact]
        public void ButtonHover_SwapsFillTextAndBorder_AndRestoresThemOnLeave ()
        {
            var button = Add (new WF.Button { Text = "OK", Size = new Size (100, 30) });
            Apply ("Button { background-color: #111111; color: #eeeeee; border: 1px solid #333333; } Button:hover { background-color: #222222; color: #ffffff; border-color: #ff0000; }");

            AssertColor ("#111111", button.BackColor);

            Raise (button, "OnMouseEnter", EventArgs.Empty);
            AssertColor ("#222222", button.BackColor);
            AssertColor ("#ffffff", button.ForeColor);
            AssertColor ("#ff0000", button.FlatAppearance.BorderColor);

            Raise (button, "OnMouseLeave", EventArgs.Empty);
            AssertColor ("#111111", button.BackColor);
            AssertColor ("#eeeeee", button.ForeColor);
            AssertColor ("#333333", button.FlatAppearance.BorderColor);
        }

        [Fact]
        public void ButtonHover_WithoutARule_UsesTheCoresDefaults_TextOnAccentAndAccent2Border ()
        {
            var button = Add (new WF.Button { Text = "OK", Size = new Size (100, 30) });
            Apply (":root { --accent-color: #f2a93b; --accent-color-2: #c77f12; --foreground-color-on-accent: #1a1200; --foreground-color: #e8e8ea; }");

            Raise (button, "OnMouseEnter", EventArgs.Empty);

            AssertColor ("#f2a93b", button.BackColor);
            AssertColor ("#1a1200", button.ForeColor);
            AssertColor ("#c77f12", button.FlatAppearance.BorderColor);
        }

        [Fact]
        public void ButtonActive_UsesItsOwnRule_AndFallsBackToTheHoveredLookWithout_One ()
        {
            var button = Add (new WF.Button { Text = "OK", Size = new Size (100, 30) });
            Apply ("Button { background-color: #111111; } Button:hover { background-color: #222222; } Button:active { background-color: #ff0000; color: #00ff00; }");

            Raise (button, "OnMouseEnter", EventArgs.Empty);
            Press (button);
            AssertColor ("#ff0000", button.BackColor);
            AssertColor ("#00ff00", button.ForeColor);
            AssertColor ("#ff0000", button.FlatAppearance.MouseDownBackColor);

            Release (button);
            AssertColor ("#222222", button.BackColor);

            WinFormsCssTheme.Apply ("Button { background-color: #111111; } Button:hover { background-color: #222222; }");
            Press (button);
            AssertColor ("#222222", button.BackColor);
        }

        [Fact]
        public void ButtonDisabled_SwapsTheColours_AndDefaultsToTheDisabledForeground ()
        {
            var button = Add (new WF.Button { Text = "OK", Size = new Size (100, 30) });
            Apply (":root { --foreground-disabled-color: #777777; } Button { color: #eeeeee; } Button:disabled { background-color: #222222; }");

            button.Enabled = false;
            AssertColor ("#222222", button.BackColor);
            AssertColor ("#777777", button.ForeColor);

            button.Enabled = true;
            AssertColor ("#eeeeee", button.ForeColor);
        }

        [Fact]
        public void AnExplicitAppColour_StillWinsOverAState ()
        {
            var button = Add (new WF.Button { Text = "OK", Size = new Size (100, 30), BackColor = Color.Purple });
            Apply ("Button:hover { background-color: #222222; }");

            Raise (button, "OnMouseEnter", EventArgs.Empty);

            Assert.Equal (Color.Purple.ToArgb (), button.BackColor.ToArgb ());
        }

        [Fact]
        public void LinkLabelStates_UseTheirSeams ()
        {
            var link = Add (new WF.LinkLabel { Text = "docs" });
            Apply ("LinkLabel { color: #0000ff; } LinkLabel:hover { color: #00ff00; } LinkLabel:active { color: #ff0000; } LinkLabel:disabled { color: #888888; }");

            AssertColor ("#0000ff", link.LinkColor);
            AssertColor ("#ff0000", link.ActiveLinkColor);
            AssertColor ("#888888", link.DisabledLinkColor);

            Raise (link, "OnMouseEnter", EventArgs.Empty);
            AssertColor ("#00ff00", link.LinkColor);

            Raise (link, "OnMouseLeave", EventArgs.Empty);
            AssertColor ("#0000ff", link.LinkColor);
        }

        [Fact]
        public void LinkLabel_WithoutStateRules_StillGetsAPressedAndADisabledColour ()
        {
            var link = Add (new WF.LinkLabel { Text = "docs" });
            Apply (":root { --accent-color: #0000ff; --foreground-disabled-color: #999999; }");

            AssertColor ("#0000ff", link.LinkColor);
            Assert.NotEqual (link.LinkColor.ToArgb (), link.ActiveLinkColor.ToArgb ());
            AssertColor ("#999999", link.DisabledLinkColor);
        }

        [Fact]
        public void TrackBarHover_SwapsTheBackground ()
        {
            var track = Add (new WF.TrackBar ());
            Apply ("TrackBar { background-color: #111111; } TrackBar:hover { background-color: #222222; }");

            Raise (track, "OnMouseEnter", EventArgs.Empty);
            AssertColor ("#222222", track.BackColor);

            Raise (track, "OnMouseLeave", EventArgs.Empty);
            AssertColor ("#111111", track.BackColor);
        }

        [Fact]
        public void TextBoxFocus_SwapsFillTextAndTheRepaintedFrame ()
        {
            var firstStop = Add (new WF.Button { Location = new Point (10, 100), TabIndex = 0 });   // takes the initial focus when the form shows
            var text = Add (new WF.TextBox { Size = new Size (100, 24), Location = new Point (10, 10), TabIndex = 1 });
            Apply ("TextBox { background-color: #111111; color: #cccccc; border-color: #333333; } TextBox:focus { background-color: #222222; color: #ffffff; border-color: #ff0000; }");
            form.Show ();
            try {
                using (var rest = Draw (text))
                    AssertColor ("#333333", rest.GetPixel (0, rest.Height / 2));

                text.Focus ();
                Assert.True (text.Focused, "the text box could not take focus in this session");

                AssertColor ("#222222", text.BackColor);
                AssertColor ("#ffffff", text.ForeColor);
                using (var focused = Draw (text))
                    AssertColor ("#ff0000", focused.GetPixel (0, focused.Height / 2));

                firstStop.Focus ();
                AssertColor ("#111111", text.BackColor);
            } finally {
                form.Hide ();
            }
        }

        [Fact]
        public void UnsupportedPropertyInAStateWithASeam_IsReportedPerDeclaration ()
        {
            Add (new WF.Button ());
            Apply ("Button:active { background-color: #ff0000; font-size: 20px; border-radius: 4px; }");

            var warning = Assert.Single (WinFormsCssTheme.Diagnostics, d => d.Message.StartsWith ("'Button:active { font-size }'", StringComparison.Ordinal));
            Assert.Equal (MF.ThemeCssSeverity.Warning, warning.Severity);
            Assert.DoesNotContain (WinFormsCssTheme.Diagnostics, d => d.Message.StartsWith ("'Button:active { background-color }'", StringComparison.Ordinal));
        }

        // ---- phase 3: corners and dashes ---------------------------------------------------------------------

        [Fact]
        public void ButtonPerCornerRadius_ClipsEachCornerByItsOwnRadius ()
        {
            var button = Add (new WF.Button { Text = "OK", Size = new Size (100, 40) });
            Apply ("Button { border-radius: 0 14px 14px 0; }");

            var region = Assert.IsType<Region> (button.Region);
            Assert.True (region.IsVisible (0, 0), "top-left has radius 0 and stays square");
            Assert.True (region.IsVisible (0, 39), "bottom-left has radius 0 and stays square");
            Assert.False (region.IsVisible (99, 0), "top-right is rounded away");
            Assert.False (region.IsVisible (99, 39), "bottom-right is rounded away");
            Assert.True (region.IsVisible (50, 20));
        }

        [Fact]
        public void BorderRadius_ThenACornerLonghand_AndTheReverse_FollowSourceOrder ()
        {
            var first = Add (new WF.Button { Text = "A", Size = new Size (100, 40) });
            Apply ("Button { border-radius: 14px; border-top-left-radius: 0; }");
            Assert.True (Assert.IsType<Region> (first.Region).IsVisible (0, 0), "a corner written after border-radius wins");
            Assert.False (first.Region!.IsVisible (99, 0));

            WinFormsCssTheme.Apply ("Button { border-top-left-radius: 0; border-radius: 14px; }");
            Assert.False (Assert.IsType<Region> (first.Region).IsVisible (0, 0), "border-radius written after a corner resets it");
        }

        [Fact]
        public void ButtonWithNoRadius_HasNoRegion ()
        {
            var button = Add (new WF.Button { Text = "OK", Size = new Size (100, 40) });
            Apply ("Button { border-radius: 0; }");

            Assert.Null (button.Region);
        }

        [Fact]
        public void TextBox_DashedBorder_RepaintsTheFrameAsDashes ()
        {
            var box = Add (new WF.TextBox { Size = new Size (100, 24), Location = new Point (10, 10) });
            Apply ("TextBox { border: 1px dashed " + Red + "; }");

            using var bitmap = Draw (box);

            // Three pixels of dash, three of gap, along the top edge.
            AssertColor (Red, bitmap.GetPixel (1, 0));
            Assert.NotEqual (Rgb (Red).ToArgb (), bitmap.GetPixel (4, 0).ToArgb ());
            AssertColor (Red, bitmap.GetPixel (7, 0));
            Assert.Equal (box.BackColor.ToArgb (), bitmap.GetPixel (4, 0).ToArgb ());
        }

        [Fact]
        public void DashedBorder_OnAButton_IsReportedUnsupported ()
        {
            Add (new WF.Button ());
            Apply ("Button { border: 2px dashed " + Red + "; }");

            var warning = Assert.Single (WinFormsCssTheme.Diagnostics, d => d.Message.StartsWith ("'Button { border-style }'", StringComparison.Ordinal));
            Assert.Equal (MF.ThemeCssSeverity.Warning, warning.Severity);
        }

        // ---- phase 4: effects --------------------------------------------------------------------------------

        // A white panel with one button on it, drawn as a whole: the parent paints the shadow and outline, the child on top.
        private (WF.Panel Panel, WF.Button Button) PanelWithButton ()
        {
            var panel = Add (new WF.Panel { Size = new Size (220, 140), Location = new Point (10, 10), BackColor = Color.White });
            var button = new WF.Button { Text = "", Size = new Size (80, 40), Location = new Point (60, 40) };
            panel.Controls.Add (button);
            return (panel, button);
        }

        [Fact]
        public void BoxShadow_Hard_IsPaintedBehindTheButton_ByItsParent ()
        {
            var (panel, button) = PanelWithButton ();
            Apply ("Button { background-color: #222222; border: none; box-shadow: 8px 8px " + Red + "; }");

            using var bitmap = Draw (panel);

            // The shadow is the button's box moved 8px right and down: it shows along the right and bottom edges.
            AssertColor (Red, bitmap.GetPixel (button.Right + 4, button.Bottom - 5));
            AssertColor (Red, bitmap.GetPixel (button.Right - 5, button.Bottom + 4));
            // (A form that was never shown prints none of its children, so the face is checked on the button itself.)
            using (var face = Draw (button))
                AssertColor ("#222222", face.GetPixel (20, 20));
            AssertColor ("#ffffff", bitmap.GetPixel (button.Left - 6, button.Top - 6));
            AssertColor ("#ffffff", bitmap.GetPixel (button.Right + 14, button.Bottom + 14));
        }

        [Fact]
        public void BoxShadow_Blurred_FadesWithDistance ()
        {
            var (panel, button) = PanelWithButton ();
            Apply ("Button { border: none; box-shadow: 0 0 12px #000000; }");

            using var bitmap = Draw (panel);

            int Luma (Color c) => c.R + c.G + c.B;
            var near = Luma (bitmap.GetPixel (button.Right + 2, button.Top + 20));
            var far = Luma (bitmap.GetPixel (button.Right + 10, button.Top + 20));
            var gone = Luma (bitmap.GetPixel (button.Right + 30, button.Top + 20));

            Assert.True (near < far, $"{near} should be darker than {far}");
            Assert.True (far < gone, $"{far} should be darker than {gone}");
            Assert.Equal (255 * 3, gone);
        }

        [Fact]
        public void BoxShadow_TakesNoRoomFromTheControl ()
        {
            var (panel, button) = PanelWithButton ();
            Apply ("Button { box-shadow: 8px 8px 4px #000000; }");

            Assert.Equal (new Size (80, 40), button.Size);
            Assert.Equal (new Point (60, 40), button.Location);
        }

        [Fact]
        public void BoxShadow_List_PaintsTheFirstOnTop ()
        {
            var (panel, button) = PanelWithButton ();
            Apply ("Button { border: none; box-shadow: 6px 6px #0000ff, 12px 12px #ff0000; }");

            using var bitmap = Draw (panel);

            // Where both overlap (the first 6px) the blue one is on top; the red shows only beyond it.
            AssertColor ("#0000ff", bitmap.GetPixel (button.Right + 2, button.Bottom - 5));
            AssertColor (Red, bitmap.GetPixel (button.Right + 9, button.Bottom - 5));
        }

        [Fact]
        public void BoxShadow_Moves_WithTheControl ()
        {
            var (panel, button) = PanelWithButton ();
            Apply ("Button { border: none; box-shadow: 8px 8px " + Red + "; }");
            button.Location = new Point (20, 20);

            using var bitmap = Draw (panel);

            AssertColor (Red, bitmap.GetPixel (button.Right + 4, button.Bottom - 5));
            AssertColor ("#ffffff", bitmap.GetPixel (60 + 80 + 4, 40 + 40 - 5));
        }

        [Fact]
        public void InsetShadow_IsPaintedOverTheFace_OnControlsThatRaisePaint ()
        {
            var panel = Add (new WF.Panel { Size = new Size (100, 60), Location = new Point (10, 10), BackColor = Color.White });
            Apply ("Panel { box-shadow: inset 0 0 0 4px #00ff00; }");

            using var bitmap = Draw (panel);

            AssertColor ("#00ff00", bitmap.GetPixel (2, 30));
            AssertColor ("#00ff00", bitmap.GetPixel (50, 1));
            AssertColor ("#ffffff", bitmap.GetPixel (50, 30));
        }

        [Fact]
        public void InsetShadow_OnATextBox_IsReported_AndAnOuterShadowStillDraws ()
        {
            var panel = Add (new WF.Panel { Size = new Size (200, 100), Location = new Point (10, 10), BackColor = Color.White });
            var box = new WF.TextBox { Size = new Size (80, 24), Location = new Point (40, 30) };
            panel.Controls.Add (box);
            Apply ("TextBox { box-shadow: inset 0 0 4px #000000, 6px 6px " + Red + "; }");

            using var bitmap = Draw (panel);

            AssertColor (Red, bitmap.GetPixel (box.Right + 3, box.Bottom - 4));
            Assert.Contains (WinFormsCssTheme.Diagnostics, d => d.Message.Contains ("does not raise Paint", StringComparison.Ordinal));
        }

        [Fact]
        public void Outline_IsARingOutsideTheControl_AtItsOffset ()
        {
            var (panel, button) = PanelWithButton ();
            Apply ("Button { border: none; outline: 2px solid #0000ff; outline-offset: 4px; }");

            using var bitmap = Draw (panel);

            // The ring's stroke is centred 5px outside the edge (4px offset + half the 2px width): pixels 4..6px out.
            AssertColor ("#0000ff", bitmap.GetPixel (button.Left - 5, button.Top + 20));
            AssertColor ("#0000ff", bitmap.GetPixel (button.Left + 40, button.Top - 5));
            AssertColor ("#ffffff", bitmap.GetPixel (button.Left - 2, button.Top + 20));
            Assert.Equal (new Size (80, 40), button.Size);
        }

        [Fact]
        public void OutlineStyleNone_DrawsNothing_UntilARuleSaysSolid ()
        {
            var (panel, button) = PanelWithButton ();
            Apply ("Button { border: none; outline-width: 2px; outline-color: #0000ff; outline-offset: 4px; }");

            using var bitmap = Draw (panel);

            AssertColor ("#ffffff", bitmap.GetPixel (button.Left - 5, button.Top + 20));
        }

        [Fact]
        public void Outline_OnFocusVisible_AppearsWithTheState ()
        {
            var (panel, button) = PanelWithButton ();
            Apply ("Button { border: none; } Button:hover { outline: 2px solid #0000ff; outline-offset: 4px; }");

            using (var rest = Draw (panel))
                AssertColor ("#ffffff", rest.GetPixel (button.Left - 5, button.Top + 20));

            Raise (button, "OnMouseEnter", EventArgs.Empty);
            using (var hover = Draw (panel))
                AssertColor ("#0000ff", hover.GetPixel (button.Left - 5, button.Top + 20));

            Raise (button, "OnMouseLeave", EventArgs.Empty);
            using (var left = Draw (panel))
                AssertColor ("#ffffff", left.GetPixel (button.Left - 5, button.Top + 20));
        }

        [Fact]
        public void LinearGradient_BecomesTheButtonBackgroundImage ()
        {
            var button = Add (new WF.Button { Text = "", Size = new Size (120, 40), Location = new Point (10, 10) });
            Apply ("Button { border: none; background-image: linear-gradient(to right, #ff0000, #0000ff); }");

            using var bitmap = Draw (button);

            var left = bitmap.GetPixel (6, 20);
            var right = bitmap.GetPixel (bitmap.Width - 7, 20);
            Assert.True (left.R > 200 && left.B < 60, $"left end is {left}");
            Assert.True (right.B > 200 && right.R < 60, $"right end is {right}");
            Assert.NotNull (button.BackgroundImage);
        }

        [Fact]
        public void Gradient_FollowsTheControlsSize ()
        {
            var panel = Add (new WF.Panel { Size = new Size (100, 40), Location = new Point (10, 10) });
            Apply ("Panel { background-image: linear-gradient(to right, #ff0000, #0000ff); }");
            var small = panel.BackgroundImage!.Size;

            panel.Size = new Size (200, 40);
            WF.Application.DoEvents ();
            using var bitmap = Draw (panel);

            Assert.Equal (new Size (100, 40), small);
            Assert.Equal (new Size (200, 40), panel.BackgroundImage!.Size);
            Assert.True (bitmap.GetPixel (bitmap.Width - 5, 20).B > 200);
        }

        [Fact]
        public void RemovingTheGradientRule_TakesTheBackgroundImageBack ()
        {
            var panel = Add (new WF.Panel { Size = new Size (100, 40), Location = new Point (10, 10) });
            Apply ("Panel { background-image: linear-gradient(red, blue); }");
            Assert.NotNull (panel.BackgroundImage);

            WinFormsCssTheme.Apply ("Panel { background-color: #101010; }");

            Assert.Null (panel.BackgroundImage);
        }

        [Fact]
        public void AControlWithItsOwnBackgroundImage_KeepsIt_AndIsReported ()
        {
            using var own = new Bitmap (4, 4);
            var panel = Add (new WF.Panel { Size = new Size (100, 40), Location = new Point (10, 10), BackgroundImage = own });
            Apply ("Panel { background-image: linear-gradient(red, blue); }");

            Assert.Same (own, panel.BackgroundImage);
            Assert.Contains (WinFormsCssTheme.Diagnostics, d => d.Message.Contains ("BackgroundImage of its own", StringComparison.Ordinal));
        }

        [Fact]
        public void GradientOnAControlThatPaintsNoBackgroundImage_IsReported ()
        {
            Add (new WF.TextBox ());
            Apply ("TextBox { background-image: linear-gradient(red, blue); }");

            Assert.Contains (WinFormsCssTheme.Diagnostics, d => d.Message.StartsWith ("'TextBox { background-image }'", StringComparison.Ordinal));
        }

        [Fact]
        public void PaintEffectsOff_DrawsNothing_AndSaysSo ()
        {
            var (panel, button) = PanelWithButton ();
            WinFormsCssTheme.PaintEffects = false;
            try {
                Apply ("Button { border: none; box-shadow: 8px 8px " + Red + "; outline: 2px solid blue; background-image: linear-gradient(red, blue); }");

                using var bitmap = Draw (panel);

                AssertColor ("#ffffff", bitmap.GetPixel (button.Right + 4, button.Bottom - 5));
                Assert.Null (button.BackgroundImage);
                var warning = Assert.Single (WinFormsCssTheme.Diagnostics, d => d.Message.StartsWith ("'Button { box-shadow }'", StringComparison.Ordinal));
                Assert.Equal (MF.ThemeCssSeverity.Warning, warning.Severity);
                Assert.Contains ("PaintEffects is off", warning.Message, StringComparison.Ordinal);
            } finally {
                WinFormsCssTheme.PaintEffects = true;
            }
        }

        [Fact]
        public void TurningPaintEffectsOff_RemovesWhatWasPainted ()
        {
            var (panel, button) = PanelWithButton ();
            Apply ("Button { border: none; box-shadow: 8px 8px " + Red + "; background-image: linear-gradient(red, blue); }");
            Assert.NotNull (button.BackgroundImage);

            WinFormsCssTheme.PaintEffects = false;
            try {
                WinFormsCssTheme.Apply ("Button { border: none; box-shadow: 8px 8px " + Red + "; background-image: linear-gradient(red, blue); }");

                using var bitmap = Draw (panel);
                AssertColor ("#ffffff", bitmap.GetPixel (button.Right + 4, button.Bottom - 5));
                Assert.Null (button.BackgroundImage);
            } finally {
                WinFormsCssTheme.PaintEffects = true;
            }
        }

        [Fact]
        public void ShadowOnAForm_IsReported_NotDrawn ()
        {
            Apply ("Form { box-shadow: 4px 4px red; }");

            Assert.Contains (WinFormsCssTheme.Diagnostics, d => d.Message.StartsWith ("'Form { box-shadow }'", StringComparison.Ordinal));
        }

        private sealed class OwnerPaintedPanel : WF.Panel
        {
            protected override void OnPaint (WF.PaintEventArgs e) => base.OnPaint (e);
        }

        [Fact]
        public void AParentThatOverridesOnPaint_IsLeftAlone_AndReported ()
        {
            var panel = Add (new OwnerPaintedPanel { Size = new Size (220, 140), Location = new Point (10, 10), BackColor = Color.White });
            var button = new WF.Button { Text = "", Size = new Size (80, 40), Location = new Point (60, 40) };
            panel.Controls.Add (button);
            Apply ("Button { border: none; box-shadow: 8px 8px " + Red + "; }");

            using var bitmap = Draw (panel);

            AssertColor ("#ffffff", bitmap.GetPixel (button.Right + 4, button.Bottom - 5));
            Assert.Contains (WinFormsCssTheme.Diagnostics, d => d.Message.Contains ("OwnerPaintedPanel' overrides OnPaint", StringComparison.Ordinal));
        }

        [Fact]
        public void EffectsAreReleasedWithTheirControls ()
        {
            static int GdiObjects () => (int) GetGuiResources (System.Diagnostics.Process.GetCurrentProcess ().Handle, 0);

            var panel = Add (new WF.Panel { Size = new Size (300, 300) });
            Apply ("Button { box-shadow: 8px 8px 6px red; background-image: linear-gradient(red, blue); }");

            for (var i = 0; i < 20; i++) {
                var warm = new WF.Button { Size = new Size (60, 30) };
                panel.Controls.Add (warm);
                panel.Controls.Remove (warm);
                warm.Dispose ();
            }

            GC.Collect ();
            var before = GdiObjects ();

            for (var i = 0; i < 200; i++) {
                var button = new WF.Button { Size = new Size (60, 30) };
                panel.Controls.Add (button);
                _ = button.Handle;
                panel.Controls.Remove (button);
                button.Dispose ();
            }

            GC.Collect ();
            GC.WaitForPendingFinalizers ();
            var after = GdiObjects ();

            Assert.True (after - before < 40, $"{after - before} GDI objects leaked by 200 buttons with effects");
        }

        // ---- phase 5: motion ---------------------------------------------------------------------------------

        // Runs the message loop, and so the animation timer, for a while.
        private static void Pump (int milliseconds)
        {
            var end = Environment.TickCount64 + milliseconds;
            while (Environment.TickCount64 < end) {
                WF.Application.DoEvents ();
                System.Threading.Thread.Sleep (4);
            }
        }

        private WF.Button MovingButton (string css)
        {
            var button = Add (new WF.Button { Text = "OK", Size = new Size (100, 30) });
            _ = button.Handle;   // a control without a window does not animate
            Apply (css);
            return button;
        }

        [Fact]
        public void Transition_FadesTheHoverFill_ThroughTheMiddle_ToTheTarget ()
        {
            var button = MovingButton ("Button { background-color: #000000; border: none; transition: background-color 400ms linear; } Button:hover { background-color: #ffffff; }");

            Raise (button, "OnMouseEnter", EventArgs.Empty);

            // Right after the change nothing has moved yet; part way through the fill is between the two; in the end it is the target.
            Assert.True (button.BackColor.R < 40, $"started at {button.BackColor}");
            Pump (200);
            var middle = button.BackColor.R;
            Assert.InRange (middle, 60, 200);
            Pump (400);
            AssertColor ("#ffffff", button.BackColor);
        }

        [Fact]
        public void Transition_Leaving_ReversesFromWhatIsOnScreen ()
        {
            var button = MovingButton ("Button { background-color: #000000; border: none; transition: background-color 400ms linear; } Button:hover { background-color: #ffffff; }");

            Raise (button, "OnMouseEnter", EventArgs.Empty);
            Pump (200);
            var atLeave = button.BackColor.R;
            Raise (button, "OnMouseLeave", EventArgs.Empty);

            // It starts from where it was, not from the hover colour, and heads back.
            Assert.InRange (Math.Abs (button.BackColor.R - atLeave), 0, 40);
            Pump (700);
            AssertColor ("#000000", button.BackColor);
        }

        [Fact]
        public void StateRule_InheritsTheTransitionItLeavesUnsaid_AndMayChangeTheDuration ()
        {
            var button = MovingButton ("Button { background-color: #000000; border: none; transition: background-color 2s linear; } Button:hover { background-color: #ffffff; transition-duration: 100ms; }");

            Raise (button, "OnMouseEnter", EventArgs.Empty);
            Pump (400);

            // The hover style's own 100ms is the one that runs, though the properties and easing came from the base rule.
            AssertColor ("#ffffff", button.BackColor);
        }

        [Fact]
        public void NoTransition_ChangesAtOnce ()
        {
            var button = MovingButton ("Button { background-color: #000000; border: none; } Button:hover { background-color: #ffffff; }");

            Raise (button, "OnMouseEnter", EventArgs.Empty);

            AssertColor ("#ffffff", button.BackColor);
        }

        [Fact]
        public void TransitionOnADifferentProperty_DoesNotSlowTheFill ()
        {
            var button = MovingButton ("Button { background-color: #000000; border: none; transition: color 2s; } Button:hover { background-color: #ffffff; }");

            Raise (button, "OnMouseEnter", EventArgs.Empty);

            AssertColor ("#ffffff", button.BackColor);
        }

        [Fact]
        public void AnimateOff_ChangesAtOnce_AndSaysSo ()
        {
            WinFormsCssTheme.Animate = false;
            try {
                var button = MovingButton ("Button { background-color: #000000; border: none; transition: background-color 2s; } Button:hover { background-color: #ffffff; }");

                Raise (button, "OnMouseEnter", EventArgs.Empty);

                AssertColor ("#ffffff", button.BackColor);
                var warning = Assert.Single (WinFormsCssTheme.Diagnostics, d => d.Message.StartsWith ("'Button { transition-property }'", StringComparison.Ordinal));
                Assert.Contains ("Animate is off", warning.Message, StringComparison.Ordinal);
            } finally {
                WinFormsCssTheme.Animate = true;
            }
        }

        [Fact]
        public void Keyframes_PlayOnTheControlsColours_AndStopWhenTheAnimationIsGone ()
        {
            var button = MovingButton (@"
                @keyframes flash { from { background-color: #000000; } to { background-color: #ffffff; } }
                Button { background-color: #404040; border: none; }
                Button:hover { animation: flash 400ms linear forwards; }");

            AssertColor ("#404040", button.BackColor);

            Raise (button, "OnMouseEnter", EventArgs.Empty);
            Pump (200);
            var middle = button.BackColor.R;
            Assert.InRange (middle, 40, 220);
            Pump (400);

            // `forwards` holds the last keyframe.
            AssertColor ("#ffffff", button.BackColor);

            Raise (button, "OnMouseLeave", EventArgs.Empty);
            Pump (50);
            AssertColor ("#404040", button.BackColor);
        }

        [Fact]
        public void InfiniteAlternatingAnimation_KeepsMoving ()
        {
            var button = MovingButton (@"
                @keyframes pulse { from { background-color: #000000; } to { background-color: #ffffff; } }
                Button { background-color: #808080; border: none; animation: pulse 300ms linear infinite alternate; }");

            Pump (100);
            var first = button.BackColor.R;
            Pump (150);
            var second = button.BackColor.R;

            Assert.NotEqual (first, second);
        }

        [Fact]
        public void AnimationNames_AreKept_AcrossAStateChange_ButANewNameRestarts ()
        {
            var button = MovingButton (@"
                @keyframes a { from { background-color: #000000; } to { background-color: #000000; } }
                @keyframes b { from { background-color: #ff0000; } to { background-color: #ff0000; } }
                Button { border: none; animation: a 5s linear infinite; }
                Button:hover { animation: b 5s linear infinite; }");

            Pump (30);
            AssertColor ("#000000", button.BackColor);

            Raise (button, "OnMouseEnter", EventArgs.Empty);
            Pump (30);
            AssertColor ("#ff0000", button.BackColor);

            Raise (button, "OnMouseLeave", EventArgs.Empty);
            Pump (30);
            AssertColor ("#000000", button.BackColor);
        }

        [Fact]
        public void ControlWithoutAWindow_DoesNotAnimate ()
        {
            var button = Add (new WF.Button { Text = "OK", Size = new Size (100, 30) });
            Apply ("Button { background-color: #000000; border: none; transition: background-color 2s; } Button:hover { background-color: #ffffff; }");

            Raise (button, "OnMouseEnter", EventArgs.Empty);

            AssertColor ("#ffffff", button.BackColor);
        }

        [Fact]
        public void TextBoxFocusTransition_FadesTheRepaintedFrame ()
        {
            var firstStop = Add (new WF.Button { Location = new Point (10, 100), TabIndex = 0 });
            var text = Add (new WF.TextBox { Size = new Size (100, 24), Location = new Point (10, 10), TabIndex = 1 });
            _ = text.Handle;
            Apply ("TextBox { background-color: #101010; border-color: #000000; transition: border-color 400ms linear; } TextBox:focus { border-color: #ffffff; }");
            form.Show ();
            try {
                text.Focus ();
                Assert.True (text.Focused, "the text box could not take focus in this session");
                Pump (200);
                using (var middle = Draw (text))
                    Assert.InRange (middle.GetPixel (0, middle.Height / 2).R, 40, 220);

                Pump (500);
                using (var done = Draw (text))
                    AssertColor ("#ffffff", done.GetPixel (0, done.Height / 2));

                firstStop.Focus ();
            } finally {
                form.Hide ();
            }
        }

        [Fact]
        public void Motion_StopsTheTimerWhenNothingMoves_AndReleasesDisposedControls ()
        {
            var button = MovingButton ("Button { background-color: #000000; border: none; transition: background-color 100ms linear; } Button:hover { background-color: #ffffff; }");

            Raise (button, "OnMouseEnter", EventArgs.Empty);
            button.Dispose ();
            Pump (200);

            // Disposing mid-transition must not throw from the timer.
            Assert.True (button.IsDisposed);
        }

        // ---- the real paint messages ---------------------------------------------------------------------------
        // DrawToBitmap goes through WM_PRINT. These send the messages Windows itself sends while a window is on screen and read the
        // result back from the window's own device context, which works even where a screen capture does not.

        [System.Runtime.InteropServices.DllImport ("user32.dll")] private static extern IntPtr GetWindowDC (IntPtr hwnd);
        [System.Runtime.InteropServices.DllImport ("user32.dll")] private static extern int ReleaseDC (IntPtr hwnd, IntPtr hdc);
        [System.Runtime.InteropServices.DllImport ("user32.dll")] private static extern IntPtr SendMessage (IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam);
        [System.Runtime.InteropServices.DllImport ("gdi32.dll")] private static extern uint GetPixel (IntPtr hdc, int x, int y);

        // The colour of one pixel of a window (counted from its top-left corner, frame included), as the window itself would show it.
        private static Color WindowPixel (WF.Control control, int x, int y)
        {
            var dc = GetWindowDC (control.Handle);
            try {
                var colorRef = GetPixel (dc, x, y);
                return Color.FromArgb (255, (int) (colorRef & 0xFF), (int) ((colorRef >> 8) & 0xFF), (int) ((colorRef >> 16) & 0xFF));
            } finally {
                _ = ReleaseDC (control.Handle, dc);
            }
        }

        private static void Scribble (WF.Control control, Color color)
        {
            var dc = GetWindowDC (control.Handle);
            try {
                using var g = Graphics.FromHdc (dc);
                using var brush = new SolidBrush (color);
                g.FillRectangle (brush, 0, 0, control.Width, control.Height);
            } finally {
                _ = ReleaseDC (control.Handle, dc);
            }
        }

        [Fact]
        public void NcPaintMessage_RepaintsTheFrame_AfterTheControlPaintedItsOwn ()
        {
            form.StartPosition = WF.FormStartPosition.Manual;
            form.Location = new Point (50, 50);
            var text = Add (new WF.TextBox { Size = new Size (120, 24), Location = new Point (10, 10) });
            Apply ("TextBox { border-color: " + Red + "; }");
            form.Show ();
            try {
                WF.Application.DoEvents ();
                Scribble (text, Color.Lime);
                AssertColor ("#00ff00", WindowPixel (text, 0, 12));

                SendMessage (text.Handle, 0x85 /* WM_NCPAINT */, (IntPtr) 1, IntPtr.Zero);

                AssertColor (Red, WindowPixel (text, 0, 12));
                AssertColor (Red, WindowPixel (text, 60, 0));
                AssertColor (Red, WindowPixel (text, text.Width - 1, 12));
                AssertColor (Red, WindowPixel (text, 60, text.Height - 1));
            } finally {
                form.Hide ();
            }
        }

        [Fact]
        public void TheFrame_SurvivesTyping_AndFocusChanges ()
        {
            form.StartPosition = WF.FormStartPosition.Manual;
            form.Location = new Point (50, 50);
            var other = Add (new WF.Button { Location = new Point (10, 100), TabIndex = 0 });
            var text = Add (new WF.TextBox { Size = new Size (120, 24), Location = new Point (10, 10), TabIndex = 1 });
            Apply ("TextBox { border-color: " + Red + "; }");
            form.Show ();
            try {
                text.Focus ();
                text.Text = "hello";
                text.SelectAll ();
                WF.Application.DoEvents ();
                text.Update ();
                other.Focus ();
                WF.Application.DoEvents ();

                // A control's non-client frame is only repainted when Windows asks; ask the way it does after a focus change.
                SendMessage (text.Handle, 0x85, (IntPtr) 1, IntPtr.Zero);
                AssertColor (Red, WindowPixel (text, 0, 12));
            } finally {
                form.Hide ();
            }
        }

        [Fact]
        public void ComboBox_FrameIsRepainted_AfterANormalPaint ()
        {
            form.StartPosition = WF.FormStartPosition.Manual;
            form.Location = new Point (50, 50);
            var combo = Add (new WF.ComboBox { Size = new Size (120, 24), Location = new Point (10, 10), DropDownStyle = WF.ComboBoxStyle.DropDownList });
            combo.Items.Add ("one");
            Apply ("ComboBox { border-color: " + Red + "; }");
            form.Show ();
            try {
                WF.Application.DoEvents ();
                Scribble (combo, Color.Lime);

                combo.Invalidate ();
                combo.Update ();

                AssertColor (Red, WindowPixel (combo, 0, 12));
                AssertColor (Red, WindowPixel (combo, 60, 0));
            } finally {
                form.Hide ();
            }
        }

        [Fact]
        public void GroupBox_FrameIsRepainted_AfterANormalPaint ()
        {
            form.StartPosition = WF.FormStartPosition.Manual;
            form.Location = new Point (50, 50);
            var group = Add (new WF.GroupBox { Text = "Group", Size = new Size (160, 80), Location = new Point (10, 10) });
            Apply ("GroupBox { border-color: " + Red + "; }");
            form.Show ();
            try {
                WF.Application.DoEvents ();
                group.Invalidate ();
                group.Update ();

                AssertColor (Red, WindowPixel (group, 0, 40));
                AssertColor (Red, WindowPixel (group, 80, group.Height - 1));
            } finally {
                form.Hide ();
            }
        }

        [Fact]
        public void OuterShadowAndOutline_ArePaintedByTheParent_OnARealPaint ()
        {
            form.StartPosition = WF.FormStartPosition.Manual;
            form.Location = new Point (50, 50);
            var (panel, button) = PanelWithButton ();
            Apply ("Button { border: none; box-shadow: 8px 8px " + Red + "; } Button:hover { outline: 2px solid #0000ff; outline-offset: 4px; }");
            form.Show ();
            try {
                WF.Application.DoEvents ();
                panel.Invalidate (true);
                panel.Update ();

                AssertColor (Red, WindowPixel (panel, button.Right + 4, button.Bottom - 5));

                Raise (button, "OnMouseEnter", EventArgs.Empty);
                WF.Application.DoEvents ();
                panel.Update ();

                AssertColor ("#0000ff", WindowPixel (panel, button.Left - 5, button.Top + 20));

                Raise (button, "OnMouseLeave", EventArgs.Empty);
                WF.Application.DoEvents ();
                panel.Update ();

                AssertColor ("#ffffff", WindowPixel (panel, button.Left - 5, button.Top + 20));
            } finally {
                form.Hide ();
            }
        }
    }
}
