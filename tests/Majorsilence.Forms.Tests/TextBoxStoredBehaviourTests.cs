using System;
using Majorsilence.Forms.Headless;
using Majorsilence.Forms.Renderers;
using Xunit;

namespace Majorsilence.Forms.Tests
{
    // W5.11 (findings TXT-05, TXT-06, TXT-07, TXT-11, TXT-12, TXT-22, TXT-26): five properties that
    // were stored and read by nothing, and two crashes. The crashes are the reason this item is not
    // cosmetic -- a MaxLength set after the text was loaded, or End pressed in an empty box with a
    // placeholder, threw ArgumentOutOfRangeException out of a keystroke and took the app with it.
    [Collection ("Headless")]
    public class TextBoxStoredBehaviourTests
    {
        private static TextBox Box (bool multiline = false)
        {
            HeadlessRenderer.Use ();

            return new TextBox { Width = 120, Height = multiline ? 60 : 24, Multiline = multiline };
        }

        private static void Type (TextBox box, string characters)
        {
            foreach (var c in characters)
                box.RaiseKeyPress (new KeyPressEventArgs (c));
        }

        // OnDeselected is protected, and reaching it through real focus needs a shown window.
        private sealed class Deselectable : TextBox
        {
            // Not named Deselect: Control.Deselect already exists, and this is deliberately the
            // protected notification rather than the framework's own focus move.
            internal void LoseFocus () => OnDeselected (EventArgs.Empty);
        }

        private sealed class ProbeRenderer : TextBoxRenderer
        {
            internal TextSelection Selection (TextBox control) => GetTextSelection (control);
        }

        [Fact]
        public void Typing_into_a_box_already_longer_than_MaxLength_is_refused_not_a_crash ()
        {
            // TXT-05. The order here is the normal one: the designer sets the limit, then binding or
            // code fills the box from a database value that predates it.
            using var box = Box ();
            box.MaxLength = 3;
            box.Text = "abcdef";

            Type (box, "x");     // threw ArgumentOutOfRangeException

            Assert.Equal ("abcdef", box.Text);
        }

        [Fact]
        public void MaxLength_still_truncates_input_to_the_room_left ()
        {
            // GUARD, not proof: the old arithmetic truncated correctly whenever there WAS room -- it
            // only went wrong (and threw) past the limit. This pins that fixing the crash did not
            // turn the limit itself off.
            using var box = Box ();
            box.MaxLength = 4;
            box.Text = "ab";
            box.Select (2, 0);   // a programmatic Text set leaves the caret at 0; type at the end

            Type (box, "xyz");

            // Two characters of room, so the third is dropped rather than the whole insert refused.
            Assert.Equal ("abxy", box.Text);
        }

        [Fact]
        public void End_in_an_empty_box_with_a_placeholder_leaves_the_caret_in_the_text ()
        {
            // TXT-06. The laid-out block is the placeholder while the text is empty, so the caret used
            // to land inside "Search" -- index 6 of a zero-length string -- and the next insert threw.
            using var box = Box ();
            box.PlaceholderText = "Search";

            box.RaiseKeyDown (new KeyEventArgs (Keys.End));
            Assert.Equal (0, box.SelectionStart);

            Type (box, "a");

            Assert.Equal ("a", box.Text);
            Assert.Equal (1, box.SelectionStart);
        }

        [Fact]
        public void Right_and_Down_in_an_empty_box_with_a_placeholder_are_also_safe ()
        {
            using var box = Box (multiline: true);
            box.PlaceholderText = "Type here";

            box.RaiseKeyDown (new KeyEventArgs (Keys.Right));
            box.RaiseKeyDown (new KeyEventArgs (Keys.Down));

            Type (box, "b");

            Assert.Equal ("b", box.Text);
        }

        [Fact]
        public void The_selection_survives_losing_focus ()
        {
            // TXT-07. Every focus move runs through OnDeselected, so an Edit menu or a Find dialog --
            // both of which take focus -- read SelectionLength == 0 and acted on the wrong place.
            HeadlessRenderer.Use ();
            using var box = new Deselectable { Width = 120, Height = 24, Text = "hello" };
            box.Select (1, 2);

            box.LoseFocus ();

            Assert.Equal (2, box.SelectionLength);
            Assert.Equal ("el", box.SelectedText);
            Assert.Equal (1, box.SelectionStart);
        }

        [Fact]
        public void HideSelection_decides_whether_the_selection_is_painted_not_whether_it_exists ()
        {
            HeadlessRenderer.Use ();
            using var box = new Deselectable { Width = 120, Height = 24, Text = "hello" };
            box.Select (1, 2);
            box.LoseFocus ();
            var renderer = new ProbeRenderer ();

            // Unfocused and hiding (the default): no highlight painted...
            Assert.True (box.HideSelection);
            Assert.Equal (TextSelection.Empty.Start, renderer.Selection (box).Start);
            Assert.Equal (TextSelection.Empty.End, renderer.Selection (box).End);

            // ...but the selection is still there to paint once asked for.
            box.HideSelection = false;

            Assert.Equal (1, renderer.Selection (box).Start);
            Assert.Equal (3, renderer.Selection (box).End);
        }

