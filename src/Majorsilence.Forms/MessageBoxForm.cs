using System.Drawing;

namespace Majorsilence.Forms
{
    /// <summary>
    /// Represents a popup dialog used to inform the user of a message.
    /// </summary>
    public class MessageBoxForm : Form
    {
        private readonly Label label;
        private readonly Panel button_panel;

        /// <summary>
        /// Initializes a new instance of the MessageBoxForm class.
        /// </summary>
        public MessageBoxForm ()
        {
            StartPosition = FormStartPosition.CenterParent;
            AllowMinimize = false;
            AllowMaximize = false;

            // A Label, not a read-only TextBox: a TextBox has no word wrap, so it laid the message out
            // as one long line and scrolled to the end of it, cutting off the *start* of the sentence
            // ("...t find mplayer" instead of "Cannot find mplayer"). A multiline Label wraps.
            label = Controls.Add (new Label {
                Dock = DockStyle.Fill,
                Multiline = true,
                Padding = new Padding (10)
            });

            label.Style.BackgroundColor = Theme.BackgroundColor;
            label.Style.Border.Width = 0;

            button_panel = Controls.Add (new Panel {
                Dock = DockStyle.Bottom,
                Height = 45
            });

            AddButtons (MessageBoxButtons.OK);

            // Re-centre whenever the panel itself is resized. The form's own resize is too early: the
            // docked panel has not been given its final width by then, so centring against it put the
            // buttons well left of centre.
            button_panel.SizeChanged += (_, _) => CenterButtons ();
        }

        /// <summary>
        /// Initializes a new instance of the MessageBoxForm class with the specified title, message, and buttons.
        /// </summary>
        public MessageBoxForm (string title, string message, MessageBoxButtons buttons = MessageBoxButtons.OK) : this ()
        {
            Text = title;
            label.Text = message;
            AddButtons (buttons);
            CalculateDialogSize ();
        }

        /// <summary>The buttons each <see cref="MessageBoxButtons"/> set shows, in left-to-right order.</summary>
        /// <remarks>
        /// Four of the seven sets used to fall through to a lone OK button that returned
        /// <see cref="DialogResult.OK"/>: <c>YesNoCancel</c>, <c>AbortRetryIgnore</c>,
        /// <c>RetryCancel</c> and <c>CancelTryContinue</c>. A "Save changes? Yes / No / Cancel" prompt
        /// therefore offered one button and told the caller Yes — silently taking the destructive
        /// branch of a three-way decision. That is why this is a table rather than a switch: a set
        /// nobody wrote a case for is now a compile-time hole, not a wrong answer at runtime.
        /// </remarks>
        private static (string Text, DialogResult Result)[] ButtonsFor (MessageBoxButtons buttons) => buttons switch {
            MessageBoxButtons.OK => [("OK", DialogResult.OK)],
            MessageBoxButtons.OKCancel => [("OK", DialogResult.OK), ("Cancel", DialogResult.Cancel)],
            MessageBoxButtons.AbortRetryIgnore =>
                [("Abort", DialogResult.Abort), ("Retry", DialogResult.Retry), ("Ignore", DialogResult.Ignore)],
            MessageBoxButtons.YesNoCancel =>
                [("Yes", DialogResult.Yes), ("No", DialogResult.No), ("Cancel", DialogResult.Cancel)],
            MessageBoxButtons.YesNo => [("Yes", DialogResult.Yes), ("No", DialogResult.No)],
            MessageBoxButtons.RetryCancel => [("Retry", DialogResult.Retry), ("Cancel", DialogResult.Cancel)],
            MessageBoxButtons.CancelTryContinue =>
                [("Cancel", DialogResult.Cancel), ("Try Again", DialogResult.TryAgain), ("Continue", DialogResult.Continue)],
            _ => [("OK", DialogResult.OK)],
        };

        private void AddButtons (MessageBoxButtons buttons)
        {
            button_panel.Controls.Clear ();

            // Added in reading order; CenterButtons lays them out left to right in collection order, so
            // the collection order is the visual order. The previous hand-written cases added the
            // secondary button first, which put No before Yes and Cancel before OK.
            var left = 10;

            foreach (var (text, result) in ButtonsFor (buttons)) {
                var button = button_panel.Controls.Add (new Button { Text = text, Width = 80, Top = 8, Left = left });
                button.Click += (_, _) => DialogResult = result;
                left += 90;
            }
        }

        private void CalculateDialogSize ()
        {
            var num_lines = label?.Text?.Count (c => c == '\n') ?? 0;

            Size = num_lines > 10 ? new Size (800, 400)
                 : num_lines > 4  ? new Size (600, 300)
                 :                   new Size (400, 200);

            CenterButtons ();
        }

        // Centres the buttons in their panel.
        //
        // Called on every resize, not once at construction. The dialog is built before it has a window,
        // so the docked children are laid out against a client area that does not exist yet -- the Fill
        // label comes out 0 x -45 and the Bottom panel sits above the top edge. That is corrected by the
        // layout pass the backend runs when the window appears, and positions computed before it are
        // simply wrong: measured against a zero-width panel the buttons landed at negative X, off the
        // left edge, so the message box showed its text and no buttons at all.
        private void CenterButtons ()
        {
            // Buttons only: GetAllControls also yields the panel's implicit chrome -- its scrollbars --
            // which were being counted into the total width and, worse, repositioned along with the
            // buttons. That is what left them sitting well left of centre.
            var buttons = button_panel.Controls.GetAllControls ().OfType<Button> ().ToList ();
            if (buttons.Count == 0 || button_panel.Width <= 0)
                return;

            var totalW = buttons.Sum (c => c.Width + 10) - 10;
            var startX = Math.Max (0, (button_panel.Width - totalW) / 2);

            foreach (var btn in buttons) {
                btn.Left = startX;
                startX += btn.Width + 10;
            }
        }


        /// <inheritdoc/>
        protected override Size DefaultSize => new Size (400, 200);

        /// <summary>
        /// Gets or sets the message of the dialog.
        /// </summary>
        public string Message {
            get => label.Text;
            set {
                if (label.Text != value) {
                    label.Text = value;
                    CalculateDialogSize ();
                }
            }
        }
    }
}
