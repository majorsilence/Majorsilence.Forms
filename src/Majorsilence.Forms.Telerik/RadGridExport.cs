using Majorsilence.Forms.Printing;

namespace Majorsilence.Forms.Telerik
{
    /// <summary>
    /// Shared, store-and-approximate export options set on every <see cref="RadGridView"/> exporter
    /// (<see cref="GridViewSpreadExport"/>, <see cref="ExportToExcelML"/>, <see cref="ExportToCSV"/>,
    /// <see cref="ExportToHTML"/>, <see cref="GridViewPdfExport"/>). Honored where the grid model actually
    /// supports it (<see cref="HiddenColumnOption"/>, <see cref="SummariesExportOption"/>); the pure
    /// visual-fidelity flags (<see cref="ExportVisualSettings"/>, <see cref="ExportHierarchy"/>) are stored
    /// but ignored — Majorsilence.Forms's exporters always emit plain data (no Telerik theme/element-tree
    /// styling to reproduce) and hierarchy/master-detail export has no compat grid tree to walk (see
    /// <c>RadGridView</c>'s flat, structural-row model). This mirrors the compat layer's stated philosophy:
    /// compile-and-approximate, not pixel-perfect (see the header comment in RadInfrastructure.cs).
    /// </summary>
    public abstract class GridExportBase
    {
        private protected GridExportBase (RadGridView grid) => Grid = grid;

        /// <summary>Gets the grid being exported.</summary>
        protected RadGridView Grid { get; }

        /// <summary>Gets or sets the file extension used when a caller doesn't supply one. Stored, not enforced.</summary>
        public string FileExtension { get; set; } = string.Empty;

        /// <summary>Gets or sets which summary rows are included in the export.</summary>
        public SummariesOption SummariesExportOption { get; set; } = SummariesOption.ExportAll;

        /// <summary>Gets or sets whether visual settings (theme colors/fonts) are exported. Stored but ignored — the compat exporters always emit plain data.</summary>
        public bool ExportVisualSettings { get; set; }

        /// <summary>Gets or sets whether hierarchical (master-detail) structure is exported. Stored but ignored — see <see cref="GridExportBase"/> remarks.</summary>
        public bool ExportHierarchy { get; set; }

        /// <summary>Gets or sets whether hidden columns are included in the export.</summary>
        public HiddenOption HiddenColumnOption { get; set; } = HiddenOption.DoNotExport;

        /// <summary>Gets or sets which pages are exported. Majorsilence.Forms's exporters always export the full (filtered/sorted) view; stored for API compatibility.</summary>
        public PagingExportOption PagingExportOption { get; set; } = PagingExportOption.AllPages;

        // Shared column selection: visible columns, plus hidden ones too when HiddenColumnOption asks for it.
        // Internal (not private protected) so the sibling RadGridExportCore class — not a derived type — can
        // call it; both live in this same file/assembly and are conceptually one implementation unit.
        internal List<DataGridViewColumn> ExportColumns ()
        {
            var cols = new List<DataGridViewColumn> ();
            foreach (DataGridViewColumn c in Grid.Columns)
                if (c.Visible || HiddenColumnOption != HiddenOption.DoNotExport)
                    cols.Add (c);
            return cols;
        }

        // Exposes RadGridView's internal formatted-display lookup (format strings, combo FK->display text)
        // to the sibling RadGridExportCore class.
        internal string GridDisplay (DataGridViewRow row, int columnIndex) => Grid.GetCellDisplay (row, columnIndex);

