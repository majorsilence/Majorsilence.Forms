using System.Drawing;
using Majorsilence.Forms.Printing;

namespace Majorsilence.Forms
{
    /// <summary>
    /// Represents a dialog for configuring print settings.
    /// Simplified implementation — shows no UI, returns OK immediately.
    /// </summary>
    public class PrintDialog : Form
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
    public class PrintPreviewDialog : Form
    {
        /// <summary>Gets or sets the PrintDocument to preview.</summary>
        public PrintDocument? Document { get; set; }

        /// <summary>Gets or sets whether to use anti-aliasing in the preview. Stub in Majorsilence.Forms.</summary>
        public bool UseAntiAlias { get; set; } = true;

        /// <inheritdoc/>
        public new DialogResult ShowDialog ()
        {
            if (Document != null) {
                var pdf = Document.Print ();
                System.Diagnostics.Process.Start (new System.Diagnostics.ProcessStartInfo (pdf) { UseShellExecute = true });
            }

            return DialogResult.OK;
        }
    }

    /// <summary>
    /// Represents a dialog for configuring page setup (margins, orientation, paper size).
    /// Stub implementation — shows no UI, returns OK immediately.
    /// </summary>
    public class PageSetupDialog : Form
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
    public class PrintPreviewControl : Control
    {
        /// <summary>Gets or sets the PrintDocument to preview.</summary>
        public PrintDocument? Document { get; set; }

        /// <summary>Gets or sets the zoom level (1.0 = 100%).</summary>
        public double Zoom { get; set; } = 0.3;

        /// <inheritdoc/>
        public override ControlStyle Style { get; } = new ControlStyle (Control.DefaultStyle);
    }
}
