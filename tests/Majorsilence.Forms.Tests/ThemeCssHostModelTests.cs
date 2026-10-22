using System.Linq;
using Xunit;

namespace Majorsilence.Forms.Tests
{
    // What a host applier (real WinForms, native Avalonia) reads from a parsed sheet instead of re-parsing the CSS:
    // the rule's state as one value, its transitions and animations as numbers, the @keyframes blocks and the
    // gradient layers (docs/theming-parity-plan.md, phase 0).
    public class ThemeCssHostModelTests
    {
        private static ThemeStyleSheet Parse (string css)
        {
            var sheet = ThemeStyleSheet.Parse (css);
            Assert.False (sheet.HasErrors, string.Join ("\n", sheet.Diagnostics));
            return sheet;
        }

        [Fact]
        public void RuleState_IsOneValue_ForEveryPseudoClass ()
        {
            var sheet = Parse ("Button { color: red; } Button:hover { color: red; } Button:active { color: red; } Button:disabled { color: red; } Button:focus { color: red; } Button:focus-visible { color: red; } Menu::item:hover { color: red; }");

            Assert.Equal (new[] {
                ThemeCssState.Normal, ThemeCssState.Hover, ThemeCssState.Active, ThemeCssState.Disabled, ThemeCssState.Focus, ThemeCssState.FocusVisible, ThemeCssState.Hover
            }, sheet.Rules.Select (r => r.State).ToArray ());
        }

        [Fact]
        public void SelectorSupports_MatchesTheStateFlags ()
        {
            var button = ThemeCssReference.FindSelector ("Button")!;
            var text = ThemeCssReference.FindSelector ("TextBox")!;
            var label = ThemeCssReference.FindSelector ("Label")!;

            Assert.True (button.Supports (ThemeCssState.Active));
            Assert.True (text.Supports (ThemeCssState.Focus));
            Assert.False (text.Supports (ThemeCssState.Hover));
            Assert.False (label.Supports (ThemeCssState.Disabled));
            Assert.True (label.Supports (ThemeCssState.Normal));
        }

