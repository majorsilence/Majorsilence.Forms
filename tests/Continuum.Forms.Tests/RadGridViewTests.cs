using System.ComponentModel;
using System.Data;
using System.IO;
using System.IO.Compression;
using System.Linq;
using Continuum.Forms.Telerik;
using Xunit;

namespace Continuum.Forms.Tests
{
    // Exercises the interactive RadGridView pipeline (filtering, sorting, grouping, collapse/expand, and
    // layout XML persistence) at the model level — no rendering required, so these run on the headless
    // backend. RowCount counts only displayed data rows (group-header rows and collapsed children are
    // excluded), which makes the view transform observable.
    public class RadGridViewTests
    {
        private static DataTable Depts ()
        {
            var depts = new DataTable ();
            depts.Columns.Add ("DeptId", typeof (int));
            depts.Columns.Add ("DeptName", typeof (string));
            depts.Rows.Add (1, "Engineering");
            depts.Rows.Add (2, "Design");
            depts.Rows.Add (3, "Support");
            return depts;
        }

        private static RadGridView MakeGrid ()
        {
            var grid = new RadGridView ();
            grid.Columns.Add (new GridViewTextBoxColumn ("Name") { HeaderText = "Name", Width = 150 });
            grid.Columns.Add (new GridViewComboBoxColumn ("Dept") {
                HeaderText = "Department", Width = 150,
                DataSource = Depts (), ValueMember = "DeptId", DisplayMember = "DeptName"
            });
            grid.Columns.Add (new GridViewDecimalColumn ("Salary") { HeaderText = "Salary", Width = 120, FormatString = "C0" });
            return grid;
        }

        private static void AddRow (RadGridView grid, string name, int dept, decimal salary)
        {
            grid.Rows.Add ();
            var row = grid.Rows[grid.RowCount - 1];
            row.Cells["Name"].Value = name;
            row.Cells["Dept"].Value = dept;
            row.Cells["Salary"].Value = salary;
        }

        private static RadGridView Populated ()
        {
            var grid = MakeGrid ();
            AddRow (grid, "Alice", 1, 85000m);
            AddRow (grid, "Bob", 2, 48000m);
            AddRow (grid, "Carol", 1, 64000m);
            AddRow (grid, "Dave", 3, 105000m);
            AddRow (grid, "Eve", 2, 42500m);
            return grid;
        }

        private static string Name (RadGridView grid, int i) => grid.Rows[i].Cells["Name"].Value?.ToString () ?? "";

        [Fact]
        public void DataRows_Unaffected_WithoutTransform ()
        {
            using var grid = Populated ();
            Assert.Equal (5, grid.RowCount);
            Assert.Equal ("Alice", Name (grid, 0));
            Assert.Equal ("Eve", Name (grid, 4));
        }

        [Fact]
        public void Filter_DistinctValues_RestrictsRows ()
        {
            using var grid = Populated ();

            grid.FilterDescriptors.Add (new FilterDescriptor {
                PropertyName = "Name",
                SelectedValues = new System.Collections.Generic.HashSet<string> (System.StringComparer.CurrentCultureIgnoreCase) { "Alice", "Bob" }
            });

            Assert.Equal (2, grid.RowCount);

            grid.FilterDescriptors.Clear ();
            Assert.Equal (5, grid.RowCount);
        }

        [Fact]
        public void Filter_NumericCondition_HandlesCurrencyFormat ()
        {
            using var grid = Populated ();

            // Salary renders as "$85,000" (C0) but the < 60000 condition must still compare numerically.
            grid.FilterDescriptors.Add (new FilterDescriptor ("Salary", FilterOperator.IsLessThan, 60000));

            Assert.Equal (2, grid.RowCount); // Bob 48000, Eve 42500
            foreach (var row in grid.Rows)
                Assert.True ((decimal)row.Cells["Salary"].Value! < 60000);
        }

