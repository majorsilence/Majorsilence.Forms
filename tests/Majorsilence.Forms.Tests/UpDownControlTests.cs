using System;
using System.Linq;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using Majorsilence.Forms.Headless;
using SkiaSharp;
using Xunit;

namespace Majorsilence.Forms.Tests
{
    // W5.20b (findings SMP-32 P0, SMP-33, SMP-36, SMP-37).
    //
    // NumericUpDown had no OnKeyDown, OnKeyPress, OnKeyUp or ProcessDialogKey anywhere: a user could not
    // type a number into it, which on a data-entry form with a quantity or an amount is a hard blocker.
    // InterceptArrowKeys and ReadOnly were stored and read by nothing. The renderer drew with the
    // theme's font and colour and ignored ThousandsSeparator, Hexadecimal, TextAlign and UpDownAlign, so
    // setting Font on a spin box had no effect at all. It derived from Control rather than UpDownBase,
    // so `(UpDownBase)nud` threw. And DomainUpDown derived from NumericUpDown, so it was painted by the
    // numeric renderer and displayed the literal text "0" whatever its items held.
    [Collection ("Headless")]
    public class UpDownControlTests
    {
        private sealed class TypeableSpinner : NumericUpDown
        {
            internal void Press (Keys key) => OnKeyDown (new KeyEventArgs (key));

            internal void Type (string text)
            {
                foreach (var c in text)
                    OnKeyPress (new KeyPressEventArgs (c));
            }

            internal void LoseFocus () => OnLostFocus (EventArgs.Empty);
        }

        private static TypeableSpinner Spinner (out Form form, decimal max = 1000)
        {
            HeadlessRenderer.Use ();
            form = new Form { Width = 300, Height = 200 };
            var spinner = new TypeableSpinner { Width = 120, Height = 24, Maximum = max };
            form.Controls.Add (spinner);
            form.Show ();

            return spinner;
        }

        // ---------------- SMP-36 / SMP-37: the type hierarchy

        [Fact]
        public void A_NumericUpDown_is_an_UpDownBase ()
        {
            // The finding's own test. `(UpDownBase)nud` threw, and `if (c is UpDownBase)` sweeps over a
            // form's controls -- which is how third-party themers and designers find spin boxes -- missed
            // every one.
            using var spinner = new NumericUpDown ();

            Assert.IsAssignableFrom<UpDownBase> (spinner);
        }

        [Fact]
        public void A_DomainUpDown_is_an_UpDownBase_and_not_a_NumericUpDown ()
        {
            // Deriving from NumericUpDown gave it a nonsense public surface -- Minimum, Maximum,
            // DecimalPlaces, Hexadecimal, Increment -- none of which means anything for a list.
            using var domain = new DomainUpDown ();

            Assert.IsAssignableFrom<UpDownBase> (domain);

            // Asserted reflectively: with the hierarchy right, `domain is NumericUpDown` is a
            // compile-time impossibility and will not build -- which is the point, but it needs a form
            // that compiles either way to be a test rather than a tautology.
            Assert.False (typeof (NumericUpDown).IsAssignableFrom (typeof (DomainUpDown)),
                "a DomainUpDown is not a kind of NumericUpDown");
        }

        // ---------------- SMP-32 (P0): typing

        [Fact]
        public void Typing_a_number_and_leaving_commits_it ()
        {
            // The finding's own test: type "42", lose focus, and the value is 42 -- with ValueChanged
            // raised once, not once per keystroke.
            var spinner = Spinner (out var form);
            using var _form = form;

            try {
                var raised = 0;
                spinner.ValueChanged += (_, _) => raised++;

                spinner.Type ("42");
                spinner.LoseFocus ();

                Assert.Equal (42m, spinner.Value);
                Assert.Equal (1, raised);
            } finally {
                form.Close ();
            }
        }

        [Fact]
        public void Typing_does_not_commit_until_the_control_is_left ()
        {
            // Upstream parses on validate, not per keystroke: a half-typed "4" on the way to "42" must
            // not snap the field to 4 under the user.
            var spinner = Spinner (out var form);
            using var _form = form;

            try {
                spinner.Value = 7;
                spinner.Type ("42");

                Assert.Equal (7m, spinner.Value);

                spinner.LoseFocus ();

                // 42, not 742: arriving at a spin box selects its text, so the first character typed
                // REPLACES it. This test read 742 while that was missing, which is what surfaced it.
                Assert.Equal (42m, spinner.Value);
            } finally {
                form.Close ();
            }
        }

