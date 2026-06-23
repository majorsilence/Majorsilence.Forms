using System.Drawing;
using Majorsilence.Forms;
using SkiaSharp;

namespace ControlGallery.Panels
{
    // Demonstrates Majorsilence.Forms' emulated MDI. Like the Dialogs/MessageBox examples, this page
    // opens a separate window — an MDI parent — because MDI containers are forms, not panels.
    public class MdiPanel : BasePanel
    {
        private MdiDemoForm? demo;

        public MdiPanel ()
        {
            var info = Controls.Add (new Label {
                Left = 20,
                Top = 20,
                Width = 760,
                Height = 28,
                Text = "Emulated MDI: child forms hosted inside the parent, with drag, resize, min/max and tile/cascade."
            });
            info.Style.BackgroundColor = SKColors.Transparent;

            var open = Controls.Add (new Button { Left = 20, Top = 120, Width = 200, Height = 32, Text = "Open MDI Window" });
            open.Click += (o, e) => {
                if (demo == null) {
                    demo = new MdiDemoForm ();
                    demo.Closed += (_, _) => demo = null;
                }

                demo.Show ();
            };
        }

        // Close the demo window when navigating away from this page.
        public override void UnloadPanel ()
        {
            demo?.Close ();
            demo = null;
        }
    }

    // A standalone MDI parent window with a toolbar for spawning and arranging child documents.
    public class MdiDemoForm : Form
    {
        private int counter;

        public MdiDemoForm ()
        {
            Text = "MDI Demo";
            Size = new Size (900, 600);

            // Add the MDI client first (Dock=Fill) so the toolbar, docked Top afterwards, takes the
            // top edge and the client fills the remainder.
            IsMdiContainer = true;

            var bar = new Panel { Dock = DockStyle.Top, Height = 44 };
            bar.Style.BackgroundColor = Theme.ControlMidHighColor;

            var x = 8;
            AddButton (bar, "New Child", ref x, 110, (o, e) => NewChild ());
            AddButton (bar, "Cascade", ref x, 90, (o, e) => LayoutMdi (MdiLayout.Cascade));
            AddButton (bar, "Tile Horiz.", ref x, 100, (o, e) => LayoutMdi (MdiLayout.TileHorizontal));
            AddButton (bar, "Tile Vert.", ref x, 100, (o, e) => LayoutMdi (MdiLayout.TileVertical));
            AddButton (bar, "Arrange Icons", ref x, 120, (o, e) => LayoutMdi (MdiLayout.ArrangeIcons));
            AddButton (bar, "Close Active", ref x, 110, (o, e) => ActiveMdiChild?.Close ());

            Controls.Add (bar);

            NewChild ();
            NewChild ();
        }

        private void NewChild ()
        {
            counter++;

            var child = new Form {
                Text = $"Document {counter}",
                Size = new Size (340, 240)
            };

            var text = new TextBox {
                Dock = DockStyle.Fill,
                Multiline = true,
                Text = $"Child window #{counter}\r\n\r\nDrag my caption to move me, drag my edges to resize, "
                     + "or use the caption buttons to minimize / maximize / close."
            };
            child.Controls.Add (text);

            child.MdiParent = this;
            child.Show ();
        }

        private static void AddButton (Panel bar, string text, ref int left, int width, EventHandler<MouseEventArgs> onClick)
        {
            var b = new Button { Text = text, Left = left, Top = 8, Width = width, Height = 28 };
            b.MouseClick += onClick;
            bar.Controls.Add (b);
            left += width + 6;
        }
    }
}
