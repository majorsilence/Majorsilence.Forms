using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.IO;
using Majorsilence.Forms.Headless;
using SkiaSharp;
using Xunit;

namespace Majorsilence.Forms.Tests
{
    // W5.21 -- buttons, labels and pictures. Thirteen findings across six controls, all of the same
    // family: a property the designer emits that no code reads, or a routine that does the right thing
    // for the default case and the wrong thing for every configured one.
    //
    //   SMP-01  RadioButton ignored AutoCheck when unchecking its group
    //   SMP-02  RadioButton never managed TabStop, so every option was its own tab stop
    //   SMP-03  Appearance stored only -- Appearance.Button drew as an ordinary glyph
    //   SMP-04  AppearanceChanged could not fire from an auto-property
    //   SMP-05  FlatStyle/FlatAppearance stored only on CheckBox and RadioButton
    //   SMP-07  Form.AcceptButton never called NotifyDefault; IsDefault was never rendered
    //   SMP-13  button-family captions never word-wrapped
    //   SMP-14  Label.Multiline defaulted false, so every label collapsed to one clipped line
    //   SMP-15  Label.BorderStyle stored only, never drawn
    //   SMP-20  PictureBox.Load was asynchronous and swallowed failures
    //   SMP-21  LoadAsync/CancelAsync/LoadCompleted/LoadProgressChanged were inert
    //   SMP-23  PictureBox.SizeMode did not invalidate and did not sync AutoSize
    //   SMP-26  ProgressBar.Style was never read; a Marquee bar rendered permanently empty
    //   SMP-29  TrackBar.Value raised Scroll, the "the user moved it" signal
    [Collection ("Headless")]
    public sealed class ButtonsLabelsPicturesTests : IDisposable
    {
        private readonly List<string> temp_files = new ();

        public void Dispose ()
        {
            foreach (var file in temp_files)
                try {
                    File.Delete (file);
                } catch (IOException) {
                    // A leftover temp file is not worth failing a test over.
                }
        }

        // ---------------- SMP-01: AutoCheck decides who takes part in the group

        [Fact]
        public void Two_manual_radio_buttons_can_both_be_checked ()
        {
            // The finding's own test. UpdateSiblings unchecked EVERY sibling regardless of anyone's
            // AutoCheck, so the manually-managed group -- code decides who is checked -- could never
            // show what it was told to.
            using var panel = new Panel ();
            var a = new RadioButton { AutoCheck = false };
            var b = new RadioButton { AutoCheck = false };
            panel.Controls.Add (a);
            panel.Controls.Add (b);

            a.Checked = true;
            b.Checked = true;

            Assert.True (a.Checked);
            Assert.True (b.Checked);
        }

        [Fact]
        public void A_manual_radio_button_is_not_unchecked_by_an_automatic_sibling ()
        {
            using var panel = new Panel ();
            var manual = new RadioButton { AutoCheck = false, Checked = true };
            var automatic = new RadioButton ();
            panel.Controls.Add (manual);
            panel.Controls.Add (automatic);

            automatic.Checked = true;

            Assert.True (manual.Checked);
            Assert.True (automatic.Checked);
        }

        [Fact]
        public void An_automatic_group_still_unchecks_itself ()
        {
            // GUARD, not proof: this is the behaviour that already worked, and the one the AutoCheck
            // filters could most easily have broken. A radio group that stops being exclusive is a
            // worse bug than the one being fixed.
            using var panel = new Panel ();
            var first = new RadioButton { Checked = true };
            var second = new RadioButton ();
            panel.Controls.Add (first);
            panel.Controls.Add (second);

            second.Checked = true;

            Assert.False (first.Checked);
            Assert.True (second.Checked);
        }

        // ---------------- SMP-02: one tab stop per group

        [Fact]
        public void A_radio_button_is_not_a_tab_stop_on_its_own ()
        {
            using var button = new RadioButton ();

            Assert.False (button.TabStop);
        }

