using System.Drawing;
using Majorsilence.Forms.Renderers;
using SkiaSharp;
using Xunit;

namespace Majorsilence.Forms.Tests
{
    // End-to-end tests for the DataGridView hooks that used to be declared-but-never-raised:
    // CellFormatting, RowPrePaint/RowPostPaint, CellPainting, CellParsing, RowValidating/RowValidated
    // (plus RowEnter/RowLeave), GetClipboardContent and the advanced (per-edge) cell border style.
    //
    // Everything here drives the REAL pipeline: paint tests push a frame through
    // RenderManager.Render onto an off-screen Skia surface and observe what the renderer was asked
    // to draw; edit tests go through BeginEdit/EndEdit; validation tests move the current row.
    public class DataGridViewHookTests
    {
        // A grid type of its own so the capturing renderer below can be registered for it without
        // affecting the renderer every other DataGridView test uses.
        private sealed class HookGrid : DataGridView { }

        // Records every cell the renderer actually draws, with the text and paint parts it was given.
        // Every RenderCell path funnels through the paintParts overload, so this sees them all.
        private sealed class CapturingRenderer : DataGridViewRenderer
        {
            public override Type Type => typeof (HookGrid);

            public List<(int Row, int Column, string Value, DataGridViewPaintParts Parts, Color Back)> Cells { get; } = [];

            protected override void RenderCell (DataGridView control, DataGridViewColumn column, string value, int rowIndex,
                int columnIndex, Rectangle bounds, ControlStyle? cellStyle, PaintEventArgs e, DataGridViewPaintParts paintParts)
            {
                Cells.Add ((rowIndex, columnIndex, value, paintParts, cellStyle?.BackColor ?? Color.Empty));
                base.RenderCell (control, column, value, rowIndex, columnIndex, bounds, cellStyle, e, paintParts);
            }
        }

        private static CapturingRenderer UseCapturingRenderer ()
        {
            var renderer = new CapturingRenderer ();
            RenderManager.SetRenderer<HookGrid> (renderer);
            return renderer;
        }

        private static HookGrid MakeGrid ()
        {
            var grid = new HookGrid { Width = 420, Height = 200 };
            grid.Columns.Add ("Name", 160);
            grid.Columns.Add ("Amount", 160);
            grid.Rows.Add ("Alice", 1234.5);
            grid.Rows.Add ("Bob", 99.25);
            return grid;
        }

        private static SKBitmap Paint (DataGridView grid)
        {
            var info = new SKImageInfo (Math.Max (1, grid.Width), Math.Max (1, grid.Height));
            var bitmap = new SKBitmap (info);
            using var canvas = new SKCanvas (bitmap);
            canvas.Clear (SKColors.White);
            RenderManager.Render (grid, new PaintEventArgs (info, canvas, 1.0));
            return bitmap;
        }

        // ── CellFormatting ──

        [Fact]
        public void CellFormatting_RaisedDuringPaint_WithRealArgs_AndItsValueIsWhatGetsDrawn ()
        {
            var renderer = UseCapturingRenderer ();
            using var grid = MakeGrid ();

            var seen = new List<(int Row, int Column, object? Value, bool HasStyle)> ();

            grid.CellFormatting += (o, e) => {
                seen.Add ((e.RowIndex, e.ColumnIndex, e.Value, e.CellStyle is not null));

                if (e.ColumnIndex == 1 && e.Value is double d) {
                    e.Value = $"[{d:0.00}]";
                    e.FormattingApplied = true;
                }
            };

            Paint (grid);

            // Raised for every visible cell, with the raw value and a resolved style.
            Assert.Contains ((0, 0, (object?)"Alice", true), seen);
            Assert.Contains ((0, 1, (object?)1234.5, true), seen);
            Assert.Contains ((1, 1, (object?)99.25, true), seen);

            // The handler's formatted value is what the renderer was asked to draw.
            Assert.Contains (renderer.Cells, c => c is { Row: 0, Column: 1, Value: "[1234.50]" });
            Assert.Contains (renderer.Cells, c => c is { Row: 1, Column: 1, Value: "[99.25]" });

            // Untouched cells keep their own value.
            Assert.Contains (renderer.Cells, c => c is { Row: 0, Column: 0, Value: "Alice" });
        }

