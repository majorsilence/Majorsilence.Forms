using Majorsilence.Forms;

namespace PointOfSale.Client;

public partial class MainForm : Form
{
    private NavigationPane nav = null!;
    private StatusBar statusbar = null!;
    private StatusBarPanel statusUserPanel = null!;
    private StatusBarPanel statusConnectionPanel = null!;

    private void InitializeComponent()
    {
        // A touchscreen POS terminal runs on a larger screen than the framework's 1080x720
        // default — give panels (esp. Checkout's keypad-driven layout) room to breathe.
        ClientSize = new System.Drawing.Size(1400, 1180);

        // NavigationPane.DefaultSize is only 49px wide (an icon-rail default) — too narrow for
        // text items like "Categories"; widen it so labels don't wrap into a mangled 2-line stack.
        nav = Controls.Add(new NavigationPane { Dock = DockStyle.Left, Width = 220, Visible = false });
        nav.SelectedItemChanged += Nav_SelectedItemChanged;

        statusbar = Controls.Add(new StatusBar { ShowPanels = true });
        statusUserPanel = statusbar.Panels.Add("Not signed in");
        statusConnectionPanel = statusbar.Panels.Add(string.Empty);

        Text = "Point of Sale";
        Image = ImageLoader.Get("folder.png");
    }
}
