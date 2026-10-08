using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;

namespace Majorsilence.Forms
{
    // ListView grouping (LST-46). `Groups` was a collection nothing read: the renderer had no group
    // code and the layout did not group, so an application that built groups got an ungrouped flat
    // list with no error -- every item present, in insertion order, reading as "grouping did nothing"
    // rather than as a failure. Fourteen of the control's twenty stored-only baseline entries were
    // that one fact.
    //
    // A header band occupies exactly one row, which is what keeps the scrolling arithmetic
    // (`top_index`, `ScaledLineHeight`, `VisibleLineCount`, the scrollbar maximum) working unchanged:
    // every line is still one row tall, there are simply more lines. Upstream's bands are taller than
    // a row; matching that means teaching the scroll model about variable line heights, which is a
    // separate job and not what this finding is about.
    public partial class ListView
    {
        /// <summary>A laid-out group header, in device pixels.</summary>
        internal sealed class GroupBand
        {
            internal GroupBand (ListViewGroup group, Rectangle deviceBounds, bool isFooter = false)
            {
                Group = group;
                DeviceBounds = deviceBounds;
                IsFooter = isFooter;
            }

            internal ListViewGroup Group { get; }
            internal Rectangle DeviceBounds { get; }

            /// <summary>Whether this band is the group's footer rather than its header.</summary>
            internal bool IsFooter { get; }
        }

        private readonly List<GroupBand> group_bands = new ();

        /// <summary>The group headers the last layout pass placed, in display order.</summary>
        internal IReadOnlyList<GroupBand> GroupBands => group_bands;

        /// <summary>
        /// Whether this list is currently showing groups: it has some, and <see cref="ShowGroups"/>
        /// has not turned them off.
        /// </summary>
        /// <remarks>
        /// Every grouping path is behind this, so a list that never touched <c>Groups</c> lays out and
        /// paints exactly as it did before.
        /// </remarks>
        internal bool IsGrouped => ShowGroups && Groups.Count > 0;

        /// <summary>
        /// The items in display order: each group's items under that group, then anything left over.
        /// </summary>
        /// <remarks>
        /// The leftovers run last and without a band, which is what upstream's <c>DefaultGroup</c>
        /// amounts to here. Dropping them instead would make assigning groups to *some* items hide the
        /// rest -- a far worse failure than the one being fixed, and the reason this is a run rather
        /// than a filter.
        /// </remarks>
        internal IEnumerable<(ListViewGroup? Group, List<ListViewItem> Items)> GroupRuns ()
        {
            var claimed = new HashSet<ListViewItem> ();

            foreach (var group in Groups) {
                var members = Items.Cast<ListViewItem> ().Where (i => ReferenceEquals (i.Group, group)).ToList ();

                foreach (var member in members)
                    claimed.Add (member);

                yield return (group, members);
            }

            var ungrouped = Items.Cast<ListViewItem> ().Where (i => !claimed.Contains (i)).ToList ();

            if (ungrouped.Count > 0)
                yield return (null, ungrouped);
        }

        // The lines a grouped list lays out: per group a header band, its items in whole lines (a
        // group always starts a fresh line, so a part-filled tile line is a line), and a footer band
        // when it has one; a collapsed group's items take none. It has to agree with LayoutRowsGrouped
        // and LayoutTilesGrouped exactly or the scrollbar and the layout disagree about how far the
        // list runs -- which is what counting collapsed items as lines did (a blank tail you could
        // scroll into). All three read HasFooter and FlowsInGroup, the single places those rules live.
        internal int GroupedLineCount ()
        {
            var per_line = Math.Max (1, ItemsPerLine);
            var lines = 0;

            foreach (var (group, items) in GroupRuns ()) {
                if (group is not null) {
                    lines++;

                    if (group.CollapsedState == ListViewGroupCollapsedState.Collapsed)
                        continue;
                }

                var flowing = items.Count (FlowsInGroup);

                lines += (flowing + per_line - 1) / per_line;

                if (group is not null && HasFooter (group))
                    lines++;
            }

            return lines;
        }

        // An item placed by ListViewItem.Position (tile views, AutoArrange off) sits where it was put
        // and takes no slot in the flow, grouped or not (W6 mechanisms).
        private bool FlowsInGroup (ListViewItem item) => IsRowView || AutoArrange || item.PlacedPosition is null;

        // The line each item was laid out on, for EnsureVisible: grouped, that is not index / per-line.
        private readonly Dictionary<ListViewItem, int> item_lines = new ();

        // A collapsed group shows no footer: the footer belongs to the items, and they are not there.
        private static bool HasFooter (ListViewGroup group)
            => !string.IsNullOrEmpty (group.Footer) && group.CollapsedState != ListViewGroupCollapsedState.Collapsed;

        /// <summary>
        /// The rectangle a group's task link occupies inside its header band, in device pixels.
        /// </summary>
        /// <remarks>
        /// Shared by the renderer that draws the link and the click that raises
        /// <see cref="GroupTaskLinkClick"/>, so the two cannot disagree about where it is -- the
        /// failure mode that makes a link look right and do nothing.
        /// </remarks>
        internal Rectangle GroupTaskLinkBounds (ListViewGroup group, Rectangle band)
        {
            if (string.IsNullOrEmpty (group.TaskLink))
                return Rectangle.Empty;

            var inset = LogicalToDeviceUnits (4);

            // Measured rather than guessed at a fixed width: a short link would otherwise claim a strip
            // of empty header that swallows clicks meant for the caption.
            var width = System.Math.Min (
                LogicalToDeviceUnits (8) + (int)Renderers.ListViewRenderer.MeasureLinkWidth (this, group.TaskLink),
                System.Math.Max (0, band.Width / 2));

            return new Rectangle (band.Right - inset - width, band.Top, width, band.Height);
        }

