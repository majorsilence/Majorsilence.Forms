using Majorsilence.Forms;

namespace ControlGallery.Panels
{
    public class MenuStripPanel : Panel
    {
        public MenuStripPanel ()
        {
            // --- Description ---
            Controls.Add (new Label {
                Text = "MenuStrip docked at the top (WinForms-compatible MenuStrip with ToolStripMenuItems).",
                Left = 20, Top = 40, Width = 580
            });

            var lastActionLabel = Controls.Add (new Label {
                Text = "Last action: (none)",
                Left = 20, Top = 70, Width = 400
            });

            void Report (string action) => lastActionLabel.Text = $"Last action: {action}";

            // --- Build the MenuStrip using WinForms-compatible API ---
            var menuStrip = new MenuStrip ();

            // File menu
            var fileItem = new ToolStripMenuItem { Text = "&File" };

            var newItem  = new ToolStripMenuItem { Text = "&New" };
            newItem.Click += (o, e) => Report ("File → New");

            var openItem = new ToolStripMenuItem { Text = "&Open…" };
            openItem.Click += (o, e) => Report ("File → Open");

            var sep1 = new ToolStripSeparator ();

            var saveItem    = new ToolStripMenuItem { Text = "&Save" };
            saveItem.Click += (o, e) => Report ("File → Save");

            var saveAsItem    = new ToolStripMenuItem { Text = "Save &As…" };
            saveAsItem.Click += (o, e) => Report ("File → Save As");

            var sep2 = new ToolStripSeparator ();

            var exitItem    = new ToolStripMenuItem { Text = "E&xit" };
            exitItem.Click += (o, e) => Report ("File → Exit");

            fileItem.DropDownItems.AddRange (new ToolStripItem[] {
                newItem, openItem, sep1, saveItem, saveAsItem, sep2, exitItem
            });

            // Edit menu
            var editItem = new ToolStripMenuItem { Text = "&Edit" };

            var cutItem   = new ToolStripMenuItem { Text = "Cu&t" };
            cutItem.Click += (o, e) => Report ("Edit → Cut");

            var copyItem  = new ToolStripMenuItem { Text = "&Copy" };
            copyItem.Click += (o, e) => Report ("Edit → Copy");

            var pasteItem = new ToolStripMenuItem { Text = "&Paste" };
            pasteItem.Click += (o, e) => Report ("Edit → Paste");

            editItem.DropDownItems.AddRange (new ToolStripItem[] { cutItem, copyItem, pasteItem });

            // View menu with a sub-menu
            var viewItem = new ToolStripMenuItem { Text = "&View" };

            var zoomItem = new ToolStripMenuItem { Text = "Zoo&m" };

            var zoom50   = new ToolStripMenuItem { Text = "50%" };
            zoom50.Click += (o, e) => Report ("View → Zoom → 50%");

            var zoom100  = new ToolStripMenuItem { Text = "100%" };
            zoom100.Click += (o, e) => Report ("View → Zoom → 100%");

            var zoom200  = new ToolStripMenuItem { Text = "200%" };
            zoom200.Click += (o, e) => Report ("View → Zoom → 200%");

            zoomItem.DropDownItems.AddRange (new ToolStripItem[] { zoom50, zoom100, zoom200 });

            var fullScreen = new ToolStripMenuItem { Text = "&Full Screen" };
            fullScreen.Click += (o, e) => Report ("View → Full Screen");

            viewItem.DropDownItems.AddRange (new ToolStripItem[] { zoomItem, fullScreen });

            // Help menu
            var helpItem = new ToolStripMenuItem { Text = "&Help" };

            var docsItem  = new ToolStripMenuItem { Text = "&Documentation" };
            docsItem.Click += (o, e) => Report ("Help → Documentation");

            var aboutItem = new ToolStripMenuItem { Text = "&About" };
            aboutItem.Click += (o, e) => Report ("Help → About");

            helpItem.DropDownItems.AddRange (new ToolStripItem[] { docsItem, aboutItem });

            // Assemble the strip
            menuStrip.Items.AddRange (new ToolStripItem[] { fileItem, editItem, viewItem, helpItem });

            Controls.Add (menuStrip);

            // --- Show contrast with the existing Menu API ---
            Controls.Add (new Label {
                Text = "For comparison — native Majorsilence.Forms Menu API:",
                Left = 20, Top = 105, Width = 400
            });

            var nativeMenu = new Menu ();
            var nativeFile = nativeMenu.Items.Add ("File");
            nativeFile.Items.Add ("New").Click  += (o, e) => Report ("Native menu → File → New");
            nativeFile.Items.Add ("Open").Click += (o, e) => Report ("Native menu → File → Open");
            nativeMenu.Style.Border.Top.Width = 1;

            Controls.Add (new Panel {
                Left = 20, Top = 130, Width = 580, Height = 28,
                Controls = { nativeMenu }
            });

            Controls.Add (new Label {
                Text = "Both share the same rendering engine. MenuStrip uses WinForms-compatible constructor syntax.",
                Left = 20, Top = 170, Width = 580
            });
        }
    }
}
