using System;
using System.Drawing;
using System.IO;
using Majorsilence.Forms.Headless;
using SkiaSharp;
using Xunit;

namespace Majorsilence.Forms.Tests
{
    // W5.14 (findings TXT-04 P0, TXT-14, TXT-15, TXT-16, TXT-17): the RichTextBox document model.
    //
    // The P0 is data loss on save. `Rtf` returned a stored string that only its own setter ever
    // wrote, so `note.Body = rtb.Rtf` stored an empty document for everything the user had typed, or
    // the RTF of the last programmatic assignment -- stale content overwriting the edits. The reader
    // then compounded it: paragraphs, accented characters and anything inside a `{...}` group were
    // dropped on load, and the getter saved that back.
    [Collection ("Headless")]
    public class RichTextBoxDocumentTests
    {
        private static RichTextBox Rich (string? text = null)
        {
            HeadlessRenderer.Use ();
            var box = new RichTextBox { Width = 200, Height = 80 };

            if (text is not null)
                box.Text = text;

            return box;
        }

        private static void Type (RichTextBox box, string characters)
        {
            foreach (var c in characters)
                box.RaiseKeyPress (new KeyPressEventArgs (c));
        }

        // ---------------- TXT-04: the getter renders the current document

        [Fact]
        public void Rtf_renders_the_current_text ()
        {
            using var box = Rich ("a\nb");

            var rtf = box.Rtf;

            Assert.StartsWith (@"{\rtf1", rtf);
            Assert.Contains (@"a\par", rtf);
            Assert.Contains ("b", rtf);

            // And it is readable back: a save followed by a load is the whole point of the property.
            using var reloaded = Rich ();
            reloaded.Rtf = rtf;
            Assert.Equal ("a\nb", reloaded.Text);
        }

        [Fact]
        public void Rtf_includes_what_the_user_typed ()
        {
            // The P0 in one assertion: this used to return an empty document, so saving a note the
            // user had just written stored nothing.
            using var box = Rich ();

            Type (box, "typed");

            Assert.Contains ("typed", box.Rtf);
        }

        [Fact]
        public void Rtf_round_trips_non_ascii_and_the_escaped_characters ()
        {
            using var box = Rich (@"Café {braces} and \ back");

            var rtf = box.Rtf;
            using var reloaded = Rich ();
            reloaded.Rtf = rtf;

            Assert.Equal (box.Text, reloaded.Text);
        }

        // ---------------- TXT-15: the reader keeps text, paragraphs and escapes

        [Fact]
        public void Reading_rtf_keeps_paragraphs_grouped_runs_and_escapes ()
        {
            // The finding's own document. Every part of it used to be lost: \par vanished, \'e9 came
            // through as the literal "'e9", and `{\b Bold}` disappeared because it sits at depth 2.
            using var box = Rich ();

            box.Rtf = @"{\rtf1\ansi{\fonttbl{\f0 Arial;}}\f0 Caf\'e9\par {\b Bold} line\par}";

            Assert.Equal ("Café\nBold line", box.Text);
        }

        [Fact]
        public void Reading_rtf_skips_metadata_groups ()
        {
            // GUARD, not proof: the old reader dropped these too, by keeping only depth-1 text -- it
            // just dropped the document's real content with them. This pins that the new reader, which
            // keeps text at every depth, still knows which groups are metadata.
            using var box = Rich ();

            box.Rtf = @"{\rtf1\ansi{\fonttbl{\f0\fnil Segoe UI;}}{\colortbl ;\red255\green0\blue0;}"
                    + @"{\*\generator Riched20 10.0;}{\info{\title Secret}}Visible text}";

            Assert.Equal ("Visible text", box.Text);
        }

        [Fact]
        public void Reading_rtf_maps_tabs_and_unicode_escapes ()
        {
            using var box = Rich ();

            box.Rtf = @"{\rtf1\ansi one\tab two\par caf\u233? end}";

            Assert.Equal ("one\ttwo\ncafé end", box.Text);
        }

        // ---------------- TXT-16: the file overloads mean RTF

        [Fact]
        public void LoadFile_and_SaveFile_default_to_rich_text ()
        {
            var path = Path.Combine (Path.GetTempPath (), Path.GetRandomFileName () + ".rtf");
            var saved = Path.Combine (Path.GetTempPath (), Path.GetRandomFileName () + ".rtf");

            try {
                File.WriteAllText (path, @"{\rtf1 Hello}");

                using var box = Rich ();
                box.LoadFile (path);

                // It used to show the markup itself, because both overloads meant plain text.
                Assert.Equal ("Hello", box.Text);

                box.SaveFile (saved);
                Assert.StartsWith (@"{\rtf1", File.ReadAllText (saved));
            } finally {
                File.Delete (path);
                File.Delete (saved);
            }
        }