        [Fact]
        public void Only_the_checked_member_of_a_group_is_a_tab_stop ()
        {
            // The finding's own test. Every button being its own tab stop means a six-option group
            // costs six tabs to cross instead of one.
            using var panel = new Panel ();
            var first = new RadioButton ();
            var second = new RadioButton ();
            var third = new RadioButton ();
            panel.Controls.Add (first);
            panel.Controls.Add (second);
            panel.Controls.Add (third);

            second.Checked = true;

            Assert.False (first.TabStop);
            Assert.True (second.TabStop);
            Assert.False (third.TabStop);
        }

        [Fact]
        public void The_tab_stop_follows_the_checked_button ()
        {
            using var panel = new Panel ();
            var first = new RadioButton ();
            var second = new RadioButton ();
            panel.Controls.Add (first);
            panel.Controls.Add (second);

            first.Checked = true;
            second.Checked = true;

            Assert.False (first.TabStop);
            Assert.True (second.TabStop);
        }

        [Fact]
        public void A_manual_radio_button_keeps_its_own_tab_stop ()
        {
            // AutoCheck off means the application owns both the check state and the focus order.
            using var panel = new Panel ();
            var manual = new RadioButton { AutoCheck = false, TabStop = true };
            var automatic = new RadioButton ();
            panel.Controls.Add (manual);
            panel.Controls.Add (automatic);

            automatic.Checked = true;

            Assert.True (manual.TabStop);
        }

        // ---------------- SMP-03 / SMP-04: Appearance

        [Fact]
        public void A_toggle_button_radio_reserves_no_glyph_column ()
        {
            // The finding's own test: a row of Appearance.Button radio buttons is the segmented control
            // idiom, and it rendered as ordinary radio circles.
            //
            // Asserted on the preferred size rather than on pixels, because a toggle button paints its
            // own face over the whole control -- every probe region has ink in it whatever is drawn
            // there, so "no glyph" cannot be measured as absence of ink.
            using var normal = new RadioButton { Text = "One", AutoSize = true };
            using var toggle = new RadioButton { Text = "One", AutoSize = true, Appearance = Appearance.Button };

            Assert.True (toggle.PreferredSize.Width < normal.PreferredSize.Width,
                $"a toggle button should not reserve the glyph column ({toggle.PreferredSize.Width} vs {normal.PreferredSize.Width})");
        }

        [Fact]
        public void A_toggle_button_radio_renders_differently ()
        {
            // GUARD, not proof: no neutralization moves it, because the two appearances differ by the
            // glyph OR by the frame and removing either leaves the other. It pins the weaker claim that
            // they are visually distinct at all.
            using var normal = new RadioButton { Width = 90, Height = 28, Text = "One" };
            using var toggle = new RadioButton { Width = 90, Height = 28, Text = "One", Appearance = Appearance.Button };

            Assert.NotEqual (Pixels (normal), Pixels (toggle));
        }

        [Fact]
        public void An_unchecked_toggle_button_still_has_a_frame ()
        {
            // Found by neutralization, not by review: removing the border fallback broke no test.
            // A glyph-less control with no frame and no fill is an INVISIBLE control -- the unselected
            // segments of a segmented bar would simply not be there.
            using var normal = new RadioButton { Width = 90, Height = 28, Text = "One" };
            using var toggle = new RadioButton { Width = 90, Height = 28, Text = "One", Appearance = Appearance.Button };

            Assert.Equal (0, normal.CurrentStyle.Border.GetWidth ());
            Assert.True (toggle.CurrentStyle.Border.GetWidth () > 0, "a toggle button needs a frame of its own");
        }

        [Fact]
        public void A_checked_toggle_button_carries_its_state_in_the_background ()
        {
            // Without a glyph the background is the only thing left to carry the state, so if it does
            // not change, a segmented control cannot show which segment is selected.
            using var off = new RadioButton { Width = 90, Height = 28, Text = "One", Appearance = Appearance.Button };
            using var on = new RadioButton { Width = 90, Height = 28, Text = "One", Appearance = Appearance.Button, Checked = true };

            Assert.NotEqual (Pixels (off), Pixels (on));
        }

