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
        /// <summary>Shows the form modally, without an explicit owner, and without blocking the caller.</summary>
        /// <remarks>Owned by the window <see cref="ShowDialog()"/> would pick -- the innermost modal dialog
        /// showing, otherwise the first open form -- so a call ported from <c>ShowDialog ()</c> to this one
        /// (the browser target needs it; issue #406) lands over the same window. It used to take the most
        /// recently opened form instead, which is not the choice <c>ShowDialog ()</c> makes.</remarks>
        public Task<DialogResult> ShowDialogAsync ()
            => ShowDialogAsync ((Application.ActiveModalForm != this ? Application.ActiveModalForm : null) ?? FindModalOwner (this));

        /// <summary>Shows the form modally, owned by the given window, without blocking the caller.</summary>
        /// <remarks>A control owner means the form it is on, as for <see cref="ShowDialog(IWin32Window)"/>;
        /// anything else gets the ownerless choice of <see cref="ShowDialogAsync()"/>.</remarks>
        public Task<DialogResult> ShowDialogAsync (IWin32Window owner)
            => OwnerFormOf (owner) is { } parent ? ShowDialogAsync (parent) : ShowDialogAsync ();

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
        /// clipboard's, which is why it is not simply <see cref="TextBoxBase.Paste()"/>. Like upstream's
        /// <c>SetSelectedTextInternal (text, clearUndo: false)</c> it ignores <see cref="TextBoxBase.ReadOnly"/>
        /// and <see cref="MaxLength"/> but, unlike <see cref="SelectedText"/>, keeps the undo step and
        /// counts as an edit.</remarks>
        public void Paste (string text)
        {
            ReplaceSelection (text, clearUndo: false);
            ScrollToCaret ();
        }
    }

    public partial class DataGridViewRowCollection
    {
        /// <summary>Adds one empty row and returns its index.</summary>
        public int Add () => Add (1);

        /// <summary>Inserts the given number of empty rows at the given index.</summary>
        public void Insert (int rowIndex, int count)
        {
            Guard.ThrowIfNegative (rowIndex);
            Guard.ThrowIfGreaterThan (rowIndex, Count);
            Guard.ThrowIfNegativeOrZero (count);

            // Insert (int, row) already refuses while bound and creates the cells; this just repeats it.
            for (var i = 0; i < count; i++)
                Insert (rowIndex + i, CreateEmptyRow ());
        }
    }

    public partial class ListViewGroupCollection
    {
        /// <summary>Copies this collection into an array.</summary>
        public void CopyTo (Array dest, int index)
        {
            Guard.ThrowIfNull (dest);

            foreach (var group in this)
                dest.SetValue (group, index++);
        }
    }
}
