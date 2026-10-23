using System;
using System.Linq;
using Majorsilence.Forms.Headless;
using SkiaSharp;
using Xunit;

namespace Majorsilence.Forms.Tests
{
    // Classes and ids in theme CSS (docs/theming-selectors-plan.md): `.primary`, `Button.primary:hover`, `#saveButton`. The parser's model
    // is checked directly; what a rule MEANS -- specificity, toggling, ids, @media, scopes -- is proved by the pixels painted, since a
    // rule that parses but reaches no control is the silent no-op the subset forbids.
    [Collection ("Headless")]
    public class ThemeCssClassAndIdTests : IDisposable
    {
        private static readonly SKColor Red = new (0xff, 0, 0);
        private static readonly SKColor Blue = new (0, 0, 0xff);
        private static readonly SKColor Green = new (0, 0xff, 0);
        private static readonly SKColor Black = new (0, 0, 0);
        private static readonly SKColor White = new (0xff, 0xff, 0xff);

        public ThemeCssClassAndIdTests ()
        {
            HeadlessRenderer.Use ();
            HeadlessRenderer.AnimationClock.Reset ();
            HeadlessRenderer.PrefersReducedMotion = false;
            HeadlessRenderer.PrefersDarkColorScheme = false;
            Theme.SetBuiltInTheme (BuiltInTheme.Light);
        }

        public void Dispose ()
        {
            GC.SuppressFinalize (this);
            HeadlessRenderer.AnimationClock.Reset ();
            HeadlessRenderer.PrefersReducedMotion = false;
            HeadlessRenderer.PrefersDarkColorScheme = false;
            Theme.SetBuiltInTheme (BuiltInTheme.Light);
        }

        private static string ErrorsOf (string css)
            => string.Join ("\n", ThemeStyleSheet.Parse (css).Diagnostics.Where (d => d.Severity == ThemeCssSeverity.Error).Select (d => d.Message));

        // ---- the model ----------------------------------------------------------------------------

        [Theory]
        [InlineData ("Button { color: red; }", "", null, "(0,0,1)", false)]
        [InlineData ("Button:hover { color: red; }", "", null, "(0,1,1)", false)]
        [InlineData (".primary { color: red; }", "primary", null, "(0,1,0)", true)]
        [InlineData ("Button.primary { color: red; }", "primary", null, "(0,1,1)", false)]
        [InlineData ("Button.primary:hover { color: red; }", "primary", null, "(0,2,1)", false)]
        [InlineData (".primary:hover { color: red; }", "primary", null, "(0,2,0)", true)]
        [InlineData (".a.b { color: red; }", "a b", null, "(0,2,0)", true)]
        [InlineData (".a.a { color: red; }", "a", null, "(0,1,0)", true)]
        [InlineData ("#saveButton { color: red; }", "", "saveButton", "(1,0,0)", true)]
        [InlineData ("Button#saveButton.primary { color: red; }", "primary", "saveButton", "(1,1,1)", false)]
        [InlineData ("DataGridView::header { color: red; }", "", null, "(0,0,2)", false)]
        public void Compound_selectors_parse_into_classes_an_id_and_a_specificity (string css, string classes, string? id, string specificity, bool typeless)
        {
            var sheet = ThemeStyleSheet.Parse (css);

            Assert.False (sheet.HasErrors, ErrorsOf (css));
            var rule = Assert.Single (sheet.Rules);

            Assert.Equal (classes, string.Join (" ", rule.Classes));
            Assert.Equal (id, rule.Id);
            Assert.Equal (specificity, rule.Specificity.ToString ());
            Assert.Equal (typeless, rule.AppliesToAnyType);
            Assert.Equal (rule.Classes.Count > 0 || id is not null, rule.IsScoped);
        }

        [Fact]
        public void Every_documented_selector_form_parses_with_the_specificity_the_reference_states ()
        {
            Assert.NotEmpty (ThemeCssReference.ClassAndIdForms);

            foreach (var form in ThemeCssReference.ClassAndIdForms) {
                var sheet = ThemeStyleSheet.Parse (form.Selector + " { color: red; }");

                Assert.False (sheet.HasErrors, $"{form.Selector}: {ErrorsOf (form.Selector + " { color: red; }")}");
                Assert.Equal (form.Specificity, Assert.Single (sheet.Rules).Specificity.ToString ());
            }
        }

