using System;
using System.Drawing;
using Majorsilence.Forms.Headless;
using Xunit;

namespace Majorsilence.Forms.Tests
{
    // AutoScaleBaseSize is the .NET 1.x designer's scaling, which WinForms still applies: a dialog
    // recorded against an older, smaller font (5x13 average character size) is scaled up by the ratio
    // to the form font's own before it is shown. It was stored and ignored, so ReportDesigner's legacy
    // dialogs opened about 30% smaller than under WinForms.
    [Collection ("Headless")]
    public class LegacyAutoScaleBaseSizeTests
    {
        private static (Form form, Button button) Dialog (Size? baseSize)
        {
            HeadlessRenderer.Use ();

            // A larger font than the 8.25pt the dialog was recorded against, as Segoe UI 9pt is under
            // WinForms on .NET. The library's own default is the classic 8.25pt, which needs no scaling.
            var form = new Form {
                FormBorderStyle = FormBorderStyle.None,
                ClientSize = new Size (456, 374),
                Font = new Majorsilence.Forms.Drawing.Font (Control.DefaultFont.Name, 11f),
            };
            var button = new Button { Location = new Point (100, 300), Size = new Size (75, 23) };
            form.Controls.Add (button);

            if (baseSize is { } size)
                form.AutoScaleBaseSize = size;

            form.Show ();
            return (form, button);
        }

        [Fact]
        public void A_dialog_recorded_at_5x13_is_scaled_up_before_it_is_shown ()
        {
            var (form, button) = Dialog (new Size (5, 13));

            using (form) {
                var x = form.ClientSize.Width / 456f;
                var y = form.ClientSize.Height / 374f;

                // The factors are not a fixed number: they come from the face the machine resolves. Each axis
                // is the form font's average character size over the recorded one, as whole pixels, plus the
                // 8% upstream adds to a ratio clearly above 1. Linux CI's face rounds an average character to
                // 8px against 7px elsewhere, which moves the width factor from 1.48 to 1.68 (a fixed range
                // failed there), so the expectation is worked out from the font and not assumed.

                // Height: the font's line height over the recorded 13.
                var expectedY = (form.Font ?? Control.DefaultFont).Height / 13f + 0.08f;
                Assert.InRange (y, expectedY - 0.02f, expectedY + 0.02f);

                // Width: a whole number of pixels over the recorded 5, plus the 8%. Which whole number is the
                // face's business; that it is one, and that the dialog clearly grew, is the behaviour.
                var pixels = (x - 0.08f) * 5f;
                Assert.InRange (Math.Abs (pixels - MathF.Round (pixels)), 0f, 0.05f);
                Assert.InRange (x, 1.2f, 2.0f);

                // The children move and grow with it.
                Assert.InRange (button.Left / 100f, x - 0.02f, x + 0.02f);
                Assert.InRange (button.Width / 75f, x - 0.03f, x + 0.03f);
            }
        }

        [Fact]
        public void A_larger_font_scales_the_dialog_more ()
        {
            // The ratio is to the form font's own size, so for any face a bigger font gives a bigger dialog.
            // This holds whatever the machine's metrics are, unlike a fixed expected factor.
            HeadlessRenderer.Use ();

            Size Scaled (float points)
            {
                var form = new Form {
                    FormBorderStyle = FormBorderStyle.None,
                    ClientSize = new Size (456, 374),
                    Font = new Majorsilence.Forms.Drawing.Font (Control.DefaultFont.Name, points),
                };
                form.AutoScaleBaseSize = new Size (5, 13);
                form.Show ();
                using (form)
                    return form.ClientSize;
            }

            var small = Scaled (9f);
            var large = Scaled (14f);

            Assert.True (large.Width > small.Width, $"{large.Width} should exceed {small.Width}");
            Assert.True (large.Height > small.Height, $"{large.Height} should exceed {small.Height}");
        }

        [Fact]
        public void Controls_sized_by_their_content_keep_that_size_along_the_fixed_axis ()
        {
            // Upstream's ControlStyles.FixedHeight: a combo box, a single-line text box and an AutoSize
            // strip are as thick as their font or items make them, so scaling leaves that dimension.
            // Scaled anyway, a legacy dialog's combo boxes came out a third taller than under WinForms.
            HeadlessRenderer.Use ();

            var form = new Form {
                FormBorderStyle = FormBorderStyle.None,
                ClientSize = new Size (456, 374),
                Font = new Majorsilence.Forms.Drawing.Font (Control.DefaultFont.Name, 11f),
            };
            var combo = new ComboBox { Location = new Point (10, 10), Size = new Size (120, 21) };
            var single = new TextBox { Location = new Point (10, 40), Size = new Size (120, 20) };
            var multi = new TextBox { Multiline = true, Location = new Point (10, 70), Size = new Size (120, 60) };
            var strip = new ToolStrip { Dock = DockStyle.Bottom, Height = 27 };
            form.Controls.AddRange (new Control[] { combo, single, multi, strip });
            form.AutoScaleBaseSize = new Size (5, 13);

            form.Show ();

            using (form) {
                Assert.True (combo.Width > 120, "the combo box's width scales");
                Assert.Equal (21, combo.Height);
                Assert.True (single.Width > 120, "the text box's width scales");
                Assert.Equal (20, single.Height);
                Assert.True (multi.Height > 60, "a multiline text box is sized by its owner, so it scales");
                Assert.Equal (27, strip.Height);
            }
        }

