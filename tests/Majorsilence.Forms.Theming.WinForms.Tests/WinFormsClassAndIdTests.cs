using System;
using System.Drawing;
using System.Linq;
using Majorsilence.Forms.Theming.WinForms;
using Xunit;
using MF = Majorsilence.Forms;
using WF = System.Windows.Forms;

namespace Majorsilence.Forms.Theming.WinForms.Tests
{
    // Classes and ids in the theme CSS (docs/theming-selectors-plan.md) on REAL System.Windows.Forms controls: a `#id` rule styles
    // the control whose Name it is, through the same code that reads the type's rules; a class rule reaches none (a WinForms
    // control has no style classes) and is reported as an info diagnostic. Each is proved by the colour the control ends up with.
    public class WinFormsClassAndIdTests : IDisposable
    {
        private readonly WF.Form form = new () { ClientSize = new Size (400, 300) };

        public void Dispose ()
        {
            GC.SuppressFinalize (this);
            form.Dispose ();
            MF.Theme.SetBuiltInTheme (MF.BuiltInTheme.Light);
        }

        private static void AssertColor (string expectedHex, Color actual) => WinFormsParityTests.AssertColor (expectedHex, actual);

        private T Add<T> (T control) where T : WF.Control
        {
            form.Controls.Add (control);
            return control;
        }

        private void Apply (string css)
        {
            WinFormsCssTheme.Track (form);
            WinFormsCssTheme.Apply (css);
        }

        private static void Raise (WF.Control control, string method, EventArgs args)
            => control.GetType ().GetMethod (method, System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Public)!.Invoke (control, new object[] { args });

        [Fact]
        public void AnIdRule_StylesTheControlWithThatName_AndNoOther ()
        {
            var save = Add (new WF.Button { Name = "saveButton", Text = "Save", Size = new Size (100, 30) });
            var cancel = Add (new WF.Button { Name = "cancelButton", Text = "Cancel", Size = new Size (100, 30) });
            Apply ("Button { background-color: #111111; } #saveButton { background-color: #ff0000; color: #00ff00; }");

            AssertColor ("#ff0000", save.BackColor);
            AssertColor ("#00ff00", save.ForeColor);
            AssertColor ("#111111", cancel.BackColor);
            Assert.NotEqual (Color.FromArgb (255, 0, 255, 0).ToArgb (), cancel.ForeColor.ToArgb ());
        }

        [Theory]
        // an id outranks a type rule whichever is written first
        [InlineData ("#x { background-color: #ff0000; } Button { background-color: #111111; }")]
        [InlineData ("Button { background-color: #111111; } #x { background-color: #ff0000; }")]
        [InlineData ("Button#x { background-color: #ff0000; } Button { background-color: #111111; }")]
        [InlineData ("Button { background-color: #111111; } Button#x { background-color: #ff0000; }")]
        public void AnIdRule_OutranksTheTypeRule_InEitherOrder (string css)
        {
            var button = Add (new WF.Button { Name = "x", Size = new Size (100, 30) });
            Apply (css);

            AssertColor ("#ff0000", button.BackColor);
        }

        [Fact]
        public void ALaterIdRuleWinsOverAnEarlierOneOfTheSameSpecificity ()
        {
            var button = Add (new WF.Button { Name = "x", Size = new Size (100, 30) });
            Apply ("#x { background-color: #ff0000; } Button#x { background-color: #0000ff; } #x { color: #00ff00; }");

            // Button#x (1,0,1) outranks #x (1,0,0), so its colour wins though it is written between the two.
            AssertColor ("#0000ff", button.BackColor);
            AssertColor ("#00ff00", button.ForeColor);
        }

        [Fact]
        public void ATypedIdRule_ReachesOnlyThatType ()
        {
            var label = Add (new WF.Label { Name = "x", Text = "label" });
            var button = Add (new WF.Button { Name = "x", Size = new Size (100, 30) });
            Apply ("Label#x { background-color: #ff0000; }");

            AssertColor ("#ff0000", label.BackColor);
            Assert.NotEqual (Color.FromArgb (255, 255, 0, 0).ToArgb (), button.BackColor.ToArgb ());
        }

        [Fact]
        public void AnIdRuleWithNoType_ReadsTheRowsOfWhateverControlItFinds ()
        {
            var text = Add (new WF.TextBox { Name = "target" });
            var panel = Add (new WF.Panel { Name = "target", Size = new Size (50, 50) });
            var combo = Add (new WF.ComboBox { Name = "target" });
            Apply ("#target { background-color: #123456; color: #fedcba; }");

            AssertColor ("#123456", text.BackColor);
            AssertColor ("#fedcba", text.ForeColor);
            AssertColor ("#123456", panel.BackColor);
            AssertColor ("#123456", combo.BackColor);
        }

        [Fact]
        public void AnIdRule_HoldsInTheHoveredState_AndOutranksTheTypesHoverRule ()
        {
            var button = Add (new WF.Button { Name = "x", Size = new Size (100, 30) });
            Apply ("Button:hover { background-color: #222222; } #x { background-color: #00aa00; }");

            AssertColor ("#00aa00", button.BackColor);

            Raise (button, "OnMouseEnter", EventArgs.Empty);
            AssertColor ("#00aa00", button.BackColor);

            Raise (button, "OnMouseLeave", EventArgs.Empty);
            AssertColor ("#00aa00", button.BackColor);
        }