        [Fact]
        public void Sort_Ascending_And_Descending ()
        {
            using var grid = Populated ();

            grid.SortDescriptors.Add (new SortDescriptor ("Salary", ListSortDirection.Ascending));
            Assert.Equal ("Eve", Name (grid, 0));   // 42500
            Assert.Equal ("Dave", Name (grid, 4));  // 105000

            grid.SortDescriptors.Clear ();
            grid.SortDescriptors.Add (new SortDescriptor ("Salary", ListSortDirection.Descending));
            Assert.Equal ("Dave", Name (grid, 0));
            Assert.Equal ("Eve", Name (grid, 4));
        }

        [Fact]
        public void Group_KeepsDataRowCount_AndIsContiguous ()
        {
            using var grid = Populated ();
            grid.EnableGrouping = true;
            grid.GroupByColumn ("Dept");

            // All five data rows are still displayed (groups expanded by default).
            Assert.Equal (5, grid.RowCount);

            // Groups ordered ascending by department NAME: Design(2), Engineering(1), Support(3).
            // So the first rows belong to Design (dept 2).
            Assert.Equal (2, (int)grid.Rows[0].Cells["Dept"].Value!);
            Assert.Equal (2, (int)grid.Rows[1].Cells["Dept"].Value!);
            Assert.Equal (1, (int)grid.Rows[2].Cells["Dept"].Value!);
        }

        [Fact]
        public void CollapseAll_HidesChildren_ExpandAll_Restores ()
        {
            using var grid = Populated ();
            grid.EnableGrouping = true;
            grid.GroupByColumn ("Dept");

            grid.CollapseAllGroups ();
            Assert.Equal (0, grid.RowCount); // only (hidden) group headers remain

            grid.ExpandAllGroups ();
            Assert.Equal (5, grid.RowCount);
        }

        [Fact]
        public void Group_Then_Ungroup_Restores ()
        {
            using var grid = Populated ();
            grid.EnableGrouping = true;
            grid.GroupByColumn ("Dept");
            grid.ClearGrouping ();

            Assert.Equal (5, grid.RowCount);
            Assert.Empty (grid.GroupDescriptors);
            Assert.Equal ("Alice", Name (grid, 0)); // original order restored
        }

        [Fact]
        public void Layout_RoundTrips_Descriptors_And_ColumnWidths ()
        {
            using var source = Populated ();
            source.SortDescriptors.Add (new SortDescriptor ("Salary", ListSortDirection.Descending));
            source.GroupByColumn ("Dept");
            source.FilterDescriptors.Add (new FilterDescriptor ("Salary", FilterOperator.IsLessThan, 60000));
            source.Columns["Name"]!.Width = 222;

            var xml = source.SaveLayoutToString ();

            using var target = MakeGrid ();
            AddRow (target, "Alice", 1, 85000m);
            AddRow (target, "Bob", 2, 48000m);
            target.LoadLayoutFromString (xml);

            Assert.Single (target.SortDescriptors);
            Assert.Equal ("Salary", target.SortDescriptors[0].PropertyName);
            Assert.Equal (ListSortDirection.Descending, target.SortDescriptors[0].Direction);

            Assert.Single (target.GroupDescriptors);
            Assert.Equal ("Dept", target.GroupDescriptors[0].PropertyName);

            Assert.Single (target.FilterDescriptors);
            Assert.True (target.FilterDescriptors[0].IsActive);

            Assert.Equal (222, target.Columns["Name"]!.Width);
        }

        [Fact]
        public void Layout_RestoresColumnOrder ()
        {
            using var grid = MakeGrid ();
            // Craft a layout that reorders the columns to Salary, Name, Dept.
            var xml =
                "<RadGridView><Columns>" +
                "<Column Name=\"Salary\" Width=\"120\" Index=\"0\" IsVisible=\"true\" SortOrder=\"None\" />" +
                "<Column Name=\"Name\" Width=\"150\" Index=\"1\" IsVisible=\"true\" SortOrder=\"None\" />" +
                "<Column Name=\"Dept\" Width=\"150\" Index=\"2\" IsVisible=\"true\" SortOrder=\"None\" />" +
                "</Columns></RadGridView>";

            grid.LoadLayoutFromString (xml);

            Assert.Equal ("Salary", grid.Columns[0].Name);
            Assert.Equal ("Name", grid.Columns[1].Name);
            Assert.Equal ("Dept", grid.Columns[2].Name);
        }