        [Fact]
        public void A_toggle_check_box_reserves_no_glyph_column ()
        {
            using var normal = new CheckBox { Text = "One", AutoSize = true };
            using var toggle = new CheckBox { Text = "One", AutoSize = true, Appearance = Appearance.Button };

            Assert.True (toggle.PreferredSize.Width < normal.PreferredSize.Width);
        }

        [Fact]
        public void Changing_Appearance_raises_AppearanceChanged ()
        {
            // SMP-04: an auto-property has nowhere to raise from, so the event -- which exists, with a
            // raiser -- could never fire.
            using var box = new CheckBox ();
            var raised = 0;
            box.AppearanceChanged += (_, _) => raised++;

            box.Appearance = Appearance.Button;
            box.Appearance = Appearance.Button;   // no change, no event

            Assert.Equal (1, raised);
        }

        // ---------------- SMP-05: FlatStyle and FlatAppearance reach the style

        [Fact]
        public void A_flat_check_box_takes_its_border_size_from_FlatAppearance ()
        {
            // The finding's own test is "BorderSize = 0 gives no border", but a check box's themed
            // border is already none, so that assertion passes whether or not anything reads the
            // property -- neutralizing proved exactly that. Asserted with a width the theme would
            // never produce instead.
            using var box = new CheckBox { FlatStyle = FlatStyle.Flat, Width = 90, Height = 28 };
            box.FlatAppearance.BorderSize = 3;

            Assert.Equal (3, box.CurrentStyle.Border.GetWidth ());
        }

        [Fact]
        public void A_standard_check_box_ignores_FlatAppearance ()
        {
            // GUARD, not proof: FlatAppearance applies to a FLAT control, and a control that never
            // reads it passes this too. It pins the gate rather than the reading.
            using var box = new CheckBox { FlatStyle = FlatStyle.Standard, Width = 90, Height = 28 };
            box.FlatAppearance.BorderSize = 3;

            Assert.NotEqual (3, box.CurrentStyle.Border.GetWidth ());
        }

        [Fact]
        public void A_flat_radio_button_takes_its_border_colour_from_FlatAppearance ()
        {
            using var button = new RadioButton { FlatStyle = FlatStyle.Flat, Width = 90, Height = 28 };
            button.FlatAppearance.BorderSize = 2;
            button.FlatAppearance.BorderColor = Color.Red;

            Assert.Equal (2, button.CurrentStyle.Border.GetWidth ());
            Assert.Equal (new SKColor (Color.Red.R, Color.Red.G, Color.Red.B, Color.Red.A), button.CurrentStyle.Border.GetColor ());
        }

        [Fact]
        public void A_checked_flat_check_box_uses_CheckedBackColor ()
        {
            using var box = new CheckBox { FlatStyle = FlatStyle.Flat, Width = 90, Height = 28, Checked = true };
            box.FlatAppearance.CheckedBackColor = Color.Red;

            Assert.Equal (new SKColor (Color.Red.R, Color.Red.G, Color.Red.B, Color.Red.A), box.CurrentStyle.GetBackgroundColor ());
        }

        [Fact]
        public void Returning_to_Standard_restores_the_themed_border ()
        {
            // GUARD, not proof: clearing the override rather than remembering an old value is the whole
            // reason this is resolved at paint time instead of in the setter.
            using var box = new CheckBox { FlatStyle = FlatStyle.Flat, Width = 90, Height = 28 };
            box.FlatAppearance.BorderSize = 3;
            _ = box.CurrentStyle;

            box.FlatStyle = FlatStyle.Standard;

            Assert.Equal (new CheckBox ().CurrentStyle.Border.GetWidth (), box.CurrentStyle.Border.GetWidth ());
        }

        // ---------------- SMP-07: the form's default button

        [Fact]
        public void AcceptButton_tells_the_button_it_is_the_default ()
        {
            // The finding's own test. NotifyDefault existed and nothing called it.
            using var form = new Form ();
            var ok = new Button { Text = "OK" };
            var cancel = new Button { Text = "Cancel" };
            form.Controls.Add (ok);
            form.Controls.Add (cancel);

            form.AcceptButton = ok;

            Assert.True (ok.IsDefault);
            Assert.False (cancel.IsDefault);

            form.AcceptButton = cancel;

            Assert.False (ok.IsDefault);
            Assert.True (cancel.IsDefault);
        }

