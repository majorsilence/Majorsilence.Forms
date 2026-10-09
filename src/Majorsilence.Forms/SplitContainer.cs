using System.Drawing;
using Majorsilence.Forms.Renderers;
using SkiaSharp;

namespace Majorsilence.Forms
{
    /// <summary>
    /// Represents a SplitContainer control.
    /// </summary>
    public partial class SplitContainer : Control, System.ComponentModel.ISupportInitialize
    {
        // WinForms' SplitContainer starts at _splitterDistance = 50. Ours used to inherit whatever
        // Panel's own default width happened to be, so "restore the saved distance, else use the
        // default" code landed somewhere else than it does on Windows (LAY-08).
        private const int DefaultSplitterDistance = 50;

        private readonly Splitter splitter;
        // Vertical, matching WinForms' default and its meaning: the splitter bar is vertical, so the
        // panels sit side by side. This control used to read the enum as the direction of the layout
        // rather than of the bar, so the same arrangement was called Horizontal.
        private Orientation orientation = Orientation.Vertical;
        private int panel1_min_size = 25;
        private int panel2_min_size = 25;
        // The client extent along the split axis as of the last layout. FixedPanel needs it to know
        // how much the container just grew or shrank by: -1 means nothing has been measured yet, so
        // the first pass records instead of redistributing (LAY-02).
        private int last_client_extent = -1;
        // Whether the drag in progress has actually moved the split, so that SplitterMoved fires once
        // at the end of a real drag and not at all for a press-and-release (LAY-03).
        private bool split_moved_by_drag;

        /// <summary>
        /// Initializes a new instance of the SplitContainer class.
        /// </summary>
        public SplitContainer ()
        {
            // No Dock assignment here. WinForms' SplitContainer inherits DockStyle.None, and the
            // designer's default for one is Anchor plus an explicit Location and Size, which a forced
            // Fill silently overrode: the control took the whole form (LAY-08).
            Panel2 = Controls.Add (new SplitterPanel (this) { Dock = DockStyle.Fill });

            // ResizesTarget off: the legacy Splitter resizes the sibling it is docked against, which
            // here is Panel1, and this container does that arithmetic itself against
            // Panel1MinSize/Panel2MinSize. Left on, every drag would move the split twice.
            splitter = Controls.Add (new Splitter { SplitterWidth = 5, ResizesTarget = false });
            Panel1 = Controls.Add (new SplitterPanel (this) {
                Dock = DockStyle.Left,
                Width = DefaultSplitterDistance,
            });

            splitter.Drag += Splitter_Drag;
            splitter.MouseDown += Splitter_MouseDown;
            splitter.MouseUp += Splitter_MouseUp;
        }

        /// <inheritdoc/>
        protected override Size DefaultSize => new Size (150, 100);

        /// <summary>
        /// The default <see cref="ControlStyle"/> for all <see cref="SplitContainer"/> instances. Gives the type
        /// its own layer in the style chain so a CSS theme rule (<c>SplitContainer { ... }</c>) can target it.
        /// </summary>
        public new static readonly ControlStyle DefaultStyle = new ControlStyle (Control.DefaultStyle);

        /// <inheritdoc/>
        public override ControlStyle Style { get; } = new ControlStyle (DefaultStyle);

        // WinForms designer-generated InitializeComponent code always brackets a SplitContainer's
        // property assignments with ((ISupportInitialize)(this.splitContainer1)).BeginInit()/
        // EndInit() -- explicit no-op implementations (matching NumericUpDown/DataGridView's own)
        // so that cast succeeds instead of throwing InvalidCastException. Found via a real migrated
        // designer app (ReportDesigner.Forms) crashing on every dialog containing a SplitContainer
        // (DialogDatabase, DataSetsCtl, DialogExprEditor, RdlUserControl, SQLCtl -- File > New alone
        // hits DialogDatabase).
        //
        // They are not no-ops any more: a SplitterDistance assigned between them is applied again at
        // EndInit, as upstream defers it, against the size the designer has given the container by then.
        void System.ComponentModel.ISupportInitialize.BeginInit () => initializing = true;

