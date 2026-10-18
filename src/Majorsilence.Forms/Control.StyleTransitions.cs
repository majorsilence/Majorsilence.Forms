using System.Collections.Generic;
using Majorsilence.Forms.Animation;
using Majorsilence.Forms.Layout;
using SkiaSharp;

namespace Majorsilence.Forms
{
    // CSS transitions (ControlStyle.Transition): when the state style CurrentStyle resolves to changes -- hover, press, focus, enable --
    // and the style being entered has a transition, CurrentStyle hands out a style in between the two for the transition's length instead
    // of switching at once.
    //
    // The change is noticed where the style is read, not in the state setters: every source of a state change (mouse, keyboard, code,
    // a parent's Enabled) ends in a repaint that reads CurrentStyle, so this catches all of them, at most a frame late, and never animates
    // a control nobody paints. The in-between style is an ordinary ControlStyle chained to the state style being entered, with only the
    // moving values set on it, so everything that does not move -- fonts, widths, anything not in the transition -- reads through to the
    // real style, and renderers need no change.
    public partial class Control
    {
        private static readonly int s_styleTransitionProperty = PropertyStore.CreateKey ();

        // What each animated value is: the CSS longhand whose timing the transition looks up, in a fixed order.
        private static readonly string[] transition_channel_properties = {
            "background-color", "color",
            "border-left-color", "border-top-color", "border-right-color", "border-bottom-color",
            "border-top-left-radius", "border-top-right-radius", "border-bottom-right-radius", "border-bottom-left-radius",
            "box-shadow",
        };

        private const int ChannelBackground = 0;
        private const int ChannelForeground = 1;
        private const int ChannelBorderColor = 2;    // + side, in BorderSideStyle order: left, top, right, bottom
        private const int ChannelRadius = 6;         // + corner: top-left, top-right, bottom-right, bottom-left
        private const int ChannelShadow = 10;
        private const int ChannelCount = 11;

        private struct TransitionChannel
        {
            public bool Active;
            public SKColor FromColor, ToColor;
            public int FromLength, ToLength;
            public ControlBoxShadow FromShadow, ToShadow;
            public TimeSpan Duration, Delay;
            public EasingFunction Easing;
        }

        private sealed class StyleTransitionState
        {
            public ControlStyle? Target;                     // the state style last resolved
            public ControlStyle? Blend;                      // the in-between style while a transition runs, else null
            public readonly TransitionChannel[] Channels = new TransitionChannel[ChannelCount];
            public TimeSpan? Start;
            public TimeSpan Length;
            public Action<TimeSpan>? OnFrame;
            public bool FrameRequested;

            // One in-between style per state style, made the first time a transition enters it and reused after, so a running
            // transition allocates nothing per frame.
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

        // The state style behind `style`: itself, or the style an in-between transition style is chained to.
        private ControlStyle TransitionTargetOf (ControlStyle style)
            => Properties.GetObject (s_styleTransitionProperty) is StyleTransitionState { Blend: { } blend } state && ReferenceEquals (style, blend)
                ? state.Target!
                : style;

        /// <summary>Gets whether a CSS transition is moving this control between two state styles right now.</summary>
        internal bool IsStyleTransitionRunning => Properties.GetObject (s_styleTransitionProperty) is StyleTransitionState { Blend: not null };

