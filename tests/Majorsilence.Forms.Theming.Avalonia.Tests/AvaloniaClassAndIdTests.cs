using System;
using System.Linq;
using System.Threading.Tasks;
using Xunit;
using MF = Majorsilence.Forms;

namespace Majorsilence.Forms.Theming.Avalonia.Tests
{
    // Inside the namespace, so Avalonia types win over the Majorsilence.Forms ones of the same name.
    using global::Avalonia;
    using global::Avalonia.Animation;
    using global::Avalonia.Controls;
    using global::Avalonia.Controls.Presenters;
    using global::Avalonia.Media;

    // Classes and ids in the theme CSS (docs/theming-selectors-plan.md) on native Avalonia controls: `.primary` is a style class
    // (Control.Classes), `#save` the control's Name. Each is proved on real controls -- the colour the template part ends up with --
    // not by the sheet parsing, and the type-level resources must not move for a control that has no class.
    public class AvaloniaClassAndIdTests : IAsyncDisposable
    {
        public AvaloniaClassAndIdTests () => AvaloniaCssTheme.PrefersReducedMotion = false;

        public ValueTask DisposeAsync ()
        {
            GC.SuppressFinalize (this);
            return new ValueTask (Headless.Run (() => {
                AvaloniaCssTheme.PrefersReducedMotion = null;
                AvaloniaCssTheme.DeriveFromTokens = true;
                MF.Theme.SetBuiltInTheme (MF.BuiltInTheme.Light);
                AvaloniaCssTheme.Apply (MF.ThemeStyleSheet.Parse ("Button { }"));
                MF.Theme.SetBuiltInTheme (MF.BuiltInTheme.Light);
            }));
        }

        private static T Show<T> (T control) where T : Control
        {
            var window = new Window { Content = control };
            window.Show ();
            return control;
        }

        private static ContentPresenter PartOf (Button button)
            => global::Avalonia.VisualTree.VisualExtensions.GetVisualDescendants (button).OfType<ContentPresenter> ().First (p => p.Name == "PART_ContentPresenter");

        private static Border BorderOf (Control control, string name)
            => global::Avalonia.VisualTree.VisualExtensions.GetVisualDescendants (control).OfType<Border> ().First (b => b.Name == name);

        private static Color BrushColor (IBrush? brush) => Assert.IsAssignableFrom<ISolidColorBrush> (brush).Color;

        private static readonly Color Red = Color.Parse ("#ff0000");
        private static readonly Color Blue = Color.Parse ("#0000ff");
        private static readonly Color Green = Color.Parse ("#00ff00");

        private static void Hover (Control control) => ((IPseudoClasses) control.Classes).Set (":pointerover", true);

        private static void Unhover (Control control) => ((IPseudoClasses) control.Classes).Set (":pointerover", false);

        [Fact]
        public Task AClassRule_RestylesOnlyTheControlsThatHaveTheClass () => Headless.Run (() => {
            AvaloniaCssTheme.Apply ("Button.primary { background-color: #ff0000; color: #00ff00; }");

            var primary = Show (new Button { Content = "A" });
            primary.Classes.Add ("primary");
            var plain = Show (new Button { Content = "B" });

            Assert.Equal (Red, BrushColor (PartOf (primary).Background));
            Assert.Equal (Green, BrushColor (PartOf (primary).Foreground));
            Assert.NotEqual (Red, BrushColor (PartOf (plain).Background));

            // The application's Fluent resource is untouched: the class is scoped to its controls.
            Assert.NotEqual (Red, BrushColor ((IBrush?) Resource ("ButtonBackground")));
        });

        private static object? Resource (string key)
            => Application.Current!.TryGetResource (key, global::Avalonia.Styling.ThemeVariant.Light, out var value) ? value : null;

        [Fact]
        public Task AClassRuleWithNoType_ReachesEveryControlTypeThatHasTheClass () => Headless.Run (() => {
            AvaloniaCssTheme.Apply (".card { background-color: #ff0000; }");

            var button = Show (new Button { Content = "A" });
            button.Classes.Add ("card");
            var list = Show (new ListBox ());
            list.Classes.Add ("card");
            var text = Show (new TextBox ());
            text.Classes.Add ("card");
            var other = Show (new ListBox ());

            Assert.Equal (Red, BrushColor (PartOf (button).Background));
            Assert.Equal (Red, BrushColor (list.Background));
            Assert.Equal (Red, BrushColor (BorderOf (text, "PART_BorderElement").Background));
            Assert.NotEqual (Red, BrushColor (other.Background));
        });

