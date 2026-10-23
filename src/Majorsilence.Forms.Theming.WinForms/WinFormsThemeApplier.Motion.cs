using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Runtime.CompilerServices;
using WF = System.Windows.Forms;

namespace Majorsilence.Forms.Theming.WinForms
{
    // CSS transitions and @keyframes animations on real WinForms controls, for the colours a state or an animation changes. WinForms
    // repaints a changed property at once and has no animation system, so a single UI-thread timer steps the colours along the
    // rule's easing (the same curves the core uses: ThemeCssTiming.Evaluate) and writes them through the properties the states already
    // use (BackColor, ForeColor, a button's border colour, a link's colour, a text box's repainted frame). Only colours move -- not
    // a shadow, a radius or an opacity -- and the timer runs only while something is moving. Nothing moves when Windows has UI
    // effects off (the reduced-motion setting) or WinFormsCssTheme.Animate is false.
    internal static partial class WinFormsThemeApplier
    {
        private sealed class Tween
        {
            public Color From { get; set; }
            public Color To { get; set; }
            public long StartTicks { get; set; }
            public double DurationMs { get; set; }
            public double DelayMs { get; set; }
            public ThemeCssTiming Timing { get; set; } = null!;
        }

        private sealed class KeyframeRun
        {
            public string Names { get; set; } = "";
            public long StartTicks { get; set; }
            public IReadOnlyList<ThemeCssAnimation> Animations { get; set; } = Array.Empty<ThemeCssAnimation> ();
        }

        private sealed class MotionState
        {
            public Dictionary<string, Tween> Tweens { get; } = new ();
            public KeyframeRun? Run { get; set; }
            public bool Hooked { get; set; }
        }

        private static readonly ConditionalWeakTable<WF.Control, MotionState> motion = new ();
        private static readonly List<WeakReference<WF.Control>> moving = new ();
        private static readonly Dictionary<string, ThemeCssTiming> timings = new (StringComparer.Ordinal);
        private static WF.Timer? motion_timer;

        // ---- when motion is allowed --------------------------------------------------------------------

        // Windows' "show animations" setting is the reduced-motion preference of the platform.
        private static bool MotionAllowed => WinFormsCssTheme.Animate && WF.SystemInformation.UIEffectsEnabled;

        private static long Now => Stopwatch.GetTimestamp ();

        private static double MillisecondsSince (long ticks) => (Stopwatch.GetTimestamp () - ticks) * 1000.0 / Stopwatch.Frequency;

        // ---- the entry point the refresh functions use ---------------------------------------------------

        // The colour to write now for one property of a control that is moving to `target`: the target itself when nothing moves, the
        // current frame when a transition or an animation does. `key` names the WinForms property, `cssProperty` the CSS one.
        private static Color Animated (WF.Control control, ApplyContext ctx, string selector, ThemeCssState state, string key, string cssProperty, Color target)
        {
            var ms = motion.GetOrCreateValue (control);

            // An animation that sets this property wins over the style's own value and over a transition, as in CSS.
            if (ms.Run is { } run && AnimationColor (ctx, run, cssProperty, target) is { } animated) {
                ms.Tweens.Remove (key);
                return animated;
            }

            if (!MotionAllowed || !control.IsHandleCreated) {
                ms.Tweens.Remove (key);
                return target;
            }

            var spec = TransitionFor (ctx, selector, state, cssProperty);
            if (spec is null) {
                ms.Tweens.Remove (key);
                return target;
            }

            Color current;
            if (ms.Tweens.TryGetValue (key, out var running)) {
                if (running.To.ToArgb () == target.ToArgb ())
                    return TweenColor (running);

                // Cut short: reverse from what is on screen.
                current = TweenColor (running);
            } else {
                current = ReadColor (control, key);
                if (current.ToArgb () == target.ToArgb ())
                    return target;
            }

            ms.Tweens[key] = new Tween { From = current, To = target, StartTicks = Now, DurationMs = spec.DurationMs, DelayMs = spec.DelayMs, Timing = spec.Timing };
            Register (control, ms);
            return current;
        }

        private static ThemeCssTransition? TransitionFor (ApplyContext ctx, string selector, ThemeCssState state, string cssProperty)
        {
            if (!ctx.Motion.TryGetValue ((selector, state), out var resolved)) {
                var cascade = ctx.Rules.Cascade (selector, Chain (state)).ToList ();
                ctx.Motion[(selector, state)] = resolved = (ThemeCssTransition.Resolve (cascade), ThemeCssAnimation.Resolve (cascade));
            }

            // A later entry for the same property wins, as in CSS; "all" covers every one.
            ThemeCssTransition? found = null;
            foreach (var t in resolved.Transitions)
                if (t.Property == "all" || t.Property == cssProperty)
                    found = t;

            return found is { } f && (f.DurationMs > 0 || f.DelayMs > 0) ? f : null;
        }

