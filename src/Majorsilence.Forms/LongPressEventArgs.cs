// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Drawing;

namespace Majorsilence.Forms
{
    /// <summary>
    ///  Provides data for the <see cref='Control.LongPress'/> event, raised when a touch or pen
    ///  contact is held in place for the platform's press-and-hold duration. Does not fire for the
    ///  mouse (a held mouse button is used for drag/selection instead).
    /// </summary>
    public class LongPressEventArgs : EventArgs
    {
        /// <summary>
        ///  Initializes a new instance of the <see cref='LongPressEventArgs'/> class.
        /// </summary>
        public LongPressEventArgs (int x, int y)
        {
            X = x;
            Y = y;
        }

        /// <summary>
        ///  Gets the x-coordinate of the press.
        /// </summary>
        public int X { get; }

        /// <summary>
        ///  Gets the y-coordinate of the press.
        /// </summary>
        public int Y { get; }

        /// <summary>
        ///  Gets the location of the press.
        /// </summary>
        public Point Location => new Point (X, Y);
    }
}
