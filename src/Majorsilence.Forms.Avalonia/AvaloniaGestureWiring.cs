using Avalonia.Input;

namespace Majorsilence.Forms
{
    /// <summary>
    /// Attaches Avalonia's built-in touch/pen gesture recognizers (<see cref="PinchGestureRecognizer"/>,
    /// <see cref="Avalonia.Input.GestureRecognizers.ScrollGestureRecognizer"/>) plus the built-in
    /// <c>Holding</c> long-press event to one of this backend's host controls, translating the
    /// already-computed gesture data into the neutral <see cref="WindowBase"/> gesture pipeline
    /// (<see cref="WindowBase.HandleLongPress"/>/<see cref="WindowBase.HandlePinch"/>/
    /// <see cref="WindowBase.HandleSwipe"/>/<see cref="WindowBase.HandleScrollGesture"/>).
    ///
    /// Every one of these is self-gated to non-mouse pointers by Avalonia itself (confirmed by
    /// reading the actual recognizer sources): Pinch/Scroll ignore anything but Touch/Pen, Swipe's
    /// IsMouseEnabled defaults false, and Holding only fires for a mouse press if IsHoldWithMouseEnabled
    /// is explicitly turned on (also default off). So attaching this unconditionally on every Avalonia
    /// target (desktop, Android, browser) is safe: on a pure-mouse machine none of it ever activates.
    ///
    /// On Android (Avalonia 12) the ScrollGestureRecognizer
    /// frequently never fires even though the touch stream reaches <c>OnPointerMoved</c>, so
    /// <c>MajorsilenceFormsSingleViewHost</c> also synthesises scrolling from raw pointer moves and
    /// passes an <c>onRecognizerScroll</c> callback to switch that fallback off once the real
    /// recognizer proves it works.
    /// </summary>
    internal static class AvaloniaGestureWiring
    {
        /// <summary>
        /// Converts a recognizer scroll delta, in logical pixels, to the finger's own movement in device pixels. Avalonia's delta is
        /// how far the content offset moves (positive when the finger moves up); the neutral pipeline takes the finger's movement,
        /// as the WinUI manipulation does, so it is negated.
        /// </summary>
        internal static (int X, int Y) FingerDelta (double deltaX, double deltaY, double scale)
            => (-(int)(deltaX * scale), -(int)(deltaY * scale));

        internal static void Attach (Avalonia.Controls.Control host, WindowBase owner, System.Func<double> scale,
            System.Action? onRecognizerScroll = null)
        {
            // Neither ScrollGestureEventArgs nor SwipeGestureEventArgs carry a position (only Pinch's
            // ScaleOrigin does), but the neutral pipeline needs one -- RaiseScrollGesture hit-tests by
            // location to find which control (and its nearest ScrollableControl ancestor) the drag is
            // over. Track the live pointer position ourselves rather than touching this class's own
            // OnPointerMoved/OnPointerPressed overrides.
            var lastPosition = default (Avalonia.Point);
            host.PointerPressed += (_, e) => lastPosition = e.GetPosition (host);
            host.PointerMoved += (_, e) => lastPosition = e.GetPosition (host);

            host.GestureRecognizers.Add (new PinchGestureRecognizer ());
            // No SwipeGestureRecognizer: a drag is claimed by the first recognizer whose start distance it crosses, the swipe
            // recognizer's (about 10 px) comes before the scroll recognizer's, and the winner takes the pointer from the others. On
            // Android that left the scroll recognizer with nothing after the first few pixels, so a drag moved the content a third
            // of the way or not at all. The swipe is worked out from the scroll's release velocity instead (ScrollGestureInertiaStarting).
            host.GestureRecognizers.Add (new Avalonia.Input.GestureRecognizers.ScrollGestureRecognizer {
                CanHorizontallyScroll = true,
                CanVerticallyScroll = true,
                IsScrollInertiaEnabled = true
            });

            host.Pinch += (_, e) => owner.HandlePinch (
                (int)(e.ScaleOrigin.X * scale ()), (int)(e.ScaleOrigin.Y * scale ()),
                e.Scale, e.Angle, e.AngleDelta);

            // Avalonia's inertia is the scroll delta's velocity, so the finger moves the other way: negate it for a direction of travel.
            host.ScrollGestureInertiaStarting += (_, e) => {
                var vx = -e.Inertia.X * scale ();
                var vy = -e.Inertia.Y * scale ();
                if (GestureHeuristics.TryClassifySwipe (vx, vy, out var direction))
                    owner.HandleSwipe ((int)(lastPosition.X * scale ()), (int)(lastPosition.Y * scale ()), vx, vy, direction);
            };

            host.ScrollGesture += (_, e) => {
                onRecognizerScroll?.Invoke ();
                var (dx, dy) = FingerDelta (e.Delta.X, e.Delta.Y, scale ());
                owner.HandleScrollGesture ((int)(lastPosition.X * scale ()), (int)(lastPosition.Y * scale ()), dx, dy);
            };

            host.Holding += (_, e) => {
                if (e.HoldingState == HoldingState.Completed)
                    owner.HandleLongPress ((int)(e.Position.X * scale ()), (int)(e.Position.Y * scale ()));
            };
        }
    }
}
