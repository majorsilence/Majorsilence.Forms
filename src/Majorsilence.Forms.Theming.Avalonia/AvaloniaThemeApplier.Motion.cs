using System.Collections.Generic;
using System.Linq;

namespace Majorsilence.Forms.Theming.Avalonia
{
    using global::Avalonia;
    using global::Avalonia.Animation;
    using global::Avalonia.Animation.Easings;
    using global::Avalonia.Controls;
    using global::Avalonia.Controls.Presenters;
    using global::Avalonia.Media;
    using global::Avalonia.Styling;

    /// <summary>Avalonia's easing seam taking a CSS timing function: <c>cubic-bezier()</c>, the keywords and <c>steps()</c>.</summary>
    internal sealed class ThemeEasing : Easing
    {
        private readonly ThemeCssTiming timing;

        public ThemeEasing (ThemeCssTiming timing) => this.timing = timing;

        public override double Ease (double progress) => timing.Evaluate (progress);
    }

    // CSS transitions and @keyframes animations on native Avalonia controls. Avalonia animates with Transitions (a collection on any
    // object, one entry per property) and Animations (keyframes in a Style). Fluent changes a control's colours by setting them on
    // template parts, so a transition set on the control would never see them; the transitions are therefore set on the same template
    // parts the shadows use (see S.ButtonPart and friends), in the same state selector (Button:pointerover /template/ ...), which makes
    // CSS's rule -- the transition that runs is the one on the style being entered -- fall out of Avalonia's own style activation.
    internal static partial class AvaloniaThemeApplier
    {
        // The template parts and control selectors a CSS selector's motion lands on.
        private sealed class MotionSpec
        {
            public MotionSpec (string selector, bool presenter, Func<ThemeCssState, StyleSelector>[] parts, StyleSelector[] controls)
            {
                Selector = selector;
                Presenter = presenter;
                Parts = parts;
                Controls = controls;
            }

            public string Selector { get; }

            /// <summary>Whether the part is a ContentPresenter (it carries a Foreground) rather than a Border.</summary>
            public bool Presenter { get; }

            public Func<ThemeCssState, StyleSelector>[] Parts { get; }

            public StyleSelector[] Controls { get; }
        }

        private static readonly MotionSpec[] motion_specs = {
            new ("Button", true, new Func<ThemeCssState, StyleSelector>[] { S.ButtonPart, S.ToggleButtonPart }, new[] { S.Button, S.ToggleButton }),
            new ("LinkLabel", true, new Func<ThemeCssState, StyleSelector>[] { S.HyperlinkPart }, new[] { S.Hyperlink }),
            new ("TextBox", false, new Func<ThemeCssState, StyleSelector>[] { S.TextBoxPart, S.DatePickerPart }, new[] { S.TextBox, S.DatePicker }),
            new ("ComboBox", false, new Func<ThemeCssState, StyleSelector>[] { S.ComboBoxPart }, new[] { S.ComboBox }),
            new ("ListBox", false, new Func<ThemeCssState, StyleSelector>[] { S.ListBoxPart }, new[] { S.ListBox }),
            new ("DataGridView", false, new Func<ThemeCssState, StyleSelector>[] { S.DataGridPart }, new[] { S.DataGrid }),
            new ("ProgressBar", false, new Func<ThemeCssState, StyleSelector>[] { S.ProgressBarPart }, new[] { S.ProgressBar }),
        };

        private static readonly ThemeCssState[] motion_states = {
            ThemeCssState.Hover, ThemeCssState.Active, ThemeCssState.Disabled, ThemeCssState.Focus, ThemeCssState.FocusVisible
        };

        // The properties a CSS animatable name stands for, per target: part (template part) and control.
        private static string[] Covered (string cssProperty) => cssProperty switch {
            "all" => new[] { "background-color", "color", "border-color", "border-radius", "box-shadow", "opacity" },
            "border-top-color" or "border-right-color" or "border-bottom-color" or "border-left-color" => new[] { "border-color" },
            "border-top-left-radius" or "border-top-right-radius" or "border-bottom-right-radius" or "border-bottom-left-radius" => new[] { "border-radius" },
            _ => new[] { cssProperty }
        };

