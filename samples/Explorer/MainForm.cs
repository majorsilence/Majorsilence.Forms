using Majorsilence.Forms;
using SkiaSharp;

namespace Explore
{
    public partial class MainForm : Form
    {
        private string current_directory = string.Empty;

        public MainForm ()
        {
            InitializeComponent ();

            // Register a custom theme that is defined entirely in XML (see Themes/Ocean.xml).
            // Once registered it can be applied by name with Theme.ApplyTheme ("Ocean").
            Theme.RegisterThemeFromFile (Path.Combine (AppContext.BaseDirectory, "Themes", "Ocean.xml"));

            // Populate the drive list
            foreach (var drive in DriveInfo.GetDrives ().Where (d => d.IsReady))
                tree.Items.Add ($"{drive.Name.Trim ('\\')} - {drive.VolumeLabel}", ImageLoader.Get ("drive.png")).Tag = drive.Name;

            tree.SelectedItem = tree.Items.First ();

            // Select the first available drive
            if (tree.Items.First ().Tag is string s)
                SetSelectedDirectory (s);
        }

        private void View_ItemDoubleClicked (object sender, EventArgs<ListViewItem> e)
        {
            var item = e.Value;

            if (item.Tag is string s && s == "Directory")
                SetSelectedDirectory (Path.Combine (current_directory, item.Text));
        }

        private void Tree_ItemSelected (object sender, EventArgs<TreeViewItem> e)
        {
            if (e.Value.Tag is string drive)
                SetSelectedDirectory (drive);
        }

        private void SetSelectedDirectory (string directory)
        {
            current_directory = directory;

            view.Items.Clear ();

            var directories = 0;
            var files = 0;
            var tree_item = tree.SelectedItem;

            try {
                foreach (var d in Directory.EnumerateDirectories (directory).Take (30))
                    view.Items.Add (new ListViewItem { Text = Path.GetFileName (d), Image = ImageLoader.Get ("folder-closed.png"), Tag = "Directory" });

                directories = view.Items.Count;

                if (!tree_item.HasChildren)
                    tree_item.Items.AddRange (view.Items.Select (l => new TreeViewItem (l.Text) { Image = ImageLoader.Get ("folder.png"), Tag = Path.Combine (current_directory, l.Text) }));

                foreach (var f in Directory.EnumerateFiles (directory).Take (50))
                    view.Items.Add (new ListViewItem { Text = Path.GetFileName (f), Image = ImageLoader.Get ("new.png") });

                files = view.Items.Count - directories;

                statusbar.Text = $"{directories} directories, {files} files";

            } catch (UnauthorizedAccessException) {
                // Ignore
                statusbar.Text = "Unable to open directory due to permissions";
            }

            Text = "Explore Sample - " + directory;
        }

        private void ThemeButton_Clicked (object sender, EventArgs args)
        {
            var item = sender as MenuItem;

            // The "Ocean (XML)" entry is a custom theme loaded from Themes/Ocean.xml. It declares
            // its own base (Dark) plus overrides, so apply it directly instead of resetting to Light.
            if (item?.Text == "Ocean (XML)") {
                Theme.ApplyTheme ("Ocean");
                Invalidate ();
                return;
            }

            Theme.SetBuiltInTheme (BuiltInTheme.Light);

            switch (item?.Text) {
                case "Default":
                    Theme.BeginUpdate ();
                    Theme.AccentColor = new SKColor (42, 138, 208);
                    Theme.AccentColor2 = new SKColor (16, 110, 190);
                    Theme.EndUpdate ();
                    break;
                case "Green":
                    Theme.BeginUpdate ();
                    Theme.AccentColor = new SKColor (67, 148, 103);
                    Theme.AccentColor2 = new SKColor (33, 115, 70);
                    Theme.EndUpdate ();
                    break;
                case "Orange":
                    Theme.BeginUpdate ();
                    Theme.AccentColor = new SKColor (220, 89, 57);
                    Theme.AccentColor2 = new SKColor (183, 71, 42);
                    Theme.EndUpdate ();
                    break;
                case "Purple":
                    Theme.BeginUpdate ();
                    Theme.AccentColor = new SKColor (163, 86, 158);
                    Theme.AccentColor2 = new SKColor (128, 57, 123);
                    Theme.EndUpdate ();
                    break;
                case "Hotdog Stand":
                    Theme.BeginUpdate ();
                    Theme.AccentColor = new SKColor (255, 128, 128);
                    Theme.BackgroundColor = SKColors.Yellow;
                    Theme.ControlMidColor = SKColors.White;
                    Theme.ControlLowColor = new SKColor (255, 0, 0);
                    Theme.ControlHighlightLowColor = new SKColor (255, 255, 255);
                    Theme.AccentColor2 = new SKColor (255, 0, 0);
                    Theme.EndUpdate ();
                    break;
            }

            Invalidate ();
        }

        private void ParentFolder_Clicked (object sender, EventArgs args)
        {
            var parent_folder = Path.GetDirectoryName (current_directory);

            if (parent_folder != null)
                SetSelectedDirectory (parent_folder);
        }

        private void NotImplemented_Clicked (object sender, EventArgs args)
        {
            new MessageBoxForm ("Demo", "Functionality not available in demo").ShowDialog (this);
        }
    }
}
