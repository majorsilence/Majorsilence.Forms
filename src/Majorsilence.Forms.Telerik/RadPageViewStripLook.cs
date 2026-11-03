using System;
using System.Drawing;
using System.Linq;
using SkiaSharp;

namespace Majorsilence.Forms.Telerik
{
    // RadPageView's strip, laid out and painted as Telerik's is under its default theme. Measured on UI for WinForms
    // 2026.2.520 (.NET 10), in logical pixels:
    //   - the strip is #BFDBFF to the control's edges, its padding 5 left and top, 6 right and bottom; the tabs start
    //     2px into it, at (7, 5);
    //   - a tab is Font.Height + 13 high, and as wide as GDI+ measures its caption (the advance width and half an em),
    //     rounded up, plus 12: 82 x 28 for "Identification" in the default Segoe UI 8.25pt, 111 x 32 in Microsoft Sans
    //     Serif 12pt; an ItemSize the designer recorded is not read back; EqualWidth gives each the widest one's
    //     width, and ItemFitMode Fill shares out the room up to the buttons (495 in a 562px view, 549 without
    //     buttons), equally with EqualWidth, else as each tab's own width and an equal part of what is left;
    //   - the caption is #15428B, in the page view's Font when it was set and in Segoe UI 8.25pt otherwise (the
    //     pages keep the form's font);
    //   - the selected tab is framed in #6E99D2 with rounded top corners and filled top to bottom #EBF2FA to #E9F0F9
    //     over the first 15%, then #D8E6F9 to #E9F0F9, and runs down over the content's top edge; the others are
    //     the strip;
    //   - StripButtons (Auto by default: scroll back, scroll forward and close) sit in a 54px panel at the strip's
    //     end, 16px each, 1px apart, 2px in, centred down the row; the scroll arrows are pale #DCE8FF triangles while
    //     nothing overflows and #15428B once the tabs do, the close button a #15428B cross, and pressing it closes the
    //     selected page;
    //   - the content starts a pixel above the row's bottom, framed in #6E99D2 with rounded corners and a #D4E5FC
    //     line outside its right and bottom edges, #E9F0F9 inside; the page is inset 5 from its edges;
    //   - disabled, the same strip in greys: #CBCBCB, the selected tab no different from the others, captions and
    //     the close cross #7B7B7B, arrows #D5D5D5, the content frame #A7A7A7 with a #D2D2D2 line outside, #D9D9D9
    //     inside.
    internal sealed class RadPageViewStripLook : TabStripLook
    {
        private readonly RadPageView view;

        internal RadPageViewStripLook (RadPageView view) => this.view = view;

        private static readonly Majorsilence.Forms.Drawing.Font ThemeFont = new Majorsilence.Forms.Drawing.Font ("Segoe UI", 8.25f);

        private sealed class Palette
        {
            public SKColor Strip, ContentFill, Border, Shadow, Text, Arrow, Close;
            public SKColor[]? Selected;
        }

        private static readonly Palette Enabled = new Palette {
            Strip = new SKColor (0xBF, 0xDB, 0xFF), ContentFill = new SKColor (0xE9, 0xF0, 0xF9), Border = new SKColor (0x6E, 0x99, 0xD2),
            Shadow = new SKColor (0xD4, 0xE5, 0xFC), Text = new SKColor (0x15, 0x42, 0x8B), Arrow = new SKColor (0xDC, 0xE8, 0xFF),
            Close = new SKColor (0x15, 0x42, 0x8B),
            Selected = new[] { new SKColor (0xEB, 0xF2, 0xFA), new SKColor (0xE9, 0xF0, 0xF9), new SKColor (0xD8, 0xE6, 0xF9), new SKColor (0xE9, 0xF0, 0xF9) },
        };

        private static readonly Palette Disabled = new Palette {
            Strip = new SKColor (0xCB, 0xCB, 0xCB), ContentFill = new SKColor (0xD9, 0xD9, 0xD9), Border = new SKColor (0xA7, 0xA7, 0xA7),
            Shadow = new SKColor (0xD2, 0xD2, 0xD2), Text = new SKColor (0x7B, 0x7B, 0x7B), Arrow = new SKColor (0xD5, 0xD5, 0xD5),
            Close = new SKColor (0x7B, 0x7B, 0x7B),
        };

        // The arrows once the tabs overflow and they scroll them: the close cross's colour.
        private static readonly SKColor ActiveArrow = new SKColor (0x15, 0x42, 0x8B);

        internal Majorsilence.Forms.Drawing.Font CaptionFont => view.HasOwnFont ? view.Font : ThemeFont;

        private int Width => view.DeviceToLogicalUnits (view.DeviceClientRectangle.Width);

        private int Height => view.DeviceToLogicalUnits (view.DeviceClientRectangle.Height);

        private StripViewButtons Buttons => view.GetChildAt (0).StripButtons;

        private bool ShowsScroll => Buttons.HasFlag (StripViewButtons.Auto) || Buttons.HasFlag (StripViewButtons.Scroll);

