using System.Drawing;
using Xunit;

namespace Majorsilence.Forms.Tests
{
    // Upstream's DateTimePicker is always one line high: its SetBoundsCore passes PreferredHeight whatever height it
    // is given. Measured (.NET 10, Windows 11) at Microsoft Sans Serif 12pt: docked to fill a 180x48 user control,
    // given Size (180, 60), anchored top and bottom while its form grew, or set to Height 80, it stayed 26px.
    // Here it took the height it was given, so a picker docked into a taller host drew a tall field with its spin
    // buttons spread apart.
    public class DateTimePickerFixedHeightTests
    {
        private static readonly Majorsilence.Forms.Drawing.Font Font = new ("Microsoft Sans Serif", 12f);

        [Fact]
        public void A_picker_docked_into_a_taller_container_keeps_its_preferred_height ()
        {
            using var form = new Form { ClientSize = new Size (400, 300) };
            var host = new UserControl { Location = new Point (10, 10), Size = new Size (180, 48) };
            var picker = new DateTimePicker { Dock = DockStyle.Fill, Font = Font, Format = DateTimePickerFormat.Custom, CustomFormat = "yyyy", ShowUpDown = true };
            host.Controls.Add (picker);
            form.Controls.Add (host);
            form.Show ();

            Assert.Equal (picker.PreferredHeight, picker.Height);
            Assert.Equal (180, picker.Width);
            Assert.Equal (48, host.Height);
        }

        [Fact]
        public void A_taller_size_or_height_is_not_taken ()
        {
            var picker = new DateTimePicker { Font = Font, Size = new Size (180, 60) };
            Assert.Equal (picker.PreferredHeight, picker.Height);
            Assert.Equal (180, picker.Width);

            picker.Height = 80;
            Assert.Equal (picker.PreferredHeight, picker.Height);
        }

        [Fact]
        public void An_anchored_picker_keeps_its_height_as_its_form_grows ()
        {
            using var form = new Form { ClientSize = new Size (400, 200) };
            var picker = new DateTimePicker { Font = Font, Location = new Point (10, 10), Size = new Size (180, 23), Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left };
            form.Controls.Add (picker);
            form.Show ();

            form.ClientSize = new Size (400, 300);

            Assert.Equal (picker.PreferredHeight, picker.Height);
        }

        [Fact]
        public void A_new_font_gives_the_picker_its_new_preferred_height ()
        {
            var picker = new DateTimePicker { Font = Font };
            var small = picker.Height;

            picker.Font = new Majorsilence.Forms.Drawing.Font ("Microsoft Sans Serif", 20f);

            Assert.True (picker.Height > small, $"{picker.Height} should exceed {small}");
            Assert.Equal (picker.PreferredHeight, picker.Height);
        }

        [Fact]
        public void The_preferred_height_at_twelve_point_is_upstream_s ()
        {
            var picker = new DateTimePicker { Font = Font };

            Assert.Equal (26, picker.PreferredHeight);
        }
    }
}
