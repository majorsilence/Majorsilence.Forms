using System;
using System.Windows.Forms;

namespace WinFormsCompatDemo
{
    public partial class Form1 : Form
    {
        public Form1()
        {
            InitializeComponent();
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