        // Shared row selection honoring SummariesExportOption (structural summary rows are otherwise excluded).
        // RadGridView.Rows hides the base DataGridView.Rows with a GridViewRowInfoCollection that already
        // filters out structural rows, so the underlying DataGridView.Rows (via the DataGridView upcast) is
        // used here instead — export needs to see (and selectively include) summary rows itself.
        internal List<DataGridViewRow> ExportRows ()
        {
            var rows = new List<DataGridViewRow> ();
            foreach (DataGridViewRow row in ((DataGridView)Grid).Rows) {
                if (RadGridView.IsSummaryRow (row)) {
                    if (SummariesExportOption == SummariesOption.DoNotExport)
                        continue;
                    // GridSummaryRow.Level 0 rows are appended after all data by RadGridView (grand totals);
                    // treat those as "bottom" and everything else (group footers) as "top" for the
                    // ExportOnlyTop/ExportOnlyBottom split.
                    var isBottom = IsGrandTotalRow (row);
                    if (SummariesExportOption == SummariesOption.ExportOnlyTop && isBottom)
                        continue;
                    if (SummariesExportOption == SummariesOption.ExportOnlyBottom && !isBottom)
                        continue;
                    rows.Add (row);
                    continue;
                }

                if (RadGridView.IsStructuralRow (row))
                    continue; // group headers, detail rows, the new-row placeholder: never exported.

                rows.Add (row);
            }
            return rows;
        }

        private static bool IsGrandTotalRow (DataGridViewRow row) => row.Tag is GridSummaryRow { Level: 0 };
    }

    /// <summary>
    /// Telerik-compat spreadsheet exporter. <see cref="RunExport(string, object?)"/> writes an .xlsx
    /// workbook via <see cref="RadGridXlsxExport"/> — the same dependency-free writer backing
    /// <see cref="RadGridView.ExportToXlsx(string, string, bool)"/>.
    /// </summary>
    public sealed class GridViewSpreadExport : GridExportBase
    {
        /// <summary>Initializes a new instance exporting the specified grid.</summary>
        public GridViewSpreadExport (RadGridView grid) : base (grid) { }

        /// <summary>Exports to the specified file using the given renderer. The renderer parameter is accepted for API compatibility; Majorsilence.Forms has a single .xlsx writer.</summary>
        public void RunExport (string fileName, object? renderer = null)
            => File.WriteAllBytes (fileName, RadGridExportCore.BuildXlsx (this));
    }

    /// <summary>Marker renderer for <see cref="GridViewSpreadExport.RunExport(string, object?)"/>. Majorsilence.Forms has a single .xlsx writer, so this carries no configuration.</summary>
    public sealed class SpreadExportRenderer
    {
    }

    /// <summary>
    /// Telerik-compat Excel-ML (SpreadsheetML XML) exporter. Produces a real, minimal SpreadsheetML
    /// document (Excel opens <c>.xml</c>/<c>.xls</c> files in this format directly) — dependency-free, no
    /// zip archive required (unlike the Open XML <c>.xlsx</c> format <see cref="GridViewSpreadExport"/> uses).
    /// </summary>
    public sealed class ExportToExcelML : GridExportBase
    {
        /// <summary>Initializes a new instance exporting the specified grid.</summary>
        public ExportToExcelML (RadGridView grid) : base (grid) { }

        /// <summary>Exports to the specified file.</summary>
        public void RunExport (string fileName) => File.WriteAllText (fileName, RadGridExportCore.BuildExcelMl (this));
    }

    /// <summary>Telerik-compat CSV exporter. Reuses <see cref="RadGridView.ExportToCsv(bool)"/>'s escaping, honoring column visibility and summary-row options.</summary>
    public sealed class ExportToCSV : GridExportBase
    {
        /// <summary>Initializes a new instance exporting the specified grid.</summary>
        public ExportToCSV (RadGridView grid) : base (grid) { }

        /// <summary>Exports to the specified file.</summary>
        public void RunExport (string fileName) => File.WriteAllText (fileName, RadGridExportCore.BuildCsv (this));
    }

    /// <summary>Telerik-compat HTML table exporter.</summary>
    public sealed class ExportToHTML : GridExportBase
    {
        /// <summary>Initializes a new instance exporting the specified grid.</summary>
        public ExportToHTML (RadGridView grid) : base (grid) { }

        /// <summary>Exports to the specified file.</summary>
        public void RunExport (string fileName) => File.WriteAllText (fileName, RadGridExportCore.BuildHtml (this));
    }

