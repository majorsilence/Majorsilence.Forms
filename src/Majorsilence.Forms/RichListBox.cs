using System;
using System.Collections.Generic;
using System.Drawing;
using Majorsilence.Forms.Drawing;
using SkiaSharp;

namespace Majorsilence.Forms
{
    /// <summary>One line of text in a <see cref="RichListBox"/> row: its own size, weight and colour, wrapped to the row's width.</summary>
    public sealed class ListItemLine
    {
        /// <summary>Initializes a new instance of the <see cref="ListItemLine"/> class.</summary>
        public ListItemLine (string text)
        {
            Text = text ?? string.Empty;
        }

        /// <summary>The text. It wraps onto further lines at the row's width.</summary>
        public string Text { get; }

        /// <summary>The font size in logical pixels, or 0 for the list's own font size.</summary>
        public int FontSize { get; set; }

        /// <summary>Whether the text is bold.</summary>
        public bool Bold { get; set; }

        /// <summary>Whether the text is italic.</summary>
        public bool Italic { get; set; }

        /// <summary>Whether the text is drawn in the softer secondary colour, for dates and other detail.</summary>
        public bool Muted { get; set; }

        /// <summary>The most lines the text may take before it is cut off with an ellipsis, or 0 for no limit.</summary>
        public int MaxLines { get; set; }
    }

    /// <summary>
    /// A <see cref="ListBox"/> whose rows are built from a template: each item becomes a few lines of styled, wrapping text on a
    /// rounded card, so a list of posts or messages can show a title, a date and an excerpt per row. It keeps everything
    /// <see cref="ListBox"/> does -- selection, keyboard, mouse wheel, touch scrolling -- and only the rows differ.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Set <see cref="ItemTemplate"/> to turn an item into its lines. Rows are as tall as their wrapped text needs, and only the
    /// rows on screen are drawn. Without a template the list behaves as a plain <see cref="ListBox"/> would for owner drawing.
    /// </para>
    /// <para>
    /// Row heights are measured once per item and again when the width changes. If an item's text changes without the item count
    /// changing, call <see cref="ListBox.RefreshItems"/> to measure again.
    /// </para>
    /// </remarks>
    public class RichListBox : ListBox
    {
        // The scroll bar is not part of the width a row can use; this is a safe allowance for it in logical pixels.
        private const int ScrollBarAllowance = 18;

        // The gap between two lines of one row, in logical pixels.
        private const int LineGap = 2;

        private Func<object, IEnumerable<ListItemLine>>? item_template;
        private int corner_radius = 10;
        private int item_padding = 10;
        private int item_spacing = 6;
        private int measured_width = -1;

        /// <summary>Initializes a new instance of the <see cref="RichListBox"/> class.</summary>
        public RichListBox ()
        {
            DrawMode = DrawMode.OwnerDrawVariable;
            // Row heights are only consulted when a MeasureItem handler exists, so the control subscribes its own.
            MeasureItem += OnMeasureRow;
        }

        /// <summary>Gets or sets how an item becomes the lines shown in its row. Null shows each item's text on one line.</summary>
        public Func<object, IEnumerable<ListItemLine>>? ItemTemplate {
            get => item_template;
            set {
                item_template = value;
                RefreshItems ();
            }
        }

        /// <summary>Gets or sets the corner radius of a row's card in logical pixels. Defaults to 10.</summary>
        public int ItemCornerRadius {
            get => corner_radius;
            set {
                if (value < 0)
                    throw new ArgumentOutOfRangeException (nameof (value), "The corner radius cannot be negative.");
                corner_radius = value;
                Invalidate ();
            }
        }

        /// <summary>Gets or sets the space between a row's card edge and its text in logical pixels. Defaults to 10.</summary>
        public int ItemPadding {
            get => item_padding;
            set {
                if (value < 0)
                    throw new ArgumentOutOfRangeException (nameof (value), "The padding cannot be negative.");
                item_padding = value;
                RefreshItems ();
            }
        }

        /// <summary>Gets or sets the gap between one row's card and the next in logical pixels. Defaults to 6.</summary>
        public int ItemSpacing {
            get => item_spacing;
            set {
                if (value < 0)
                    throw new ArgumentOutOfRangeException (nameof (value), "The spacing cannot be negative.");
                item_spacing = value;
                RefreshItems ();
            }
        }

        /// <inheritdoc/>
        protected override void OnSizeChanged (EventArgs e)
        {
            base.OnSizeChanged (e);

            // Wrapped text re-flows when the width changes, so the heights measured at the old width are stale.
            if (ClientSize.Width != measured_width && measured_width >= 0)
                RefreshItems ();
        }

        // ── Layout shared by measuring and drawing ──────────────────────────────────────────────────

        private readonly struct Row
        {
            public Row (ListItemLine line, SKTypeface typeface, int fontSize, int height)
            {
                Line = line;
                Typeface = typeface;
                FontSize = fontSize;
                Height = height;
            }

            public ListItemLine Line { get; }
            public SKTypeface Typeface { get; }
            public int FontSize { get; }
            public int Height { get; }
        }

