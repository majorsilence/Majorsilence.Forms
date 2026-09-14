using System;
using System.Drawing;
using Majorsilence.Forms.Headless;
using SkiaSharp;
using Xunit;

namespace Majorsilence.Forms.Tests
{
    // W5.20c's DateTimePicker half (findings SMP-39 P0, SMP-40 P0, SMP-41).
    //
    // The control derived from TextBox, so Text was free-form and never parsed back into Value:
    // `dtp.Text = "2024-01-15"` -- a common way to seed a picker from a string -- displayed the text and
    // left Value at today, so the app saved the wrong date, and a user could delete the date and type
    // anything. It painted a drop-down arrow that nothing hit-tested, with no popup anywhere, so with
    // no keyboard path either a DateTimePicker in a migrated app was a read-only display of today.
    // ShowCheckBox and Checked -- the only way WinForms expresses an OPTIONAL date -- were stored and
    // read by nothing, so code reading Checked always got true and nulls were written as today.
    [Collection ("Headless")]
    public class DateTimePickerTests
    {
        private sealed class DrivenPicker : DateTimePicker
        {
            internal void ClickAt (Point p) => OnMouseDown (new MouseEventArgs (MouseButtons.Left, 1, p.X, p.Y, Point.Empty));

            internal void Press (Keys key) => OnKeyDown (new KeyEventArgs (key));

            internal Point ButtonCentre => new Point (
                ButtonBounds.Left + ButtonBounds.Width / 2,
                ButtonBounds.Top + ButtonBounds.Height / 2);

            internal Point CheckBoxCentre => new Point (
                CheckBoxBounds.Left + CheckBoxBounds.Width / 2,
                CheckBoxBounds.Top + CheckBoxBounds.Height / 2);
        }

        private static DrivenPicker Picker (out Form form)
        {
            HeadlessRenderer.Use ();
            form = new Form { Width = 400, Height = 240 };
            var picker = new DrivenPicker { Width = 200, Height = 24, Value = new DateTime (2024, 6, 15) };
            form.Controls.Add (picker);
            form.Show ();

            return picker;
        }

        // ---------------- SMP-39 (P0): a picker is not a text box

        [Fact]
        public void A_DateTimePicker_is_not_a_TextBox ()
        {
            // The finding's own test. It polluted the surface with Multiline, PasswordChar and
            // AcceptsReturn, and `foreach (Control c in ...) if (c is TextBox t)` sweeps -- a common way
            // to clear or validate a form's text boxes -- picked up every date picker.
            using var picker = new DateTimePicker ();

            Assert.IsNotAssignableFrom<TextBox> (picker);
        }

        [Fact]
        public void Setting_Text_parses_it_into_Value ()
        {
            // The finding's own test: it displayed the text and left Value at today, so the app saved
            // the wrong date.
            var picker = Picker (out var form);
            using var _form = form;

            try {
                picker.Text = "2024-01-15";

                Assert.Equal (new DateTime (2024, 1, 15), picker.Value.Date);
            } finally {
                form.Close ();
            }
        }

        [Fact]
        public void Text_that_is_not_a_date_is_refused ()
        {
            // As a TextBox the user could delete the date and type "asdf", with nothing validating and
            // Value still reading today.
            var picker = Picker (out var form);
            using var _form = form;

            try {
                picker.Text = "asdf";

                Assert.Equal (new DateTime (2024, 6, 15), picker.Value.Date);
                Assert.Contains ("2024", picker.Text);
            } finally {
                form.Close ();
            }
        }

        [Fact]
        public void A_date_outside_the_range_is_refused ()
        {
            var picker = Picker (out var form);
            using var _form = form;

            try {
                picker.MinDate = new DateTime (2024, 1, 1);
                picker.MaxDate = new DateTime (2024, 12, 31);

                picker.Text = "2023-05-01";

                Assert.Equal (new DateTime (2024, 6, 15), picker.Value.Date);
            } finally {
                form.Close ();
            }
        }

