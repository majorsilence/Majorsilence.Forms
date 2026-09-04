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
            splitter.MouseUp += Splitter_MouseUp;
        }

        /// <inheritdoc/>
        protected override Size DefaultSize => new Size (150, 100);

        // WinForms designer-generated InitializeComponent code always brackets a SplitContainer's
        // property assignments with ((ISupportInitialize)(this.splitContainer1)).BeginInit()/
        // EndInit() -- explicit no-op implementations (matching NumericUpDown/DataGridView's own)
        // so that cast succeeds instead of throwing InvalidCastException. Found via a real migrated
        // designer app (ReportDesigner.Forms) crashing on every dialog containing a SplitContainer
        // (DialogDatabase, DataSetsCtl, DialogExprEditor, RdlUserControl, SQLCtl -- File > New alone
        // hits DialogDatabase).
        void System.ComponentModel.ISupportInitialize.BeginInit () { }
        void System.ComponentModel.ISupportInitialize.EndInit () { }

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
        public int SplitterDistance {
            // Vertical docks Panel1 to the left, so its Width is the distance.
            get => orientation == Orientation.Vertical ? Panel1.Width : Panel1.Height;
            set => ResizePanels (value);
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

        /// <summary>Gets or sets the number of pixels the splitter moves when incremented via keyboard. Stub in Majorsilence.Forms.</summary>
        public int SplitterIncrement { get; set; } = 1;

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

            if (extent <= 0)
                return;

            if (last_client_extent > 0 && extent != last_client_extent) {
                var distance = FixedPanel switch {
                    // Panel1 keeps its size, so the whole delta lands on Panel2 and the distance does
                    // not move.
                    FixedPanel.Panel1 => SplitterDistance,
                    // Panel2 keeps its size, so the whole delta lands on Panel1.
                    FixedPanel.Panel2 => SplitterDistance + (extent - last_client_extent),
                    // Neither is fixed: keep the proportion the split had. Scaling the distance by the
                    // extent ratio is the same thing as upstream's stored _ratioWidth/_ratioHeight,
                    // without a second field to keep in step.
                    _ => (int)Math.Round ((double)SplitterDistance * extent / last_client_extent),
                };

                ResizePanels (distance);
            }

            last_client_extent = extent;
        }

        // Handles the splitter's Drag event.
        private void Splitter_Drag (object? sender, EventArgs<Point> e)
        {
            var vertical = orientation == Orientation.Vertical;
            var before = SplitterDistance;
            var proposed = before - (vertical
                ? (int)(e.Value.X / ScaleFactor.Width)
                : (int)(e.Value.Y / ScaleFactor.Height));

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

            ResizePanels (vertical ? moving.SplitX : moving.SplitY);

            if (SplitterDistance != before)
                split_moved_by_drag = true;

            Invalidate ();
        }

        // LAY-03: WinForms raises SplitterMoved once the drag finishes, which is where applications
        // persist the layout the user has just chosen, and not on every intermediate move.
        private void Splitter_MouseUp (object? sender, MouseEventArgs e)
        {
            if (!split_moved_by_drag)
                return;

            split_moved_by_drag = false;

            var rect = SplitterRectangle;

            OnSplitterMoved (new SplitterEventArgs (e.ScreenLocation.X, e.ScreenLocation.Y, rect.X, rect.Y));
        }
    }
}
