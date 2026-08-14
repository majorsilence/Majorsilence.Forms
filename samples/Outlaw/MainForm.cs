using System.Drawing;
using Majorsilence.Forms;

namespace Outlaw
{
    public partial class MainForm : Form
    {
        public MainForm ()
        {
            InitializeComponent ();

            PopulateEmailList ();
            email_list.DrawNode += EmailListDrawNode;

            email_list.Style.SelectedItemBackgroundColor = Theme.ControlMidHighColor;

        }

        // TreeView.DrawNode carries WinForms' DrawTreeNodeEventArgs. The Skia canvas and the scale
        // come off the same args (they are not upstream members) so owner-draw code still paints
        // through this library's own text and shape helpers rather than through Graphics.
        private void EmailListDrawNode (object? sender, DrawTreeNodeEventArgs e)
        {
            if (e.Canvas is not { } canvas)
                return;

            var item = (EmailListItem)e.Node;

            if (item.Unread) {
                var bounds = new Rectangle (item.Bounds.Left, item.Bounds.Top + e.LogicalToDeviceUnits (1), e.LogicalToDeviceUnits (3), item.Bounds.Height - e.LogicalToDeviceUnits (2));
                canvas.FillRectangle (bounds, Theme.AccentColor2);
            }

            var line1_bounds = new Rectangle (item.Bounds.Left + e.LogicalToDeviceUnits (12), item.Bounds.Top + e.LogicalToDeviceUnits (3), item.Bounds.Width - e.LogicalToDeviceUnits (80), e.LogicalToDeviceUnits (23));
            var line2_bounds = new Rectangle (item.Bounds.Left + e.LogicalToDeviceUnits (12), line1_bounds.Bottom - e.LogicalToDeviceUnits (3), item.Bounds.Width - e.LogicalToDeviceUnits (16), e.LogicalToDeviceUnits (20));
            var line3_bounds = new Rectangle (item.Bounds.Left + e.LogicalToDeviceUnits (12), line2_bounds.Bottom - e.LogicalToDeviceUnits (3), item.Bounds.Width - e.LogicalToDeviceUnits (16), e.LogicalToDeviceUnits (20));
            var date_bounds = new Rectangle (item.Bounds.Width - e.LogicalToDeviceUnits (80), item.Bounds.Top + e.LogicalToDeviceUnits (3), e.LogicalToDeviceUnits (74), e.LogicalToDeviceUnits (23));

            canvas.DrawText (item.Text, Theme.UIFont, e.LogicalToDeviceUnits (16), line1_bounds, Theme.ForegroundColor, Majorsilence.Forms.ContentAlignment.MiddleLeft, maxLines: e.LogicalToDeviceUnits (1));
            canvas.DrawText (item.Subject, Theme.UIFont, e.LogicalToDeviceUnits (12), line2_bounds, CustomTheme.LighterGrayFont, Majorsilence.Forms.ContentAlignment.MiddleLeft, maxLines: e.LogicalToDeviceUnits (1));
            canvas.DrawText (item.Body, Theme.UIFont, e.LogicalToDeviceUnits (12), line3_bounds, CustomTheme.LighterGrayFont, Majorsilence.Forms.ContentAlignment.MiddleLeft, maxLines: e.LogicalToDeviceUnits (1));
            canvas.DrawText (FormatDateTime (item.ReceiveDate), Theme.UIFont, e.LogicalToDeviceUnits (11), date_bounds, CustomTheme.LighterGrayFont, Majorsilence.Forms.ContentAlignment.MiddleRight, maxLines: e.LogicalToDeviceUnits (1));

            canvas.DrawLine (item.Bounds.Left, item.Bounds.Bottom - e.LogicalToDeviceUnits (1), item.Bounds.Right, item.Bounds.Bottom - e.LogicalToDeviceUnits (1), Theme.ControlMidColor, e.LogicalToDeviceUnits (1));
        }

        private static string FormatDateTime (DateTime date)
        {
            if (date.ToShortDateString () == DateTime.Now.ToShortDateString ())
                return date.ToShortTimeString ();

            return date.ToString ("ddd M/d");
        }

