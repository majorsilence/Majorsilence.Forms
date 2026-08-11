using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using Majorsilence.Forms.Layout;

namespace Majorsilence.Forms
{
    // Row layout for ToolStripPanel, plus the two sizing rules it depends on.
    //
    // Real WinForms hosts every strip added to a ToolStripPanel on its own ToolStripPanelRow,
    // stacked across the panel's minor axis, and ignores the child's Dock while doing it. That is
    // what makes the classic "menu bar on top, toolbar underneath" arrangement work when a menu and
    // a toolbar are both added to the same edge panel of a ToolStripContainer.
    //
    // Without row layout the edge panel was a plain docked Panel: the first child to claim the edge
    // won, the other collapsed to nothing, and only one strip was ever visible. Found via a migrated
    // app whose module windows put a MainMenu and a toolbar in TopToolStripPanel and rendered only
    // the toolbar, squashed to menu height.
    public partial class ToolStripPanel
    {
        // Re-entrancy guard: positioning children dirties layout, which can call back into
        // OnLayout. One arrangement pass per layout is enough.
        private bool _inRowLayout;

        private List<Control> RowChildren ()
        {
            var children = new List<Control> ();

            foreach (Control child in Controls) {
                // ParticipatesInLayout, not Visible: Visible walks the parent chain and reports
                // false for anything not yet on a shown form (a parentless control returns false
                // outright), which would skip row layout during InitializeComponent -- exactly when
                // designer-built module windows are assembled. This is the same predicate the
                // layout engine itself uses.
                if (((IArrangedElement)child).ParticipatesInLayout)
                    children.Add (child);
            }

            // Menu bars rank above toolbars regardless of the order they were added, matching where
            // a menu sits in every WinForms window. Insertion order is not usable on its own: a
            // container that creates its own toolbar up front (ToolStripContainer subclasses
            // typically do) has it in Controls before the designer adds the menu, which would put
            // the menu underneath. Stable within each group, so several toolbars keep their order.
            return children
                .OrderBy (c => c is Menu ? 0 : 1)
                .ToList ();
        }

        // A row is as thick as the strip wants to be. Ask the strip itself: ToolBar reports a
        // height that fits its items (see GetPreferredSizeCore below), so a toolbar of tall
        // image-above-text buttons gets a tall row while a menu bar stays thin.
        private static Size RowPreferredSize (Control child, Size proposed)
        {
            var preferred = child.GetPreferredSize (proposed);

            // A strip with nothing to measure still occupies its current box rather than vanishing.
            if (preferred.Width <= 0)
                preferred.Width = child.Width;
            if (preferred.Height <= 0)
                preferred.Height = child.Height;

            return preferred;
        }

        internal override Size GetPreferredSizeCore (Size proposedSize)
        {
            var children = RowChildren ();

            // Matches a childless Panel so an unpopulated edge panel still collapses to nothing.
            if (children.Count == 0)
                return Size.Empty;

            var horizontal = Orientation == Orientation.Horizontal;
            var across = 0;   // summed along the stacking axis
            var along = 0;    // widest/tallest row

            foreach (var child in children) {
                var size = RowPreferredSize (child, proposedSize);

                if (horizontal) {
                    across += size.Height;
                    along = Math.Max (along, size.Width);
                } else {
                    across += size.Width;
                    along = Math.Max (along, size.Height);
                }
            }

            return horizontal
                ? new Size (along + Padding.Horizontal, across + Padding.Vertical)
                : new Size (across + Padding.Horizontal, along + Padding.Vertical);
        }

        /// <inheritdoc/>
        protected override void OnLayout (LayoutEventArgs e)
        {
            // Let the base raise Layout and run the default pass first; the row arrangement below
            // then overrides the positions, which is what lets us disregard each child's Dock.
            base.OnLayout (e);

            if (_inRowLayout)
                return;

            var children = RowChildren ();

            if (children.Count == 0)
                return;

            _inRowLayout = true;

            try {
                var area = ClientRectangle;

                area = new Rectangle (
                    area.X + Padding.Left,
                    area.Y + Padding.Top,
                    Math.Max (0, area.Width - Padding.Horizontal),
                    Math.Max (0, area.Height - Padding.Vertical));

                var horizontal = Orientation == Orientation.Horizontal;
                var offset = horizontal ? area.Top : area.Left;

                foreach (var child in children) {
                    var size = RowPreferredSize (child, area.Size);

                    if (horizontal) {
                        // Full width, natural height: one horizontal row per strip.
                        child.SetBounds (area.Left, offset, area.Width, size.Height);
                        offset += size.Height;
                    } else {
                        child.SetBounds (offset, area.Top, size.Width, area.Height);
                        offset += size.Width;
                    }
                }
            } finally {
                _inRowLayout = false;
            }
        }
    }

    public partial class ToolBar
    {
        // A strip is as tall as its tallest item and as wide as its items laid end to end.
        //
        // The base Control implementation reports the explicitly-set bounds, which left a strip
        // stuck at whatever its container handed it — so buttons sized for image-above-text got
        // squashed, because StackLayoutEngine gives every item the strip's client height.
        internal override Size GetPreferredSizeCore (Size proposedSize)
        {
            var specified = base.GetPreferredSizeCore (proposedSize);
            var items = Items?.Cast<MenuItem> ().Where (i => i is not null).ToList ();

            if (items is null || items.Count == 0)
                return specified;

            var width = 0;
            var height = 0;

            foreach (var item in items) {
                var size = item.GetPreferredSize (Size.Empty);

                width += size.Width + item.Margin.Horizontal;
                height = Math.Max (height, size.Height + item.Margin.Vertical);
            }

            // Never shrink below the explicitly-set box: a designer-assigned Size stays a floor.
            return new Size (
                Math.Max (specified.Width, width + Padding.Horizontal),
                Math.Max (specified.Height, height + Padding.Vertical));
        }
    }

    public partial class ToolStripItem
    {
        /// <inheritdoc/>
        public override Size GetPreferredSize (Size proposedSize)
        {
            // WinForms treats AutoSize=false plus an explicit Size as a fixed item box. The
            // renderer measures text instead, which ignored a designer/host-assigned button size
            // (e.g. a 150x64 image-above-text button) and collapsed it to its caption width.
            if (!AutoSize && Size.Width > 0 && Size.Height > 0)
                return Size;

            return base.GetPreferredSize (proposedSize);
        }
    }
}
