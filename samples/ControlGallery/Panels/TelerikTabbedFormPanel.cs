using System.Drawing;
using Majorsilence.Forms;
using Majorsilence.Forms.Telerik;

namespace ControlGallery.Panels
{
    // Showcases RadTabbedForm (Majorsilence.Forms.Telerik) — a form whose document tabs are
    // managed through TabbedFormControl.Items, backed by Majorsilence.Forms.TabControl.
    // RadTabbedForm is a Form, so it is opened in its own window (shown modeless so the tear-off
    // windows can live alongside it). On custom-chrome platforms (Windows/Linux) the tabs render in
    // the title bar with drag-to-reorder, tear-off and re-attach; on macOS native chrome the tabs
    // fall back to a docked strip below the OS title bar.
    public class TelerikTabbedFormPanel : BasePanel
    {
        public TelerikTabbedFormPanel ()
        {
            Controls.Add (new Label {
                Text = "RadTabbedForm hosts document tabs in the title bar (custom-chrome platforms): drag to reorder, drag a tab out to tear it into a new window, or drop it on another tabbed form's title bar to re-attach.",
                Left = 10, Top = 10, Width = 780, Height = 40
            });

            var open = new RadButton {
                Text = "Open RadTabbedForm", Left = 10, Top = 58, Width = 200
            };

            open.Click += (_, _) => BuildTabbedForm ().Show ();

            Controls.Add (open);
        }

        private static RadTabbedForm BuildTabbedForm ()
        {
            var form = new RadTabbedForm {
                Text = "RadTabbedForm",
                Size = new Size (560, 360)
            };

            var general = form.TabbedFormControl.Items.Add ("General");
            general.ContentPanel.Controls.Add (new RadLabel {
                Text = "Controls dropped on a tab live in its ContentPanel.", Left = 20, Top = 20, Width = 420
            });
            general.ContentPanel.Controls.Add (new RadButton {
                Text = "A RadButton on the General tab", Left = 20, Top = 56, Width = 240
            });

            var details = form.TabbedFormControl.Items.Add ("Details");
            details.ContentPanel.Controls.Add (new RadLabel {
                Text = "RadTextBox on the Details tab:", Left = 20, Top = 20, Width = 260
            });
            details.ContentPanel.Controls.Add (new RadTextBox {
                Text = "Edit me", Left = 20, Top = 50, Width = 240
            });

            var options = form.TabbedFormControl.Items.Add ("Options");
            options.ContentPanel.Controls.Add (new RadCheckBox {
                Text = "Enable feature", Left = 20, Top = 20, Width = 200
            });

            // Select the first tab and react to tab changes.
            form.TabbedFormControl.SelectedTab = general;
            form.TabbedFormControl.SelectedTabChanged += (_, _) => {
                form.Text = $"RadTabbedForm — {form.TabbedFormControl.SelectedTab?.Text}";
            };

            return form;
        }
    }
}