        [Fact]
        public void ShowGroupPanel_TogglesContentOffset ()
        {
            using var grid = Populated ();
            Assert.Equal (0, grid.GroupPanelBandHeight);

            grid.ShowGroupPanel = true;
            Assert.True (grid.GroupPanelBandHeight > 0);
        }

        private static GridSummaryRow SummaryRow (RadGridView grid)
        {
            var dgv = (DataGridView)grid;
            return (GridSummaryRow)dgv.Rows.First (RadGridView.IsSummaryRow).Tag!;
        }

        [Fact]
        public void SummaryRow_ComputesAggregates_OverData ()
        {
            using var grid = Populated ();
            grid.SummaryRowsBottom.Add (new GridViewSummaryRowItem (
                new GridViewSummaryItem ("Name", GridAggregateFunction.Count),
                new GridViewSummaryItem ("Salary", GridAggregateFunction.Sum)));

            var tag = SummaryRow (grid);
            Assert.Equal ("5", tag.Values[0]);          // Count of 5 rows
            Assert.Equal ("344500", tag.Values[2]);     // 85000+48000+64000+105000+42500

            // Summary rows do not count as data rows.
            Assert.Equal (5, grid.RowCount);
        }

        [Fact]
        public void SummaryRow_RespectsActiveFilter ()
        {
            using var grid = Populated ();
            grid.FilterDescriptors.Add (new FilterDescriptor ("Salary", FilterOperator.IsLessThan, 60000));
            grid.SummaryRowsBottom.Add (new GridViewSummaryRowItem (
                new GridViewSummaryItem ("Name", GridAggregateFunction.Count),
                new GridViewSummaryItem ("Salary", GridAggregateFunction.Sum)));

            var tag = SummaryRow (grid);
            Assert.Equal ("2", tag.Values[0]);          // Bob + Eve
            Assert.Equal ("90500", tag.Values[2]);      // 48000 + 42500
        }

        [Fact]
        public void SummaryItem_FormatString_Applied ()
        {
            using var grid = Populated ();
            grid.SummaryRowsTop.Add (new GridViewSummaryRowItem (
                new GridViewSummaryItem ("Salary", GridAggregateFunction.Average, "Avg: {0:N0}")));

            var tag = SummaryRow (grid);
            Assert.Equal ("Avg: 68,900", tag.Values[2]); // (344500/5)
        }

        [Fact]
        public void ExportToXlsx_ProducesReadableWorkbook ()
        {
            using var grid = Populated ();
            grid.SortDescriptors.Add (new SortDescriptor ("Salary", ListSortDirection.Ascending));

            var bytes = grid.ExportToXlsxBytes ("Employees");
            using var ms = new MemoryStream (bytes);
            using var zip = new ZipArchive (ms, ZipArchiveMode.Read);

            Assert.NotNull (zip.GetEntry ("[Content_Types].xml"));
            Assert.NotNull (zip.GetEntry ("xl/_rels/workbook.xml.rels"));

            var sheet = zip.GetEntry ("xl/worksheets/sheet1.xml");
            Assert.NotNull (sheet);
            using (var reader = new StreamReader (sheet!.Open ())) {
                var xml = reader.ReadToEnd ();
                Assert.Contains ("<t xml:space=\"preserve\">Name</t>", xml);  // header cell
                Assert.Contains ("Eve", xml);                                  // a data value (lowest salary, first)
            }

            using (var wb = new StreamReader (zip.GetEntry ("xl/workbook.xml")!.Open ()))
                Assert.Contains ("Employees", wb.ReadToEnd ());                 // sheet name
        }

        [Fact]
        public void Xlsx_ColumnName_Encoding ()
        {
            Assert.Equal ("A", RadGridXlsxExport.ColumnName (0));
            Assert.Equal ("Z", RadGridXlsxExport.ColumnName (25));
            Assert.Equal ("AA", RadGridXlsxExport.ColumnName (26));
            Assert.Equal ("AB", RadGridXlsxExport.ColumnName (27));
        }