        private static void BuildMotion (IReadOnlyList<ThemeStyleSheet> chain, Func<StyleSelector, StyleBuilder> styleFor)
        {
            if (AvaloniaCssTheme.PrefersReducedMotion ?? global::Majorsilence.Forms.SystemInformation.PrefersReducedMotion)
                return;

            // Every declaration in source order per (selector, state), across the chain.
            var declarations = new Dictionary<(string Selector, ThemeCssState State), List<ThemeCssDeclaration>> ();
            var keyframes = new Dictionary<string, ControlKeyframes> (StringComparer.Ordinal);

            foreach (var sheet in chain) {
                foreach (var frames in sheet.Keyframes)
                    keyframes[frames.Name] = frames;

                foreach (var rule in sheet.Rules) {
                    if (rule.Part is not null)
                        continue;

                    var key = (rule.Selector.Name, rule.State);
                    if (!declarations.TryGetValue (key, out var list))
                        declarations[key] = list = new List<ThemeCssDeclaration> ();

                    list.AddRange (rule.Declarations);
                }
            }

            static bool HasMotion (List<ThemeCssDeclaration>? list, string prefix)
                => list is not null && list.Any (d => d.Property.StartsWith (prefix, StringComparison.Ordinal));

            foreach (var spec in motion_specs) {
                var info = ThemeCssReference.FindSelector (spec.Selector)!;

                foreach (var state in new[] { ThemeCssState.Normal }.Concat (motion_states)) {
                    if (state != ThemeCssState.Normal && !info.Supports (state))
                        continue;

                    declarations.TryGetValue ((spec.Selector, state), out var own);
                    declarations.TryGetValue ((spec.Selector, ThemeCssState.Normal), out var resting);

                    // Transitions: the style being entered decides, inheriting the resting rule's longhands it leaves unsaid.
                    if (HasMotion (own, "transition-")) {
                        var cascade = (resting ?? new List<ThemeCssDeclaration> ()).Concat (state == ThemeCssState.Normal ? Array.Empty<ThemeCssDeclaration> () : own!);
                        AddTransitions (spec, state, ThemeCssTransition.Resolve (cascade), styleFor);
                    }

                    // Animations: played while the rule's own style is active.
                    if (HasMotion (own, "animation-"))
                        AddAnimations (spec, state, ThemeCssAnimation.Resolve (own!), keyframes, styleFor);
                }
            }
        }

        // ---- transitions -----------------------------------------------------------------------------------

        private static void AddTransitions (MotionSpec spec, ThemeCssState state, IReadOnlyList<ThemeCssTransition> transitions, Func<StyleSelector, StyleBuilder> styleFor)
        {
            foreach (var part in spec.Parts) {
                var collection = new Transitions ();
                foreach (var transition in transitions)
                    foreach (var property in Covered (transition.Property).Distinct ())
                        if (PartTransition (spec, property, transition) is { } made)
                            collection.Add (made);

                if (collection.Count > 0)
                    styleFor (part (state)).Transitions = collection;
            }

            foreach (var control in spec.Controls) {
                var collection = new Transitions ();
                foreach (var transition in transitions)
                    foreach (var property in Covered (transition.Property).Distinct ())
                        if (ControlTransition (spec, property, transition) is { } made)
                            collection.Add (made);

                if (collection.Count > 0)
                    styleFor (S.InState (control, state)).Transitions = collection;
            }
        }

        // The transition a template part carries for one CSS property, or null when the part has no such property.
        private static ITransition? PartTransition (MotionSpec spec, string property, ThemeCssTransition source)
        {
            var duration = TimeSpan.FromMilliseconds (source.DurationMs);
            var delay = TimeSpan.FromMilliseconds (Math.Max (0, source.DelayMs));
            var easing = new ThemeEasing (source.Timing);

            switch (property) {
                case "background-color":
                    return new BrushTransition { Property = spec.Presenter ? ContentPresenter.BackgroundProperty : Border.BackgroundProperty, Duration = duration, Delay = delay, Easing = easing };
                case "border-color":
                    return new BrushTransition { Property = spec.Presenter ? ContentPresenter.BorderBrushProperty : Border.BorderBrushProperty, Duration = duration, Delay = delay, Easing = easing };
                case "color" when spec.Presenter:
                    return new BrushTransition { Property = ContentPresenter.ForegroundProperty, Duration = duration, Delay = delay, Easing = easing };
                case "box-shadow":
                    return new BoxShadowsTransition { Property = spec.Presenter ? ContentPresenter.BoxShadowProperty : Border.BoxShadowProperty, Duration = duration, Delay = delay, Easing = easing };
                case "border-radius":
                    return new CornerRadiusTransition { Property = spec.Presenter ? ContentPresenter.CornerRadiusProperty : Border.CornerRadiusProperty, Duration = duration, Delay = delay, Easing = easing };
                default:
                    return null;
            }
        }

        // What the control itself carries: its opacity, and the text colour of a control whose part is a Border (which has none).
        private static ITransition? ControlTransition (MotionSpec spec, string property, ThemeCssTransition source)
        {
            var duration = TimeSpan.FromMilliseconds (source.DurationMs);
            var delay = TimeSpan.FromMilliseconds (Math.Max (0, source.DelayMs));
            var easing = new ThemeEasing (source.Timing);

            return property switch {
                "opacity" => new DoubleTransition { Property = Visual.OpacityProperty, Duration = duration, Delay = delay, Easing = easing },
                "color" when !spec.Presenter => new BrushTransition { Property = global::Avalonia.Controls.Primitives.TemplatedControl.ForegroundProperty, Duration = duration, Delay = delay, Easing = easing },
                _ => null
            };
        }