        [Fact]
        public void CellFormatting_StyleChange_AppliesToThatPaintOnly_AndNeverMutatesTheCell ()
        {
            var renderer = UseCapturingRenderer ();
            using var grid = MakeGrid ();

            grid.CellFormatting += (o, e) => {
                if (e.RowIndex == 1 && e.ColumnIndex == 1)
                    e.CellStyle.BackColor = Color.FromArgb (255, 200, 0, 0);
            };

            Paint (grid);

            Assert.Contains (renderer.Cells, c => c.Row == 1 && c.Column == 1 && c.Back == Color.FromArgb (255, 200, 0, 0));
            Assert.DoesNotContain (renderer.Cells, c => c.Row == 0 && c.Back == Color.FromArgb (255, 200, 0, 0));

            // The cell object itself is untouched, so the conditional format cannot go stale.
            Assert.True (grid.Rows[1].Cells[1].Style.BackColor.IsEmpty);
        }

        [Fact]
        public void CellFormatting_WithoutFormattingApplied_LetsTheGridApplyTheStyleFormat ()
        {
            var renderer = UseCapturingRenderer ();
            using var grid = MakeGrid ();
            grid.Columns[1].DefaultCellStyle.Format = "N2";

            // No handler at all: the resolved (inherited) style's format string is now honored.
            Paint (grid);
            Assert.Contains (renderer.Cells, c => c is { Row: 0, Column: 1, Value: "1,234.50" });

            // A handler that replaces the value but leaves FormattingApplied false hands the value back
            // to the grid's default formatting, exactly as WinForms does.
            renderer.Cells.Clear ();
            grid.CellFormatting += (o, e) => {
                if (e.ColumnIndex == 1)
                    e.Value = 5d;
            };

            Paint (grid);
            Assert.Contains (renderer.Cells, c => c is { Row: 0, Column: 1, Value: "5.00" });
        }

        // ── RowPrePaint / RowPostPaint ──

        [Fact]
        public void RowPrePaint_RaisedWithGeometry_AndHandledSuppressesTheRow ()
        {
            var renderer = UseCapturingRenderer ();
            using var grid = MakeGrid ();

            var seen = new List<DataGridViewRowPrePaintEventArgs> ();

            grid.RowPrePaint += (o, e) => {
                seen.Add (e);

                if (e.RowIndex == 0)
                    e.Handled = true;      // "I drew this row myself"
            };

            Paint (grid);

            Assert.Equal ([0, 1], seen.Select (a => a.RowIndex).ToArray ());
            Assert.All (seen, a => {
                Assert.NotNull (a.Graphics);
                Assert.NotNull (a.InheritedRowStyle);
                Assert.False (a.RowBounds.IsEmpty);
            });
            Assert.True (seen[0].IsFirstDisplayedRow);
            Assert.True (seen[1].IsLastVisibleRow);

            // Row 0 was suppressed; row 1 painted normally.
            Assert.DoesNotContain (renderer.Cells, c => c.Row == 0);
            Assert.Equal (2, renderer.Cells.Count (c => c.Row == 1));
        }

        [Fact]
        public void RowPrePaint_PaintParts_LimitWhatTheGridDraws ()
        {
            var renderer = UseCapturingRenderer ();
            using var grid = MakeGrid ();

            grid.RowPrePaint += (o, e) => {
                if (e.RowIndex == 1)
                    e.PaintParts = DataGridViewPaintParts.ContentForeground;
            };

            Paint (grid);

            Assert.All (renderer.Cells.Where (c => c.Row == 0), c => Assert.Equal (DataGridViewPaintParts.All, c.Parts));
            Assert.All (renderer.Cells.Where (c => c.Row == 1), c => Assert.Equal (DataGridViewPaintParts.ContentForeground, c.Parts));
        }

