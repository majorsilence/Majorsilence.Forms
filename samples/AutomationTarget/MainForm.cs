using System;
using System.Drawing;
using Majorsilence.Forms;

namespace AutomationTarget
{
    /// <summary>
    /// A deliberately small form whose controls each demonstrate one thing an automation client has to
    /// cope with: reading and writing text, a click that changes state, a control that refuses to be
    /// clicked, and a control that only becomes enabled once something else happens.
    ///
    /// Every control that a client should target has a <c>Name</c>, because that is what becomes the
    /// <c>id</c> in the automation tree. The one unnamed control is deliberate too — see instructions.
    /// </summary>
    public class MainForm : Form
    {
        private readonly Label greetingLabel;
        private readonly TextBox nameBox;
        private readonly CheckBox agreeCheck;
        private readonly Button submitButton;
        private readonly Label lastActionLabel;
        private readonly ListBox logList;

        public MainForm ()
        {
            Text = "Automation target";
            ClientSize = new Size (460, 380);

            // Not named on purpose: it shows up in the tree with an empty id, which is what "name your
            // controls" is about. Locating this one needs XPath or its text.
            var instructions = new Label {
                Text = "Point an automation client at this window. Every other control has a Name.",
                Left = 12, Top = 12, Width = 436, Height = 34
            };

            greetingLabel = new Label {
                Name = "greetingLabel", Text = "Who are you?",
                Left = 12, Top = 54, Width = 436, Height = 22
            };

            nameBox = new TextBox {
                Name = "nameBox", AccessibleName = "Full name",
                Left = 12, Top = 82, Width = 436, Height = 28
            };

            var greetButton = new Button {
                Name = "greetButton", Text = "Greet",
                Left = 12, Top = 122, Width = 100, Height = 30
            };

            var clearButton = new Button {
                Name = "clearButton", Text = "Clear",
                Left = 122, Top = 122, Width = 100, Height = 30
            };

            // Never enabled: a client should report that it refused to click, not pretend it did.
            var lockedButton = new Button {
                Name = "lockedButton", Text = "Locked",
                Left = 232, Top = 122, Width = 100, Height = 30, Enabled = false
            };

            agreeCheck = new CheckBox {
                Name = "agreeCheck", Text = "I agree to be automated",
                Left = 12, Top = 166, Width = 300, Height = 24
            };

            // Enabled only once the box is ticked, so a client has something real to wait for rather than
            // sleeping and hoping.
            submitButton = new Button {
                Name = "submitButton", Text = "Submit",
                Left = 12, Top = 198, Width = 100, Height = 30, Enabled = false
            };

            // The list's items are in the automation tree (each one a listitem node with its own bounds), so
            // a client can read the history directly. This label stays because the newest entry is what most
            // assertions actually want, and one stable target beats scanning children for it.
            lastActionLabel = new Label {
                Name = "lastActionLabel", Text = "nothing yet",
                Left = 12, Top = 236, Width = 436, Height = 22
            };

            logList = new ListBox {
                Name = "logList",
                Left = 12, Top = 264, Width = 436, Height = 102
            };

            greetButton.Click += (sender, e) => {
                greetingLabel.Text = string.IsNullOrWhiteSpace (nameBox.Text)
                    ? "Who are you?"
                    : $"Hello, {nameBox.Text}!";
                Log ($"greet: '{nameBox.Text}'");
            };

            clearButton.Click += (sender, e) => {
                nameBox.Text = string.Empty;
                greetingLabel.Text = "Who are you?";
                Log ("clear");
            };

            agreeCheck.CheckedChanged += (sender, e) => {
                submitButton.Enabled = agreeCheck.Checked;
                Log ($"agree: {agreeCheck.Checked}");
            };

            submitButton.Click += (sender, e) => Log ($"submit: '{nameBox.Text}'");

            Controls.Add (instructions);
            Controls.Add (greetingLabel);
            Controls.Add (nameBox);
            Controls.Add (greetButton);
            Controls.Add (clearButton);
            Controls.Add (lockedButton);
            Controls.Add (agreeCheck);
            Controls.Add (submitButton);
            Controls.Add (lastActionLabel);
            Controls.Add (logList);
        }

        // A visible record of what the app itself saw, so a client can verify its actions reached the
        // real event handlers instead of trusting its own "ok".
        private void Log (string entry)
        {
            lastActionLabel.Text = entry;
            logList.Items.Add ($"{DateTime.Now:HH:mm:ss}  {entry}");
            Console.WriteLine (entry);
        }
    }
}