    /// <summary>
    /// Telerik-compat PDF exporter. Renders the grid as a paginated PDF via
    /// <see cref="Majorsilence.Forms.Printing.PrintDocument"/> — one row of column headers repeated at the
    /// top of every page (when <see cref="ShowHeaderAndFooter"/> is set, a caption band above that), rows
    /// laid out top to bottom, and a new page started whenever the next row would not fit. This is a
    /// compile-and-approximate pagination, not the Telerik layout engine: column widths are scaled from the
    /// grid's own pixel widths, but page-break placement, exact spacing, and multi-line cell wrapping are
    /// approximations rather than a pixel-perfect match.
    /// </summary>
    public sealed class GridViewPdfExport : GridExportBase
    {
        /// <summary>Initializes a new instance exporting the specified grid.</summary>
        public GridViewPdfExport (RadGridView grid) : base (grid) { }

        /// <summary>Gets or sets whether a header/footer caption band is drawn on each page.</summary>
        public bool ShowHeaderAndFooter { get; set; }

        /// <summary>Gets or sets the height, in points, of the header band.</summary>
        public float HeaderHeight { get; set; } = 20f;

        /// <summary>Gets or sets the font used to draw the header/middle-header text.</summary>
        public Majorsilence.Forms.Drawing.Font HeaderFont { get; set; } = new Majorsilence.Forms.Drawing.Font ("Arial", 10);

        /// <summary>Gets or sets the text centered in the header band.</summary>
        public string MiddleHeader { get; set; } = string.Empty;

        /// <summary>Gets or sets whether the header is mirrored (drawn right-to-left) on even pages. Stored but ignored — Majorsilence.Forms's PDF pipeline does not implement facing-page mirroring.</summary>
        public bool ReverseHeaderOnEvenPages { get; set; }

        /// <summary>Gets or sets whether columns are scaled to fit within the page width.</summary>
        public bool FitToPageWidth { get; set; } = true;

        /// <summary>Gets or sets an additional uniform scale factor applied to the exported content.</summary>
        public float Scale { get; set; } = 1f;

        /// <summary>Exports to the specified file using the given renderer. The renderer parameter is accepted for API compatibility; Majorsilence.Forms has a single PDF pipeline.</summary>
        public void RunExport (string fileName, object? renderer = null)
        {
            using var stream = File.Create (fileName);
            RadGridExportCore.BuildPdf (this).PrintToPdf (stream);
        }
    }

    /// <summary>Marker renderer for <see cref="GridViewPdfExport.RunExport(string, object?)"/>. Majorsilence.Forms has a single PDF writer, so this carries no configuration.</summary>
    public sealed class PdfExportRenderer
    {
    }

    /// <summary>Specifies which summary rows a grid exporter includes. Compat for Telerik SummariesOption.</summary>
    public enum SummariesOption
    {
        /// <summary>Export all summary rows (top and bottom).</summary>
        ExportAll = 0,
        /// <summary>Export only group-header ("top") summary rows.</summary>
        ExportOnlyTop = 1,
        /// <summary>Export only grand-total ("bottom") summary rows.</summary>
        ExportOnlyBottom = 2,
        /// <summary>Export no summary rows.</summary>
        DoNotExport = 3
    }

    /// <summary>Specifies how a grid exporter treats hidden columns. Compat for Telerik HiddenOption.</summary>
    public enum HiddenOption
    {
        /// <summary>Hidden columns are not exported.</summary>
        DoNotExport = 0,
        /// <summary>Hidden columns are exported alongside visible ones.</summary>
        Export = 1
    }

    /// <summary>Specifies which pages a grid exporter includes. Compat for Telerik PagingExportOption.</summary>
    public enum PagingExportOption
    {
        /// <summary>Export every page of data (Majorsilence.Forms always exports the full filtered/sorted view).</summary>
        AllPages = 0,
        /// <summary>Export only the current page. No-op in Majorsilence.Forms — paging is a UI-only concept here.</summary>
        CurrentPage = 1
    }

