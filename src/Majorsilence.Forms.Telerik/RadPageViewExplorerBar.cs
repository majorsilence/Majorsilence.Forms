using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using SkiaSharp;

namespace Majorsilence.Forms.Telerik
{
    // RadPageView's ExplorerBar mode, laid out and painted as Telerik's is under its default theme. Measured on UI for
    // WinForms 2026.2.520 (.NET 10), in logical pixels, a 271 x 843 page view:
    //   - a title band along the top, rows 0 to 24: the selected page's caption in Segoe UI 11.25pt bold, #15428B, from
    //     x 4, on a #E3EFFF to #B4D5FF fill inside a white line; the whole view framed in #6593CF;
    //   - under it a stack of one item per page, the LAST page at the top (Telerik stacks them in reverse), from row 25.
    //     The items are all as high as the highest (the default EqualHeight): 16 more than the 50px image, or than the
    //     caption's line plus one -- 66 with the image, 32 without; an ItemSize the designer recorded is not read. Each
    //     is framed in #6593CF and shares its bottom line with the next item, so they are one less than that apart (64);
    //     filled #E3EFFF to #C4DDFF over the top 40%, then #ADD1FF to #C0DBFF, or for the selected page #FFD9AA to
    //     #FFBB6E, then #FFAB3F to #FEE17A; the image at (5, 7) and the caption after it (or from x 5), in Segoe UI
    //     8.25pt bold, #204D89, centred down the item -- the page view's own Font is not used;
    //   - an expanded page (IsContentVisible) opens a 300px content area under its item, white whatever PageBackColor
    //     is, with the page in it 4 in from the sides and 3 from the lines above and below: (5, item + 68), as wide as
    //     the stack less 8, 292 high; the next item starts on the content's last row, so it is a row higher;
    //   - once the stack is higher than the room under the title band (the view's height less 26), a 17px scroll bar
    //     at (width - 18, 26) down to the bottom edge; the items and content then end before it. Expanding a page
    //     scrolls its item to the top, as far as the scroll bar goes; a wheel notch scrolls 15px;
    //   - clicking an item selects its page (SelectedPageChanged), then expands or collapses it
    //     (PageExpanding, PageExpanded or PageCollapsing, PageCollapsed); a double click expands it once; several pages
    //     can be expanded at once;
    //   - PageBackColor is the BackColor of every page that has none of its own, including pages added later.
    // Not done: the hover and pressed item fills, the expand animation, and Telerik's own scroll bar look (the core
    // scroll bar is used). A page scrolled up past the title band draws over it.
    public partial class RadPageView
    {
        // Telerik's default theme.
        private static readonly SKColor ExplorerBorder = new SKColor (0x65, 0x93, 0xCF);
        private static readonly SKColor[] ExplorerTitleFill = { new SKColor (0xE3, 0xEF, 0xFF), new SKColor (0xB4, 0xD5, 0xFF) };
        private static readonly SKColor ExplorerTitleText = new SKColor (0x15, 0x42, 0x8B);
        private static readonly SKColor[] ExplorerItemFill = {
            new SKColor (0xE3, 0xEF, 0xFF), new SKColor (0xC4, 0xDD, 0xFF), new SKColor (0xAD, 0xD1, 0xFF), new SKColor (0xC0, 0xDB, 0xFF) };
        private static readonly SKColor[] ExplorerSelectedFill = {
            new SKColor (0xFF, 0xD9, 0xAA), new SKColor (0xFF, 0xBB, 0x6E), new SKColor (0xFF, 0xAB, 0x3F), new SKColor (0xFE, 0xE1, 0x7A) };
        private static readonly float[] ExplorerItemStops = { 0f, 0.4f, 0.4f, 1f };
        private static readonly SKColor ExplorerItemText = new SKColor (0x20, 0x4D, 0x89);
        private static readonly SKColor ExplorerContentFill = SKColors.White;

        private static readonly Majorsilence.Forms.Drawing.Font ExplorerItemFont
            = new Majorsilence.Forms.Drawing.Font ("Segoe UI", 8.25f, Majorsilence.Forms.Drawing.FontStyle.Bold);
        private static readonly Majorsilence.Forms.Drawing.Font ExplorerTitleFont
            = new Majorsilence.Forms.Drawing.Font ("Segoe UI", 11.25f, Majorsilence.Forms.Drawing.FontStyle.Bold);

