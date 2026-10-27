using System.Collections.Generic;
using System.Drawing;
using SkiaSharp;

namespace Majorsilence.Forms.Telerik
{
    // Telerik's paging panel: the strip along the bottom of a grid with EnablePaging. On the left the first, previous and
    // "..." (the previous group of pages) buttons, a button for each page of the current group, then "...", next and
    // last; on the right "Page [n] of N", the box taking a page number. Laid out as measured from Telerik UI for WinForms:
    //
    // - items are 24px tall, or the font's line height plus 5 when that is taller, 2px down a band 3px taller than them
    //   (29px at least), below the rows and the horizontal scrollbar;
    // - the image buttons are 24px wide and "..." its text plus 5, each pair 3px either side of a 2px separator, and the
    //   page buttons share what the strip leaves equally, five pages to a group;
    // - the labels are their text wide and the box 30px, its height the font's line height plus 3 (20 at least), all together
    //   4px in from the right.
    //
    // Text is measured as GDI+ measures it, a sixth of the em wider either side than the glyphs, and the line height is the
    // font's Height.
    public partial class RadGridView
    {
        /// <summary>The page buttons the paging panel shows at a time (Telerik's NumericButtonsCount).</summary>
        internal const int PagingNumericButtonsCount = 5;

        private TextBox? _pageEditBox;
        private bool _showedPagingPanel;

        /// <summary>Whether the paging panel is shown: paging enabled on a grid with columns.</summary>
        internal bool ShowsPagingPanel => MasterTemplate is not null && MasterTemplate.EnablePaging && ((DataGridView) this).Columns.Count > 0;

        /// <inheritdoc/>
        protected override int ContentBottomOffset => ShowsPagingPanel ? LogicalToDeviceUnits (PagingPanelLogicalHeight) : 0;

        /// <inheritdoc/>
        /// <remarks>Less the paging panel, so the docked scrollbars stop above it.</remarks>
        public override Rectangle DisplayRectangle {
            get {
                var display = base.DisplayRectangle;

                if (ShowsPagingPanel)
                    display.Height = Math.Max (0, display.Height - PagingPanelLogicalHeight);

                return display;
            }
        }

        // The panel's font: the cells', so the grid's own once it has one.
        private (SKTypeface Typeface, int Size, int Line) PagingFont ()
        {
            var (typeface, size) = CellFallbackFont ();
            var style = (typeface.IsBold ? Majorsilence.Forms.Drawing.FontStyle.Bold : Majorsilence.Forms.Drawing.FontStyle.Regular)
                      | (typeface.IsItalic ? Majorsilence.Forms.Drawing.FontStyle.Italic : Majorsilence.Forms.Drawing.FontStyle.Regular);
            using var font = new Majorsilence.Forms.Drawing.Font (typeface.FamilyName, size * 72f / 96f, style);
            return (typeface, size, font.Height);
        }

        private int PagingItemHeight => Math.Max (24, PagingFont ().Line + 5);

        /// <summary>The paging panel's height in logical pixels.</summary>
        internal int PagingPanelLogicalHeight => Math.Max (29, PagingItemHeight + 3);

        // GDI+'s width of a string: the glyphs and a sixth of the em either side.
        private static float GdiPlusWidth (string text, SKTypeface typeface, int size)
            => TextMeasurer.MeasureText (text, typeface, size).Width + size / 3f;

        // A label's width, GDI+'s rounded up. GDI+'s hinted glyphs come out a little wider than Skia's: half a pixel more
        // gives "Page", "of" and the page counts Telerik's widths in Segoe UI 9 and 12pt and Microsoft Sans Serif 12pt, but
        // for Segoe UI 12pt's "Page", a pixel short.
        private static int LabelWidth (string text, SKTypeface typeface, int size)
            => (int) Math.Ceiling (GdiPlusWidth (text, typeface, size) + 0.5f);

        /// <summary>The first page of the group of page buttons the current page is in.</summary>
        internal int PagingGroupStart => _currentPage / PagingNumericButtonsCount * PagingNumericButtonsCount;

