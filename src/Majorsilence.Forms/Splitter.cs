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
        // Whether the current drag has actually moved the target. WinForms raises SplitterMoved once,
        // when the drag ends, and not at all for a press-and-release that moved nothing (LAY-03).
        private bool target_moved;
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

        // SplitContainer hosts one of these as its bar but does its own panel arithmetic -- its
        // clamps are Panel1MinSize/Panel2MinSize, not this control's MinSize/MinExtra -- so it turns
        // the legacy resize-the-sibling behaviour off rather than have every drag move the split
        // twice. Upstream's SplitContainer does not use a Splitter at all; it draws its own bar.
        internal bool ResizesTarget { get; set; } = true;

        // The screen position of the most recent drag move. SplitContainer needs the cursor for the
        // MouseCursorX/MouseCursorY of its own SplitterMoving args, and the Drag event carries only a
        // delta.
        internal Point LastDragScreenLocation { get; private set; }

        /// <summary>
        /// Raises the Drag event.
        /// </summary>
        protected void OnDrag (EventArgs<Point> e) => Drag?.Invoke (this, e);

        /// <inheritdoc/>
        protected override void OnMouseDown (MouseEventArgs e)
        {
            base.OnMouseDown (e);

            is_dragging = true;
            target_moved = false;
            drag_start_point = e.ScreenLocation;
        }

        /// <inheritdoc/>
        protected override void OnMouseMove (MouseEventArgs e)
        {
            base.OnMouseMove (e);

            if (!is_dragging)
                return;

            last_drag_point ??= drag_start_point;

            var current = e.ScreenLocation;
            var delta = new Point (last_drag_point.Value.X - current.X, last_drag_point.Value.Y - current.Y);

            last_drag_point = current;
            LastDragScreenLocation = current;

            // LAY-05: resizing the control the bar is docked against is this control's entire
            // purpose, and nothing here used to touch any sibling. A migrated form using the classic
            // Panel(Dock=Left) + Splitter(Dock=Left) + Panel(Dock=Fill) idiom showed a bar with the
            // right cursor that moved nothing at all.
            if (ResizesTarget && !DragTarget (current, delta))
                return;

            OnDrag (new EventArgs<Point> (delta));
        }

        /// <inheritdoc/>
        protected override void OnMouseUp (MouseEventArgs e)
        {
            base.OnMouseUp (e);

            var moved = is_dragging && target_moved;

            is_dragging = false;
            target_moved = false;
            last_drag_point = null;

            // LAY-03: SplitterMoved marks the end of the drag, which is where applications persist
            // the layout they have just been given.
            if (moved)
                OnSplitterMoved (new SplitterEventArgs (e.ScreenLocation.X, e.ScreenLocation.Y, Left, Top));
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

        /// <summary>Gets or sets the size of the control the splitter is docked against.</summary>
        /// <remarks>
        /// As in WinForms this is the extent of the <em>sibling</em> the bar sits next to, clamped by
        /// <see cref="MinSize"/> and <see cref="MinExtra"/>, and <c>-1</c> when there is no such
        /// sibling. It used to be an alias for <see cref="SplitterWidth"/>, so
        /// <c>splitter1.SplitPosition = 200</c> produced a 200 pixel thick bar instead of a 200 pixel
        /// wide panel, and reading it back reported the bar's own width to code restoring a saved
        /// layout (LAY-04).
        /// </remarks>
        public int SplitPosition {
            get {
                var target = FindTarget ();

                return target is null ? -1 : TargetExtent (target);
            }
            set => SetSplitPosition (value, raiseMoved: true);
        }

        /// <summary>Gets or sets the minimum size of the first panel. Negative values are coerced to 0.</summary>
        public int MinSize {
            get => min_size;
            set => min_size = Math.Max (0, value);
        }

        /// <summary>Gets or sets the minimum remaining space after the splitter. Negative values are coerced to 0.</summary>
        public int MinExtra {
            get => min_extra;
            set => min_extra = Math.Max (0, value);
        }

        /// <summary>Raised when the splitter has finished being moved.</summary>
        /// <remarks>This and <see cref="SplitterMoving"/> were declared with empty
        /// <c>add { } remove { }</c> accessors, which silently discarded every handler: the shape
        /// looks wired at compile time and leaks nothing at runtime (LAY-03).</remarks>
        public event EventHandler<SplitterEventArgs>? SplitterMoved;

        /// <summary>Raised while the splitter is being moved. Setting
        /// <see cref="System.ComponentModel.CancelEventArgs.Cancel"/> ends the drag.</summary>
        public event EventHandler<SplitterCancelEventArgs>? SplitterMoving;

        /// <summary>Raises the <see cref="SplitterMoved"/> event.</summary>
        /// <param name="e">The event data.</param>
        protected virtual void OnSplitterMoved (SplitterEventArgs e) => SplitterMoved?.Invoke (this, e);

        /// <summary>Raises the <see cref="SplitterMoving"/> event.</summary>
        /// <param name="e">The event data.</param>
        protected virtual void OnSplitterMoving (SplitterCancelEventArgs e) => SplitterMoving?.Invoke (this, e);

        // True when the bar moves along the x axis, i.e. it is docked to a vertical edge. Upstream's
        // Splitter spells this the same way.
        private bool Horizontal => Dock is DockStyle.Left or DockStyle.Right;

        // The control the bar is docked against, found geometrically: the sibling whose facing edge
        // touches ours. The audit's suggested fix said "the previous sibling by z-order", which is
        // wrong -- the dock walk runs in REVERSE z-order, so the control next to the bar is generally
        // not the one before it in Controls (inside SplitContainer it is the one two places after it).
        // Upstream matches edges for exactly that reason (Splitter.FindTarget).
        internal Control? FindTarget ()
        {
            var parent = Parent;

            if (parent is null)
                return null;

            foreach (var sibling in parent.Controls.GetAllControls ()) {
                if (ReferenceEquals (sibling, this))
                    continue;

                var touches = Dock switch {
                    DockStyle.Left => sibling.Right == Left,
                    DockStyle.Right => sibling.Left == Right,
                    DockStyle.Top => sibling.Bottom == Top,
                    DockStyle.Bottom => sibling.Top == Bottom,
                    _ => false,
                };

                if (touches)
                    return sibling;
            }

            return null;
        }

        // One mouse step of a drag. Returns false when a SplitterMoving handler vetoed it, in which
        // case the drag is over and the Drag event must not be raised either.
        private bool DragTarget (Point mouse, Point delta)
        {
            var target = FindTarget ();

            if (target is null)
                return true;

            var horizontal = Horizontal;
            var position = horizontal ? Left : Top;
            var proposed = position - (horizontal ? delta.X : delta.Y);

            // LAY-03: SplitterMoving is cancellable in WinForms and a handler may also rewrite
            // SplitX/SplitY to steer the bar somewhere else, so it is raised before anything moves
            // and the position it leaves behind is the one applied.
            var moving = new SplitterCancelEventArgs (mouse.X, mouse.Y,
                horizontal ? proposed : Left, horizontal ? Top : proposed);

            OnSplitterMoving (moving);

            if (moving.Cancel) {
                is_dragging = false;
                last_drag_point = null;
                return false;
            }

            // Which way the target grows depends on the edge it is pinned to: a left- or top-docked
            // target grows as the bar moves away from that edge, a right- or bottom-docked one
            // shrinks by the same amount.
            var toward_target = Dock is DockStyle.Left or DockStyle.Top ? 1 : -1;
            var wanted = (horizontal ? moving.SplitX : moving.SplitY) - position;

            SetSplitPosition (TargetExtent (target) + toward_target * wanted, raiseMoved: false);

            return true;
        }

        // Applies a new extent to the docked sibling, bounded by MinSize (how small the target may
        // get) and MinExtra (how much room must be left over for whatever fills the rest). LAY-05.
        private void SetSplitPosition (int value, bool raiseMoved)
        {
            var target = FindTarget ();

            if (target is null)
                return;

            // MinSize wins a fight with MinExtra, as it does upstream: a container too small to
            // satisfy both keeps the target usable rather than collapsing it to nothing.
            value = value.Clamp (min_size, Math.Max (min_size, MaximumTargetExtent (target)));

            if (TargetExtent (target) == value)
                return;

            switch (Dock) {
                case DockStyle.Left:
                    target.Width = value;
                    break;
                case DockStyle.Right:
                    // A right-docked target is pinned by its RIGHT edge, so widening it has to move
                    // its origin as well as its size.
                    target.SetBounds (target.Right - value, target.Top, value, target.Height);
                    break;
                case DockStyle.Top:
                    target.Height = value;
                    break;
                case DockStyle.Bottom:
                    target.SetBounds (target.Left, target.Bottom - value, target.Width, value);
                    break;
                default:
                    return;
            }

            target_moved = true;

            if (raiseMoved)
                OnSplitterMoved (new SplitterEventArgs (Left, Top, Left, Top));
        }

        // The largest the target may become: the parent's client extent along the split axis, less
        // what every other control docked along that axis already claims (this bar included), less
        // MinExtra for the fill area. Mirrors upstream's CalcSplitBounds.
        private int MaximumTargetExtent (Control target)
        {
            var parent = Parent;

            if (parent is null)
                return 0;

            var claimed = 0;

            foreach (var sibling in parent.Controls.GetAllControls ()) {
                if (ReferenceEquals (sibling, target))
                    continue;

                if (Horizontal) {
                    if (sibling.Dock is DockStyle.Left or DockStyle.Right)
                        claimed += sibling.Width;
                } else if (sibling.Dock is DockStyle.Top or DockStyle.Bottom) {
                    claimed += sibling.Height;
                }
            }

            return UnscaledClientExtent (parent, Horizontal) - claimed - min_extra;
        }

        private int TargetExtent (Control target) => Horizontal ? target.Width : target.Height;

        // PaddedClientRectangle is in scaled (device) pixels while Bounds, Width and Height are
        // unscaled, so one cannot be subtracted from the other: at 200% a clamp derived from the
        // client rectangle comes out twice as loose as it should. Shared with SplitContainer, which
        // has the same arithmetic to do.
        internal static int UnscaledClientExtent (Control control, bool horizontal)
        {
            var padded = control.PaddedClientRectangle;
            var extent = horizontal ? padded.Width : padded.Height;
            var scale = horizontal ? control.ScaleFactor.Width : control.ScaleFactor.Height;

            return scale > 0 ? (int)(extent / scale) : extent;
        }
    }
}