        [Fact]
        public void The_default_button_is_drawn_differently ()
        {
            // A flag nothing renders leaves the user unable to see which button Enter will press.
            using var form = new Form ();
            var plain = new Button { Text = "OK", Width = 80, Height = 26 };
            form.Controls.Add (plain);

            var before = Pixels (plain, attach: false);
            form.AcceptButton = plain;
            var after = Pixels (plain, attach: false);

            Assert.NotEqual (before, after);
        }

        // ---------------- SMP-13 / SMP-14: captions wrap

        [Fact]
        public void A_two_word_caption_wraps_on_a_tall_button ()
        {
            // The finding's own test. Upstream ORs WordBreak unconditionally for the button family;
            // pinned to one line, "Export Selected" came out clipped.
            using var one_word = new Button { Width = 60, Height = 60, Text = "Export" };
            using var two_words = new Button { Width = 60, Height = 60, Text = "Export Selected" };

            // Relational, so it survives the font differing between macOS and CI: a wrapped caption
            // occupies strictly more rows of the button than the single word it starts with.
            // "More rows" is too weak: a clipped caption gains a row from its ellipsis, which was
            // enough to pass the test with wrapping neutralized. A second LINE is what wrapping means,
            // so the wrapped caption has to be about twice the height -- relational, so it survives the
            // font measuring 11px on macOS and 13px on CI.
            Assert.True (InkRows (two_words) >= InkRows (one_word) * 3 / 2,
                $"a wrapped caption should be about twice as tall ({InkRows (two_words)} vs {InkRows (one_word)})");
        }

        [Fact]
        public void A_long_check_box_label_wraps ()
        {
            using var one_word = new CheckBox { Width = 90, Height = 60, Text = "Agree" };
            using var sentence = new CheckBox { Width = 90, Height = 60, Text = "I agree to the terms and conditions" };

            Assert.True (InkRows (sentence) >= InkRows (one_word) * 3 / 2,
                $"a wrapped label should be about twice as tall ({InkRows (sentence)} vs {InkRows (one_word)})");
        }

        [Fact]
        public void A_label_wraps_by_default ()
        {
            // SMP-14: upstream's Label has no Multiline property -- it always wraps. Defaulting ours to
            // false collapsed every description and warning in a migrated form to one truncated line,
            // and the app had no designer line to fix it with.
            using var label = new Label { Width = 100, Height = 60, Text = "The quick brown fox jumps over the lazy dog" };

            Assert.True (label.Multiline);

            using var single = new Label { Width = 100, Height = 60, Text = "The quick brown fox jumps over the lazy dog", Multiline = false };

            Assert.True (InkRows (label) >= InkRows (single) * 3 / 2,
                $"a wrapped label should be about twice as tall ({InkRows (label)} vs {InkRows (single)})");
        }

        // ---------------- SMP-15: Label.BorderStyle

        [Fact]
        public void A_label_border_is_drawn ()
        {
            // The finding's own test: labels used as separators or as the poor man's group box lost
            // their frame entirely.
            using var plain = new Label { Width = 80, Height = 30, Text = "x" };
            using var framed = new Label { Width = 80, Height = 30, Text = "x", BorderStyle = BorderStyle.FixedSingle };

            Assert.Equal (0, EdgeInk (plain));
            Assert.True (EdgeInk (framed) > 0, "a FixedSingle label should draw its outer ring");
        }

        [Fact]
        public void A_label_border_shrinks_the_text_region ()
        {
            // Upstream's GetBordersAndPadding takes the border off the text rectangle, which is why the
            // caption moves by a pixel or two -- and why the preferred size changes with it.
            using var plain = new Label { Width = 80, Height = 30, Text = "x", AutoSize = true };
            using var framed = new Label { Width = 80, Height = 30, Text = "x", AutoSize = true, BorderStyle = BorderStyle.Fixed3D };

            Assert.True (framed.PreferredSize.Height > plain.PreferredSize.Height,
                "a 3-D border should make the label want to be taller");
        }