        [Fact]
        public void AnIdRulesOwnHoverRule_SwapsTheLookOnHover_AndAnotherControlKeepsTheTypes ()
        {
            var save = Add (new WF.Button { Name = "save", Size = new Size (100, 30) });
            var other = Add (new WF.Button { Name = "other", Size = new Size (100, 30) });
            Apply ("Button { background-color: #111111; } Button:hover { background-color: #222222; } #save:hover { background-color: #0000ff; }");

            Raise (save, "OnMouseEnter", EventArgs.Empty);
            Raise (other, "OnMouseEnter", EventArgs.Empty);

            AssertColor ("#0000ff", save.BackColor);
            AssertColor ("#222222", other.BackColor);

            Raise (save, "OnMouseLeave", EventArgs.Empty);
            AssertColor ("#111111", save.BackColor);
        }

        [Fact]
        public void AnExplicitAppColour_StillWinsOverAnIdRule ()
        {
            var button = Add (new WF.Button { Name = "x", Size = new Size (100, 30), BackColor = Color.Purple });
            Apply ("#x { background-color: #ff0000; }");

            Assert.Equal (Color.Purple.ToArgb (), button.BackColor.ToArgb ());
        }

        [Fact]
        public void AControlAddedLaterWithAMatchingName_IsStyledAsItArrives ()
        {
            Apply ("#late { background-color: #ff0000; }");

            var late = Add (new WF.Button { Name = "late", Size = new Size (100, 30) });
            var other = Add (new WF.Button { Name = "notLate", Size = new Size (100, 30) });

            AssertColor ("#ff0000", late.BackColor);
            Assert.NotEqual (Color.FromArgb (255, 255, 0, 0).ToArgb (), other.BackColor.ToArgb ());
        }

        [Fact]
        public void AnIdRuleIsReplacedByTheNextSheet ()
        {
            var button = Add (new WF.Button { Name = "x", Size = new Size (100, 30) });
            Apply ("#x { background-color: #ff0000; }");
            AssertColor ("#ff0000", button.BackColor);

            WinFormsCssTheme.Apply ("#x { background-color: #0000ff; }");
            AssertColor ("#0000ff", button.BackColor);

            WinFormsCssTheme.Apply ("Button { background-color: #111111; }");
            AssertColor ("#111111", button.BackColor);
        }

        [Fact]
        public void AClassRule_ReachesNoWinFormsControl_AndIsReportedAsInfo ()
        {
            var button = Add (new WF.Button { Name = "primary", Size = new Size (100, 30) });
            Apply ("Button { background-color: #111111; } Button.primary { background-color: #ff0000; } .primary { color: #00ff00; }");

            // It must not leak into the type's rules ('Button.primary' is not 'Button'), nor into a control that is merely named after the class.
            AssertColor ("#111111", button.BackColor);
            Assert.NotEqual (Color.FromArgb (255, 0, 255, 0).ToArgb (), button.ForeColor.ToArgb ());

            var reported = WinFormsCssTheme.Diagnostics.Where (d => d.Message.Contains ("styles Majorsilence.Forms controls only", StringComparison.Ordinal)).ToList ();
            Assert.Equal (2, reported.Count);
            Assert.All (reported, d => Assert.Equal (MF.ThemeCssSeverity.Info, d.Severity));
            Assert.Contains (reported, d => d.Message.Contains ("'Button.primary'", StringComparison.Ordinal));
            Assert.Contains (reported, d => d.Message.Contains ("'.primary'", StringComparison.Ordinal));
        }

        [Fact]
        public void AnIdRuleCarriesNoClassDiagnostic ()
        {
            Add (new WF.Button { Name = "x", Size = new Size (100, 30) });
            Apply ("#x { background-color: #ff0000; }");

            Assert.DoesNotContain (WinFormsCssTheme.Diagnostics, d => d.Message.Contains ("style class", StringComparison.Ordinal));
        }

        [Fact]
        public void AnIdRuleThatAsksForSomethingWinFormsCannotDo_IsReportedForTheControlItReaches ()
        {
            Add (new WF.Button { Name = "x", Size = new Size (100, 30) });
            Add (new WF.Button { Name = "other", Size = new Size (100, 30) });
            Apply ("#x { opacity: .5; } #nobody { opacity: .5; }");

            var warning = Assert.Single (WinFormsCssTheme.Diagnostics, d => d.Severity == MF.ThemeCssSeverity.Warning && d.Message.Contains ("opacity", StringComparison.Ordinal));
            Assert.Contains ("#x", warning.Message, StringComparison.Ordinal);
            Assert.Contains ("Button", warning.Message, StringComparison.Ordinal);
        }

        [Fact]
        public void TheSelectorFormsHaveRows_IdsNative_ClassesUnsupported ()
        {
            Assert.Equal (MF.ThemeCssReference.ClassAndIdForms.Select (f => f.Selector), WinFormsThemeSupport.SelectorForms.Select (f => f.Selector));

            foreach (var form in WinFormsThemeSupport.SelectorForms)
                Assert.Equal (form.Selector.Contains ('.') ? WinFormsThemeSupportLevel.Unsupported : WinFormsThemeSupportLevel.Native, form.Level);

            Assert.Contains ("### Selectors with classes and ids", WinFormsThemeSupport.ToMarkdown (), StringComparison.Ordinal);
        }
    }
}
