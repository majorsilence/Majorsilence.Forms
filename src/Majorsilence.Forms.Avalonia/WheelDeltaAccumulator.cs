namespace Majorsilence.Forms.Backends
{
    /// <summary>
    /// Converts Avalonia's wheel deltas into the units WinForms reports.
    /// </summary>
    /// <remarks>
    /// Avalonia reports LINES -- 1.0 per notch on a mouse, a fraction per frame on a trackpad --
    /// whereas WinForms reports multiples of <see cref="WheelDelta"/> (WHEEL_DELTA = 120), and ported
    /// code divides by that constant, overwhelmingly with integer arithmetic. Casting the Avalonia
    /// value straight to an int therefore delivered 1 where 120 was expected, so one notch scrolled
    /// roughly a hundredth of what it should, and sub-notch trackpad movement truncated to 0 and was
    /// dropped outright.
    ///
    /// The remainder is accumulated so that only whole units are emitted -- which is what Windows does
    /// for a precision touchpad too. Both halves matter: emitting fractions makes
    /// <c>e.Delta / 120</c> come out zero for a gesture the user can see, and discarding them brings
    /// the slow scrolling back in a subtler form.
    ///
    /// Not thread-safe by design: input arrives on the UI thread only.
    /// </remarks>
    internal sealed class WheelDeltaAccumulator
    {
        /// <summary>WinForms' WHEEL_DELTA: the wheel movement of one detent.</summary>
        internal const int WheelDelta = 120;

        private double residualX, residualY;

        /// <summary>
        /// Adds a line-based delta and returns the whole WinForms-unit delta now available, which is
        /// (0, 0) until at least one full unit has accumulated in some direction.
        /// </summary>
        internal System.Drawing.Point Add (double linesX, double linesY)
        {
            residualX += linesX * WheelDelta;
            residualY += linesY * WheelDelta;

            // Truncation toward zero is deliberate and correct for both directions: whatever is left
            // stays in the residual for the next event rather than being rounded away.
            var x = (int)(residualX / WheelDelta) * WheelDelta;
            var y = (int)(residualY / WheelDelta) * WheelDelta;

            residualX -= x;
            residualY -= y;

            return new System.Drawing.Point (x, y);
        }
    }
}