        [Fact]
        public void Clipboard_CopiesSelectedRowsAsTsv ()
        {
            using var grid = Populated ();
            grid.Rows[0].Selected = true;   // Alice
            grid.Rows[2].Selected = true;   // Carol

            var lines = grid.BuildClipboardText ().Replace ("\r\n", "\n").Split ('\n');
            Assert.Equal ("Name\tDepartment\tSalary", lines[0]);   // auto headers (default mode)
            Assert.Equal (3, lines.Length);                         // header + 2 selected
            Assert.StartsWith ("Alice\t", lines[1]);
            Assert.StartsWith ("Carol\t", lines[2]);

            // Without headers.
            grid.ClipboardCopyMode = DataGridViewClipboardCopyMode.EnableWithoutHeaderText;
            var noHeader = grid.BuildClipboardText ().Replace ("\r\n", "\n").Split ('\n');
            Assert.Equal (2, noHeader.Length);
            Assert.StartsWith ("Alice\t", noHeader[0]);

            // Disabled.
            grid.ClipboardCopyMode = DataGridViewClipboardCopyMode.Disable;
            Assert.Equal (string.Empty, grid.BuildClipboardText ());
        }

        [Fact]
        public void DateColumn_TryGetCellDate_FromDateTimeOrText ()
        {
            var dt = new System.DateTime (2026, 6, 21);

            Assert.True (RadGridView.TryGetCellDate (dt, out var a));
            Assert.Equal (dt, a);

            Assert.True (RadGridView.TryGetCellDate ("2026-06-21", out var b));
            Assert.Equal (dt, b.Date);

            Assert.False (RadGridView.TryGetCellDate ("not a date", out _));
            Assert.False (RadGridView.TryGetCellDate (null, out _));
        }

        [Fact]
        public void ComboColumn_EditorItems_AndValueRoundTrip ()
        {
            using var grid = Populated ();
            var combo = (GridViewComboBoxColumn)grid.Columns["Dept"]!;

            var items = combo.GetEditorItems ();
            Assert.Equal (3, items.Count);                                   // Engineering, Design, Support
            Assert.Contains (items, p => p.Display == "Engineering");

            // Picking "Support" stores the underlying value (DeptId 3) the editor would write back.
            var support = items.First (p => p.Display == "Support");
            grid.Rows[0].Cells["Dept"].Value = support.Value;
            Assert.Equal ("3", grid.Rows[0].Cells["Dept"].Value?.ToString ());
        }

        [Fact]
        public void ExportToCsv_EscapesAndRespectsView ()
        {
            using var grid = Populated ();
            grid.Rows[0].Cells["Name"].Value = "Alice, J.";   // embedded comma forces quoting
            grid.SortDescriptors.Add (new SortDescriptor ("Salary", ListSortDirection.Ascending));

            var csv = grid.ExportToCsv ();
            var lines = csv.Replace ("\r\n", "\n").Split ('\n');

            Assert.Equal ("Name,Department,Salary", lines[0]);   // headers from HeaderText
            Assert.Equal (6, lines.Length);                       // header + 5 data rows (no structural rows)
            Assert.StartsWith ("Eve,", lines[1]);                 // lowest salary first (sorted view)
            Assert.Contains ("\"Alice, J.\"", csv);               // comma field is quoted
            Assert.Contains ("Design", csv);                      // combo column exports the display name
        }

        [Fact]
        public void Search_FiltersAcrossColumns ()
        {
            using var grid = Populated ();

            grid.Search ("ali");                 // matches "Alice" in the Name column
            Assert.Equal (1, grid.RowCount);
            Assert.Equal ("Alice", Name (grid, 0));

            grid.Search ("Engineering");         // matches the Dept combo display (dept 1 → Alice, Carol)
            Assert.Equal (2, grid.RowCount);

            grid.SearchText = string.Empty;      // cleared → all rows
            Assert.Equal (5, grid.RowCount);
        }

        [Fact]
        public void GroupFooters_AggregatePerGroup ()
        {
            using var grid = Populated ();
            grid.EnableGrouping = true;
            grid.GroupByColumn ("Dept");
            grid.GroupSummaryItems.Add (new GridViewSummaryItem ("Salary", GridAggregateFunction.Sum));

            var dgv = (DataGridView)grid;
            var salaryCol = grid.ColumnIndexByName ("Salary");

            // One footer per leaf group, in group order (Design, Engineering, Support).
            var footers = dgv.Rows
                .Where (RadGridView.IsSummaryRow)
                .Select (r => ((GridSummaryRow)r.Tag!).Values[salaryCol])
                .ToList ();

            Assert.Equal (new[] { "90500", "149000", "105000" }, footers);
            // Design: Bob 48000 + Eve 42500; Engineering: Alice 85000 + Carol 64000; Support: Dave 105000.
        }