        [Fact]
        public void A_non_wrapping_multiline_box_keeps_a_long_line_on_one_row ()
        {
            // TXT-11, asserted as a relationship in both directions rather than against a pixel.
            using var wrapped = Box (multiline: true);
            wrapped.Width = 50;
            wrapped.Text = "aaaa bbbb cccc dddd";

            using var flat = Box (multiline: true);
            flat.Width = 50;
            flat.WordWrap = false;
            flat.Text = "aaaa bbbb cccc dddd";

            // Wrapping on: the far end of the text is on a lower row than the start.
            Assert.True (wrapped.GetPositionFromCharIndex (18).Y > wrapped.GetPositionFromCharIndex (0).Y,
                "wrapping is on, so the text should occupy more than one row");

            // Wrapping off: one row, however long the line is.
            Assert.Equal (flat.GetPositionFromCharIndex (0).Y, flat.GetPositionFromCharIndex (18).Y);
        }

        [Fact]
        public void A_horizontal_bar_needs_both_the_mode_and_wrapping_off ()
        {
            // TXT-11/TXT-26: with wrapping on there is nothing to the right to scroll to, so the mode
            // alone is not enough.
            using var box = Box (multiline: true);
            box.Width = 50;
            box.ScrollBars = ScrollBars.Both;
            box.WordWrap = false;
            box.Text = "aaaa bbbb cccc dddd eeee ffff";

            PaintSurface.Render (box).Dispose ();
            Assert.True (box.HorizontalScrollBar.Enabled);

            box.WordWrap = true;

            PaintSurface.Render (box).Dispose ();
            Assert.False (box.HorizontalScrollBar.Enabled);
        }

        // The Normal row is a GUARD rather than proof -- it passed before the fix too, because
        // nothing converted anything. It is here so an over-eager casing pass cannot go unnoticed.
        [Theory]
        [InlineData (CharacterCasing.Upper, "A", "ABC")]
        [InlineData (CharacterCasing.Lower, "a", "abc")]
        [InlineData (CharacterCasing.Normal, "a", "aBc")]
        public void CharacterCasing_converts_typed_and_assigned_text (CharacterCasing casing, string typed, string assigned)
        {
            // TXT-12. Upstream's flag is on the edit control, so it converts WM_SETTEXT as well as
            // keystrokes -- a code field marked Upper reads back upper case however it was filled.
            using var typing = Box ();
            typing.CharacterCasing = casing;

            Type (typing, "a");
            Assert.Equal (typed, typing.Text);

            using var setting = Box ();
            setting.CharacterCasing = casing;
            setting.Text = "aBc";

            Assert.Equal (assigned, setting.Text);
        }

        [Fact]
        public void ShortcutsEnabled_false_refuses_the_clipboard_shortcuts ()
        {
            // TXT-22. What a kiosk or exam-style field sets to keep text off the clipboard.
            using var box = Box ();
            box.Text = "abc";
            box.ShortcutsEnabled = false;

            var e = new KeyEventArgs (Keys.Control | Keys.A);
            box.RaiseKeyDown (e);

            Assert.Equal (0, box.SelectionLength);
            Assert.False (e.Handled);   // and it is left for the form, as upstream's ProcessCmdKey does

            box.ShortcutsEnabled = true;
            box.RaiseKeyDown (new KeyEventArgs (Keys.Control | Keys.A));

            Assert.Equal (3, box.SelectionLength);
        }

        [Fact]
        public void ScrollBars_None_enables_no_vertical_bar_and_Vertical_does ()
        {
            // TXT-26, with a correction to the finding: nothing ever made a bar VISIBLE, so no box got
            // one -- the shadowed property could not reach the base setter that shows them.
            using var quiet = Box (multiline: true);
            quiet.Text = "one\ntwo\nthree\nfour\nfive\nsix\nseven";

            PaintSurface.Render (quiet).Dispose ();
            Assert.Equal (ScrollBars.None, quiet.ScrollBars);
            Assert.False (quiet.VerticalScrollBar.Enabled);

            using var scrolled = Box (multiline: true);
            scrolled.ScrollBars = ScrollBars.Vertical;
            scrolled.Text = "one\ntwo\nthree\nfour\nfive\nsix\nseven";

            PaintSurface.Render (scrolled).Dispose ();
            Assert.True (scrolled.VerticalScrollBar.Enabled);
        }

        [Fact]
        public void Setting_ScrollBars_reaches_the_base_property_that_shows_the_bars ()
        {
            // On a form, because Control.Visible is ambient.
            HeadlessRenderer.Use ();
            using var form = new Form { Width = 300, Height = 200 };
            using var box = Box (multiline: true);
            form.Controls.Add (box);

            Assert.False (box.VerticalScrollBar.Visible);

            box.ScrollBars = ScrollBars.Vertical;

            Assert.True (box.VerticalScrollBar.Visible);
            Assert.False (box.HorizontalScrollBar.Visible);

            box.ScrollBars = ScrollBars.Both;

            Assert.True (box.HorizontalScrollBar.Visible);
        }

        [Fact]
        public void A_box_with_no_bars_still_scrolls_to_its_caret ()
        {
            // GUARD, not proof: no previous version could fail this, since none gated scrolling at all.
            // ScrollBars.None means "no bars", not "no scrolling" -- upstream still brings the caret
            // into view -- so this pins that the new mode gate was applied to the BARS only.
            using var box = Box ();
            box.Text = "a very long single line of text that does not fit the control at all";
            box.Select (box.Text.Length, 0);

            box.ScrollToCaret ();

            Assert.True (box.GetPositionFromCharIndex (box.Text.Length).X <= box.Width,
                "the caret should have been scrolled into view");
        }
    }
}