    /// <summary>
    /// Specifies how a bound column's value is displayed for export/editor purposes (e.g. a foreign-key
    /// column showing its raw value vs. its looked-up display text). Compat for Telerik
    /// <c>Telerik.WinControls.UI.Export.DisplayFormatType</c> — referenced only as an enum-binder
    /// <c>GetType()</c> target in migrated designer code (see <c>frmWebImport.Designer.vb</c>), so no
    /// behavior is implemented beyond providing the enum's shape.
    /// </summary>
    public enum DisplayFormatType
    {
        /// <summary>The underlying (unformatted) value.</summary>
        Value = 0,
        /// <summary>The formatted display text.</summary>
        DisplayText = 1
    }

    // Shared rendering logic for the exporters above, kept out of the small per-format public types so each
    // one stays a thin Telerik-shaped facade.
    internal static class RadGridExportCore
    {
        public static byte[] BuildXlsx (GridExportBase export)
        {
            var cols = export.ExportColumns ();
            var headers = cols.Select (c => string.IsNullOrEmpty (c.HeaderText) ? c.Name : c.HeaderText).ToList ();

            var rows = new List<IReadOnlyList<RadGridXlsxExport.Cell>> ();
            foreach (var row in export.ExportRows ()) {
                var values = new List<RadGridXlsxExport.Cell> (cols.Count);
                foreach (var c in cols)
                    values.Add (new RadGridXlsxExport.Cell (CellText (export, row, c), false));
                rows.Add (values);
            }

            return RadGridXlsxExport.Build ("Sheet1", headers, rows);
        }

        public static string BuildCsv (GridExportBase export)
        {
            var cols = export.ExportColumns ();
            var lines = new List<string> {
                string.Join (",", cols.Select (c => CsvEscape (string.IsNullOrEmpty (c.HeaderText) ? c.Name : c.HeaderText)))
            };

            foreach (var row in export.ExportRows ())
                lines.Add (string.Join (",", cols.Select (c => CsvEscape (CellText (export, row, c)))));

            return string.Join ("\r\n", lines);
        }

        public static string BuildHtml (GridExportBase export)
        {
            var cols = export.ExportColumns ();
            var sb = new System.Text.StringBuilder ();
            sb.Append ("<html><head><meta charset=\"utf-8\"></head><body><table border=\"1\">");

            sb.Append ("<thead><tr>");
            foreach (var c in cols)
                sb.Append ("<th>").Append (HtmlEscape (string.IsNullOrEmpty (c.HeaderText) ? c.Name : c.HeaderText)).Append ("</th>");
            sb.Append ("</tr></thead><tbody>");

            foreach (var row in export.ExportRows ()) {
                sb.Append ("<tr>");
                foreach (var c in cols)
                    sb.Append ("<td>").Append (HtmlEscape (CellText (export, row, c))).Append ("</td>");
                sb.Append ("</tr>");
            }

            sb.Append ("</tbody></table></body></html>");
            return sb.ToString ();
        }

        // Excel 2003 SpreadsheetML — a plain-text XML format Excel opens natively, distinct from the
        // zip-based Open XML .xlsx format RadGridXlsxExport produces.
        public static string BuildExcelMl (GridExportBase export)
        {
            var cols = export.ExportColumns ();
            var sb = new System.Text.StringBuilder ();
            sb.Append ("<?xml version=\"1.0\"?>");
            sb.Append ("<?mso-application progid=\"Excel.Sheet\"?>");
            sb.Append ("<Workbook xmlns=\"urn:schemas-microsoft-com:office:spreadsheet\" ");
            sb.Append ("xmlns:ss=\"urn:schemas-microsoft-com:office:spreadsheet\">");
            sb.Append ("<Worksheet ss:Name=\"Sheet1\"><Table>");

            sb.Append ("<Row>");
            foreach (var c in cols)
                sb.Append ("<Cell><Data ss:Type=\"String\">").Append (XmlEscape (string.IsNullOrEmpty (c.HeaderText) ? c.Name : c.HeaderText)).Append ("</Data></Cell>");
            sb.Append ("</Row>");

            foreach (var row in export.ExportRows ()) {
                sb.Append ("<Row>");
                foreach (var c in cols)
                    sb.Append ("<Cell><Data ss:Type=\"String\">").Append (XmlEscape (CellText (export, row, c))).Append ("</Data></Cell>");
                sb.Append ("</Row>");
            }

            sb.Append ("</Table></Worksheet></Workbook>");
            return sb.ToString ();
        }