        /// <summary>Lays the paging panel out, in device pixels.</summary>
        internal PagingPanelLayout LayoutPagingPanel ()
        {
            var client = DeviceClientRectangle;
            var band_height = LogicalToDeviceUnits (PagingPanelLogicalHeight);
            var band = new Rectangle (client.Left, client.Bottom - band_height, client.Width, band_height);
            var layout = new PagingPanelLayout { Band = band };

            var (typeface, size, line) = PagingFont ();
            var width = DeviceToLogicalUnits (client.Width);
            var item_height = PagingItemHeight;
            const int top = 2;

            Rectangle Item (int x, int w, int y = top, int h = -1)
            {
                var logical = new Rectangle (x, y, Math.Max (0, w), h < 0 ? item_height : h);
                var device = LogicalToDeviceUnits (logical);
                device.Offset (band.Left, band.Top);
                return device;
            }

            // The right strip, ending 4px in from the panel's right.
            var page_width = LabelWidth ("Page", typeface, size);
            var of_width = LabelWidth ("of", typeface, size);
            var count_width = LabelWidth (PageCount.ToString (System.Globalization.CultureInfo.CurrentCulture), typeface, size);
            const int box_width = 30;
            var right = width - 1 - (page_width + box_width + of_width + count_width + 4);
            var box_height = Math.Max (20, line + 3);

            layout.PageLabel = Item (right, page_width);
            layout.PageBox = Item (right + page_width, box_width, top + (item_height - box_height) / 2, box_height);
            layout.OfLabel = Item (right + page_width + box_width, of_width);
            layout.CountLabel = Item (right + page_width + box_width + of_width, count_width);

            // The left strip, from the panel's left to the right strip: its buttons in from either end, the pages between.
            const int image = 24, gap = 3, separator = 2;
            var fast = (int) Math.Round (GdiPlusWidth ("...", typeface, size) + 5);

            var x = 1;
            layout.First = Item (x, image);
            x += image + gap;
            layout.Separators.Add (Item (x, separator));
            x += separator + gap;
            layout.Previous = Item (x, image);
            x += image + gap;
            layout.Separators.Add (Item (x, separator));
            x += separator + gap;
            layout.FastBack = Item (x, fast);
            x += fast + gap;
            layout.Separators.Add (Item (x, separator));
            var pages_left = x + separator + gap;

            x = right - image;
            layout.Last = Item (x, image);
            layout.Separators.Add (Item (x - gap - separator, separator));
            x -= gap + separator + gap + image;
            layout.Next = Item (x, image);
            layout.Separators.Add (Item (x - gap - separator, separator));
            x -= gap + separator + gap + fast;
            layout.FastForward = Item (x, fast);
            layout.Separators.Add (Item (x - gap - separator, separator));
            var pages_right = x - gap - separator - gap;

            var start = PagingGroupStart;
            var count = Math.Max (0, Math.Min (PagingNumericButtonsCount, PageCount - start));
            var available = Math.Max (0, pages_right - pages_left);

            for (var i = 0; i < count; i++) {
                var left = pages_left + (int) Math.Round (available * (double) i / count);
                var next = pages_left + (int) Math.Round (available * (double) (i + 1) / count);
                layout.Pages.Add ((start + i, Item (left, next - left)));
            }

            return layout;
        }

        // Rows, columns or paging changed: the panel came or went, so the scrollbars dock round it afresh.
        internal void OnPagingLayoutChanged ()
        {
            var shows = ShowsPagingPanel;

            if (shows == _showedPagingPanel)
                return;

            _showedPagingPanel = shows;

            if (!shows)
                ClosePageEditor ();

            PerformLayout ();
            Invalidate ();
        }

        // A press in the paging panel: the button under it, or the page box.
        private void HandlePagingPanelMouseDown (Point device)
        {
            var layout = LayoutPagingPanel ();
            var start = PagingGroupStart;

            if (layout.First.Contains (device))
                GoToPage (0);
            else if (layout.Previous.Contains (device))
                GoToPage (_currentPage - 1);
            else if (layout.FastBack.Contains (device))
                GoToPage (start - PagingNumericButtonsCount);
            else if (layout.FastForward.Contains (device))
                GoToPage (start + PagingNumericButtonsCount);
            else if (layout.Next.Contains (device))
                GoToPage (_currentPage + 1);
            else if (layout.Last.Contains (device))
                GoToPage (PageCount - 1);
            else if (layout.PageBox.Contains (device))
                OpenPageEditor (layout.PageBox);
            else
                foreach (var (page, bounds) in layout.Pages)
                    if (bounds.Contains (device)) {
                        GoToPage (page);
                        break;
                    }
        }

        // The page box takes a page number: Enter goes to it, Escape leaves the page as it was.
        private void OpenPageEditor (Rectangle box)
        {
            if (FindWindow () is null)
                return;

            ClosePageEditor ();

            _pageEditBox = new TextBox { Text = (_currentPage + 1).ToString (System.Globalization.CultureInfo.CurrentCulture) };
            _pageEditBox.Style.Border.Width = 0;
            _pageEditBox.KeyDown += PageEditBox_KeyDown;

            var logical = DeviceToLogicalUnits (box);
            _pageEditBox.SetBounds (logical.Left + 1, logical.Top + 1, Math.Max (1, logical.Width - 2), Math.Max (1, logical.Height - 2));
            Controls.Add (_pageEditBox);
            _pageEditBox.Select ();
            _pageEditBox.SelectAll ();
            Invalidate ();
        }

        private void ClosePageEditor ()
        {
            if (_pageEditBox is null)
                return;

            _pageEditBox.KeyDown -= PageEditBox_KeyDown;
            Controls.Remove (_pageEditBox);
            _pageEditBox.Dispose ();
            _pageEditBox = null;
            Invalidate ();
        }

        private void PageEditBox_KeyDown (object? sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Enter) {
                if (int.TryParse (_pageEditBox?.Text, System.Globalization.NumberStyles.Integer, System.Globalization.CultureInfo.CurrentCulture, out var page))
                    GoToPage (page - 1);

                ClosePageEditor ();
                e.Handled = true;
            } else if (e.KeyCode == Keys.Escape) {
                ClosePageEditor ();
                e.Handled = true;
            }
        }

        /// <summary>Whether the page box is being edited (its editor hosted over it).</summary>
        internal bool EditingPageBox => _pageEditBox is not null;
    }

    /// <summary>Where the paging panel's parts are, in device pixels.</summary>
    internal sealed class PagingPanelLayout
    {
        public Rectangle Band;
        public Rectangle First, Previous, FastBack, FastForward, Next, Last;
        public Rectangle PageLabel, PageBox, OfLabel, CountLabel;
        public readonly List<(int Page, Rectangle Bounds)> Pages = new ();
        public readonly List<Rectangle> Separators = new ();
    }
}
