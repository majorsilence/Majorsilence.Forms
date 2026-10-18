using System;
using System.Linq;
using Majorsilence.Forms.Animation;
using Xunit;

namespace Majorsilence.Forms.Tests
{
    // The CSS transition properties: what parses, what the model and the ControlStyle carry, and that everything outside the subset is
    // an error that says what to write instead. The animation itself is ThemeCssTransitionPaintTests.
    [Collection ("Headless")]
    public class ThemeCssTransitionTests : IDisposable
    {
        public ThemeCssTransitionTests () => Theme.SetBuiltInTheme (BuiltInTheme.Light);

        public void Dispose ()
        {
            GC.SuppressFinalize (this);
            Theme.SetBuiltInTheme (BuiltInTheme.Light);
        }

        private static ThemeStyleSheet ParseClean (string css)
        {
            var sheet = ThemeStyleSheet.Parse (css);
            Assert.False (sheet.HasErrors, string.Join ("\n", sheet.Diagnostics));
            return sheet;
        }

        private static string ErrorsOf (string css)
            => string.Join ("\n", ThemeStyleSheet.Parse (css).Diagnostics.Where (d => d.Severity == ThemeCssSeverity.Error).Select (d => d.Message));

        private static ThemeCssDeclaration Declaration (ThemeStyleSheet sheet, string property)
            => sheet.Rules.SelectMany (r => r.Declarations).Single (d => d.Property == property);

        [Fact]
        public void Shorthand_expands_to_the_four_longhands_with_css_initial_values_for_what_it_leaves_out ()
        {
            var sheet = ParseClean ("Button { transition: background-color 150ms ease-in-out, border-color .2s, box-shadow 1s cubic-bezier(.22, 1, .36, 1) 50ms; }");

            var rule = Assert.Single (sheet.Rules);
            Assert.Equal (new[] { "transition-property", "transition-duration", "transition-timing-function", "transition-delay" },
                rule.Declarations.Select (d => d.Property));

            Assert.Equal ("background-color, border-color, box-shadow", Declaration (sheet, "transition-property").Value.ToString ());
            Assert.Equal ("150ms, 200ms, 1000ms", Declaration (sheet, "transition-duration").Value.ToString ());
            Assert.Equal ("ease-in-out, ease, cubic-bezier(0.22, 1, 0.36, 1)", Declaration (sheet, "transition-timing-function").Value.ToString ());
            Assert.Equal ("0ms, 0ms, 50ms", Declaration (sheet, "transition-delay").Value.ToString ());

            var durations = Declaration (sheet, "transition-duration").Value;
            Assert.Equal (ThemeCssValueKind.List, durations.Kind);
            Assert.Equal (ThemeCssValueKind.Time, durations.Items[0].Kind);
            Assert.Equal (150, durations.Items[0].Milliseconds);
        }

        [Fact]
        public void An_entry_without_a_property_is_all_and_none_is_an_empty_list ()
        {
            Assert.Equal ("all", Declaration (ParseClean ("Button { transition: 300ms; }"), "transition-property").Value.ToString ());
            Assert.Empty (Declaration (ParseClean ("Button { transition: none; }"), "transition-property").Value.Items);
            Assert.Empty (Declaration (ParseClean ("Button { transition-property: none; }"), "transition-property").Value.Items);
        }