        private bool ShowsClose => Buttons.HasFlag (StripViewButtons.Auto) || Buttons.HasFlag (StripViewButtons.Close);

        // The buttons panel: 2px padding round 16px buttons 1px apart.
        private int ButtonCount => (ShowsScroll ? 2 : 0) + (ShowsClose ? 1 : 0);

        private int PanelWidth => ButtonCount == 0 ? 0 : 4 + 16 * ButtonCount + (ButtonCount - 1);

        public override Point TabOrigin => new Point (7, 5);

        public override int RowHeight => CaptionFont.Height + 13;

        public override int TabWidth (Majorsilence.Forms.TabStripItem tab)
        {
            var tabs = view.TabStrip.Tabs;
            var equal = view.ItemSizeMode == PageViewItemSizeMode.EqualWidth;

            // Fill: the tabs share the room up to the buttons, equally with EqualWidth, else each its own width and an
            // equal part of what is left over.
            if ((view.GetChildAt (0).ItemFitMode & StripViewItemFitMode.Fill) != 0 && tabs.Count > 0) {
                var room = Math.Max (0, Width - TabOrigin.X - TrailingWidth);

                if (equal)
                    return room / tabs.Count;

                var slack = room - tabs.Sum (NaturalWidth);

                return NaturalWidth (tab) + Math.Max (0, slack / tabs.Count);
            }

            return equal ? tabs.Select (NaturalWidth).DefaultIfEmpty (0).Max () : NaturalWidth (tab);
        }

        // As GDI+ measures the caption by default -- its advance width and half an em -- rounded up, plus the tab's 4px
        // padding, 1px border and 1px inner border each side.
        private int NaturalWidth (Majorsilence.Forms.TabStripItem tab)
        {
            var font = CaptionFont;
            var em = PixelSizeOf (font);
            var advance = string.IsNullOrEmpty (tab.Text) ? 0 : TextMeasurer.MeasureText (tab.Text, TypefaceOf (font), (int) Math.Round (em)).Width;

            return (int) Math.Ceiling (advance + em / 2) + 12;
        }

        public override int TrailingWidth => 6 + PanelWidth;

        public override int StripHeight => TabOrigin.Y + RowHeight;

        // The content: from a pixel above the row's bottom to the strip's padding.
        private Rectangle Content {
            get {
                var top = TabOrigin.Y + RowHeight - 1;
                return new Rectangle (5, top, Math.Max (0, Width - 11), Math.Max (0, Height - top - 6));
            }
        }

        public override Rectangle PageBounds => Rectangle.Inflate (Content, -5, -5) is var page && page.Width > 0 && page.Height > 0 ? page : new Rectangle (Content.Location, Size.Empty);

        // A button's square, in order back, forward, close.
        private Rectangle Button (int slot)
            => new Rectangle (Width - 6 - PanelWidth + 2 + 17 * slot, TabOrigin.Y + (RowHeight - 16) / 2, 16, 16);

        public override Rectangle ScrollButtons => ShowsScroll ? Rectangle.Union (Button (0), Button (1)) : Rectangle.Empty;

        private Rectangle CloseButton => ShowsClose ? Button (ShowsScroll ? 2 : 0) : Rectangle.Empty;

        public override bool OnStripPress (Point location)
        {
            if (ShowsScroll && ScrollButtons.Contains (location))
                return !view.TabStrip.TabsOverflow;   // the strip scrolls an overflowing row itself

            if (!CloseButton.Contains (location))
                return false;

            // Through the collection, which asks PageRemoving first.
            if (view.SelectedTabPage is { } page)
                view.TabPages.Remove (page);

            return true;
        }

        public override void Paint (PaintEventArgs e)
        {
            var palette = view.Enabled ? Enabled : Disabled;
            var canvas = e.Canvas;
            float D (float logical) => logical * (float) e.Scaling;
            var one = D (1);

            using var paint = new SKPaint { IsAntialias = true };

            paint.Color = palette.Strip;
            canvas.DrawRect (new SKRect (0, 0, D (Width), D (Height)), paint);

            // The content: its outside line, then its fill and frame over it.
            var content = Content;
            var frame = new SKRect (D (content.Left) + one / 2, D (content.Top) + one / 2, D (content.Right) - one / 2, D (content.Bottom) - one / 2);
            var radius = D (2);

            paint.Style = SKPaintStyle.Stroke;
            paint.StrokeWidth = one;
            paint.Color = palette.Shadow;
            canvas.DrawRoundRect (new SKRect (frame.Left + one, frame.Top + one, frame.Right + one, frame.Bottom + one), radius, radius, paint);

            paint.Style = SKPaintStyle.Fill;
            paint.Color = palette.ContentFill;
            canvas.DrawRoundRect (frame, radius, radius, paint);

            paint.Style = SKPaintStyle.Stroke;
            paint.Color = palette.Border;
            canvas.DrawRoundRect (frame, radius, radius, paint);

            var strip = view.TabStrip;
            var clip = new SKRect (0, 0, D (Width - TrailingWidth), D (Height));

            canvas.Save ();
            canvas.ClipRect (clip);

            foreach (var tab in strip.Tabs) {
                var bounds = new Rectangle (strip.Left + tab.Bounds.Left, strip.Top + tab.Bounds.Top, tab.Bounds.Width, tab.Bounds.Height);

                if (tab.Selected && palette.Selected is { } fill)
                    PaintSelectedTab (canvas, paint, bounds, fill, palette.Border, D);

                PaintCaption (e, tab, bounds, tab.Enabled ? palette.Text : Disabled.Text);
            }

            canvas.Restore ();

            if (ShowsScroll) {
                var arrows = view.Enabled && strip.TabsOverflow ? ActiveArrow : palette.Arrow;
                PaintArrow (canvas, paint, Button (0), back: true, arrows, D);
                PaintArrow (canvas, paint, Button (1), back: false, arrows, D);
            }

            if (ShowsClose)
                PaintClose (canvas, paint, CloseButton, palette.Close, D);
        }

