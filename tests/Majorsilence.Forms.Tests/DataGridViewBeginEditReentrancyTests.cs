using System;
using Majorsilence.Forms;
using Xunit;

namespace Majorsilence.Forms.Tests
{
    /// <summary>
    /// BeginEdit invokes callbacks that are allowed to end the edit they were told about. It must not
    /// keep using the editing control afterwards.
    /// </summary>
    /// <remarks>
    /// This crashed the app: double-clicking a grid cell reached BeginEdit, the focus change inside
    /// Select () deselected whichever control held focus, that raised LostFocus back into the grid,
    /// EditTextBox_LostFocus called EndEdit, and EndEdit disposed the editor and nulled the field --
    /// which BeginEdit then dereferenced, throwing NullReferenceException out of a UI event handler.
    ///
    /// The focus round-trip needs a real windowing backend to reproduce, but the defect is the
    /// re-entrancy, and the EditingControlShowing handler below re-enters by exactly the same route.
    /// </remarks>
    public class DataGridViewBeginEditReentrancyTests
    {
        private static DataGridView GridWithData ()
        {
            var grid = new DataGridView { Width = 400, Height = 300 };
            grid.Columns.Add ("Name", 100);
            grid.Columns.Add ("City", 100);
            grid.Rows.Add ("ada", "london");
            grid.Rows.Add ("grace", "new york");
            return grid;
        }

        [Fact]
        public void A_handler_that_ends_the_edit_does_not_crash_BeginEdit ()
        {
            using var grid = GridWithData ();
            grid.EditingControlShowing += (_, _) => grid.EndEdit ();

            grid.BeginEdit (0, 0);   // threw NullReferenceException before the fix
        }

        [Fact]
        public void A_handler_that_ends_the_edit_leaves_the_grid_not_editing ()
        {
            using var grid = GridWithData ();
            grid.EditingControlShowing += (_, _) => grid.EndEdit ();

            grid.BeginEdit (0, 0);

            Assert.False (grid.IsCurrentCellInEditMode);
        }

        [Fact]
        public void A_handler_that_starts_a_different_edit_does_not_crash ()
        {
            using var grid = GridWithData ();
            var reentered = false;

            grid.EditingControlShowing += (_, _) => {
                if (reentered)
                    return;

                reentered = true;
                grid.BeginEdit (1, 1);
            };

            grid.BeginEdit (0, 0);

            Assert.True (reentered);
        }

        [Fact]
        public void An_undisturbed_BeginEdit_still_enters_edit_mode ()
        {
            using var grid = GridWithData ();
            grid.BeginEdit (0, 0);

            Assert.True (grid.IsCurrentCellInEditMode);
        }

        [Fact]
        public void EditingControlShowing_still_receives_the_editor ()
        {
            using var grid = GridWithData ();
            Control? shown = null;
            grid.EditingControlShowing += (_, e) => shown = e.Control;

            grid.BeginEdit (0, 0);

            Assert.NotNull (shown);
        }

        [Fact]
        public void EndEdit_after_a_normal_BeginEdit_commits_the_value ()
        {
            using var grid = GridWithData ();
            grid.BeginEdit (0, 0);
            grid.EndEdit ();

            Assert.False (grid.IsCurrentCellInEditMode);
            Assert.Equal ("ada", grid.Rows[0].Cells[0].Value);
        }
    }
}
