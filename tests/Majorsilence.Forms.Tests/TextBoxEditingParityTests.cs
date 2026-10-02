using System;
using System.Linq;
using Majorsilence.Forms.Headless;
using SkiaSharp;
using Xunit;

namespace Majorsilence.Forms.Tests
{
    // #350, the text-controls behaviour-gap findings (docs/behaviour-gap/text.md): clipboard verbs on
    // a password box (TXT-08), the keyboard and mouse editing gestures the native edit control has
    // (TXT-13, TXT-23, TXT-24), the selection members' upstream semantics (TXT-20, TXT-21, TXT-28),
    // the read-only and placeholder appearance (TXT-25, TXT-27), RichTextBox's selection and link
    // notifications (TXT-29, TXT-30), visual line numbers (TXT-31), MaskedTextBox's type validation
    // (TXT-33) and the two password flags (TXT-34).
    [Collection ("Headless")]
    public class TextBoxEditingParityTests
    {
        private static T Make<T> (T box, int width = 160, int height = 24) where T : TextBox
        {
            HeadlessRenderer.Use ();

            box.Width = width;
            box.Height = height;

            return box;
        }

        private static TextBox Box (string text = "", bool multiline = false, int width = 160, int height = 24)
        {
            var box = Make (new TextBox (), width, height);
            box.Multiline = multiline;
            box.Text = text;

            return box;
        }

        private static void Type (TextBox box, string characters)
        {
            foreach (var c in characters)
                box.RaiseKeyPress (new KeyPressEventArgs (c));
        }

        private static void Key (TextBox box, Keys keys) => box.RaiseKeyDown (new KeyEventArgs (keys));

        // The protected mouse and focus notifications, which a real window reaches through hit-testing
        // and the focus walk.
        private sealed class Probe : TextBox
        {
            internal bool focused_override;

            public override bool Focused => focused_override || base.Focused;

            internal void Down (System.Drawing.Point p, int clicks, Keys keys)
                => OnMouseDown (new MouseEventArgs (MouseButtons.Left, clicks, p.X, p.Y, System.Drawing.Point.Empty, keyData: keys));

            internal void Up (System.Drawing.Point p, int clicks, Keys keys)
                => OnMouseUp (new MouseEventArgs (MouseButtons.Left, clicks, p.X, p.Y, System.Drawing.Point.Empty, keyData: keys));

            internal void ClickAt (int index, int clicks = 1, Keys keys = Keys.None)
            {
                var at = GetPositionFromCharIndex (index);
                var p = new System.Drawing.Point (at.X + 1, at.Y + 2);

                Down (p, clicks, keys);
                Up (p, clicks, keys);
            }

            internal void GainFocus () => OnGotFocus (EventArgs.Empty);
        }

        private static Probe ProbeBox (string text)
        {
            var box = Make (new Probe ());
            box.Text = text;

            return box;
        }

        // ── TXT-08 ──────────────────────────────────────────────────────────────────────────────────

        [Theory]
        [InlineData (false)]
        [InlineData (true)]
        public void Copy_from_a_password_box_leaves_the_clipboard_alone (bool system)
        {
            using var box = Box ("secret");

            if (system)
                box.UseSystemPasswordChar = true;
            else
                box.PasswordChar = '*';

            Clipboard.SetText ("before");
            box.SelectAll ();

            box.Copy ();
            Key (box, Keys.Control | Keys.C);

            Assert.Equal ("before", Clipboard.GetText ());
        }

        [Fact]
        public void Cut_from_a_password_box_neither_copies_nor_deletes ()
        {
            using var box = Box ("secret");
            box.PasswordChar = '*';
            Clipboard.SetText ("before");
            box.SelectAll ();

            box.Cut ();

            Assert.Equal ("before", Clipboard.GetText ());
            Assert.Equal ("secret", box.Text);
        }

        [Fact]
        public void Copy_from_a_masked_password_box_leaves_the_clipboard_alone ()
        {
            using var box = Make (new MaskedTextBox { Mask = "0000" });
            box.PasswordChar = '*';
            box.Text = "1234";
            Clipboard.SetText ("before");
            box.SelectAll ();

            box.Copy ();

            Assert.Equal ("before", Clipboard.GetText ());
        }

        [Fact]
        public void Copy_from_an_ordinary_box_still_copies ()
        {
            // GUARD, not proof: pins that the password check did not switch copying off everywhere.
            using var box = Box ("plain");
            Clipboard.SetText ("before");
            box.SelectAll ();

            box.Copy ();

            Assert.Equal ("plain", Clipboard.GetText ());
        }