        [Fact]
        public void Clearing_the_border_style_clears_the_border ()
        {
            // GUARD, not proof: a label that never draws a border passes this as well. It pins that
            // None really clears the override, which is the half a remembered-old-value implementation
            // would get wrong.
            using var label = new Label { Width = 80, Height = 30, Text = "x", BorderStyle = BorderStyle.FixedSingle };

            label.BorderStyle = BorderStyle.None;

            Assert.Equal (0, EdgeInk (label));
        }

        // ---------------- SMP-20: Load is synchronous and reports failures

        [Fact]
        public void Load_of_a_missing_file_throws ()
        {
            // The finding's own test. `try { pb.Load (path); } catch (FileNotFoundException) { }` could
            // never catch: the failure happened later, on another thread, inside an async void.
            using var box = new PictureBox ();

            Assert.ThrowsAny<Exception> (() => box.Load (Path.Combine (Path.GetTempPath (), "majorsilence-does-not-exist.png")));
            Assert.True (box.IsErrored);
        }

        [Fact]
        public void Load_of_a_file_that_is_not_an_image_is_an_error ()
        {
            // SKBitmap.Decode returns null rather than throwing, so this silently produced a blank box
            // that reported no error at all.
            using var box = new PictureBox ();
            var path = TempFile (".png", File.WriteAllText, "not an image");

            Assert.ThrowsAny<Exception> (() => box.Load (path));
            Assert.True (box.IsErrored);
        }

        [Fact]
        public void The_image_is_readable_the_moment_Load_returns ()
        {
            // `pb.Load (path); var w = pb.Image.Width;` -- the line the finding opens with -- threw
            // NullReferenceException because the load had not started yet.
            using var box = new PictureBox ();

            box.Load (WriteImage (12, 9));

            Assert.NotNull (box.SKImage);
            Assert.Equal (12, box.SKImage!.Width);
            Assert.False (box.IsErrored);
        }

        [Fact]
        public void Setting_ImageLocation_loads_without_throwing ()
        {
            // A property setter has no way to report a failure, so it records one instead of raising it.
            using var box = new PictureBox ();

            box.ImageLocation = Path.Combine (Path.GetTempPath (), "majorsilence-does-not-exist.png");

            Assert.True (box.IsErrored);
            Assert.Null (box.SKImage);
        }

        // ---------------- SMP-21: the async load pattern works

        [Fact]
        public void LoadAsync_raises_LoadCompleted ()
        {
            // The finding's own test. Both events were declared `add { } remove { }`, so the
            // subscription was discarded at the add site -- the handler that hides the spinner and
            // shows the image could never run, and no failure could be detected at runtime either.
            using var box = new PictureBox ();
            AsyncCompletedEventArgs? completed = null;
            box.LoadCompleted += (_, e) => completed = e;

            box.LoadAsync (WriteImage (8, 8));
            PumpUntil (() => completed is not null);

            Assert.NotNull (completed);
            Assert.Null (completed!.Error);
            Assert.False (completed.Cancelled);
            Assert.NotNull (box.SKImage);
        }

        [Fact]
        public void LoadAsync_reports_a_failure_through_LoadCompleted ()
        {
            using var box = new PictureBox ();
            AsyncCompletedEventArgs? completed = null;
            box.LoadCompleted += (_, e) => completed = e;

            box.LoadAsync (Path.Combine (Path.GetTempPath (), "majorsilence-does-not-exist.png"));
            PumpUntil (() => completed is not null);

            Assert.NotNull (completed);
            Assert.NotNull (completed!.Error);
            Assert.True (box.IsErrored);
        }

        [Fact]
        public void LoadAsync_reports_progress ()
        {
            using var box = new PictureBox ();
            var progress = 0;
            box.LoadProgressChanged += (_, _) => progress++;
            var done = false;
            box.LoadCompleted += (_, _) => done = true;

            box.LoadAsync (WriteImage (8, 8));
            PumpUntil (() => done);

            Assert.True (progress > 0);
        }

