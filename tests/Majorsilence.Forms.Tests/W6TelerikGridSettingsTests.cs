using System.Drawing;
using Majorsilence.Forms.Headless;
using Majorsilence.Forms.Telerik;
using SkiaSharp;
using Xunit;

namespace Majorsilence.Forms.Tests;

// W6 mechanisms, twenty-third chunk (#176): the grid settings Telerik designer code sets most, which
// were stored and read by nothing -- TableElement's sizes and colour, SelectionMode, BeginEditMode,
// AllowCopyPaste, the command column's caption, MaxWidth, IsCurrent, and the formatting element's
// alignment and wrap.
[Collection ("Headless")]
public class W6TelerikGridSettingsTests
{
    private static RadGridView Grid (out Form form, bool command = false)
    {
        HeadlessRenderer.Use ();
        var grid = new RadGridView { Width = 360, Height = 200 };
        grid.Columns.Add (new GridViewTextBoxColumn ("Name") { HeaderText = "Name", Width = 120 });

        if (command)
            grid.Columns.Add (new GridViewCommandColumn ("Action") { HeaderText = "Action", Width = 120 });

        grid.Rows.Add ();
        grid.Rows.Add ();
        grid.Rows[0].Cells["Name"].Value = "Alice";
        grid.Rows[1].Cells["Name"].Value = "Bob";

        form = new Form { Width = 460, Height = 300 };
        form.Controls.Add (grid);
        form.Show ();
        PaintSurface.Render (grid).Dispose ();
        return grid;
    }

    [Fact]
    public void TableElement_sizes_and_colours_the_grid ()
    {
        var grid = Grid (out var form);

        using (form) {
            grid.TableElement.RowHeight = 40;
            Assert.Equal (40, ((DataGridView) grid).RowTemplate.Height);
            Assert.All (((DataGridView) grid).Rows.Cast<DataGridViewRow> (), r => Assert.Equal (40, r.Height));

            grid.TableElement.TableHeaderHeight = 36;
            Assert.Equal (36, ((DataGridView) grid).ColumnHeadersHeight);
            Assert.Equal (36, grid.TableElement.TableHeaderHeight);

            grid.TableElement.AlternatingRowColor = Color.Red;
            Assert.Equal (new SKColor (255, 0, 0), ((DataGridView) grid).AlternatingRowsDefaultCellStyle.BackgroundColor);

            grid.TableElement.AlternatingRowColor = Color.Empty;
            Assert.Null (((DataGridView) grid).AlternatingRowsDefaultCellStyle.BackgroundColor);
        }
    }

    [Fact]
    public void SelectionMode_BeginEditMode_and_AllowCopyPaste_set_the_grids_own_switches ()
    {
        var grid = Grid (out var form);

        using (form) {
            grid.SelectionMode = GridViewSelectionMode.CellSelect;
            Assert.Equal (DataGridViewSelectionMode.CellSelect, ((DataGridView) grid).SelectionMode);
            grid.SelectionMode = GridViewSelectionMode.FullRowSelect;
            Assert.Equal (DataGridViewSelectionMode.FullRowSelect, ((DataGridView) grid).SelectionMode);

            grid.BeginEditMode = RadGridViewBeginEditMode.BeginEditProgrammatically;
            Assert.Equal (DataGridViewEditMode.EditProgrammatically, grid.EditMode);
            grid.BeginEditMode = RadGridViewBeginEditMode.BeginEditOnSingleClick;
            Assert.Equal (DataGridViewEditMode.EditOnEnter, grid.EditMode);
            grid.BeginEditMode = RadGridViewBeginEditMode.BeginEditOnKeyPressOrSelectFirstChar;
            Assert.Equal (DataGridViewEditMode.EditOnKeystroke, grid.EditMode);
            grid.BeginEditMode = RadGridViewBeginEditMode.BeginEditOnDoubleClick;
            Assert.Equal (DataGridViewEditMode.EditOnKeystrokeOrF2, grid.EditMode);

            grid.MasterTemplate.AllowCopyPaste = CopyPasteMode.Disallow;
            Assert.Equal (DataGridViewClipboardCopyMode.Disable, grid.ClipboardCopyMode);
            grid.MasterTemplate.AllowCopyPaste = CopyPasteMode.Copy | CopyPasteMode.CopyHeaderText;
            Assert.Equal (DataGridViewClipboardCopyMode.EnableAlwaysIncludeHeaderText, grid.ClipboardCopyMode);
            grid.MasterTemplate.AllowCopyPaste = CopyPasteMode.All;
            Assert.Equal (DataGridViewClipboardCopyMode.EnableWithAutoHeaderText, grid.ClipboardCopyMode);
        }
    }

    [Fact]
    public void BeginEditProgrammatically_keeps_a_double_click_from_opening_an_editor ()
    {
        var grid = Grid (out var form);

        using (form) {
            grid.BeginEditMode = RadGridViewBeginEditMode.BeginEditProgrammatically;

            var cell = grid.DeviceToLogicalUnits (((DataGridView) grid).GetCellBounds (0, 0));
            grid.RaiseDoubleClick (new MouseEventArgs (MouseButtons.Left, 2, cell.Left + 5, cell.Top + (cell.Height / 2), 0));

            Assert.False (grid.IsCurrentCellInEditMode);
        }
    }