        // ── TXT-13 ──────────────────────────────────────────────────────────────────────────────────

        [Fact]
        public void Ctrl_Z_undoes_typing ()
        {
            using var box = Box ();
            Type (box, "ab");

            Key (box, Keys.Control | Keys.Z);

            Assert.Equal ("", box.Text);

            // And again redoes it: the Win32 single-level buffer toggles.
            Key (box, Keys.Control | Keys.Z);

            Assert.Equal ("ab", box.Text);
        }

        [Fact]
        public void Ctrl_Y_redoes_in_a_RichTextBox ()
        {
            using var box = Make (new RichTextBox ());
            Type (box, "ab");
            Key (box, Keys.Control | Keys.Z);
            Assert.Equal ("", box.Text);

            Key (box, Keys.Control | Keys.Y);

            Assert.Equal ("ab", box.Text);
        }

        [Fact]
        public void Ctrl_Z_does_nothing_when_shortcuts_are_off ()
        {
            // GUARD, not proof: before the binding existed Ctrl+Z did nothing either. Pins that it
            // honours ShortcutsEnabled, whose list upstream includes CtrlZ.
            using var box = Box ();
            Type (box, "ab");
            box.ShortcutsEnabled = false;

            Key (box, Keys.Control | Keys.Z);

            Assert.Equal ("ab", box.Text);
        }

        // ── TXT-20 ──────────────────────────────────────────────────────────────────────────────────

        [Fact]
        public void SelectedText_ignores_MaxLength_and_clears_Modified_and_undo ()
        {
            using var box = Box ("ab");
            box.MaxLength = 3;
            Type (box, "x");          // a real edit: Modified, and something to undo
            Assert.True (box.Modified);

            box.Select (box.TextLength, 0);
            box.SelectedText = "XYZ";

            Assert.Equal ("xabXYZ", box.Text);   // past MaxLength: EM_LIMITTEXT is lifted for it
            Assert.False (box.Modified);          // EM_SETMODIFY 0
            Assert.False (box.CanUndo);           // ClearUndo
        }

        [Fact]
        public void Paste_string_ignores_the_limits_but_is_an_undoable_edit ()
        {
            // Upstream's Paste (string) is SetSelectedTextInternal (text, clearUndo: false).
            using var box = Box ("ab");
            box.ReadOnly = true;
            box.MaxLength = 2;
            box.Select (2, 0);

            box.Paste ("cd");

            Assert.Equal ("abcd", box.Text);
            Assert.True (box.Modified);
            Assert.True (box.CanUndo);
        }

        // ── TXT-21 ──────────────────────────────────────────────────────────────────────────────────

        [Fact]
        public void SelectionStart_keeps_the_selection_length ()
        {
            using var box = Box ("abcdef");
            box.SelectionLength = 2;

            box.SelectionStart = 3;

            Assert.Equal ("de", box.SelectedText);
        }

        // ── TXT-23 ──────────────────────────────────────────────────────────────────────────────────

        [Fact]
        public void Ctrl_Backspace_deletes_back_to_the_start_of_the_word ()
        {
            using var box = Box ("foo bar");
            box.Select (7, 0);

            Key (box, Keys.Control | Keys.Back);

            Assert.Equal ("foo ", box.Text);
            Assert.Equal (4, box.SelectionStart);

            // From just after a space, the word before it goes with the gap (GetWordBoundaryStart).
            Key (box, Keys.Control | Keys.Back);

            Assert.Equal ("", box.Text);
        }

        [Fact]
        public void Ctrl_Delete_deletes_the_word_after_the_caret ()
        {
            using var box = Box ("foo bar");
            box.Select (0, 0);

            Key (box, Keys.Control | Keys.Delete);

            Assert.Equal ("bar", box.Text);
        }

        // ── TXT-24 ──────────────────────────────────────────────────────────────────────────────────

        [Fact]
        public void Double_click_selects_the_word_under_the_pointer ()
        {
            using var box = ProbeBox ("foo bar baz");

            box.ClickAt (5);
            box.ClickAt (5, clicks: 2);

            Assert.Equal ("bar", box.SelectedText);
        }

        [Fact]
        public void Shift_click_extends_the_selection_from_the_caret ()
        {
            using var box = ProbeBox ("foo bar baz");
            box.Select (1, 0);

            box.ClickAt (6, keys: Keys.Shift);

            Assert.Equal (1, box.SelectionStart);
            Assert.Equal (5, box.SelectionLength);
        }

