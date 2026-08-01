// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Drawing;

namespace Majorsilence.Forms
{
    /// <summary>
    ///  The dominant direction of a <see cref='Control.Swipe'/> gesture.
    /// </summary>
    public enum SwipeDirection
    {
        /// <summary>The swipe moved predominantly to the left.</summary>
        Left,
        /// <summary>The swipe moved predominantly to the right.</summary>
        Right,
        /// <summary>The swipe moved predominantly upward.</summary>
        Up,
        /// <summary>The swipe moved predominantly downward.</summary>
        Down
    }

    /// <summary>
    ///  Provides data for the <see cref='Control.Swipe'/> event, raised for a quick, discrete
    ///  single-direction touch or pen drag (e.g. carousel/paging navigation). For continuous
    ///  drag-to-pan with inertia, see <see cref='Control.ScrollGesture'/> instead.
    /// </summary>
    public class SwipeGestureEventArgs : EventArgs
    {
        /// <summary>
        ///  Initializes a new instance of the <see cref='SwipeGestureEventArgs'/> class.
        /// </summary>
        public SwipeGestureEventArgs (int x, int y, double velocityX, double velocityY, SwipeDirection direction)
        {
            X = x;
            Y = y;
            VelocityX = velocityX;
            VelocityY = velocityY;
            Direction = direction;
        }

        /// <summary>
        ///  Gets the x-coordinate where the swipe was recognized.
        /// </summary>
        public int X { get; }

        /// <summary>
        ///  Gets the y-coordinate where the swipe was recognized.
        /// </summary>
        public int Y { get; }

        /// <summary>
        ///  Gets the location where the swipe was recognized.
        /// </summary>
        public Point Location => new Point (X, Y);

        /// <summary>
        ///  Gets the horizontal swipe velocity, in pixels per second.
        /// </summary>
        public double VelocityX { get; }

        /// <summary>
        ///  Gets the vertical swipe velocity, in pixels per second.
        /// </summary>
        public double VelocityY { get; }

        /// <summary>
        ///  Gets the dominant direction of the swipe.
        /// </summary>
        public SwipeDirection Direction { get; }
    }
}
