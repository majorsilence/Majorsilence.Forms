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
    /// <see cref="UIElement.ManipulationCompleted"/>'s velocity. That decision, and the pinch-vs-pan
    /// split below, live in <see cref="GestureHeuristics"/> rather than inline here: they are
    /// judgement calls that cannot be verified by running them (no sandbox has multi-touch hardware),
    /// so they are at least unit-tested.
    ///
    /// <b>Units matter here.</b> WinUI reports manipulation velocity in DIP per *millisecond*
    /// (confirmed from Uno's own shipped XML documentation for
    /// <c>Microsoft.UI.Input.ManipulationVelocities.Linear</c>), whereas Avalonia reports swipe
    /// velocity in pixels per *second*. Both feed the same neutral <see cref="Control.Swipe"/> event,
    /// so this backend converts -- otherwise <see cref="SwipeGestureEventArgs.VelocityX"/> would mean
    /// something different depending on which backend an app happened to be running on.
    /// </summary>
    internal static class UnoGestureWiring
    {
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
                // frame's incremental scale/rotation is a deliberate pinch rather than the drift two
                // contacts always show while being dragged across the screen.
                if (GestureHeuristics.IsPinchFrame (e.Delta.Scale, e.Delta.Rotation))
                    owner.HandlePinch (x, y, e.Cumulative.Scale, e.Cumulative.Rotation, e.Delta.Rotation);
                else
                    owner.HandleScrollGesture (x, y, (int)(e.Delta.Translation.X * scale ()), (int)(e.Delta.Translation.Y * scale ()));
            };

            host.ManipulationCompleted += (_, e) => {
                if (e.PointerDeviceType == PointerDeviceType.Mouse)
                    return;

                // DIP per millisecond from WinUI, pixels per second for the neutral event.
                var vx = e.Velocities.Linear.X * GestureHeuristics.MillisecondsPerSecond * scale ();
                var vy = e.Velocities.Linear.Y * GestureHeuristics.MillisecondsPerSecond * scale ();

                if (!GestureHeuristics.TryClassifySwipe (vx, vy, out var direction))
                    return;

                owner.HandleSwipe (
                    (int)(e.Position.X * scale ()), (int)(e.Position.Y * scale ()),
                    vx, vy, direction);
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