        [Fact]
        public void PageDown_moves_further_than_Down_and_Shift_selects ()
        {
            var text = string.Join ("\n", Enumerable.Range (0, 30).Select (i => "line" + i));
            using var by_line = Box (text, multiline: true, height: 80);
            using var by_page = Box (text, multiline: true, height: 80);

            Key (by_line, Keys.Down);
            Key (by_page, Keys.PageDown);

            var one_line = by_line.GetLineFromCharIndex (by_line.SelectionStart);
            var one_page = by_page.GetLineFromCharIndex (by_page.SelectionStart);

            Assert.Equal (1, one_line);
            Assert.True (one_page > one_line + 1, $"PageDown reached line {one_page}");

            Key (by_page, Keys.Shift | Keys.PageUp);

            Assert.Equal (0, by_page.SelectionStart);
            Assert.True (by_page.SelectionLength > 0);
        }

        // ── TXT-25 ──────────────────────────────────────────────────────────────────────────────────

        [Fact]
        public void A_read_only_box_reports_and_paints_the_control_background ()
        {
            using var box = Box ("x");
            var editable = box.BackColor;
            Assert.NotEqual (Theme.BackgroundColor.ToDrawingColor (), editable);   // precondition

            box.ReadOnly = true;

            Assert.Equal (Theme.BackgroundColor.ToDrawingColor (), box.BackColor);

            using (var bitmap = PaintSurface.Render (box, 1f))
                Assert.Equal (Theme.BackgroundColor, bitmap.GetPixel (box.Width - 4, box.Height / 2));

            box.ReadOnly = false;

            Assert.Equal (editable, box.BackColor);
        }

        [Fact]
        public void An_explicit_BackColor_wins_over_read_only ()
        {
            // GUARD, not proof: ShouldSerializeBackColor's meaning -- only a colour the box was not
            // given follows ReadOnly.
            using var box = Box ("x");
            box.BackColor = System.Drawing.Color.Red;

            box.ReadOnly = true;

            Assert.Equal (System.Drawing.Color.Red.ToArgb (), box.BackColor.ToArgb ());
        }

        // ── TXT-27 ──────────────────────────────────────────────────────────────────────────────────

        [Fact]
        public void The_placeholder_hides_while_the_box_has_focus ()
        {
            using var box = ProbeBox ("");
            box.PlaceholderText = "Search here";

            using (var unfocused = PaintSurface.Render (box, 1f))
                Assert.True (InkColumns (unfocused, box) > 0, "the placeholder should paint while unfocused");

            box.focused_override = true;
            box.Invalidate ();

            Assert.Equal ("", box.document.DisplayText);

            using (var focused = PaintSurface.Render (box, 1f))
                Assert.Equal (0, InkColumns (focused, box));
        }

        // Columns inside the padded client area holding anything other than the background colour.
        private static int InkColumns (SKBitmap bitmap, TextBox box)
        {
            var inner = box.PaddedClientRectangle;
            var background = bitmap.GetPixel (inner.Right - 2, inner.Top + inner.Height / 2);
            var columns = 0;

            for (var x = inner.Left + 1; x < inner.Right - 1; x++)
                for (var y = inner.Top + 1; y < inner.Bottom - 1; y++)
                    if (bitmap.GetPixel (x, y) != background) {
                        columns++;
                        break;
                    }

            return columns;
        }

        // ── TXT-28 ──────────────────────────────────────────────────────────────────────────────────

        [Fact]
        public void First_keyboard_focus_after_Text_selects_all ()
        {
            using var box = ProbeBox ("abc");
            var buttons = Control.MouseButtons;

            try {
                // Control.MouseButtons is process-wide: an earlier test that left a button down
                // would suppress the select-all, so pin it for this test.
                Control.MouseButtons = MouseButtons.None;
                box.GainFocus ();

                Assert.Equal (3, box.SelectionLength);

                // One shot: a later focus leaves the caret where the user put it.
                box.Select (1, 0);
                box.GainFocus ();

                Assert.Equal (0, box.SelectionLength);
            } finally {
                Control.MouseButtons = buttons;
            }
        }

        [Fact]
        public void A_programmatic_selection_or_a_mouse_press_stops_the_select_all ()
        {
            using var selected = ProbeBox ("abc");
            selected.Select (1, 0);
            selected.GainFocus ();

            Assert.Equal (0, selected.SelectionLength);

            using var clicked = ProbeBox ("abc");
            var buttons = Control.MouseButtons;

            try {
                Control.MouseButtons = MouseButtons.Left;
                clicked.GainFocus ();
            } finally {
                Control.MouseButtons = buttons;
            }

            Assert.Equal (0, clicked.SelectionLength);
        }