        // The stack starts on the title band's last row + 1; the scroll bar a row lower.
        internal const int ExplorerStackTop = 25;
        internal const int ExplorerScrollBarWidth = 17;

        // Telerik's FixedLength content: PageLength 300.
        internal const int ExplorerPageLength = 300;

        // A wheel notch: three of the scroll bar's 5px small changes.
        private const int ExplorerWheelStep = 15;

        /// <summary>Gets or sets how the page view presents its pages: a strip of tabs (<see cref="PageViewMode.Strip"/>, the
        /// default) or a stack of items that expand (<see cref="PageViewMode.ExplorerBar"/>).</summary>
        /// <remarks>The other modes show the strip.</remarks>
        public PageViewMode ViewMode {
            get => view_mode;
            set {
                if (view_mode == value)
                    return;

                view_mode = value;
                TabStripVisible = !IsExplorerBar;

                var selected = SelectedTabPage;

                foreach (var page in TabPages)
                    page.Visible = ShowsPage (page, selected);

                if (!IsExplorerBar && explorer_scroll is not null)
                    explorer_scroll.Visible = false;

                explorer_offset = 0;
                PerformLayout ();
                Invalidate ();
            }
        }

        private PageViewMode view_mode = PageViewMode.Strip;

        internal bool IsExplorerBar => view_mode == PageViewMode.ExplorerBar;

        internal override bool LaysOutPagesItself => IsExplorerBar;

        // In the ExplorerBar mode every expanded page shows, whichever is selected.
        internal override bool ShowsPage (TabPage page, TabPage? selected)
            => IsExplorerBar ? page is RadPageViewPage { IsContentVisible: true } : page == selected;

        /// <summary>Gets or sets the BackColor of the pages that have none of their own, pages added later included. Empty by
        /// default.</summary>
        public Color PageBackColor {
            get => page_back_color;
            set {
                page_back_color = value;

                foreach (var page in TabPages.OfType<RadPageViewPage> ())
                    page.ApplyViewBackColor (value);
            }
        }

        private Color page_back_color = Color.Empty;

        /// <inheritdoc/>
        protected override void OnControlAdded (ControlEventArgs e)
        {
            base.OnControlAdded (e);

            if (e.Control is RadPageViewPage page) {
                if (!page_back_color.IsEmpty)
                    page.ApplyViewBackColor (page_back_color);

                if (IsExplorerBar)
                    page.Visible = page.IsContentVisible;
            }
        }

        /// <summary>Raised before a page expands in the <see cref="PageViewMode.ExplorerBar"/> mode; cancel to keep it collapsed.</summary>
        public event EventHandler<RadPageViewCancelEventArgs>? PageExpanding;

        /// <summary>Raised after a page expands in the <see cref="PageViewMode.ExplorerBar"/> mode.</summary>
        public event EventHandler<RadPageViewEventArgs>? PageExpanded;

        /// <summary>Raised before a page collapses in the <see cref="PageViewMode.ExplorerBar"/> mode; cancel to keep it expanded.</summary>
        public event EventHandler<RadPageViewCancelEventArgs>? PageCollapsing;

        /// <summary>Raised after a page collapses in the <see cref="PageViewMode.ExplorerBar"/> mode.</summary>
        public event EventHandler<RadPageViewEventArgs>? PageCollapsed;

        // Expands or collapses a page, with Telerik's events around it; RadPageViewPage.IsContentVisible comes here in the
        // ExplorerBar mode.
        internal void SetContentVisible (RadPageViewPage page, bool visible)
        {
            var changing = new RadPageViewCancelEventArgs { Page = page };
            (visible ? PageExpanding : PageCollapsing)?.Invoke (this, changing);

            if (changing.Cancel)
                return;

            page.StoreContentVisible (visible);
            page.Visible = visible;

            // Its item to the top, as far as the scroll bar goes. Only once the view is up: the designer's expanded pages
            // leave the stack where it starts.
            if (visible && Created)
                explorer_offset = ExplorerSlotTop (page);

            PerformLayout ();
            Invalidate ();

            (visible ? PageExpanded : PageCollapsed)?.Invoke (this, new RadPageViewEventArgs (page));
        }