        // ---------------- SMP-23: SizeMode

        [Fact]
        public void Changing_SizeMode_invalidates ()
        {
            // The finding's own test. UpdateSize only ever resized under AutoSize, so there was no
            // Invalidate on any path that did not change a bound -- the previously painted image stayed
            // on screen until something else happened to repaint, the classic "click Fit and nothing
            // happens" bug.
            //
            // Asserted on the Invalidated event, not by comparing two renders: rendering builds the
            // bitmap from scratch every time and so is completely insensitive to whether the control
            // ever asked to be repainted. The first version of this test compared renders and passed
            // with the fix neutralized.
            // On a shown form because Control.Invalidate returns early when the control is not
            // Created -- a detached control cannot be asked to repaint.
            HeadlessRenderer.Use ();
            using var form = new Form { Width = 200, Height = 140 };
            var box = new PictureBox { Width = 60, Height = 40 };
            form.Controls.Add (box);
            form.Show ();

            try {
                box.Load (WriteImage (20, 10));
                var invalidated = 0;
                box.Invalidated += (_, _) => invalidated++;

                box.SizeMode = PictureBoxSizeMode.CenterImage;

                Assert.True (invalidated > 0, "changing SizeMode should invalidate the control");
            } finally {
                form.Close ();
            }
        }

        [Fact]
        public void Each_SizeMode_draws_the_image_somewhere_different ()
        {
            // GUARD, not proof: the renderer already did this. It pins that the modes stay distinct,
            // which is the reason the invalidate above matters at all.
            using var normal = new PictureBox { Width = 60, Height = 40 };
            normal.Load (WriteImage (20, 10));
            using var centred = new PictureBox { Width = 60, Height = 40, SizeMode = PictureBoxSizeMode.CenterImage };
            centred.Load (WriteImage (20, 10));

            Assert.NotEqual (Pixels (normal), Pixels (centred));
        }

        [Fact]
        public void SizeMode_AutoSize_turns_AutoSize_on ()
        {
            using var box = new PictureBox ();

            box.SizeMode = PictureBoxSizeMode.AutoSize;

            Assert.True (box.AutoSize);

            box.SizeMode = PictureBoxSizeMode.Zoom;

            Assert.False (box.AutoSize);
        }

        // ---------------- SMP-26: the three progress bar styles

        [Fact]
        public void A_marquee_bar_is_not_empty_at_value_zero ()
        {
            // The finding's own test. Style was never read, so the standard indeterminate bar -- whose
            // Value stays at 0 by definition -- rendered as a permanently empty track and the app
            // looked hung.
            using var bar = new ProgressBar { Width = 120, Height = 20, Style = ProgressBarStyle.Marquee };

            Assert.True (Filled (bar) > 0, "a marquee bar should paint its block");
        }

        [Fact]
        public void The_marquee_block_moves ()
        {
            using var bar = new ProgressBar { Width = 120, Height = 20, Style = ProgressBarStyle.Marquee };

            var first = FirstFilledColumn (bar);
            bar.AdvanceMarquee ();
            var second = FirstFilledColumn (bar);

            Assert.True (second > first, $"the marquee block should advance ({first} -> {second})");
        }

        [Fact]
        public void Blocks_and_Continuous_draw_differently ()
        {
            // Blocks is the WinForms default and is a row of chunks, not a solid fill.
            using var blocks = new ProgressBar { Width = 120, Height = 20, Style = ProgressBarStyle.Blocks, Value = 100 };
            using var continuous = new ProgressBar { Width = 120, Height = 20, Style = ProgressBarStyle.Continuous, Value = 100 };

            Assert.True (Filled (blocks) < Filled (continuous),
                $"a segmented bar should paint less than a solid one over the same extent ({Filled (blocks)} vs {Filled (continuous)})");
        }

        [Fact]
        public void A_determinate_bar_still_tracks_its_value ()
        {
            // GUARD, not proof: the one behaviour that already worked, and the one the style branch
            // could most easily have broken.
            using var quarter = new ProgressBar { Width = 120, Height = 20, Style = ProgressBarStyle.Continuous, Value = 25 };
            using var full = new ProgressBar { Width = 120, Height = 20, Style = ProgressBarStyle.Continuous, Value = 100 };

            Assert.True (Filled (quarter) < Filled (full));
        }