        // ---- animations ------------------------------------------------------------------------------------

        // Starts, keeps or stops the keyframe animations the control's rules give its current state: one with the same name across a
        // state change keeps running, a new name starts, and none stops.
        private static void SyncAnimation (WF.Control control, ApplyContext ctx, string selector, ThemeCssState state)
        {
            var ms = motion.GetOrCreateValue (control);

            if (!ctx.Motion.TryGetValue ((selector, state), out var resolved)) {
                var cascade = ctx.Rules.Cascade (selector, Chain (state)).ToList ();
                ctx.Motion[(selector, state)] = resolved = (ThemeCssTransition.Resolve (cascade), ThemeCssAnimation.Resolve (cascade));
            }

            var animations = resolved.Animations;

            if (!MotionAllowed || animations.Count == 0 || !control.IsHandleCreated) {
                ms.Run = null;
                return;
            }

            var names = string.Join ("|", animations.Select (a => a.Name));
            if (ms.Run is { } run && run.Names == names) {
                run.Animations = animations;
                return;
            }

            ms.Run = new KeyframeRun { Names = names, StartTicks = Now, Animations = animations };
            Register (control, ms);
        }

        // The colour of one property the running animations give it now, or null when none of them sets it (or they have ended).
        private static Color? AnimationColor (ApplyContext ctx, KeyframeRun run, string cssProperty, Color baseColor)
        {
            Color? result = null;
            var elapsedMs = MillisecondsSince (run.StartTicks);

            foreach (var animation in run.Animations) {
                if (ctx.Rules.Keyframes (animation.Name) is not { } keyframes)
                    continue;

                var points = new List<(double Offset, Color Color, ThemeCssTiming? Timing)> ();
                foreach (var frame in keyframes.Frames)
                    if (frame.Declarations.LastOrDefault (d => d.Property == cssProperty)?.Value is { Kind: ThemeCssValueKind.Color } value)
                        points.Add ((frame.Offset, Flat (ctx, Color.FromArgb (value.Alpha, value.Red, value.Green, value.Blue)), frame.TimingFunction is { } f ? CachedTiming (f.Css) : null));

                if (points.Count == 0)
                    continue;

                // A property a keyframe list leaves out of from or to animates from or to the control's own value.
                if (points[0].Offset > 0)
                    points.Insert (0, (0, baseColor, null));
                if (points[^1].Offset < 1)
                    points.Add ((1, baseColor, null));

                if (AnimationProgress (animation, elapsedMs) is not { } progress)
                    continue;

                var segment = 0;
                while (segment < points.Count - 2 && progress > points[segment + 1].Offset)
                    segment++;

                var (from, to) = (points[segment], points[segment + 1]);
                var span = to.Offset - from.Offset;
                var local = span <= 0 ? 1 : Math.Min (1, Math.Max (0, (progress - from.Offset) / span));
                var eased = (from.Timing ?? animation.Timing).Evaluate (local);
                result = Lerp (from.Color, to.Color, eased);
            }

            return result;
        }

        // Where in the keyframes (0..1) an animation is after `elapsedMs`, or null when it is not showing: before its delay without
        // a backwards fill, or ended without a forwards one.
        private static double? AnimationProgress (ThemeCssAnimation animation, double elapsedMs)
        {
            var start = animation.Direction is "reverse" or "alternate-reverse" ? 1.0 : 0.0;
            var fillBackwards = animation.FillMode is "backwards" or "both";
            var fillForwards = animation.FillMode is "forwards" or "both";

            if (animation.Paused)
                return start;

            var elapsed = elapsedMs - Math.Max(0, animation.DelayMs);
            if (elapsed < 0)
                return fillBackwards ? start : null;

            if (animation.DurationMs <= 0)
                return fillForwards ? 1 - start : null;

            var iterations = elapsed / animation.DurationMs;

            if (!double.IsPositiveInfinity (animation.IterationCount) && iterations >= animation.IterationCount) {
                if (!fillForwards)
                    return null;

                // Held on the last iteration's final frame.
                var last = Math.Max (0, Math.Ceiling (animation.IterationCount) - 1);
                var whole = animation.IterationCount - Math.Floor (animation.IterationCount);
                return DirectedProgress (animation.Direction, (long) last, whole > 0 ? whole : 1);
            }

            var index = (long) Math.Floor (iterations);
            return DirectedProgress (animation.Direction, index, iterations - index);
        }

        private static double DirectedProgress (string direction, long iteration, double local) => direction switch {
            "reverse" => 1 - local,
            "alternate" => iteration % 2 == 0 ? local : 1 - local,
            "alternate-reverse" => iteration % 2 == 0 ? 1 - local : local,
            _ => local
        };

