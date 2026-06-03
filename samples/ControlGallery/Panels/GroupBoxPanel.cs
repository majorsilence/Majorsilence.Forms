using Modern.Forms;

namespace ControlGallery.Panels
{
    public class GroupBoxPanel : Panel
    {
        public GroupBoxPanel ()
        {
            // Basic group box with a mix of child controls
            var basicGroup = Controls.Add (new GroupBox {
                Text = "Personal Details",
                Left = 20,
                Top = 20,
                Width = 300,
                Height = 150
            });

            basicGroup.Controls.Add (new Label { Text = "First name:", Left = 10, Top = 22, Width = 80 });
            basicGroup.Controls.Add (new TextBox { Left = 95, Top = 20, Width = 185, Placeholder = "Enter first name" });

            basicGroup.Controls.Add (new Label { Text = "Last name:", Left = 10, Top = 57, Width = 80 });
            basicGroup.Controls.Add (new TextBox { Left = 95, Top = 55, Width = 185, Placeholder = "Enter last name" });

            basicGroup.Controls.Add (new CheckBox { Text = "Active user", Left = 10, Top = 92, Width = 130 });
            basicGroup.Controls.Add (new CheckBox { Text = "Administrator", Left = 150, Top = 92, Width = 130 });

            // Group box with radio buttons
            var radioGroup = Controls.Add (new GroupBox {
                Text = "Notification Preference",
                Left = 340,
                Top = 20,
                Width = 250,
                Height = 150
            });

            radioGroup.Controls.Add (new RadioButton { Text = "Email",         Left = 10, Top = 22, Width = 200 });
            radioGroup.Controls.Add (new RadioButton { Text = "SMS",           Left = 10, Top = 52, Width = 200 });
            radioGroup.Controls.Add (new RadioButton { Text = "Push notification", Left = 10, Top = 82, Width = 200 });
            radioGroup.Controls.Add (new RadioButton { Text = "None",          Left = 10, Top = 112, Width = 200 });

            // Nested group boxes
            var outerGroup = Controls.Add (new GroupBox {
                Text = "Outer Group",
                Left = 20,
                Top = 195,
                Width = 300,
                Height = 180
            });

            var innerGroup = new GroupBox {
                Text = "Inner Group",
                Left = 15,
                Top = 22,
                Width = 260,
                Height = 120
            };

            innerGroup.Controls.Add (new Label  { Text = "Nested controls work correctly", Left = 10, Top = 25, Width = 240 });
            innerGroup.Controls.Add (new Button { Text = "Click me",                        Left = 10, Top = 60, Width = 100 });
            outerGroup.Controls.Add (innerGroup);

            // Empty / title-only group box
            Controls.Add (new GroupBox {
                Text = "Empty Group (placeholder)",
                Left = 340,
                Top = 195,
                Width = 250,
                Height = 80
            });
        }
    }
}
