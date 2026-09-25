using System;
using System.ComponentModel;
using System.Diagnostics.CodeAnalysis;
using System.Drawing;
using System.Linq;

namespace Majorsilence.Forms
{
    // In-place value editing (W6 mechanisms). The grid was read-only, which is why
    // PropertyValueChanged was declared and raised by nothing: there was no path that could change a
    // value. A click in the value column of a writable property now opens an editor over the cell --
    // a combo box for a boolean or an enum, a text box otherwise -- and committing it writes through
    // the PropertyDescriptor and announces the change.
    public partial class PropertyGrid
    {
        private Control? editor;
        private PropertyGridEntry? editing_item;

        /// <summary>The editor currently open over a value cell, or null.</summary>
        internal Control? EditingControl => editor;

        /// <summary>The item being edited, or null.</summary>
        internal GridItem? EditingItem => editing_item;

        /// <summary>Whether the item at <paramref name="index"/> can be edited in place.</summary>
        internal bool IsEditable (int index)
            => index >= 0 && index < VisibleRows.Count
               && VisibleRows[index] is { PropertyDescriptor: { IsReadOnly: false } };

        [UnconditionalSuppressMessage ("Trimming", "IL2026", Justification = "PropertyGrid edits through TypeDescriptor at runtime; trimming is not supported for this control.")]
        [UnconditionalSuppressMessage ("AOT", "IL3050", Justification = "PropertyGrid edits through TypeDescriptor at runtime; trimming is not supported for this control.")]
        internal void BeginEdit (int index)
        {
            if (!IsEditable (index) || VisibleRows[index] is not PropertyGridEntry item || item.PropertyDescriptor is not { } property)
                return;

            EndEdit (commit: true);

            var bounds = ValueCellBounds (index);

            if (bounds.Width <= 0 || bounds.Height <= 0)
                return;

            item.CurrentValue = item.Value;

            // A closed set of choices edits in a combo box, as upstream's grid does; everything else
            // is text the type's converter parses back.
            var choices = ChoicesFor (property);

            if (choices is not null) {
                var combo = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList };

                foreach (var choice in choices)
                    combo.Items.Add (choice);

                combo.SelectedItem = ValueTextOf (item);
                combo.SelectedIndexChanged += (_, _) => CommitEdit ();
                editor = combo;
            } else {
                var text = new TextBox { Text = ValueTextOf (item) };
                text.KeyDown += Editor_KeyDown;
                editor = text;
            }

            editing_item = item;
            editor.Bounds = DeviceToLogicalUnits (bounds);
            Controls.Add (editor);
            editor.Select ();
            Invalidate ();
        }

        private void Editor_KeyDown (object? sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Enter) {
                EndEdit (commit: true);
                e.Handled = true;
            } else if (e.KeyCode == Keys.Escape) {
                EndEdit (commit: false);
                e.Handled = true;
            }
        }

        /// <summary>Closes the open editor, writing its value back first when asked.</summary>
        internal void EndEdit (bool commit)
        {
            if (editor is null)
                return;

            if (commit)
                CommitEdit ();

            var closing = editor;
            editor = null;
            editing_item = null;

            if (closing is TextBox box)
                box.KeyDown -= Editor_KeyDown;

            Controls.Remove (closing);
            closing.Dispose ();
            Invalidate ();
        }

        // Writes the editor's text back through the property's converter and announces the change.
        [UnconditionalSuppressMessage ("Trimming", "IL2026", Justification = "PropertyGrid edits through TypeDescriptor at runtime; trimming is not supported for this control.")]
        internal void CommitEdit ()
        {
            if (editor is null || editing_item is not { PropertyDescriptor: { } property } item || _selected_object is null)
                return;

            var text = editor is ComboBox combo ? combo.SelectedItem?.ToString () ?? string.Empty : editor.Text;

            if (text == ValueTextOf (item))
                return;

            var old = item.Value;

            try {
                var parsed = property.Converter.ConvertFromString (text);
                property.SetValue (_selected_object, parsed);
                item.Value = property.GetValue (_selected_object);
            } catch {
                // A value the converter or the setter refuses leaves the property alone, and the row
                // goes back to showing what it holds -- the grid never reports a change that did not
                // happen.
                item.Value = old;
                return;
            }

            item.CurrentValue = item.Value;
            OnPropertyValueChanged (new PropertyValueChangedEventArgs (item, old!));
        }

        /// <summary>The device rectangle of the value cell of the row at <paramref name="index"/>.</summary>
        internal Rectangle ValueCellBounds (int index)
        {
            var row = RowBounds (index);
            var name_width = ScaledNameColumnWidth;

            return new Rectangle (row.Left + name_width + 1, row.Top + 1,
                Math.Max (0, row.Width - name_width - 2), Math.Max (0, row.Height - 2));
        }

        // The values a property can take when they are a closed set: a boolean, or an enum.
        [UnconditionalSuppressMessage ("Trimming", "IL2026", Justification = "PropertyGrid edits through TypeDescriptor at runtime; trimming is not supported for this control.")]
        private static string[]? ChoicesFor (PropertyDescriptor property)
        {
            if (property.PropertyType == typeof (bool))
                return ["False", "True"];

            if (property.PropertyType.IsEnum)
                return Enum.GetNames (property.PropertyType);

            return null;
        }
    }
}