        public static PrintDocument BuildPdf (GridViewPdfExport export)
        {
            var cols = export.ExportColumns ();
            var rows = export.ExportRows ();

            var doc = new PrintDocument { DocumentName = "grid-export" };
            var pageSettings = doc.DefaultPageSettings;
            var scale = export.Scale <= 0 ? 1f : export.Scale;

            // Column widths in PDF points, scaled from the grid's pixel widths. FitToPageWidth compresses
            // (never stretches) an overly wide grid down to the printable width; Scale is an additional
            // caller-supplied multiplier layered on top.
            var pixelWidths = cols.Select (c => (float)Math.Max (c.Width, 20)).ToArray ();
            var totalPixelWidth = pixelWidths.Sum ();
            var printableWidth = pageSettings.EffectiveWidthHundredths / 100f * 72f
                - pageSettings.Margins.Left / 100f * 72f - pageSettings.Margins.Right / 100f * 72f;

            var widthScale = export.FitToPageWidth && totalPixelWidth > 0 && totalPixelWidth * 0.75f > printableWidth
                ? printableWidth / (totalPixelWidth * 0.75f)
                : 1f;

            var colWidths = pixelWidths.Select (w => w * 0.75f * widthScale * scale).ToArray (); // px -> pt (96dpi) then scaled

            const float rowHeight = 16f;
            var headerBandHeight = export.ShowHeaderAndFooter ? Math.Max (export.HeaderHeight, rowHeight) : 0f;

            var rowIndex = 0;

            doc.PrintPage += (_, e) => {
                var g = e.Graphics;
                var top = e.MarginBounds.Top;

                if (export.ShowHeaderAndFooter && !string.IsNullOrEmpty (export.MiddleHeader)) {
                    var headerRect = new System.Drawing.RectangleF (e.MarginBounds.Left, top, e.MarginBounds.Width, headerBandHeight);
                    g.DrawString (export.MiddleHeader, export.HeaderFont, Majorsilence.Forms.Drawing.Brushes.Black, headerRect, Majorsilence.Forms.ContentAlignment.MiddleCenter);
                    top += headerBandHeight;
                }

                var headerFont = new Majorsilence.Forms.Drawing.Font (export.HeaderFont.FamilyName, export.HeaderFont.Size, bold: true);
                DrawRow (g, cols.Select (c => string.IsNullOrEmpty (c.HeaderText) ? c.Name : c.HeaderText).ToArray (),
                    colWidths, e.MarginBounds.Left, top, rowHeight, headerFont);
                top += rowHeight;

                while (rowIndex < rows.Count && top + rowHeight <= e.MarginBounds.Bottom) {
                    var row = rows[rowIndex];
                    var values = cols.Select (c => CellText (export, row, c)).ToArray ();
                    DrawRow (g, values, colWidths, e.MarginBounds.Left, top, rowHeight, export.HeaderFont);
                    top += rowHeight;
                    rowIndex++;
                }

                e.HasMorePages = rowIndex < rows.Count;
            };

            return doc;
        }

        private static void DrawRow (Drawing.SkiaGraphics g, string[] values, float[] widths, float left, float top, float height, Majorsilence.Forms.Drawing.Font font)
        {
            var x = left;
            for (var i = 0; i < values.Length && i < widths.Length; i++) {
                var rect = new System.Drawing.RectangleF (x, top, widths[i], height);
                g.DrawString (values[i], font, Majorsilence.Forms.Drawing.Brushes.Black, rect, Majorsilence.Forms.ContentAlignment.MiddleLeft);
                x += widths[i];
            }
        }

        private static string CellText (GridExportBase export, DataGridViewRow row, DataGridViewColumn col)
        {
            if (row.Tag is GridSummaryRow summary)
                return summary.Values.TryGetValue (col.Index, out var text) ? text : string.Empty;
            return export.GridDisplay (row, col.Index);
        }

        private static readonly char[] CsvSpecialChars = [',', '"', '\r', '\n'];