        void System.ComponentModel.ISupportInitialize.EndInit ()
        {
            initializing = false;

            if (pending_distance >= 0) {
                var distance = pending_distance;
                pending_distance = -1;
                SetSplitterDistance (distance, force: true);
            }
        }

        private bool initializing;
        private int pending_distance = -1;

        // Where the split sits as a share of the container, kept from the last time it was placed on
        // purpose (in code, by a drag or by the keyboard), as upstream keeps _ratioWidth/_ratioHeight.
        // -1 until it is known. A resize recomputes the distance from it rather than from the distance
        // the previous resize left: that compounded every clamp, so a container measured once at an
        // interim size never got its split back -- ReportDesigner's New Report SQL tab, a 203 of 612
        // split, opened with the table tree taking 755 of 786 and the SQL box squeezed off the edge.
        private double split_ratio = -1;

        // Places the split and remembers where, as a share of the container.
        private void PlaceSplit (int distance)
        {
            ResizePanels (distance);

            var extent = ClientExtent;
            split_ratio = extent > 0 && !initializing ? (double) SplitterDistance / extent : -1;
        }

        // Calculates the size of Panel1.
        private int GetMaximumPanel1Size ()
            // This is the maximum Panel1 size taking Panel2MinSize into account.
            => ClientExtent - SplitterWidth - panel2_min_size;

        // The container's own usable extent along the split axis, in the same unscaled units the
        // panels' Width and Height are in.
        private int ClientExtent => Splitter.UnscaledClientExtent (this, orientation == Orientation.Vertical);

        /// <inheritdoc/>
        protected override void OnPaint (PaintEventArgs e)
        {
            base.OnPaint (e);

            RenderManager.Render (this, e);
        }

        /// <inheritdoc/>
        /// <remarks>Redistributes a change in the container's own size between the two panels
        /// according to <see cref="FixedPanel"/>, before the dock walk rather than after it: the
        /// panels' bounds come out of that walk, so the new distance has to already be in place
        /// (LAY-02).</remarks>
        protected override void OnLayout (LayoutEventArgs e)
        {
            ApplyFixedPanel ();

            base.OnLayout (e);
        }

        /// <summary>
        /// Gets or sets the orientation of the splitter.
        /// </summary>
        /// <remarks>
        /// As in WinForms, this is the direction of the splitter <em>bar</em>, not of the layout:
        /// <see cref="Orientation.Vertical"/> means a vertical bar with the panels side by side, and
        /// is the default. Earlier versions of this control read the enum the other way round.
        /// </remarks>
        public Orientation Orientation {
            get => orientation;
            set {
                if (orientation != value) {
                    orientation = value;

                    SuspendLayout ();

                    splitter.Orientation = orientation;
                    Panel1.Dock = orientation == Orientation.Vertical ? DockStyle.Left : DockStyle.Top;
                    Panel1.Size = new Size (Panel1.Height, Panel1.Width);

                    // The extent recorded for FixedPanel was measured along the OTHER axis, so it is
                    // not a size change to redistribute. Forget it and let the next layout re-measure
                    // (LAY-02).
                    last_client_extent = -1;

                    ResumeLayout (true);

                    // LAY-10: the transposed Panel1 carries the same pixel distance across, but nothing
                    // re-validated it against the new axis, so a 300px split flipped into a 100px-tall
                    // container squashed Panel2 to nothing. Upstream's setter (Layout/Containers/
                    // SplitContainer.cs, Orientation) zeroes _splitDistance and re-assigns
                    // SplitterDistance, which re-clamps against Panel1MinSize/Panel2MinSize and
                    // raises SplitterMoved.
                    SetSplitterDistance (SplitterDistance, force: true);
                }
            }
        }

        /// <summary>
        /// Gets the left or top panel, depending on orientation.
        /// </summary>
        /// <remarks>Typed as <see cref="SplitterPanel"/>, as WinForms types it: designer-generated and
        /// migrated code declares these panels by that name, and a <see cref="Panel"/> made
        /// <c>SplitterPanel p = sc.Panel1;</c> fail to compile (LAY-07).</remarks>
        public SplitterPanel Panel1 { get; }