        [Fact]
        public void Plain_text_is_still_available_by_asking_for_it ()
        {
            // GUARD, not proof: this was the old default, so it passed before. It pins that changing
            // the default to RichText left the explicit plain-text path alone.
            var path = Path.Combine (Path.GetTempPath (), Path.GetRandomFileName () + ".txt");

            try {
                using var box = Rich ("just text");
                box.SaveFile (path, RichTextBoxStreamType.PlainText);

                Assert.Equal ("just text", File.ReadAllText (path));
            } finally {
                File.Delete (path);
            }
        }

        [Fact]
        public void The_stream_overloads_round_trip_rich_text ()
        {
            using var box = Rich ("line one\nline two");
            using var stream = new MemoryStream ();

            box.SaveFile (stream);
            stream.Position = 0;

            using var reloaded = Rich ();
            reloaded.LoadFile (stream);

            Assert.Equal (box.Text, reloaded.Text);
        }

        // ---------------- TXT-14: Find

        [Fact]
        public void Find_ignores_case_by_default_and_selects_the_hit ()
        {
            using var box = Rich ("Hello hello");

            var at = box.Find ("HELLO");

            Assert.Equal (0, at);

            // Selecting it is what makes the standard highlight loop work: the SelectionColor
            // assignment that follows a Find has to have something to colour.
            Assert.Equal (0, box.SelectionStart);
            Assert.Equal (5, box.SelectionLength);
        }

        [Fact]
        public void Find_with_MatchCase_respects_case ()
        {
            using var box = Rich ("hello world");

            // The PAIR is what discriminates. Asserting only that MatchCase misses a differently-cased
            // hit passes against the old Ordinal search too, which missed it whatever the options said.
            Assert.Equal (0, box.Find ("HELLO"));
            Assert.Equal (-1, box.Find ("HELLO", RichTextBoxFinds.MatchCase));
            Assert.Equal (0, box.Find ("hello", RichTextBoxFinds.MatchCase));
        }

        [Fact]
        public void Find_treats_an_end_of_minus_one_as_the_end_of_the_text ()
        {
            // The documented whole-text form, which used to produce an empty range and never match.
            using var box = Rich ("Hello hello");

            Assert.Equal (6, box.Find ("hello", 1, -1, RichTextBoxFinds.None));
        }

        [Fact]
        public void Find_with_NoHighlight_leaves_the_selection_alone ()
        {
            using var box = Rich ("Hello hello");
            box.Select (2, 0);

            var at = box.Find ("hello", RichTextBoxFinds.NoHighlight);

            Assert.Equal (0, at);
            Assert.Equal (2, box.SelectionStart);
            Assert.Equal (0, box.SelectionLength);
        }

        [Fact]
        public void Find_with_WholeWord_skips_a_match_inside_a_word ()
        {
            using var box = Rich ("concatenate cat");

            Assert.Equal (12, box.Find ("cat", RichTextBoxFinds.WholeWord));
            Assert.Equal (3, box.Find ("cat", RichTextBoxFinds.None));
        }

        [Fact]
        public void Find_with_Reverse_returns_the_last_match ()
        {
            using var box = Rich ("cat dog cat");

            Assert.Equal (8, box.Find ("cat", RichTextBoxFinds.Reverse));
        }

        [Theory]
        [InlineData (-1, 0)]
        [InlineData (99, -1)]
        [InlineData (0, -2)]
        [InlineData (5, 2)]
        public void Find_rejects_a_range_it_cannot_search (int start, int end)
        {
            // Upstream throws rather than returning -1, and a caller with a bad range wants to know:
            // silently finding nothing is how an off-by-one turns into a mystery. Two of these four
            // rows threw before this change as well, but incidentally -- out of Substring, not out of
            // an argument check -- and the other two returned -1 without complaint.
            using var box = Rich ("Hello hello");

            Assert.Throws<ArgumentOutOfRangeException> (() => box.Find ("hello", start, end, RichTextBoxFinds.None));
        }

        // ---------------- TXT-17: the Selection* family

        [Fact]
        public void SelectionColor_colours_appended_text_and_reads_back_per_run ()
        {
            // The finding's own test, and the canonical coloured-log idiom.
            using var box = Rich ();

            box.SelectionColor = Color.Red;
            box.AppendText ("x");
            box.SelectionColor = Color.Blue;
            box.AppendText ("y");

            box.Select (0, 1);
            Assert.Equal (Color.Red, box.SelectionColor);

            box.Select (1, 1);
            Assert.Equal (Color.Blue, box.SelectionColor);
        }

