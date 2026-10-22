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
    using global::Avalonia.Media;
    using global::Avalonia.Styling;

    // Parity with the Majorsilence.Forms engine (docs/theming-parity-plan.md): each test applies a sheet to a headless
    // Avalonia app and checks a relationship on real controls or on the Fluent resources they read.
    public class AvaloniaParityTests : IAsyncDisposable
    {
        private const string TokensOnly = @"
            :root {
                --accent-color: #f2a93b;
                --accent-color-2: #c77f12;
                --background-color: #1c1c1e;
                --control-low-color: #232326;
                --control-mid-color: #2c2c30;
                --control-highlight-low-color: #34343a;
                --border-low-color: #3a3a40;
                --foreground-color: #e8e8ea;
                --foreground-color-on-accent: #1a1200;
            }";

        public ValueTask DisposeAsync ()
        {
            GC.SuppressFinalize (this);
            return new ValueTask (Headless.Run (() => {
                AvaloniaCssTheme.DeriveFromTokens = true;
                MF.Theme.SetBuiltInTheme (MF.BuiltInTheme.Light);
                AvaloniaCssTheme.Apply (MF.ThemeStyleSheet.Parse ("Button { }"));
                MF.Theme.SetBuiltInTheme (MF.BuiltInTheme.Light);
            }));
        }

        private static object? Resource (string key)
            => Application.Current!.TryGetResource (key, ThemeVariant.Dark, out var value) ? value : null;

        private static Color BrushColor (object? brush) => Assert.IsAssignableFrom<ISolidColorBrush> (brush).Color;

        private static Color BrushColor (IBrush? brush) => Assert.IsAssignableFrom<ISolidColorBrush> (brush).Color;

        private static T Show<T> (T control) where T : Control
        {
            var window = new Window { Content = control };
            window.Show ();
            return control;
        }

        // ---- phase 1: tokens ---------------------------------------------------------------------------------

        [Fact]
        public Task TokensOnlySheet_RestylesControlsNoRuleMentions () => Headless.Run (() => {
            AvaloniaCssTheme.Apply (TokensOnly);

            // The Fluent resources the buttons and inputs read.
            Assert.Equal (Color.Parse ("#2c2c30"), BrushColor (Resource ("ButtonBackground")));
            Assert.Equal (Color.Parse ("#3a3a40"), BrushColor (Resource ("ButtonBorderBrush")));
            Assert.Equal (Color.Parse ("#e8e8ea"), BrushColor (Resource ("ButtonForeground")));
            Assert.Equal (Color.Parse ("#f2a93b"), BrushColor (Resource ("ButtonBackgroundPointerOver")));
            Assert.Equal (Color.Parse ("#1a1200"), BrushColor (Resource ("ButtonForegroundPointerOver")));
            Assert.Equal (Color.Parse ("#232326"), BrushColor (Resource ("TextControlBackground")));
            Assert.Equal (Color.Parse ("#2c2c30"), BrushColor (Resource ("ComboBoxBackground")));

            // The controls whose colours are style setters.
            var list = Show (new ListBox ());
            Assert.Equal (Color.Parse ("#232326"), BrushColor (list.Background));
            Assert.Equal (Color.Parse ("#e8e8ea"), BrushColor (list.Foreground));
            Assert.Equal (Color.Parse ("#3a3a40"), BrushColor (list.BorderBrush));
        });

        [Fact]
        public Task ExplicitRule_WinsOverTheDerivedDefault_ButTheRestStillDerives () => Headless.Run (() => {
            AvaloniaCssTheme.Apply (TokensOnly + "Button { background-color: #112233; }");

            Assert.Equal (Color.Parse ("#112233"), BrushColor (Resource ("ButtonBackground")));
            Assert.Equal (Color.Parse ("#3a3a40"), BrushColor (Resource ("ButtonBorderBrush")));
        });

        [Fact]
        public Task DeriveFromTokensOff_KeepsFluentsOwnColours () => Headless.Run (() => {
            AvaloniaCssTheme.DeriveFromTokens = false;
            AvaloniaCssTheme.Apply (TokensOnly);

            Assert.NotEqual (Color.Parse ("#2c2c30"), BrushColor (Resource ("ButtonBackground")));
            Assert.NotEqual (Color.Parse ("#3a3a40"), BrushColor (Resource ("ButtonBorderBrush")));
            // The accent and the window colours are tokens Fluent reads directly, so they still apply.
            Assert.Equal (Color.Parse ("#f2a93b"), Resource ("SystemAccentColor"));
        });

        [Fact]
        public Task DerivedDefaults_AreNotReportedAsDiagnostics () => Headless.Run (() => {
            AvaloniaCssTheme.Apply (TokensOnly);

            Assert.Empty (AvaloniaCssTheme.Diagnostics);
        });

        // ---- phase 1: selectors Fluent can express ---------------------------------------------------------------

        [Fact]
        public Task ProgressBarRule_StylesTheTrackAndFill () => Headless.Run (() => {
            AvaloniaCssTheme.Apply (TokensOnly + "ProgressBar { background-color: #112233; color: #445566; border-color: #778899; border-radius: 3px; }");

            var bar = Show (new ProgressBar { Minimum = 0, Maximum = 100, Value = 50 });

            Assert.Equal (Color.Parse ("#112233"), BrushColor (bar.Background));
            Assert.Equal (Color.Parse ("#445566"), BrushColor (bar.Foreground));
            Assert.Equal (Color.Parse ("#778899"), BrushColor (bar.BorderBrush));
            Assert.Equal (new CornerRadius (3), bar.CornerRadius);
            Assert.DoesNotContain (AvaloniaCssTheme.Diagnostics, d => d.Message.Contains ("'ProgressBar' has no", StringComparison.Ordinal));
        });

        [Fact]
        public Task ProgressBar_WithoutARule_FillsWithAccent2 () => Headless.Run (() => {
            AvaloniaCssTheme.Apply (TokensOnly);

            var bar = Show (new ProgressBar { Minimum = 0, Maximum = 100, Value = 50 });

            Assert.Equal (Color.Parse ("#c77f12"), BrushColor (bar.Foreground));
        });

        [Fact]
        public Task DateTimePickerRule_WritesThePickerButtonResources () => Headless.Run (() => {
            AvaloniaCssTheme.Apply ("DateTimePicker { background-color: #112233; color: #445566; border-color: #778899; }");

            Assert.Equal (Color.Parse ("#112233"), BrushColor (Resource ("DatePickerButtonBackground")));
            Assert.Equal (Color.Parse ("#112233"), BrushColor (Resource ("TimePickerButtonBackground")));
            Assert.Equal (Color.Parse ("#445566"), BrushColor (Resource ("DatePickerButtonForeground")));
            Assert.Equal (Color.Parse ("#778899"), BrushColor (Resource ("DatePickerButtonBorderBrush")));
        });

        [Fact]
        public Task PopupWindowRule_WritesTheFlyoutAndToolTipResources () => Headless.Run (() => {
            AvaloniaCssTheme.Apply ("PopupWindow { background-color: #112233; color: #445566; border-color: #778899; }");

            Assert.Equal (Color.Parse ("#112233"), BrushColor (Resource ("FlyoutPresenterBackground")));
            Assert.Equal (Color.Parse ("#112233"), BrushColor (Resource ("ToolTipBackground")));
            Assert.Equal (Color.Parse ("#778899"), BrushColor (Resource ("ToolTipBorderBrush")));
            Assert.Equal (Color.Parse ("#445566"), BrushColor (Resource ("ToolTipForeground")));
        });

        // ---- phase 2: states ---------------------------------------------------------------------------------

        [Fact]
        public Task ButtonActive_WritesThePressedColoursExactly_WhateverTheRuleOrder () => Headless.Run (() => {
            // The :hover rule writes a darker pressed fill of its own; the explicit :active rule must win even when written first.
            AvaloniaCssTheme.Apply ("Button:active { background-color: #ff0000; color: #00ff00; border-color: #0000ff; } Button:hover { background-color: #123456; }");

            Assert.Equal (Color.Parse ("#ff0000"), BrushColor (Resource ("ButtonBackgroundPressed")));
            Assert.Equal (Color.Parse ("#ff0000"), BrushColor (Resource ("ToggleButtonBackgroundPressed")));
            Assert.Equal (Color.Parse ("#00ff00"), BrushColor (Resource ("ButtonForegroundPressed")));
            Assert.Equal (Color.Parse ("#0000ff"), BrushColor (Resource ("ButtonBorderBrushPressed")));
            Assert.Equal (Color.Parse ("#123456"), BrushColor (Resource ("ButtonBackgroundPointerOver")));
            Assert.DoesNotContain (AvaloniaCssTheme.Diagnostics, d => d.Message.Contains ("has no Avalonia counterpart", StringComparison.Ordinal));
        });

        [Fact]
        public Task ButtonDisabled_WritesTheDisabledResources () => Headless.Run (() => {
            AvaloniaCssTheme.Apply ("Button:disabled { background-color: #111111; color: #222222; border-color: #333333; }");

            Assert.Equal (Color.Parse ("#111111"), BrushColor (Resource ("ButtonBackgroundDisabled")));
            Assert.Equal (Color.Parse ("#222222"), BrushColor (Resource ("ButtonForegroundDisabled")));
            Assert.Equal (Color.Parse ("#333333"), BrushColor (Resource ("ButtonBorderBrushDisabled")));
            Assert.Equal (Color.Parse ("#222222"), BrushColor (Resource ("ToggleButtonForegroundDisabled")));
        });

        [Fact]
        public Task DisabledText_FollowsTheForegroundDisabledToken_UntilARuleSaysOtherwise () => Headless.Run (() => {
            AvaloniaCssTheme.Apply (TokensOnly + ":root { --foreground-disabled-color: #777777; }");
            Assert.Equal (Color.Parse ("#777777"), BrushColor (Resource ("ButtonForegroundDisabled")));
            Assert.Equal (Color.Parse ("#777777"), BrushColor (Resource ("TextControlForegroundDisabled")));
            Assert.Equal (Color.Parse ("#777777"), BrushColor (Resource ("CheckBoxForegroundUncheckedDisabled")));

            AvaloniaCssTheme.Apply (TokensOnly + ":root { --foreground-disabled-color: #777777; } Button:disabled { color: #abcdef; }");
            Assert.Equal (Color.Parse ("#abcdef"), BrushColor (Resource ("ButtonForegroundDisabled")));
            Assert.Equal (Color.Parse ("#777777"), BrushColor (Resource ("TextControlForegroundDisabled")));
        });

        [Fact]
        public Task ButtonFocus_StylesTheTemplatePart_OnlyWhileFocused () => Headless.Run (() => {
            AvaloniaCssTheme.Apply ("Button:focus { background-color: #ff0000; border-color: #00ff00; }");

            var button = new Button { Content = "OK" };
            var other = new Button { Content = "Other" };
            var window = new Window { Content = new StackPanel { Children = { button, other } } };
            window.Show ();

            global::Avalonia.Controls.Presenters.ContentPresenter Presenter (Button b)
                => global::Avalonia.VisualTree.VisualExtensions.GetVisualDescendants (b).OfType<global::Avalonia.Controls.Presenters.ContentPresenter> ().First (p => p.Name == "PART_ContentPresenter");

            button.Focus ();

            Assert.Equal (Color.Parse ("#ff0000"), BrushColor (Presenter (button).Background));
            Assert.Equal (Color.Parse ("#00ff00"), BrushColor (Presenter (button).BorderBrush));
            Assert.NotEqual (Color.Parse ("#ff0000"), (Presenter (other).Background as ISolidColorBrush)?.Color);
        });

        [Fact]
        public Task ButtonFocusVisible_NeedsKeyboardFocus () => Headless.Run (() => {
            AvaloniaCssTheme.Apply ("Button:focus-visible { background-color: #ff0000; }");

            var keyboard = new Button { Content = "A" };
            var pointer = new Button { Content = "B" };
            var window = new Window { Content = new StackPanel { Children = { keyboard, pointer } } };
            window.Show ();

            global::Avalonia.Controls.Presenters.ContentPresenter Presenter (Button b)
                => global::Avalonia.VisualTree.VisualExtensions.GetVisualDescendants (b).OfType<global::Avalonia.Controls.Presenters.ContentPresenter> ().First (p => p.Name == "PART_ContentPresenter");

            pointer.Focus (global::Avalonia.Input.NavigationMethod.Pointer);
            Assert.NotEqual (Color.Parse ("#ff0000"), (Presenter (pointer).Background as ISolidColorBrush)?.Color);

            keyboard.Focus (global::Avalonia.Input.NavigationMethod.Tab);
            Assert.Equal (Color.Parse ("#ff0000"), BrushColor (Presenter (keyboard).Background));
        });

        [Fact]
        public Task TextBoxFocus_WritesTheFocusedResources () => Headless.Run (() => {
            AvaloniaCssTheme.Apply ("TextBox:focus { background-color: #111111; color: #222222; border-color: #ff0000; }");

            Assert.Equal (Color.Parse ("#111111"), BrushColor (Resource ("TextControlBackgroundFocused")));
            Assert.Equal (Color.Parse ("#222222"), BrushColor (Resource ("TextControlForegroundFocused")));
            Assert.Equal (Color.Parse ("#ff0000"), BrushColor (Resource ("TextControlBorderBrushFocused")));
        });

        [Fact]
        public Task TextBoxFocusVisible_WritesTheSameResources_AsFocus () => Headless.Run (() => {
            AvaloniaCssTheme.Apply ("TextBox:focus { border-color: #ff0000; } TextBox:focus-visible { border-color: #00ff00; }");

            // A focused text box counts as :focus-visible in the core, and Fluent has one focused look, so the later state wins.
            Assert.Equal (Color.Parse ("#00ff00"), BrushColor (Resource ("TextControlBorderBrushFocused")));
        });

        [Fact]
        public Task LinkLabelStates_WriteTheHyperlinkResources () => Headless.Run (() => {
            AvaloniaCssTheme.Apply ("LinkLabel:active { color: #ff0000; } LinkLabel:disabled { color: #00ff00; background-color: #0000ff; }");

            Assert.Equal (Color.Parse ("#ff0000"), BrushColor (Resource ("HyperlinkButtonForegroundPressed")));
            Assert.Equal (Color.Parse ("#00ff00"), BrushColor (Resource ("HyperlinkButtonForegroundDisabled")));
            Assert.Equal (Color.Parse ("#0000ff"), BrushColor (Resource ("HyperlinkButtonBackgroundDisabled")));
        });

        [Fact]
        public Task TrackBarActive_AndDisabled_WriteTheSliderContainer () => Headless.Run (() => {
            AvaloniaCssTheme.Apply ("TrackBar:active { background-color: #ff0000; } TrackBar:disabled { background-color: #00ff00; }");

            Assert.Equal (Color.Parse ("#ff0000"), BrushColor (Resource ("SliderContainerBackgroundPressed")));
            Assert.Equal (Color.Parse ("#00ff00"), BrushColor (Resource ("SliderContainerBackgroundDisabled")));
        });

        [Fact]
        public Task PropertyWithoutASeamInAState_IsReportedPerDeclaration_AndDoesNotOverrideTheBaseRule () => Headless.Run (() => {
            AvaloniaCssTheme.Apply ("TrackBar { background-color: #112233; } TrackBar:focus { background-color: #ff0000; } TrackBar:focus { color: #00ff00; }");

            Assert.Equal (Color.Parse ("#112233"), BrushColor (Resource ("SliderContainerBackground")));
            Assert.Single (AvaloniaCssTheme.Diagnostics, d => d.Message.StartsWith ("'TrackBar:focus { background-color }'", StringComparison.Ordinal));
            Assert.Single (AvaloniaCssTheme.Diagnostics, d => d.Message.StartsWith ("'TrackBar:focus { color }'", StringComparison.Ordinal));
        });

        [Fact]
        public Task UnsupportedPropertyInAStateWithASeam_IsReportedPerDeclaration () => Headless.Run (() => {
            AvaloniaCssTheme.Apply ("Button:active { background-color: #ff0000; font-size: 20px; }");

            var warning = Assert.Single (AvaloniaCssTheme.Diagnostics, d => d.Message.StartsWith ("'Button:active { font-size }'", StringComparison.Ordinal));
            Assert.Equal (MF.ThemeCssSeverity.Warning, warning.Severity);
        });

        // ---- phase 3: corners and borders --------------------------------------------------------------------

        [Fact]
        public Task PerCornerRadius_BuildsOneCornerRadius () => Headless.Run (() => {
            AvaloniaCssTheme.Apply ("Button { border-radius: 12px 12px 0 0; } TextBox { border-radius: 5px; border-top-left-radius: 1px; }");

            var button = Show (new Button { Content = "OK" });
            var text = Show (new TextBox ());

            Assert.Equal (new CornerRadius (12, 12, 0, 0), button.CornerRadius);
            Assert.Equal (new CornerRadius (1, 5, 5, 5), text.CornerRadius);
            Assert.DoesNotContain (AvaloniaCssTheme.Diagnostics, d => d.Message.Contains ("border-top-left-radius", StringComparison.Ordinal));
        });

        [Fact]
        public Task BorderRadius_AfterACorner_ResetsIt_AndACornerAfterBorderRadius_Wins () => Headless.Run (() => {
            AvaloniaCssTheme.Apply ("Button { border-top-left-radius: 9px; border-radius: 4px; } TextBox { border-radius: 4px; border-bottom-right-radius: 0; }");

            var button = Show (new Button { Content = "OK" });
            var text = Show (new TextBox ());

            Assert.Equal (new CornerRadius (4), button.CornerRadius);
            Assert.Equal (new CornerRadius (4, 4, 0, 4), text.CornerRadius);
        });

        [Fact]
        public Task DashedBorders_StayUnsupported_AndAreReported () => Headless.Run (() => {
            AvaloniaCssTheme.Apply ("Button { border: 2px dashed #ff0000; }");

            var warning = Assert.Single (AvaloniaCssTheme.Diagnostics, d => d.Message.StartsWith ("'Button { border-style }'", StringComparison.Ordinal));
            Assert.Equal (MF.ThemeCssSeverity.Warning, warning.Severity);
        });

        // ---- phase 4: effects --------------------------------------------------------------------------------

        private static global::Avalonia.Controls.Presenters.ContentPresenter PartOf (Button button)
            => global::Avalonia.VisualTree.VisualExtensions.GetVisualDescendants (button).OfType<global::Avalonia.Controls.Presenters.ContentPresenter> ().First (p => p.Name == "PART_ContentPresenter");

        private static Border BorderOf (Control control, string name)
            => global::Avalonia.VisualTree.VisualExtensions.GetVisualDescendants (control).OfType<Border> ().First (b => b.Name == name);

        [Fact]
        public Task Opacity_FadesTheControl_AndTheStatesChangeIt () => Headless.Run (() => {
            AvaloniaCssTheme.Apply ("Button { opacity: .5; } Button:disabled { opacity: 40%; } TextBox { opacity: .75; }");

            var enabled = Show (new Button { Content = "A" });
            var disabled = new Button { Content = "B", IsEnabled = false };
            var text = Show (new TextBox ());
            var window = new Window { Content = new StackPanel { Children = { disabled } } };
            window.Show ();

            Assert.Equal (0.5, enabled.Opacity);
            Assert.Equal (0.4, disabled.Opacity, 3);
            Assert.Equal (0.75, text.Opacity);
            Assert.DoesNotContain (AvaloniaCssTheme.Diagnostics, d => d.Message.Contains ("{ opacity }", StringComparison.Ordinal));
        });

        [Fact]
        public Task BoxShadow_IsPutOnTheTemplatesRootPart_ListAndInsetIncluded () => Headless.Run (() => {
            AvaloniaCssTheme.Apply ("Button { box-shadow: 4px 6px 8px 2px #ff0000, inset 0 0 0 1px #0000ff; } TextBox { box-shadow: 0 2px 4px #00ff00; }");

            var button = Show (new Button { Content = "OK" });
            var text = Show (new TextBox ());

            var shadows = PartOf (button).BoxShadow;
            Assert.Equal (2, shadows.Count);
            Assert.Equal (4, shadows[0].OffsetX);
            Assert.Equal (6, shadows[0].OffsetY);
            Assert.Equal (8, shadows[0].Blur);
            Assert.Equal (2, shadows[0].Spread);
            Assert.False (shadows[0].IsInset);
            Assert.Equal (Color.Parse ("#ff0000"), shadows[0].Color);
            Assert.True (shadows[1].IsInset);
            Assert.Equal (Color.Parse ("#0000ff"), shadows[1].Color);

            Assert.Equal (Color.Parse ("#00ff00"), BorderOf (text, "PART_BorderElement").BoxShadow[0].Color);
        });

        [Fact]
        public Task BoxShadow_OnHover_IsAppliedOnlyWhileHovered () => Headless.Run (() => {
            AvaloniaCssTheme.Apply ("Button:hover { box-shadow: 0 4px 8px #ff0000; }");

            var button = Show (new Button { Content = "OK" });
            Assert.Equal (0, PartOf (button).BoxShadow.Count);

            ((global::Avalonia.Controls.IPseudoClasses) button.Classes).Set (":pointerover", true);

            Assert.Equal (1, PartOf (button).BoxShadow.Count);
            Assert.Equal (Color.Parse ("#ff0000"), PartOf (button).BoxShadow[0].Color);
        });

        [Fact]
        public Task BoxShadow_None_ClearsTheShadow () => Headless.Run (() => {
            AvaloniaCssTheme.Apply ("Button { box-shadow: 2px 2px red; } Button { box-shadow: none; }");

            var button = Show (new Button { Content = "OK" });

            Assert.Equal (0, PartOf (button).BoxShadow.Count);
        });

        [Fact]
        public Task LinearGradient_IsTheButtonFill () => Headless.Run (() => {
            AvaloniaCssTheme.Apply ("Button { background-image: linear-gradient(to right, #ff0000, #0000ff 80%); }");

            var brush = Assert.IsType<LinearGradientBrush> (Resource ("ButtonBackground"));
            Assert.Equal (2, brush.GradientStops.Count);
            Assert.Equal (Color.Parse ("#ff0000"), brush.GradientStops[0].Color);
            Assert.Equal (0.0, brush.GradientStops[0].Offset, 3);
            Assert.Equal (0.8, brush.GradientStops[1].Offset, 3);
            Assert.Equal (0.0, brush.StartPoint.Point.X, 3);
            Assert.Equal (1.0, brush.EndPoint.Point.X, 3);
            Assert.Equal (brush.StartPoint.Point.Y, brush.EndPoint.Point.Y, 3);

            var button = Show (new Button { Content = "OK" });
            Assert.IsType<LinearGradientBrush> (PartOf (button).Background);
        });

        [Fact]
        public Task Gradient_StopsFollowCssFixUp () => Headless.Run (() => {
            AvaloniaCssTheme.Apply ("ListBox { background-image: linear-gradient(#ff0000, #ffff00 20%, #00ff00, #0000ff); }");

            var list = Show (new ListBox ());
            var brush = Assert.IsType<LinearGradientBrush> (list.Background);

            Assert.Equal (new[] { 0.0, 0.2, 0.6, 1.0 }, brush.GradientStops.Select (s => Math.Round (s.Offset, 3)).ToArray ());
        });

        [Fact]
        public Task RadialAndConicGradients_BuildTheirBrushes () => Headless.Run (() => {
            AvaloniaCssTheme.Apply ("ListBox { background-image: radial-gradient(circle at 25% 75%, #ffffff, #000000); } TreeView { background-image: conic-gradient(from 90deg at 50% 50%, red, blue); }");

            var list = Show (new ListBox ());
            var tree = Show (new TreeView ());

            var radial = Assert.IsType<RadialGradientBrush> (list.Background);
            Assert.Equal (0.25, radial.Center.Point.X, 3);
            Assert.Equal (0.75, radial.Center.Point.Y, 3);

            var conic = Assert.IsType<ConicGradientBrush> (tree.Background);
            Assert.Equal (90, conic.Angle);
            Assert.Equal (2, conic.GradientStops.Count);
        });

        [Fact]
        public Task RepeatingGradient_RepeatsItsStopSpan () => Headless.Run (() => {
            AvaloniaCssTheme.Apply ("ListBox { background-image: repeating-linear-gradient(to right, #ff0000 0%, #0000ff 25%); }");

            var list = Show (new ListBox ());
            var brush = Assert.IsType<LinearGradientBrush> (list.Background);

            Assert.Equal (GradientSpreadMethod.Repeat, brush.SpreadMethod);
            Assert.Equal (0.25, brush.EndPoint.Point.X - brush.StartPoint.Point.X, 3);
            Assert.Equal (new[] { 0.0, 1.0 }, brush.GradientStops.Select (s => s.Offset).ToArray ());
        });

        [Fact]
        public Task FocusVisibleOutline_BecomesTheFocusAdorner () => Headless.Run (() => {
            AvaloniaCssTheme.Apply ("Button:focus-visible { outline: 2px solid #ff0000; outline-offset: 3px; }");

            var button = Show (new Button { Content = "OK" });

            var template = Assert.IsAssignableFrom<global::Avalonia.Controls.ITemplate<Control>> (button.FocusAdorner);
            var ring = Assert.IsType<Border> (template.Build ());
            Assert.Equal (Color.Parse ("#ff0000"), BrushColor (ring.BorderBrush));
            Assert.Equal (new Thickness (2), ring.BorderThickness);
            Assert.Equal (new Thickness (-5), ring.Margin);
            Assert.DoesNotContain (AvaloniaCssTheme.Diagnostics, d => d.Message.Contains ("outline-", StringComparison.Ordinal) && d.Severity == MF.ThemeCssSeverity.Warning);
        });

        [Fact]
        public Task OutlineStyleNone_LeavesFluentsFocusAdorner () => Headless.Run (() => {
            AvaloniaCssTheme.Apply ("Button:focus-visible { outline-width: 2px; outline-color: #ff0000; }");

            var button = Show (new Button { Content = "OK" });
            var fluent = new Button { Content = "Fluent" };

            Assert.Equal (fluent.FocusAdorner is null, button.FocusAdorner is null);
        });

        [Fact]
        public Task OutlineAtRest_IsReportedUnsupported () => Headless.Run (() => {
            AvaloniaCssTheme.Apply ("Button { outline: 2px solid red; }");

            var warning = Assert.Single (AvaloniaCssTheme.Diagnostics, d => d.Message.StartsWith ("'Button { outline-width }'", StringComparison.Ordinal));
            Assert.Equal (MF.ThemeCssSeverity.Warning, warning.Severity);
        });

        [Fact]
        public Task ShadowOnAControlWithoutANamedRoot_IsReportedUnsupported () => Headless.Run (() => {
            AvaloniaCssTheme.Apply ("TreeView { box-shadow: 2px 2px red; }");

            var warning = Assert.Single (AvaloniaCssTheme.Diagnostics, d => d.Message.StartsWith ("'TreeView { box-shadow }'", StringComparison.Ordinal));
            Assert.Equal (MF.ThemeCssSeverity.Warning, warning.Severity);
        });

        // ---- phase 5: motion ---------------------------------------------------------------------------------

        private static global::Avalonia.AvaloniaProperty? PropertyOf (ITransition transition) => transition switch {
            BrushTransition b => b.Property,
            BoxShadowsTransition s => s.Property,
            CornerRadiusTransition c => c.Property,
            DoubleTransition d => d.Property,
            _ => null
        };

        private static AvaloniaThemeBuild Built (string css)
        {
            var sheet = MF.ThemeStyleSheet.Parse (css);
            Assert.False (sheet.HasErrors, string.Join ("\n", sheet.Diagnostics));
            MF.Theme.ApplyStyleSheet (sheet);
            return AvaloniaThemeApplier.Build (MF.Theme.CurrentStyleSheets);
        }

        [Fact]
        public Task Transition_IsPutOnTheTemplatePart_WithTheRulesTimingAndEasing () => Headless.Run (() => {
            AvaloniaCssTheme.Apply ("Button { transition: background-color 150ms ease-in-out, box-shadow .2s linear 20ms, opacity 100ms; }");

            var button = Show (new Button { Content = "OK" });
            var part = PartOf (button);

            var brush = Assert.Single (part.Transitions!.OfType<BrushTransition> (), t => t.Property == global::Avalonia.Controls.Presenters.ContentPresenter.BackgroundProperty);
            Assert.Equal (TimeSpan.FromMilliseconds (150), brush.Duration);
            Assert.IsType<ThemeEasing> (brush.Easing);

            var shadow = Assert.Single (part.Transitions!.OfType<BoxShadowsTransition> ());
            Assert.Equal (TimeSpan.FromMilliseconds (200), shadow.Duration);
            Assert.Equal (TimeSpan.FromMilliseconds (20), shadow.Delay);

            // The opacity lives on the control, not on the part.
            Assert.Single (button.Transitions!.OfType<DoubleTransition> (), t => t.Property == Visual.OpacityProperty);
            Assert.DoesNotContain (part.Transitions!, t => PropertyOf (t) == Visual.OpacityProperty);
        });

        [Fact]
        public Task Transition_Easing_FollowsTheCssCurve () => Headless.Run (() => {
            AvaloniaCssTheme.Apply ("Button { transition: background-color 100ms cubic-bezier(.22, 1, .36, 1); }");

            var part = PartOf (Show (new Button { Content = "OK" }));
            var easing = Assert.IsType<ThemeEasing> (Assert.Single (part.Transitions!.OfType<BrushTransition> ()).Easing);

            // A strongly front-loaded curve: it is most of the way there at the half.
            Assert.True (easing.Ease (0.5) > 0.8, $"{easing.Ease (0.5)}");
            Assert.Equal (0, easing.Ease (0), 3);
            Assert.Equal (1, easing.Ease (1), 3);
        });

        [Fact]
        public Task StateTransition_InheritsWhatItLeavesUnsaid_AndIsActiveOnlyInTheState () => Headless.Run (() => {
            AvaloniaCssTheme.Apply ("Button { transition: background-color 150ms ease-in-out; } Button:hover { transition-duration: 60ms; }");

            var button = Show (new Button { Content = "OK" });
            Assert.Equal (TimeSpan.FromMilliseconds (150), Assert.Single (PartOf (button).Transitions!.OfType<BrushTransition> ()).Duration);

            ((global::Avalonia.Controls.IPseudoClasses) button.Classes).Set (":pointerover", true);

            // The style being entered decides: 60ms, with the property and easing of the resting rule.
            var hovered = Assert.Single (PartOf (button).Transitions!.OfType<BrushTransition> ());
            Assert.Equal (TimeSpan.FromMilliseconds (60), hovered.Duration);
            Assert.Equal (global::Avalonia.Controls.Presenters.ContentPresenter.BackgroundProperty, hovered.Property);
        });

        [Fact]
        public Task TransitionAll_CoversEveryAnimatableProperty () => Headless.Run (() => {
            AvaloniaCssTheme.Apply ("TextBox { transition: all 120ms; }");

            var text = Show (new TextBox ());
            var part = BorderOf (text, "PART_BorderElement");

            Assert.Contains (part.Transitions!, t => PropertyOf (t) == Border.BackgroundProperty);
            Assert.Contains (part.Transitions!, t => PropertyOf (t) == Border.BorderBrushProperty);
            Assert.Contains (part.Transitions!, t => PropertyOf (t) == Border.BoxShadowProperty);
            Assert.Contains (part.Transitions!, t => PropertyOf (t) == Border.CornerRadiusProperty);
            Assert.Contains (text.Transitions!, t => PropertyOf (t) == Visual.OpacityProperty);
            // A Border has no text colour: it moves on the TextBox itself.
            Assert.Contains (text.Transitions!, t => PropertyOf (t) == global::Avalonia.Controls.Primitives.TemplatedControl.ForegroundProperty);
        });

        [Fact]
        public Task TransitionNone_AndNoTransition_LeaveNoTransitions () => Headless.Run (() => {
            AvaloniaCssTheme.Apply ("Button { transition: none; }");

            var part = PartOf (Show (new Button { Content = "OK" }));

            Assert.True (part.Transitions is null || part.Transitions.OfType<BrushTransition> ().All (t => t.Duration == TimeSpan.Zero));
        });

        [Fact]
        public Task Keyframes_BecomeAnAvaloniaAnimation_OnTheControl_ForOpacity () => Headless.Run (() => {
            var build = Built (@"
                @keyframes pulse { from { opacity: 1; } 50% { opacity: .5; animation-timing-function: ease-in; } to { opacity: 1; } }
                Button { animation: pulse 1.2s ease-in-out 2 alternate both; }");

            // One per control selector the rule lands on (Button and ToggleButton).
            var animation = build.Styles.SelectMany (s => s.Animations).OfType<Animation> ()
                .First (a => a.Children.SelectMany (k => k.Setters).OfType<Setter> ().Any (s => s.Property == Visual.OpacityProperty));

            Assert.Equal (TimeSpan.FromMilliseconds (1200), animation.Duration);
            Assert.Equal (new IterationCount (2), animation.IterationCount);
            Assert.Equal (PlaybackDirection.Alternate, animation.PlaybackDirection);
            Assert.Equal (FillMode.Both, animation.FillMode);
            Assert.Equal (new[] { 0.0, 0.5, 1.0 }, animation.Children.Select (k => k.Cue.CueValue).ToArray ());
            Assert.IsType<ThemeEasing> (animation.Easing);
            Assert.Equal (0.5, (double) animation.Children[1].Setters.OfType<Setter> ().Single ().Value!, 3);
            Assert.NotNull (animation.Children[1].KeySpline);
        });

        [Fact]
        public Task Keyframes_OnColoursAndShadow_GoToTheTemplatePart () => Headless.Run (() => {
            var build = Built (@"
                @keyframes glow { from { background-color: #000000; box-shadow: 0 0 0 0 #ff0000; } to { background-color: #ffffff; box-shadow: 0 0 8px 2px #ff0000; } }
                Button { animation: glow 1s infinite; }");

            var animation = build.Styles.SelectMany (s => s.Animations).OfType<Animation> ()
                .First (a => a.Children.SelectMany (k => k.Setters).OfType<Setter> ().Any (s => s.Property == global::Avalonia.Controls.Presenters.ContentPresenter.BackgroundProperty));

            Assert.Equal (IterationCount.Infinite, animation.IterationCount);
            var last = animation.Children[1].Setters.OfType<Setter> ().ToList ();
            Assert.Contains (last, s => s.Property == global::Avalonia.Controls.Presenters.ContentPresenter.BoxShadowProperty);
            Assert.Equal (Colors.White, BrushColor (last.Single (s => s.Property == global::Avalonia.Controls.Presenters.ContentPresenter.BackgroundProperty).Value));
        });

        [Fact]
        public Task StateAnimation_IsOnTheStateStyle_NotTheRestingOne () => Headless.Run (() => {
            var build = Built (@"
                @keyframes pulse { to { opacity: .5; } }
                Button:hover { animation: pulse 1s; }");

            var styles = build.Styles.Where (s => s.Animations.Count > 0).ToList ();

            Assert.NotEmpty (styles);
            Assert.All (styles, s => Assert.Contains (":pointerover", s.Selector!.ToString ()));
        });

        [Fact]
        public Task MotionOnAControlWithoutANamedPart_IsReportedUnsupported () => Headless.Run (() => {
            AvaloniaCssTheme.Apply ("TrackBar { transition: opacity 100ms; }");

            var warning = Assert.Single (AvaloniaCssTheme.Diagnostics, d => d.Message.StartsWith ("'TrackBar { transition-property }'", StringComparison.Ordinal));
            Assert.Equal (MF.ThemeCssSeverity.Warning, warning.Severity);
        });

        [Fact]
        public Task MotionOnAButton_IsReportedApproximate_NotUnsupported () => Headless.Run (() => {
            AvaloniaCssTheme.Apply ("Button { transition: background-color 100ms; }");

            var info = Assert.Single (AvaloniaCssTheme.Diagnostics, d => d.Message.StartsWith ("'Button { transition-property }'", StringComparison.Ordinal));
            Assert.Equal (MF.ThemeCssSeverity.Info, info.Severity);
        });

        // ---- phase 6: elements the app opts in -----------------------------------------------------------------

        [Fact]
        public Task MarkedBorder_TakesTheRulesOfItsSelector_AndAnUnmarkedOneDoesNot () => Headless.Run (() => {
            AvaloniaCssTheme.Apply ("StatusBar { background-color: #112233; color: #ffffff; border: 2px solid #ff0000; border-radius: 3px 0 0 3px; font-size: 20px; font-weight: bold; }");

            var marked = new Border ();
            CssTheme.SetSelector (marked, "StatusBar");
            var plain = new Border ();
            var window = new Window { Content = new StackPanel { Children = { marked, plain } } };
            window.Show ();

            Assert.Equal (Color.Parse ("#112233"), BrushColor (marked.Background));
            Assert.Equal (Color.Parse ("#ff0000"), BrushColor (marked.BorderBrush));
            Assert.Equal (new Thickness (2), marked.BorderThickness);
            Assert.Equal (new CornerRadius (3, 0, 0, 3), marked.CornerRadius);
            Assert.Equal (Color.Parse ("#ffffff"), BrushColor (global::Avalonia.Controls.Documents.TextElement.GetForeground (marked)));
            Assert.Equal (20, global::Avalonia.Controls.Documents.TextElement.GetFontSize (marked));
            Assert.Equal (FontWeight.Bold, global::Avalonia.Controls.Documents.TextElement.GetFontWeight (marked));

            Assert.Null (plain.Background);
            Assert.Equal (default, plain.BorderThickness);
            Assert.DoesNotContain (AvaloniaCssTheme.Diagnostics, d => d.Message.Contains ("has no Avalonia counterpart", StringComparison.Ordinal));
        });

        [Fact]
        public Task MarkedPanel_TakesBackgroundAndText_ButNoBorder () => Headless.Run (() => {
            AvaloniaCssTheme.Apply ("Panel { background-color: #445566; color: #00ff00; }");

            var panel = new StackPanel ();
            CssTheme.SetSelector (panel, "Panel");
            var window = new Window { Content = panel };
            window.Show ();

            Assert.Equal (Color.Parse ("#445566"), BrushColor (panel.Background));
            Assert.Equal (Color.Parse ("#00ff00"), BrushColor (global::Avalonia.Controls.Documents.TextElement.GetForeground (panel)));
        });

        [Fact]
        public Task EachSelectorTakesOnlyItsOwnMarkedElements () => Headless.Run (() => {
            AvaloniaCssTheme.Apply ("ToolBar { background-color: #111111; } StatusBar { background-color: #222222; }");

            var tools = new Border ();
            var status = new Border ();
            CssTheme.SetSelector (tools, "ToolBar");
            CssTheme.SetSelector (status, "StatusBar");
            var window = new Window { Content = new StackPanel { Children = { tools, status } } };
            window.Show ();

            Assert.Equal (Color.Parse ("#111111"), BrushColor (tools.Background));
            Assert.Equal (Color.Parse ("#222222"), BrushColor (status.Background));
        });

        [Fact]
        public Task MarkedElement_ThatIsAnUnsupportedPropertyOnAPanel_IsStillReportedForTheSelector () => Headless.Run (() => {
            AvaloniaCssTheme.Apply ("GroupBox { box-shadow: 2px 2px red; }");

            var warning = Assert.Single (AvaloniaCssTheme.Diagnostics, d => d.Message.StartsWith ("'GroupBox { box-shadow }'", StringComparison.Ordinal));
            Assert.Equal (MF.ThemeCssSeverity.Warning, warning.Severity);
        });

        [Fact]
        public Task OpacityOnAMarkedElement_FadesIt () => Headless.Run (() => {
            AvaloniaCssTheme.Apply ("StatusBar { opacity: .5; }");

            var marked = new Border ();
            CssTheme.SetSelector (marked, "StatusBar");
            new Window { Content = marked }.Show ();

            Assert.Equal (0.5, marked.Opacity);
        });
    }
}