        [Fact]
        public void RowPrePaint_PaintCells_RunsTheGridsOwnCellPainting ()
        {
            var renderer = UseCapturingRenderer ();
            using var grid = MakeGrid ();

            grid.RowPrePaint += (o, e) => {
                if (e.RowIndex != 0)
                    return;

                // Compose: let the grid draw the backgrounds, then take over.
                e.PaintCellsBackground (e.ClipBounds, true);
                e.PaintHeader (false);
                e.Handled = true;
            };

            Paint (grid);

            var row0 = renderer.Cells.Where (c => c.Row == 0).ToList ();
            Assert.Equal (2, row0.Count);                                    // the callback really painted
            Assert.All (row0, c => {
                Assert.True (c.Parts.HasFlag (DataGridViewPaintParts.Background));
                Assert.False (c.Parts.HasFlag (DataGridViewPaintParts.ContentForeground));
            });
        }

        [Fact]
        public void RowPostPaint_RaisedAfterTheRowsCells_WithGeometry ()
        {
            UseCapturingRenderer ();
            using var grid = MakeGrid ();

            var order = new List<string> ();
            var args = new List<DataGridViewRowPostPaintEventArgs> ();

            grid.CellPainting += (o, e) => order.Add ($"cell{e.RowIndex}.{e.ColumnIndex}");
            grid.RowPostPaint += (o, e) => { order.Add ($"post{e.RowIndex}"); args.Add (e); };

            Paint (grid);

            Assert.Equal (["cell0.0", "cell0.1", "post0", "cell1.0", "cell1.1", "post1"], order);
            Assert.All (args, a => {
                Assert.NotNull (a.Graphics);
                Assert.False (a.RowBounds.IsEmpty);
                Assert.NotNull (a.InheritedRowStyle);
            });
        }

        // ── CellPainting ──

        [Fact]
        public void CellPainting_RaisedPerCell_WithBoundsAndValues_AndHandledSuppressesTheCell ()
        {
            var renderer = UseCapturingRenderer ();
            using var grid = MakeGrid ();

            var seen = new List<DataGridViewCellPaintingEventArgs> ();

            grid.CellPainting += (o, e) => {
                seen.Add (e);

                if (e.ColumnIndex == 0)
                    e.Handled = true;
            };

            Paint (grid);

            Assert.Equal (4, seen.Count);
            Assert.All (seen, a => {
                Assert.NotNull (a.Graphics);
                Assert.NotNull (a.CellStyle);
                Assert.False (a.CellBounds.IsEmpty);
            });
            Assert.Equal ("Alice", seen[0].Value);
            Assert.Equal ("Alice", seen[0].FormattedValue);
            Assert.Equal (1234.5, seen[1].Value);

            // Handled cells were not drawn by the grid; the others were.
            Assert.DoesNotContain (renderer.Cells, c => c.Column == 0);
            Assert.Equal (2, renderer.Cells.Count (c => c.Column == 1));
        }

        [Fact]
        public void CellPainting_PaintBackground_RunsTheGridsDefaultPainting ()
        {
            var renderer = UseCapturingRenderer ();
            using var grid = MakeGrid ();

            grid.CellPainting += (o, e) => {
                if (e.RowIndex != 0 || e.ColumnIndex != 0)
                    return;

                e.PaintBackground (e.CellBounds, false);
                e.Handled = true;
            };

            Paint (grid);

            var painted = renderer.Cells.Where (c => c is { Row: 0, Column: 0 }).ToList ();
            Assert.Single (painted);
            Assert.True (painted[0].Parts.HasFlag (DataGridViewPaintParts.Background));
            Assert.False (painted[0].Parts.HasFlag (DataGridViewPaintParts.ContentForeground));
        }