        /// <summary>
        /// Gets or sets the minimum size Panel1 can be set to.
        /// </summary>
        /// <remarks>Majorsilence.Forms name for <see cref="Panel1MinSize"/>, which is what WinForms
        /// calls the same clamp. Both read and write the one value.</remarks>
        public int Panel1MinimumSize {
            get => Panel1MinSize;
            set => Panel1MinSize = value;
        }

        /// <summary>
        /// Gets the right or bottom panel, depending on orientation.
        /// </summary>
        /// <remarks>Typed as <see cref="SplitterPanel"/> for the reason
        /// <see cref="SplitContainer.Panel1"/> gives (LAY-07).</remarks>
        public SplitterPanel Panel2 { get; }

        /// <summary>
        /// Gets or sets the minimum size Panel2 can be set to.
        /// </summary>
        /// <remarks>Majorsilence.Forms name for <see cref="Panel2MinSize"/>, which is what WinForms
        /// calls the same clamp. Both read and write the one value.</remarks>
        public int Panel2MinimumSize {
            get => Panel2MinSize;
            set => Panel2MinSize = value;
        }

        // Updates the size of Panel1 to resize and move all controls.
        private void ResizePanels (int value)
        {
            // GetMaximumPanel1Size can come out below panel1_min_size in a container too small to
            // honour both minimums; Clamp lets the minimum win, which keeps Panel1 usable rather than
            // collapsing it.
            var clamped = value.Clamp (panel1_min_size, GetMaximumPanel1Size ());

            if (orientation == Orientation.Vertical)
                Panel1.Width = clamped;
            else
                Panel1.Height = clamped;
        }

        /// <summary>
        /// Gets or sets the color of the splitter.
        /// </summary>
        public System.Drawing.Color SplitterColor {
            get => splitter.Style.GetBackgroundColor ().ToDrawingColor ();
            set => splitter.Style.BackgroundColor = value.ToSKColor ();
        }

        /// <summary>
        /// Gets or sets the width of the splitter.
        /// </summary>
        public int SplitterWidth {
            get => splitter.SplitterWidth;
            set => splitter.SplitterWidth = value;
        }

        /// <summary>Gets or sets the distance in pixels from the left or top edge to the splitter.</summary>
        /// <remarks>A value below <see cref="Panel1MinSize"/>, or one that would leave Panel2 smaller
        /// than <see cref="Panel2MinSize"/>, is moved to the nearest allowed position, and a change raises
        /// <see cref="SplitterMoved"/>, as upstream does.</remarks>
        /// <exception cref="ArgumentOutOfRangeException">The value is negative.</exception>
        public int SplitterDistance {
            // Vertical docks Panel1 to the left, so its Width is the distance.
            get => orientation == Orientation.Vertical ? Panel1.Width : Panel1.Height;
            set => SetSplitterDistance (value, force: false);
        }

        // LAY-11, after upstream's SplitterDistance setter (Layout/Containers/SplitContainer.cs): a
        // negative value throws, anything else is clamped to the two minimums, and every assignment that
        // asked for a different distance raises SplitterMoved -- programmatic moves included, which is how
        // listeners that persist the layout see a restore. Upstream compares the requested value, not the
        // clamped one, so an assignment clamped back to where the splitter already was still raises.
        // Upstream additionally throws InvalidOperationException when the container is too small to honour
        // both minimums; here the minimum wins instead (see ResizePanels), because a container whose layout
        // has not run yet is routinely that small.
        private void SetSplitterDistance (int value, bool force)
        {
            if (!force && value == SplitterDistance)
                return;

            if (value < 0)
                throw new ArgumentOutOfRangeException (nameof (SplitterDistance), value, string.Format (Majorsilence.Forms.Layout.SR.InvalidLowBoundArgumentEx, nameof (SplitterDistance), value, 0));

            if (initializing)
                pending_distance = value;

            PlaceSplit (value);

            var rect = SplitterRectangle;
            OnSplitterMoved (new SplitterEventArgs (rect.X + rect.Width / 2, rect.Y + rect.Height / 2, rect.X, rect.Y));
        }

        /// <summary>Gets or sets which panel keeps its size when the container is resized.</summary>
        /// <remarks>
        /// This was stored and never read, and because Panel1 is docked with a fixed extent the
        /// control behaved as though <see cref="FixedPanel.Panel1"/> were permanently set: maximising
        /// a form put every extra pixel into Panel2, and an app that asked for
        /// <see cref="FixedPanel.Panel2"/> got the exact opposite. The default,
        /// <see cref="FixedPanel.None"/>, keeps the split proportional (LAY-02).
        /// </remarks>
        public FixedPanel FixedPanel { get; set; } = FixedPanel.None;

