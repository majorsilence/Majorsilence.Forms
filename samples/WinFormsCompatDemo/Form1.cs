using System;
using System.Windows.Forms;

namespace WinFormsCompatDemo
{
    public partial class Form1 : Form
    {
        public Form1()
        {
            InitializeComponent();

            // Exercises the event-shadowing pass: MouseDown/KeyDown subscriptions through compat
            // EventArgs types, and a Paint subscription alongside PaintDemoPanel's OnPaint override --
            // see RESULTS.md's "event shadowing" section. Added here rather than in
            // Form1.Designer.cs since InitializeComponent is designer-owned.
            var paintPanel = new PaintDemoPanel
            {
                Location = new System.Drawing.Point(20, 140),
                Size = new System.Drawing.Size(244, 80),
                TabStop = true,
            };
            paintPanel.MouseDown += (sender, e) => label1.Text = $"Mouse {e.Button} at {e.X},{e.Y}";
            paintPanel.KeyDown += (sender, e) =>
            {
                label1.Text = $"Key {e.KeyCode}";
                e.Handled = true;
            };
            paintPanel.Paint += (sender, e) => label1.Text = $"Painted {paintPanel.PaintCount} time(s)";
            Controls.Add(paintPanel);
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
