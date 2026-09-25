using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;

namespace Majorsilence.Forms
{
    // W6 mechanisms: the RichTextBox options that were stored and read by nothing. The character
    // formatting model (TXT-17) already carried colour, weight, slant and underline per run; these add
    // the baseline shift and the protected flag to it, and wire the input-level options -- URL
    // detection, the formatting shortcuts, word-granular drag selection, the selection margin -- and
    // the ContentsResized notification.
    public partial class RichTextBox
    {
        /// <summary>Gets or sets how far the selection is raised above the baseline.</summary>
        /// <remarks>Real as of W6 mechanisms: a positive value draws the run as superscript and a
        /// negative one as subscript. The size of the shift is the font's variant, not the value --
        /// there is one superscript size in the text pipeline, where upstream takes the offset in
        /// twips.</remarks>
        public int SelectionCharOffset {
            get => FormatAt (SelectionStart).CharOffset ?? 0;
            set => SetFormat (format => { format.CharOffset = value; return format; });
        }

        /// <summary>Gets or sets whether the selected text refuses to be edited.</summary>
        /// <remarks>Real as of W6 mechanisms: an edit that would change protected text is refused and
        /// raises <see cref="Protected"/>, as upstream's does.</remarks>
        public bool SelectionProtected {
            get => FormatAt (SelectionStart).Protected ?? false;
            set => SetFormat (format => { format.Protected = value; return format; });
        }

        /// <summary>Raised when an edit was refused because it would have changed protected text.</summary>
        public event EventHandler? Protected;

        /// <summary>Raises <see cref="Protected"/>.</summary>
        protected virtual void OnProtected (EventArgs e) => Protected?.Invoke (this, e);

        /// <summary>Whether any character in <paramref name="start"/>..<paramref name="start"/>+<paramref name="length"/> is protected.</summary>
        internal bool RangeIsProtected (int start, int length)
        {
            var end = start + Math.Max (length, 1);

            for (var i = start; i < end; i++)
                if (FormatAt (i).Protected == true)
                    return true;

            return false;
        }

        // An edit that would touch protected text is refused, and the refusal is announced once.
        private bool RefuseIfProtected (int start, int length)
        {
            if (!RangeIsProtected (start, length))
                return false;

            OnProtected (EventArgs.Empty);
            return true;
        }

        // ── URL detection ───────────────────────────────────────────────────────────────────────────

        /// <summary>The spans of text that look like links, when <see cref="DetectUrls"/> is on.</summary>
        /// <remarks>Recomputed from the text rather than stored, so it cannot drift from it.</remarks>
        internal IReadOnlyList<(int Start, int Length)> DetectedUrls {
            get {
                if (!DetectUrls || Text.Length == 0)
                    return [];

                var found = new List<(int, int)> ();

                foreach (System.Text.RegularExpressions.Match match in UrlPattern.Matches (Text))
                    found.Add ((match.Index, match.Length));

                return found;
            }
        }

        // The shapes upstream's autodetection recognises, minus the mail and file schemes it also takes:
        // a scheme-qualified URL, or a bare host that starts with www.
        private static readonly System.Text.RegularExpressions.Regex UrlPattern =
            new (@"(?:https?|ftp)://[^\s]+|www\.[^\s]+", System.Text.RegularExpressions.RegexOptions.IgnoreCase);

        /// <summary>The detected link the character at <paramref name="index"/> belongs to, or null.</summary>
        internal string? LinkAt (int index)
        {
            foreach (var (start, length) in DetectedUrls)
                if (index >= start && index < start + length)
                    return Text.Substring (start, length);

            return null;
        }

        // ── input ───────────────────────────────────────────────────────────────────────────────────

        /// <inheritdoc/>
        protected override void OnKeyDown (KeyEventArgs e)
        {
            // RichTextShortcutsEnabled (W6 mechanisms): the formatting shortcuts upstream's rich edit
            // takes. They run before the base so the box's own Ctrl handling does not claim them.
            if (RichTextShortcutsEnabled && e.Control && !e.Alt) {
                switch (e.KeyCode) {
                    case Keys.B:
                        SelectionBold = !SelectionBold;
                        e.Handled = true;
                        return;
                    case Keys.I:
                        SelectionItalic = !SelectionItalic;
                        e.Handled = true;
                        return;
                    case Keys.U:
                        SelectionUnderline = !SelectionUnderline;
                        e.Handled = true;
                        return;
                }
            }

            base.OnKeyDown (e);
        }

