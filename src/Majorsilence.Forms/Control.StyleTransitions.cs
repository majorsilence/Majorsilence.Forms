using System.Collections.Generic;
using System.Linq;
using Majorsilence.Forms.Animation;
using Majorsilence.Forms.Layout;
using SkiaSharp;

namespace Majorsilence.Forms
{
    // CSS transitions (ControlStyle.Transition) and keyframe animations (ControlStyle.Animation).
    //
    // Transitions: when the state style CurrentStyle resolves to changes -- hover, press, focus, enable -- and the style being entered
    // has a transition, CurrentStyle hands out a style in between the two for the transition's length instead of switching at once.
    // Animations: while the state style has one, CurrentStyle hands out a style carrying the animation's values for the moment.
    //
    // The change is noticed where the style is read, not in the state setters: every source of a state change (mouse, keyboard, code,
    // a parent's Enabled) ends in a repaint that reads CurrentStyle, so this catches all of them, at most a frame late, and never animates
    // a control nobody paints. The in-between style is an ordinary ControlStyle chained to the state style, with only the moving values
    // set on it, so everything that does not move -- fonts, widths, anything not animated -- reads through to the real style, and
    // renderers need no change. Animation values are written after transition values, so an animation wins, as in CSS.
    public partial class Control
    {
        private static readonly int s_styleTransitionProperty = PropertyStore.CreateKey ();

        // What each animated value is: the CSS longhand whose timing a transition looks up, in a fixed order.
        private static readonly string[] transition_channel_properties = {
            "background-color", "color",
            "border-left-color", "border-top-color", "border-right-color", "border-bottom-color",
            "border-top-left-radius", "border-top-right-radius", "border-bottom-right-radius", "border-bottom-left-radius",
            "box-shadow",
            "opacity",
            "outline-color", "outline-width", "outline-offset",
        };

        private const int ChannelBackground = 0;
        private const int ChannelForeground = 1;
        private const int ChannelBorderColor = 2;    // + side, in BorderSideStyle order: left, top, right, bottom
        private const int ChannelRadius = 6;         // + corner: top-left, top-right, bottom-right, bottom-left
        private const int ChannelShadow = 10;
        private const int ChannelOpacity = 11;
        private const int ChannelOutlineColor = 12;
        private const int ChannelOutlineWidth = 13;
        private const int ChannelOutlineOffset = 14;
        private const int ChannelCount = 15;

        // One channel's value; which member means something depends on the channel.
        private struct ChannelValue
        {
            public SKColor Color;
            public int Length;
            public float Number;
            public ControlBoxShadow[]? Shadows;
        }

        private struct TransitionChannel
        {
            public bool Active;
            public ChannelValue From, To;
            public ControlBoxShadow[]? Shadows;   // the in-between shadows, reused every frame
            public TimeSpan Duration, Delay;
            public EasingFunction Easing;
        }

        // One keyframe of one channel of a running animation.
        private struct Key
        {
            public double Offset;
            public ChannelValue Value;
            public EasingFunction? Easing;
        }

        private sealed class RunningAnimation
        {
            public ControlKeyframes Keyframes = null!;
            public TimeSpan? Start;
            public readonly Key[]?[] Tracks = new Key[]?[ChannelCount];
            public readonly ControlBoxShadow[]?[] ShadowBuffers = new ControlBoxShadow[]?[ChannelCount];
        }

        private sealed class StyleTransitionState
        {
            public StyleTransitionState (Control owner) => Owner = owner;

            public Control Owner { get; }

            // The frame callback is a method of this state, not a lambda in ApplyStyleTransition: a lambda capturing a local makes the
            // compiler allocate its closure on every call of the method, which is every read of CurrentStyle.
            public void Frame (TimeSpan timestamp) => Owner.OnStyleTransitionFrame (this, timestamp);

            public ControlStyle? Target;                     // the state style last resolved
            public ControlStyle? Blend;                      // the in-between style while a transition or animation runs, else null
            public int Generation = -1;                      // ControlStyle.Generation when the animation was last resolved

            public bool TransitionRunning;
            public readonly TransitionChannel[] Channels = new TransitionChannel[ChannelCount];
            public TimeSpan? Start;
            public TimeSpan Length;