        private ControlStyle ApplyStyleTransition (ControlStyle target)
        {
            // Nothing to track until some style somewhere has a transition: an application that never uses one pays one static read.
            if (!ControlStyle.TransitionsInUse)
                return target;

            if (Properties.GetObject (s_styleTransitionProperty) is not StyleTransitionState state) {
                state = new StyleTransitionState ();
                Properties.SetObject (s_styleTransitionProperty, state);
            }

            var previous = state.Target;

            if (ReferenceEquals (previous, target))
                return state.Blend ?? target;

            state.Target = target;

            // The first look at a control, or a state change while it is not on screen: nothing was shown to move from.
            if (previous is null || IsDisposed || !Visible) {
                StopStyleTransition (state);
                return target;
            }

            var transition = target.GetTransition ();

            if (transition is null || SystemInformation.PrefersReducedMotion) {
                StopStyleTransition (state);
                return target;
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

            if (!any || length <= TimeSpan.Zero) {
                StopStyleTransition (state);
                return target;
            }

            state.Blend = state.BlendFor (target);
            state.Start = null;
            state.Length = length;
            WriteBlend (state, TimeSpan.Zero);

            if (!state.FrameRequested) {
                state.FrameRequested = true;
                RequestAnimationFrame (state.OnFrame ??= timestamp => OnStyleTransitionFrame (state, timestamp));
            }

            return state.Blend;
        }

        private static void StopStyleTransition (StyleTransitionState state) => state.Blend = null;

        private void OnStyleTransitionFrame (StyleTransitionState state, TimeSpan timestamp)
        {
            state.FrameRequested = false;

            if (state.Blend is null)
                return;

            if (IsDisposed) {
                StopStyleTransition (state);
                return;
            }

            // The first frame is time zero, as for Animator, so the start values show for a whole frame and the movement takes its full length.
            state.Start ??= timestamp;
            var elapsed = timestamp - state.Start.Value;

            if (elapsed >= state.Length) {
                StopStyleTransition (state);
                Invalidate ();
                return;
            }

            WriteBlend (state, elapsed);
            Invalidate ();

            state.FrameRequested = true;
            RequestAnimationFrame (state.OnFrame!);
        }

        // Reads one value at both ends; false when they are the same, so there is nothing to move.
        private bool ReadChannel (int index, ControlStyle from, ControlStyle to, ref TransitionChannel channel)
        {
            switch (index) {
                case ChannelBackground:
                    channel.FromColor = EffectiveBackgroundColorFor (from);
                    channel.ToColor = EffectiveBackgroundColorFor (to);
                    return channel.FromColor != channel.ToColor;

                case ChannelForeground:
                    channel.FromColor = EffectiveForegroundColorFor (from);
                    channel.ToColor = EffectiveForegroundColorFor (to);
                    return channel.FromColor != channel.ToColor;

                case < ChannelRadius:
                    channel.FromColor = SideOf (from.Border, index - ChannelBorderColor).GetColor ();
                    channel.ToColor = SideOf (to.Border, index - ChannelBorderColor).GetColor ();
                    return channel.FromColor != channel.ToColor;

                case < ChannelShadow:
                    channel.FromLength = CornerOf (from.Border, index - ChannelRadius);
                    channel.ToLength = CornerOf (to.Border, index - ChannelRadius);
                    return channel.FromLength != channel.ToLength;

                default:
                    // CSS interpolates "no shadow" as a zero-offset transparent one, so a shadow grows out of, or shrinks into, nothing.
                    channel.FromShadow = from.GetBoxShadow () ?? default;
                    channel.ToShadow = to.GetBoxShadow () ?? default;
                    return channel.FromShadow != channel.ToShadow;
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

        // Puts the values for `elapsed` on the in-between style; a value not moving is left unset so it reads through to the target.
        private static void WriteBlend (StyleTransitionState state, TimeSpan elapsed)
        {
            var blend = state.Blend!;
            var border = blend.Border;

            for (var i = 0; i < ChannelCount; i++) {
                ref var channel = ref state.Channels[i];

                if (!channel.Active) {
                    ClearChannel (blend, i);
                    continue;
                }

                var progress = Progress (ref channel, elapsed);

                switch (i) {
                    case ChannelBackground:
                        blend.BackgroundColor = InterpolateColor (channel.FromColor, channel.ToColor, progress);
                        break;
                    case ChannelForeground:
                        blend.ForegroundColor = InterpolateColor (channel.FromColor, channel.ToColor, progress);
                        break;
                    case < ChannelRadius:
                        SideOf (border, i - ChannelBorderColor).Color = InterpolateColor (channel.FromColor, channel.ToColor, progress);
                        break;
                    case < ChannelShadow:
                        SetCorner (border, i - ChannelRadius, Math.Max (0, InterpolateLength (channel.FromLength, channel.ToLength, progress)));
                        break;
                    default:
                        blend.BoxShadow = new ControlBoxShadow (
                            InterpolateLength (channel.FromShadow.OffsetX, channel.ToShadow.OffsetX, progress),
                            InterpolateLength (channel.FromShadow.OffsetY, channel.ToShadow.OffsetY, progress),
                            InterpolateColor (channel.FromShadow.Color, channel.ToShadow.Color, progress));
                        break;
                }
            }
        }

        private static void ClearChannel (ControlStyle blend, int index)
        {
            switch (index) {
                case ChannelBackground:
                    blend.BackgroundColor = null;
                    break;
                case ChannelForeground:
                    blend.ForegroundColor = null;
                    break;
                case < ChannelRadius:
                    SideOf (blend.Border, index - ChannelBorderColor).Color = null;
                    break;
                case < ChannelShadow:
                    SetCorner (blend.Border, index - ChannelRadius, null);
                    break;
                default:
                    blend.BoxShadow = null;
                    break;
            }
        }

        private static void SetCorner (ControlBorderStyle border, int corner, int? radius)
        {
            switch (corner) {
                case 0: border.TopLeftRadius = radius; break;
                case 1: border.TopRightRadius = radius; break;
                case 2: border.BottomRightRadius = radius; break;
                default: border.BottomLeftRadius = radius; break;
            }
        }

        // The eased progress of one value: 0 until its delay has passed (a negative delay starts part-way in), then its timing function
        // over its duration, 1 after.
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