        private void PopulateEmailList ()
        {
            email_list.Items.Add (new EmailListItem ("Megan Smith", "New mockups", "Hey I got those new mockups you requested!", GetNextDateTime (), true));
            email_list.Items.Add (new EmailListItem ("Greg Simon", "Dinner on Friday", "Are you available for dinner on Friday? Ashley said she is available.", GetNextDateTime (), true));
            email_list.Items.Add (new EmailListItem ("Victor Craig", "New mockups", "Hey I got those new mockups you requested!", GetNextDateTime ()));
            email_list.Items.Add (new EmailListItem ("Beverly Williams", "New mockups", "Hey I got those new mockups you requested!", GetNextDateTime (), true));
            email_list.Items.Add (new EmailListItem ("Morgan Graves", "New mockups", "Hey I got those new mockups you requested!", GetNextDateTime ()));
            email_list.Items.Add (new EmailListItem ("Megan Smith", "New mockups", "Hey I got those new mockups you requested!", GetNextDateTime ()));
            email_list.Items.Add (new EmailListItem ("noreply@marketing.example.com", "Dinner on Friday", "Are you available for dinner on Friday? Ashley said she is available.", GetNextDateTime ()));
            email_list.Items.Add (new EmailListItem ("Victor Craig", "New mockups", "Hey I got those new mockups you requested!", GetNextDateTime ()));
            email_list.Items.Add (new EmailListItem ("Beverly Williams", "New mockups", "Hey I got those new mockups you requested!", GetNextDateTime ()));
            email_list.Items.Add (new EmailListItem ("Morgan Graves", "New mockups", "Hey I got those new mockups you requested!", GetNextDateTime ()));
            email_list.Items.Add (new EmailListItem ("Megan Smith", "New mockups", "Hey I got those new mockups you requested!", GetNextDateTime ()));
            email_list.Items.Add (new EmailListItem ("Greg Simon", "Dinner on Friday", "Are you available for dinner on Friday? Ashley said she is available.", GetNextDateTime ()));
            email_list.Items.Add (new EmailListItem ("Victor Craig", "New mockups", "Hey I got those new mockups you requested!", GetNextDateTime ()));
            email_list.Items.Add (new EmailListItem ("Beverly Williams", "New mockups", "Hey I got those new mockups you requested!", GetNextDateTime ()));
            email_list.Items.Add (new EmailListItem ("Morgan Graves", "New mockups", "Hey I got those new mockups you requested!", GetNextDateTime ()));
            email_list.Items.Add (new EmailListItem ("Megan Smith", "New mockups", "Hey I got those new mockups you requested!", GetNextDateTime ()));
            email_list.Items.Add (new EmailListItem ("Greg Simon", "Dinner on Friday", "Are you available for dinner on Friday? Ashley said she is available.", GetNextDateTime ()));
            email_list.Items.Add (new EmailListItem ("Victor Craig", "New mockups", "Hey I got those new mockups you requested!", GetNextDateTime ()));
            email_list.Items.Add (new EmailListItem ("Beverly Williams", "New mockups", "Hey I got those new mockups you requested!", GetNextDateTime ()));
            email_list.Items.Add (new EmailListItem ("Morgan Graves", "New mockups", "Hey I got those new mockups you requested!", GetNextDateTime ()));
            email_list.Items.Add (new EmailListItem ("Megan Smith", "New mockups", "Hey I got those new mockups you requested!", GetNextDateTime ()));
            email_list.Items.Add (new EmailListItem ("Greg Simon", "Dinner on Friday", "Are you available for dinner on Friday? Ashley said she is available.", GetNextDateTime ()));
            email_list.Items.Add (new EmailListItem ("Victor Craig", "New mockups", "Hey I got those new mockups you requested!", GetNextDateTime ()));
            email_list.Items.Add (new EmailListItem ("Beverly Williams", "New mockups", "Hey I got those new mockups you requested!", GetNextDateTime ()));
            email_list.Items.Add (new EmailListItem ("Morgan Graves", "New mockups", "Hey I got those new mockups you requested!", GetNextDateTime ()));
        }

        private DateTime GetNextDateTime ()
        {
            hours += Random.Shared.Next (1, 6);

            return DateTime.Now.Subtract (new TimeSpan (hours, Random.Shared.Next (1, 59), Random.Shared.Next (1, 59)));
        }

        private int hours;
    }
}