        [Fact]
        public Task AnIdRule_RestylesTheControlWithThatName () => Headless.Run (() => {
            AvaloniaCssTheme.Apply ("#save { background-color: #ff0000; } Button#cancel { background-color: #0000ff; } Label#cancel { background-color: #00ff00; }");

            var save = Show (new Button { Content = "A", Name = "save" });
            var cancel = Show (new Button { Content = "B", Name = "cancel" });
            var other = Show (new Button { Content = "C", Name = "other" });

            Assert.Equal (Red, BrushColor (PartOf (save).Background));
            Assert.Equal (Blue, BrushColor (PartOf (cancel).Background));
            Assert.NotEqual (Red, BrushColor (PartOf (other).Background));
            Assert.NotEqual (Blue, BrushColor (PartOf (other).Background));
        });

        [Fact]
        public Task ToggingAClassAtRunTime_RestylesTheControl () => Headless.Run (() => {
            AvaloniaCssTheme.Apply (".on { background-color: #ff0000; }");

            var button = Show (new Button { Content = "A" });
            var before = BrushColor (PartOf (button).Background);

            button.Classes.Add ("on");
            Assert.Equal (Red, BrushColor (PartOf (button).Background));

            button.Classes.Remove ("on");
            Assert.Equal (before, BrushColor (PartOf (button).Background));
        });

        [Theory]
        // equal specificity: the later rule wins
        [InlineData (".a { background-color: #ff0000; } .b { background-color: #0000ff; }", "#0000ff")]
        [InlineData (".b { background-color: #0000ff; } .a { background-color: #ff0000; }", "#ff0000")]
        // an id beats a class, a type with a class beats a class, two classes beat one -- in either order
        [InlineData ("#x { background-color: #ff0000; } .a { background-color: #0000ff; }", "#ff0000")]
        [InlineData (".a { background-color: #0000ff; } #x { background-color: #ff0000; }", "#ff0000")]
        [InlineData ("ListBox.a { background-color: #ff0000; } .a { background-color: #0000ff; }", "#ff0000")]
        [InlineData (".a { background-color: #0000ff; } ListBox.a { background-color: #ff0000; }", "#ff0000")]
        [InlineData (".a.b { background-color: #ff0000; } .a { background-color: #0000ff; }", "#ff0000")]
        [InlineData (".a { background-color: #0000ff; } .a.b { background-color: #ff0000; }", "#ff0000")]
        // a rule for the type alone never beats a class
        [InlineData ("ListBox { background-color: #0000ff; } .a { background-color: #ff0000; }", "#ff0000")]
        [InlineData (".a { background-color: #ff0000; } ListBox { background-color: #0000ff; }", "#ff0000")]
        public Task Specificity_DecidesWhichRuleStylesTheControl_WhateverTheOrder (string css, string expected) => Headless.Run (() => {
            AvaloniaCssTheme.Apply (css);

            var list = Show (new ListBox { Name = "x" });
            list.Classes.Add ("a");
            list.Classes.Add ("b");

            Assert.Equal (Color.Parse (expected), BrushColor (list.Background));
        });

        [Theory]
        // Fluent colours go through resources, scoped to the control; specificity decides there too
        [InlineData ("Button.a { background-color: #ff0000; } .a { background-color: #0000ff; }", "#ff0000")]
        [InlineData (".a { background-color: #0000ff; } Button.a { background-color: #ff0000; }", "#ff0000")]
        [InlineData ("#x { background-color: #ff0000; } .a { background-color: #0000ff; }", "#ff0000")]
        [InlineData (".a { background-color: #0000ff; } #x { background-color: #ff0000; }", "#ff0000")]
        [InlineData (".a { background-color: #ff0000; } .b { background-color: #0000ff; }", "#0000ff")]
        [InlineData (".b { background-color: #0000ff; } .a { background-color: #ff0000; }", "#ff0000")]
        public Task Specificity_AlsoDecidesTheScopedFluentResources (string css, string expected) => Headless.Run (() => {
            AvaloniaCssTheme.Apply (css);

            var button = Show (new Button { Content = "A", Name = "x" });
            button.Classes.Add ("a");
            button.Classes.Add ("b");

            Assert.Equal (Color.Parse (expected), BrushColor (PartOf (button).Background));
        });

