using System.Drawing;
using Majorsilence.Forms;

namespace ControlGallery.Panels;

// A StackPanel with the properties that shape it on a control strip above, so the effect of each is visible at once.
public class StackPanelPanel : Panel
{
    private readonly StackPanel demo;
    private readonly Control saveButton;

    public StackPanelPanel ()
    {
        // The Fill panel goes in first: docking is applied from the last-added control backwards, so the strip claims its edge before
        // Fill takes what is left.
        demo = Controls.Add (new StackPanel { Dock = DockStyle.Fill, AutoScroll = true, Spacing = 8, Padding = new Padding (12), MaximumContentWidth = 480 });
        var strip = Controls.Add (new StackPanel { Dock = DockStyle.Top, Orientation = Orientation.Horizontal, Spacing = 8, Padding = new Padding (8), Height = 44 });

        var horizontal = new CheckBox { Text = "Horizontal", AutoSize = true };
        var spacing = new NumericUpDown { Minimum = 0, Maximum = 40, Value = 8, Width = 60 };
        var maxWidth = new NumericUpDown { Minimum = 0, Maximum = 2000, Increment = 40, Value = 480, Width = 80 };
        var content = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Width = 90 };
        var children = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Width = 90 };
        var endButton = new CheckBox { Text = "Save at the end", AutoSize = true };

        foreach (var name in Enum.GetNames<StackAlignment> ()) {
            content.Items.Add (name);
            children.Items.Add (name);
        }

        content.SelectedItem = nameof (StackAlignment.Center);
        children.SelectedItem = nameof (StackAlignment.Stretch);

        strip.Controls.Add (horizontal);
        strip.Controls.Add (new Label { Text = "Spacing", AutoSize = true });
        strip.Controls.Add (spacing);
        strip.Controls.Add (new Label { Text = "Max width (0 = none)", AutoSize = true });
        strip.Controls.Add (maxWidth);
        strip.Controls.Add (new Label { Text = "Column", AutoSize = true });
        strip.Controls.Add (content);
        strip.Controls.Add (new Label { Text = "Children", AutoSize = true });
        strip.Controls.Add (children);
        strip.Controls.Add (endButton);

        BuildRows ();
        saveButton = demo.Controls[^1];

        horizontal.CheckedChanged += (_, _) => demo.Orientation = horizontal.Checked ? Orientation.Horizontal : Orientation.Vertical;
        spacing.ValueChanged += (_, _) => demo.Spacing = (int)spacing.Value;
        maxWidth.ValueChanged += (_, _) => demo.MaximumContentWidth = (int)maxWidth.Value;
        content.SelectedIndexChanged += (_, _) => demo.ContentAlignment = Enum.Parse<StackAlignment> ((string)content.SelectedItem!);
        children.SelectedIndexChanged += (_, _) => demo.ChildAlignment = Enum.Parse<StackAlignment> ((string)children.SelectedItem!);
        endButton.CheckedChanged += (_, _) => {
            if (endButton.Checked)
                demo.SetAlignment (saveButton, StackAlignment.End);
            else
                demo.SetAlignment (saveButton, demo.ChildAlignment);
        };
    }

    private void BuildRows ()
    {
        demo.Controls.Add (new Label { Text = "Server address", AutoSize = true });
        demo.Controls.Add (new TextBox { Height = 32 });
        demo.Controls.Add (new Label {
            Text = "A caption long enough to wrap onto a second line when the column is narrow: each label is asked for its height at the width it will get.",
            AutoSize = true,
        });
        demo.Controls.Add (new TextBox { Height = 32 });
        demo.Controls.Add (new Label { Text = "Sign in", AutoSize = true });

        var combo = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Height = 32 };
        combo.Items.Add ("No sign-in");
        combo.Items.Add ("Username and password");
        combo.SelectedIndex = 0;
        demo.Controls.Add (combo);

        demo.Controls.Add (new CheckBox { Text = "Play sounds", AutoSize = true });
        for (var i = 1; i <= 10; i++)
            demo.Controls.Add (new Label { Text = $"More rows make the column scroll ({i} of 10)", AutoSize = true });

        demo.Controls.Add (new Button { Text = "Save", Size = new Size (120, 40) });
    }
}