            public ControlAnimation? Animation;
            public readonly List<RunningAnimation> Animations = new ();

            public TimeSpan? Now;                            // the latest frame's timestamp
            public bool NeedsFrames;                         // false once every animation has finished or is paused
            public Action<TimeSpan>? OnFrame;
            public bool FrameRequested;

            // One in-between style per state style, made the first time one is needed and reused after, so a running transition or
            // animation allocates nothing per frame.
            public readonly List<KeyValuePair<ControlStyle, ControlStyle>> Blends = new (2);

            public ControlStyle BlendFor (ControlStyle target)
            {
                foreach (var pair in Blends)
                    if (ReferenceEquals (pair.Key, target))
                        return pair.Value;

                var blend = new ControlStyle (target);
                Blends.Add (new KeyValuePair<ControlStyle, ControlStyle> (target, blend));
                return blend;
            }
        }

        // The state style behind `style`: itself, or the style an in-between style is chained to.
        private ControlStyle TransitionTargetOf (ControlStyle style)
            => Properties.GetObject (s_styleTransitionProperty) is StyleTransitionState { Blend: { } blend } state && ReferenceEquals (style, blend)
                ? state.Target!
                : style;

        /// <summary>Gets whether a CSS transition is moving this control between two state styles right now.</summary>
        internal bool IsStyleTransitionRunning => Properties.GetObject (s_styleTransitionProperty) is StyleTransitionState { TransitionRunning: true };

        /// <summary>Gets whether a CSS keyframe animation is playing on this control right now.</summary>
        internal bool IsStyleAnimationRunning => Properties.GetObject (s_styleTransitionProperty) is StyleTransitionState { Blend: not null } state && state.Animations.Count > 0;

        private ControlStyle ApplyStyleTransition (ControlStyle target)
        {
            // Nothing to track until some style somewhere has a transition or an animation: an application that never uses one pays
            // one static read.
            if (!ControlStyle.TransitionsInUse)
                return target;

            if (Properties.GetObject (s_styleTransitionProperty) is not StyleTransitionState state) {
                state = new StyleTransitionState (this);
                Properties.SetObject (s_styleTransitionProperty, state);
            }

            var previous = state.Target;

            if (ReferenceEquals (previous, target) && state.Generation == ControlStyle.Generation) {
                // An animation whose frames stopped while the control was hidden picks up again when it is painted.
                if (state.Animations.Count > 0 && state.NeedsFrames && !state.FrameRequested)
                    RequestStyleFrame (state);
                return state.Blend ?? target;
            }

            state.Target = target;
            state.Generation = ControlStyle.Generation;

            if (!ReferenceEquals (previous, target))
                StartTransition (state, previous, target);

            UpdateAnimations (state, target);

            if (!state.TransitionRunning && state.Animations.Count == 0) {
                state.Blend = null;
                return target;
            }

            state.Blend = state.BlendFor (target);
            state.NeedsFrames = true;
            WriteBlend (state);
            RequestStyleFrame (state);

            return state.Blend;
        }

        // Invalidate without raising Invalidated: a frame of an animation is the framework's own repaint, and the event's arguments
        // would be the only thing a frame allocates (KeyframeAnimationBenchmarks).
        private void RepaintForAnimation ()
        {
            if (!Created)
                return;

            SetState (States.IsDirty, true);
            FindWindow ()?.Invalidate (Bounds);
        }

        private void RequestStyleFrame (StyleTransitionState state)
        {
            if (state.FrameRequested)
                return;

            state.FrameRequested = true;
            RequestAnimationFrame (state.OnFrame ??= state.Frame);
        }

        // ---- transitions ------------------------------------------------------------------------------

