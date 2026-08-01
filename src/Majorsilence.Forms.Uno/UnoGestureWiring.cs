using System;
using Microsoft.UI.Input;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Input;

namespace Majorsilence.Forms.Uno
{
    /// <summary>
    /// Attaches WinUI's built-in manipulation events (<see cref="UIElement.ManipulationDelta"/>,
    /// <see cref="UIElement.ManipulationCompleted"/>) plus the built-in <see cref="UIElement.Holding"/>
    /// long-press event to one of this backend's host controls, translating the already-computed
    /// gesture data into the neutral <see cref="WindowBase"/> gesture pipeline
    /// (<see cref="WindowBase.HandleLongPress"/>/<see cref="WindowBase.HandlePinch"/>/
    /// <see cref="WindowBase.HandleSwipe"/>/<see cref="WindowBase.HandleScrollGesture"/>).
    ///
    /// Unlike the Avalonia backend, WinUI's manipulation engine is NOT self-gated to non-mouse
    /// pointers (confirmed by decompiling <c>Microsoft.UI.Input.GestureRecognizer.ProcessDownEvent</c>
    /// -- manipulation tracking starts for any pointer type once <see cref="UIElement.ManipulationMode"/>
    /// enables it), so every manipulation handler here explicitly filters out
    /// <see cref="PointerDeviceType.Mouse"/> itself, to keep ordinary desktop mouse-drag interactions
    /// (scrollbar thumb, drag-select, drag-and-drop) unaffected. <see cref="UIElement.Holding"/> does
    /// not need this: subscribing to it only ever sets the low-level recognizer's non-mouse "Hold"
    /// gesture setting, never the separate "HoldWithMouse" one, so it is already safe for a held mouse
    /// button by construction.
    ///
    /// WinUI also has no native swipe gesture (only the unrelated, heavyweight <c>SwipeControl</c> for
    /// reveal-actions) -- <see cref="Control.Swipe"/> is synthesized here from
    /// <see cref="UIElement.ManipulationCompleted"/>'s velocity against <see cref="MinSwipeVelocity"/>,
    /// a chosen heuristic rather than a platform capability.
    /// </summary>
    internal static class UnoGestureWiring
    {
        // Chosen heuristic -- WinUI has no native swipe gesture to read a platform default from.
        private const double MinSwipeVelocity = 500; // px/sec

        internal static void Attach (UIElement host, WindowBase owner, Func<double> scale)
        {
            host.ManipulationMode = ManipulationModes.TranslateX | ManipulationModes.TranslateY
                | ManipulationModes.Rotate | ManipulationModes.Scale
                | ManipulationModes.TranslateInertia | ManipulationModes.RotateInertia | ManipulationModes.ScaleInertia;
            host.IsHoldingEnabled = true;

            host.ManipulationDelta += (_, e) => {
                if (e.PointerDeviceType == PointerDeviceType.Mouse)
                    return;

                var x = (int)(e.Position.X * scale ());
                var y = (int)(e.Position.Y * scale ());

                // WinUI reports pan and pinch/rotate through the same event; split on whether this
                // frame's incremental Scale/Rotation is non-trivial (a real two-finger frame) vs. a
                // pure single-finger pan (Delta.Scale == 1, Delta.Rotation == 0).
                if (e.Delta.Scale != 1f || e.Delta.Rotation != 0f)
                    owner.HandlePinch (x, y, e.Cumulative.Scale, e.Cumulative.Rotation, e.Delta.Rotation);
                else
                    owner.HandleScrollGesture (x, y, (int)(e.Delta.Translation.X * scale ()), (int)(e.Delta.Translation.Y * scale ()));
            };

            host.ManipulationCompleted += (_, e) => {
                if (e.PointerDeviceType == PointerDeviceType.Mouse)
                    return;

                var vx = e.Velocities.Linear.X;
                var vy = e.Velocities.Linear.Y;
                if (vx * vx + vy * vy < MinSwipeVelocity * MinSwipeVelocity)
                    return;

                var direction = Math.Abs (vx) >= Math.Abs (vy)
                    ? (vx >= 0 ? SwipeDirection.Right : SwipeDirection.Left)
                    : (vy >= 0 ? SwipeDirection.Down : SwipeDirection.Up);

                owner.HandleSwipe (
                    (int)(e.Position.X * scale ()), (int)(e.Position.Y * scale ()),
                    vx * scale (), vy * scale (), direction);
            };

            host.Holding += (_, e) => {
                if (e.HoldingState != HoldingState.Completed)
                    return;

                var p = e.GetPosition (host);
                owner.HandleLongPress ((int)(p.X * scale ()), (int)(p.Y * scale ()));
            };
        }
    }
}
