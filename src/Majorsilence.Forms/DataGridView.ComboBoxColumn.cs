using System;
using System.Collections;
using System.ComponentModel;
using System.Diagnostics.CodeAnalysis;
using System.Linq;

namespace Majorsilence.Forms
{
    // DGV-26: the combo-box column showed the raw value and edited as free text.
    //
    // The universal lookup column -- a CustomerId cell that displays the customer's NAME -- rendered the
    // id. DataSource, DisplayMember, ValueMember and Items were stored and read by nothing, and
    // BeginEdit hosted a TextBox whatever the column was, so editing a lookup column offered free text
    // instead of the list. DataGridViewComboBoxEditingControl existed and was never constructed.
    public partial class DataGridView
    {
        /// <summary>
        /// The text a combo-box column shows for a stored value: the item whose
        /// <see cref="DataGridViewComboBoxColumn.ValueMember"/> matches it, rendered through its
        /// <see cref="DataGridViewComboBoxColumn.DisplayMember"/>.
        /// </summary>
        /// <remarks>
        /// Returns null when the column is not a lookup column or the value matches no item, which
        /// leaves the caller to fall back to the raw value -- upstream raises <c>DataError</c> for an
        /// unmatched value, and showing the id beats showing nothing while that is not implemented.
        /// </remarks>
        [UnconditionalSuppressMessage ("Trimming", "IL2026",
            Justification = "Resolving a display member is reflection over application types, exactly as upstream; an unresolvable member falls back to the raw value.")]
        internal static string? LookUpDisplayText (DataGridViewColumn? column, object? value)
        {
            if (column is not DataGridViewComboBoxColumn combo || value is null || value is DBNull)
                return null;

            // A statically populated column (Items, no DataSource) displays its items directly -- there
            // is no member to look a value up by, so the value IS the item.
            var items = ResolveItems (combo);

            if (items is null)
                return null;

            foreach (var item in items) {
                if (item is null)
                    continue;

                if (!Equals (ValueOf (combo, item), value)
                    && !string.Equals (ValueOf (combo, item)?.ToString (), value.ToString (), StringComparison.Ordinal))
                    continue;

                return DisplayOf (combo, item);
            }

            return null;
        }

        [UnconditionalSuppressMessage ("Trimming", "IL2026",
            Justification = "See LookUpDisplayText.")]
        private static IEnumerable? ResolveItems (DataGridViewComboBoxColumn combo)
        {
            if (combo.DataSource is { } source)
                return ListBindingHelper.GetList (source) as IEnumerable;

            return combo.Items.Count > 0 ? combo.Items : null;
        }

        // The value an item contributes: its ValueMember when the column names one, else the item.
        private static object? ValueOf (DataGridViewComboBoxColumn combo, object item)
            => string.IsNullOrEmpty (combo.ValueMember) ? item : ReadMember (item, combo.ValueMember);

        // The text an item shows: its DisplayMember when the column names one, else the item.
        private static string? DisplayOf (DataGridViewComboBoxColumn combo, object item)
            => (string.IsNullOrEmpty (combo.DisplayMember) ? item : ReadMember (item, combo.DisplayMember))?.ToString ();

        // ---------------- editing

        // Builds the editor a column wants. A combo column gets a real combo box bound the same way the
        // display lookup reads it, so the list the user picks from and the text they saw are the same
        // list. Everything else gets the text box.
        private Control CreateEditorFor (DataGridViewColumn? column, DataGridViewCell cell, string text)
        {
            if (column is not DataGridViewComboBoxColumn combo)
                return new TextBox { Text = text };

            var editor = new DataGridViewComboBoxEditingControl {
                EditingControlDataGridView = this,
                EditingControlRowIndex = editing_row_index,
                DropDownStyle = ComboBoxStyle.DropDownList,
                MaxDropDownItems = combo.MaxDropDownItems, // the column's list shape reaches the editor (W6.2 sweep)
            };

            if (combo.DataSource is { } source) {
                editor.DisplayMember = combo.DisplayMember;
                editor.ValueMember = combo.ValueMember;
                editor.DataSource = source;
                editor.SelectedValue = cell.Value;
            } else {
                foreach (var item in combo.Items)
                    editor.Items.Add (item);

                editor.SelectedItem = cell.Value;
            }

            return editor;
        }

        // What the editor currently holds, as the value to commit. A combo box commits its
        // SelectedValue -- the id behind the name -- which is the whole point of a lookup column; the
        // text box commits its text.
        private object? EditorValue ()
            => edit_control switch {
                DataGridViewComboBoxEditingControl combo => combo.SelectedValue ?? combo.SelectedItem,
                TextBox box => box.Text,
                _ => null,
            };
    }
}
