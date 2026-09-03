using System;
using Majorsilence.Forms.Headless;
using Xunit;

namespace Majorsilence.Forms.Tests
{
    // W5.13 (findings TXT-03 P0, TXT-18, TXT-19): the mask was stored and read by nothing. Text was
    // whatever had been typed, MaskCompleted and MaskFull were `=> true` unconditionally, no prompt
    // characters were drawn, and MaskInputRejected/TypeValidationCompleted discarded their handlers --
    // so a phone/SSN/date field accepted anything and the documented ways to validate one were dead.
    //
    // The engine is the BCL's MaskedTextProvider, so these tests pin the WIRING -- that typing, Text,
    // the completion flags and the events all go through it -- rather than mask semantics, which are
    // the provider's own and are not this layer's to redefine.
    [Collection ("Headless")]
    public class MaskedTextBoxMaskEngineTests
    {
        private static MaskedTextBox Masked (string mask)
        {
            HeadlessRenderer.Use ();
            return new MaskedTextBox { Mask = mask };
        }

        private static void Type (MaskedTextBox box, string characters)
        {
            foreach (var c in characters)
                box.RaiseKeyPress (new KeyPressEventArgs (c));
        }

        [Fact]
        public void Typing_places_digits_and_rejects_letters ()
        {
            using var box = Masked ("000-0000");
            var rejected = 0;
            MaskInputRejectedEventArgs? last = null;
            box.MaskInputRejected += (_, e) => { rejected++; last = e; };

            Type (box, "55a5");

            // The letter is refused and the digits keep their positions -- it does not shift them along.
            Assert.Equal (1, rejected);
            Assert.NotNull (last);
            Assert.Equal ("555-", box.Text);
            Assert.False (box.MaskCompleted);
        }

        [Fact]
        public void A_completed_mask_reports_completed_and_full ()
        {
            using var box = Masked ("000-0000");

            Type (box, "5551234");

            Assert.Equal ("555-1234", box.Text);
            Assert.True (box.MaskCompleted);
            Assert.True (box.MaskFull);
        }

        [Fact]
        public void The_prompt_is_displayed_before_anything_is_typed ()
        {
            using var box = Masked ("000-0000");

            // The visible field, which is what made a masked box look like a plain TextBox before: the
            // document shows the prompt-and-literal display string even while Text (IncludeLiterals)
            // reports only what is filled in.
            Assert.Equal ("___-____", box.DisplayedMaskText);
        }

        [Fact]
        public void TextMaskFormat_decides_what_Text_includes ()
        {
            using var box = Masked ("000-0000");
            Type (box, "5551234");

            box.TextMaskFormat = MaskFormat.ExcludePromptAndLiterals;
            Assert.Equal ("5551234", box.Text);

            box.TextMaskFormat = MaskFormat.IncludeLiterals;
            Assert.Equal ("555-1234", box.Text);

            box.TextMaskFormat = MaskFormat.IncludePromptAndLiterals;
            Assert.Equal ("555-1234", box.Text);
        }

        [Fact]
        public void Backspace_clears_a_position_without_shortening_the_field ()
        {
            using var box = Masked ("000-0000");
            Type (box, "5551234");

            box.RaiseKeyDown (new KeyEventArgs (Keys.Back));

            // The mask's length and literals are fixed, so Backspace blanks the character back to its
            // prompt rather than deleting it and pulling the rest along.
            Assert.Equal ("555-123", box.Text);
            Assert.Equal ("555-123_", box.DisplayedMaskText);
            Assert.False (box.MaskCompleted);
        }

        [Fact]
        public void Setting_Mask_carries_the_existing_text_across ()
        {
            HeadlessRenderer.Use ();
            using var box = new MaskedTextBox ();
            var mask_changes = 0;
            box.MaskChanged += (_, _) => mask_changes++;

            box.Text = "5551234";
            box.Mask = "000-0000";

            // A designer sets Text before Mask as often as after, and MaskChanged used to be declared
            // with its raiser suppressed (TXT-19).
            Assert.Equal ("555-1234", box.Text);
            Assert.Equal (1, mask_changes);
        }

        // A guard rather than a proof: an unmasked box behaved this way before too, so this cannot
        // fail against the old code. It is here because the null-mask path is easy to break while
        // making the masked one work.
        [Fact]
        public void An_empty_mask_behaves_as_a_plain_TextBox ()
        {
            // Upstream's null-mask path: with no mask there is no provider, nothing is enforced, and the
            // completion flags answer true because there is nothing to complete.
            HeadlessRenderer.Use ();
            using var box = new MaskedTextBox ();

            box.Text = "anything at all";

            Assert.Equal ("anything at all", box.Text);
            Assert.True (box.MaskCompleted);
            Assert.Null (box.MaskedTextProvider);
        }

        [Fact]
        public void MaskedTextProvider_is_a_clone_not_the_live_engine ()
        {
            using var box = Masked ("000-0000");
            Type (box, "555");

            var provider = box.MaskedTextProvider;

            Assert.NotNull (provider);
            Assert.Equal ("555-____", provider!.ToDisplayString ());

            // Mutating the handed-out provider must not desynchronise the box from its own engine.
            provider.Set ("9999999");

            Assert.Equal ("555-", box.Text);
        }

        [Fact]
        public void UseSystemPasswordChar_reaches_the_TextBox_that_implements_it ()
        {
            // It was a `new bool` shadow storing a flag nothing read, so a PIN box showed its contents
            // in clear text (TXT-18).
            using var box = Masked ("0000");

            box.UseSystemPasswordChar = true;

            Assert.True (box.UseSystemPasswordChar);
            Assert.True (((TextBox)box).UseSystemPasswordChar);
        }

        // ── TXT-19: type validation ─────────────────────────────────────────────────────────────

        private sealed class ValidatingBox : MaskedTextBox
        {
            internal void Validate (System.ComponentModel.CancelEventArgs e) => OnValidating (e);
        }

        [Fact]
        public void TypeValidationCompleted_reports_a_value_that_does_not_convert ()
        {
            HeadlessRenderer.Use ();
            using var box = new ValidatingBox { Mask = "0000", ValidatingType = typeof (int) };
            TypeValidationEventArgs? seen = null;
            box.TypeValidationCompleted += (_, e) => { seen = e; e.Cancel = true; };

            Type (box, "12");   // incomplete: the mask wants four digits

            var cancel = new System.ComponentModel.CancelEventArgs ();
            box.Validate (cancel);

            // The documented way to validate a masked box, and its handlers were discarded outright.
            Assert.NotNull (seen);
            Assert.False (seen!.IsValidInput);
            Assert.True (cancel.Cancel);   // the handler's Cancel propagates, trapping focus
        }

        [Fact]
        public void TypeValidationCompleted_reports_the_converted_value ()
        {
            HeadlessRenderer.Use ();
            using var box = new ValidatingBox { Mask = "0000", ValidatingType = typeof (int) };
            TypeValidationEventArgs? seen = null;
            box.TypeValidationCompleted += (_, e) => seen = e;

            Type (box, "1234");
            box.Validate (new System.ComponentModel.CancelEventArgs ());

            Assert.NotNull (seen);
            Assert.True (seen!.IsValidInput);
            Assert.Equal (1234, seen.ReturnValue);
        }
    }
}
