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