        [Fact]
        public void A_coloured_run_is_actually_painted ()
        {
            // State without paint is what the whole plan is about, so one test goes to the pixels.
            // Asserted as a relationship between the two runs rather than against a colour value: the
            // first character's box has red-dominant pixels and the second's has none.
            using var box = Rich ();
            box.Font = new Majorsilence.Forms.Drawing.Font ("Arial", 24);

            box.SelectionColor = Color.Red;
            box.AppendText ("A");
            box.SelectionColor = Color.Empty;   // back to the control's own colour
            box.SelectionBold = false;
            box.AppendText ("B");

            using var bitmap = PaintSurface.Render (box);

            var first = RedPixels (bitmap, box.GetPositionFromCharIndex (0), box.GetPositionFromCharIndex (1));
            var second = RedPixels (bitmap, box.GetPositionFromCharIndex (1), box.GetPositionFromCharIndex (2));

            Assert.True (first > 0, "the red run painted no red pixels");
            Assert.Equal (0, second);
        }

        private static int RedPixels (SKBitmap bitmap, Point from, Point to)
        {
            var left = Math.Max (0, Math.Min (from.X, to.X));
            var right = Math.Min (bitmap.Width, Math.Max (from.X, to.X));
            var count = 0;

            for (var x = left; x < right; x++)
                for (var y = Math.Max (0, from.Y); y < Math.Min (bitmap.Height, from.Y + 32); y++) {
                    var p = bitmap.GetPixel (x, y);

                    if (p.Red > 128 && p.Green < 90 && p.Blue < 90)
                        count++;
                }

            return count;
        }

        [Fact]
        public void Applying_bold_across_two_coloured_runs_keeps_both_colours ()
        {
            using var box = Rich ();
            box.SelectionColor = Color.Red;
            box.AppendText ("ab");
            box.SelectionColor = Color.Blue;
            box.AppendText ("cd");

            box.Select (0, 4);
            box.SelectionBold = true;

            box.Select (0, 1);
            Assert.Equal (Color.Red, box.SelectionColor);
            Assert.True (box.SelectionBold);

            box.Select (3, 1);
            Assert.Equal (Color.Blue, box.SelectionColor);
            Assert.True (box.SelectionBold);
        }

        [Fact]
        public void Typed_text_takes_the_insertion_point_format ()
        {
            using var box = Rich ();

            box.SelectionColor = Color.Green;
            Type (box, "hi");

            box.Select (0, 2);
            Assert.Equal (Color.Green, box.SelectionColor);
        }

        [Fact]
        public void Deleting_before_a_run_keeps_the_run_over_the_same_characters ()
        {
            using var box = Rich ();
            box.AppendText ("xx");
            box.SelectionColor = Color.Red;
            box.AppendText ("ab");

            // Caret is after "ab"; put it after the "xx" and backspace one character away.
            box.Select (2, 0);
            box.RaiseKeyDown (new KeyEventArgs (Keys.Back));

            Assert.Equal ("xab", box.Text);

            // "a" is now at index 1, and the red run has to have moved with it.
            box.Select (1, 1);
            Assert.Equal (Color.Red, box.SelectionColor);

            box.Select (0, 1);
            Assert.Equal (box.ForeColor, box.SelectionColor);
        }

        [Fact]
        public void Assigning_Text_drops_the_formatting ()
        {
            // GUARD, not proof: with no formatting at all there was nothing to drop. It pins that a
            // whole-document assignment clears the runs, which would otherwise repaint the new text in
            // the old document's colours.
            using var box = Rich ();
            box.SelectionColor = Color.Red;
            box.AppendText ("red");

            box.Text = "replaced";

            box.Select (0, 3);
            Assert.Equal (box.ForeColor, box.SelectionColor);
        }

        [Fact]
        public void SelectionFont_carries_the_style_flags_and_not_the_family ()
        {
            using var box = Rich ("abc");
            box.Select (0, 3);

            box.SelectionFont = new Majorsilence.Forms.Drawing.Font ("Courier New", 30, Majorsilence.Forms.Drawing.FontStyle.Italic);

            Assert.True (box.SelectionItalic);
            Assert.False (box.SelectionBold);

            // Per-run family and size are deliberately not supported; the getter reports the
            // control's own, which is what actually gets painted.
            Assert.Equal (box.Font.Name, box.SelectionFont!.Name);
        }

        [Fact]
        public void SelectionBackColor_reports_no_colour_until_one_is_set ()
        {
            using var box = Rich ("abc");
            box.Select (0, 3);

            Assert.Equal (Color.Empty, box.SelectionBackColor);

            box.SelectionBackColor = Color.Yellow;

            Assert.Equal (Color.Yellow, box.SelectionBackColor);
        }
    }
}