        /// <summary>Gets or sets whether Panel1 is collapsed. Stub in Majorsilence.Forms.</summary>
        public bool Panel1Collapsed {
            get => !Panel1.Visible;
            set => Panel1.Visible = !value;
        }

        /// <summary>Gets or sets whether Panel2 is collapsed. Stub in Majorsilence.Forms.</summary>
        public bool Panel2Collapsed {
            get => !Panel2.Visible;
            set => Panel2.Visible = !value;
        }

        /// <summary>Gets or sets whether the splitter can be moved by the user.</summary>
        public bool IsSplitterFixed { get; set; }

        /// <summary>Gets or sets the minimum size, in pixels, of Panel1. Negative values are coerced to 0.</summary>
        /// <remarks>This is the clamp the splitter honours. It used to be a plain auto-property that
        /// nothing read, with the working minimum kept under the Majorsilence-only name
        /// <see cref="Panel1MinimumSize"/>, so a designer's <c>splitContainer1.Panel1MinSize = 150</c>
        /// was accepted and the splitter still dragged down to the hard-coded 25 (LAY-01).</remarks>
        public int Panel1MinSize {
            get => panel1_min_size;
            set {
                panel1_min_size = Math.Max (0, value);

                // Re-clamp where the split already is: a minimum raised past the current distance has
                // to push the splitter out, as it does upstream.
                ResizePanels (SplitterDistance);
            }
        }

        /// <summary>Gets or sets the minimum size, in pixels, of Panel2. Negative values are coerced to 0.</summary>
        /// <remarks>The counterpart of <see cref="Panel1MinSize"/>, and inert in the same way before
        /// (LAY-01).</remarks>
        public int Panel2MinSize {
            get => panel2_min_size;
            set {
                panel2_min_size = Math.Max (0, value);

                ResizePanels (SplitterDistance);
            }
        }

        /// <summary>Gets or sets the step, in logical pixels, a drag moves the splitter by.</summary>
        /// <remarks>Real as of W6 mechanisms: a drag is rounded down to a whole number of steps from
        /// where it began, as upstream's <c>SplitMove</c> does, so a container asking for a 10-pixel
        /// increment snaps to a grid instead of following the pointer exactly.</remarks>
        public int SplitterIncrement {
            get => splitter_increment;
            set => splitter_increment = Math.Max (1, value);
        }

        private int splitter_increment = 1;

        // Where the splitter sat when the current drag began, so the increment is measured from the
        // start of the drag rather than from the previous move (upstream measures the same way).
        private int drag_origin_distance = -1;


        /// <summary>Raised when the splitter has finished being moved.</summary>
        public event EventHandler<SplitterEventArgs>? SplitterMoved;

        /// <summary>Raised while the splitter is being moved. Setting
        /// <see cref="System.ComponentModel.CancelEventArgs.Cancel"/> abandons the move.</summary>
        public event EventHandler<SplitterCancelEventArgs>? SplitterMoving;

        // Redistributes a change in the container's own size between the two panels per FixedPanel,
        // mirroring upstream's OnResize/SetSplitterRect (LAY-02).
        private void ApplyFixedPanel ()
        {
            var extent = ClientExtent;

            // The constructor lays out while adding the panels, before Panel1 exists.
            if (extent <= 0 || Panel1 is null)
                return;

            if (last_client_extent > 0 && extent != last_client_extent) {
                var distance = FixedPanel switch {
                    // Panel1 keeps its size, so the whole delta lands on Panel2 and the distance does
                    // not move.
                    FixedPanel.Panel1 => SplitterDistance,
                    // Panel2 keeps its size, so the whole delta lands on Panel1.
                    FixedPanel.Panel2 => SplitterDistance + (extent - last_client_extent),
                    // Neither is fixed: keep the share the split was given (split_ratio), not the one the
                    // last resize happened to leave.
                    _ => split_ratio >= 0
                        ? (int)Math.Round (split_ratio * extent)
                        : (int)Math.Round ((double)SplitterDistance * extent / last_client_extent),
                };

                ResizePanels (distance);
            }

            // The first real measurement fixes the share a split placed before it was known.
            if (split_ratio < 0 && !initializing)
                split_ratio = (double) SplitterDistance / extent;

            last_client_extent = extent;
        }