        private static string CsvEscape (string value)
        {
            value ??= string.Empty;
            return value.IndexOfAny (CsvSpecialChars) >= 0
                ? "\"" + value.Replace ("\"", "\"\"") + "\""
                : value;
        }

        private static string HtmlEscape (string value) => System.Net.WebUtility.HtmlEncode (value ?? string.Empty);

        private static string XmlEscape (string value) => (value ?? string.Empty)
            .Replace ("&", "&amp;").Replace ("<", "&lt;").Replace (">", "&gt;").Replace ("\"", "&quot;");
    }

    /// <summary>
    /// Telerik-compat message box, used by the grid export dialogs (<c>frmRadGridExport.vb</c>) to report
    /// export success/failure and confirm opening the exported file. Wraps
    /// <see cref="Majorsilence.Forms.MessageBox"/>, mapping the Telerik-specific <see cref="RadMessageIcon"/>
    /// onto the equivalent <see cref="MessageBoxIcon"/>; <see cref="SetThemeName(string)"/> is a stored no-op
    /// (Majorsilence.Forms's message box has no theme concept).
    /// </summary>
    public static class RadMessageBox
    {
        /// <summary>Gets the theme name most recently set via <see cref="SetThemeName(string)"/>. No-op stub — Majorsilence.Forms's message box has no theme concept.</summary>
        public static string ThemeName { get; private set; } = string.Empty;

        /// <summary>Sets the theme used by subsequent <see cref="Show(string)"/> calls. No-op stub.</summary>
        public static void SetThemeName (string themeName) => ThemeName = themeName;

        /// <summary>Shows a message box with the specified text.</summary>
        public static DialogResult Show (string text) => Majorsilence.Forms.MessageBox.Show (text);

        /// <summary>Shows a message box with the specified text and caption.</summary>
        public static DialogResult Show (string text, string caption) => Majorsilence.Forms.MessageBox.Show (text, caption);

        /// <summary>Shows a message box with the specified text, caption, and buttons.</summary>
        public static DialogResult Show (string text, string caption, MessageBoxButtons buttons)
            => Majorsilence.Forms.MessageBox.Show (text, caption, buttons);

        /// <summary>Shows a message box with the specified text, caption, buttons, and icon.</summary>
        public static DialogResult Show (string text, string caption, MessageBoxButtons buttons, RadMessageIcon icon)
            => Majorsilence.Forms.MessageBox.Show (text, caption, buttons, ToMessageBoxIcon (icon));

        /// <summary>Shows a message box owned by the specified form, with text, caption, buttons, and icon.</summary>
        public static DialogResult Show (Form owner, string text, string caption, MessageBoxButtons buttons, RadMessageIcon icon)
            => Majorsilence.Forms.MessageBox.Show (owner, text, caption, buttons, ToMessageBoxIcon (icon));

        /// <summary>Shows a message box owned by the specified window, with text, caption, buttons, and icon.</summary>
        public static DialogResult Show (IWin32Window owner, string text, string caption, MessageBoxButtons buttons, RadMessageIcon icon)
            => Majorsilence.Forms.MessageBox.Show (owner, text, caption, buttons, ToMessageBoxIcon (icon));

        private static MessageBoxIcon ToMessageBoxIcon (RadMessageIcon icon) => icon switch {
            RadMessageIcon.Error => MessageBoxIcon.Error,
            RadMessageIcon.Warning => MessageBoxIcon.Warning,
            RadMessageIcon.Info => MessageBoxIcon.Information,
            RadMessageIcon.Question => MessageBoxIcon.Question,
            _ => MessageBoxIcon.None,
        };
    }

    /// <summary>Specifies the icon shown by <see cref="RadMessageBox"/>. Compat for Telerik RadMessageIcon.</summary>
    public enum RadMessageIcon
    {
        /// <summary>No icon.</summary>
        None = 0,
        /// <summary>Informational icon.</summary>
        Info = 1,
        /// <summary>Warning icon.</summary>
        Warning = 2,
        /// <summary>Error icon.</summary>
        Error = 3,
        /// <summary>Question icon.</summary>
        Question = 4
    }
}