        [Fact]
        public void Enter_commits_without_waiting_for_focus_to_leave ()
        {
            var spinner = Spinner (out var form);
            using var _form = form;

            try {
                spinner.Type ("15");
                spinner.Press (Keys.Enter);

                Assert.Equal (15m, spinner.Value);
            } finally {
                form.Close ();
            }
        }

        [Fact]
        public void Escape_abandons_what_was_typed ()
        {
            var spinner = Spinner (out var form);
            using var _form = form;

            try {
                spinner.Value = 9;
                spinner.Type ("123");
                spinner.Press (Keys.Escape);
                spinner.LoseFocus ();

                Assert.Equal (9m, spinner.Value);
            } finally {
                form.Close ();
            }
        }

        [Fact]
        public void A_typed_value_is_clamped_to_the_range ()
        {
            var spinner = Spinner (out var form, max: 50);
            using var _form = form;

            try {
                spinner.Type ("999");
                spinner.LoseFocus ();

                Assert.Equal (50m, spinner.Value);
            } finally {
                form.Close ();
            }
        }

        [Fact]
        public void Text_that_cannot_be_a_number_is_discarded ()
        {
            // Upstream's ValidateEditText drops it and returns to showing the value, rather than
            // storing something that is not a number.
            var spinner = Spinner (out var form);
            using var _form = form;

            try {
                spinner.Value = 5;
                spinner.Press (Keys.Back);
                spinner.LoseFocus ();

                Assert.Equal (5m, spinner.Value);
            } finally {
                form.Close ();
            }
        }

        [Fact]
        public void Letters_are_refused_before_they_reach_the_text ()
        {
            var spinner = Spinner (out var form);
            using var _form = form;

            try {
                spinner.Type ("1a2");
                spinner.LoseFocus ();

                Assert.Equal (12m, spinner.Value);
            } finally {
                form.Close ();
            }
        }

        [Fact]
        public void A_minus_is_only_accepted_when_a_negative_value_is_reachable ()
        {
            var spinner = Spinner (out var form);

            try {
                spinner.Type ("-5");
                spinner.LoseFocus ();
                Assert.Equal (5m, spinner.Value);      // the minus was refused; Minimum is 0
            } finally {
                form.Close ();
            }

            // A SECOND control rather than resetting the first: Escape abandons typed text, it does not
            // undo a committed value, so typing into the same control would append to what it holds.
            var negative = Spinner (out var negative_form);
            using var _negative_form = negative_form;

            try {
                negative.Minimum = -100;

                negative.Type ("-5");
                negative.LoseFocus ();

                Assert.Equal (-5m, negative.Value);
            } finally {
                form.Close ();
            }
        }

        [Fact]
        public void A_decimal_separator_is_accepted_only_when_DecimalPlaces_allows_one ()
        {
            var spinner = Spinner (out var form);
            var separator = CultureInfo.CurrentCulture.NumberFormat.NumberDecimalSeparator;

            try {

                spinner.Type ($"1{separator}5");
                spinner.LoseFocus ();
                Assert.Equal (15m, spinner.Value);     // DecimalPlaces is 0, so the separator was refused
            } finally {
                form.Close ();
            }

            // A second control, for the reason in A_minus_is_only_accepted_...
            var fractional = Spinner (out var fractional_form);
            using var _fractional_form = fractional_form;

            try {
                fractional.DecimalPlaces = 2;

                fractional.Type ($"1{separator}5");
                fractional.LoseFocus ();

                Assert.Equal (1.5m, fractional.Value);
            } finally {
                form.Close ();
            }
        }

        [Fact]
        public void The_arrows_step_the_value_when_InterceptArrowKeys_is_on ()
        {
            var spinner = Spinner (out var form);
            using var _form = form;

            try {
                spinner.Increment = 5;
                spinner.Value = 10;

                spinner.Press (Keys.Up);
                Assert.Equal (15m, spinner.Value);

                spinner.Press (Keys.Down);
                Assert.Equal (10m, spinner.Value);
            } finally {
                form.Close ();
            }
        }

        [Fact]
        public void InterceptArrowKeys_off_leaves_the_arrows_alone ()
        {
            // It was stored and read by nothing, so a form that wanted the arrows for navigation could
            // not have them.
            var spinner = Spinner (out var form);
            using var _form = form;

            try {
                spinner.Value = 10;
                spinner.InterceptArrowKeys = false;

                spinner.Press (Keys.Up);

                Assert.Equal (10m, spinner.Value);
            } finally {
                form.Close ();
            }
        }