        [Fact]
        public void Keyframes_AreExposedWithTheirDeclarations ()
        {
            var sheet = Parse (@"
                @keyframes pulse { from { opacity: 1; } 50% { opacity: .5; animation-timing-function: ease-in; } to { opacity: 1; } }
                @keyframes other { to { box-shadow: 0 0 0 4px red; } }");

            Assert.Equal (new[] { "pulse", "other" }, sheet.Keyframes.Select (k => k.Name).ToArray ());

            var frames = sheet.Keyframes[0].Frames;
            Assert.Equal (new[] { 0.0, 0.5, 1.0 }, frames.Select (f => f.Offset).ToArray ());
            Assert.Equal ("opacity", Assert.Single (frames[1].Declarations).Property);
            Assert.Equal (0.5, frames[1].Declarations[0].Value.Number);
            Assert.Equal ("ease-in", frames[1].TimingFunction!.Css);
            Assert.Null (frames[0].TimingFunction);
        }

        [Fact]
        public void Animations_LineUpTheirLists ()
        {
            var sheet = Parse (@"
                @keyframes pulse { to { opacity: .5; } }
                @keyframes spin { to { opacity: 1; } }
                Button { animation: pulse 1.2s ease-in-out infinite alternate, spin 300ms 50ms 2 both; }");

            var animations = sheet.Rules[0].Animations;
            Assert.Equal (2, animations.Count);

            Assert.Equal ("pulse", animations[0].Name);
            Assert.Equal (1200, animations[0].DurationMs);
            Assert.True (double.IsPositiveInfinity (animations[0].IterationCount));
            Assert.Equal ("alternate", animations[0].Direction);
            Assert.Equal (new[] { 0.42, 0.0, 0.58, 1.0 }, animations[0].Timing.CubicBezier.ToArray ());

            Assert.Equal ("spin", animations[1].Name);
            Assert.Equal (300, animations[1].DurationMs);
            Assert.Equal (50, animations[1].DelayMs);
            Assert.Equal (2, animations[1].IterationCount);
            Assert.Equal ("both", animations[1].FillMode);
        }

        [Fact]
        public void Transitions_AreOnePerProperty_WithTheirTiming ()
        {
            var sheet = Parse ("Button { transition: background-color 150ms ease-in-out, box-shadow .2s cubic-bezier(.22, 1, .36, 1) 10ms; }");

            var transitions = sheet.Rules[0].Transitions;
            Assert.Equal (new[] { "background-color", "box-shadow" }, transitions.Select (t => t.Property).ToArray ());
            Assert.Equal (150, transitions[0].DurationMs);
            Assert.Equal (200, transitions[1].DurationMs);
            Assert.Equal (10, transitions[1].DelayMs);
            Assert.Equal (new[] { 0.22, 1.0, 0.36, 1.0 }, transitions[1].Timing.CubicBezier.ToArray ());
        }

        [Fact]
        public void StepsTiming_ExposesCountAndPosition ()
        {
            var sheet = Parse ("Button { transition: opacity 1s steps(4, jump-start); }");

            var timing = sheet.Rules[0].Transitions[0].Timing;
            Assert.Empty (timing.CubicBezier);
            Assert.Equal (4, timing.Steps);
            Assert.Equal ("jump-start", timing.StepPosition);
        }

        [Fact]
        public void RuleWithoutMotion_HasNone ()
        {
            var rule = Parse ("Button { color: red; }").Rules[0];

            Assert.Empty (rule.Transitions);
            Assert.Empty (rule.Animations);
        }

        [Fact]
        public void Gradients_AreExposedStructurally ()
        {
            var sheet = Parse ("Button { background-image: linear-gradient(to bottom right, #ff0000, #0000ff 50%), radial-gradient(circle at 25% 75%, white, black); }");
            var gradients = sheet.Rules[0].Declarations.Single (d => d.Property == "background-image").Value.Gradients;

            Assert.Equal (2, gradients.Count);

            var linear = gradients[0];
            Assert.Equal (ControlGradientKind.Linear, linear.Kind);
            Assert.Equal ("bottom right", linear.Corner);
            Assert.Equal (2, linear.Stops.Count);
            Assert.Equal (0xFFFF0000u, linear.Stops[0].Argb);
            Assert.Equal ("auto", linear.Stops[0].Unit);
            Assert.Equal ("percent", linear.Stops[1].Unit);

            var radial = gradients[1];
            Assert.Equal (ControlGradientKind.Radial, radial.Kind);
            Assert.True (radial.Circle);
            Assert.Equal (0.25, radial.CenterX.Value);
            Assert.Equal (0.75, radial.CenterY.Value);
            Assert.False (radial.CenterX.IsLength);
        }

        [Fact]
        public void Gradients_AreEmpty_ForOtherValues ()
        {
            var value = Parse ("Button { background-color: red; }").Rules[0].Declarations[0].Value;

            Assert.Empty (value.Gradients);
        }

        [Theory]
        [InlineData ("ease")]
        [InlineData ("ease-in")]
        [InlineData ("ease-out")]
        [InlineData ("ease-in-out")]
        [InlineData ("linear")]
        [InlineData ("cubic-bezier(.22, 1, .36, 1)")]
        [InlineData ("cubic-bezier(.3, -0.5, .7, 1.6)")]
        [InlineData ("steps(4, jump-start)")]
        [InlineData ("steps(4)")]
        [InlineData ("steps(3, jump-both)")]
        [InlineData ("steps(5, jump-none)")]
        [InlineData ("step-start")]
        [InlineData ("step-end")]
        public void TimingEvaluate_AgreesWithTheCoresOwnEasing (string css)
        {
            var sheet = Parse ("Button { transition: opacity 1s " + css + "; }");
            var hostTiming = sheet.Rules[0].Transitions[0].Timing;

            // What a Majorsilence.Forms button animates with once the sheet is applied.
            Theme.ApplyStyleSheet (sheet);
            try {
                var core = Button.DefaultStyle.Transition!.TimingFunctions![0].Function;

                foreach (var p in new[] { 0.0, 0.1, 0.25, 0.4, 0.5, 0.6, 0.75, 0.9, 1.0 })
                    Assert.InRange (Math.Abs (core ((float) p) - hostTiming.Evaluate (p)), 0, 0.002);
            } finally {
                Theme.SetBuiltInTheme (BuiltInTheme.Light);
            }
        }

        [Fact]
        public void TransitionResolve_LetsAStateInheritWhatItLeavesUnsaid ()
        {
            var sheet = Parse ("Button { transition: background-color 150ms ease-in-out, box-shadow .2s; } Button:active { transition-duration: 60ms; }");

            var resting = ThemeCssTransition.Resolve (sheet.Rules[0].Declarations);
            var pressed = ThemeCssTransition.Resolve (sheet.Rules[0].Declarations.Concat (sheet.Rules[1].Declarations));

            Assert.Equal (new[] { 150.0, 200.0 }, resting.Select (t => t.DurationMs).ToArray ());
            Assert.Equal (new[] { "background-color", "box-shadow" }, pressed.Select (t => t.Property).ToArray ());
            Assert.Equal (new[] { 60.0, 60.0 }, pressed.Select (t => t.DurationMs).ToArray ());
            Assert.Equal ("ease-in-out", pressed[0].Timing.Css);
        }

        [Fact]
        public void TransitionResolve_None_IsEmpty ()
        {
            var sheet = Parse ("Button { transition: opacity 1s; } Button:hover { transition: none; }");

            Assert.Empty (ThemeCssTransition.Resolve (sheet.Rules[0].Declarations.Concat (sheet.Rules[1].Declarations)));
            Assert.Single (ThemeCssTransition.Resolve (sheet.Rules[0].Declarations));
        }
    }
}