        [Fact]
        public void Column_AllowGroup_False_BlocksGrouping ()
        {
            using var grid = Populated ();
            grid.EnableGrouping = true;
            ((GridViewColumn)grid.Columns["Dept"]!).AllowGroup = false;

            grid.GroupByColumn ("Dept");
            Assert.Empty (grid.GroupDescriptors);
        }

        [Fact]
        public void Column_AllowFiltering_Honored ()
        {
            using var grid = Populated ();
            var col = (GridViewColumn)grid.Columns["Name"]!;

            Assert.True (RadGridView.ColumnAllowsFiltering (col));
            col.AllowFiltering = false;
            Assert.False (RadGridView.ColumnAllowsFiltering (col));
        }

        [Fact]
        public void AlternatingRowColor_DefaultsOff_AndToggles ()
        {
            using var grid = Populated ();
            Assert.False (grid.EnableAlternatingRowColor);
            Assert.False (((DataGridView)grid).AlternatingRowColorsEnabled);

            grid.EnableAlternatingRowColor = true;
            Assert.True (((DataGridView)grid).AlternatingRowColorsEnabled);
        }

        [Fact]
        public void ConditionalFormatting_Cell_ColorsMatchingCell ()
        {
            using var grid = Populated ();
            var salary = (GridViewColumn)grid.Columns["Salary"]!;
            salary.ConditionalFormattingObjectList.Add (
                new ConditionalFormattingObject ("big", ConditionTypes.Greater, "100000") { CellBackColor = System.Drawing.Color.Red });

            var dgv = (DataGridView)grid;
            var col = grid.ColumnIndexByName ("Salary");

            var dave = dgv.Rows[3];     // 105000 → matches
            grid.RaiseRowFormatting (dave, 3);
            grid.RaiseCellFormatting (dave, 3, col);
            Assert.Equal (new SkiaSharp.SKColor (255, 0, 0), dave.Cells[col].Style.BackgroundColor);

            var bob = dgv.Rows[1];      // 48000 → no match
            grid.RaiseRowFormatting (bob, 1);
            grid.RaiseCellFormatting (bob, 1, col);
            Assert.Null (bob.Cells[col].Style.BackgroundColor);
        }

        [Fact]
        public void ConditionalFormatting_ApplyToRow_ColorsWholeRow ()
        {
            using var grid = Populated ();
            var salary = (GridViewColumn)grid.Columns["Salary"]!;
            salary.ConditionalFormattingObjectList.Add (
                new ConditionalFormattingObject ("hl", ConditionTypes.Greater, "100000", "", applyToRow: true) {
                    RowBackColor = System.Drawing.Color.Yellow
                });

            var dgv = (DataGridView)grid;
            var dave = dgv.Rows[3];
            grid.RaiseRowFormatting (dave, 3);

            var yellow = new SkiaSharp.SKColor (255, 255, 0);
            foreach (DataGridViewCell cell in dave.Cells)
                Assert.Equal (yellow, cell.Style.BackgroundColor);
        }

        [Fact]
        public void AutoSizeColumns_Fill_FillsViewportWidth ()
        {
            using var grid = Populated ();
            grid.Width = 600;
            grid.Height = 300;
            grid.MasterTemplate.AutoSizeColumnsMode = GridViewAutoSizeColumnsMode.Fill;

            var availableDevice = grid.GetContentArea ().Width - (grid.RowHeadersVisible ? grid.ScaledRowHeadersWidth : 0);
            var available = grid.DeviceToLogicalUnits (availableDevice);
            Assert.True (available > 0);

            var sum = grid.Columns.Where (c => c.Visible).Sum (c => c.Width);
            Assert.Equal (available, sum);
        }