        [Fact]
        public void Setting_Value_still_updates_the_displayed_text ()
        {
            // GUARD, not proof: UpdateText always wrote the formatted date. It pins that overriding Text
            // to parse did not break the direction that already worked.
            var picker = Picker (out var form);
            using var _form = form;

            try {
                picker.Format = DateTimePickerFormat.Short;
                picker.Value = new DateTime (2024, 3, 9);

                Assert.Equal (new DateTime (2024, 3, 9).ToString ("d"), picker.Text);
            } finally {
                form.Close ();
            }
        }

        // ---------------- SMP-40 (P0): the drop-down works and announces itself

        [Fact]
        public void Clicking_the_button_opens_the_drop_down_and_raises_DropDown ()
        {
            // The arrow was painted and dead: nothing hit-tested it and there was no popup anywhere.
            var picker = Picker (out var form);
            using var _form = form;

            try {
                var opened = 0;
                picker.DropDown += (_, _) => opened++;

                picker.ClickAt (picker.ButtonCentre);

                Assert.True (picker.DroppedDown);
                Assert.Equal (1, opened);
            } finally {
                form.Close ();
            }
        }

        [Fact]
        public void Clicking_the_button_again_closes_it_and_raises_CloseUp ()
        {
            var picker = Picker (out var form);
            using var _form = form;

            try {
                var closed = 0;
                picker.CloseUp += (_, _) => closed++;
                picker.ClickAt (picker.ButtonCentre);

                picker.ClickAt (picker.ButtonCentre);

                Assert.False (picker.DroppedDown);
                Assert.Equal (1, closed);
            } finally {
                form.Close ();
            }
        }

        [Fact]
        public void F4_opens_the_drop_down_from_the_keyboard ()
        {
            var picker = Picker (out var form);
            using var _form = form;

            try {
                picker.Press (Keys.F4);
                Assert.True (picker.DroppedDown);

                picker.Press (Keys.Escape);
                Assert.False (picker.DroppedDown);
            } finally {
                form.Close ();
            }
        }

        [Fact]
        public void Clicking_the_text_does_not_open_the_drop_down ()
        {
            // GUARD, not proof: nothing opened it before. It pins that the hit-test is the BUTTON and
            // not the whole control -- a picker that dropped down on any click would be unusable.
            var picker = Picker (out var form);
            using var _form = form;

            try {
                picker.ClickAt (new Point (picker.TextBounds.Left + 2, picker.TextBounds.Top + 2));

                Assert.False (picker.DroppedDown);
            } finally {
                form.Close ();
            }
        }

        [Fact]
        public void Changing_the_format_raises_FormatChanged ()
        {
            // The setter raised nothing, so a handler watching for it never ran.
            var picker = Picker (out var form);
            using var _form = form;

            try {
                var raised = 0;
                picker.FormatChanged += (_, _) => raised++;

                picker.Format = DateTimePickerFormat.Short;
                picker.Format = DateTimePickerFormat.Short;   // no change, no event

                Assert.Equal (1, raised);

                picker.Format = DateTimePickerFormat.Custom;
                picker.CustomFormat = "yyyy-MM-dd";

                Assert.Equal (3, raised);
            } finally {
                form.Close ();
            }
        }

        // ---------------- SMP-41: the optional-date pattern

        [Fact]
        public void The_check_box_toggles_Checked_and_announces_it ()
        {
            // ShowCheckBox + Checked is the only way WinForms expresses an optional date. The box never
            // drew, so the user could neither clear nor set it.
            var picker = Picker (out var form);
            using var _form = form;

            try {
                picker.ShowCheckBox = true;
                var raised = 0;
                picker.ValueChanged += (_, _) => raised++;

                picker.ClickAt (picker.CheckBoxCentre);

                Assert.False (picker.Checked);
                Assert.Equal (1, raised);

                picker.ClickAt (picker.CheckBoxCentre);

                Assert.True (picker.Checked);
            } finally {
                form.Close ();
            }
        }

        [Fact]
        public void Space_toggles_the_check_box_from_the_keyboard ()
        {
            var picker = Picker (out var form);
            using var _form = form;

            try {
                picker.ShowCheckBox = true;

                picker.Press (Keys.Space);

                Assert.False (picker.Checked);
            } finally {
                form.Close ();
            }
        }