        [Fact]
        public void ReadOnly_blocks_typing_but_not_the_arrows ()
        {
            // ReadOnly could not mean anything before, because there was no editing to block. Upstream
            // still lets the arrows move a read-only spin box -- it blocks the TEXT.
            var spinner = Spinner (out var form);
            using var _form = form;

            try {
                spinner.Value = 10;
                spinner.ReadOnly = true;

                spinner.Type ("99");
                spinner.LoseFocus ();
                Assert.Equal (10m, spinner.Value);

                spinner.Press (Keys.Up);
                Assert.Equal (11m, spinner.Value);
            } finally {
                form.Close ();
            }
        }

        // ---------------- SMP-33: the control's own appearance

        [Fact]
        public void ThousandsSeparator_groups_the_displayed_number ()
        {
            var spinner = Spinner (out var form, max: 10_000_000);
            using var _form = form;

            try {
                spinner.Value = 1234567;

                Assert.DoesNotContain (CultureInfo.CurrentCulture.NumberFormat.NumberGroupSeparator, spinner.DisplayText);

                spinner.ThousandsSeparator = true;

                Assert.Contains (CultureInfo.CurrentCulture.NumberFormat.NumberGroupSeparator, spinner.DisplayText);
            } finally {
                form.Close ();
            }
        }

        [Fact]
        public void Hexadecimal_shows_the_value_in_hex ()
        {
            var spinner = Spinner (out var form, max: 70000);
            using var _form = form;

            try {
                spinner.Value = 255;
                Assert.Equal ("255", spinner.DisplayText);

                spinner.Hexadecimal = true;

                Assert.Equal ("FF", spinner.DisplayText);
            } finally {
                form.Close ();
            }
        }

        [Fact]
        public void Hexadecimal_accepts_hex_digits_when_typing ()
        {
            var spinner = Spinner (out var form, max: 70000);
            using var _form = form;

            try {
                spinner.Hexadecimal = true;
                spinner.Value = 0;
                spinner.Press (Keys.Escape);

                spinner.Type ("FF");
                spinner.LoseFocus ();

                Assert.Equal (255m, spinner.Value);
            } finally {
                form.Close ();
            }
        }

        [Fact]
        public void DecimalPlaces_is_still_honoured ()
        {
            // GUARD, not proof: the renderer built "F" + DecimalPlaces before, so this half worked. It
            // pins that routing the format through Hexadecimal/ThousandsSeparator kept it.
            var spinner = Spinner (out var form);
            using var _form = form;

            try {
                spinner.DecimalPlaces = 2;
                spinner.Value = 3;

                Assert.Equal (3m.ToString ("F2", CultureInfo.CurrentCulture), spinner.DisplayText);
            } finally {
                form.Close ();
            }
        }

        [Fact]
        public void UpDownAlign_moves_the_buttons_to_the_other_side ()
        {
            // It was stored and the strip was always at Width - ButtonWidth.
            var spinner = Spinner (out var form);
            using var _form = form;

            try {
                Assert.True (spinner.GetIncrementArea ().Left > spinner.Width / 2, "the buttons default to the right");

                spinner.UpDownAlign = LeftRightAlignment.Left;

                Assert.Equal (0, spinner.GetIncrementArea ().Left);
                Assert.Equal (0, spinner.GetDecrementArea ().Left);
            } finally {
                form.Close ();
            }
        }

        [Fact]
        public void The_control_paints_with_its_own_font_not_the_themes ()
        {
            // The most visible half of SMP-33: setting Font on a NumericUpDown had NO effect, so it did
            // not scale with the rest of the form. Asserted as a relationship -- bigger font, taller
            // text -- rather than as a pixel count, which would encode the rasteriser.
            var spinner = Spinner (out var form);
            using var _form = form;

            try {
                // Tall enough that neither font fills it: at the control's default 24px height both
                // measured 22 rows, because the ink saturated the area rather than because the font was
                // ignored -- the measurement, not the behaviour, was the problem.
                spinner.Height = 80;
                spinner.Value = 8;

                spinner.Font = new Majorsilence.Forms.Drawing.Font (spinner.Font!.FontFamily, 8);
                var small = InkHeight (spinner);
                spinner.Font = new Majorsilence.Forms.Drawing.Font (spinner.Font!.FontFamily, 28);
                var large = InkHeight (spinner);

                Assert.True (large > small, $"a larger font should draw taller text: {large}px vs {small}px");
            } finally {
                form.Close ();
            }
        }

        // ---------------- SMP-37: a DomainUpDown shows its items