        private void StartTransition (StyleTransitionState state, ControlStyle? previous, ControlStyle target)
        {
            // The first look at a control, or a state change while it is not on screen: nothing was shown to move from.
            if (previous is null || IsDisposed || !Visible) {
                state.TransitionRunning = false;
                return;
            }

            var transition = target.GetTransition ();

            if (transition is null || SystemInformation.PrefersReducedMotion) {
                state.TransitionRunning = false;
                return;
            }

            // Start from what is on screen now: the in-between values when a transition is cut short (hover off before hover-in
            // finished), the previous state's otherwise.
            var shown = state.Blend ?? previous;
            var any = false;
            var length = TimeSpan.Zero;

            for (var i = 0; i < ChannelCount; i++) {
                ref var channel = ref state.Channels[i];
                channel.Active = false;

                if (!transition.TryGetTiming (transition_channel_properties[i], out var duration, out var delay, out var timing))
                    continue;

                if (!ReadChannel (i, shown, target, ref channel))
                    continue;

                channel.Active = true;
                channel.Duration = duration;
                channel.Delay = delay;
                channel.Easing = timing.Function;
                any = true;

                var end = delay + duration;
                if (end > length)
                    length = end;
            }

            state.TransitionRunning = any && length > TimeSpan.Zero;
            state.Start = null;
            state.Length = length;
        }

        // Reads one value at both ends; false when they are the same, so there is nothing to move.
        private bool ReadChannel (int index, ControlStyle from, ControlStyle to, ref TransitionChannel channel)
        {
            if (index == ChannelShadow)
                return ReadShadows (from.GetBoxShadows (), to.GetBoxShadows (), ref channel);

            channel.From = ResolvedValue (index, from);
            channel.To = ResolvedValue (index, to);
            return !SameValue (index, channel.From, channel.To);
        }

        // A channel's value as painting resolves it from `style`.
        private ChannelValue ResolvedValue (int index, ControlStyle style)
        {
            var value = new ChannelValue ();

            switch (index) {
                case ChannelBackground: value.Color = EffectiveBackgroundColorFor (style); break;
                case ChannelForeground: value.Color = EffectiveForegroundColorFor (style); break;
                case < ChannelRadius: value.Color = SideOf (style.Border, index - ChannelBorderColor).GetColor (); break;
                case < ChannelShadow: value.Length = CornerOf (style.Border, index - ChannelRadius); break;
                case ChannelShadow: value.Shadows = style.GetBoxShadows ().ToArray (); break;
                case ChannelOpacity: value.Number = style.GetOpacity (); break;
                case ChannelOutlineColor: value.Color = style.GetOutlineColor (); break;
                case ChannelOutlineWidth: value.Length = style.GetOutlineWidth (); break;
                default: value.Length = style.GetOutlineOffset (); break;
            }

            return value;
        }

        private static bool SameValue (int index, ChannelValue a, ChannelValue b) => index switch {
            ChannelBackground or ChannelForeground or ChannelOutlineColor or < ChannelRadius => a.Color == b.Color,
            ChannelOpacity => a.Number == b.Number,
            ChannelShadow => (a.Shadows ?? Array.Empty<ControlBoxShadow> ()).SequenceEqual (b.Shadows ?? Array.Empty<ControlBoxShadow> ()),
            _ => a.Length == b.Length
        };

        // Shadow lists move pairwise, the shorter one padded with zero-length transparent shadows, so a shadow grows out of, or
        // shrinks into, nothing -- CSS's rule. A pair where one is inset and the other not has no in-between, so the list switches.
        private static bool ReadShadows (IReadOnlyList<ControlBoxShadow> from, IReadOnlyList<ControlBoxShadow> to, ref TransitionChannel channel)
        {
            if (from.SequenceEqual (to) || !TryPairShadows (from, to, out var a, out var b))
                return false;

            channel.From.Shadows = a;
            channel.To.Shadows = b;
            channel.Shadows = new ControlBoxShadow[a.Length];
            return true;
        }

        private static bool TryPairShadows (IReadOnlyList<ControlBoxShadow> from, IReadOnlyList<ControlBoxShadow> to, out ControlBoxShadow[] a, out ControlBoxShadow[] b)
        {
            var count = Math.Max (from.Count, to.Count);
            a = new ControlBoxShadow[count];
            b = new ControlBoxShadow[count];

            for (var i = 0; i < count; i++) {
                var inset = i < from.Count ? from[i].Inset : to[i].Inset;
                a[i] = i < from.Count ? from[i] : new ControlBoxShadow (0, 0, 0, 0, SKColors.Transparent, inset);
                b[i] = i < to.Count ? to[i] : new ControlBoxShadow (0, 0, 0, 0, SKColors.Transparent, a[i].Inset);

                if (a[i].Inset != b[i].Inset)
                    return false;
            }

            return true;
        }