        /// <inheritdoc/>
        protected override void OnSelected (TabControlEventArgs e)
        {
            base.OnSelected (e);

            // The title band and the selected item's fill.
            if (IsExplorerBar)
                Invalidate ();
        }

        // ---- layout ----

        private VScrollBar? explorer_scroll;
        private int explorer_offset;

        // The items as last laid out, top to bottom: the item's frame and, when expanded, the content area, in this
        // control's logical coordinates (scrolled).
        private readonly List<(RadPageViewPage Page, Rectangle Item, Rectangle Content)> explorer_items = new ();

        private int LogicalWidth => DeviceToLogicalUnits (DeviceClientRectangle.Width);

        private int LogicalHeight => DeviceToLogicalUnits (DeviceClientRectangle.Height);

        // Telerik stacks the pages last first.
        private IEnumerable<RadPageViewPage> ExplorerOrder => TabPages.OfType<RadPageViewPage> ().Reverse ();

        private static int ExplorerItemHeight (RadPageViewPage page)
            => Math.Max (page.ImageBitmap?.Height ?? 0, ExplorerItemFont.Height + 1) + 16;

        // How far apart the items are: their height less the line they share.
        private int ExplorerItemStep {
            get {
                var pages = TabPages.OfType<RadPageViewPage> ().ToList ();

                if (pages.Count == 0)
                    return 0;

                return pages.Max (ExplorerItemHeight) - 2;
            }
        }

        // The stack's height, unscrolled, as Telerik's scroll bar counts it: every item and every open content area.
        private int ExplorerExtent {
            get {
                var step = ExplorerItemStep;
                var extent = 0;

                foreach (var page in TabPages.OfType<RadPageViewPage> ())
                    extent += step + (page.IsContentVisible ? ExplorerPageLength : 0);

                return extent;
            }
        }

        // The room under the title band, down to the bottom line.
        private int ExplorerViewport => Math.Max (0, LogicalHeight - ExplorerStackTop - 1);

        // Where a page's item starts in the unscrolled stack.
        private int ExplorerSlotTop (RadPageViewPage target)
        {
            var step = ExplorerItemStep;
            var top = 0;

            foreach (var page in ExplorerOrder) {
                if (page == target)
                    return top;

                top += step + (page.IsContentVisible ? ExplorerPageLength : 0);
            }

            return top;
        }

        private void LayoutExplorerBar ()
        {
            var width = LogicalWidth;
            var height = LogicalHeight;
            var extent = ExplorerExtent;
            var viewport = ExplorerViewport;
            var scrolls = extent > viewport && viewport > 0;

            explorer_offset = scrolls ? Math.Max (0, Math.Min (explorer_offset, extent - viewport)) : 0;

            if (scrolls) {
                explorer_scroll ??= CreateExplorerScrollBar ();
                explorer_scroll.Bounds = new Rectangle (width - ExplorerScrollBarWidth - 1, ExplorerStackTop + 1, ExplorerScrollBarWidth, Math.Max (0, height - ExplorerStackTop - 2));
                explorer_scroll.Minimum = 0;
                explorer_scroll.Maximum = extent - 1;
                explorer_scroll.LargeChange = viewport;
                explorer_scroll.SmallChange = 5;
                explorer_scroll.Value = explorer_offset;
                explorer_scroll.Visible = true;
            } else if (explorer_scroll is not null) {
                explorer_scroll.Visible = false;
            }

            // The items run to the right edge, or to the scroll bar; the content areas inside the frame's left line.
            var item_width = scrolls ? width - ExplorerScrollBarWidth - 1 : width;
            var content_width = item_width - 2 + (scrolls ? 1 : 0);
            var step = ExplorerItemStep;
            var y = ExplorerStackTop - explorer_offset;

            explorer_items.Clear ();

            var after_content = false;

            foreach (var page in ExplorerOrder) {
                // Its lines on its first and last rows; an item right under an open page starts on that page's last row.
                var top = after_content ? y - 1 : y;
                var item = new Rectangle (0, top, item_width, y + step + 1 - top);
                var content = Rectangle.Empty;

                y += step;
                after_content = page.IsContentVisible;

                if (page.IsContentVisible) {
                    // From the item's bottom line, which it shares; the page 4 in.
                    content = new Rectangle (1, y, content_width, ExplorerPageLength);

                    var bounds = new Rectangle (5, y + 4, Math.Max (0, content_width - 8), ExplorerPageLength - 8);

                    if (page.Bounds != bounds)
                        page.Bounds = bounds;

                    y += ExplorerPageLength;
                }

                if (page.Visible != page.IsContentVisible)
                    page.Visible = page.IsContentVisible;

                explorer_items.Add ((page, item, content));
            }
        }

