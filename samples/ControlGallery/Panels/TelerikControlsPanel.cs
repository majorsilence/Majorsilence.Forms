using Continuum.Forms;
using Continuum.Forms.Telerik;

namespace ControlGallery.Panels
{
    // Showcases the Telerik (Rad*) compatibility controls from the Continuum.Forms.Telerik namespace.
    // Migrated WinForms+Telerik code uses these by swapping `Imports Telerik.WinControls.*`
    // for `Imports Continuum.Forms.Telerik`; each is backed by a native Continuum.Forms control.
    public class TelerikControlsPanel : BasePanel
    {
        public TelerikControlsPanel ()
        {
            Controls.Add (new Label {
                Text = "Telerik compatibility controls (Continuum.Forms.Telerik) — backed by native Continuum.Forms controls.",
                Left = 20, Top = 16, Width = 720
            });

            var status = Controls.Add (new Label {
                Text = "Last action: (none)",
                Left = 20, Top = 44, Width = 720
            });

            void Report (string action) => status.Text = $"Last action: {action}";

            // RadGroupBox hosting the basic input controls.
            var group = new RadGroupBox {
                HeaderText = "Input controls",
                Left = 20, Top = 80, Width = 360, Height = 320
            };

            group.Controls.Add (new RadLabel { Text = "RadLabel:", Left = 16, Top = 36, Width = 120 });

            var textBox = new RadTextBox { Text = "RadTextBox", Left = 140, Top = 32, Width = 190 };
            textBox.TextChanged += (o, e) => Report ($"RadTextBox = \"{textBox.Text}\"");
            group.Controls.Add (textBox);

            var dropDown = new RadDropDownList { Left = 140, Top = 70, Width = 190 };
            dropDown.Items.Add ("First");
            dropDown.Items.Add ("Second");
            dropDown.Items.Add ("Third");
            dropDown.SelectedIndexChanged += (o, e) => Report ($"RadDropDownList → {dropDown.SelectedItem}");
            group.Controls.Add (new RadLabel { Text = "RadDropDownList:", Left = 16, Top = 74, Width = 120 });
            group.Controls.Add (dropDown);

            var check = new RadCheckBox { Text = "RadCheckBox", Left = 16, Top = 110, Width = 200 };
            check.ToggleStateChanged += (o, e) => Report ($"RadCheckBox.IsChecked = {check.IsChecked}");
            group.Controls.Add (check);

            var radioA = new RadRadioButton { Text = "RadRadioButton A", Left = 16, Top = 144, Width = 200 };
            var radioB = new RadRadioButton { Text = "RadRadioButton B", Left = 16, Top = 174, Width = 200 };
            radioA.ToggleStateChanged += (o, e) => { if (radioA.IsChecked) Report ("RadRadioButton A selected"); };
            radioB.ToggleStateChanged += (o, e) => { if (radioB.IsChecked) Report ("RadRadioButton B selected"); };
            group.Controls.Add (radioA);
            group.Controls.Add (radioB);

            var toggle = new RadToggleSwitch { OnText = "On", OffText = "Off", Left = 16, Top = 210, Width = 120, Height = 28 };
            toggle.ValueChanged += (o, e) => Report ($"RadToggleSwitch.Value = {toggle.Value}");
            group.Controls.Add (new RadLabel { Text = "RadToggleSwitch:", Left = 150, Top = 214, Width = 180 });
            group.Controls.Add (toggle);

            var button = new RadButton { Text = "RadButton", Left = 16, Top = 256, Width = 120 };
            button.Click += (o, e) => Report ("RadButton clicked");
            group.Controls.Add (button);

            Controls.Add (group);

            // RadWaitingBar demo on the right.
            Controls.Add (new RadLabel { Text = "RadWaitingBar:", Left = 410, Top = 92, Width = 200 });

            var waitingBar = new RadWaitingBar { Left = 410, Top = 120, Width = 320, Height = 24 };
            Controls.Add (waitingBar);

            var startStop = new RadButton { Text = "Start", Left = 410, Top = 156, Width = 100 };
            startStop.Click += (o, e) => {
                if (waitingBar.IsWaiting) {
                    waitingBar.StopWaiting ();
                    startStop.Text = "Start";
                    Report ("RadWaitingBar stopped");
                } else {
                    waitingBar.StartWaiting ();
                    startStop.Text = "Stop";
                    Report ("RadWaitingBar started");
                }
            };
            Controls.Add (startStop);

            // RadCheckedDropDownList (multi-select) demo.
            Controls.Add (new RadLabel { Text = "RadCheckedDropDownList:", Left = 410, Top = 210, Width = 220 });

            var checkedDropDown = new RadCheckedDropDownList { Left = 410, Top = 238, Width = 320 };
            checkedDropDown.Items.Add ("Apples");
            checkedDropDown.Items.Add ("Oranges");
            checkedDropDown.Items.Add ("Bananas");
            Controls.Add (checkedDropDown);
        }
    }
}