        [Fact]
        public Task AClassRule_HoldsWhileHovered_OverTheTypesBuiltInHoverLook () => Headless.Run (() => {
            AvaloniaCssTheme.Apply (":root { --accent-color: #f2a93b; } Button.primary { background-color: #ff0000; }");

            var primary = Show (new Button { Content = "A" });
            primary.Classes.Add ("primary");
            var plain = Show (new Button { Content = "B" });

            Hover (primary);
            Hover (plain);

            Assert.Equal (Red, BrushColor (PartOf (primary).Background));
            Assert.Equal (Color.Parse ("#f2a93b"), BrushColor (PartOf (plain).Background));
        });

        [Fact]
        public Task AStateRuleOnAClass_AppliesOnlyInThatState () => Headless.Run (() => {
            AvaloniaCssTheme.Apply ("Button.primary { background-color: #ff0000; } Button.primary:hover { background-color: #0000ff; }");

            var button = Show (new Button { Content = "A" });
            button.Classes.Add ("primary");
            Assert.Equal (Red, BrushColor (PartOf (button).Background));

            Hover (button);
            Assert.Equal (Blue, BrushColor (PartOf (button).Background));

            Unhover (button);
            Assert.Equal (Red, BrushColor (PartOf (button).Background));
        });

        [Fact]
        public Task ATemplatePartRule_CarriesTheClassConstraintBeforeTheTemplate () => Headless.Run (() => {
            // Box shadows live on a template part: the generated selector must be Button.primary /template/ ..., not Button /template/ ....Class.
            AvaloniaCssTheme.Apply ("Button.primary { box-shadow: 0 4px 8px #ff0000; } Button.primary:hover { box-shadow: 0 8px 8px #0000ff; }");

            var primary = Show (new Button { Content = "A" });
            primary.Classes.Add ("primary");
            var plain = Show (new Button { Content = "B" });

            Assert.Equal (1, PartOf (primary).BoxShadow.Count);
            Assert.Equal (0, PartOf (plain).BoxShadow.Count);

            Hover (primary);
            Assert.Equal (Blue, PartOf (primary).BoxShadow[0].Color);
        });

        [Fact]
        public Task ATransitionOnAClass_RunsOnlyOnItsControls () => Headless.Run (() => {
            AvaloniaCssTheme.Apply ("Button.primary { transition: background-color 120ms linear; } .fade { transition: all 90ms; }");

            var primary = Show (new Button { Content = "A" });
            primary.Classes.Add ("primary");
            var plain = Show (new Button { Content = "B" });
            var fade = Show (new TextBox ());
            fade.Classes.Add ("fade");

            var transition = Assert.Single (PartOf (primary).Transitions!.OfType<BrushTransition> ());
            Assert.Equal (TimeSpan.FromMilliseconds (120), transition.Duration);
            Assert.True (PartOf (plain).Transitions is null || PartOf (plain).Transitions!.Count == 0);
            Assert.Contains (BorderOf (fade, "PART_BorderElement").Transitions!.OfType<BrushTransition> (), t => t.Property == Border.BackgroundProperty);
        });

        [Fact]
        public Task AnAnimationOnAClass_IsAStyleThatMatchesOnlyControlsWithTheClass () => Headless.Run (() => {
            AvaloniaCssTheme.Apply ("@keyframes pulse { from { background-color: #ff0000; } to { background-color: #0000ff; } } Button.attention { animation: pulse 1s infinite; }");

            var animated = Application.Current!.Styles.OfType<global::Avalonia.Styling.Styles> ().SelectMany (s => s).OfType<global::Avalonia.Styling.Style> ()
                .Where (s => s.Animations.Count > 0).ToList ();

            // The selector carries the class before /template/, so the animation plays on that button's template part only.
            Assert.NotEmpty (animated);
            Assert.All (animated, style => {
                var text = style.Selector!.ToString ()!;
                Assert.Contains (".attention", text, StringComparison.Ordinal);
                Assert.True (text.IndexOf (".attention", StringComparison.Ordinal) < text.IndexOf ("/template/", StringComparison.Ordinal), text);
            });
        });