        // ---- animations -------------------------------------------------------------------------------

        // Starts, keeps or stops the animations of `target`'s resolved animation: one whose keyframes are still named keeps running
        // (its time goes on, as in CSS), a new name starts from the beginning, a dropped one stops. Tracks are rebuilt either way,
        // since a keyframe that leaves a property out animates from the style underneath, which may have changed.
        private void UpdateAnimations (StyleTransitionState state, ControlStyle target)
        {
            var animation = SystemInformation.PrefersReducedMotion || IsDisposed ? null : target.GetAnimation ();
            var keyframes = animation?.Keyframes ?? Array.Empty<ControlKeyframes> ();

            var kept = new List<RunningAnimation> (keyframes.Count);

            foreach (var frames in keyframes) {
                var running = state.Animations.FirstOrDefault (r => ReferenceEquals (r.Keyframes, frames) && !kept.Contains (r))
                    ?? new RunningAnimation { Keyframes = frames };
                BuildTracks (running, target);
                kept.Add (running);
            }

            state.Animation = keyframes.Count > 0 ? animation : null;
            state.Animations.Clear ();
            state.Animations.AddRange (kept);
        }

        private void BuildTracks (RunningAnimation running, ControlStyle target)
        {
            var frames = running.Keyframes.Frames;
            var styles = new ControlStyle[frames.Count];

            for (var i = 0; i < frames.Count; i++) {
                styles[i] = new ControlStyle (Control.DefaultStyle);
                frames[i].Apply (styles[i]);
            }

            for (var channel = 0; channel < ChannelCount; channel++) {
                var keys = new List<Key> ();

                for (var i = 0; i < frames.Count; i++)
                    if (KeyframeValue (channel, styles[i], out var value))
                        keys.Add (new Key { Offset = frames[i].Offset, Value = value, Easing = frames[i].TimingFunction?.Function });

                if (keys.Count == 0) {
                    running.Tracks[channel] = null;
                    continue;
                }

                // A keyframe list that leaves out 0% or 100% animates from or to the style underneath (CSS's implicit keyframes).
                var underlying = ResolvedValue (channel, target);
                if (keys[0].Offset > 0)
                    keys.Insert (0, new Key { Offset = 0, Value = underlying, Easing = null });
                if (keys[keys.Count - 1].Offset < 1)
                    keys.Add (new Key { Offset = 1, Value = underlying, Easing = null });

                if (channel == ChannelShadow && !PadShadowTrack (keys)) {
                    running.Tracks[channel] = null;
                    continue;
                }

                running.Tracks[channel] = keys.ToArray ();
                running.ShadowBuffers[channel] = channel == ChannelShadow ? new ControlBoxShadow[keys[0].Value.Shadows!.Length] : null;
            }
        }

        // Every keyframe's shadow list padded to the same length, so a frame interpolates pairwise without allocating; false when
        // two keyframes disagree on inset, which has no in-between.
        private static bool PadShadowTrack (List<Key> keys)
        {
            var count = keys.Max (k => k.Value.Shadows?.Length ?? 0);
            var template = keys.Select (k => k.Value.Shadows ?? Array.Empty<ControlBoxShadow> ()).First (s => s.Length == count);

            for (var i = 0; i < keys.Count; i++) {
                var list = keys[i].Value.Shadows ?? Array.Empty<ControlBoxShadow> ();
                var padded = new ControlBoxShadow[count];

                for (var k = 0; k < count; k++) {
                    padded[k] = k < list.Length ? list[k] : new ControlBoxShadow (0, 0, 0, 0, SKColors.Transparent, template[k].Inset);
                    if (padded[k].Inset != template[k].Inset)
                        return false;
                }

                var key = keys[i];
                key.Value.Shadows = padded;
                keys[i] = key;
            }

            return true;
        }

