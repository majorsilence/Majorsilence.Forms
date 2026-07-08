using System;
using Microsoft.VisualBasic;

namespace Majorsilence.Forms
{
    /// <summary>
    /// Cross-platform stand-ins for classic VB's <c>Microsoft.VisualBasic.Interaction.MsgBox</c> and
    /// <c>InputBox</c> (the same family as <see cref="ComputerInfo"/>). VB's own MsgBox/InputBox
    /// late-bind into <c>System.Windows.Forms</c> and throw <see cref="PlatformNotSupportedException"/>
    /// ("Method requires System.Windows.Forms") off Windows, so migrated code that keeps calling
    /// <c>MsgBox(...)</c>/<c>InputBox(...)</c> breaks at runtime. The migrator rewrites those bare calls to
    /// <c>Majorsilence.Forms.Interaction.MsgBox(...)</c>/<c>.InputBox(...)</c>, which render with
    /// Majorsilence.Forms instead.
    ///
    /// The signatures keep VB's <see cref="MsgBoxStyle"/>/<see cref="MsgBoxResult"/> (both live in the
    /// cross-platform Microsoft.VisualBasic.Core assembly — only the FUNCTION is Windows-bound), so
    /// migrated comparison sites like <c>If MsgBox(...) = MsgBoxResult.Yes</c> compile and behave
    /// unchanged. Their numeric values line up exactly with <see cref="MessageBoxButtons"/> (low nibble),
    /// <see cref="MessageBoxIcon"/> (icon bits) and <see cref="DialogResult"/> (== MsgBoxResult), so the
    /// mapping is a straight numeric cast.
    /// </summary>
    public static class Interaction
    {
        /// <summary>
        /// Displays a message in a dialog box and returns which button the user pressed.
        /// Cross-platform replacement for <c>Microsoft.VisualBasic.Interaction.MsgBox</c>.
        /// </summary>
        public static MsgBoxResult MsgBox (object? prompt, MsgBoxStyle buttons = MsgBoxStyle.OkOnly, object? title = null)
        {
            var text = prompt?.ToString () ?? string.Empty;
            var caption = title?.ToString () ?? string.Empty;
            var button = (MessageBoxButtons) ((int) buttons & 0xF);
            var icon = (MessageBoxIcon) ((int) buttons & 0xF0);

            var result = MessageBox.Show (text, caption, button, icon);
            return (MsgBoxResult) (int) result;
        }

        /// <summary>
        /// Prompts the user for a single line of text and returns it, or an empty string if cancelled.
        /// Cross-platform replacement for <c>Microsoft.VisualBasic.Interaction.InputBox</c>. The
        /// <paramref name="xPos"/>/<paramref name="yPos"/> parameters are accepted for signature
        /// compatibility; the dialog is centered rather than positioned.
        /// </summary>
        public static string InputBox (string prompt, string title = "", string defaultResponse = "", int xPos = -1, int yPos = -1)
            => InputBoxDialog.ShowInput (prompt, title, defaultResponse);

        // A minimal single-line prompt dialog; the fork has no InputBox of its own.
        private sealed class InputBoxDialog : Form
        {
            private readonly TextBox _textBox;

            private InputBoxDialog (string prompt, string title, string defaultResponse)
            {
                Text = title;
                Width = 420;
                Height = 180;

                var promptLabel = new Label { Text = prompt, Left = 12, Top = 12, Width = 396, Height = 54 };
                _textBox = new TextBox { Left = 12, Top = 72, Width = 396, Text = defaultResponse };
                var okButton = new Button { Text = "OK", Left = 232, Top = 108, Width = 84, DialogResult = DialogResult.OK };
                var cancelButton = new Button { Text = "Cancel", Left = 324, Top = 108, Width = 84, DialogResult = DialogResult.Cancel };

                Controls.Add (promptLabel);
                Controls.Add (_textBox);
                Controls.Add (okButton);
                Controls.Add (cancelButton);

                AcceptButton = okButton;
                CancelButton = cancelButton;
            }

            public static string ShowInput (string prompt, string title, string defaultResponse)
            {
                using var dialog = new InputBoxDialog (prompt, title, defaultResponse);
                return dialog.ShowDialog () == DialogResult.OK ? dialog._textBox.Text : string.Empty;
            }
        }
    }
}
