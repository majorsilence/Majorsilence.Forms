using System;
using System.Collections;
using System.ComponentModel;
using System.Drawing;
using System.Linq;
using System.Threading.Tasks;

namespace Majorsilence.Forms
{
    // The last of the overload parity pass — see OverloadParity.cs for what the pass is for.

    public partial class Form
    {
        /// <summary>Shows the form modally, without an explicit owner.</summary>
        /// <remarks>Falls back to the most recently opened form as the owner, which is the same
        /// choice <c>ShowDialog ()</c> already makes.</remarks>
        public Task<DialogResult> ShowDialogAsync ()
            => ShowDialogAsync (Application.OpenForms.LastOrDefault ()!);

        /// <summary>Validates the child controls, limited to those the constraints select.</summary>
        public bool ValidateChildren (ValidationConstraints validationConstraints)
            => ContainerControl.ValidateChildrenCore (Controls, validationConstraints);
    }

    public partial class UserControl
    {
        /// <summary>Validates the child controls, limited to those the constraints select.</summary>
        public bool ValidateChildren (ValidationConstraints validationConstraints)
            => ContainerControl.ValidateChildrenCore (Controls, validationConstraints);
    }

    public partial class PictureBox
    {
        /// <summary>Loads the image named by <see cref="ImageLocation"/>.</summary>
        public void Load ()
        {
            if (!string.IsNullOrEmpty (ImageLocation))
                Load (ImageLocation);
        }

        /// <inheritdoc cref="Load()"/>
        public void LoadAsync ()
        {
            if (!string.IsNullOrEmpty (ImageLocation))
                LoadAsync (ImageLocation);
        }
    }

    public partial class ContextMenu
    {
        /// <summary>Shows the menu at the given point, aligned to the left or right of it.</summary>
        public void Show (Control parent, Point pos, LeftRightAlignment alignment)
        {
            // Right alignment means the menu's right edge meets the point, so it opens leftwards --
            // which is what makes this overload worth having rather than ignoring the argument.
            var location = alignment == LeftRightAlignment.Left
                ? new Point (pos.X - Width, pos.Y)
                : pos;

            Show (parent, location);
        }
    }

    public partial class ToolStripDropDown
    {
        /// <summary>Shows the drop-down at the given point, opening in the given direction.</summary>
        public void Show (Point position, ToolStripDropDownDirection direction)
            => Show (Offset (position, direction));

        /// <inheritdoc cref="Show(Point,ToolStripDropDownDirection)"/>
        public void Show (Control control, Point position, ToolStripDropDownDirection direction)
            => Show (control, Offset (position, direction));

        // The direction says which corner of the drop-down lands on the point. Left-opening variants
        // therefore shift by the width, upward ones by the height.
        private Point Offset (Point position, ToolStripDropDownDirection direction) => direction switch {
            ToolStripDropDownDirection.AboveLeft => new Point (position.X - Width, position.Y - Height),
            ToolStripDropDownDirection.AboveRight => new Point (position.X, position.Y - Height),
            ToolStripDropDownDirection.BelowLeft => new Point (position.X - Width, position.Y),
            ToolStripDropDownDirection.Left => new Point (position.X - Width, position.Y),
            _ => position,
        };
    }

    public partial class TextBox
    {
        /// <summary>Replaces the selection with the given text.</summary>
        /// <remarks>WinForms' <c>Paste (string)</c> pastes a caller-supplied string rather than the
        /// clipboard's, which is why it is not simply <see cref="TextBoxBase.Paste()"/>.</remarks>
        public void Paste (string text) => SelectedText = text ?? string.Empty;
    }

    public partial class DataGridViewRowCollection
    {
        /// <summary>Adds one empty row and returns its index.</summary>
        public int Add () => Add (1);

        /// <summary>Inserts the given number of empty rows at the given index.</summary>
        public void Insert (int rowIndex, int count)
        {
            ArgumentOutOfRangeException.ThrowIfNegative (rowIndex);
            ArgumentOutOfRangeException.ThrowIfGreaterThan (rowIndex, Count);
            ArgumentOutOfRangeException.ThrowIfNegativeOrZero (count);

            for (var i = 0; i < count; i++)
                Insert (rowIndex + i, new DataGridViewRow ());
        }
    }

    public partial class ListViewGroupCollection
    {
        /// <summary>Copies this collection into an array.</summary>
        public void CopyTo (Array dest, int index)
        {
            ArgumentNullException.ThrowIfNull (dest);

            foreach (var group in this)
                dest.SetValue (group, index++);
        }
    }
}
