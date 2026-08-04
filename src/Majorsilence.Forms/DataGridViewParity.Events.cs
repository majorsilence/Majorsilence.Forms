using System;
using System.Drawing;

namespace Majorsilence.Forms
{
    // The event-args types the DataGridView parity pass needed, and the three delegates that go with
    // them (docs/winforms-gap-plan.md).
    //
    // All three were on item 2's deliberately-absent list, for one reason: they descend from
    // HandledMouseEventArgs, whose upstream constructor takes a scalar `int delta` that this
    // assembly's MouseEventArgs -- which carries a Point delta, so it can report horizontal wheels --
    // could not be chained to. Generating them mechanically was therefore impossible, and they were
    // skipped rather than guessed at. Written by hand here, the mapping is the obvious one: WinForms'
    // scalar delta is the vertical component.

    /// <summary>Provides mouse data for an event a handler can mark as already dealt with.</summary>
    public class HandledMouseEventArgs : MouseEventArgs
    {
        /// <summary>Initializes a new instance of the <see cref="HandledMouseEventArgs"/> class.</summary>
        public HandledMouseEventArgs (MouseButtons button, int clicks, int x, int y, int delta)
            : this (button, clicks, x, y, delta, defaultHandledValue: false) { }

        /// <summary>Initializes a new instance with an explicit initial <see cref="Handled"/> value.</summary>
        public HandledMouseEventArgs (MouseButtons button, int clicks, int x, int y, int delta, bool defaultHandledValue)
            : base (button, clicks, x, y, new Point (0, delta)) => Handled = defaultHandledValue;

        /// <summary>Gets or sets whether the event has been handled and default processing should stop.</summary>
        public bool Handled { get; set; }
    }

    // Both divider-double-click args have to forward an existing HandledMouseEventArgs into their own
    // base constructor, and a base call cannot touch `this`. These readers exist so that work happens
    // in one place instead of twice, and so the null check runs before any member is read.
    internal static class DividerMouse
    {
        internal static MouseButtons Button (HandledMouseEventArgs e) => Require (e).Button;

        internal static int Clicks (HandledMouseEventArgs e) => Require (e).Clicks;

        internal static int X (HandledMouseEventArgs e) => Require (e).X;

        internal static int Y (HandledMouseEventArgs e) => Require (e).Y;

        internal static int Delta (HandledMouseEventArgs e) => Require (e).Delta.Y;

        internal static bool Handled (HandledMouseEventArgs e) => Require (e).Handled;

        private static HandledMouseEventArgs Require (HandledMouseEventArgs e)
        {
            ArgumentNullException.ThrowIfNull (e);
            return e;
        }
    }

    /// <summary>Provides data for a double-click on the divider between two columns.</summary>
    public class DataGridViewColumnDividerDoubleClickEventArgs : HandledMouseEventArgs
    {
        /// <summary>Initializes a new instance of the <see cref="DataGridViewColumnDividerDoubleClickEventArgs"/> class.</summary>
        public DataGridViewColumnDividerDoubleClickEventArgs (int columnIndex, HandledMouseEventArgs e)
            : base (DividerMouse.Button (e), DividerMouse.Clicks (e), DividerMouse.X (e),
                    DividerMouse.Y (e), DividerMouse.Delta (e), DividerMouse.Handled (e))
        {
            ArgumentOutOfRangeException.ThrowIfLessThan (columnIndex, -1);
            ColumnIndex = columnIndex;
        }

        /// <summary>Gets the index of the column to the left of the divider.</summary>
        public int ColumnIndex { get; }

    }

    /// <summary>Provides data for a double-click on the divider between two rows.</summary>
    public class DataGridViewRowDividerDoubleClickEventArgs : HandledMouseEventArgs
    {
        /// <summary>Initializes a new instance of the <see cref="DataGridViewRowDividerDoubleClickEventArgs"/> class.</summary>
        public DataGridViewRowDividerDoubleClickEventArgs (int rowIndex, HandledMouseEventArgs e)
            : base (DividerMouse.Button (e), DividerMouse.Clicks (e), DividerMouse.X (e),
                    DividerMouse.Y (e), DividerMouse.Delta (e), DividerMouse.Handled (e))
        {
            ArgumentOutOfRangeException.ThrowIfLessThan (rowIndex, -1);
            RowIndex = rowIndex;
        }

        /// <summary>Gets the index of the row above the divider.</summary>
        public int RowIndex { get; }
    }

    /// <summary>Provides data for a request for a cell's error text.</summary>
    public class DataGridViewCellErrorTextNeededEventArgs : DataGridViewCellEventArgs
    {
        internal DataGridViewCellErrorTextNeededEventArgs (int columnIndex, int rowIndex, string errorText)
            : base (columnIndex, rowIndex) => ErrorText = errorText;

        /// <summary>Gets or sets the error text to show for the cell.</summary>
        public string ErrorText { get; set; }
    }

    /// <summary>Handles a double-click on a column divider.</summary>
    public delegate void DataGridViewColumnDividerDoubleClickEventHandler (object? sender, DataGridViewColumnDividerDoubleClickEventArgs e);

    /// <summary>Handles a double-click on a row divider.</summary>
    public delegate void DataGridViewRowDividerDoubleClickEventHandler (object? sender, DataGridViewRowDividerDoubleClickEventArgs e);

    /// <summary>Handles a request for a cell's error text.</summary>
    public delegate void DataGridViewCellErrorTextNeededEventHandler (object? sender, DataGridViewCellErrorTextNeededEventArgs e);
}