        [Fact]
        public void CellPainting_PaintPartsFromHandler_LimitDefaultPainting ()
        {
            var renderer = UseCapturingRenderer ();
            using var grid = MakeGrid ();

            grid.CellPainting += (o, e) => e.PaintParts = DataGridViewPaintParts.ContentForeground;

            Paint (grid);

            Assert.NotEmpty (renderer.Cells);
            Assert.All (renderer.Cells, c => Assert.Equal (DataGridViewPaintParts.ContentForeground, c.Parts));
        }

        // ── CellParsing ──

        [Fact]
        public void CellParsing_ConvertsEditedTextBeforeItIsStored ()
        {
            using var grid = new DataGridView { Width = 400, Height = 200 };
            grid.Columns.Add ("Qty", 120);
            grid.Columns[0].ValueType = typeof (int);
            grid.Rows.Add ((object)1);

            DataGridViewCellParsingEventArgs? seen = null;

            grid.CellParsing += (o, e) => {
                seen = e;
                e.Value = int.Parse ((string)e.Value!, System.Globalization.CultureInfo.InvariantCulture) * 10;
                e.ParsingApplied = true;
            };

            grid.BeginEdit (0, 0);
            Assert.True (grid.IsCurrentCellInEditMode);

            // Type a new value and commit.
            SetEditText (grid, "4");
            Assert.True (grid.EndEdit ());

            Assert.NotNull (seen);
            Assert.Equal (0, seen!.RowIndex);
            Assert.Equal (0, seen.ColumnIndex);
            Assert.Equal (typeof (int), seen.DesiredType);
            Assert.NotNull (seen.InheritedCellStyle);

            // The parsed (typed) value was stored, not the raw text.
            Assert.Equal (40, grid.Rows[0].Cells[0].Value);
        }

        [Fact]
        public void CellParsing_NotHandled_StoresTheEditedTextAsBefore ()
        {
            using var grid = new DataGridView { Width = 400, Height = 200 };
            grid.Columns.Add ("Qty", 120);
            grid.Rows.Add ("1");

            var raised = 0;
            grid.CellParsing += (o, e) => raised++;         // inspects but does not set ParsingApplied

            grid.BeginEdit (0, 0);
            SetEditText (grid, "7");
            Assert.True (grid.EndEdit ());

            Assert.Equal (1, raised);
            Assert.Equal ("7", grid.Rows[0].Cells[0].Value);
        }

        // Types into the grid's active editing TextBox (the only editing control the compat grid uses).
        private static void SetEditText (DataGridView grid, string text)
        {
            var box = grid.Controls.OfType<TextBox> ().Last ();
            box.Text = text;
        }

        // ── RowValidating / RowValidated / RowEnter / RowLeave ──

        [Fact]
        public void RowValidation_RunsWhenTheCurrentRowChanges_InWinFormsOrder ()
        {
            using var grid = MakeGrid ();
            grid.SelectedRowIndex = 0;

            var order = new List<string> ();
            grid.RowValidating += (o, e) => order.Add ($"validating{e.RowIndex}");
            grid.RowValidated += (o, e) => order.Add ($"validated{e.RowIndex}");
            grid.RowLeave += (o, e) => order.Add ($"leave{e.RowIndex}");
            grid.RowEnter += (o, e) => order.Add ($"enter{e.RowIndex}");

            grid.SelectedRowIndex = 1;

            Assert.Equal (["validating0", "validated0", "leave0", "enter1"], order);
            Assert.Equal (1, grid.SelectedRowIndex);
        }

        [Fact]
        public void RowValidating_Cancel_KeepsTheRowCurrent ()
        {
            using var grid = MakeGrid ();
            grid.SelectedRowIndex = 0;

            var validated = 0;
            grid.RowValidating += (o, e) => e.Cancel = e.RowIndex == 0;
            grid.RowValidated += (o, e) => validated++;

            grid.SelectedRowIndex = 1;

            Assert.Equal (0, grid.SelectedRowIndex);      // move refused
            Assert.Equal (0, validated);                  // and no RowValidated
            Assert.True (grid.Rows[0].Selected);
        }

