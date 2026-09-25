using System.Drawing;
using Majorsilence.Forms.Printing;

namespace Majorsilence.Forms
{
    /// <summary>
    /// Represents a dialog for configuring print settings.
    /// Simplified implementation — shows no UI, returns OK immediately.
    /// </summary>
    public partial class PrintDialog : Form
    {
        /// <summary>Gets or sets the PrintDocument to configure.</summary>
        public PrintDocument? Document { get; set; }

        /// <summary>Gets or sets the printer settings.</summary>
        public PrinterSettings PrinterSettings {
            get => Document?.PrinterSettings ?? new PrinterSettings ();
            set {
                if (Document != null)
                    Document.PrinterSettings = value;
            }
        }

        /// <summary>Gets or sets whether the print-to-file option is shown.</summary>
        public bool AllowPrintToFile { get; set; } = true;

        /// <summary>Gets or sets whether the page-range controls are enabled.</summary>
        public bool AllowSomePages { get; set; }

        /// <summary>Gets or sets whether the "Selection" print-range option is enabled. Stub in Majorsilence.Forms (this whole dialog shows no real UI).</summary>
        public bool AllowSelection { get; set; }

        /// <summary>Shows the print dialog and returns OK (stub — no UI is displayed).</summary>
        public new DialogResult ShowDialog () => DialogResult.OK;
    }

    /// <summary>
    /// Represents a dialog for previewing documents before printing.
    /// Stub implementation — opens the generated PDF file externally.
    /// </summary>
    public partial class PrintPreviewDialog : Form
    {
        /// <summary>Gets or sets the document previewed.</summary>
        public PrintDocument? Document {
            get => PrintPreviewControl.Document;
            set => PrintPreviewControl.Document = value;
        }

        /// <summary>Gets or sets whether the previewed pages are scaled smoothly.</summary>
        /// <remarks>Forwarded to the hosted control as of W6 mechanisms; it used to be stored here and
        /// read nowhere, because the dialog had no preview to apply it to.</remarks>
        public bool UseAntiAlias {
            get => PrintPreviewControl.UseAntiAlias;
            set => PrintPreviewControl.UseAntiAlias = value;
        }

        /// <summary>Shows the preview dialog.</summary>
        /// <remarks>
        /// Real as of W6 mechanisms: the document's pages are captured and shown in the dialog's own
        /// <see cref="PrintPreviewControl"/>. It used to print the document to a PDF and hand that file
        /// to the operating system's handler, which is not a preview at all -- it left a file behind
        /// and returned OK without ever showing anything of this application's. <c>UseWaitCursor</c> is
        /// honoured for the page walk, which is the slow part.
        /// </remarks>
        public new DialogResult ShowDialog ()
        {
            HostPreview ();

            var previous = Cursor;

            if (UseWaitCursor)
                Cursor = Cursors.Wait;

            try {
                PrintPreviewControl.InvalidatePreview ();
            } finally {
                if (UseWaitCursor)
                    Cursor = previous;
            }

            return base.ShowDialog ();
        }

        // The hosted control fills the dialog. Done on show rather than in the constructor because the
        // control is created lazily, and a caller may never ask for a preview at all.
        private void HostPreview ()
        {
            if (ReferenceEquals (PrintPreviewControl.Parent, this))
                return;

            if (string.IsNullOrEmpty (Text))
                Text = "Print preview";

            if (Width < 200 || Height < 200) {
                Width = 720;
                Height = 560;
            }

            PrintPreviewControl.Dock = DockStyle.Fill;
            Controls.Add (PrintPreviewControl);
        }
    }

    /// <summary>Represents a dialog that configures a document's page settings.</summary>
    public partial class PageSetupDialog : Form
    {
        /// <summary>Gets or sets the PrintDocument whose page settings are configured.</summary>
        public PrintDocument? Document { get; set; }

        /// <summary>Gets or sets the page settings.</summary>
        public PageSettings PageSettings {
            get => Document?.DefaultPageSettings ?? new PageSettings ();
            set {
                if (Document != null)
                    Document.DefaultPageSettings = value;
            }
        }

        /// <summary>Gets or sets the printer settings.</summary>
        public PrinterSettings PrinterSettings {
            get => Document?.PrinterSettings ?? new PrinterSettings ();
            set {
                if (Document != null)
                    Document.PrinterSettings = value;
            }
        }

        /// <summary>Gets or sets whether the margins tab is shown.</summary>
        public bool AllowMargins { get; set; } = true;

