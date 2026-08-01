// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Drawing;

namespace Majorsilence.Forms
{
    /// <summary>
    ///  Provides data for the <see cref='Control.Pinch'/> event, raised while two touch or pen
    ///  contacts move relative to each other (pinch-to-zoom and two-finger rotate).
    /// </summary>
    public class PinchGestureEventArgs : EventArgs
    {
        /// <summary>
        ///  Initializes a new instance of the <see cref='PinchGestureEventArgs'/> class.
        /// </summary>
        public PinchGestureEventArgs (int x, int y, double scale, double angle, double angleDelta)
        {
            X = x;
            Y = y;
            Scale = scale;
            Angle = angle;
            AngleDelta = angleDelta;
        }

        /// <summary>
        ///  Gets the x-coordinate of the midpoint between the two contacts.
        /// </summary>
        public int X { get; }

        /// <summary>
        ///  Gets the y-coordinate of the midpoint between the two contacts.
        /// </summary>
        public int Y { get; }

        /// <summary>
        ///  Gets the midpoint between the two contacts.
        /// </summary>
        public Point Location => new Point (X, Y);

        /// <summary>
        ///  Gets the pinch scale factor, cumulative since the two contacts first touched down (1.0 at
        ///  the start of the gesture; greater than 1.0 as the contacts move apart, less than 1.0 as
        ///  they move together). Unlike <see cref="AngleDelta"/>, this is not a per-event delta.
        /// </summary>
        public double Scale { get; }

        /// <summary>
        ///  Gets the rotation, in degrees, accumulated since the two contacts first touched down.
        ///  The zero-reference is backend-defined (e.g. the absolute angle of the line between the
        ///  two contacts on some backends, vs. rotation-since-gesture-start on others) -- prefer
        ///  <see cref="AngleDelta"/> for incremental rotation, which is consistent across backends.
        /// </summary>
        public double Angle { get; }

        /// <summary>
        ///  Gets the change in <see cref="Angle"/> since the previous <see cref='Control.Pinch'/>
        ///  event (unlike <see cref="Scale"/>, this is a per-event delta, not cumulative). Positive is
        ///  clockwise, negative is counter-clockwise.
        /// </summary>
        public double AngleDelta { get; }
    }
}