        // ---------------- SMP-29: Scroll means the user moved it

        [Fact]
        public void Setting_TrackBar_Value_from_code_does_not_raise_Scroll ()
        {
            // The finding's own test. Scroll is the "the USER moved it" signal; apps drive a linked
            // control from it while writing Value back from code, so raising it here made the pair
            // re-entrant -- duplicate saves, and an infinite loop between linked sliders.
            using var bar = new TrackBar { Minimum = 0, Maximum = 10 };
            var scrolled = 0;
            var changed = 0;
            bar.Scroll += (_, _) => scrolled++;
            bar.ValueChanged += (_, _) => changed++;

            bar.Value = 5;

            Assert.Equal (0, scrolled);
            Assert.Equal (1, changed);
        }

        [Fact]
        public void A_keyboard_move_still_raises_Scroll ()
        {
            // GUARD, not proof: silencing the setter must not silence the gesture, which is the only
            // thing Scroll is for.
            using var bar = new DrivenTrackBar { Minimum = 0, Maximum = 10, Value = 5 };
            var scrolled = 0;
            bar.Scroll += (_, _) => scrolled++;

            bar.Press (Keys.Right);

            Assert.Equal (1, scrolled);
        }

        private sealed class DrivenTrackBar : TrackBar
        {
            internal void Press (Keys key) => OnKeyDown (new KeyEventArgs (key));
        }

        // ---------------- helpers

        private static SKBitmap Render (Control control, bool attach = true)
        {
            HeadlessRenderer.Use ();

            // Scale 1 explicitly: these compare ink between two renders, and the probe regions are in
            // bitmap pixels, so a doubled bitmap under MF_HEADLESS_SCALE=2 would move them.
            return attach ? PaintSurface.RenderOnForm (control, 1f) : PaintSurface.Render (control, 1f);
        }

        // The rendered pixels as a string, so two renders can be compared for "this looks different"
        // without asserting a colour the theme owns.
        private static string Pixels (Control control, bool attach = true)
        {
            using var bitmap = Render (control, attach);
            var sb = new System.Text.StringBuilder ();

            for (var y = 0; y < bitmap.Height; y++)
                for (var x = 0; x < bitmap.Width; x++)
                    sb.Append (bitmap.GetPixel (x, y));

            return sb.ToString ();
        }

        // Pixels that differ from the render's most common colour -- its background, whatever the theme
        // makes that.
        private static int Ink (Control control) => InkIn (control, null);

        private static int InkIn (Control control, Rectangle? region)
        {
            using var bitmap = Render (control);
            var area = region ?? new Rectangle (0, 0, bitmap.Width, bitmap.Height);
            var background = Background (bitmap);
            var ink = 0;

            for (var y = Math.Max (0, area.Top); y < Math.Min (bitmap.Height, area.Bottom); y++)
                for (var x = Math.Max (0, area.Left); x < Math.Min (bitmap.Width, area.Right); x++)
                    if (bitmap.GetPixel (x, y) != background)
                        ink++;

            return ink;
        }

        // How many rows of the control's INTERIOR carry ink. Wrapping shows up here as more rows,
        // which is a relational fact about the same string and so survives the font measuring
        // differently on macOS and on CI.
        //
        // The inset matters: a button paints a face and a frame over the whole control, so counting
        // rows that merely differ from the commonest colour counts every row of it. Inside the frame
        // the only thing that is not the face is the caption.
        private static int InkRows (Control control, int inset = 4)
        {
            using var bitmap = Render (control);
            var area = Rectangle.Inflate (new Rectangle (0, 0, bitmap.Width, bitmap.Height), -inset, -inset);
            var background = Background (bitmap, area);
            var rows = 0;

            for (var y = area.Top; y < area.Bottom; y++)
                for (var x = area.Left; x < area.Right; x++)
                    if (bitmap.GetPixel (x, y) != background) {
                        rows++;
                        break;
                    }

            return rows;
        }

