using Majorsilence.Forms;
using Majorsilence.Forms.Telerik;

namespace ControlGallery.Panels
{
    // Showcases RadSpellChecker (Majorsilence.Forms.Telerik) attached to a plain Majorsilence.Forms.TextBox
    // via AutoSpellCheckControl. The text box is pre-filled with a few intentional typos so the wavy-underline
    // squiggle rendering is visible immediately; right-click a misspelled word for the suggestions /
    // "Add to Dictionary" context menu (backed by Majorsilence.Forms.SpellCheck).
    public class TelerikSpellCheckPanel : BasePanel
    {
        private readonly RadSpellChecker checker = new ();
        private readonly TextBox textBox;
        private readonly Label status;

        private const string SampleText =
            "This sentance has a fewe intentional typos so you can seee the squiggle rendering.\r\n" +
            "Right-click a misspelled wrod for suggestions or to add it to the dictionary.";

        public TelerikSpellCheckPanel ()
        {
            Controls.Add (new Label {
                Text = "RadSpellChecker.AutoSpellCheckControl attached to a TextBox — misspelled words (sentance, fewe, seee, wrod) render with a wavy underline.",
                Left = 10, Top = 10, Width = 780, Height = 34
            });

            textBox = new TextBox {
                Left = 10, Top = 54, Width = 780, Height = 160,
                Multiline = true,
                Text = SampleText,
            };
            Controls.Add (textBox);

            checker.AutoSpellCheckControl = textBox;

            status = new Label {
                Text = $"Last action: attached RadSpellChecker (SpellCheckMode: {checker.SpellCheckMode})",
                Left = 10, Top = 222, Width = 780
            };
            Controls.Add (status);

            var detachButton = new Button { Text = "Detach Spell Checker", Left = 10, Top = 250, Width = 180, Height = 30 };
            detachButton.Click += (_, _) => {
                checker.AutoSpellCheckControl = null;
                Report ("Detached — squiggles cleared");
            };
            Controls.Add (detachButton);

            var reattachButton = new Button { Text = "Attach Spell Checker", Left = 200, Top = 250, Width = 180, Height = 30 };
            reattachButton.Click += (_, _) => {
                checker.AutoSpellCheckControl = textBox;
                Report ("Attached to TextBox");
            };
            Controls.Add (reattachButton);

            var resetButton = new Button { Text = "Reset Sample Text", Left = 390, Top = 250, Width = 160, Height = 30 };
            resetButton.Click += (_, _) => {
                textBox.Text = SampleText;
                Report ("Reset sample text (with typos)");
            };
            Controls.Add (resetButton);
        }

        private void Report (string action) => status.Text = $"Last action: {action}";

        public override void UnloadPanel ()
        {
            checker.AutoSpellCheckControl = null;
        }

        public override void LoadPanel ()
        {
            checker.AutoSpellCheckControl = textBox;
        }
    }
}
