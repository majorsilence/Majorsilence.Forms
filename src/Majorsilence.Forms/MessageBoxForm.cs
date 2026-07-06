using System.Drawing;

namespace Majorsilence.Forms
{
    /// <summary>
    /// Represents a popup dialog used to inform the user of a message.
    /// </summary>
    public class MessageBoxForm : Form
    {
        private readonly TextBox label;
        private readonly Panel button_panel;

        /// <summary>
        /// Initializes a new instance of the MessageBoxForm class.
        /// </summary>
        public MessageBoxForm ()
        {
            StartPosition = FormStartPosition.CenterParent;
            AllowMinimize = false;
            AllowMaximize = false;

            label = Controls.Add (new TextBox {
                Dock = DockStyle.Fill,
                Multiline = true,
                ReadOnly = true,
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

            // Center buttons horizontally.
            var totalW = button_panel.Controls.GetAllControls ().Sum (c => c.Width + 10);
            var startX = (Size.Width - totalW) / 2;

            foreach (var btn in button_panel.Controls.GetAllControls ()) {
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