        /// <summary>Gets or sets whether the orientation tab is shown.</summary>
        public bool AllowOrientation { get; set; } = true;

        /// <summary>Gets or sets whether the paper tab is shown.</summary>
        public bool AllowPaper { get; set; } = true;

        /// <summary>Gets or sets whether the printer button is shown.</summary>
        public bool AllowPrinter { get; set; } = true;

        /// <summary>Shows the page setup dialog and returns OK (stub — no UI is displayed).</summary>
        public new DialogResult ShowDialog () => DialogResult.OK;
    }

    /// <summary>
    /// Represents a control that renders a print-preview of a document.
    /// </summary>
    public partial class PrintPreviewControl : Control
    {
        private PrintDocument? document;
        private PreviewPageInfo[] captured = [];

        /// <summary>Gets or sets the document previewed.</summary>
        /// <remarks>Real as of W6 mechanisms: assigning one runs it through a
        /// <see cref="PreviewPrintController"/> and the captured pages are what this control paints.</remarks>
        public PrintDocument? Document {
            get => document;
            set {
                if (ReferenceEquals (document, value))
                    return;

                document = value;
                RefreshPages ();
            }
        }

        /// <summary>The pages captured from <see cref="Document"/>, in order.</summary>
        internal IReadOnlyList<PreviewPageInfo> Pages => captured;

        /// <summary>
        /// Re-runs the document through a preview controller. <see cref="InvalidatePreview"/> is the
        /// public way to ask for this, as upstream.
        /// </summary>
        private void RefreshPages ()
        {
            captured = [];

            if (document is null)
                return;

            var previous = document.PrintController;
            var preview = new PreviewPrintController { UseAntiAlias = UseAntiAlias };

            try {
                document.PrintController = preview;
                document.RunThroughController (PrintAction.PrintToPreview);
                captured = preview.GetPreviewPageInfo ();
            } catch {
                // A preview must never take down the application: a document whose PrintPage handler
                // throws simply previews nothing.
                captured = [];
            } finally {
                document.PrintController = previous;
            }

            Invalidate ();
        }

        /// <summary>The device rectangle page <paramref name="index"/> of the visible grid occupies.</summary>
        /// <remarks>Empty when that cell holds no page. The grid is <see cref="Rows"/> by
        /// <see cref="Columns"/> starting at <see cref="StartPage"/>, each cell holding one page
        /// scaled by <see cref="Zoom"/> -- or fitted to the cell when <see cref="AutoZoom"/> is on
        /// (W6 mechanisms).</remarks>
        internal Rectangle PageBoundsAt (int index)
        {
            var page = StartPage + index;

            if (index < 0 || page >= captured.Length || Rows < 1 || Columns < 1 || index >= Rows * Columns)
                return Rectangle.Empty;

            var gap = LogicalToDeviceUnits (8);
            var client = ClientRectangle;
            var cell_width = Math.Max (1, (client.Width - (gap * (Columns + 1))) / Columns);
            var cell_height = Math.Max (1, (client.Height - (gap * (Rows + 1))) / Rows);

            var size = captured[page].PhysicalSize;
            var scale = AutoZoom
                ? Math.Min ((double) cell_width / Math.Max (1, size.Width), (double) cell_height / Math.Max (1, size.Height))
                : Zoom * ScaleFactor.Width;

            var width = Math.Max (1, (int) Math.Round (size.Width * scale));
            var height = Math.Max (1, (int) Math.Round (size.Height * scale));
            var column = index % Columns;
            var row = index / Columns;

            // Centred in its cell, as upstream's preview centres a page that does not fill it.
            return new Rectangle (
                client.Left + gap + (column * (cell_width + gap)) + Math.Max (0, (cell_width - width) / 2),
                client.Top + gap + (row * (cell_height + gap)) + Math.Max (0, (cell_height - height) / 2),
                width, height);
        }

        /// <inheritdoc/>
        protected override void OnPaint (PaintEventArgs e)
        {
            base.OnPaint (e);
            Renderers.RenderManager.Render (this, e);
        }

        /// <summary>Gets or sets the zoom level (1.0 = 100%), used when <see cref="AutoZoom"/> is off.</summary>
        /// <remarks>Read as of W6 mechanisms.</remarks>
        public double Zoom {
            get => zoom;
            set {
                if (zoom == value)
                    return;

                zoom = value;
                Invalidate ();
            }
        }

        private double zoom = 0.3;

        /// <inheritdoc/>
        public override ControlStyle Style { get; } = new ControlStyle (Control.DefaultStyle);
    }
}
