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

        private void AddButtons (MessageBoxButtons buttons)
        {
            button_panel.Controls.Clear ();

            switch (buttons) {
                case MessageBoxButtons.YesNo: {
                    var no = button_panel.Controls.Add (new Button { Text = "No", Width = 80, Top = 8, Left = 10 });
                    no.Click += (_, _) => DialogResult = DialogResult.No;

                    var yes = button_panel.Controls.Add (new Button { Text = "Yes", Width = 80, Top = 8, Left = 100 });
                    yes.Click += (_, _) => DialogResult = DialogResult.Yes;
                    break;
                }
                case MessageBoxButtons.OKCancel: {
                    var cancel = button_panel.Controls.Add (new Button { Text = "Cancel", Width = 80, Top = 8, Left = 10 });
                    cancel.Click += (_, _) => DialogResult = DialogResult.Cancel;

                    var ok = button_panel.Controls.Add (new Button { Text = "OK", Width = 80, Top = 8, Left = 100 });
                    ok.Click += (_, _) => DialogResult = DialogResult.OK;
                    break;
                }
                default: {
                    var ok = button_panel.Controls.Add (new Button { Text = "OK", Width = 80, Top = 8, Left = 10 });
                    ok.Click += (_, _) => DialogResult = DialogResult.OK;
                    break;
                }
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
            var buttons = button_panel.Controls.GetAllControls ().ToList ();
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
        protected override void OnResize (EventArgs e)
        {
            base.OnResize (e);
            CenterButtons ();
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
