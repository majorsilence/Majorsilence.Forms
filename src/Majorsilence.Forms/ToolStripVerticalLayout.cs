using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;

namespace Majorsilence.Forms
{
    // A ported app's ToolStrip and MenuStrip run down the form when docked Left or Right, as upstream's do: their LayoutStyle
    // is StackWithOverflow, which follows the docked edge, and a vertical strip stacks its items from the top. Measured with a
    // WinForms probe (Microsoft Sans Serif 12pt as the app font, strips in SystemFonts.MenuFont), a ToolStrip of "Button0",
    // an image button "Btn1", a separator, an image-only button and a label "Lbl", docked left on a 200px panel:
    //
    // - the strip is 54 x 200, as wide as its widest item (53) and its (0, 0, 1, 0) padding; it prefers 54 x 103, its items
    //   and margins stacked, 2px after the last and the 9px grip band above the first;
    // - the items sit from y 10 (the grip band and a button's 1px top margin), each its own height (19, 20, 6, 20, 15) and
    //   51 wide: upstream's ToolStripSplitStackLayout pads them to the strip's preferred width less its padding (twice,
    //   as it subtracts the padding from a size that never had it) and a pixel. A strip with AutoSize off, or one not
    //   docked to a side, pads them to its display rectangle less a pixel instead.
    //
    // A MenuStrip the same way: 50 x 200 for "File0", "Edit", a separator and "Help", its items 19px tall from (6, 2), 37 wide.
    // MF laid every one of these strips out in a row, whatever its edge. An app that never chose a font keeps that.
    public partial class ToolStrip
    {
        // A ported app's strip that runs down its edge as upstream's does. A StatusStrip is a table, laid out on its own.
        internal bool LaysOutVerticallyAsUpstream
            => SizesAsUpstreamStrip && this is not StatusStrip && EffectiveLayoutStyle == ToolStripLayoutStyle.VerticalStackWithOverflow;

        // Upstream's default LayoutStyle for a ToolStrip and a MenuStrip, which reads back as the stack its orientation gives.
        private bool StacksWithOverflowAsUpstream => SizesAsUpstreamStrip && this is not (StatusStrip or ToolStripDropDown);

        // The orientation a StackWithOverflow strip takes: its own if it was set, else its docked edge's.
        private Orientation DockedOrientation
            => orientation_override ?? (Dock is DockStyle.Left or DockStyle.Right ? Orientation.Vertical : Orientation.Horizontal);

        // An item's preferred size down a vertical strip: a separator 6px tall, the rest as across one.
        internal virtual Size UpstreamVerticalItemSize (MenuItem item)
            => item is ToolStripSeparator ? new Size (23, 6) : UpstreamItemSize (item);

        private IEnumerable<MenuItem> UpstreamLaidOutItems
            => RootItems.Cast<MenuItem> ().Where (i => i is not null && i.Visible && i is not ToolStripOverflowButton);

        // Upstream's ToolStrip.GetPreferredSizeVertical, plus the padding and the grip band: as wide as the widest item with
        // its margin, as tall as the items and margins stacked, then 2px, or the overflow button when an item always
        // overflows.
        internal Size UpstreamPreferredSizeVertical ()
        {
            var width = 0;
            var height = 0;
            var overflow_always = false;

            foreach (var item in UpstreamLaidOutItems) {
                if (item is ToolStripItem { Overflow: ToolStripItemOverflow.Always }) {
                    overflow_always = true;
                    continue;
                }

                var size = UpstreamVerticalItemSize (item);
                width = Math.Max (width, size.Width + item.Margin.Horizontal);
                height += size.Height + item.Margin.Vertical;
            }

            if (overflow_always) {
                var button = OverflowButton;
                height += button.GetPreferredSize (Size.Empty).Height + button.Margin.Vertical;
            } else {
                height += 2;
            }

            return new Size (width + Padding.Horizontal, height + GripBandWidth + Padding.Vertical);
        }

        // Upstream's ToolStripSplitStackLayout.LayoutVertical: the items stacked down the display rectangle from its top,
        // each its own height inside its margin, all padded to one width (see above).
        internal void LayoutItemsVerticallyAsUpstream (Rectangle display, List<MenuItem> items)
        {
            var across = Dock is DockStyle.Left or DockStyle.Right && AutoSize && Parent is not ToolStripPanel
                ? UpstreamPreferredSizeVertical ().Width - 2 * Padding.Horizontal
                : display.Width;
            var y = display.Y;

            foreach (var item in items) {
                var margin = item.Margin;
                var size = UpstreamVerticalItemSize (item);
                var width = item is ToolStripItem { AutoSize: false } ? size.Width : Math.Max (0, across - 1 - margin.Horizontal);

                item.SetBounds (display.X + margin.Left, y + margin.Top, width, size.Height);
                y += margin.Vertical + size.Height;
            }
        }

        // The display rectangle a ported app's vertical strip stacks its items in: inside its padding, below the grip band.
        internal Rectangle UpstreamVerticalDisplay (Rectangle area)
            => new Rectangle (area.X + Padding.Left, area.Y + Padding.Top,
                Math.Max (0, area.Width - Padding.Horizontal), Math.Max (0, area.Height - Padding.Vertical));
    }

    public partial class Menu
    {
        // A menu item down a vertical MenuStrip is a line of its font tall in its 2px border, or its 16px image's: "File0" in
        // Segoe UI 9pt is 19px. Across a horizontal strip the row is never less than 16px of text, which is what makes
        // upstream's 24px bar; down a vertical one the items are their own height.
        internal override Size UpstreamVerticalItemSize (MenuItem item)
        {
            var size = base.UpstreamVerticalItemSize (item);

            if (item is ToolStripSeparator || item is MdiControlItem || UpstreamFont is not { } font)
                return size;

            var line = TextLineHeight.Of ((item as ToolStripItem)?.Font ?? font);
            var content = item.ImageSK is null ? line : Math.Max (line, 16);

            return new Size (size.Width, content + item.Padding.Vertical + 4);
        }
    }
}