        // Handles the splitter's Drag event.
        // Test seam: the drag gesture without a mouse. The splitter's own drag path needs a laid-out,
        // hit-tested bar; what these tests are about is whether IsSplitterFixed is consulted.
        internal void DriveSplitterDrag (Point delta) => Splitter_Drag (this, new EventArgs<Point> (delta));

        private void Splitter_Drag (object? sender, EventArgs<Point> e)
        {
            // IsSplitterFixed pins the splitter: the panels can still be resized programmatically, the
            // user just cannot drag the bar. It was stored and read by nothing, so a container the
            // application had deliberately locked dragged like any other.
            if (IsSplitterFixed)
                return;

            var vertical = orientation == Orientation.Vertical;
            var before = SplitterDistance;

            if (drag_origin_distance < 0)
                drag_origin_distance = before;

            var proposed = before - (vertical
                ? (int)(e.Value.X / ScaleFactor.Width)
                : (int)(e.Value.Y / ScaleFactor.Height));

            // SplitterIncrement (W6 mechanisms): the move is rounded down to whole steps measured from
            // where this drag started.
            if (splitter_increment > 1)
                proposed -= (proposed - drag_origin_distance) % splitter_increment;

            // LAY-03: SplitterMoving is cancellable in WinForms and a handler may rewrite SplitX or
            // SplitY to steer the split elsewhere. Both events were declared here, and the drag path
            // resized the panel and called Invalidate() without raising either, so an app that
            // vetoed a drag or persisted the position on SplitterMoved did nothing at all.
            var cursor = splitter.LastDragScreenLocation;
            var moving = new SplitterCancelEventArgs (cursor.X, cursor.Y,
                vertical ? proposed : 0, vertical ? 0 : proposed);

            OnSplitterMoving (moving);

            if (moving.Cancel)
                return;

            PlaceSplit (vertical ? moving.SplitX : moving.SplitY);

            if (SplitterDistance != before)
                split_moved_by_drag = true;

            Invalidate ();
        }

        // LAY-06: pressing the bar focuses the container, which is what upstream's "splitter focused"
        // state is (Layout/Containers/SplitContainer.cs, OnMouseDown sets _splitterFocused and makes the
        // container the active control), so the arrow keys move the bar after a click. The bar itself is
        // not selectable (Splitter), as upstream draws it rather than hosting a control.
        private void Splitter_MouseDown (object? sender, MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Left && e.Clicks == 1 && !IsSplitterFixed)
                Focus ();
        }

        // Keyboard move state (LAY-06). A held arrow key is one move: it begins on the first key-down,
        // each auto-repeat moves the bar again, and the key-up ends it -- upstream's _splitBegin/_splitMove.
        private bool key_split_active;
        private int key_split_origin;

        // Upstream's IsSplitterMovable: a container too small to honour both minimums does not move.
        private bool IsSplitterMovable
            => Panel1.Visible && Panel2.Visible && ClientExtent >= panel1_min_size + SplitterWidth + panel2_min_size;

        private static bool IsArrowKey (Keys keyData)
            => keyData is Keys.Left or Keys.Right or Keys.Up or Keys.Down;

        /// <inheritdoc/>
        /// <remarks>With the splitter focused the unmodified arrow keys are not dialog keys: they reach
        /// <see cref="OnKeyDown"/> and move the bar, as upstream's override arranges
        /// (Layout/Containers/SplitContainer.cs, ProcessDialogKey) (LAY-06).</remarks>
        protected override bool ProcessDialogKey (Keys keyData)
        {
            if (Focused && IsArrowKey (keyData))
                return false;

            return base.ProcessDialogKey (keyData);
        }