        // The value a keyframe sets for a channel, read from the keyframe's own style (not inherited); false when it sets none.
        private static bool KeyframeValue (int index, ControlStyle style, out ChannelValue value)
        {
            value = new ChannelValue ();

            switch (index) {
                case ChannelBackground:
                    if (style.BackgroundColor is not { } background)
                        return false;
                    value.Color = background;
                    return true;
                case ChannelForeground:
                    if (style.ForegroundColor is not { } foreground)
                        return false;
                    value.Color = foreground;
                    return true;
                case < ChannelRadius:
                    if (SideOf (style.Border, index - ChannelBorderColor).Color is not { } side)
                        return false;
                    value.Color = side;
                    return true;
                case < ChannelShadow:
                    var corner = index - ChannelRadius;
                    var own = corner switch { 0 => style.Border.TopLeftRadius, 1 => style.Border.TopRightRadius, 2 => style.Border.BottomRightRadius, _ => style.Border.BottomLeftRadius };
                    if ((own ?? style.Border.Radius) is not { } radius)
                        return false;
                    value.Length = radius;
                    return true;
                case ChannelShadow:
                    if (style.BoxShadows is not { } shadows)
                        return false;
                    value.Shadows = shadows.ToArray ();
                    return true;
                case ChannelOpacity:
                    if (style.Opacity is not { } opacity)
                        return false;
                    value.Number = opacity;
                    return true;
                case ChannelOutlineColor:
                    if (style.OutlineColor is not { } outline)
                        return false;
                    value.Color = outline;
                    return true;
                case ChannelOutlineWidth:
                    if (style.OutlineWidth is not { } width)
                        return false;
                    value.Length = width;
                    return true;
                default:
                    if (style.OutlineOffset is not { } offset)
                        return false;
                    value.Length = offset;
                    return true;
            }
        }

        // Where an animation is at `elapsed` since it started: the progress through its keyframes, or null when it has nothing to
        // show (in its delay without a backwards fill, or finished without a forwards fill). `finished` says it needs no more frames.
        private static double? AnimationProgress (ControlAnimation animation, int index, TimeSpan elapsed, out bool finished)
        {
            var (duration, _, delay, iterations, direction, fill, paused) = animation.At (index);
            var local = paused ? TimeSpan.Zero - (delay > TimeSpan.Zero ? delay : TimeSpan.Zero) : elapsed - delay;
            finished = paused;

            double iteration, progress;

            if (local < TimeSpan.Zero) {
                if (fill is not (ControlAnimationFillMode.Backwards or ControlAnimationFillMode.Both))
                    return null;
                iteration = 0;
                progress = 0;
            } else {
                var active = duration.Ticks * iterations;

                if (duration <= TimeSpan.Zero || local.Ticks >= active) {
                    finished = true;
                    if (fill is not (ControlAnimationFillMode.Forwards or ControlAnimationFillMode.Both))
                        return null;

                    // The end of the last iteration: a fractional count stops part-way through it.
                    iteration = iterations > 0 ? Math.Ceiling (iterations) - 1 : 0;
                    var fraction = iterations - Math.Floor (iterations);
                    progress = iterations == 0 ? 0 : fraction == 0 ? 1 : fraction;
                } else {
                    var cycles = local.Ticks / (double) duration.Ticks;
                    iteration = Math.Floor (cycles);
                    progress = cycles - iteration;
                }
            }

            var odd = ((long) iteration & 1) == 1;
            var reversed = direction switch {
                ControlAnimationDirection.Reverse => true,
                ControlAnimationDirection.Alternate => odd,
                ControlAnimationDirection.AlternateReverse => !odd,
                _ => false
            };

            return reversed ? 1 - progress : progress;
        }

        // ---- frames -----------------------------------------------------------------------------------