        [Fact]
        public void Space_does_nothing_when_there_is_no_check_box ()
        {
            // GUARD, not proof: nothing handled Space before. It pins that the key is bound to the
            // check box rather than to the control.
            var picker = Picker (out var form);
            using var _form = form;

            try {
                picker.Press (Keys.Space);

                Assert.True (picker.Checked);
            } finally {
                form.Close ();
            }
        }

        [Fact]
        public void An_unchecked_picker_greys_its_date ()
        {
            // The visible half of the optional-date pattern: "no value" has to look like no value.
            var picker = Picker (out var form);
            using var _form = form;

            try {
                picker.ShowCheckBox = true;

                var checked_ink = TextInk (picker);
                picker.Checked = false;
                var unchecked_ink = TextInk (picker);

                Assert.NotEqual (checked_ink, unchecked_ink);
            } finally {
                form.Close ();
            }
        }

        [Fact]
        public void The_check_box_is_painted_when_ShowCheckBox_is_on ()
        {
            var picker = Picker (out var form);
            using var _form = form;

            try {
                var without = InkIn (picker, () => new Rectangle (0, 0, picker.ButtonBounds.Left, picker.ScaledSize.Height));
                picker.ShowCheckBox = true;
                var with = InkIn (picker, () => picker.CheckBoxBounds);

                Assert.True (with > 0, "the check box should be painted");
                Assert.True (with != without, "the left of the control should differ once a check box is there");
            } finally {
                form.Close ();
            }
        }

        // ---------------- SMP-41: ShowUpDown

        [Fact]
        public void ShowUpDown_steps_the_date_instead_of_dropping_down ()
        {
            // It was stored, and the drop-down arrow was drawn regardless.
            var picker = Picker (out var form);
            using var _form = form;

            try {
                picker.ShowUpDown = true;
                var top_of_strip = new Point (picker.ButtonCentre.X, picker.ButtonBounds.Top + 2);

                picker.ClickAt (top_of_strip);

                Assert.Equal (new DateTime (2024, 6, 16), picker.Value.Date);
                Assert.False (picker.DroppedDown);
            } finally {
                form.Close ();
            }
        }

        [Fact]
        public void ShowUpDown_has_no_drop_down_to_open ()
        {
            var picker = Picker (out var form);
            using var _form = form;

            try {
                picker.ShowUpDown = true;

                picker.Press (Keys.F4);

                Assert.False (picker.DroppedDown);
            } finally {
                form.Close ();
            }
        }

        // ---------------- helpers

        // The set of pixels drawn in the control's text area, as a string, so two renders can be
        // compared for "the text looks different" without asserting a colour.
        private static string TextInk (DrivenPicker picker)
        {
            using var bitmap = PaintSurface.RenderOnForm (picker, 1f);
            var area = picker.TextBounds;
            var sb = new System.Text.StringBuilder ();

            for (var y = Math.Max (0, area.Top); y < Math.Min (bitmap.Height, area.Bottom); y++)
                for (var x = Math.Max (0, area.Left); x < Math.Min (bitmap.Width, area.Right); x++)
                    sb.Append (bitmap.GetPixel (x, y).ToString ());

            return sb.ToString ();
        }

        // Pixels in a region that differ from the region's most common colour.
        private static int InkIn (DrivenPicker picker, Func<Rectangle> region)
        {
            using var bitmap = PaintSurface.RenderOnForm (picker, 1f);
            var area = region ();
            var counts = new System.Collections.Generic.Dictionary<SKColor, int> ();

            for (var x = Math.Max (0, area.Left); x < Math.Min (bitmap.Width, area.Right); x++)
                for (var y = Math.Max (0, area.Top); y < Math.Min (bitmap.Height, area.Bottom); y++) {
                    var p = bitmap.GetPixel (x, y);
                    counts[p] = counts.TryGetValue (p, out var n) ? n + 1 : 1;
                }

            if (counts.Count == 0)
                return 0;

            var background = SKColors.Transparent;
            var best = -1;

            foreach (var entry in counts)
                if (entry.Value > best) { best = entry.Value; background = entry.Key; }

            var ink = 0;

            foreach (var entry in counts)
                if (entry.Key != background)
                    ink += entry.Value;

            return ink;
        }
    }
}