        [Fact]
        public void RowValidation_CommitsAPendingEdit_BeforeValidating ()
        {
            using var grid = MakeGrid ();
            grid.SelectedRowIndex = 0;
            grid.BeginEdit (0, 0);
            SetEditText (grid, "Alicia");

            object? valueAtValidation = null;
            grid.RowValidating += (o, e) => valueAtValidation = grid.Rows[0].Cells[0].Value;

            grid.SelectedRowIndex = 1;

            Assert.Equal ("Alicia", valueAtValidation);
            Assert.False (grid.IsCurrentCellInEditMode);
        }

        [Fact]
        public void ValidateCurrentRow_IsAlsoRunWhenTheGridLosesFocus ()
        {
            using var grid = MakeGrid ();
            grid.SelectedRowIndex = 1;

            var validating = 0;
            grid.RowValidating += (o, e) => { validating++; Assert.Equal (1, e.RowIndex); };

            Assert.True (grid.ValidateCurrentRow ());
            Assert.Equal (1, validating);

            grid.RaiseLeave ();                            // what focus loss does internally
            Assert.Equal (2, validating);
        }

        // ── GetClipboardContent ──

        [Fact]
        public void GetClipboardContent_ReturnsSelectedCells_AsTextCsvAndHtml ()
        {
            using var grid = MakeGrid ();
            grid.Columns[0].HeaderText = "Name";
            grid.Columns[1].HeaderText = "Amount";
            grid.ClipboardCopyMode = DataGridViewClipboardCopyMode.EnableAlwaysIncludeHeaderText;
            grid.SelectedRowIndex = 1;                     // FullRowSelect → the whole row is selected

            var data = grid.GetClipboardContent ();

            Assert.NotNull (data);
            var text = (string)data!.GetData (DataFormats.Text.Name)!;
            Assert.Equal ($"Name\tAmount{Environment.NewLine}Bob\t99.25{Environment.NewLine}", text);
            Assert.Equal (text, data.GetData (DataFormats.UnicodeText.Name));

            var csv = (string)data.GetData (DataFormats.CommaSeparatedValue.Name)!;
            Assert.Equal ($"Name,Amount{Environment.NewLine}Bob,99.25{Environment.NewLine}", csv);

            var html = (string)data.GetData (DataFormats.Html.Name)!;
            Assert.Contains ("<TH>Name</TH>", html);
            Assert.Contains ("<TD>Bob</TD>", html);
        }

        [Fact]
        public void GetClipboardContent_HonorsClipboardCopyMode_AndQuotesCsv ()
        {
            using var grid = MakeGrid ();
            grid.Rows.Add ("Smith, John", "1");
            grid.ClipboardCopyMode = DataGridViewClipboardCopyMode.EnableWithoutHeaderText;
            grid.SelectedRowIndex = 2;

            var data = grid.GetClipboardContent ();
            Assert.NotNull (data);
            var csv = (string)data!.GetData (DataFormats.CommaSeparatedValue.Name)!;
            Assert.Equal ($"\"Smith, John\",1{Environment.NewLine}", csv);       // quoted, no header row

            grid.ClipboardCopyMode = DataGridViewClipboardCopyMode.Disable;
            Assert.Null (grid.GetClipboardContent ());

            // Nothing selected → nothing to copy.
            grid.ClipboardCopyMode = DataGridViewClipboardCopyMode.EnableWithoutHeaderText;
            grid.ClearSelection ();
            Assert.Null (grid.GetClipboardContent ());
        }

        [Fact]
        public void GetClipboardContent_UsesTheFormattedValue ()
        {
            using var grid = MakeGrid ();
            grid.SelectedRowIndex = 0;
            grid.ClipboardCopyMode = DataGridViewClipboardCopyMode.EnableWithoutHeaderText;
            grid.Rows[0].Cells[1].Value = 12.5;

            var text = (string)grid.GetClipboardContent ()!.GetData (DataFormats.Text.Name)!;
            Assert.Equal ($"Alice\t12.5{Environment.NewLine}", text);
        }

