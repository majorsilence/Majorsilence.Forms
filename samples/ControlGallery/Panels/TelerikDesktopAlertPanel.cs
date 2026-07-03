using System.Drawing;
using Majorsilence.Forms;
using Majorsilence.Forms.Telerik;

namespace ControlGallery.Panels
{
    // Showcases RadDesktopAlert (Majorsilence.Forms.Telerik) — a borderless, always-on-top toast positioned
    // at the bottom-right of the primary screen. Each button below creates and shows an alert with a
    // different caption/content/AutoCloseDelay; showing several at once demonstrates stacking (each new
    // alert appears above the ones already on screen) and auto-close after the configured delay.
    public class TelerikDesktopAlertPanel : BasePanel
    {
        private readonly System.Collections.Generic.List<RadDesktopAlert> active = new ();
        private readonly Label status;

        public TelerikDesktopAlertPanel ()
        {
            Controls.Add (new Label {
                Text = "RadDesktopAlert — toasts appear bottom-right of the primary screen and stack above each other. Try firing more than one to see the stacking.",
                Left = 10, Top = 10, Width = 780, Height = 34
            });

            status = new Label { Text = "Last action: (none)", Left = 10, Top = 48, Width = 780 };
            Controls.Add (status);

            AddButton ("Show Info (3s auto-close)", 10, 80, () =>
                ShowAlert ("Info", "This is an informational alert that closes automatically after 3 seconds.", 3000, Color.Empty));

            AddButton ("Show Warning (8s auto-close)", 250, 80, () =>
                ShowAlert ("Warning", "Something needs your attention soon.", 8000, Color.Orange));

            AddButton ("Show Error (no auto-close)", 490, 80, () =>
                ShowAlert ("Error", "Something went wrong. This alert stays until you close it.", 0, Color.Red));

            AddButton ("Show 3 Stacked Alerts", 10, 120, () => {
                ShowAlert ("Alert 1", "First alert (bottom of the stack).", 15000, Color.Empty);
                ShowAlert ("Alert 2", "Second alert (stacks above the first).", 15000, Color.Empty);
                ShowAlert ("Alert 3", "Third alert (stacks above the second).", 15000, Color.Empty);
                Report ("Fired 3 stacked alerts");
            });

            AddButton ("Close All Active Alerts", 250, 120, () => {
                foreach (var alert in active)
                    alert.Close ();
                var count = active.Count;
                active.Clear ();
                Report ($"Closed {count} active alert(s)");
            });
        }

        private void ShowAlert (string caption, string content, int autoCloseDelay, Color borderColor)
        {
            var alert = new RadDesktopAlert {
                CaptionText = caption,
                ContentText = content,
                AutoCloseDelay = autoCloseDelay,
            };

            if (!borderColor.IsEmpty)
                alert.Popup.AlertElement.BorderColor = borderColor;

            active.Add (alert);
            alert.Show ();

            Report ($"Showed alert \"{caption}\" (AutoCloseDelay={autoCloseDelay}ms)");
        }

        private void AddButton (string text, int left, int top, System.Action onClick)
        {
            var button = new Button { Text = text, Left = left, Top = top, Width = 230, Height = 30 };
            button.Click += (_, _) => onClick ();
            Controls.Add (button);
        }

        private void Report (string action) => status.Text = $"Last action: {action}";

        public override void UnloadPanel ()
        {
            foreach (var alert in active)
                alert.Dispose ();
            active.Clear ();
        }
    }
}
