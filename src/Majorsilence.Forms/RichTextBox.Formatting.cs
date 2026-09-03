using System;
using System.Collections.Generic;
using System.Drawing;
using SkiaSharp;

namespace Majorsilence.Forms
{
    public partial class RichTextBox
    {
        // Character formatting for the Selection* family (finding TXT-17). The six properties were
        // plain auto-properties, so coloured log output -- SelectionColor = Red; AppendText ("ERROR
        // ...") -- painted in one colour, highlighted search hits were not highlighted, and the
        // getters lied twice over: they returned the last value assigned rather than the format under
        // the caret.
        //
        // The model is a list of non-overlapping runs, plus a PENDING format for the insertion point,
        // which is what upstream applies to text typed or appended next. Runs are painted through the
        // existing TextBox.Colorizer hook, so nothing new was needed in the layout path.
        //
        // What is NOT tracked: an edit this class does not see. Typing, Backspace/Delete and
        // AppendText all route through overridable seams and shift the runs with them, and assigning
        // Text drops them, but Undo, Paste and a programmatic SelectedText assignment move text
        // without telling this list, so runs after such an edit can end up over the wrong characters.
        // Getting that right means the document owning the formatting, which is a larger change than
        // this finding; the common case -- append, colour, append -- is exact.

        private readonly List<FormatRun> runs = [];
        private CharFormat pending;

        // A run's format. Every field is optional: an unset one means "whatever the control uses",
        // which is what makes SelectionBold = true leave the colour alone.
        private struct CharFormat
        {
            internal Color? ForeColor;
            internal Color? BackColor;
            internal bool? Bold;
            internal bool? Italic;
            internal bool? Underline;

            internal bool IsEmpty => ForeColor is null && BackColor is null
                                  && Bold is null && Italic is null && Underline is null;

            internal bool Matches (CharFormat other)
                => ForeColor == other.ForeColor && BackColor == other.BackColor
                && Bold == other.Bold && Italic == other.Italic && Underline == other.Underline;
        }

        private struct FormatRun
        {
            internal int Start;
            internal int Length;
            internal CharFormat Format;

            internal int End => Start + Length;
        }

        // ---------------------------------------------------------------------------------------
        // Reading and writing the format of the current selection
        // ---------------------------------------------------------------------------------------

        private CharFormat FormatAt (int index)
        {
            foreach (var run in runs)
                if (index >= run.Start && index < run.End)
                    return run.Format;

            // With nothing selected the insertion point's format is the pending one -- the format the
            // next character typed there would take, which is what upstream's getter reports.
            return SelectionLength == 0 && index == SelectionStart ? pending : default;
        }

        private void SetFormat (Func<CharFormat, CharFormat> mutate)
        {
            if (SelectionLength <= 0) {
                pending = mutate (FormatAt (SelectionStart));
                return;
            }

            ApplyFormat (SelectionStart, SelectionLength, mutate);
            EnsurePainted ();
            Invalidate ();
        }

        private void ApplyFormat (int start, int length, Func<CharFormat, CharFormat> mutate)
        {
            var end = start + length;
            var rebuilt = new List<FormatRun> (runs.Count + 2);

            // Everything outside the range survives untouched; anything overlapping is split so the
            // covered part can take the new format.
            foreach (var run in runs) {
                if (run.End <= start || run.Start >= end) {
                    rebuilt.Add (run);
                    continue;
                }

                if (run.Start < start)
                    rebuilt.Add (new FormatRun { Start = run.Start, Length = start - run.Start, Format = run.Format });

                if (run.End > end)
                    rebuilt.Add (new FormatRun { Start = end, Length = run.End - end, Format = run.Format });
            }

            // The range is then laid down piece by piece, each piece carrying the mutation applied to
            // whatever format was there before -- so SelectionBold over two differently coloured runs
            // makes both bold and keeps both colours.
            var position = start;

            while (position < end) {
                var covering = FindRun (position);
                var piece_end = covering.HasValue ? Math.Min (end, covering.Value.End) : NextBoundary (position, end);
                var basis = covering?.Format ?? default;

                rebuilt.Add (new FormatRun { Start = position, Length = piece_end - position, Format = mutate (basis) });
                position = piece_end;
            }

            runs.Clear ();
            runs.AddRange (rebuilt);
            Normalise ();
        }

        private FormatRun? FindRun (int index)
        {
            foreach (var run in runs)
                if (index >= run.Start && index < run.End)
                    return run;

            return null;
        }

        // The next position at which the format could change, so an unformatted gap is written as one
        // piece rather than one per character.
        private int NextBoundary (int from, int limit)
        {
            var boundary = limit;

            foreach (var run in runs)
                if (run.Start > from && run.Start < boundary)
                    boundary = run.Start;

            return boundary;
        }