        [Fact]
        public void A_rule_with_no_type_has_a_safe_selector_hosts_can_test_for ()
        {
            var rule = Assert.Single (ThemeStyleSheet.Parse (".primary { color: red; }").Rules);

            // Never null, so a host that assumes Selector is a real control type does not fail with a null reference.
            Assert.NotNull (rule.Selector);
            Assert.True (rule.Selector.IsAny);
            Assert.True (rule.AppliesToAnyType);
            Assert.DoesNotContain (ThemeCssReference.Selectors, s => s.IsAny);
            Assert.StartsWith (".primary {", rule.ToString ());

            var typed = Assert.Single (ThemeStyleSheet.Parse ("Button.primary { color: red; }").Rules);
            Assert.False (typed.Selector.IsAny);
            Assert.Equal ("Button", typed.Selector.Name);
            Assert.StartsWith ("Button.primary {", typed.ToString ());
        }

        [Theory]
        [InlineData ("Button.big { color: red; } .big:hover { color: blue; }")]
        [InlineData ("#a, .b, Button.c:disabled { color: red; }")]
        [InlineData (".primary { --bg: #123456; background-color: var(--bg); } .primary:hover { --bg: #654321; }")]
        public void Compound_selectors_are_valid (string css) => Assert.Equal (string.Empty, ErrorsOf (css));

        [Theory]
        // combinators say they come in a later phase, and what to do now
        [InlineData ("Panel Button { color: red; }", "combinators come in a later phase")]
        [InlineData ("Panel > Button { color: red; }", "combinators come in a later phase")]
        [InlineData ("Button + Button { color: red; }", "combinators come in a later phase")]
        [InlineData ("Button ~ Button { color: red; }", "combinators come in a later phase")]
        [InlineData (".toolbar Button { color: red; }", "combinators come in a later phase")]
        [InlineData ("Button .primary { color: red; }", "combinators come in a later phase")]
        [InlineData (".a > .b { color: red; }", "combinators come in a later phase")]
        [InlineData ("Button:not(.primary) { color: red; }", "':not ()' is not supported yet")]
        [InlineData (".a:is(.b, .c) { color: red; }", "':is ()' is not supported yet")]
        [InlineData ("Panel:has(Button) { color: red; }", "':has ()' is not supported yet")]
        [InlineData ("Button[type] { color: red; }", "attribute selectors are not supported yet")]
        [InlineData ("[data-x] { color: red; }", "is not a valid selector")]
        // a class or id with a ::part, for now
        [InlineData ("DataGridView.dense::header { color: red; }", "cannot be combined with a ::part yet")]
        [InlineData ("DataGridView#grid::header { color: red; }", "cannot be combined with a ::part yet")]
        [InlineData (".a::header { color: red; }", "a part belongs to a control type")]
        // malformed compounds
        [InlineData ("Button. { color: red; }", "expected a class name after '.'")]
        [InlineData ("Button.1x { color: red; }", "is not a valid selector")]
        [InlineData ("#a#b { color: red; }", "a selector names one id")]
        [InlineData ("Button:hover.primary { color: red; }", "put classes and ids before the pseudo-class, e.g. 'Button.primary:hover'")]
        [InlineData ("Button:hover:active { color: red; }", "takes one pseudo-class at a time")]
        [InlineData ("Form.dialog { color: red; }", "a window has no style classes or id")]
        [InlineData ("Form#main { color: red; }", "a window has no style classes or id")]
        [InlineData ("Buton.primary { color: red; }", "Unknown selector 'Buton'. Did you mean 'Button'?")]
        [InlineData ("TextBox.big:hover { color: red; }", "'TextBox:hover' is not supported")]
        public void Selector_forms_that_are_not_supported_are_errors_that_say_why (string css, string expected)
        {
            var errors = ErrorsOf (css);

            Assert.Contains (expected, errors);
        }

        [Fact]
        public void Class_and_id_rules_do_not_touch_the_type_level_styles ()
        {
            var before = Button.DefaultStyle.BackgroundColor;
            var panelBefore = Panel.DefaultStyle.BackgroundColor;

            Theme.LoadFromCss (".primary { background-color: #ff0000; } Button.primary { background-color: #ff0000; } #x { background-color: #ff0000; }");

            Assert.Equal (before, Button.DefaultStyle.BackgroundColor);
            Assert.Equal (panelBefore, Panel.DefaultStyle.BackgroundColor);
        }

