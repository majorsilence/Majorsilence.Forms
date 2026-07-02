using System;
using System.Drawing;
using Majorsilence.Forms;

namespace Majorsilence.Forms
{
    /// <summary>
    ///  A text box cell used by <see cref="DataGrid"/> to display and edit text values.
    /// </summary>
    public class DataGridTextBox : DataGridViewTextBoxCell
    {
        /// <summary>
        ///  Gets or sets the text displayed in the cell.
        /// </summary>
        public string? Text { get => this.Value?.ToString(); set => this.Value = value; }

        /// <summary>
        ///  Gets or sets the parent control that hosts this cell, for compatibility with the WinForms API surface.
        /// </summary>
        public Control? Parent { get; set; }

        /// <summary>
        ///  Gets or sets the location of the cell, for compatibility with the WinForms API surface.
        /// </summary>
        public Point Location { get; set; }

        /// <summary>
        ///  Gets or sets a value indicating whether the cell is visible, for compatibility with the WinForms API surface.
        /// </summary>
        public new bool Visible { get; set; }

        /// <summary>
        ///  Gets or sets the maximum number of characters that can be entered into the cell.
        /// </summary>
        public int MaxLength { get; set; }

#pragma warning disable CS0067 // Event is part of the WinForms-compat surface; this shim never raises it.
        /// <summary>
        ///  Occurs when the text of the cell changes. Never raised by this compat shim.
        /// </summary>
        public event EventHandler? TextChanged;

        /// <summary>
        ///  Occurs when a key is pressed while the cell has focus. Never raised by this compat shim.
        /// </summary>
        public event EventHandler? KeyPress;
#pragma warning restore CS0067
    }
}