        /// <inheritdoc/>
        protected override void OnMouseDown (MouseEventArgs e)
        {
            // ShowSelectionMargin (W6 mechanisms): a click in the margin strip selects the whole line
            // under it rather than moving the caret, which is what the margin is for.
            // The margin width is device and the pointer logical (RC-8).
            var margin = DeviceToLogicalUnits (ScaledSelectionMargin);

            if (ShowSelectionMargin && e.Button == MouseButtons.Left && e.X < margin) {
                SelectLineAt (new Point (margin + 1, e.Y));
                return;
            }

            base.OnMouseDown (e);

            // A click on a detected URL reports it, as upstream's LinkClicked does (W6 mechanisms).
            if (e.Button == MouseButtons.Left && LinkAt (GetCharIndexFromPosition (e.Location)) is { } link)
                OnLinkClicked (new LinkClickedEventArgs (link));
        }

        /// <inheritdoc/>
        protected override void OnMouseMove (MouseEventArgs e)
        {
            base.OnMouseMove (e);

            // AutoWordSelection (W6 mechanisms): a drag extends by whole words rather than characters.
            if (AutoWordSelection && e.Button == MouseButtons.Left && SelectionLength > 0)
                ExtendSelectionToWords ();
        }

        // Grows the current selection outwards to word boundaries.
        private void ExtendSelectionToWords ()
        {
            var text = Text;

            if (text.Length == 0)
                return;

            var start = Math.Max (0, Math.Min (SelectionStart, text.Length));
            var end = Math.Max (start, Math.Min (SelectionStart + SelectionLength, text.Length));

            while (start > 0 && !char.IsWhiteSpace (text[start - 1]))
                start--;

            while (end < text.Length && !char.IsWhiteSpace (text[end]))
                end++;

            if (start == SelectionStart && end - start == SelectionLength)
                return;

            SelectionStart = start;
            SelectionLength = end - start;
        }

        // Selects the line the point falls on, for the selection margin.
        private void SelectLineAt (Point location)
        {
            var index = GetCharIndexFromPosition (location);
            var text = Text;

            if (text.Length == 0)
                return;

            var start = index;
            var end = Math.Min (index, text.Length - 1);

            while (start > 0 && text[start - 1] != '\n')
                start--;

            while (end < text.Length && text[end] != '\n')
                end++;

            // The line's newline goes with it, as selecting a line in a word processor does.
            if (end < text.Length)
                end++;

            SelectionStart = start;
            SelectionLength = end - start;
            Invalidate ();
        }

        /// <summary>The width the selection margin takes, in device pixels; zero when it is off.</summary>
        internal int ScaledSelectionMargin => ShowSelectionMargin ? LogicalToDeviceUnits (12) : 0;

        /// <inheritdoc/>
        /// <remarks>The text starts past the selection margin when one is shown (W6 mechanisms).</remarks>
        internal override Point TextOrigin
            => new Point (base.TextOrigin.X + ScaledSelectionMargin, base.TextOrigin.Y);

        // ── ContentsResized ─────────────────────────────────────────────────────────────────────────

        private Rectangle last_contents;

        /// <summary>
        /// Announces the laid-out size of the text when it changes (W6 mechanisms).
        /// </summary>
        /// <remarks>
        /// Checked as the control paints, which is the only moment the measured size is known: the
        /// text is laid out by the paint pipeline, not by the edit that changed it. Upstream reports
        /// the same rectangle -- the content's bounds, not the control's.
        /// </remarks>
        internal void CheckContentsResized ()
        {
            var block = document.GetTextBlock ();
            var measured = new Rectangle (0, 0, (int) Math.Ceiling (block.MeasuredWidth), (int) Math.Ceiling (block.MeasuredHeight));

            if (measured == last_contents)
                return;

            last_contents = measured;
            OnContentsResized (new ContentsResizedEventArgs (measured));
        }

        /// <summary>Raises <see cref="ContentsResized"/>.</summary>
        protected virtual void OnContentsResized (ContentsResizedEventArgs e) => ContentsResized?.Invoke (this, e);

        /// <inheritdoc/>
        protected override void OnPaint (PaintEventArgs e)
        {
            base.OnPaint (e);

            if (ShowSelectionMargin) {
                var margin = new Rectangle (PaddedClientRectangle.Left, PaddedClientRectangle.Top,
                    ScaledSelectionMargin, PaddedClientRectangle.Height);

                e.Canvas.FillRectangle (margin, Theme.ControlMidColor);
                e.Canvas.DrawLine (margin.Right - 1, margin.Top, margin.Right - 1, margin.Bottom, Theme.BorderLowColor);
            }

            CheckContentsResized ();
        }
    }
}