        private VScrollBar CreateExplorerScrollBar ()
        {
            var bar = Controls.AddImplicitControl (new VScrollBar { Visible = false });

            bar.ValueChanged += (_, _) => {
                if (bar.Value == explorer_offset)
                    return;

                explorer_offset = bar.Value;
                PerformLayout ();
                Invalidate ();
            };

            return bar;
        }

        // ---- input ----

        /// <summary>The page whose item is at a logical point in the ExplorerBar mode, or null.</summary>
        internal RadPageViewPage? ExplorerItemAt (Point location)
        {
            if (!IsExplorerBar || location.Y < ExplorerStackTop || location.Y >= LogicalHeight - 1)
                return null;

            foreach (var (page, item, _) in explorer_items)
                if (item.Contains (location))
                    return page;

            return null;
        }

        /// <summary>The rectangle, in logical coordinates, of a page's item as last laid out in the ExplorerBar mode.</summary>
        internal Rectangle ExplorerItemBounds (RadPageViewPage page)
            => explorer_items.FirstOrDefault (i => i.Page == page).Item;

        /// <summary>The scroll position of the ExplorerBar stack.</summary>
        internal int ExplorerScrollOffset => explorer_offset;

        /// <summary>The scroll bar of the ExplorerBar stack, when it shows one.</summary>
        internal VScrollBar? ExplorerScrollBar => explorer_scroll is { Visible: true } ? explorer_scroll : null;

        /// <inheritdoc/>
        protected override void OnMouseClick (MouseEventArgs e)
        {
            ToggleExplorerItem (e);
            base.OnMouseClick (e);
        }

        // Measured: a double click expands the page once, as here, where its second click arrives as a double click.
        private void ToggleExplorerItem (MouseEventArgs e)
        {
            if (e.Button != MouseButtons.Left || ExplorerItemAt (e.Location) is not { } page)
                return;

            // Selected first, then expanded or collapsed, as Telerik's.
            SelectedTabPage = page;
            page.IsContentVisible = !page.IsContentVisible;
        }

        /// <inheritdoc/>
        protected override void OnMouseWheel (MouseEventArgs e)
        {
            base.OnMouseWheel (e);

            if (!IsExplorerBar || explorer_scroll is not { Visible: true } bar)
                return;

            var notches = e.Delta / 120;
            var value = Math.Max (0, Math.Min (bar.Maximum - bar.LargeChange + 1, explorer_offset - notches * ExplorerWheelStep));

            if (value != explorer_offset) {
                explorer_offset = value;
                PerformLayout ();
                Invalidate ();
            }
        }

        // ---- paint ----

        /// <inheritdoc/>
        protected override void OnPaintBackground (PaintEventArgs e)
        {
            base.OnPaintBackground (e);

            if (!IsExplorerBar)
                return;

            using var device = e.DeviceSpace ();
            PaintExplorerBar (e);
        }