        // ── TXT-29 ──────────────────────────────────────────────────────────────────────────────────

        [Fact]
        public void RichTextBox_raises_SelectionChanged_once_per_change ()
        {
            using var box = Make (new RichTextBox ());
            box.Text = "hello";
            var raised = 0;
            box.SelectionChanged += (_, _) => raised++;

            box.Select (0, 1);
            Assert.Equal (1, raised);

            box.Select (0, 1);        // no change, no event
            Assert.Equal (1, raised);

            Key (box, Keys.End);      // the caret moving is a selection change too
            Assert.Equal (2, raised);

            Type (box, "!");
            Assert.Equal (3, raised);
        }

        // ── TXT-30 ──────────────────────────────────────────────────────────────────────────────────

        [Fact]
        public void A_detected_link_is_painted_as_a_link ()
        {
            using var box = Make (new RichTextBox ());
            box.Text = "see https://x.y now";

            var spans = box.Colorizer!.Invoke (box.Text).ToList ();
            var link = Assert.Single (spans);

            Assert.Equal (4, link.Start);
            Assert.Equal ("https://x.y".Length, link.Length);
            Assert.True (link.Underline);
            Assert.Equal (Theme.AccentColor, link.Color);

            // And not once detection is off.
            box.DetectUrls = false;

            Assert.Empty (box.Colorizer!.Invoke (box.Text));
        }

        // ── TXT-31 ──────────────────────────────────────────────────────────────────────────────────

        [Fact]
        public void Line_numbers_count_wrapped_lines_in_a_multiline_box ()
        {
            using var box = Box ("aaaa bbbb cccc dddd eeee ffff", multiline: true, width: 60, height: 100);

            var last = box.GetLineFromCharIndex (box.TextLength - 1);
            Assert.True (last >= 1, $"no wrapped line was counted (last line {last})");

            // The line's first character is where the wrap put it -- the start of a word, not 0.
            var second = box.GetFirstCharIndexFromLine (1);
            Assert.True (second > 0);
            Assert.Equal (' ', box.Text[second - 1]);
            Assert.Equal (1, box.GetLineFromCharIndex (second));
            Assert.Equal (-1, box.GetFirstCharIndexFromLine (last + 1));
        }

        [Fact]
        public void Unwrapped_multiline_text_still_counts_its_newlines ()
        {
            // GUARD, not proof: with nothing to wrap, visual and logical lines agree.
            using var box = Box ("one\ntwo\nthree", multiline: true, width: 300, height: 100);

            Assert.Equal (1, box.GetLineFromCharIndex (5));
            Assert.Equal (8, box.GetFirstCharIndexFromLine (2));
        }

        // ── TXT-33 ──────────────────────────────────────────────────────────────────────────────────

        [Fact]
        public void ValidateText_parses_types_that_are_not_IConvertible ()
        {
            using var guid = new MaskedTextBox { ValidatingType = typeof (Guid), Text = Guid.Empty.ToString () };
            using var span = new MaskedTextBox { ValidatingType = typeof (TimeSpan), Text = "01:02:03" };

            Assert.Equal (Guid.Empty, guid.ValidateText ());
            Assert.Equal (new TimeSpan (1, 2, 3), span.ValidateText ());
        }

        [Fact]
        public void ValidateText_reports_an_incomplete_mask_without_parsing ()
        {
            using var box = new MaskedTextBox { Mask = "00-00", ValidatingType = typeof (string) };
            box.Text = "12";
            TypeValidationEventArgs? seen = null;
            box.TypeValidationCompleted += (_, e) => seen = e;

            Assert.Null (box.ValidateText ());

            // ValidateText is PerformTypeValidation (null) upstream, so it raises the event too.
            Assert.NotNull (seen);
            Assert.False (seen!.IsValidInput);
        }

        // ── TXT-34 ──────────────────────────────────────────────────────────────────────────────────

        [Fact]
        public void The_two_password_flags_are_independent ()
        {
            using var box = new TextBox ();

            box.PasswordChar = '*';
            Assert.False (box.UseSystemPasswordChar);

            box.UseSystemPasswordChar = false;
            Assert.Equal ('*', box.PasswordChar);

            box.UseSystemPasswordChar = true;
            Assert.Equal (TextBox.SystemPasswordChar, box.PasswordChar);

            box.UseSystemPasswordChar = false;
            Assert.Equal ('*', box.PasswordChar);
        }
    }
}