        /// <summary>The group whose task link covers this device point, or null.</summary>
        internal ListViewGroup? GroupTaskLinkAt (Point location)
        {
            foreach (var band in group_bands) {
                if (band.IsFooter || string.IsNullOrEmpty (band.Group.TaskLink))
                    continue;

                if (GroupTaskLinkBounds (band.Group, band.DeviceBounds).Contains (location))
                    return band.Group;
            }

            return null;
        }

        /// <summary>Re-lays out and repaints after the group set or a group's state changed.</summary>
        internal void RefreshGroups ()
        {
            PerformLayout ();
            Invalidate ();
        }

        private void LayoutRowsGrouped (Rectangle bounds)
        {
            var row_height = ScaledRowHeight;
            var y = bounds.Top - top_index * row_height;

            group_bands.Clear ();
            item_lines.Clear ();

            // Not grouped: lay the items out in order, exactly as before this existed. Checked here
            // rather than at the call site so there is one place that decides, and so a list whose
            // ShowGroups is turned off mid-run drops its stale bands on the next pass.
            if (!IsGrouped) {
                var flat_y = bounds.Top - top_index * row_height;

                foreach (var item in Items) {
                    item.SetBounds (bounds.Left, flat_y, bounds.Width, row_height);
                    LayoutSubItems (item);

                    flat_y += row_height;
                }

                return;
            }

            foreach (var (group, items) in GroupRuns ()) {
                if (group is not null) {
                    group_bands.Add (new GroupBand (group, new Rectangle (bounds.Left, y, bounds.Width, row_height)));
                    y += row_height;

                    if (group.CollapsedState == ListViewGroupCollapsedState.Collapsed) {
                        // Laid out nowhere: an empty rectangle contains no point, so a collapsed item
                        // cannot be clicked or drawn without every hit-test having to know about
                        // collapse separately.
                        foreach (var item in items)
                            item.SetBounds (0, 0, 0, 0);

                        continue;
                    }
                }

                foreach (var item in items) {
                    item_lines[item] = (y - bounds.Top) / row_height + top_index;
                    item.SetBounds (bounds.Left, y, bounds.Width, row_height);
                    LayoutSubItems (item);

                    y += row_height;
                }

                if (group is not null && HasFooter (group)) {
                    group_bands.Add (new GroupBand (group, new Rectangle (bounds.Left, y, bounds.Width, row_height), isFooter: true));
                    y += row_height;
                }
            }
        }

        // The tile views grouped (LST-46's remainder). Upstream groups LargeIcon and Tile as it groups
        // Details: a header over each group's tiles, which start on a fresh line. Each band takes a
        // whole tile line, so the scrolling arithmetic -- top_index, ScaledLineHeight, VisibleLineCount
        // -- still steps by one uniform line, the same simplification the row views make; the band is
        // drawn one text row tall at the FOOT of its line, next to the tiles it heads, so the rest of the
        // line reads as the gap between two groups. A footer band sits at the HEAD of its line, under
        // its group's tiles, for the same reason.
        private void LayoutTilesGrouped (Rectangle bounds)
        {
            var item_width = ScaledTileSize;
            var item_height = ScaledTileHeight;
            var item_margin = LogicalToDeviceUnits (6);
            var stride = item_height + item_margin;
            var band_height = Math.Min (ScaledRowHeight, stride);
            var origin = bounds.Top - top_index * stride;
            var y = origin;

            foreach (var (group, items) in GroupRuns ()) {
                if (group is not null) {
                    group_bands.Add (new GroupBand (group, new Rectangle (bounds.Left, y + stride - band_height, bounds.Width, band_height)));
                    y += stride;

                    if (group.CollapsedState == ListViewGroupCollapsedState.Collapsed) {
                        // Laid out nowhere, as in the row views: an empty rectangle is neither drawn
                        // nor hit.
                        foreach (var item in items)
                            item.SetBounds (0, 0, 0, 0);

                        continue;
                    }
                }

                var x = bounds.Left;
                var line_open = false;

                foreach (var item in items) {
                    if (!FlowsInGroup (item) && item.PlacedPosition is { } placed) {
                        item.SetBounds (LogicalToDeviceUnits (placed.X), LogicalToDeviceUnits (placed.Y) - top_index * stride, item_width, item_height);
                        continue;
                    }

                    // Against the right edge, as the ungrouped flow wraps; the first tile on a line
                    // always goes there, however narrow the list.
                    if (line_open && x + item_width > bounds.Right) {
                        x = bounds.Left;
                        y += stride;
                    }

                    item_lines[item] = (y - origin) / stride;
                    item.SetBounds (x, y, item_width, item_height);
                    x += item_width + item_margin;
                    line_open = true;
                }

                // A group's last line is a line even when it is part-filled: the next group starts
                // below it.
                if (line_open)
                    y += stride;

                if (group is not null && HasFooter (group)) {
                    group_bands.Add (new GroupBand (group, new Rectangle (bounds.Left, y, bounds.Width, band_height), isFooter: true));
                    y += stride;
                }
            }
        }
    }
}