        // Pixels painted in the progress fill colour. A progress bar that is completely full makes the
        // fill the commonest colour in the bitmap, so "not the background" reports a full bar as empty
        // -- the fill has to be named rather than inferred.
        private static int FirstFilledColumn (ProgressBar bar)
        {
            using var bitmap = Render (bar);
            var fill = bar.Enabled ? Theme.AccentColor2 : Theme.ForegroundDisabledColor;

            for (var x = 0; x < bitmap.Width; x++)
                for (var y = 0; y < bitmap.Height; y++)
                    if (bitmap.GetPixel (x, y) == fill)
                        return x;

            return -1;
        }

        private static int Filled (ProgressBar bar)
        {
            using var bitmap = Render (bar);
            var fill = bar.Enabled ? Theme.AccentColor2 : Theme.ForegroundDisabledColor;
            var filled = 0;

            for (var y = 0; y < bitmap.Height; y++)
                for (var x = 0; x < bitmap.Width; x++)
                    if (bitmap.GetPixel (x, y) == fill)
                        filled++;

            return filled;
        }

        // Ink on the outermost ring of the control -- where a border is and nothing else goes.
        private static int EdgeInk (Control control)
        {
            using var bitmap = Render (control);
            var background = Background (bitmap);
            var ink = 0;

            for (var x = 0; x < bitmap.Width; x++) {
                if (bitmap.GetPixel (x, 0) != background)
                    ink++;

                if (bitmap.GetPixel (x, bitmap.Height - 1) != background)
                    ink++;
            }

            for (var y = 1; y < bitmap.Height - 1; y++) {
                if (bitmap.GetPixel (0, y) != background)
                    ink++;

                if (bitmap.GetPixel (bitmap.Width - 1, y) != background)
                    ink++;
            }

            return ink;
        }

        private static SKColor Background (SKBitmap bitmap, Rectangle? region = null)
        {
            var area = region ?? new Rectangle (0, 0, bitmap.Width, bitmap.Height);
            var counts = new Dictionary<SKColor, int> ();

            for (var y = Math.Max (0, area.Top); y < Math.Min (bitmap.Height, area.Bottom); y++)
                for (var x = Math.Max (0, area.Left); x < Math.Min (bitmap.Width, area.Right); x++) {
                    var pixel = bitmap.GetPixel (x, y);
                    counts[pixel] = counts.TryGetValue (pixel, out var n) ? n + 1 : 1;
                }

            var background = SKColors.Transparent;
            var best = -1;

            foreach (var entry in counts)
                if (entry.Value > best) {
                    best = entry.Value;
                    background = entry.Key;
                }

            return background;
        }

        // Pumps the backend's action queue until the condition holds or the deadline passes, then
        // returns either way so the caller's assertion produces the failure. LoadAsync completes on a
        // thread-pool continuation that marshals back through this queue, and how long that takes is
        // the runner's business -- a fixed sleep here would be a race.
        private static void PumpUntil (Func<bool> condition, int timeout_ms = 10_000)
        {
            var deadline = DateTime.UtcNow.AddMilliseconds (timeout_ms);

            while (DateTime.UtcNow < deadline) {
                Application.DoEvents ();

                if (condition ())
                    return;

                System.Threading.Thread.Sleep (10);
            }

            Application.DoEvents ();
        }

        private string WriteImage (int width, int height)
        {
            using var bitmap = new SKBitmap (width, height);
            using var canvas = new SKCanvas (bitmap);
            canvas.Clear (SKColors.Blue);

            using var data = bitmap.Encode (SKEncodedImageFormat.Png, 100);

            return TempFile (".png", (path, content) => File.WriteAllBytes (path, content), data.ToArray ());
        }

        private string TempFile<T> (string extension, Action<string, T> write, T content)
        {
            var path = Path.Combine (Path.GetTempPath (), $"majorsilence-w521-{Guid.NewGuid ():N}{extension}");
            write (path, content);
            temp_files.Add (path);

            return path;
        }
    }
}