        // ---- painting -----------------------------------------------------------------------------

        // A form with a decoy button that takes the initial focus, and the control under test beside it.
        private static (Form Form, T Control) Show<T> (T control, Action<T>? configure = null) where T : Control
        {
            var form = new Form { Size = new System.Drawing.Size (240, 180) };
            form.Controls.Add (new Button { Location = System.Drawing.Point.Empty, Size = new System.Drawing.Size (20, 15), TabIndex = 0 });
            control.Location = new System.Drawing.Point (40, 40);
            control.Size = new System.Drawing.Size (100, 60);
            control.TabIndex = 1;
            configure?.Invoke (control);
            form.Controls.Add (control);
            form.Show ();
            return (form, control);
        }

        private static SKColor CenterPixel (Control control, Form form)
        {
            HeadlessRenderer.CapturePng (form);
            var buffer = typeof (Control).GetMethod ("GetBackBuffer", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!;
            var bitmap = (SKBitmap) buffer.Invoke (control, null)!;
            return bitmap.GetPixel (bitmap.Width / 2, bitmap.Height / 2);
        }

        private static void Hover (Button button) => button.RaiseMouseEnter (new MouseEventArgs (MouseButtons.None, 0, 5, 5, System.Drawing.Point.Empty));

        private static SKColor Hex (string hex) => SKColor.Parse (hex);

        [Fact]
        public void A_class_rule_paints_only_the_controls_that_have_the_class ()
        {
            Theme.LoadFromCss (".card { background-color: #ff0000; }");

            var (form, with) = Show (new Panel (), p => p.StyleClasses.Add ("card"));
            using var _ = form;
            var without = new Panel { Location = new System.Drawing.Point (150, 40), Size = new System.Drawing.Size (60, 60) };
            form.Controls.Add (without);

            Assert.Equal (Red, CenterPixel (with, form));
            Assert.NotEqual (Red, CenterPixel (without, form));
        }

        [Fact]
        public void A_type_and_class_rule_reaches_only_that_type ()
        {
            Theme.LoadFromCss ("Panel.card { background-color: #ff0000; }");

            var (form, panel) = Show (new Panel (), p => p.CssClass = "card");
            using var _ = form;
            var label = new Label { Location = new System.Drawing.Point (150, 40), Size = new System.Drawing.Size (60, 60), CssClass = "card" };
            form.Controls.Add (label);

            Assert.Equal (Red, CenterPixel (panel, form));
            Assert.NotEqual (Red, CenterPixel (label, form));
        }

        [Fact]
        public void A_rule_on_a_type_reaches_the_controls_styled_by_it_too ()
        {
            // "Panel.card" reaches a type styled by Panel's rule, as "Panel { }" does.
            Theme.LoadFromCss ("Panel.card { background-color: #ff0000; }");

            var (form, flow) = Show (new FlowLayoutPanel (), p => p.StyleClasses.Add ("card"));
            using var _ = form;

            Assert.Equal (Red, CenterPixel (flow, form));
        }

        [Theory]
        // equal specificity: the later rule wins, so the order decides
        [InlineData (".a { background-color: #ff0000; } .b { background-color: #0000ff; }", "#0000ff")]
        [InlineData (".b { background-color: #0000ff; } .a { background-color: #ff0000; }", "#ff0000")]
        // an id beats a class in either order
        [InlineData ("#x { background-color: #ff0000; } .a { background-color: #0000ff; }", "#ff0000")]
        [InlineData (".a { background-color: #0000ff; } #x { background-color: #ff0000; }", "#ff0000")]
        // a type and a class beat a class alone, in either order
        [InlineData ("Panel.a { background-color: #ff0000; } .a { background-color: #0000ff; }", "#ff0000")]
        [InlineData (".a { background-color: #0000ff; } Panel.a { background-color: #ff0000; }", "#ff0000")]
        // two classes beat one, in either order
        [InlineData (".a.b { background-color: #ff0000; } .a { background-color: #0000ff; }", "#ff0000")]
        [InlineData (".a { background-color: #0000ff; } .a.b { background-color: #ff0000; }", "#ff0000")]
        // a rule for the type alone never beats a class
        [InlineData ("Panel { background-color: #0000ff; } .a { background-color: #ff0000; }", "#ff0000")]
        [InlineData (".a { background-color: #ff0000; } Panel { background-color: #0000ff; }", "#ff0000")]
        public void Specificity_decides_which_rule_paints_whatever_the_order (string css, string expected)
        {
            Theme.LoadFromCss (css);

            var (form, panel) = Show (new Panel { Name = "x" }, p => p.CssClass = "a b");
            using var _ = form;

            Assert.Equal (Hex (expected), CenterPixel (panel, form));
        }

        [Fact]
        public void With_equal_specificity_the_later_rule_wins_so_swapping_them_swaps_the_colour ()
        {
            Theme.LoadFromCss (".a { background-color: #ff0000; } .b { background-color: #0000ff; }");
            var (form, panel) = Show (new Panel (), p => p.CssClass = "a b");
            using var _ = form;
            Assert.Equal (Blue, CenterPixel (panel, form));

            Theme.LoadFromCss (".b { background-color: #0000ff; } .a { background-color: #ff0000; }");
            Assert.Equal (Red, CenterPixel (panel, form));
        }

        [Theory]
        // Button:hover (0,1,1) beats .primary (0,1,0) while hovered, in either order; at rest .primary shows
        [InlineData ("Button:hover { background-color: #ff0000; } .primary { background-color: #0000ff; }")]
        [InlineData (".primary { background-color: #0000ff; } Button:hover { background-color: #ff0000; }")]
        public void A_type_pseudo_class_rule_outranks_a_plain_class_while_hovered (string css)
        {
            Theme.LoadFromCss (css);
            var (form, button) = Show (new Button (), b => b.StyleClasses.Add ("primary"));
            using var _ = form;

            Assert.Equal (Blue, CenterPixel (button, form));
            Hover (button);
            Assert.Equal (Red, CenterPixel (button, form));
        }

        [Theory]
        // .primary:hover (0,2,0) beats Button:hover (0,1,1) in either order
        [InlineData ("Button:hover { background-color: #0000ff; } .primary:hover { background-color: #ff0000; }")]
        [InlineData (".primary:hover { background-color: #ff0000; } Button:hover { background-color: #0000ff; }")]
        public void A_class_pseudo_class_rule_outranks_a_type_pseudo_class_rule (string css)
        {
            Theme.LoadFromCss (css);
            var (form, button) = Show (new Button (), b => b.StyleClasses.Add ("primary"));
            using var _ = form;

            Hover (button);
            Assert.Equal (Red, CenterPixel (button, form));
        }

        [Theory]
        // Button.primary and Button:hover are both (0,1,1): the later one wins while hovered
        [InlineData ("Button.primary { background-color: #0000ff; } Button:hover { background-color: #ff0000; }", "ff0000")]
        [InlineData ("Button:hover { background-color: #ff0000; } Button.primary { background-color: #0000ff; }", "0000ff")]
        public void A_tie_goes_to_the_later_rule_between_a_class_and_a_type_pseudo_class (string css, string hovered)
        {
            Theme.LoadFromCss (css);
            var (form, button) = Show (new Button (), b => b.StyleClasses.Add ("primary"));
            using var _ = form;

            Hover (button);
            Assert.Equal (Hex ("#" + hovered), CenterPixel (button, form));
        }

        [Fact]
        public void A_class_rule_shows_in_the_hovered_state_too_over_the_types_built_in_hover_look ()
        {
            // The built-in hover look is the accent colour; ".primary" is the author's, and outranks it, as in a browser.
            Theme.LoadFromCss (".primary { background-color: #0000ff; }");
            var (form, button) = Show (new Button (), b => b.StyleClasses.Add ("primary"));
            using var _ = form;

            Hover (button);
            Assert.Equal (Blue, CenterPixel (button, form));
        }

        [Fact]
        public void A_button_with_a_class_hovers_as_one_without_unless_a_class_rule_says_otherwise ()
        {
            // A rule on the type alone leaves the type's own hover look alone for a classed control, as it does for any other.
            Theme.LoadFromCss ("Button { background-color: #ff0000; } .other { color: #00ff00; }");
            var (form, plain) = Show (new Button ());
            using var _ = form;
            var classed = new Button { Location = new System.Drawing.Point (150, 40), Size = new System.Drawing.Size (60, 60), TabIndex = 2, CssClass = "other" };
            form.Controls.Add (classed);

            Hover (plain);
            var plainHovered = CenterPixel (plain, form);
            Hover (classed);
            var classedHovered = CenterPixel (classed, form);

            Assert.Equal (plainHovered, classedHovered);
            Assert.NotEqual (Red, classedHovered);
        }

        [Fact]
        public void An_explicit_value_on_the_control_wins_over_every_rule ()
        {
            Theme.LoadFromCss ("#x { background-color: #ff0000; } .a { background-color: #0000ff; }");

            var (form, panel) = Show (new Panel { Name = "x" }, p => p.CssClass = "a");
            using var _ = form;
            Assert.Equal (Red, CenterPixel (panel, form));

            panel.Style.BackgroundColor = Green;
            panel.Invalidate ();
            Assert.Equal (Green, CenterPixel (panel, form));
        }

        [Fact]
        public void Adding_removing_and_toggling_a_class_restyles_the_control ()
        {
            Theme.LoadFromCss (".on { background-color: #ff0000; }");

            var (form, panel) = Show (new Panel ());
            using var _ = form;
            var plain = CenterPixel (panel, form);
            Assert.NotEqual (Red, plain);

            Assert.True (panel.StyleClasses.Add ("on"));
            Assert.False (panel.StyleClasses.Add ("on"));
            Assert.Equal (Red, CenterPixel (panel, form));
            Assert.Equal ("on", panel.CssClass);

            Assert.True (panel.StyleClasses.Remove ("on"));
            Assert.Equal (plain, CenterPixel (panel, form));

            Assert.True (panel.StyleClasses.Toggle ("on"));
            Assert.Equal (Red, CenterPixel (panel, form));
            Assert.False (panel.StyleClasses.Toggle ("on"));
            Assert.Equal (plain, CenterPixel (panel, form));

            panel.StyleClasses.Toggle ("on", true);
            panel.StyleClasses.Toggle ("on", true);
            Assert.Equal (Red, CenterPixel (panel, form));
            panel.StyleClasses.Toggle ("on", false);
            Assert.Equal (plain, CenterPixel (panel, form));
        }

        [Fact]
        public void The_css_class_string_and_the_class_set_are_one_thing ()
        {
            Theme.LoadFromCss (".a { background-color: #ff0000; } .b { color: #00ff00; }");
            var (form, panel) = Show (new Panel ());
            using var _ = form;
            CenterPixel (panel, form);

            panel.CssClass = "  a   b ";
            Assert.Equal ("a b", panel.CssClass);
            Assert.True (panel.StyleClasses.Contains ("a"));
            Assert.True (panel.StyleClasses.Contains ("b"));
            Assert.Equal (2, panel.StyleClasses.Count);
            Assert.Equal (Red, CenterPixel (panel, form));

            panel.StyleClasses.Remove ("a");
            Assert.Equal ("b", panel.CssClass);

            panel.CssClass = string.Empty;
            Assert.Equal (0, panel.StyleClasses.Count);
            Assert.NotEqual (Red, CenterPixel (panel, form));

            Assert.False (new Panel ().StyleClasses.Contains ("a"));
        }

        [Theory]
        [InlineData ("")]
        [InlineData ("two words")]
        [InlineData ("1abc")]
        [InlineData ("a.b")]
        [InlineData ("#a")]
        public void A_class_name_a_selector_could_not_write_is_refused (string name)
        {
            // It could never match, which would be a silent no-op.
            Assert.Throws<ArgumentException> (() => new Panel ().StyleClasses.Add (name));
        }

        [Fact]
        public void An_id_matches_the_name_a_designer_gives_a_control ()
        {
            Theme.LoadFromCss ("#saveButton { background-color: #ff0000; }");

            var (form, save) = Show (new Button { Name = "saveButton" });
            using var _ = form;
            var other = new Button { Name = "button2", Location = new System.Drawing.Point (150, 40), Size = new System.Drawing.Size (60, 60), TabIndex = 2 };
            form.Controls.Add (other);

            Assert.Equal (Red, CenterPixel (save, form));
            Assert.NotEqual (Red, CenterPixel (other, form));

            // Renaming at run time restyles both.
            other.Name = "saveButton";
            save.Name = "renamed";
            Assert.Equal (Red, CenterPixel (other, form));
            Assert.NotEqual (Red, CenterPixel (save, form));
        }

        [Fact]
        public void An_id_with_a_type_only_reaches_that_type ()
        {
            Theme.LoadFromCss ("Label#x { background-color: #ff0000; }");

            var (form, panel) = Show (new Panel { Name = "x" });
            using var _ = form;

            Assert.NotEqual (Red, CenterPixel (panel, form));
        }

        [Fact]
        public void A_sheet_applied_after_the_controls_exist_restyles_them ()
        {
            var (form, panel) = Show (new Panel (), p => p.CssClass = "a");
            using var _ = form;
            var before = CenterPixel (panel, form);

            Theme.LoadFromCss (".a { background-color: #ff0000; }");
            Assert.Equal (Red, CenterPixel (panel, form));

            Theme.LoadFromCss (".a { background-color: #0000ff; }");
            Assert.Equal (Blue, CenterPixel (panel, form));

            // A sheet that no longer names the class returns the control to its type's look.
            Theme.LoadFromCss ("Button { color: red; }");
            Assert.Equal (before, CenterPixel (panel, form));
        }

        [Fact]
        public void A_rule_reads_the_tokens_and_follows_them_when_they_change ()
        {
            Theme.LoadFromCss (":root { --accent-color: #ff0000; } .a { background-color: var(--accent-color); }");
            var (form, panel) = Show (new Panel (), p => p.CssClass = "a");
            using var _ = form;
            Assert.Equal (Red, CenterPixel (panel, form));

            Theme.AccentColor = Blue;
            Assert.Equal (Blue, CenterPixel (panel, form));
        }

        [Fact]
        public void Rule_variables_are_scoped_to_the_selector_and_its_states ()
        {
            Theme.LoadFromCss (@"
                .primary { --bg: #0000ff; background-color: var(--bg); }
                .primary:hover { --bg: #ff0000; }");
            var (form, button) = Show (new Button (), b => b.StyleClasses.Add ("primary"));
            using var _ = form;

            Assert.Equal (Blue, CenterPixel (button, form));
            Hover (button);
            Assert.Equal (Red, CenterPixel (button, form));
        }

        [Fact]
        public void A_media_query_around_a_class_rule_follows_the_system_setting ()
        {
            Theme.LoadFromCss (@"
                .a { background-color: #ff0000; }
                @media (prefers-color-scheme: dark) { .a { background-color: #0000ff; } }");
            var (form, panel) = Show (new Panel (), p => p.CssClass = "a");
            using var _ = form;
            Assert.Equal (Red, CenterPixel (panel, form));

            HeadlessRenderer.PrefersDarkColorScheme = true;
            Assert.Equal (Blue, CenterPixel (panel, form));

            HeadlessRenderer.PrefersDarkColorScheme = false;
            Assert.Equal (Red, CenterPixel (panel, form));
        }

        [Fact]
        public void A_class_rule_inside_a_media_query_that_does_not_match_is_not_applied ()
        {
            Theme.LoadFromCss ("@media (prefers-color-scheme: dark) { .a { background-color: #ff0000; } }");
            var (form, panel) = Show (new Panel (), p => p.CssClass = "a");
            using var _ = form;

            Assert.NotEqual (Red, CenterPixel (panel, form));

            HeadlessRenderer.PrefersDarkColorScheme = true;
            Assert.Equal (Red, CenterPixel (panel, form));
        }

        // ---- ThemeScope ---------------------------------------------------------------------------

        [Fact]
        public void A_scope_with_a_sheet_of_its_own_uses_its_class_rules_and_the_rest_the_applications ()
        {
            Theme.LoadFromCss (".a { background-color: #ff0000; }");

            using var form = new Form { Size = new System.Drawing.Size (400, 200) };
            var inside = new Panel { Location = new System.Drawing.Point (10, 10), Size = new System.Drawing.Size (80, 80), CssClass = "a" };
            var outside = new Panel { Location = new System.Drawing.Point (200, 10), Size = new System.Drawing.Size (80, 80), CssClass = "a" };
            var scoped = new Panel { Location = new System.Drawing.Point (100, 100), Size = new System.Drawing.Size (150, 90) };
            scoped.Controls.Add (inside);
            form.Controls.Add (outside);
            form.Controls.Add (scoped);
            form.Show ();

            var scope = new ThemeScope ();
            scope.LoadFromCss (".a { background-color: #0000ff; }");
            scoped.ThemeScope = scope;

            Assert.Equal (Blue, CenterPixel (inside, form));
            Assert.Equal (Red, CenterPixel (outside, form));

            // A scope with no sheet of its own follows the application's class rules.
            scope.ResetAll ();
            Assert.Equal (Red, CenterPixel (inside, form));
        }

        [Fact]
        public void A_scopes_tokens_reach_a_class_rule_s_values ()
        {
            Theme.LoadFromCss (".a { background-color: var(--accent-color); }");

            using var form = new Form { Size = new System.Drawing.Size (400, 200) };
            var inside = new Panel { Location = new System.Drawing.Point (10, 10), Size = new System.Drawing.Size (80, 80), CssClass = "a" };
            var outside = new Panel { Location = new System.Drawing.Point (200, 10), Size = new System.Drawing.Size (80, 80), CssClass = "a" };
            var scoped = new Panel { Location = new System.Drawing.Point (100, 100), Size = new System.Drawing.Size (150, 90) };
            scoped.Controls.Add (inside);
            form.Controls.Add (outside);
            form.Controls.Add (scoped);
            form.Show ();
            scoped.ThemeScope = new ThemeScope { AccentColor = Green };

            Assert.Equal (Green, CenterPixel (inside, form));
            Assert.Equal (Theme.AccentColor, CenterPixel (outside, form));
        }

        // ---- transitions --------------------------------------------------------------------------

        private const string FadeSheet = @"
            Button { background-color: #000000; transition: background-color 100ms linear; }
            .on { background-color: #ffffff; }";

        [Fact]
        public void Toggling_a_class_fades_to_the_new_style_when_it_has_a_transition ()
        {
            Theme.LoadFromCss (FadeSheet);
            var (form, button) = Show (new Button ());
            using var _ = form;
            Assert.Equal (Black, CenterPixel (button, form));

            button.StyleClasses.Add ("on");

            // Painted right after the change: still the start colour, and a transition is running.
            Assert.Equal (Black, CenterPixel (button, form));
            Assert.True (button.IsStyleTransitionRunning);

            // Frame 1 is time zero; frame 4 is 50ms in, half way on a linear curve.
            HeadlessRenderer.AnimationClock.Step (4);
            var middle = CenterPixel (button, form);
            Assert.InRange (middle.Red, 0x70, 0x90);
            Assert.Equal (middle.Red, middle.Green);

            HeadlessRenderer.AnimationClock.Step (10);
            Assert.False (button.IsStyleTransitionRunning);
            Assert.Equal (White, CenterPixel (button, form));

            // And back: removing the class fades out again.
            button.StyleClasses.Remove ("on");
            Assert.Equal (White, CenterPixel (button, form));
            Assert.True (button.IsStyleTransitionRunning);
            HeadlessRenderer.AnimationClock.Step (10);
            Assert.Equal (Black, CenterPixel (button, form));
        }

        [Fact]
        public void Toggling_a_class_is_immediate_when_the_new_style_has_no_transition ()
        {
            Theme.LoadFromCss ("Button { background-color: #000000; } .on { background-color: #ffffff; }");
            var (form, button) = Show (new Button ());
            using var _ = form;
            Assert.Equal (Black, CenterPixel (button, form));

            button.StyleClasses.Add ("on");

            Assert.Equal (White, CenterPixel (button, form));
            Assert.False (button.IsStyleTransitionRunning);
        }

        [Fact]
        public void A_transition_on_a_class_rule_governs_the_change_into_it ()
        {
            // The destination's transition is the one that runs, as in CSS: the class brings its own.
            Theme.LoadFromCss ("Button { background-color: #000000; } .on { background-color: #ffffff; transition: background-color 100ms linear; }");
            var (form, button) = Show (new Button ());
            using var _ = form;
            Assert.Equal (Black, CenterPixel (button, form));

            button.StyleClasses.Add ("on");
            Assert.Equal (Black, CenterPixel (button, form));
            Assert.True (button.IsStyleTransitionRunning);

            HeadlessRenderer.AnimationClock.Step (10);
            Assert.Equal (White, CenterPixel (button, form));
        }

        [Fact]
        public void A_class_toggled_before_the_control_is_ever_shown_does_not_animate ()
        {
            Theme.LoadFromCss (FadeSheet);
            var button = new Button { CssClass = "on" };
            var (form, _) = Show (button);
            using var _form = form;

            Assert.Equal (White, CenterPixel (button, form));
            Assert.False (button.IsStyleTransitionRunning);
        }

        [Fact]
        public void A_keyframe_animation_a_class_names_runs_on_the_control ()
        {
            Theme.LoadFromCss (@"
                @keyframes pulse { from { background-color: #ff0000; } to { background-color: #ff0000; } }
                .pulsing { animation: pulse 1s linear infinite; }");
            var (form, panel) = Show (new Panel ());
            using var _ = form;
            CenterPixel (panel, form);
            Assert.False (panel.IsStyleAnimationRunning);

            panel.StyleClasses.Add ("pulsing");
            Assert.Equal (Red, CenterPixel (panel, form));
            Assert.True (panel.IsStyleAnimationRunning);

            panel.StyleClasses.Remove ("pulsing");
            CenterPixel (panel, form);
            HeadlessRenderer.AnimationClock.Step (2);
            CenterPixel (panel, form);
            Assert.False (panel.IsStyleAnimationRunning);
        }

        // ---- no classes: nothing changes -----------------------------------------------------------

        [Fact]
        public void A_control_with_no_class_or_name_a_rule_names_keeps_the_type_level_chain ()
        {
            Theme.LoadFromCss ("Panel { background-color: #ff0000; } .never { background-color: #0000ff; } #nobody { background-color: #0000ff; }");

            var (form, panel) = Show (new Panel { Name = "someone" });
            using var _ = form;

            Assert.Equal (Red, CenterPixel (panel, form));
            // The instance style is still chained straight to the type's default style: no layer in between.
            Assert.Same (Panel.DefaultStyle, panel.Style._parent);
        }

        [Fact]
        public void A_sheet_without_class_or_id_rules_never_builds_a_cascade ()
        {
            Theme.LoadFromCss ("Panel { background-color: #ff0000; }");
            var (form, panel) = Show (new Panel { Name = "x", CssClass = "a" });
            using var _ = form;

            Assert.Equal (Red, CenterPixel (panel, form));
            Assert.Same (Panel.DefaultStyle, panel.Style._parent);
        }

        [Fact]
        public void A_matching_control_s_style_is_chained_through_one_layer_and_taken_out_again ()
        {
            Theme.LoadFromCss (".a { background-color: #ff0000; }");
            var (form, panel) = Show (new Panel (), p => p.CssClass = "a");
            using var _ = form;
            CenterPixel (panel, form);

            Assert.NotSame (Panel.DefaultStyle, panel.Style._parent);
            Assert.Same (Panel.DefaultStyle, panel.Style._parent!._parent);

            panel.CssClass = "";
            CenterPixel (panel, form);
            Assert.Same (Panel.DefaultStyle, panel.Style._parent);
        }

        [Fact]
        public void Painting_a_classed_control_allocates_no_more_than_painting_a_plain_one ()
        {
            Theme.LoadFromCss ("Button { background-color: #333333; } Button.primary { background-color: #ff0000; } .primary:hover { color: #00ff00; } #go { border-radius: 4px; }");
            var (form, classed) = Show (new Button { Name = "go" }, b => b.StyleClasses.Add ("primary"));
            using var _ = form;
            var plain = new Button { Location = new System.Drawing.Point (150, 40), Size = new System.Drawing.Size (60, 60), TabIndex = 2 };
            form.Controls.Add (plain);

            var host = (HeadlessWindowHost) form.Backend;
            host.Size = new System.Drawing.Size (240, 180);

            long Measure (Control control)
            {
                var best = long.MaxValue;

                for (var round = 0; round < 5; round++) {
                    for (var i = 0; i < 20; i++) {
                        control.Invalidate ();
                        using var warm = host.Render ();
                    }

                    var before = GC.GetAllocatedBytesForCurrentThread ();
                    for (var i = 0; i < 20; i++) {
                        control.Invalidate ();
                        using var image = host.Render ();
                    }
                    best = Math.Min (best, (GC.GetAllocatedBytesForCurrentThread () - before) / 20);
                }

                return best;
            }

            var withoutClass = Measure (plain);
            var withClass = Measure (classed);

            // The layers are built when the sheet or the classes change, not when a control is painted, so a frame costs the same.
            Assert.True (withClass - withoutClass <= 64, $"painting a classed control allocated {withClass} B a frame, a plain one {withoutClass} B");
            Assert.Equal (Red, CenterPixel (classed, form));
        }
    }
}