        // ── Advanced (per-edge) cell border style ──

        [Fact]
        public void CellBorderStyle_RewritesTheAdvancedEdges_AndHandEditingSwitchesToCustom ()
        {
            using var grid = new DataGridView ();

            // Default is Single on every edge.
            Assert.Equal (DataGridViewCellBorderStyle.Single, grid.CellBorderStyle);
            Assert.Equal (DataGridViewAdvancedCellBorderStyle.Single, grid.AdvancedCellBorderStyle.Left);
            Assert.Equal (DataGridViewAdvancedCellBorderStyle.Single, grid.AdvancedCellBorderStyle.Bottom);

            grid.CellBorderStyle = DataGridViewCellBorderStyle.SingleHorizontal;
            Assert.Equal (DataGridViewAdvancedCellBorderStyle.Single, grid.AdvancedCellBorderStyle.Top);
            Assert.Equal (DataGridViewAdvancedCellBorderStyle.None, grid.AdvancedCellBorderStyle.Left);
            Assert.Equal (DataGridViewCellBorderStyle.SingleHorizontal, grid.CellBorderStyle);

            grid.CellBorderStyle = DataGridViewCellBorderStyle.None;
            Assert.Equal (DataGridViewAdvancedCellBorderStyle.None, grid.AdvancedCellBorderStyle.Right);

            // Hand-editing an edge makes the coarse property report Custom, as in WinForms.
            grid.AdvancedCellBorderStyle.Right = DataGridViewAdvancedCellBorderStyle.Outset;
            Assert.Equal (DataGridViewCellBorderStyle.Custom, grid.CellBorderStyle);

            var clone = grid.AdvancedCellBorderStyle.Clone ();
            Assert.Equal (grid.AdvancedCellBorderStyle, clone);
            clone.All (DataGridViewAdvancedCellBorderStyle.Inset);
            Assert.NotEqual (grid.AdvancedCellBorderStyle, clone);
        }

        [Fact]
        public void AdvancedCellBorderStyle_ChangesWhatTheRendererDraws ()
        {
            UseCapturingRenderer ();
            using var grid = MakeGrid ();
            grid.RowHeadersVisible = false;

            using var withBorders = Paint (grid);

            grid.CellBorderStyle = DataGridViewCellBorderStyle.None;
            using var withoutBorders = Paint (grid);

            // A vertical grid line sits on the right edge of column 0, at the middle of row 0.
            var x = grid.GetColumnDeviceLeft (0) + grid.LogicalToDeviceUnits (grid.Columns[0].Width) - 1;
            var y = grid.GetCellBounds (0, 0).Top + 2;

            var lineColor = Theme.BorderLowColor;
            Assert.Equal (lineColor, withBorders.GetPixel (x, y));
            Assert.NotEqual (lineColor, withoutBorders.GetPixel (x, y));

            // ... and the horizontal grid line along the row's bottom is gone too.
            var bottom = grid.GetCellBounds (0, 0).Bottom - 1;
            Assert.Equal (lineColor, withBorders.GetPixel (x - 5, bottom));
            Assert.NotEqual (lineColor, withoutBorders.GetPixel (x - 5, bottom));
        }

        [Fact]
        public void RowDefaultCellStyle_BackColor_IsActuallyPainted ()
        {
            UseCapturingRenderer ();
            using var grid = MakeGrid ();
            grid.RowHeadersVisible = false;
            grid.SelectedRowIndex = -1;                    // no selection highlight in the way
            grid.Rows[1].DefaultCellStyle.BackColor = Color.FromArgb (255, 0, 128, 255);

            using var bitmap = Paint (grid);

            var bounds = grid.GetCellBounds (1, 0);
            var pixel = bitmap.GetPixel (bounds.Left + 3, bounds.Top + 3);
            Assert.Equal (new SKColor (0, 128, 255), pixel);

            // Row 0, which has no row-level style, is left to the grid/theme colors.
            var row0 = grid.GetCellBounds (0, 0);
            Assert.NotEqual (new SKColor (0, 128, 255), bitmap.GetPixel (row0.Left + 3, row0.Top + 3));
        }

