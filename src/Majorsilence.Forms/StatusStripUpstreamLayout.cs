using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;

namespace Majorsilence.Forms
{
    // A ported app's StatusStrip (one that chose its font with Application.SetDefaultFont) lays its items out as upstream's
    // does in its default Table layout, as measured with a WinForms probe (Microsoft Sans Serif 12pt as the app font):
    //
    // - each item is its own preferred width -- a status label its text as TextRenderer measures it in the strip's font
    //   (SystemFonts.MenuFont, Segoe UI 9pt, unless the strip or the item has one), beside its image -- and the strip's
    //   height less its margin: a label's (0, 3, 0, 2), a progress bar's (1, 3, 1, 3), another item's (0, 2, 0, 0);
    // - Spring labels share what the others leave of the strip's display rectangle, inside its (1, 0, 14, 0) padding;
    // - an item that runs into the sizing grip, or overlaps the item before it, is not shown (Placement None);
    // - docked Left or Right, the strip is vertical: the items stack down it, as wide as it is, in a (1, 3, 1, 22) padding;
    // - right to left, the row is mirrored and so is the padding.
    //
    // MF laid every item out 120px wide, 4px apart and the strip's full height, in the theme's font. An app that never
    // chose a font keeps that.
    public partial class StatusStrip
    {
        // Upstream's sizing grip is 12px wide, at most DefaultSize's 22px tall, in the trailing bottom corner.
        private const int UpstreamGripWidth = 12;

        /// <summary>Whether this strip lays its items out as upstream's StatusStrip does: in a ported app.</summary>
        internal bool LaysOutAsUpstream => ControlPaint.UsesUpstreamGlyphs;

        private bool IsVerticalStatusStrip => Orientation == Orientation.Vertical;

        // StatusStrip.DefaultPadding upstream: the sizing grip's side is the wider.
        private Padding UpstreamStatusPadding
            => IsVerticalStatusStrip ? new Padding (1, 3, 1, 22)
             : RightToLeft == RightToLeft.Yes ? new Padding (14, 0, 1, 0)
             : new Padding (1, 0, 14, 0);

        private Rectangle UpstreamSizeGripBounds ()
        {
            if (!SizingGrip)
                return Rectangle.Empty;

            var height = Math.Min (DefaultSize.Height, Height);
            var x = RightToLeft == RightToLeft.Yes ? 0 : Width - UpstreamGripWidth;

            return new Rectangle (x, Height - height, UpstreamGripWidth, height);
        }

        private static bool IsSpring (MenuItem item) => item is ToolStripStatusLabel { Spring: true };

        /// <summary>An item's preferred size on this strip, as upstream's item measures itself there.</summary>
        internal Size UpstreamStatusItemSize (MenuItem item)
        {
            // A separator asks for 6 x 23 whichever way the strip runs.
            if (item is ToolStripSeparator)
                return new Size (6, 23);

            if (item is ToolStripItem { AutoSize: false } fixed_size)
                return fixed_size.Size;

            // A hosted progress bar is its control's size: upstream's 100 x 15 unless one was given.
            if (item is ToolStripProgressBar bar)
                return bar.Size is { Width: > 0, Height: > 0 } given ? given : new Size (100, 15);

            var strip_item = item as ToolStripItem;
            var font = Renderers.StripItemFont.ResolveFont (item, this) ?? SystemFonts.MenuFont;
            var text = strip_item is { DisplaysText: false } || string.IsNullOrEmpty (item.Text) ? null : item.Text;
            var text_size = text is null ? Size.Empty : new Size (TextRenderer.MeasureText (text, font).Width, TextLineHeight.Of (font));
            var image_size = UpstreamImageSize (item);
            var stacked = strip_item?.TextImageRelation is TextImageRelation.ImageAboveText or TextImageRelation.TextAboveImage;

            var size = stacked
                ? new Size (Math.Max (text_size.Width, image_size.Width), text_size.Height + image_size.Height)
                : new Size (text_size.Width + image_size.Width, Math.Max (text_size.Height, image_size.Height));

            // A label has no border; a button has upstream's 2px all round; a drop-down button its 5px arrow in 2px
            // either side, a split button its 11px drop-down part and the line before it.
            size += item switch {
                ToolStripStatusLabel { BorderSides: not ToolStripStatusLabelBorderSides.None } => new Size (4, 4),
                ToolStripSplitButton => new Size (4 + 12, 4),
                ToolStripDropDownButton => new Size (4 + 9, 4),
                ToolStripButton => new Size (4, 4),
                _ => Size.Empty
            };

            return size + (item.AssignedPadding ?? Padding.Empty).Size;
        }

        private Size UpstreamImageSize (MenuItem item)
        {
            if (item.ImageSK is not { } image || item is ToolStripItem { DisplaysImage: false })
                return Size.Empty;

            return item is ToolStripItem { ImageScaling: ToolStripItemImageScaling.None }
                ? new Size (image.Width, image.Height)
                : ImageScalingSize;
        }

        // What the item takes across the strip's thickness when the strip measures itself. A fixed-size item is laid
        // out at the strip's height before upstream's strip measures it, so its own height never makes the strip
        // taller: a 120 x 30 label with AutoSize off leaves the strip 22px tall, where a 117 x 19 progress bar makes it 25.
        private int UpstreamThickness (MenuItem item, Size size)
            => item is ToolStripItem { AutoSize: false } and not ToolStripProgressBar ? 0
             : IsVerticalStatusStrip ? size.Width : size.Height;