        [Fact]
        public void Applying_a_sheet_puts_the_transition_on_the_type_style_and_a_state_rule_inherits_the_lists_it_leaves_out ()
        {
            Theme.LoadFromCss (@"
                Button { transition: background-color 150ms ease-out; }
                Button:active { transition-duration: 60ms; }
            ");

            var normal = Button.DefaultStyle.GetTransition ();
            Assert.NotNull (normal);
            Assert.True (normal!.TryGetTiming ("background-color", out var duration, out _, out var timing));
            Assert.Equal (TimeSpan.FromMilliseconds (150), duration);
            Assert.Same (ControlTimingFunction.EaseOut, timing);
            Assert.False (normal.TryGetTiming ("color", out _, out _, out _));

            // :active set only a duration; the property list and the easing come from the Button rule.
            var active = Button.DefaultStyleActive.GetTransition ();
            Assert.True (active!.TryGetTiming ("background-color", out duration, out _, out timing));
            Assert.Equal (TimeSpan.FromMilliseconds (60), duration);
            Assert.Same (ControlTimingFunction.EaseOut, timing);
        }

        [Fact]
        public void Reapplying_a_sheet_without_transitions_clears_them ()
        {
            Theme.LoadFromCss ("Button { transition: background-color 150ms; }");
            Assert.NotNull (Button.DefaultStyle.GetTransition ());

            Theme.LoadFromCss ("Button { background-color: #123456; }");
            Assert.Null (Button.DefaultStyle.GetTransition ());
        }

        [Fact]
        public void A_border_color_entry_covers_each_side_and_border_radius_each_corner ()
        {
            var transition = new ControlTransition (new[] { "border-color", "border-radius" }, new[] { TimeSpan.FromMilliseconds (100) });

            Assert.True (transition.TryGetTiming ("border-left-color", out _, out _, out _));
            Assert.True (transition.TryGetTiming ("border-bottom-right-radius", out _, out _, out _));
            Assert.False (transition.TryGetTiming ("background-color", out _, out _, out _));
        }

        [Fact]
        public void Lists_line_up_by_index_and_a_shorter_list_repeats ()
        {
            var transition = new ControlTransition (
                new[] { "background-color", "color", "box-shadow" },
                new[] { TimeSpan.FromMilliseconds (100), TimeSpan.FromMilliseconds (200) });

            transition.TryGetTiming ("box-shadow", out var duration, out _, out _);
            Assert.Equal (TimeSpan.FromMilliseconds (100), duration);
            transition.TryGetTiming ("color", out duration, out _, out _);
            Assert.Equal (TimeSpan.FromMilliseconds (200), duration);
        }

        [Fact]
        public void A_variable_can_hold_a_time ()
        {
            var sheet = ParseClean (":root { --fast: 90ms; } Button { transition: color var(--fast); }");
            Assert.Equal ("90ms", Declaration (sheet, "transition-duration").Value.ToString ());
        }

        [Theory]
        [InlineData ("Button { transition: border-width 1s; }", "client area")]
        [InlineData ("Button { transition-property: font-size; }", "no in-between values")]
        [InlineData ("Button { transition: colr 1s; }", "Did you mean 'color'")]
        [InlineData ("Button { transition: color 150; }", "has no unit")]
        [InlineData ("Button { transition-duration: -1s; }", "negative")]
        [InlineData ("Button { transition: color 1s 2s 3s; }", "third time")]
        [InlineData ("Button { transition: color 1s ease linear; }", "second timing function")]
        [InlineData ("Button { transition: color background-color 1s; }", "separate transitions with commas")]
        [InlineData ("Button { transition: color 1s, none; }", "'none' cannot be one entry")]
        [InlineData ("Button { transition: color 1s allow-discrete; }", "transition-behavior")]
        [InlineData ("Button { transition-behavior: allow-discrete; }", "transition-behavior")]
        [InlineData ("Button { transition-timing-function: cubic-bezier(2, 0, 1, 1); }", "between 0 and 1")]
        [InlineData ("Button { transition-timing-function: cubic-bezier(1, 1); }", "exactly four")]
        [InlineData ("Button { transition-timing-function: steps(0); }", "at least 1")]
        [InlineData ("Button { transition-timing-function: steps(1, jump-none); }", "at least 2")]
        [InlineData ("Button { transition-timing-function: linear(0, 1); }", "linear() stop-list")]
        [InlineData ("Button { transition-timing-function: bouncy; }", "not a timing function")]
        [InlineData ("Label { transition: color 1s; }", "Transitions run on: Button")]
        [InlineData ("ScrollBar::thumb { transition: background-color 1s; }", "does not apply to ScrollBar::thumb")]
        public void Everything_outside_the_subset_is_an_error_that_says_why (string css, string expected)
            => Assert.Contains (expected, ErrorsOf (css));

        [Fact]
        public void A_rejected_transition_leaves_the_rest_of_the_rule_working ()
        {
            var sheet = ThemeStyleSheet.Parse ("Label { color: #102030; transition: color 1s; }");
            Assert.Single (sheet.Diagnostics, d => d.Severity == ThemeCssSeverity.Error);
            Theme.ApplyStyleSheet (sheet);
            Assert.Equal (new SkiaSharp.SKColor (0x10, 0x20, 0x30), Label.DefaultStyle.ForegroundColor);
            Assert.Null (Label.DefaultStyle.GetTransition ());
        }

        [Fact]
        public void Every_transition_property_is_in_the_reference ()
        {
            foreach (var name in new[] { "transition", "transition-property", "transition-duration", "transition-timing-function", "transition-delay" }) {
                Assert.Contains (name, ThemeCssReference.PropertyNames);
                Assert.Contains (ThemeCssReference.Properties, p => p.Name == name);
            }

            Assert.DoesNotContain ("transition", ThemeCssReference.LonghandNames);
            Assert.Contains ("transition-delay", ThemeCssReference.LonghandNames);
        }

        // ---- the CSS timing functions -----------------------------------------------------------

        [Theory]
        // Reference values of the CSS keyword curves (the cubic-bezier definitions in CSS Easing Functions Level 1), to 3 places.
        [InlineData ("ease", 0.5f, 0.8024f)]
        [InlineData ("ease-in", 0.5f, 0.3153f)]
        [InlineData ("ease-out", 0.5f, 0.6847f)]
        [InlineData ("ease-in-out", 0.5f, 0.5f)]
        [InlineData ("ease", 0.25f, 0.4085f)]
        public void Keyword_curves_match_their_cubic_bezier_definitions (string keyword, float t, float expected)
        {
            EasingFunction function = keyword switch {
                "ease" => Easing.Ease,
                "ease-in" => Easing.EaseIn,
                "ease-out" => Easing.EaseOut,
                _ => Easing.EaseInOut
            };

            Assert.InRange (function (t), expected - 0.002f, expected + 0.002f);
            Assert.Equal (0f, function (0f));
            Assert.Equal (1f, function (1f));
        }

        [Fact]
        public void A_cubic_bezier_may_overshoot_in_y ()
        {
            var back = Easing.CubicBezier (0.3f, 1.6f, 0.6f, 1f);
            Assert.True (back (0.5f) > 1f, $"expected an overshoot, got {back (0.5f)}");
        }

        [Fact]
        public void Cubic_bezier_rejects_an_x_outside_0_to_1 ()
            => Assert.Throws<ArgumentOutOfRangeException> (() => Easing.CubicBezier (1.5f, 0f, 0.5f, 1f));

        [Theory]
        [InlineData (StepPosition.JumpEnd, 0.1f, 0f)]
        [InlineData (StepPosition.JumpEnd, 0.3f, 0.25f)]
        [InlineData (StepPosition.JumpEnd, 0.99f, 0.75f)]
        [InlineData (StepPosition.JumpStart, 0.1f, 0.25f)]
        [InlineData (StepPosition.JumpNone, 0.1f, 0f)]
        [InlineData (StepPosition.JumpNone, 0.8f, 1f)]
        [InlineData (StepPosition.JumpBoth, 0.1f, 0.2f)]
        public void Steps_jump_where_css_says (StepPosition position, float t, float expected)
            => Assert.Equal (expected, Easing.Steps (4, position) (t), 4);
    }
}
