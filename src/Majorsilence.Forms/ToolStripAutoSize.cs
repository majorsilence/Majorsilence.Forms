using System;
using System.Drawing;
using System.Linq;
using Majorsilence.Forms.Layout;

namespace Majorsilence.Forms
{
    // Upstream's ToolStrip, MenuStrip, StatusStrip and ToolStripDropDown are AutoSize (GrowAndShrink) by default.
    // A docked strip is as thick as its items need and a designer-set Size does not change that; an undocked one is as
    // big as its items in both directions; AutoSize = false keeps the Size it was given. Measured against WinForms
    // (Microsoft Sans Serif 12pt as the app font, strips in SystemFonts.MenuFont): a docked ToolStrip of two buttons
    // is 25px tall whether the designer wrote 15 or 40, a MenuStrip 24 and a StatusStrip 22.
    //
    // A ported app (one that chose its font with Application.SetDefaultFont) gets all of this. An app that never chose a
    // font keeps the old behaviour: AutoSize off, the strip as big as it was told.
    public partial class ToolStrip
    {
        // The strip types upstream makes AutoSize; MF's own Menu (MainMenu, RadMenu) and the legacy ContextMenu are not
        // ToolStrips upstream and keep their sizing.
        private bool IsUpstreamAutoSizeType
            => this is MenuStrip or StatusStrip or ToolStripDropDown || this is not (Menu or MenuDropDown);

        /// <summary>Whether this strip sizes itself as upstream's AutoSize strip does: in a ported app, one of upstream's strip types.</summary>
        internal bool SizesAsUpstreamStrip => ControlPaint.UsesUpstreamGlyphs && IsUpstreamAutoSizeType;

        // Called from both constructors. The property is stored directly rather than through the AutoSize setter: the
        // strip has no parent to lay out yet.
        private void InitializeUpstreamAutoSize ()
        {
            if (!SizesAsUpstreamStrip)
                return;

            CommonProperties.SetAutoSize (this, true);
            CommonProperties.SetAutoSizeMode (this, AutoSizeMode.GrowAndShrink);

            // Upstream's default layout, which runs down a strip docked Left or Right (ToolStripVerticalLayout.cs).
            if (StacksWithOverflowAsUpstream)
                layout_style = ToolStripLayoutStyle.StackWithOverflow;
        }

        // An AutoSize strip's size follows its items, so an item added, removed or changed lays its parent out again:
        // an undocked strip grows to a new button, a docked one to a taller item. A suspended parent defers it.
        internal void LayoutParentForItems ()
        {
            if (SizesAsUpstreamStrip && AutoSize && Parent is { } parent)
                LayoutTransaction.DoLayout (parent, this, PropertyNames.PreferredSize);
        }

        /// <inheritdoc/>
        /// <remarks>Upstream's 100 x 25 for a ported app's ToolStrip, which is also the floor of its height.</remarks>
        protected override Size DefaultSize => SizesAsUpstreamStrip ? new Size (100, 25) : base.DefaultSize;

        // Upstream's ToolStrip.GetPreferredSizeHorizontal: the items' preferred sizes and margins laid end to end, never
        // shorter than DefaultSize less the padding (so an empty strip, or one of short items, is 25px), and 2px after
        // the last item unless an item is always in the overflow, which reserves the overflow button instead. Then the
        // grip and the padding. The strip's current size takes no part: that is what lets an AutoSize strip shrink.
        internal Size UpstreamPreferredSize ()
        {
            var inner = DefaultSize - Padding.Size;
            var width = 0;
            var height = Math.Max (0, inner.Height);
            var any = false;
            var overflow_always = false;

            foreach (var item in RootItems.Cast<MenuItem> ()) {
                if (item is null || !item.Visible || item is ToolStripOverflowButton)
                    continue;

                any = true;

                if (item is ToolStripItem { Overflow: ToolStripItemOverflow.Always }) {
                    overflow_always = true;
                    continue;
                }

                var size = UpstreamItemSize (item);
                width += size.Width + item.Margin.Horizontal;
                height = Math.Max (height, size.Height + item.Margin.Vertical);
            }

            if (!any)
                width = inner.Width;

            if (overflow_always) {
                var button = OverflowButton;
                width += button.GetPreferredSize (Size.Empty).Width + button.Margin.Horizontal;
            } else {
                width += 2;
            }

            return new Size (width + GripBandWidth + Padding.Horizontal, height + Padding.Vertical);
        }

        // An item's preferred size as the strip's own layout measures it.
        internal virtual Size UpstreamItemSize (MenuItem item)
        {
            // A separator across a horizontal strip asks for 6 x 23, whatever the strip's height.
            if (item is ToolStripSeparator)
                return new Size (6, 23);

            var size = item.GetPreferredSize (Size.Empty);

            // The width is the one this strip lays the item out at, so what it paints fits; the height is upstream's.
            if (IsUpstreamPlainStrip && UpstreamPlainItemHeight (item) is { } height)
                size.Height = height;

            return size;
        }

        // Upstream's preferred height for a button, drop-down or split button, or a label, on a ToolStrip: a line of its
        // font (TextRenderer's, GDI's tmHeight) beside or above its image, inside a 2px border a label does not have, and
        // the padding the app gave it (upstream's own is none). Measured: Segoe UI 9pt (the strip's SystemFonts.MenuFont)
        // makes a button 19px, a 16px image 20, the two stacked 35, a label 15. The item paints with the theme's font for
        // now, which measured 23 and made every docked strip of text buttons 26px tall where upstream's is 25.
        // Null for the other items (hosted controls, fixed-size ones), which measure as they lay out.
        private int? UpstreamPlainItemHeight (MenuItem item)
        {
            if (item is not ToolStripItem { AutoSize: true } strip_item)
                return null;

            var border = strip_item is ToolStripButton or ToolStripDropDownButton or ToolStripSplitButton ? 4
                       : strip_item is ToolStripLabel ? 0
                       : -1;

            if (border < 0)
                return null;

            var font = Renderers.StripItemFont.ResolveFont (item, this) ?? SystemFonts.MenuFont;
            var text = strip_item.DisplaysText && item.Text.HasValue () ? TextLineHeight.Of (font) : 0;
            var image = item.ImageSK is not { } bitmap || !strip_item.DisplaysImage ? 0
                      : strip_item.ImageScaling == ToolStripItemImageScaling.None ? bitmap.Height
                      : ImageScalingSize.Height;
            var stacked = strip_item.TextImageRelation is TextImageRelation.ImageAboveText or TextImageRelation.TextAboveImage;
            var content = stacked ? text + image : Math.Max (text, image);

            return content + border + (item.AssignedPadding?.Vertical ?? 0);
        }
    }

    public partial class MenuStrip
    {
        /// <inheritdoc/>
        /// <remarks>Upstream's 200 x 24 for a ported app's menu strip, the floor of its height.</remarks>
        protected override Size DefaultSize => SizesAsUpstreamStrip ? new Size (200, 24) : base.DefaultSize;
    }

    public partial class Menu
    {
        // A menu item is as tall as a line of its font (see UpstreamItemHeight): its own, as upstream measures an item
        // with a larger font, or the bar's. A 20pt item made upstream's bar 39px tall.
        internal override Size UpstreamItemSize (MenuItem item)
        {
            var size = base.UpstreamItemSize (item);

            if (item is ToolStripSeparator || UpstreamFont is not { } font)
                return size;

            return new Size (size.Width, UpstreamItemHeight (item, (item as ToolStripItem)?.Font ?? font));
        }
    }
}