        [Fact]
        public void An_AutoSize_control_in_a_group_box_moves_by_the_factor_once ()
        {
            // An AutoSize control needs anchor layout, so SetBounds re-recorded its anchor distances from
            // the already-scaled bounds and ScaleAnchorInfo then scaled them again: ReportDesigner's
            // Options dialog had its check boxes and radio buttons at about 1.64x their place under a
            // 1.28x scale, hanging out of their group boxes.
            HeadlessRenderer.Use ();

            var form = new Form {
                FormBorderStyle = FormBorderStyle.None,
                ClientSize = new Size (466, 334),
                Font = new Majorsilence.Forms.Drawing.Font (Control.DefaultFont.Name, 11f),
            };
            var group = new GroupBox { Location = new Point (291, 84), Size = new Size (158, 87) };
            var auto = new RadioButton { AutoSize = true, Location = new Point (18, 55) };
            var fixedSize = new RadioButton { AutoSize = false, Location = new Point (18, 23), Size = new Size (57, 17) };
            group.Controls.Add (auto);
            group.Controls.Add (fixedSize);
            form.Controls.Add (group);
            form.AutoScaleBaseSize = new Size (5, 13);

            form.Show ();

            using (form) {
                var factorX = group.Left / 291f;
                var factorY = group.Top / 84f;

                Assert.InRange (auto.Left / 18f, factorX - 0.1f, factorX + 0.1f);
                Assert.InRange (auto.Top / 55f, factorY - 0.1f, factorY + 0.1f);
                Assert.InRange (fixedSize.Top / 23f, factorY - 0.1f, factorY + 0.1f);
                Assert.True (auto.Bottom <= group.Height, $"the radio button ends at {auto.Bottom}, outside its {group.Height}px group box");
            }
        }

        [Fact]
        public void An_AutoSize_label_is_as_big_as_its_text_after_scaling ()
        {
            // Upstream re-measures an AutoSize label after the scale; here it kept its scaled bounds,
            // so ReportDesigner's "Fore Color:" label measured 64px but sat at 82.
            HeadlessRenderer.Use ();

            var form = new Form {
                FormBorderStyle = FormBorderStyle.None,
                ClientSize = new Size (466, 334),
                Font = new Majorsilence.Forms.Drawing.Font (Control.DefaultFont.Name, 11f),
            };
            var label = new Label { AutoSize = true, Location = new Point (4, 15), Size = new Size (58, 13), Text = "Fore Color:" };
            var check = new CheckBox { AutoSize = true, Location = new Point (4, 40), Size = new Size (76, 17), Text = "Auto Hide" };
            form.Controls.Add (label);
            form.Controls.Add (check);
            form.AutoScaleBaseSize = new Size (5, 13);

            form.Show ();

            using (form) {
                Assert.Equal (label.GetPreferredSize (Size.Empty), label.Size);
                Assert.Equal (check.GetPreferredSize (Size.Empty), check.Size);
                Assert.True (label.Left > 4, "the label still moves with the scale");
            }
        }

        [Fact]
        public void Padding_and_margins_are_left_alone ()
        {
            // Upstream applies AutoScaleBaseSize through Scale (float, float) and ScaleCore, which move
            // and resize but do not touch Padding or Margin (only the AutoScaleMode path scales those).
            // Scaled here, ReportDesigner's menu bar padding went from 6,2 to 8,3.
            HeadlessRenderer.Use ();

            var form = new Form {
                FormBorderStyle = FormBorderStyle.None,
                ClientSize = new Size (456, 374),
                Font = new Majorsilence.Forms.Drawing.Font (Control.DefaultFont.Name, 11f),
            };
            var panel = new Panel { Location = new Point (10, 10), Size = new Size (200, 100), Padding = new Padding (6, 2, 0, 2) };
            var button = new Button { Location = new Point (10, 200), Size = new Size (75, 23), Margin = new Padding (3) };
            form.Controls.Add (panel);
            form.Controls.Add (button);
            form.AutoScaleBaseSize = new Size (5, 13);

            form.Show ();

            using (form) {
                Assert.True (panel.Width > 200, "the panel itself still scales");
                Assert.Equal (new Padding (6, 2, 0, 2), panel.Padding);
                Assert.Equal (new Padding (3), button.Margin);
            }
        }