        // Upstream's StatusStrip.GetPreferredSize: ToolStrip's GetPreferredSizeHorizontal (or Vertical) plus the padding,
        // never thinner than DefaultSize. The items laid end to end with their margins and 2px after the last.
        internal Size UpstreamStatusPreferredSize ()
        {
            var padding = Padding;
            var along = 0;
            var across = 0;
            var any = false;

            foreach (var item in Items.Cast<MenuItem> ()) {
                if (item is null || !item.Visible || item is ToolStripOverflowButton)
                    continue;

                any = true;

                var size = UpstreamStatusItemSize (item);
                var margin = item.Margin;

                if (IsVerticalStatusStrip) {
                    along += size.Height + margin.Vertical;
                    across = Math.Max (across, UpstreamThickness (item, size) + margin.Horizontal);
                } else {
                    along += size.Width + margin.Horizontal;
                    across = Math.Max (across, UpstreamThickness (item, size) + margin.Vertical);
                }
            }

            if (IsVerticalStatusStrip)
                return new Size (across + padding.Horizontal, along + 2 + padding.Vertical);

            var inner = DefaultSize - padding.Size;

            if (!any)
                along = inner.Width;

            return new Size (along + 2 + padding.Horizontal, Math.Max (inner.Height, across) + padding.Vertical);
        }

        // Upstream's spring table: each item a column (a row when vertical) of its preferred width, Spring labels sharing
        // what is left of the display rectangle, every item as thick as the row less its margin.
        private void LayoutItemsAsUpstream ()
        {
            var visible = Items.Cast<MenuItem> ().Where (i => i.Visible).ToList ();

            foreach (MenuItem item in Items)
                if (!item.Visible)
                    item.SetBounds (0, 0, 0, 0);

            var vertical = IsVerticalStatusStrip;
            var padding = Padding;
            var display = new Rectangle (padding.Left, padding.Top,
                Math.Max (0, Width - padding.Horizontal), Math.Max (0, Height - padding.Vertical));

            var sizes = visible.ToDictionary (i => i, UpstreamStatusItemSize);
            var springs = visible.Count (IsSpring);
            var taken = visible.Sum (i => (vertical ? i.Margin.Vertical : i.Margin.Horizontal)
                                          + (IsSpring (i) ? 0 : vertical ? sizes[i].Height : sizes[i].Width));
            var free = Math.Max (0, (vertical ? display.Height : display.Width) - taken);
            var share = springs == 0 ? 0 : free / springs;
            var springs_left = springs;
            var position = vertical ? display.Y : display.X;

            foreach (var item in visible) {
                var margin = item.Margin;
                var length = vertical ? sizes[item].Height : sizes[item].Width;

                // The last spring takes what the division left over, as the table's last percent column does.
                if (IsSpring (item))
                    length = --springs_left == 0 ? free - share * (springs - 1) : share;

                Rectangle bounds;

                if (vertical) {
                    bounds = new Rectangle (display.X + margin.Left, position + margin.Top, Math.Max (0, display.Width - margin.Horizontal), length);
                    position = bounds.Bottom + margin.Bottom;
                } else {
                    bounds = new Rectangle (position + margin.Left, display.Y + margin.Top, length, Math.Max (0, display.Height - margin.Vertical));
                    position = bounds.Right + margin.Right;

                    // Right to left, the row runs from the display rectangle's right edge.
                    if (RightToLeft == RightToLeft.Yes)
                        bounds.X = display.X + display.Right - bounds.Right;
                }

                item.SetBounds (bounds.X, bounds.Y, bounds.Width, bounds.Height);
            }

            HideItemsThatDoNotFit (visible, display);
        }

        // Upstream's StatusStrip.SetDisplayedItems: an item that runs into the sizing grip, or overlaps the item shown
        // before it, is moved just outside the strip and not shown; so is a spring squeezed to one pixel, and everything
        // after it.
        private void HideItemsThatDoNotFit (List<MenuItem> visible, Rectangle display)
        {
            var grip = UpstreamSizeGripBounds ();
            var outside = new Point (display.X + Width + 1, display.Y + Height + 1);
            var last = Rectangle.Empty;
            var overflow = false;

            foreach (var item in visible) {
                var hide = overflow
                        || (!grip.IsEmpty && item.Bounds.IntersectsWith (grip))
                        || StartsPastTheStrip (item.Bounds)
                        || (last.Width > 0 && last.IntersectsWith (item.Bounds));

                if (!hide && item.Bounds.Width == 1 && IsSpring (item))
                    hide = overflow = true;

                if (hide) {
                    item.SetBounds (outside.X, outside.Y, item.Bounds.Width, item.Bounds.Height);
                } else {
                    last = item.Bounds;
                }

                if (item is ToolStripItem strip_item)
                    strip_item.Placement = hide ? ToolStripItemPlacement.None : ToolStripItemPlacement.Main;
            }
        }

        // An item the row has pushed off the end is not shown either, though no grip reaches it. Measured on WinForms:
        // with a sizing grip an item is shown while its right edge is at or before the grip's left (287 and 288 of a
        // 300px strip are shown, 289 is not); past that the grip catches it. Without one the grip catches nothing, and
        // the cut is the item's own start: on a 300px strip an item at x 296 is shown and one at x 300 is not, however
        // far it runs. A row wider than the strip -- a font wider than the one it was laid out for -- lands a later item
        // beyond the grip entirely, touching neither it nor the item before it.
        private bool StartsPastTheStrip (Rectangle bounds)
            => IsVerticalStatusStrip ? bounds.Y >= Height
             : RightToLeft == RightToLeft.Yes ? bounds.Right <= 0
             : bounds.X >= Width;

        // Upstream's preferred size is its items' whatever the strip's own size or AutoSize: one with AutoSize off and 30px
        // tall still prefers 107 x 22 for two labels.
        internal override Size GetPreferredSizeCore (Size proposedSize)
            => SizesAsUpstreamStrip ? UpstreamStatusPreferredSize () : base.GetPreferredSizeCore (proposedSize);
    }
}
