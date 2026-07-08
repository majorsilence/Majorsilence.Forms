using System.Drawing;
using Majorsilence.Forms.Renderers;
using SkiaSharp;

namespace Majorsilence.Forms
{
    /// <summary>
    /// Represents a SplitContainer control.
    /// </summary>
    public class SplitContainer : Control, System.ComponentModel.ISupportInitialize
    {
        private readonly Splitter splitter;
        private Orientation orientation;
        private int panel1_min_size = 25;
        private int panel2_min_size = 25;

        /// <summary>
        /// Initializes a new instance of the SplitContainer class.
        /// </summary>
        public SplitContainer ()
        {
            Dock = DockStyle.Fill;

            Panel2 = Controls.Add (new Panel { Dock = DockStyle.Fill });
            splitter = Controls.Add (new Splitter { SplitterWidth = 5 });
            Panel1 = Controls.Add (new Panel { Dock = DockStyle.Left });

            splitter.Drag += Splitter_Drag;
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
        {
            // This is the maximum Panel1 size taking the Panel2MinimumSize into account
            if (orientation == Orientation.Horizontal)
                return PaddedClientRectangle.Width - SplitterWidth - panel2_min_size;
            else
                return PaddedClientRectangle.Height - SplitterWidth - panel2_min_size;
        }

        /// <inheritdoc/>
        protected override void OnPaint (PaintEventArgs e)
        {
            base.OnPaint (e);

            RenderManager.Render (this, e);
        }

        /// <summary>
        /// Gets or sets a value indicating the orientation of the SplitContainer.
        /// </summary>
        public Orientation Orientation {
            get => orientation;
            set {
                if (orientation != value) {
                    orientation = value;

                    SuspendLayout ();

                    splitter.Orientation = orientation;
                    Panel1.Dock = orientation == Orientation.Horizontal ? DockStyle.Left : DockStyle.Top;
                    Panel1.Size = new Size (Panel1.Height, Panel1.Width);

                    ResumeLayout (true);
                }
            }
        }

        /// <summary>
        /// Gets the left or top panel, depending on orientation.
        /// </summary>
        public Panel Panel1 { get; }

        /// <summary>
        /// Gets or sets the minimum size Panel1 can be set to.
        /// </summary>
        public int Panel1MinimumSize {
            get => panel1_min_size;
            set {
                panel1_min_size = value;

                ResizePanels (orientation == Orientation.Horizontal ? Panel1.Width : Panel1.Height);
            }
        }

        /// <summary>
        /// Gets the right or bottom panel, depending on orientation.
        /// </summary>
        public Panel Panel2 { get; }

        /// <summary>
        /// Gets or sets the minimum size Panel2 can be set to.
        /// </summary>
        public int Panel2MinimumSize {
            get => panel2_min_size;
            set {
                panel2_min_size = value;

                ResizePanels (orientation == Orientation.Horizontal ? Panel1.Width : Panel1.Height);
            }
        }

        // Updates the size of Panel1 to resize and move all controls.
        private void ResizePanels (int value)
        {
            if (orientation == Orientation.Horizontal)
                Panel1.Width = value.Clamp (panel1_min_size, GetMaximumPanel1Size ());
            else
                Panel1.Height = value.Clamp (panel1_min_size, GetMaximumPanel1Size ());
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
            get => orientation == Orientation.Vertical ? Panel1.Width : Panel1.Height;
            set => ResizePanels (value);
        }

        /// <summary>Gets or sets whether the splitter is fixed. Stub in Majorsilence.Forms.</summary>
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

        /// <summary>Gets or sets the minimum size (in pixels) of Panel1. Stub in Majorsilence.Forms.</summary>
        public int Panel1MinSize { get; set; } = 25;

        /// <summary>Gets or sets the minimum size (in pixels) of Panel2. Stub in Majorsilence.Forms.</summary>
        public int Panel2MinSize { get; set; } = 25;

        /// <summary>Gets or sets the number of pixels the splitter moves when incremented via keyboard. Stub in Majorsilence.Forms.</summary>
        public int SplitterIncrement { get; set; } = 1;

        /// <summary>Raised when the splitter is moved. Stub in Majorsilence.Forms.</summary>
        public event EventHandler<SplitterEventArgs>? SplitterMoved { add { } remove { } }

        /// <summary>Raised while the splitter is being moved. Stub in Majorsilence.Forms.</summary>
        public event EventHandler<SplitterCancelEventArgs>? SplitterMoving { add { } remove { } }

        // Handles the splitter's Drag event.
        private void Splitter_Drag (object? sender, EventArgs<Point> e)
        {
            if (orientation == Orientation.Horizontal)
                ResizePanels (Panel1.Width - (int)(e.Value.X / ScaleFactor.Width));
            else
                ResizePanels (Panel1.Height - (int)(e.Value.Y / ScaleFactor.Height));

            Invalidate ();
        }
    }
}