        [Fact]
        public void Controls_hosted_in_a_tool_strip_scale_with_it ()
        {
            // Upstream's hosted controls are the strip's children and scale with it, along each axis their
            // size is not their content's. Here the host re-applied its unscaled size on every layout:
            // ReportDesigner's expression box stayed 250px wide where WinForms makes it 320.
            HeadlessRenderer.Use ();

            var form = new Form {
                FormBorderStyle = FormBorderStyle.None,
                ClientSize = new Size (900, 200),
                Font = new Majorsilence.Forms.Drawing.Font (Control.DefaultFont.Name, 11f),
            };
            var strip = new ToolStrip ();
            var box = new ToolStripTextBox { Size = new Size (250, 38) };
            var panel = new Panel { Size = new Size (100, 30) };
            var host = new ToolStripControlHost (panel) { Size = new Size (100, 30) };
            var auto = new Panel { AutoSize = true, Size = new Size (60, 20) };
            var auto_host = new ToolStripControlHost (auto) { Size = new Size (60, 20) };
            strip.Items.AddRange (new ToolStripItem[] { box, host, auto_host });
            form.Controls.Add (strip);
            form.AutoScaleBaseSize = new Size (5, 13);

            form.Show ();
            var factor = form.ClientSize.Width / 900f;
            HeadlessRenderer.CapturePng (form, form.ClientSize.Width, form.ClientSize.Height);   // lays the strip out

            using (form) {
                Assert.True (Math.Abs (box.TextBox.Width / 250f - factor) < 0.02f, $"text box {box.TextBox.Width}px for a {factor} scale");
                Assert.True (Math.Abs (panel.Width / 100f - factor) < 0.02f, $"panel {panel.Width}px for a {factor} scale");
                Assert.True (panel.Height > 30, "a hosted control's height scales too");
                Assert.Equal (60, auto.Width);   // an AutoSize control is as big as its content
            }
        }

        [Fact]
        public void A_tool_strip_text_box_is_as_tall_as_its_font_makes_it ()
        {
            // Upstream's hosted text box is AutoSize: a set height (the resx's 38) does not stretch it.
            HeadlessRenderer.Use ();

            using var form = new Form { FormBorderStyle = FormBorderStyle.None, ClientSize = new Size (600, 100) };
            // Tall enough for the font on any platform (it measures 42px on Windows): the strip clamps
            // its hosted control, and this test is about the text box not being stretched to 38.
            var strip = new ToolStrip { AutoSize = false, Height = 60 };
            var box = new ToolStripTextBox { Size = new Size (250, 38) };
            strip.Items.Add (box);
            form.Controls.Add (strip);
            form.Show ();
            HeadlessRenderer.CapturePng (form, 600, 100);

            Assert.Equal (box.TextBox.PreferredHeight, box.TextBox.Height);
            Assert.Equal (250, box.TextBox.Width);
        }

        [Fact]
        public void An_integral_height_list_scales_the_height_it_was_asked_for ()
        {
            // Upstream scales a list box's requested height, then snaps it to whole rows once. Scaling the
            // already-snapped height compounded the roundings: ReportDesigner's Embedded Images list came
            // out a row shorter than WinForms'.
            HeadlessRenderer.Use ();

            var form = new Form {
                FormBorderStyle = FormBorderStyle.None,
                ClientSize = new Size (466, 334),
                Font = new Majorsilence.Forms.Drawing.Font (Control.DefaultFont.Name, 11f),
            };
            // 104px: at this scale the requested height and the pre-snapped one land on different rows.
            var list = new ListBox { ItemHeight = 15, IntegralHeight = true, Location = new Point (16, 8), Size = new Size (120, 104) };
            form.Controls.Add (list);
            form.AutoScaleBaseSize = new Size (5, 13);

            form.Show ();

            using (form) {
                var factor = form.ClientSize.Height / 334f;
                var chrome = list.Height - list.ClientSize.Height;
                var asked = (int) System.Math.Round (104 * factor);
                var rows = (asked - chrome) / 15;

                Assert.Equal (rows * 15 + chrome, list.Height);
            }
        }

        [Fact]
        public void A_dialog_without_a_recorded_base_size_is_left_alone ()
        {
            var (form, button) = Dialog (null);

            using (form) {
                Assert.Equal (new Size (456, 374), form.ClientSize);
                Assert.Equal (new Point (100, 300), button.Location);
            }
        }

        [Fact]
        public void Scaling_happens_once_and_records_the_new_base_size ()
        {
            var (form, _) = Dialog (new Size (5, 13));

            using (form) {
                var scaled = form.ClientSize;

                Assert.NotEqual (new Size (5, 13), form.AutoScaleBaseSize);

                form.Hide ();
                form.Show ();

                Assert.Equal (scaled, form.ClientSize);
            }
        }
    }
}