        // ── Clone / InheritedStyle / InheritedState ──

        [Fact]
        public void Cell_Clone_CopiesValueAndStyle_AndIsIndependent ()
        {
            using var grid = MakeGrid ();
            var cell = grid.Rows[0].Cells[0];
            cell.Tag = "tag";
            cell.ToolTipText = "tip";
            cell.ReadOnly = true;
            cell.Style.BackColor = Color.FromArgb (255, 1, 2, 3);

            var clone = (DataGridViewCell)cell.Clone ();

            Assert.NotSame (cell, clone);
            Assert.Equal ("Alice", clone.Value);
            Assert.Equal ("tag", clone.Tag);
            Assert.Equal ("tip", clone.ToolTipText);
            Assert.True (clone.ReadOnly);
            Assert.Equal (Color.FromArgb (255, 1, 2, 3), clone.Style.BackColor);
            Assert.Null (clone.OwningRow);                 // unowned until added to a row

            clone.Value = "Changed";
            Assert.Equal ("Alice", cell.Value);
        }

        [Fact]
        public void Cell_Clone_KeepsTheRuntimeTypeAndItsExtraState ()
        {
            var cell = new DataGridViewCheckBoxCell { Value = true, ThreeState = true };
            var clone = cell.Clone ();

            Assert.IsType<DataGridViewCheckBoxCell> (clone);
            Assert.True (((DataGridViewCheckBoxCell)clone).ThreeState);
            Assert.Equal (true, ((DataGridViewCheckBoxCell)clone).Value);

            var combo = new DataGridViewComboBoxCell { DisplayMember = "Name" };
            combo.Items.Add ("a");
            var comboClone = (DataGridViewComboBoxCell)combo.Clone ();
            Assert.Equal ("Name", comboClone.DisplayMember);
            Assert.Single (comboClone.Items);
        }

        [Fact]
        public void Row_Clone_CopiesCellsAndLayout_AndIsIndependent ()
        {
            using var grid = MakeGrid ();
            var row = grid.Rows[0];
            row.Height = 44;
            row.ErrorText = "bad";
            row.Tag = 7;
            row.DefaultCellStyle.BackColor = Color.FromArgb (255, 9, 9, 9);

            var clone = (DataGridViewRow)row.Clone ();

            Assert.Equal (44, clone.Height);
            Assert.Equal ("bad", clone.ErrorText);
            Assert.Equal (7, clone.Tag);
            Assert.Equal (Color.FromArgb (255, 9, 9, 9), clone.DefaultCellStyle.BackColor);
            Assert.Equal (2, clone.Cells.Count);
            Assert.Equal ("Alice", clone.Cells[0].Value);
            Assert.NotSame (row.Cells[0], clone.Cells[0]);
            Assert.Null (clone.DataGridView);

            clone.Cells[0].Value = "Other";
            Assert.Equal ("Alice", row.Cells[0].Value);

            // A cloned row is addable to the grid (the WinForms row-template pattern).
            grid.Rows.Add (clone);
            Assert.Equal (3, grid.Rows.Count);
            Assert.Same (grid, clone.DataGridView);
        }

