using Xunit;

namespace Majorsilence.Forms.Tests
{
    /// <summary>
    /// Covers the editing surface lifted onto <see cref="TextBoxBase"/> (docs/winforms-gap-plan.md).
    ///
    /// The point of the pass was that ordinary WinForms code typed against the base — <c>void Bind
    /// (TextBoxBase box)</c> — did not compile, so most of these deliberately call <em>through a
    /// TextBoxBase-typed reference</em> rather than through <see cref="TextBox"/>. A test that used
    /// the derived type would still pass if the members slid back down into TextBox.
    /// </summary>
    public class TextBoxBaseTests
    {
        // The base type is the point of the test, not an oversight: returning TextBox would make
        // every assertion below pass through the derived members and prove nothing about the base.
#pragma warning disable CA1859
        private static TextBoxBase NewBox () => new TextBox ();
#pragma warning restore CA1859

        [Fact]
        public void Lines_round_trips_through_the_base ()
        {
            var box = NewBox ();

            Assert.Empty (box.Lines);          // empty array, not one empty string

            box.Lines = ["alpha", "beta", "gamma"];

            Assert.Equal ("alpha\nbeta\ngamma", box.Text);
            Assert.Equal (["alpha", "beta", "gamma"], box.Lines);
            Assert.Equal (16, box.TextLength);
        }

        [Fact]
        public void Lines_treats_CRLF_and_a_bare_CR_as_one_break_each ()
        {
            var box = NewBox ();
            box.Text = "one\r\ntwo\rthree\nfour";

            Assert.Equal (["one", "two", "three", "four"], box.Lines);
        }

        [Fact]
        public void GetLineFromCharIndex_reports_the_logical_line ()
        {
            var box = NewBox ();
            box.Text = "one\ntwo\nthree";

            Assert.Equal (0, box.GetLineFromCharIndex (0));
            Assert.Equal (0, box.GetLineFromCharIndex (3));    // the newline itself ends line 0
            Assert.Equal (1, box.GetLineFromCharIndex (4));
            Assert.Equal (2, box.GetLineFromCharIndex (9));
            Assert.Equal (2, box.GetLineFromCharIndex (999));  // past the end clamps
        }

        [Fact]
        public void GetFirstCharIndexFromLine_returns_minus_one_for_a_line_that_does_not_exist ()
        {
            var box = NewBox ();
            box.Text = "one\ntwo";

            Assert.Equal (0, box.GetFirstCharIndexFromLine (0));
            Assert.Equal (4, box.GetFirstCharIndexFromLine (1));
            Assert.Equal (-1, box.GetFirstCharIndexFromLine (2));    // WinForms returns -1, not Length
            Assert.Equal (-1, box.GetFirstCharIndexFromLine (-1));
        }

        [Fact]
        public void GetFirstCharIndexOfCurrentLine_follows_the_caret ()
        {
            var box = NewBox ();
            box.Text = "one\ntwo\nthree";

            box.SelectionStart = 9;

            Assert.Equal (8, box.GetFirstCharIndexOfCurrentLine ());
        }

        [Fact]
        public void SelectedText_reads_and_replaces_the_selection ()
        {
            var box = NewBox ();
            box.Text = "hello world";
            box.SelectionStart = 6;
            box.SelectionLength = 5;

            Assert.Equal ("world", box.SelectedText);

            box.SelectedText = "there";

            Assert.Equal ("hello there", box.Text);
        }

        [Fact]
        public void SelectAll_then_DeselectAll_through_the_base ()
        {
            var box = NewBox ();
            box.Text = "abcdef";

            box.SelectAll ();
            Assert.Equal ("abcdef", box.SelectedText);

            box.DeselectAll ();
            Assert.Equal (string.Empty, box.SelectedText);
        }

        [Fact]
        public void Clear_and_AppendText_work_through_the_base ()
        {
            var box = NewBox ();

            box.AppendText ("first");
            box.AppendText (" second");
            Assert.Equal ("first second", box.Text);

            box.AppendText ("");             // no-op, not an exception
            Assert.Equal ("first second", box.Text);

            box.Clear ();
            Assert.Equal (string.Empty, box.Text);
        }

