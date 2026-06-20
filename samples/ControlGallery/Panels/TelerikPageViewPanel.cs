using Continuum.Forms;
using Continuum.Forms.Telerik;

namespace ControlGallery.Panels
{
    // Showcases RadPageView/RadPageViewPage (Continuum.Forms.Telerik) — backed by Continuum.Forms.TabControl.
    public class TelerikPageViewPanel : BasePanel
    {
        public TelerikPageViewPanel ()
        {
            Controls.Add (new Label {
                Text = "RadPageView with RadPageViewPages — backed by Continuum.Forms.TabControl.",
                Left = 10, Top = 10, Width = 760
            });

            var pageView = new RadPageView {
                Left = 10, Top = 36, Width = 600, Height = 300
            };

            var generalPage = new RadPageViewPage { Text = "General" };
            generalPage.Controls.Add (new RadLabel { Text = "This page is a RadPageViewPage.", Left = 20, Top = 20, Width = 400 });
            generalPage.Controls.Add (new RadButton { Text = "A RadButton on a page", Left = 20, Top = 56, Width = 200 });

            var detailsPage = new RadPageViewPage { Text = "Details" };
            detailsPage.Controls.Add (new RadLabel { Text = "RadTextBox on the Details page:", Left = 20, Top = 20, Width = 260 });
            detailsPage.Controls.Add (new RadTextBox { Text = "Edit me", Left = 20, Top = 50, Width = 240 });

            var optionsPage = new RadPageViewPage { Text = "Options" };
            optionsPage.Controls.Add (new RadCheckBox { Text = "Enable feature", Left = 20, Top = 20, Width = 200 });
            optionsPage.Controls.Add (new RadToggleSwitch { OnText = "On", OffText = "Off", Left = 20, Top = 52, Width = 120, Height = 28 });

            pageView.Pages.Add (generalPage);
            pageView.Pages.Add (detailsPage);
            pageView.Pages.Add (optionsPage);

            Controls.Add (pageView);
        }
    }
}
