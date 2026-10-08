using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using Majorsilence.Forms.Layout;

namespace Majorsilence.Forms
{
    // Row layout for ToolStripPanel, plus the two sizing rules it depends on.
    //
    // Real WinForms hosts every strip added to a ToolStripPanel on a ToolStripPanelRow, stacks the rows
    // across the panel's minor axis, and ignores the child's Dock while doing it. That is what makes the
    // classic "menu bar on top, toolbar underneath" arrangement work when a menu and a toolbar are both
    // added to the same edge panel of a ToolStripContainer.
    //
    // Without row layout the edge panel was a plain docked Panel: the first child to claim the edge
    // won, the other collapsed to nothing, and only one strip was ever visible. Found via a migrated
    // app whose module windows put a MainMenu and a toolbar in TopToolStripPanel and rendered only
    // the toolbar, squashed to menu height.
    //
    // The rows are the model (TSM-36). A row holds as many strips as were joined to it, laid side by
    // side along it, as upstream's ToolStripPanelRow.HorizontalRowManager lays its cells; a strip that
    // arrives through Controls.Add rather than Join gets a row of its own, which is what the designer's
    // one-strip-per-location code produces upstream. Before, the layout ignored the rows altogether and
    // stacked Controls in order, so Join (strip, row) recorded a row nothing read.
    public partial class ToolStripPanel
    {
        // Re-entrancy guard: positioning children dirties layout, which can call back into
        // OnLayout. One arrangement pass per layout is enough.
        private bool _inRowLayout;

        // Brings the rows in line with Controls: a strip that left the panel leaves its row, a row left
        // empty goes (upstream's ToolStripPanelRow.OnControlRemoved removes it), and a strip that arrived
        // through Controls.Add gets a row of its own. Run lazily, because ControlCollection lays the
        // panel out before it raises ControlAdded, so an event hook would be too late for the first pass.
        private void SyncRows ()
        {
            foreach (var row in rows)
                row.Controls.RemoveAll (strip => !ReferenceEquals (strip.Parent, this));

            rows.RemoveAll (row => row.Controls.Count == 0);

            var unrowed = Controls.OfType<ToolStrip> ().Where (strip => !rows.Any (row => row.Controls.Contains (strip))).ToList ();

            // Menu bars rank above toolbars regardless of the order they were added, matching where a
            // menu sits in every WinForms window. Insertion order is not usable on its own: a container
            // that creates its own toolbar up front (ToolStripContainer subclasses typically do) has it in
            // Controls before the designer adds the menu, which would put the menu underneath. So a menu
            // takes a new row after the menus already at the top, and anything else goes at the bottom;
            // stable within each group, so several toolbars keep their order.
            foreach (var strip in unrowed.OrderBy (s => s is Menu ? 0 : 1)) {
                var row = new ToolStripPanelRow (this);
                row.Controls.Add (strip);

                if (strip is Menu) {
                    var index = 0;

                    while (index < rows.Count && rows[index].Controls.Any (s => s is Menu))
                        index++;

                    rows.Insert (index, row);
                } else {
                    rows.Add (row);
                }
            }
        }

        /// <inheritdoc/>
        /// <remarks>The panel's background goes through its <see cref="ToolStripRenderer"/> first --
        /// <see cref="Renderer"/>, else the one <see cref="RenderMode"/> names -- and a handler that marks
        /// <see cref="ToolStripPanelRenderEventArgs.Handled"/> replaces the default fill (W6 mechanisms).</remarks>
        protected override void OnPaintBackground (PaintEventArgs e)
        {
            using var device = e.DeviceSpace ();   // laid out in device pixels (EVT-37)
            var args = new ToolStripPanelRenderEventArgs (e.Graphics, this);
            Renderers.StripRendererBridge.ResolveMode (Renderer, RenderMode).DrawToolStripPanelBackground (args);

            if (args.Handled)
                return;

            base.OnPaintBackground (e);
        }

        // ParticipatesInLayout, not Visible: Visible walks the parent chain and reports false for anything
        // not yet on a shown form (a parentless control returns false outright), which would skip row
        // layout during InitializeComponent -- exactly when designer-built module windows are assembled.
        // This is the same predicate the layout engine itself uses.
        private static bool Participates (Control child) => ((IArrangedElement)child).ParticipatesInLayout;

        // The lines the panel lays out, in order: each row's participating strips, then anything that is
        // not a ToolStrip (a legacy ToolBar, say) on a line of its own, as every child had before. A row
        // whose strips are all hidden takes no space but keeps its place, as upstream's does.
        private List<(ToolStripPanelRow? Row, List<Control> Children)> Lines ()
        {
            SyncRows ();

            var lines = new List<(ToolStripPanelRow?, List<Control>)> ();

            foreach (var row in rows)
                lines.Add ((row, row.Controls.Where (Participates).Cast<Control> ().ToList ()));

            foreach (Control child in Controls)
                if (child is not ToolStrip && Participates (child))
                    lines.Add ((null, new List<Control> { child }));

            return lines;
        }

        // A row is as thick as the strip wants to be. Ask the strip itself: ToolBar reports a
        // height that fits its items (see GetPreferredSizeCore below), so a toolbar of tall
        // image-above-text buttons gets a tall row while a menu bar stays thin.
        // The size a strip's content wants, without the floor of its explicitly set box: what an
        // unstretched strip is given in a row, as an auto-sized ToolStrip is upstream.
        private static Size ContentExtent (Control child)
            => child is ToolBar bar ? bar.ItemsPreferredSize () : child.Size;

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

