using Majorsilence.Forms;

namespace MajorsilenceFormsApp
{
    partial class MainForm
    {
        private Label helloLabel = null!;

        private void InitializeComponent ()
        {
            Text = "Majorsilence.Forms App";
            ClientSize = new System.Drawing.Size (400, 300);

            helloLabel = Controls.Add (new Label {
                AutoSize = true,
                Location = new System.Drawing.Point (24, 24),
                Text = "Hello, Majorsilence.Forms!",
            });
        }
    }
}
