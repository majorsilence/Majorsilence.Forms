// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Drawing;

namespace Majorsilence.Forms
{
    /// <summary>
    ///  Provides data for the <see cref='Control.MouseUp'/>, <see cref='Control.MouseDown'/> and
    /// <see cref='Control.MouseMove'/> events.
    /// </summary>
    public class MouseEventArgs : EventArgs
    {
        private readonly Keys key_data;

        /// <summary>
        ///  Initializes a new instance of the <see cref='MouseEventArgs'/> class.
        /// </summary>
        /// <remarks>
        /// The Majorsilence.Forms shape, taking a two-dimensional wheel delta. Backends can report
        /// horizontal wheel movement (trackpads and tilt wheels do), which WinForms' single
        /// <see cref="Delta"/> has no room for; see <see cref="DeltaPoint"/>.
        /// </remarks>
        public MouseEventArgs (MouseButtons button, int clicks, int x, int y, Point delta, int? screenX = null, int? screenY = null, Keys keyData = Keys.None)
        {
            Button = button;
            Clicks = clicks;
            DeltaPoint = delta;
            Delta = delta.Y;
            X = x;
            Y = y;
            ScreenLocation = new Point (screenX ?? x, screenY ?? y);
            key_data = keyData;

            // Keep the static Control.ModifierKeys current for WinForms-compatible callers.
            Majorsilence.Forms.Control.ModifierKeys = keyData & Keys.Modifiers;
        }

        /// <summary>
        ///  Initializes a new instance of the <see cref='MouseEventArgs'/> class, WinForms-style.
        /// </summary>
        /// <remarks>
        /// The WinForms constructor signature, so <c>new MouseEventArgs (button, clicks, x, y, delta)</c>
        /// ports unchanged. The delta is vertical; the horizontal component is zero.
        /// </remarks>
        public MouseEventArgs (MouseButtons button, int clicks, int x, int y, int delta)
            : this (button, clicks, x, y, new Point (0, delta))
        {
        }

        /// <summary>
        ///  Gets which mouse button was pressed.
        /// </summary>
        public MouseButtons Button { get; }

        /// <summary>
        ///  Gets the number of times the mouse button was pressed and released.
        /// </summary>
        public int Clicks { get; }

        /// <summary>
        ///  Gets the x-coordinate of a mouse click.
        /// </summary>
        public int X { get; }

        /// <summary>
        ///  Gets the y-coordinate of a mouse click.
        /// </summary>
        public int Y { get; }

        /// <summary>
        ///  Gets a signed count of the number of detents the mouse wheel has rotated.
        /// </summary>
        /// <remarks>
        /// Vertical movement only, matching WinForms, so <c>if (e.Delta &gt; 0)</c> ports unchanged.
        /// Use <see cref="DeltaPoint"/> when horizontal wheel movement matters.
        /// </remarks>
        public int Delta { get; }

        /// <summary>
        ///  Gets a signed count of the number of detents the mouse wheel has rotated on each axis.
        /// </summary>
        /// <remarks>
        /// Majorsilence.Forms addition: trackpads and tilt wheels report horizontal scrolling, which
        /// WinForms' one-dimensional <see cref="Delta"/> cannot carry. <c>DeltaPoint.Y</c> equals
        /// <see cref="Delta"/>.
        /// </remarks>
        public Point DeltaPoint { get; }

        /// <summary>
        ///  Gets the location of the mouse during MouseEvent.
        /// </summary>
        public Point Location => new Point (X, Y);

        /// <summary>
        /// Get the mouse location in screen coordinates.
        /// </summary>
        public Point ScreenLocation { get; }

        /// <summary>
        /// Gets whether the Control modifier key was also pressed.
        /// </summary>
        public bool Alt => key_data.HasFlag (Keys.Alt);

        /// <summary>
        /// Gets whether the Alt modifier key was also pressed.
        /// </summary>
        public bool Control => key_data.HasFlag (Keys.Control);

        /// <summary>
        /// Gets the modifier keys that were also pressed.
        /// </summary>
        public Keys Modifiers => key_data & Keys.Modifiers;

        /// <summary>
        /// Gets whether the Shift modifier key was also pressed.
        /// </summary>
        public bool Shift => key_data.HasFlag (Keys.Shift);
    }
}