        // Stretch: a strip that asks for it spans the row; one that does not keeps its preferred extent,
        // as upstream's ToolStrip (Stretch false) does in a panel while a MenuStrip (Stretch true) spans
        // it (W6 mechanisms).
        private static bool Stretched (Control child) => child is not ToolStrip { Stretch: false };

        internal override Size GetPreferredSizeCore (Size proposedSize)
        {
            var lines = Lines ().Where (line => line.Children.Count > 0).ToList ();

            // Matches a childless Panel so an unpopulated edge panel still collapses to nothing.
            if (lines.Count == 0)
                return Size.Empty;

            var horizontal = Orientation == Orientation.Horizontal;
            var across = 0;   // summed along the stacking axis
            var along = 0;    // widest/tallest row

            foreach (var (row, children) in lines) {
                var margin = row?.Margin ?? Padding.Empty;
                var thickness = 0;
                var length = 0;

                foreach (var child in children) {
                    var size = RowPreferredSize (child, proposedSize);

                    thickness = Math.Max (thickness, horizontal ? size.Height : size.Width);
                    length += horizontal ? size.Width : size.Height;
                }

                if (horizontal) {
                    across += thickness + margin.Vertical;
                    along = Math.Max (along, length + margin.Horizontal);
                } else {
                    across += thickness + margin.Horizontal;
                    along = Math.Max (along, length + margin.Vertical);
                }
            }

            // RowMargin surrounds the rows as a whole; each row's own Margin surrounds that row (W6).
            return horizontal
                ? new Size (along + Padding.Horizontal + RowMargin.Horizontal, across + Padding.Vertical + RowMargin.Vertical)
                : new Size (across + Padding.Horizontal + RowMargin.Horizontal, along + Padding.Vertical + RowMargin.Vertical);
        }

        /// <inheritdoc/>
        protected override void OnLayout (LayoutEventArgs e)
        {
            // Let the base raise Layout and run the default pass first; the row arrangement below
            // then overrides the positions, which is what lets us disregard each child's Dock.
            base.OnLayout (e);

            if (_inRowLayout)
                return;

            _inRowLayout = true;

            try {
                var lines = Lines ();

                if (lines.Count == 0)
                    return;

                var area = DeviceClientRectangle;

                area = new Rectangle (
                    area.X + Padding.Left,
                    area.Y + Padding.Top,
                    Math.Max (0, area.Width - Padding.Horizontal),
                    Math.Max (0, area.Height - Padding.Vertical));

                // RowMargin insets the block of rows (W6 mechanisms).
                area = new Rectangle (
                    area.X + RowMargin.Left, area.Y + RowMargin.Top,
                    Math.Max (0, area.Width - RowMargin.Horizontal), Math.Max (0, area.Height - RowMargin.Vertical));

                var horizontal = Orientation == Orientation.Horizontal;
                var offset = horizontal ? area.Top : area.Left;

                foreach (var (row, children) in lines) {
                    var margin = row?.Margin ?? Padding.Empty;

                    if (children.Count == 0) {
                        // Every strip on it is hidden: the row keeps its place in Rows but takes no space.
                        if (row is not null)
                            row.Bounds = horizontal ? new Rectangle (area.Left, offset, area.Width, 0) : new Rectangle (offset, area.Top, 0, area.Height);

                        continue;
                    }

                    var sizes = children.Select (child => RowPreferredSize (child, area.Size)).ToList ();
                    var thickness = sizes.Max (size => horizontal ? size.Height : size.Width);
                    var start = horizontal ? area.Left + margin.Left : area.Top + margin.Top;
                    var end = horizontal ? area.Right - margin.Right : area.Bottom - margin.Bottom;
                    var position = start;

                    for (var i = 0; i < children.Count; i++) {
                        var child = children[i];
                        var available = Math.Max (0, end - position);
                        var content = horizontal ? ContentExtent (child).Width : ContentExtent (child).Height;

                        // The last strip on a row may stretch to its end; one before it keeps its content
                        // extent so the strips after it still have somewhere to go.
                        var extent = Stretched (child) && i == children.Count - 1 ? available : Math.Min (content, available);

                        if (horizontal)
                            child.SetBounds (position, offset + margin.Top, extent, sizes[i].Height);
                        else
                            child.SetBounds (offset + margin.Left, position, sizes[i].Width, extent);

                        position += extent;
                    }

                    if (horizontal) {
                        if (row is not null)
                            row.Bounds = new Rectangle (area.Left, offset, area.Width, thickness + margin.Vertical);

                        offset += thickness + margin.Vertical;
                    } else {
                        if (row is not null)
                            row.Bounds = new Rectangle (offset, area.Top, thickness + margin.Horizontal, area.Height);

                        offset += thickness + margin.Horizontal;
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

            if (Items is null || Items.Count == 0)
                return specified;

            var content = ItemsPreferredSize ();

            // Never shrink below the explicitly-set box: a designer-assigned Size stays a floor.
            return new Size (Math.Max (specified.Width, content.Width), Math.Max (specified.Height, content.Height));
        }

        /// <summary>The size the items alone want, laid end to end, plus padding and the grip band.</summary>
        internal Size ItemsPreferredSize ()
        {
            var width = 0;
            var height = 0;

            foreach (var item in Items.Cast<MenuItem> ().Where (i => i is not null && i.Visible)) {
                var size = item.GetPreferredSize (Size.Empty);

                width += size.Width + item.Margin.Horizontal;
                height = Math.Max (height, size.Height + item.Margin.Vertical);
            }

            return new Size (width + Padding.Horizontal + GripBandWidth, height + Padding.Vertical);
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