        private void OnStyleTransitionFrame (StyleTransitionState state, TimeSpan timestamp)
        {
            state.FrameRequested = false;

            if (state.Blend is null)
                return;

            if (IsDisposed) {
                state.Blend = null;
                state.TransitionRunning = false;
                state.Animations.Clear ();
                return;
            }

            // The first frame is time zero, as for Animator, so the start values show for a whole frame and the movement takes its full length.
            state.Now = timestamp;
            state.Start ??= timestamp;

            if (state.TransitionRunning && timestamp - state.Start.Value >= state.Length)
                state.TransitionRunning = false;

            foreach (var running in state.Animations)
                running.Start ??= timestamp;

            var needsFrames = state.TransitionRunning;
            var showing = state.TransitionRunning;

            for (var i = 0; i < state.Animations.Count; i++) {
                var progress = AnimationProgress (state.Animation!, i, timestamp - state.Animations[i].Start!.Value, out var finished);
                needsFrames |= !finished;
                showing |= progress is not null;
            }

            if (!showing) {
                state.Blend = null;
                state.Animations.Clear ();
                RepaintForAnimation ();
                return;
            }

            state.NeedsFrames = needsFrames;
            WriteBlend (state);
            RepaintForAnimation ();

            // An animation that runs for ever stops asking for frames while its control is off screen; painting it again resumes it.
            if (needsFrames && Visible && FindWindow () is not null)
                RequestStyleFrame (state);
        }

        // Puts the values for this moment on the in-between style: the transition's, then the animations' over them. A value nothing
        // moves is left unset, so it reads through to the state style.
        private static void WriteBlend (StyleTransitionState state)
        {
            var blend = state.Blend!;

            for (var i = 0; i < ChannelCount; i++)
                ClearChannel (blend, i);

            if (state.TransitionRunning) {
                var elapsed = state.Now is { } now && state.Start is { } start ? now - start : TimeSpan.Zero;

                for (var i = 0; i < ChannelCount; i++) {
                    ref var channel = ref state.Channels[i];

                    if (!channel.Active)
                        continue;

                    var progress = Progress (ref channel, elapsed);
                    SetChannel (blend, i, Interpolate (i, channel.From, channel.To, progress, channel.Shadows));
                }
            }

            for (var a = 0; a < state.Animations.Count; a++) {
                var running = state.Animations[a];
                var elapsed = state.Now is { } now && running.Start is { } start ? now - start : TimeSpan.Zero;

                if (AnimationProgress (state.Animation!, a, elapsed, out _) is not { } progress)
                    continue;

                var easing = state.Animation!.At (a).Timing.Function;

                for (var i = 0; i < ChannelCount; i++)
                    if (running.Tracks[i] is { } track)
                        SetChannel (blend, i, Sample (i, track, progress, easing, running.ShadowBuffers[i]));
            }
        }

        // A track's value at `progress`: the segment around it, eased by the keyframe's own timing function or the animation's.
        private static ChannelValue Sample (int index, Key[] track, double progress, EasingFunction easing, ControlBoxShadow[]? buffer)
        {
            var k = 0;
            while (k < track.Length - 2 && progress > track[k + 1].Offset)
                k++;

            var from = track[k];
            var to = track[k + 1];
            var span = to.Offset - from.Offset;
            var local = span > 0 ? MathCompat.Clamp ((progress - from.Offset) / span, 0, 1) : 1;
            var eased = (from.Easing ?? easing) ((float) local);

            return Interpolate (index, from.Value, to.Value, eased, buffer);
        }

        private static ChannelValue Interpolate (int index, ChannelValue from, ChannelValue to, float progress, ControlBoxShadow[]? buffer)
        {
            var value = new ChannelValue ();

            switch (index) {
                case ChannelBackground:
                case ChannelForeground:
                case ChannelOutlineColor:
                case < ChannelRadius:
                    value.Color = InterpolateColor (from.Color, to.Color, progress);
                    break;
                case ChannelOpacity:
                    value.Number = from.Number + (to.Number - from.Number) * progress;
                    break;
                case ChannelShadow:
                    var shadows = buffer!;
                    for (var k = 0; k < shadows.Length; k++) {
                        var a = from.Shadows![k];
                        var b = to.Shadows![k];
                        shadows[k] = new ControlBoxShadow (
                            InterpolateLength (a.OffsetX, b.OffsetX, progress),
                            InterpolateLength (a.OffsetY, b.OffsetY, progress),
                            Math.Max (0, InterpolateLength (a.Blur, b.Blur, progress)),
                            InterpolateLength (a.Spread, b.Spread, progress),
                            InterpolateColor (a.Color, b.Color, progress),
                            a.Inset);
                    }
                    value.Shadows = shadows;
                    break;
                default:
                    value.Length = InterpolateLength (from.Length, to.Length, progress);
                    break;
            }

            return value;
        }