        private int TextWidth () => Math.Max (40, ClientSize.Width - ScrollBarAllowance - 2 * item_padding);

        private IEnumerable<ListItemLine> LinesFor (int index)
        {
            var item = Items[index];
            if (item is null)
                return Array.Empty<ListItemLine> ();

            return item_template is not null
                ? item_template (item)
                : new[] { new ListItemLine (GetItemText (item)) };
        }

        private SKTypeface TypefaceFor (ListItemLine line)
        {
            var style = (line.Bold ? Drawing.FontStyle.Bold : Drawing.FontStyle.Regular) | (line.Italic ? Drawing.FontStyle.Italic : Drawing.FontStyle.Regular);
            return new Drawing.Font (Font.FontFamily, 10, style).GetSKTypeface ();
        }

        // Lays the row's lines out at a logical text width: each line's typeface, size and wrapped height, in logical pixels.
        private List<Row> LayoutRow (int index, int textWidth)
        {
            var rows = new List<Row> ();
            var baseSize = GetEffectiveFontSize ();

            foreach (var line in LinesFor (index)) {
                if (line is null || line.Text.Length == 0)
                    continue;

                var size = line.FontSize > 0 ? line.FontSize : baseSize;
                var typeface = TypefaceFor (line);
                var natural = (int) Math.Ceiling (TextMeasurer.MeasureText (line.Text, typeface, size, new Size (textWidth, int.MaxValue)).Height);

                if (line.MaxLines > 0) {
                    var oneLine = (int) Math.Ceiling (TextMeasurer.MeasureText ("Ag", typeface, size, new Size (int.MaxValue, int.MaxValue)).Height);
                    natural = Math.Min (natural, oneLine * line.MaxLines);
                }

                rows.Add (new Row (line, typeface, size, Math.Max(1, natural)));
            }

            return rows;
        }

        private void OnMeasureRow (object? sender, MeasureItemEventArgs e)
        {
            measured_width = ClientSize.Width;

            var rows = LayoutRow (e.Index, TextWidth ());
            var text = 0;
            foreach (var row in rows)
                text += row.Height;
            if (rows.Count > 1)
                text += (rows.Count - 1) * LineGap;

            e.ItemHeight = Math.Max (ItemHeight, text + 2 * item_padding + item_spacing);
        }

        // ── Drawing (device pixels, like the library's own renderers) ───────────────────────────────

        /// <inheritdoc/>
        protected override void OnDrawItem (DrawItemEventArgs e)
        {
            var canvas = e.Graphics.Canvas;
            if (canvas is null || e.Index < 0 || e.Index >= Items.Count) {
                base.OnDrawItem (e);
                return;
            }

            var selected = (e.State & DrawItemState.Selected) != 0;
            var hot = (e.State & DrawItemState.HotLight) != 0;

            // The card: the row's bounds less the gap that separates it from the next one.
            var gap = LogicalToDeviceUnits (item_spacing);
            var card = new Rectangle (e.Bounds.Left + LogicalToDeviceUnits (2), e.Bounds.Top + gap / 2,
                                      e.Bounds.Width - LogicalToDeviceUnits (4), e.Bounds.Height - gap);

            var fill = selected ? Theme.ControlHighlightLowColor : hot ? Theme.ControlMidColor : Theme.ControlLowColor;
            var edge = selected ? Theme.AccentColor : Theme.ControlMidHighColor;
            var radius = LogicalToDeviceUnits (corner_radius);
            var rect = new SKRect (card.Left, card.Top, card.Right, card.Bottom);

            using (var paint = new SKPaint { IsAntialias = true, Style = SKPaintStyle.Fill, Color = fill })
                canvas.DrawRoundRect (rect, radius, radius, paint);
            using (var paint = new SKPaint { IsAntialias = true, Style = SKPaintStyle.Stroke, StrokeWidth = Math.Max (1, LogicalToDeviceUnits (selected ? 2 : 1)), Color = edge }) {
                rect.Inflate (-0.5f, -0.5f);
                canvas.DrawRoundRect (rect, radius, radius, paint);
            }

            // The text, laid out at the same logical width the height was measured at, then scaled to device pixels.
            var padding = LogicalToDeviceUnits (item_padding);
            var x = card.Left + padding;
            var width = card.Width - 2 * padding;
            var y = card.Top + padding;

            foreach (var row in LayoutRow (e.Index, TextWidth ())) {
                var height = LogicalToDeviceUnits (row.Height);
                var color = row.Line.Muted ? SoftForeground () : Theme.ForegroundColor;
                canvas.DrawText (row.Line.Text, row.Typeface, LogicalToDeviceUnits (row.FontSize), new Rectangle (x, y, width, height), color,
                                 ContentAlignment.TopLeft, maxLines: row.Line.MaxLines > 0 ? row.Line.MaxLines : null, ellipsis: row.Line.MaxLines > 0);
                y += height + LogicalToDeviceUnits (LineGap);
            }
        }

        private static SKColor SoftForeground ()
        {
            var c = Theme.ForegroundColor;
            return c.WithAlpha ((byte) (c.Alpha * 0.62));
        }
    }
}