        /// <inheritdoc/>
        /// <remarks>Moves the splitter by <see cref="SplitterIncrement"/> per arrow key while the
        /// container has focus and <see cref="IsSplitterFixed"/> is off; Escape abandons a move in
        /// progress. Upstream previews the move with a reversible line and applies it on key-up; here the
        /// panels follow the bar live, as they do for a mouse drag, and <see cref="SplitterMoved"/> is
        /// still raised once, on key-up (LAY-06).</remarks>
        protected override void OnKeyDown (KeyEventArgs e)
        {
            base.OnKeyDown (e);

            if (IsSplitterFixed || !IsSplitterMovable)
                return;

            if (e.KeyData == Keys.Escape && key_split_active) {
                EndKeyboardSplit (accept: false);
                e.Handled = true;
                return;
            }

            if (!Focused || !IsArrowKey (e.KeyData))
                return;

            var repeat = key_split_active;

            if (!key_split_active) {
                key_split_active = true;
                key_split_origin = SplitterDistance;
            }

            // Upstream's arithmetic (OnKeyDown): a step that would cross a minimum is not taken at all,
            // rather than clamped part of the way.
            var distance = SplitterDistance;

            if (e.KeyData is Keys.Left or Keys.Up) {
                distance -= splitter_increment;

                if (distance < panel1_min_size)
                    distance += splitter_increment;

                distance = Math.Max (distance, 0);
            } else {
                distance += splitter_increment;

                if (distance + SplitterWidth > ClientExtent - panel2_min_size)
                    distance -= splitter_increment;
            }

            // Upstream raises SplitterMoving for the repeats of a held key, not for the press that
            // starts the move, and a cancel there abandons the whole move.
            if (repeat) {
                var vertical = orientation == Orientation.Vertical;
                var bar = SplitterRectangle;
                var moving = new SplitterCancelEventArgs (Left + bar.X + bar.Width / 2, Top + bar.Y + bar.Height / 2,
                    vertical ? distance : 0, vertical ? 0 : distance);

                OnSplitterMoving (moving);

                if (moving.Cancel) {
                    EndKeyboardSplit (accept: false);
                    e.Handled = true;
                    return;
                }
            }

            PlaceSplit (distance);
            e.Handled = true;
        }

        /// <inheritdoc/>
        /// <remarks>Releasing the arrow key ends a keyboard move and raises <see cref="SplitterMoved"/>
        /// when the bar ended somewhere new, as upstream's <c>OnKeyUp</c> does (LAY-06).</remarks>
        protected override void OnKeyUp (KeyEventArgs e)
        {
            base.OnKeyUp (e);

            if (key_split_active && IsArrowKey (e.KeyData))
                EndKeyboardSplit (accept: true);
        }

        private void EndKeyboardSplit (bool accept)
        {
            key_split_active = false;

            if (!accept) {
                PlaceSplit (key_split_origin);
                return;
            }

            if (SplitterDistance == key_split_origin)
                return;

            var rect = SplitterRectangle;
            OnSplitterMoved (new SplitterEventArgs (rect.X + rect.Width / 2, rect.Y + rect.Height / 2, rect.X, rect.Y));
        }

        /// <inheritdoc/>
        /// <remarks>Repaints the bar, which carries the focus cue (LAY-06).</remarks>
        protected override void OnGotFocus (EventArgs e)
        {
            base.OnGotFocus (e);
            splitter.Invalidate ();
        }

        /// <inheritdoc/>
        /// <remarks>Repaints the bar to drop the focus cue, and finishes a keyboard move the focus change
        /// interrupted, since its panels have already moved (LAY-06).</remarks>
        protected override void OnLostFocus (EventArgs e)
        {
            base.OnLostFocus (e);

            if (key_split_active)
                EndKeyboardSplit (accept: true);

            splitter.Invalidate ();
        }

        // LAY-03: WinForms raises SplitterMoved once the drag finishes, which is where applications
        // persist the layout the user has just chosen, and not on every intermediate move.
        private void Splitter_MouseUp (object? sender, MouseEventArgs e)
        {
            // The next drag measures its increment from wherever the splitter ends up.
            drag_origin_distance = -1;

            if (!split_moved_by_drag)
                return;

            split_moved_by_drag = false;

            var rect = SplitterRectangle;

            OnSplitterMoved (new SplitterEventArgs (e.ScreenLocation.X, e.ScreenLocation.Y, rect.X, rect.Y));
        }
    }
}