        private static void SetChannel (ControlStyle blend, int index, ChannelValue value)
        {
            var border = blend.Border;

            switch (index) {
                case ChannelBackground: blend.BackgroundColor = value.Color; break;
                case ChannelForeground: blend.ForegroundColor = value.Color; break;
                case < ChannelRadius: SideOf (border, index - ChannelBorderColor).Color = value.Color; break;
                case < ChannelShadow: SetCorner (border, index - ChannelRadius, Math.Max (0, value.Length)); break;
                case ChannelShadow: blend.BoxShadows = value.Shadows; break;
                case ChannelOpacity: blend.Opacity = value.Number; break;
                case ChannelOutlineColor: blend.OutlineColor = value.Color; break;
                case ChannelOutlineWidth: blend.OutlineWidth = Math.Max (0, value.Length); break;
                default: blend.OutlineOffset = value.Length; break;
            }
        }

        private static void ClearChannel (ControlStyle blend, int index)
        {
            switch (index) {
                case ChannelBackground: blend.BackgroundColor = null; break;
                case ChannelForeground: blend.ForegroundColor = null; break;
                case < ChannelRadius: SideOf (blend.Border, index - ChannelBorderColor).Color = null; break;
                case < ChannelShadow: SetCorner (blend.Border, index - ChannelRadius, null); break;
                case ChannelShadow: blend.BoxShadows = null; break;
                case ChannelOpacity: blend.Opacity = null; break;
                case ChannelOutlineColor: blend.OutlineColor = null; break;
                case ChannelOutlineWidth: blend.OutlineWidth = null; break;
                default: blend.OutlineOffset = null; break;
            }
        }

        private static BorderSideStyle SideOf (ControlBorderStyle border, int side) => side switch {
            0 => border.Left,
            1 => border.Top,
            2 => border.Right,
            _ => border.Bottom
        };

        private static int CornerOf (ControlBorderStyle border, int corner) => corner switch {
            0 => border.GetTopLeftRadius (),
            1 => border.GetTopRightRadius (),
            2 => border.GetBottomRightRadius (),
            _ => border.GetBottomLeftRadius ()
        };

        private static void SetCorner (ControlBorderStyle border, int corner, int? radius)
        {
            switch (corner) {
                case 0: border.TopLeftRadius = radius; break;
                case 1: border.TopRightRadius = radius; break;
                case 2: border.BottomRightRadius = radius; break;
                default: border.BottomLeftRadius = radius; break;
            }
        }

        // The eased progress of one transition value: 0 until its delay has passed (a negative delay starts part-way in), then its
        // timing function over its duration, 1 after.
        private static float Progress (ref TransitionChannel channel, TimeSpan elapsed)
        {
            var into = elapsed - channel.Delay;

            if (channel.Duration <= TimeSpan.Zero)
                return into >= TimeSpan.Zero ? 1f : 0f;

            if (into <= TimeSpan.Zero)
                return channel.Easing (0f);

            if (into >= channel.Duration)
                return 1f;

            return channel.Easing ((float) (into.TotalMilliseconds / channel.Duration.TotalMilliseconds));
        }

        private static int InterpolateLength (int from, int to, float progress)
            => (int) Math.Round (from + (to - from) * (double) progress);

        // Colours move in premultiplied sRGB, as CSS interpolates them, so fading from transparent does not pass through a dark fringe.
        // Progress may leave 0..1 (an overshooting cubic-bezier); channels are clamped.
        internal static SKColor InterpolateColor (SKColor from, SKColor to, float progress)
        {
            double fa = from.Alpha / 255.0, ta = to.Alpha / 255.0;
            var alpha = fa + (ta - fa) * progress;

            if (alpha <= 0)
                return SKColors.Transparent;

            byte Channel (byte f, byte t)
            {
                var premultiplied = f * fa + (t * ta - f * fa) * progress;
                return (byte) MathCompat.Clamp (Math.Round (premultiplied / alpha), 0, 255);
            }

            return new SKColor (Channel (from.Red, to.Red), Channel (from.Green, to.Green), Channel (from.Blue, to.Blue),
                (byte) MathCompat.Clamp (Math.Round (alpha * 255), 0, 255));
        }
    }
}
