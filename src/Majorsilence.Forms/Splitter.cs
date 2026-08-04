using System;
using System.Drawing;
using Majorsilence.Forms.Renderers;

namespace Majorsilence.Forms
{
    /// <summary>
    /// Represents a Splitter control.
    /// </summary>
    public class Splitter : Control
    {
        // Vertical: the constructor docks left and shows the east-west cursor, which is a vertical
        // bar. This used to be called Horizontal, the opposite of SplitContainer's new reading and of
        // WinForms'.
        private Orientation orientation = Orientation.Vertical;
        private bool is_dragging;
        private Point drag_start_point;
        private Point? last_drag_point;
        private int min_size = 25;
        private int min_extra = 25;

        /// <summary>
        /// Initializes a new instance of the Splitter class.
        /// </summary>
        public Splitter ()
        {
            Dock = DockStyle.Left;
            Cursor = Cursors.SizeWestEast;
        }

        /// <inheritdoc/>
        public new static ControlStyle DefaultStyle = new ControlStyle (Control.DefaultStyle);

        /// <summary>Gets or sets the border style of the splitter. Stub.</summary>
        public BorderStyle BorderStyle { get; set; }

        /// <summary>
        /// Raised when the user drags the Splitter.
        /// </summary>
        public event EventHandler<EventArgs<Point>>? Drag;

        /// <inheritdoc/>
        public override ControlStyle Style { get; } = new ControlStyle (DefaultStyle);

        /// <summary>
        /// Raises the Drag event.
        /// </summary>
        protected void OnDrag (EventArgs<Point> e) => Drag?.Invoke (this, e);

        /// <inheritdoc/>
        protected override void OnMouseDown (MouseEventArgs e)
        {
            base.OnMouseDown (e);

            is_dragging = true;
            drag_start_point = e.ScreenLocation;
        }

        /// <inheritdoc/>
        protected override void OnMouseMove (MouseEventArgs e)
        {
            base.OnMouseMove (e);

            if (is_dragging) {
                last_drag_point ??= drag_start_point;

                var current = e.ScreenLocation;
                OnDrag (new EventArgs<Point> (new Point (last_drag_point.Value.X - current.X, last_drag_point.Value.Y - current.Y)));

                last_drag_point = current;
            }
        }

        /// <inheritdoc/>
        protected override void OnMouseUp (MouseEventArgs e)
        {
            base.OnMouseUp (e);

            is_dragging = false;
            last_drag_point = null;
        }

        /// <inheritdoc/>
        protected override void OnPaint (PaintEventArgs e)
        {
            base.OnPaint (e);

            RenderManager.Render (this, e);
        }

        /// <summary>
        /// Gets or sets the orientation of the splitter bar.
        /// </summary>
        /// <remarks><see cref="Orientation.Vertical"/> is a vertical bar: docked to the left edge and
        /// dragged east to west. Matches <see cref="SplitContainer.Orientation"/>.</remarks>
        public Orientation Orientation {
            get => orientation;
            set {
                if (orientation != value) {
                    orientation = value;

                    Size = new Size (Height, Width);
                    Dock = orientation == Orientation.Vertical ? DockStyle.Left : DockStyle.Top;
                    Cursor = orientation == Orientation.Vertical ? Cursors.SizeWestEast : Cursors.SizeNorthSouth;
                }
            }
        }

        /// <summary>
        /// Gets or sets the width of the splitter.
        /// </summary>
        public int SplitterWidth {
            get => orientation == Orientation.Vertical ? Width : Height;
            set {
                if (orientation == Orientation.Vertical)
                    Width = value;
                else
                    Height = value;
            }
        }

        /// <summary>Gets or sets the current position of the splitter. Alias for SplitterWidth. Stub in Majorsilence.Forms.</summary>
        public int SplitPosition { get => SplitterWidth; set => SplitterWidth = value; }

        /// <summary>Gets or sets the minimum size of the first panel. Negative values are coerced to 0. Stub in Majorsilence.Forms.</summary>
        public int MinSize {
            get => min_size;
            set => min_size = Math.Max (0, value);
        }

        /// <summary>Gets or sets the minimum remaining space after the splitter. Negative values are coerced to 0. Stub in Majorsilence.Forms.</summary>
        public int MinExtra {
            get => min_extra;
            set => min_extra = Math.Max (0, value);
        }

        /// <summary>Raised when the splitter is moved. Stub in Majorsilence.Forms.</summary>
        public event EventHandler<SplitterEventArgs>? SplitterMoved { add { } remove { } }

        /// <summary>Raised while the splitter is being moved. Stub in Majorsilence.Forms.</summary>
        public event EventHandler<SplitterCancelEventArgs>? SplitterMoving { add { } remove { } }
    }
}