        // Where the selected tab's fill turns from its first pair of colours to its second: 15% down.
        private static readonly float[] SelectedStops = { 0f, 0.15f, 0.15f, 1f };

        // The selected tab: open at the bottom, where it runs into the content, its top corners rounded.
        private static void PaintSelectedTab (SKCanvas canvas, SKPaint paint, Rectangle bounds, SKColor[] fill, SKColor border, Func<float, float> D)
        {
            var one = D (1);
            var left = D (bounds.Left) + one / 2;
            var right = D (bounds.Right) - one / 2;
            var top = D (bounds.Top) + one / 2;
            var bottom = D (bounds.Bottom);
            var radius = D (2);

            using var path = new SKPath ();
            path.MoveTo (left, bottom);
            path.LineTo (left, top + radius);
            path.ArcTo (new SKRect (left, top, left + 2 * radius, top + 2 * radius), 180, 90, false);
            path.LineTo (right - radius, top);
            path.ArcTo (new SKRect (right - 2 * radius, top, right, top + 2 * radius), 270, 90, false);
            path.LineTo (right, bottom);

            var inside = new SKRect (D (bounds.Left) + one, D (bounds.Top) + one, D (bounds.Right) - one, bottom);

            using (var shader = SKShader.CreateLinearGradient (new SKPoint (0, inside.Top), new SKPoint (0, inside.Bottom), fill,
                       SelectedStops, SKShaderTileMode.Clamp)) {
                paint.Style = SKPaintStyle.Fill;
                paint.Shader = shader;
                canvas.Save ();
                canvas.ClipPath (path, antialias: true);
                canvas.DrawRect (inside, paint);
                canvas.Restore ();
                paint.Shader = null;
            }

            paint.Style = SKPaintStyle.Stroke;
            paint.StrokeWidth = one;
            paint.Color = border;
            canvas.DrawPath (path, paint);
        }

        // The caption, left-aligned and centred down the tab inside its padding.
        private void PaintCaption (PaintEventArgs e, Majorsilence.Forms.TabStripItem tab, Rectangle bounds, SKColor colour)
        {
            if (string.IsNullOrEmpty (tab.Text))
                return;

            var font = CaptionFont;
            var size = view.LogicalToDeviceUnits ((int) Math.Round (PixelSizeOf (font)));
            var text = new Rectangle (bounds.Left + 8, bounds.Top + 4, Math.Max (0, bounds.Width - 13), Math.Max (0, bounds.Height - 10));

            e.Canvas.DrawText (tab.Text, TypefaceOf (font), size, view.LogicalToDeviceUnits (text), colour, ContentAlignment.MiddleLeft, maxLines: 1);
        }

        // A 4 x 8 triangle pointing back or forward, in the middle of its button.
        private static void PaintArrow (SKCanvas canvas, SKPaint paint, Rectangle button, bool back, SKColor colour, Func<float, float> D)
        {
            var x = button.Left + 6f;
            var y = button.Top + 4f;

            using var path = new SKPath ();

            if (back) {
                path.MoveTo (D (x + 4), D (y));
                path.LineTo (D (x + 4), D (y + 8));
                path.LineTo (D (x), D (y + 4));
            } else {
                path.MoveTo (D (x), D (y));
                path.LineTo (D (x), D (y + 8));
                path.LineTo (D (x + 4), D (y + 4));
            }

            path.Close ();
            paint.Style = SKPaintStyle.Fill;
            paint.Color = colour;
            canvas.DrawPath (path, paint);
        }

        // An 8 x 8 cross in the middle of its button.
        private static void PaintClose (SKCanvas canvas, SKPaint paint, Rectangle button, SKColor colour, Func<float, float> D)
        {
            var x = button.Left + 4.5f;
            var y = button.Top + 4.5f;

            paint.Style = SKPaintStyle.Stroke;
            paint.StrokeWidth = D (1.5f);
            paint.Color = colour;
            canvas.DrawLine (D (x), D (y), D (x + 7), D (y + 7), paint);
            canvas.DrawLine (D (x + 7), D (y), D (x), D (y + 7), paint);
        }
    }
}