        private void PaintExplorerBar (PaintEventArgs e)
        {
            var canvas = e.Canvas;
            float D (float logical) => logical * (float) e.Scaling;
            var one = D (1);
            var width = LogicalWidth;
            var height = LogicalHeight;

            using var paint = new SKPaint { IsAntialias = false };

            // The title band: a white line inside the frame round the fill.
            using (var shader = SKShader.CreateLinearGradient (new SKPoint (0, D (-1)), new SKPoint (0, D (ExplorerStackTop)), ExplorerTitleFill, null, SKShaderTileMode.Clamp)) {
                paint.Shader = shader;
                canvas.DrawRect (new SKRect (D (1), D (1), D (width - 1), D (ExplorerStackTop)), paint);
                paint.Shader = null;
            }

            paint.Style = SKPaintStyle.Stroke;
            paint.StrokeWidth = one;
            paint.Color = SKColors.White;
            canvas.DrawRect (new SKRect (D (1) + one / 2, D (1) + one / 2, D (width - 1) - one / 2, D (ExplorerStackTop) - one / 2), paint);
            paint.Style = SKPaintStyle.Fill;

            if (SelectedTabPage is { } selected && !string.IsNullOrEmpty (selected.Text)) {
                var title = new Rectangle (4, 1, Math.Max (0, width - 8), ExplorerStackTop - 1);
                canvas.DrawText (selected.Text, TabStripLook.TypefaceOf (ExplorerTitleFont), LogicalToDeviceUnits ((int) Math.Round (TabStripLook.PixelSizeOf (ExplorerTitleFont))),
                    LogicalToDeviceUnits (title), ExplorerTitleText, ContentAlignment.MiddleLeft, maxLines: 1, ellipsis: true);
            }

            // The stack, inside the frame and under the title band.
            canvas.Save ();
            canvas.ClipRect (new SKRect (0, D (ExplorerStackTop), D (width), D (height - 1)));

            var step = ExplorerItemStep;

            foreach (var (page, item, content) in explorer_items) {
                if (!content.IsEmpty) {
                    paint.Color = ExplorerContentFill;
                    canvas.DrawRect (new SKRect (D (content.Left), D (content.Top), D (content.Right), D (content.Bottom)), paint);
                }

                PaintExplorerItem (e, canvas, paint, page, item, step, D);
            }

            canvas.Restore ();

            // The frame round the whole view.
            paint.Style = SKPaintStyle.Stroke;
            paint.StrokeWidth = one;
            paint.Color = ExplorerBorder;
            canvas.DrawRect (new SKRect (one / 2, one / 2, D (width) - one / 2, D (height) - one / 2), paint);
            paint.Style = SKPaintStyle.Fill;
        }

        private void PaintExplorerItem (PaintEventArgs e, SKCanvas canvas, SKPaint paint, RadPageViewPage page, Rectangle item, int step, Func<float, float> D)
        {
            var one = D (1);
            var fill = page == SelectedTabPage ? ExplorerSelectedFill : ExplorerItemFill;

            // Its own rows: an item under an open page reaches a row higher, onto that page's last row.
            var slot = new Rectangle (item.Left, item.Bottom - 1 - step, item.Width, step + 1);

            // The fill runs over the item as Telerik sizes it, a row more above and below its lines.
            using (var shader = SKShader.CreateLinearGradient (new SKPoint (0, D (slot.Top - 1)), new SKPoint (0, D (slot.Bottom + 1)), fill,
                       ExplorerItemStops, SKShaderTileMode.Clamp)) {
                paint.Shader = shader;
                canvas.DrawRect (new SKRect (D (item.Left), D (item.Top), D (item.Right), D (item.Bottom)), paint);
                paint.Shader = null;
            }

            paint.Style = SKPaintStyle.Stroke;
            paint.StrokeWidth = one;
            paint.Color = ExplorerBorder;
            canvas.DrawRect (new SKRect (D (item.Left) + one / 2, D (item.Top) + one / 2, D (item.Right) - one / 2, D (item.Bottom) - one / 2), paint);
            paint.Style = SKPaintStyle.Fill;

            var text_left = 5;

            if (page.ImageBitmap is { } image) {
                var top = slot.Top + (slot.Height - image.Height) / 2;
                canvas.DrawBitmap (image, new SKRect (D (5), D (top), D (5 + image.Width), D (top + image.Height)));
                text_left += image.Width;
            }

            if (string.IsNullOrEmpty (page.Text))
                return;

            // GDI+'s leading space before the caption.
            var text = new Rectangle (text_left + 2, slot.Top + 1, Math.Max (0, item.Right - 5 - text_left - 2), Math.Max (0, slot.Height - 2));
            var lines = page.Text.Contains ('\n') ? (int?) null : 1;

            canvas.DrawText (page.Text, TabStripLook.TypefaceOf (ExplorerItemFont), LogicalToDeviceUnits ((int) Math.Round (TabStripLook.PixelSizeOf (ExplorerItemFont))),
                LogicalToDeviceUnits (text), page.Enabled ? ExplorerItemText : new SKColor (0x7B, 0x7B, 0x7B), ContentAlignment.MiddleLeft,
                maxLines: lines, ellipsis: lines is not null);
        }
    }
}