        private void Normalise ()
        {
            runs.RemoveAll (r => r.Length <= 0 || r.Format.IsEmpty);
            runs.Sort ((a, b) => a.Start.CompareTo (b.Start));

            for (var i = runs.Count - 1; i > 0; i--) {
                if (runs[i - 1].End != runs[i].Start || !runs[i - 1].Format.Matches (runs[i].Format))
                    continue;

                var merged = runs[i - 1];
                merged.Length += runs[i].Length;
                runs[i - 1] = merged;
                runs.RemoveAt (i);
            }
        }

        // ---------------------------------------------------------------------------------------
        // Keeping the runs over the right characters
        // ---------------------------------------------------------------------------------------

        /// <summary>
        /// Records an edit: <paramref name="removed"/> characters gone from <paramref name="start"/>,
        /// <paramref name="inserted"/> put in their place, which takes the pending format.
        /// </summary>
        private void NoteEdit (int start, int removed, int inserted)
        {
            if (removed > 0) {
                ApplyFormat (start, removed, _ => default);   // the removed text's formatting goes with it
                Shift (start + removed, -removed);
            }

            if (inserted > 0) {
                Shift (start, inserted);

                if (!pending.IsEmpty) {
                    ApplyFormat (start, inserted, _ => pending);
                    EnsurePainted ();
                }
            }

            Normalise ();
        }

        private void Shift (int from, int delta)
        {
            for (var i = 0; i < runs.Count; i++) {
                var run = runs[i];

                if (run.End <= from)
                    continue;

                if (run.Start >= from) {
                    run.Start += delta;
                } else {
                    // The edit landed inside this run, so the run grows or shrinks around it.
                    run.Length += delta;
                }

                if (run.Start < 0) {
                    run.Length += run.Start;
                    run.Start = 0;
                }

                runs[i] = run;
            }

            runs.RemoveAll (r => r.Length <= 0);
        }

        // The Colorizer hook is only attached once something is actually formatted: with it set, every
        // paint builds its own TextBlock instead of using TextMeasurer's shared cache, and an ordinary
        // RichTextBox with no formatting should not pay for that.
        private void EnsurePainted ()
        {
            if (runs.Count > 0 && Colorizer is null)
                Colorizer = ComputeSpans;
        }

        private IEnumerable<TextSpanStyle> ComputeSpans (string text)
        {
            var fallback = GetEffectiveForegroundColor ();

            foreach (var run in runs) {
                if (run.Start >= text.Length)
                    continue;

                var length = Math.Min (run.Length, text.Length - run.Start);

                if (length <= 0)
                    continue;

                yield return new TextSpanStyle (
                    run.Start,
                    length,
                    run.Format.ForeColor is { } fore ? fore.ToSKColor () : fallback,
                    run.Format.Bold ?? false,
                    run.Format.Underline ?? false,
                    run.Format.Italic ?? false,
                    run.Format.BackColor is { } back ? back.ToSKColor () : default (SKColor));
            }
        }

        // ---------------------------------------------------------------------------------------
        // The seams the edits arrive through
        // ---------------------------------------------------------------------------------------

        /// <inheritdoc/>
        /// <remarks>Assigning the whole text drops the formatting with it: the runs described the old
        /// characters, and keeping them would paint the new ones in the old document's colours.</remarks>
        public override string Text {
            get => base.Text;
            set {
                runs.Clear ();
                pending = default;
                base.Text = value;
            }
        }

        /// <inheritdoc/>
        /// <remarks>Appended text takes the current insertion-point format, as upstream's
        /// <c>AppendText</c> does -- that is what makes the coloured-log idiom work.</remarks>
        public override void AppendText (string text)
        {
            var start = TextLength;

            base.AppendText (text);

            NoteEdit (start, 0, TextLength - start);
        }

        /// <inheritdoc/>
        protected override bool InsertTypedCharacter (KeyPressEventArgs e)
        {
            // Captured before, because the insert replaces any selection and moves the caret.
            var start = SelectionStart;
            var replaced = SelectionLength;
            var before = TextLength;

            if (!base.InsertTypedCharacter (e))
                return false;

            NoteEdit (start, replaced, TextLength - before + replaced);

            return true;
        }

        /// <inheritdoc/>
        protected override bool DeleteAtCaret (bool forward, bool wholeWord)
        {
            var caret = SelectionStart;
            var selected = SelectionLength;
            var before = TextLength;

            if (!base.DeleteAtCaret (forward, wholeWord))
                return false;

            var removed = before - TextLength;

            // A backwards delete takes the characters BEFORE the caret, so the run edit starts there.
            var start = selected > 0 || forward ? caret : Math.Max (0, caret - removed);

            NoteEdit (start, removed, 0);

            return true;
        }
    }
}