        // ---- keyframe animations -----------------------------------------------------------------------------

        private static void AddAnimations (MotionSpec spec, ThemeCssState state, IReadOnlyList<ThemeCssAnimation> animations, Dictionary<string, ControlKeyframes> keyframes,
            Func<StyleSelector, StyleBuilder> styleFor)
        {
            foreach (var source in animations) {
                if (!keyframes.TryGetValue (source.Name, out var frames) || frames.Frames.Count == 0)
                    continue;

                foreach (var part in spec.Parts)
                    if (MakeAnimation (spec, source, frames, forPart: true) is { } made)
                        styleFor (part (state)).Animations.Add (made);

                foreach (var control in spec.Controls)
                    if (MakeAnimation (spec, source, frames, forPart: false) is { } made)
                        styleFor (S.InState (control, state)).Animations.Add (made);
            }
        }

        // One Avalonia animation for the properties of the keyframes that live on the part (or on the control).
        private static Animation? MakeAnimation (MotionSpec spec, ThemeCssAnimation source, ControlKeyframes frames, bool forPart)
        {
            var animation = new Animation {
                Duration = TimeSpan.FromMilliseconds (Math.Max (1, source.DurationMs)),
                Delay = TimeSpan.FromMilliseconds (Math.Max (0, source.DelayMs)),
                IterationCount = double.IsPositiveInfinity (source.IterationCount) ? IterationCount.Infinite : new IterationCount ((ulong) Math.Max (1, Math.Ceiling (source.IterationCount))),
                PlaybackDirection = source.Direction switch {
                    "reverse" => PlaybackDirection.Reverse,
                    "alternate" => PlaybackDirection.Alternate,
                    "alternate-reverse" => PlaybackDirection.AlternateReverse,
                    _ => PlaybackDirection.Normal
                },
                FillMode = source.FillMode switch {
                    "forwards" => FillMode.Forward,
                    "backwards" => FillMode.Backward,
                    "both" => FillMode.Both,
                    _ => FillMode.None
                },
                Easing = new ThemeEasing (source.Timing),
            };

            foreach (var frame in frames.Frames) {
                var keyframe = new KeyFrame { Cue = new Cue (frame.Offset) };

                foreach (var declaration in frame.Declarations) {
                    var value = declaration.Value;

                    foreach (var property in Covered (declaration.Property).Distinct ()) {
                        var target = forPart ? PartAnimated (spec, property, value) : ControlAnimated (spec, property, value);
                        if (target is { } t)
                            keyframe.Setters.Add (new Setter (t.Property, t.Value));
                    }
                }

                if (keyframe.Setters.Count == 0)
                    continue;

                if (frame.TimingFunction is { } timing && ThemeCssTiming.FromCss (timing.Css).CubicBezier is { Count: 4 } b)
                    keyframe.KeySpline = new KeySpline (b[0], b[1], b[2], b[3]);

                animation.Children.Add (keyframe);
            }

            return animation.Children.Count == 0 ? null : animation;
        }

        private static (AvaloniaProperty Property, object Value)? PartAnimated (MotionSpec spec, string property, ThemeCssValue value)
        {
            switch (property) {
                case "background-color" when value.Kind == ThemeCssValueKind.Color:
                    return (spec.Presenter ? ContentPresenter.BackgroundProperty : Border.BackgroundProperty, new SolidColorBrush (ToColor (value)));
                case "border-color" when value.Kind == ThemeCssValueKind.Color:
                    return (spec.Presenter ? ContentPresenter.BorderBrushProperty : Border.BorderBrushProperty, new SolidColorBrush (ToColor (value)));
                case "color" when spec.Presenter && value.Kind == ThemeCssValueKind.Color:
                    return (ContentPresenter.ForegroundProperty, new SolidColorBrush (ToColor (value)));
                case "box-shadow" when value.Kind is ThemeCssValueKind.BoxShadow or ThemeCssValueKind.List:
                    return (spec.Presenter ? ContentPresenter.BoxShadowProperty : Border.BoxShadowProperty, ToBoxShadows (value));
                case "border-radius" when value.Kind == ThemeCssValueKind.Length:
                    return (spec.Presenter ? ContentPresenter.CornerRadiusProperty : Border.CornerRadiusProperty, new CornerRadius (value.Pixels));
                default:
                    return null;
            }
        }

        private static (AvaloniaProperty Property, object Value)? ControlAnimated (MotionSpec spec, string property, ThemeCssValue value)
        {
            switch (property) {
                case "opacity" when value.Kind == ThemeCssValueKind.Number:
                    return (Visual.OpacityProperty, value.Number);
                case "color" when !spec.Presenter && value.Kind == ThemeCssValueKind.Color:
                    return (global::Avalonia.Controls.Primitives.TemplatedControl.ForegroundProperty, new SolidColorBrush (ToColor (value)));
                default:
                    return null;
            }
        }
    }
}