    [Fact]
    public void A_command_column_draws_buttons_captioned_by_DefaultText_or_the_value ()
    {
        var grid = Grid (out var form, command: true);

        using (form) {
            var column = (GridViewCommandColumn) ((DataGridView) grid).Columns[1];
            column.DefaultText = "Open";

            Assert.Equal ("Open", column.ButtonCaptionFor (string.Empty));
            Assert.Equal ("row value", column.ButtonCaptionFor ("row value"));

            column.UseDefaultText = true;
            Assert.Equal ("Open", column.ButtonCaptionFor ("row value"));

            // Drawn as a button: the button's border puts ink inside the cell. Measured on the second
            // row -- not current, so no selection colour -- with both cells empty, so the only ink a
            // text cell could have is none. Device pixels, as the bitmap and the cell bounds are.
            column.UseDefaultText = false;
            column.DefaultText = string.Empty;
            ((DataGridView) grid).Rows[1].Cells[0].Value = null;

            using var bitmap = PaintSurface.Render (grid);

            int Ink (Rectangle cell)
            {
                var inset = grid.LogicalToDeviceUnits (2);
                var background = bitmap.GetPixel (cell.Left + inset, cell.Top + inset);
                var ink = 0;

                for (var y = cell.Top + inset; y < cell.Bottom - inset; y++)
                    for (var x = cell.Left + inset; x < cell.Right - inset; x++)
                        if (bitmap.GetPixel (x, y) != background)
                            ink++;

                return ink;
            }

            Assert.Equal (0, Ink (((DataGridView) grid).GetCellBounds (1, 0)));
            Assert.True (Ink (((DataGridView) grid).GetCellBounds (1, 1)) > 0, "the command cell drew no button");
        }
    }

    [Fact]
    public void MaxWidth_stops_a_column_resize_drag ()
    {
        var grid = Grid (out var form);

        using (form) {
            var column = (GridViewColumn) ((DataGridView) grid).Columns[0];
            column.MaxWidth = 150;

            // On the column's right edge, in the header row: the divider the resize drag starts from.
            var edge = grid.DeviceToLogicalUnits (((DataGridView) grid).GetCellBounds (0, 0)).Right;
            var y = ((DataGridView) grid).ColumnHeadersHeight / 2;

            grid.RaiseMouseDown (new MouseEventArgs (MouseButtons.Left, 1, edge, y, 0));
            grid.RaiseMouseMove (new MouseEventArgs (MouseButtons.Left, 0, edge + 200, y, 0));
            grid.RaiseMouseUp (new MouseEventArgs (MouseButtons.Left, 1, edge + 200, y, 0));

            Assert.Equal (150, column.Width);
        }
    }

    [Fact]
    public void IsCurrent_reads_and_moves_the_current_cell ()
    {
        var grid = Grid (out var form, command: true);

        using (form) {
            ((DataGridView) grid).CurrentCell = ((DataGridView) grid).Rows[0].Cells[0];

            var name = (GridViewColumn) ((DataGridView) grid).Columns[0];
            var action = (GridViewColumn) ((DataGridView) grid).Columns[1];
            Assert.True (name.IsCurrent);
            Assert.False (action.IsCurrent);

            action.IsCurrent = true;
            Assert.Equal (1, ((DataGridView) grid).CurrentCell!.ColumnIndex);
            Assert.Equal (0, ((DataGridView) grid).CurrentCell!.RowIndex);

            var second = grid.Rows[1];
            Assert.False (second.IsCurrent);
            second.IsCurrent = true;
            Assert.Equal (1, ((DataGridView) grid).CurrentCell!.RowIndex);
            Assert.Equal (1, ((DataGridView) grid).CurrentCell!.ColumnIndex);   // the column is kept
            Assert.True (grid.Rows[1].IsCurrent);
        }
    }

    [Fact]
    public void A_formatting_handlers_alignment_and_wrap_reach_the_cell ()
    {
        var grid = Grid (out var form);

        using (form) {
            grid.CellFormatting += (_, e) => {
                if (e.RowIndex == 0) {
                    e.CellElement.TextAlignment = ContentAlignment.MiddleRight;
                    e.CellElement.TextWrap = true;
                }
            };

            PaintSurface.Render (grid).Dispose ();

            var styled = ((DataGridView) grid).Rows[0].Cells[0].Style;
            Assert.Equal (DataGridViewContentAlignment.MiddleRight, styled.Alignment);
            Assert.Equal (DataGridViewTriState.True, styled.WrapMode);

            // A row the handler left alone keeps its own.
            Assert.Equal (DataGridViewContentAlignment.NotSet, ((DataGridView) grid).Rows[1].Cells[0].Style.Alignment);
        }
    }
}