        private static ThemeCssTiming CachedTiming (string css)
        {
            lock (timings) {
                if (!timings.TryGetValue (css, out var timing))
                    timings[css] = timing = ThemeCssTiming.FromCss (css);

                return timing;
            }
        }

        // ---- the timer ---------------------------------------------------------------------------------------

        private static void Register (WF.Control control, MotionState ms)
        {
            if (!ms.Hooked) {
                ms.Hooked = true;
                control.Disposed += static (sender, _) => {
                    if (sender is WF.Control c && motion.TryGetValue (c, out var state)) {
                        state.Tweens.Clear ();
                        state.Run = null;
                    }
                };
            }

            if (!moving.Exists (w => w.TryGetTarget (out var c) && ReferenceEquals (c, control)))
                moving.Add (new WeakReference<WF.Control> (control));

            if (motion_timer is null) {
                motion_timer = new WF.Timer { Interval = 16 };
                motion_timer.Tick += static (_, _) => Tick ();
            }

            motion_timer.Start ();
        }

        private static void Tick ()
        {
            for (var i = moving.Count - 1; i >= 0; i--) {
                if (!moving[i].TryGetTarget (out var control) || control.IsDisposed || !motion.TryGetValue (control, out var ms)) {
                    moving.RemoveAt (i);
                    continue;
                }

                foreach (var (key, tween) in ms.Tweens.ToArray ()) {
                    var elapsed = MillisecondsSince (tween.StartTicks) - tween.DelayMs;
                    var progress = tween.DurationMs <= 0 ? 1 : elapsed / tween.DurationMs;

                    if (progress >= 1) {
                        ms.Tweens.Remove (key);
                        WriteColor (control, key, tween.To);
                    } else {
                        WriteColor (control, key, TweenColor (tween));
                    }
                }

                // An animation recomputes the whole look each frame (it reads the clock itself).
                if (ms.Run is not null && Current is { } ctx && StateSelectorOf (control) is { } selector && tracking.TryGetValue (control, out var track))
                    RefreshLook (control, ctx, ResolvedState (control, ctx, selector, track));

                if (ms.Tweens.Count == 0 && (ms.Run is null || !HasInfiniteOrRunning (ms.Run)))
                    moving.RemoveAt (i);
            }

            if (moving.Count == 0)
                motion_timer?.Stop ();
        }

        // A run keeps the timer going while any of its animations has not ended.
        private static bool HasInfiniteOrRunning (KeyframeRun run)
        {
            var elapsed = MillisecondsSince (run.StartTicks);
            return run.Animations.Any (a => double.IsPositiveInfinity (a.IterationCount) || elapsed < Math.Max (0, a.DelayMs) + a.DurationMs * a.IterationCount);
        }

        private static Color TweenColor (Tween tween)
        {
            var elapsed = MillisecondsSince (tween.StartTicks) - tween.DelayMs;
            if (elapsed <= 0)
                return tween.From;

            var progress = tween.DurationMs <= 0 ? 1 : Math.Min (1, elapsed / tween.DurationMs);
            return Lerp (tween.From, tween.To, tween.Timing.Evaluate (progress));
        }

        private static Color Lerp (Color from, Color to, double t)
            => Color.FromArgb (255,
                (int) Math.Round (from.R + (to.R - from.R) * t),
                (int) Math.Round (from.G + (to.G - from.G) * t),
                (int) Math.Round (from.B + (to.B - from.B) * t));

        // ---- reading and writing the colours the states own ---------------------------------------------------

        private static Color ReadColor (WF.Control control, string key) => key switch {
            "BackColor" => control.BackColor,
            "ForeColor" => control.ForeColor,
            "BorderColor" when control is WF.Button button => button.FlatAppearance.BorderColor,
            "BorderColor" when states.TryGetValue (control, out var cs) && cs.Border is { } painter => painter.Color,
            "LinkColor" when control is WF.LinkLabel link => link.LinkColor,
            _ => control.ForeColor
        };

        private static void WriteColor (WF.Control control, string key, Color color)
        {
            switch (key) {
                case "BackColor":
                    SetBackColor (control, color);
                    break;
                case "ForeColor":
                    SetForeColor (control, color);
                    break;
                case "BorderColor" when control is WF.Button button:
                    button.FlatAppearance.BorderColor = color;
                    break;
                case "BorderColor" when states.TryGetValue (control, out var cs) && cs.Border is { } painter:
                    painter.Color = color;
                    painter.Refresh ();
                    break;
                case "LinkColor" when control is WF.LinkLabel link:
                    link.LinkColor = color;
                    link.VisitedLinkColor = color;
                    break;
            }
        }
    }
}
