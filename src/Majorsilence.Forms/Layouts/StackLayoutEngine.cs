using System.Drawing;

namespace Majorsilence.Forms
{
    internal sealed class StackLayoutEngine
    {
        public static StackLayoutEngine Horizontal = new StackLayoutEngine (Orientation.Horizontal);
        public static StackLayoutEngine HorizontalExpand = new StackLayoutEngine (Orientation.Horizontal, true);
        public static StackLayoutEngine Vertical = new StackLayoutEngine (Orientation.Vertical);
        public static StackLayoutEngine VerticalExpand = new StackLayoutEngine (Orientation.Vertical, true);

        private readonly Orientation orientation;
        private readonly bool expand;

        public StackLayoutEngine (Orientation orientation = Orientation.Vertical, bool expand = false)
        {
            this.orientation = orientation;
            this.expand = expand;
        }

        public void Layout (Rectangle bounds, IEnumerable<ILayoutable> items)
        {
            if (orientation == Orientation.Horizontal)
                LayoutHorizontal (bounds, items);
            else
                LayoutVertical (bounds, items);
        }

        private void LayoutHorizontal (Rectangle bounds, IEnumerable<ILayoutable> items)
        {
            var x = bounds.Left;
            var y = bounds.Top;

            foreach (var item in items) {
                var local_x = x + item.Margin.Left;
                var local_y = y + item.Margin.Top;
                var height = bounds.Height - item.Margin.Vertical;

                var size = item.GetPreferredSize (Size.Empty);

                // Upstream's split stack stretches only an auto-sized strip item from edge to edge; one
                // with AutoSize off keeps its own height, centred in the strip (ToolStripSplitStackLayout).
                // Stretched, an 80px button on a strip laid out before it grew lost its height for good,
                // its Size being its bounds.
                if (expand && IsFixedSize (item)) {
                    height = size.Height;
                    local_y = bounds.Top + (bounds.Height - height) / 2;
                }

                item.SetBounds (local_x, local_y, size.Width, height);

                x += size.Width + item.Margin.Horizontal;
            }
        }

        private void LayoutVertical (Rectangle bounds, IEnumerable<ILayoutable> items)
        {
            var x = bounds.Left;
            var y = bounds.Top;

            foreach (var item in items) {
                var local_x = x + item.Margin.Left;
                var local_y = y + item.Margin.Top;
                var width = bounds.Width - item.Margin.Horizontal;

                var size = item.GetPreferredSize (Size.Empty);

                // As across: a fixed-size item keeps its width, centred.
                if (expand && IsFixedSize (item)) {
                    width = size.Width;
                    local_x = bounds.Left + (bounds.Width - width) / 2;
                }

                item.SetBounds (local_x, local_y, width, size.Height);

                y += size.Height + item.Margin.Vertical;
            }
        }

        // ToolStripItem.GetPreferredSize answers an AutoSize = false item's own Size.
        private static bool IsFixedSize (ILayoutable item)
            => item is ToolStripItem { AutoSize: false, Size: { Width: > 0, Height: > 0 } };
    }
}