        [Fact]
        public void A_DomainUpDown_displays_its_selected_item ()
        {
            // The finding's own test: it displayed the literal text "0" whatever its items held, because
            // RenderManager walked up the base chain to NumericUpDownRenderer.
            HeadlessRenderer.Use ();
            using var form = new Form { Width = 300, Height = 200 };
            var domain = new DomainUpDown { Width = 140, Height = 24 };
            domain.Items.Add ("Alpha");
            domain.Items.Add ("Beta");
            form.Controls.Add (domain);
            form.Show ();

            try {
                domain.SelectedIndex = 0;

                Assert.Equal ("Alpha", domain.DisplayText);
                Assert.Equal ("Alpha", domain.Text);
            } finally {
                form.Close ();
            }
        }

        [Fact]
        public void A_DomainUpDowns_arrows_step_through_its_items ()
        {
            // They changed an invisible numeric Value between 0 and 100 instead.
            using var domain = new DomainUpDown ();
            domain.Items.Add ("Alpha");
            domain.Items.Add ("Beta");
            domain.Items.Add ("Gamma");
            domain.SelectedIndex = 0;
            var changed = 0;
            domain.SelectedItemChanged += (_, _) => changed++;

            domain.DownButton ();

            Assert.Equal (1, domain.SelectedIndex);
            Assert.Equal ("Beta", domain.SelectedItem);
            Assert.Equal (1, changed);

            domain.UpButton ();

            Assert.Equal (0, domain.SelectedIndex);
            Assert.Equal (2, changed);
        }

        [Fact]
        public void A_DomainUpDown_stops_at_the_ends_unless_Wrap_is_set ()
        {
            using var domain = new DomainUpDown ();
            domain.Items.Add ("Alpha");
            domain.Items.Add ("Beta");
            domain.SelectedIndex = 0;

            domain.UpButton ();
            Assert.Equal (0, domain.SelectedIndex);

            domain.Wrap = true;
            domain.UpButton ();

            Assert.Equal (1, domain.SelectedIndex);
        }

        [Fact]
        public void An_empty_DomainUpDown_does_not_throw ()
        {
            // GUARD, not proof: it stepped a number before, so an empty list could not be a problem. It
            // pins that moving to an index model did not introduce one.
            using var domain = new DomainUpDown ();

            domain.UpButton ();
            domain.DownButton ();

            Assert.Equal (-1, domain.SelectedIndex);
            Assert.Equal (string.Empty, domain.DisplayText);
        }

        // ---------------- helpers

        // The height of the text's ink, as a proxy for the font size actually used.
        //
        // The background is the MOST COMMON colour in the probe area, not the pixel at its corner: the
        // corner sample landed on the control's border, which made every pixel "differ from the
        // background" and both fonts measure the full height of the control.
        private static int InkHeight (NumericUpDown spinner)
        {
            // Rendered at a FIXED scale 1, not the control's own: under MF_HEADLESS_SCALE=2 the bitmap
            // and the font both double and the probe's fixed insets no longer line up with the text.
            // What this test is about is the font the renderer chose, not how the surface is scaled.
            using var bitmap = PaintSurface.RenderOnForm (spinner, 1f);

            // The text's own strip: inside the border, and left of the button strip.
            var area = new Rectangle (4, 4, Math.Max (1, bitmap.Width / 2), Math.Max (1, bitmap.Height - 8));
            var counts = new Dictionary<SKColor, int> ();

            for (var x = area.Left; x < Math.Min (bitmap.Width, area.Right); x++)
                for (var y = area.Top; y < Math.Min (bitmap.Height, area.Bottom); y++) {
                    var p = bitmap.GetPixel (x, y);
                    counts[p] = counts.TryGetValue (p, out var n) ? n + 1 : 1;
                }

            var background = counts.OrderByDescending (e => e.Value).First ().Key;
            int top = int.MaxValue, bottom = -1;

            for (var y = area.Top; y < Math.Min (bitmap.Height, area.Bottom); y++)
                for (var x = area.Left; x < Math.Min (bitmap.Width, area.Right); x++) {
                    var p = bitmap.GetPixel (x, y);

                    if (Math.Abs (p.Red - background.Red) + Math.Abs (p.Green - background.Green) + Math.Abs (p.Blue - background.Blue) > 60) {
                        top = Math.Min (top, y);
                        bottom = Math.Max (bottom, y);
                        break;
                    }
                }

            return bottom < 0 ? 0 : bottom - top + 1;
        }
    }
}
