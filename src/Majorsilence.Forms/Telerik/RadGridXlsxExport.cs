using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Text;

namespace Majorsilence.Forms.Telerik
{
    /// <summary>
    /// Minimal, dependency-free Open XML (.xlsx) writer used by <see cref="RadGridView.ExportToXlsx(string, string, bool)"/>.
    /// Produces a single-worksheet workbook with inline-string cells — enough for a faithful data export
    /// that any spreadsheet app opens, without referencing a spreadsheet library.
    /// </summary>
    internal static class RadGridXlsxExport
    {
        private static readonly Encoding Utf8NoBom = new UTF8Encoding (false);

        /// <summary>One exported cell: its text and whether it should be written as a numeric value.</summary>
        internal readonly struct Cell
        {
            public Cell (string text, bool isNumber) { Text = text; IsNumber = isNumber; }
            public string Text { get; }
            public bool IsNumber { get; }
        }

        /// <summary>Builds an .xlsx file (as bytes) from an optional header row and the data rows.</summary>
        public static byte[] Build (string sheetName, IReadOnlyList<string>? headers, IReadOnlyList<IReadOnlyList<Cell>> rows)
        {
            using var ms = new MemoryStream ();

            // ZipArchive must be disposed before reading ms; do it in a nested scope.
            using (var zip = new ZipArchive (ms, ZipArchiveMode.Create, leaveOpen: true)) {
                Write (zip, "[Content_Types].xml", ContentTypes ());
                Write (zip, "_rels/.rels", RootRels ());
                Write (zip, "xl/workbook.xml", Workbook (sheetName));
                Write (zip, "xl/_rels/workbook.xml.rels", WorkbookRels ());
                Write (zip, "xl/worksheets/sheet1.xml", Worksheet (headers, rows));
            }

            return ms.ToArray ();
        }

        private static void Write (ZipArchive zip, string path, string content)
        {
            var entry = zip.CreateEntry (path, CompressionLevel.Optimal);
            using var stream = entry.Open ();
            var bytes = Utf8NoBom.GetBytes (content);
            stream.Write (bytes, 0, bytes.Length);
        }

        private static string ContentTypes () =>
            "<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?>" +
            "<Types xmlns=\"http://schemas.openxmlformats.org/package/2006/content-types\">" +
            "<Default Extension=\"rels\" ContentType=\"application/vnd.openxmlformats-package.relationships+xml\"/>" +
            "<Default Extension=\"xml\" ContentType=\"application/xml\"/>" +
            "<Override PartName=\"/xl/workbook.xml\" ContentType=\"application/vnd.openxmlformats-officedocument.spreadsheetml.sheet.main+xml\"/>" +
            "<Override PartName=\"/xl/worksheets/sheet1.xml\" ContentType=\"application/vnd.openxmlformats-officedocument.spreadsheetml.worksheet+xml\"/>" +
            "</Types>";

        private static string RootRels () =>
            "<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?>" +
            "<Relationships xmlns=\"http://schemas.openxmlformats.org/package/2006/relationships\">" +
            "<Relationship Id=\"rId1\" Type=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships/officeDocument\" Target=\"xl/workbook.xml\"/>" +
            "</Relationships>";

        private static string Workbook (string sheetName) =>
            "<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?>" +
            "<workbook xmlns=\"http://schemas.openxmlformats.org/spreadsheetml/2006/main\" " +
            "xmlns:r=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships\">" +
            "<sheets><sheet name=\"" + XmlEscape (string.IsNullOrEmpty (sheetName) ? "Sheet1" : sheetName) + "\" sheetId=\"1\" r:id=\"rId1\"/></sheets>" +
            "</workbook>";

        private static string WorkbookRels () =>
            "<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?>" +
            "<Relationships xmlns=\"http://schemas.openxmlformats.org/package/2006/relationships\">" +
            "<Relationship Id=\"rId1\" Type=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships/worksheet\" Target=\"worksheets/sheet1.xml\"/>" +
            "</Relationships>";

        private static string Worksheet (IReadOnlyList<string>? headers, IReadOnlyList<IReadOnlyList<Cell>> rows)
        {
            var sb = new StringBuilder ();
            sb.Append ("<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?>");
            sb.Append ("<worksheet xmlns=\"http://schemas.openxmlformats.org/spreadsheetml/2006/main\"><sheetData>");

            var rowNumber = 1;

            if (headers is not null) {
                var headerCells = new List<Cell> (headers.Count);
                foreach (var h in headers)
                    headerCells.Add (new Cell (h, false));
                AppendRow (sb, rowNumber++, headerCells);
            }

            foreach (var row in rows)
                AppendRow (sb, rowNumber++, row);

            sb.Append ("</sheetData></worksheet>");
            return sb.ToString ();
        }

        private static void AppendRow (StringBuilder sb, int rowNumber, IReadOnlyList<Cell> cells)
        {
            sb.Append ("<row r=\"").Append (rowNumber).Append ("\">");

            for (var i = 0; i < cells.Count; i++) {
                var reference = ColumnName (i) + rowNumber.ToString (System.Globalization.CultureInfo.InvariantCulture);
                var cell = cells[i];

                if (cell.IsNumber)
                    sb.Append ("<c r=\"").Append (reference).Append ("\"><v>").Append (cell.Text).Append ("</v></c>");
                else
                    sb.Append ("<c r=\"").Append (reference).Append ("\" t=\"inlineStr\"><is><t xml:space=\"preserve\">")
                      .Append (XmlEscape (cell.Text ?? string.Empty))
                      .Append ("</t></is></c>");
            }

            sb.Append ("</row>");
        }

        // 0 → "A", 25 → "Z", 26 → "AA", ...
        internal static string ColumnName (int index)
        {
            var name = string.Empty;
            index++;
            while (index > 0) {
                var rem = (index - 1) % 26;
                name = (char)('A' + rem) + name;
                index = (index - 1) / 26;
            }
            return name;
        }

        private static string XmlEscape (string s) => s
            .Replace ("&", "&amp;")
            .Replace ("<", "&lt;")
            .Replace (">", "&gt;")
            .Replace ("\"", "&quot;");
    }
}
