using System;
using System.Windows.Forms;

namespace WinFormsCompatDemo
{
    public partial class Form1 : Form
    {
        public Form1()
        {
            InitializeComponent();

            // Exercises the event-shadowing pass on the Form itself: MouseDown/KeyDown subscriptions
            // through compat EventArgs types (KeyPreview so child controls' keystrokes reach here too)
            // -- see RESULTS.md's "event shadowing" section.
            KeyPreview = true;
            MouseDown += (sender, e) => label1.Text = $"Mouse {e.Button} at {e.X},{e.Y}";
            KeyDown += (sender, e) =>
            {
                label1.Text = $"Key {e.KeyCode}";
                e.Handled = true;
            };

            // BinaryRainPanel.cs: a reimplementation of ControlGallery's Binary Rain example
            // (samples/ControlGallery/Panels/BinaryRainPanel.cs) through unmodified `using
            // System.Windows.Forms;` source -- a Timer-driven animation, a TrackBar/Button wired to
            // it, and a custom Control overriding the compat-typed OnPaint hook with a real per-frame
            // drawing workload, rather than the simple paint-counter this used to be.
            Controls.Add(new BinaryRainPanel
            {
                Location = new System.Drawing.Point(20, 140),
                Size = new System.Drawing.Size(660, 460),
            });
        }

        private void button1_Click(object sender, EventArgs e)
        {
            label1.Text = "Clicked: " + textBox1.Text;

            // Exercises the static-utility-class forwarding (Application, MessageBox) added
            // alongside the original Component-subclass generator -- see RESULTS.md. Every line
            // below compiles and runs against unmodified `using System.Windows.Forms;` source,
            // with zero changes, which is the whole point.
            // Fully qualified because Form.DialogResult is an instance property of the same name --
            // bare `DialogResult.OK` here is ambiguous, exactly as in real WinForms code (this is why
            // Visual Studio's designer always fully-qualifies it too).
            var answer = MessageBox.Show(this, "Close the demo?", "WinFormsCompatDemo", MessageBoxButtons.OKCancel, MessageBoxIcon.Question);
            if (answer == System.Windows.Forms.DialogResult.OK)
                Application.Exit();
        }

        private void textBox1_TextChanged(object sender, EventArgs e)
        {
            button1.Enabled = textBox1.Text.Length > 0;
        }
    }
}