        [Fact]
        public void Modified_is_false_after_a_programmatic_assignment ()
        {
            var box = NewBox ();

            box.Modified = true;
            box.Text = "assigned";

            // WinForms treats an assignment as the new baseline, so Modified means "edited since".
            Assert.False (box.Modified);
        }

        [Fact]
        public void ModifiedChanged_is_raised_when_the_flag_actually_changes ()
        {
            var box = NewBox ();
            var raised = 0;
            box.ModifiedChanged += (_, _) => raised++;

            box.Modified = true;
            Assert.Equal (1, raised);

            box.Modified = true;             // no change, no event
            Assert.Equal (1, raised);

            box.Modified = false;
            Assert.Equal (2, raised);
        }

        [Theory]
        [InlineData ("AcceptsTab")]
        [InlineData ("BorderStyle")]
        [InlineData ("HideSelection")]
        [InlineData ("Multiline")]
        [InlineData ("ReadOnly")]
        public void The_state_properties_raise_their_changed_event_once (string property)
        {
            var box = NewBox ();
            var raised = 0;

            switch (property) {
                case "AcceptsTab":
                    box.AcceptsTabChanged += (_, _) => raised++;
                    box.AcceptsTab = true;
                    box.AcceptsTab = true;
                    break;
                case "BorderStyle":
                    box.BorderStyleChanged += (_, _) => raised++;
                    box.BorderStyle = BorderStyle.None;
                    box.BorderStyle = BorderStyle.None;
                    break;
                case "HideSelection":
                    box.HideSelectionChanged += (_, _) => raised++;
                    box.HideSelection = false;
                    box.HideSelection = false;
                    break;
                case "Multiline":
                    box.MultilineChanged += (_, _) => raised++;
                    box.Multiline = true;
                    box.Multiline = true;
                    break;
                case "ReadOnly":
                    box.ReadOnlyChanged += (_, _) => raised++;
                    box.ReadOnly = true;
                    box.ReadOnly = true;
                    break;
            }

            Assert.Equal (1, raised);
        }

        [Fact]
        public void TextBox_keeps_its_own_document_backed_implementations ()
        {
            // Item 3's rule: lifting members to a base must not quietly replace the derived
            // behaviour with the base's default. Multiline and ReadOnly are stored on the base but
            // TextBox routes them into its document, so reading them back through the base type has
            // to see the document's answer.
            var textBox = new TextBox ();
            TextBoxBase asBase = textBox;

            asBase.Multiline = true;
            asBase.ReadOnly = true;
            asBase.MaxLength = 12;

            Assert.True (textBox.Multiline);
            Assert.True (textBox.ReadOnly);
            Assert.Equal (12, textBox.MaxLength);
        }

        [Fact]
        public void RichTextBox_answers_the_line_family_from_the_shared_base ()
        {
            // RichTextBox had its own weaker copies of these; they were deleted so it inherits one
            // implementation. In particular its GetFirstCharIndexFromLine returned Text.Length for a
            // missing line where WinForms returns -1.
            TextBoxBase rich = new RichTextBox ();
            rich.Text = "one\ntwo";

            Assert.Equal (4, rich.GetFirstCharIndexFromLine (1));
            Assert.Equal (-1, rich.GetFirstCharIndexFromLine (5));
            Assert.Equal (1, rich.GetLineFromCharIndex (5));
        }

        [Fact]
        public void CanUndo_is_false_because_there_is_no_undo_stack ()
        {
            // Honest rather than optimistic: reporting true would let an Undo menu item enable itself
            // for an Undo() that does nothing.
            var box = NewBox ();
            box.Text = "edited";

            Assert.False (box.CanUndo);
            box.Undo ();                    // must not throw
            box.ClearUndo ();
        }

        [Fact]
        public void PreferredHeight_covers_a_line_of_text_plus_padding ()
        {
            var box = NewBox ();
            box.Padding = new Padding (0, 5, 0, 5);

            var withPadding = box.PreferredHeight;

            box.Padding = new Padding (0);

            Assert.Equal (withPadding - 10, box.PreferredHeight);
        }
    }
}