        [Fact]
        public Task ClassAndIdRules_DoNotChangeTheTypesOwnResourcesOrStyles () => Headless.Run (() => {
            AvaloniaCssTheme.Apply ("Button { background-color: #0000ff; } .a { background-color: #ff0000; } #x { background-color: #ff0000; } Button.a:hover { color: #ff0000; }");

            Assert.Equal (Blue, BrushColor ((IBrush?) Resource ("ButtonBackground")));
            var plain = Show (new Button { Content = "A" });
            Assert.Equal (Blue, BrushColor (PartOf (plain).Background));
        });

        [Fact]
        public Task ReApplyingASheet_StylesNewControlsWithTheNewClassRules_AndNotWithTheOldOnes () => Headless.Run (() => {
            AvaloniaCssTheme.Apply (".a { background-color: #ff0000; }");
            var first = Show (new ListBox ());
            first.Classes.Add ("a");
            Assert.Equal (Red, BrushColor (first.Background));

            AvaloniaCssTheme.Apply (".a { background-color: #0000ff; }");
            var second = Show (new ListBox ());
            second.Classes.Add ("a");
            Assert.Equal (Blue, BrushColor (second.Background));

            AvaloniaCssTheme.Apply ("Button { }");
            var third = Show (new ListBox ());
            third.Classes.Add ("a");
            Assert.NotEqual (Blue, BrushColor (third.Background));
            Assert.NotEqual (Red, BrushColor (third.Background));
        });

        [Fact]
        public Task ADeclarationNoControlHasASeamFor_IsAWarning_AndAnApproximateOneIsInfo () => Headless.Run (() => {
            AvaloniaCssTheme.Apply (".a { border-style: dashed; } Button.b { border-top-color: red; }");

            var warning = Assert.Single (AvaloniaCssTheme.Diagnostics, d => d.Severity == MF.ThemeCssSeverity.Warning);
            Assert.Contains (".a { border-style }", warning.Message, StringComparison.Ordinal);

            Assert.Contains (AvaloniaCssTheme.Diagnostics, d => d.Severity == MF.ThemeCssSeverity.Info && d.Message.Contains ("Button.b { border-top-color }", StringComparison.Ordinal));
        });

        [Fact]
        public Task AValidClassRule_ProducesNoDiagnostics () => Headless.Run (() => {
            AvaloniaCssTheme.Apply (".a { background-color: #ff0000; } #b { color: #00ff00; } Button.c:hover { background-color: #0000ff; }");

            Assert.Empty (AvaloniaCssTheme.Diagnostics);
        });

        [Fact]
        public void TheSelectorFormsHaveRows_AllNative ()
        {
            Assert.Equal (MF.ThemeCssReference.ClassAndIdForms.Select (f => f.Selector), AvaloniaThemeSupport.SelectorForms.Select (f => f.Selector));
            Assert.All (AvaloniaThemeSupport.SelectorForms, f => Assert.Equal (AvaloniaThemeSupportLevel.Native, f.Level));
            Assert.Contains ("### Selectors with classes and ids", AvaloniaThemeSupport.ToMarkdown (), StringComparison.Ordinal);
        }

        [Fact]
        public void EveryMappedSelectorWithAResourceRow_KnowsItsControlTypes ()
        {
            // A class or id rule scopes a Fluent resource to the control that reads it; without the control type the row would write nothing.
            foreach (var mapping in AvaloniaThemeSupport.Mappings.Where (m => m.AvaloniaTypes is not null)) {
                var hasResource = AvaloniaThemeSupport.Entries.Any (e => e.Selector == mapping.Selector && e.Level != AvaloniaThemeSupportLevel.Unsupported && e.ResourceKeys.Count > 0);

                if (hasResource)
                    Assert.True (AvaloniaThemeSupport.ControlSelectorsOf (mapping.Selector).Count > 0, $"{mapping.Selector} writes resources but has no control selector for a scoped rule");
            }
        }
    }
}
