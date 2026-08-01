using Avalonia.Input;

namespace Majorsilence.Forms
{
    /// <summary>
    /// Attaches Avalonia's built-in touch/pen gesture recognizers (<see cref="PinchGestureRecognizer"/>,
    /// <see cref="Avalonia.Input.GestureRecognizers.SwipeGestureRecognizer"/>,
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
    /// </summary>
    internal static class AvaloniaGestureWiring
    {
        internal static void Attach (Avalonia.Controls.Control host, WindowBase owner, System.Func<double> scale)
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
            host.GestureRecognizers.Add (new Avalonia.Input.GestureRecognizers.SwipeGestureRecognizer {
                CanHorizontallySwipe = true,
                CanVerticallySwipe = true
            });
            host.GestureRecognizers.Add (new Avalonia.Input.GestureRecognizers.ScrollGestureRecognizer {
                CanHorizontallyScroll = true,
                CanVerticallyScroll = true,
                IsScrollInertiaEnabled = true
            });

            host.Pinch += (_, e) => owner.HandlePinch (
                (int)(e.ScaleOrigin.X * scale ()), (int)(e.ScaleOrigin.Y * scale ()),
                e.Scale, e.Angle, e.AngleDelta);

            host.SwipeGesture += (_, e) => owner.HandleSwipe (
                (int)(lastPosition.X * scale ()), (int)(lastPosition.Y * scale ()),
                e.Velocity.X * scale (), e.Velocity.Y * scale (),
                AvaloniaKeyInterop.ToSwipeDirection (e.SwipeDirection));

            host.ScrollGesture += (_, e) => owner.HandleScrollGesture (
                (int)(lastPosition.X * scale ()), (int)(lastPosition.Y * scale ()),
                (int)(e.Delta.X * scale ()), (int)(e.Delta.Y * scale ()));

            host.Holding += (_, e) => {
                if (e.HoldingState == HoldingState.Completed)
                    owner.HandleLongPress ((int)(e.Position.X * scale ()), (int)(e.Position.Y * scale ()));
            };
        }
    }
}