        [Fact]
        public void MultiColumnSort_SortsByPrimaryThenSecondary ()
        {
            using var grid = Populated ();
            // Primary: Department (ascending by name → Design before Engineering).
            grid.SortDescriptors.Add (new SortDescriptor ("Dept", ListSortDirection.Ascending));
            // Secondary: Salary descending within each department.
            grid.SortDescriptors.Add (new SortDescriptor ("Salary", ListSortDirection.Descending));

            Assert.Equal ("Bob", Name (grid, 0));    // Design, 48000
            Assert.Equal ("Eve", Name (grid, 1));    // Design, 42500
            Assert.Equal ("Alice", Name (grid, 2));  // Engineering, 85000
            Assert.Equal ("Carol", Name (grid, 3));  // Engineering, 64000
            Assert.Equal ("Dave", Name (grid, 4));   // Support, 105000
        }

        [Fact]
        public void DecimalColumn_ClampsAndRounds ()
        {
            var col = new GridViewDecimalColumn ("X") { Minimum = 0m, Maximum = 100m, DecimalPlaces = 1 };

            Assert.Equal (100m, RadGridView.ClampDecimal (col, 150m));  // above max
            Assert.Equal (0m, RadGridView.ClampDecimal (col, -5m));     // below min
            Assert.Equal (12.3m, RadGridView.ClampDecimal (col, 12.34m));
            Assert.Equal (12.4m, RadGridView.ClampDecimal (col, 12.35m)); // round away from zero
        }

        [Fact]
        public void ShowColumnChooser_WithoutWindow_DoesNotThrow ()
        {
            using var grid = Populated ();
            grid.AllowColumnChooser = true;
            grid.ShowColumnChooser (); // no parent window → safe no-op
        }

        [Fact]
        public void FrozenColumns_WidthAndLayout ()
        {
            using var grid = MakeGrid (); // Name(150), Dept(150), Salary(120)
            grid.Width = 600;
            grid.Height = 300;

            grid.Columns["Name"]!.Frozen = true;
            Assert.Equal (grid.LogicalToDeviceUnits (150), grid.FrozenColumnsWidth);

            // The non-frozen column starts immediately after the frozen band.
            Assert.Equal (grid.GetColumnDeviceLeft (0) + grid.FrozenColumnsWidth, grid.GetColumnDeviceLeft (1));

            // PinPosition.Left is the Telerik way to freeze a column.
            var pinned = (GridViewColumn)grid.Columns["Dept"]!;
            pinned.PinPosition = PinnedColumnPosition.Left;
            Assert.True (pinned.Frozen);

            // Two frozen columns sum their widths.
            Assert.Equal (grid.LogicalToDeviceUnits (300), grid.FrozenColumnsWidth);
            Assert.Equal (grid.GetColumnDeviceLeft (0) + grid.FrozenColumnsWidth, grid.GetColumnDeviceLeft (2));
        }

        [Fact]
        public void MasterDetail_ExpandInjectsDetailRow ()
        {
            using var grid = Populated ();
            grid.Width = 800;
            grid.Height = 400;
            grid.ChildViewProvider = _ => {
                var view = new GridChildView ("Project", "Hours");
                view.AddRow ("Apollo", "40");
                view.AddRow ("Zephyr", "12");
                return view;
            };

            var dgv = (DataGridView)grid;
            var alice = dgv.Rows[0];

            grid.ExpandRow (alice);
            Assert.True (grid.IsRowExpanded (alice));
            Assert.Same (alice, dgv.Rows[0]);
            Assert.IsType<GridDetailRow> (dgv.Rows[1].Tag);             // detail row follows its master
            Assert.Single (dgv.Rows.Where (r => r.Tag is GridDetailRow));
            Assert.Equal (5, grid.RowCount);                            // detail isn't a data row

            // Render with an expanded detail + expander glyphs must not throw.
            var info = new SkiaSharp.SKImageInfo (800, 400);
            using (var bmp = new SkiaSharp.SKBitmap (info))
            using (var canvas = new SkiaSharp.SKCanvas (bmp))
                Continuum.Forms.Renderers.RenderManager.Render (grid, new PaintEventArgs (info, canvas, 1.0));

            grid.CollapseRow (alice);
            Assert.False (grid.IsRowExpanded (alice));
            Assert.Empty (dgv.Rows.Where (r => r.Tag is GridDetailRow));
            Assert.Equal (5, grid.RowCount);
        }

