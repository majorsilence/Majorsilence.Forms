// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Drawing;

namespace Majorsilence.Forms
{
    /// <summary>
    ///  Provides data for the <see cref='Control.ScrollGesture'/> event, raised repeatedly while a
    ///  touch or pen drag pans content, and then repeatedly again (with a decaying <see cref="Delta"/>)
    ///  during the momentum/flick phase after the contact is lifted. A <see cref='ScrollableControl'/>
    ///  applies this automatically to <see cref='ScrollableControl.AutoScrollPosition'/>; subscribe to
    ///  this event directly only for custom pan behavior.
    /// </summary>
    public class ScrollGestureEventArgs : EventArgs
    {
        /// <summary>
        ///  Initializes a new instance of the <see cref='ScrollGestureEventArgs'/> class.
        /// </summary>
        public ScrollGestureEventArgs (int x, int y, Point delta)
        {
            X = x;
            Y = y;
            Delta = delta;
        }

        /// <summary>
        ///  Gets the x-coordinate of the drag.
        /// </summary>
        public int X { get; }

        /// <summary>
        ///  Gets the y-coordinate of the drag.
        /// </summary>
        public int Y { get; }

        /// <summary>
        ///  Gets the location of the drag.
        /// </summary>
        public Point Location => new Point (X, Y);

        /// <summary>
        ///  Gets the pixel delta since the previous <see cref='Control.ScrollGesture'/> event.
        /// </summary>
        public Point Delta { get; }
    }
}