        [Fact]
        public void Column_Clone_CopiesConfiguration_AndKeepsTheRuntimeType ()
        {
            using var grid = MakeGrid ();
            var column = grid.Columns[1];
            column.Name = "amount";
            column.DataPropertyName = "Amount";
            column.ValueType = typeof (double);
            column.Width = 222;
            column.Frozen = true;
            column.DefaultCellStyle.Format = "C2";

            var clone = (DataGridViewColumn)column.Clone ();

            Assert.Equal ("amount", clone.Name);
            Assert.Equal ("Amount", clone.DataPropertyName);
            Assert.Equal (typeof (double), clone.ValueType);
            Assert.Equal (222, clone.Width);
            Assert.True (clone.Frozen);
            Assert.Equal ("C2", clone.DefaultCellStyle.Format);
            Assert.Null (clone.DataGridView);

            clone.DefaultCellStyle.Format = "N0";
            Assert.Equal ("C2", column.DefaultCellStyle.Format);      // styles were cloned, not shared

            var button = new DataGridViewButtonColumn { Text = "Go", UseColumnTextForButtonValue = true };
            var buttonClone = (DataGridViewButtonColumn)button.Clone ();
            Assert.Equal ("Go", buttonClone.Text);
            Assert.True (buttonClone.UseColumnTextForButtonValue);
        }

        [Fact]
        public void Cell_InheritedStyle_MergesTheGridColumnAndRowCascade ()
        {
            using var grid = MakeGrid ();
            grid.DefaultCellStyle.BackColor = Color.FromArgb (255, 10, 10, 10);
            grid.DefaultCellStyle.ForeColor = Color.FromArgb (255, 20, 20, 20);
            grid.Columns[0].DefaultCellStyle.Format = "N1";
            grid.Rows[0].DefaultCellStyle.ForeColor = Color.FromArgb (255, 30, 30, 30);

            var inherited = grid.Rows[0].Cells[0].InheritedStyle;

            Assert.Equal (Color.FromArgb (255, 10, 10, 10), inherited.BackColor);   // from the grid
            Assert.Equal ("N1", inherited.Format);                                  // from the column
            Assert.Equal (Color.FromArgb (255, 30, 30, 30), inherited.ForeColor);   // row beats grid

            // The cell's own style wins over everything above it.
            grid.Rows[0].Cells[0].Style.BackColor = Color.FromArgb (255, 40, 40, 40);
            Assert.Equal (Color.FromArgb (255, 40, 40, 40), grid.Rows[0].Cells[0].InheritedStyle.BackColor);

            // Row/column inherited styles compose their own (shorter) cascades.
            Assert.Equal (Color.FromArgb (255, 30, 30, 30), grid.Rows[0].InheritedStyle.ForeColor);
            Assert.Equal ("N1", grid.Columns[0].InheritedStyle.Format);
            Assert.Equal (Color.FromArgb (255, 10, 10, 10), grid.Columns[0].InheritedStyle.BackColor);
        }

        [Fact]
        public void Cell_InheritedState_ReflectsCellRowColumnAndGridState ()
        {
            using var grid = MakeGrid ();
            grid.SelectedRowIndex = 0;

            var cell = grid.Rows[0].Cells[0];
            Assert.True (cell.InheritedState.HasFlag (DataGridViewElementStates.Visible));
            Assert.True (cell.InheritedState.HasFlag (DataGridViewElementStates.Selected));   // via its row
            Assert.False (cell.InheritedState.HasFlag (DataGridViewElementStates.ReadOnly));

            grid.Columns[0].ReadOnly = true;
            Assert.True (cell.InheritedState.HasFlag (DataGridViewElementStates.ReadOnly));

            grid.Columns[0].Frozen = true;
            Assert.True (cell.InheritedState.HasFlag (DataGridViewElementStates.Frozen));

            grid.Rows[0].Visible = false;
            Assert.False (cell.InheritedState.HasFlag (DataGridViewElementStates.Visible));

            // Displayed is set once the renderer has given the cell bounds.
            Assert.False (grid.Rows[1].Cells[1].InheritedState.HasFlag (DataGridViewElementStates.Displayed));
            Paint (grid);
            Assert.True (grid.Rows[1].Cells[1].InheritedState.HasFlag (DataGridViewElementStates.Displayed));
        }
    }
}