        [Fact]
        public void FilterRow_ReservesBand_AndReportsText ()
        {
            using var grid = Populated ();
            var baseOffset = grid.RowsTopOffset;

            grid.ShowFilterRow = true;
            Assert.True (grid.FilterRowBandHeight > 0);
            Assert.Equal (baseOffset + grid.FilterRowBandHeight, grid.RowsTopOffset);

            // A Contains filter (what the filter row creates) surfaces as the column's filter-cell text.
            grid.FilterDescriptors.Add (new FilterDescriptor ("Name", FilterOperator.Contains, "ali"));
            Assert.Equal ("ali", grid.CurrentColumnFilterText (grid.ColumnIndexByName ("Name")));
            Assert.Equal (string.Empty, grid.CurrentColumnFilterText (grid.ColumnIndexByName ("Salary")));
            Assert.Equal (1, grid.RowCount); // live filter applied → only Alice
        }

        [Fact]
        public void RightPinnedColumns_LayoutAndPinState ()
        {
            using var grid = MakeGrid (); // Name(150), Dept(150), Salary(120)
            grid.Width = 600;
            grid.Height = 300;

            var salary = (GridViewColumn)grid.Columns["Salary"]!;
            salary.PinPosition = PinnedColumnPosition.Right;
            Assert.True (salary.PinnedRight);
            Assert.False (salary.Frozen);
            Assert.Equal (grid.LogicalToDeviceUnits (120), grid.RightPinnedColumnsWidth);

            // A right-pinned column sits against the right edge of the content area.
            var content = grid.GetContentArea ();
            Assert.Equal (content.Right - grid.RightPinnedColumnsWidth, grid.GetColumnDeviceLeft (2));

            // Switching pin position is mutually exclusive.
            salary.PinPosition = PinnedColumnPosition.Left;
            Assert.True (salary.Frozen);
            Assert.False (salary.PinnedRight);
            salary.PinPosition = PinnedColumnPosition.None;
            Assert.False (salary.Frozen);
            Assert.False (salary.PinnedRight);
            Assert.Equal (0, grid.RightPinnedColumnsWidth);
        }

        [Fact]
        public void Renders_WithGroupPanel_Filter_And_Groups_WithoutThrowing ()
        {
            using var grid = Populated ();
            grid.Width = 800;
            grid.Height = 400;
            grid.ShowGroupPanel = true;
            grid.ShowFilterRow = true;
            grid.EnableFiltering = true;
            grid.EnableGrouping = true;
            grid.GroupByColumn ("Dept");
            grid.SortDescriptors.Add (new SortDescriptor ("Salary", ListSortDirection.Descending));
            grid.FilterDescriptors.Add (new FilterDescriptor ("Salary", FilterOperator.IsGreaterThan, 40000));
            grid.SummaryRowsBottom.Add (new GridViewSummaryRowItem (
                new GridViewSummaryItem ("Salary", GridAggregateFunction.Sum, "C0")));
            ((GridViewColumn)grid.Columns["Name"]!).WrapText = true; // exercise the wrap path
            grid.Columns["Name"]!.Frozen = true;                    // exercise the frozen-column render path
            ((GridViewColumn)grid.Columns["Salary"]!).PinPosition = PinnedColumnPosition.Right; // right-pin render path

            var info = new SkiaSharp.SKImageInfo (800, 400);
            using var bitmap = new SkiaSharp.SKBitmap (info);
            using var canvas = new SkiaSharp.SKCanvas (bitmap);
            var e = new PaintEventArgs (info, canvas, 1.0);

            // Expanded render populates the renderer's hit-test rects (pills + filter glyphs).
            Continuum.Forms.Renderers.RenderManager.Render (grid, e);
            Assert.NotEmpty (grid.GroupPillLayouts);
            Assert.NotEmpty (grid.FilterGlyphRects);

            // Collapsed groups must also render cleanly.
            grid.CollapseAllGroups ();
            Continuum.Forms.Renderers.RenderManager.Render (grid, e);
        }
    }
}
