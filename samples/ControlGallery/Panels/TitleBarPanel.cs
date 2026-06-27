using System.Collections.Generic;
using System.Drawing;
using Majorsilence.Forms;

namespace ControlGallery.Panels
{
    // Demonstrates the FormTitleBar / window-chrome support:
    //  • An inline preview of a FormTitleBar with custom content (a search box).
    //  • Launching real Forms that show the three chrome modes: fully custom chrome (Windows/Linux),
    //    plain native chrome (UseSystemDecorations), and — the point of the demo — custom content
    //    merged INTO the native macOS title bar via ExtendsContentIntoTitleBar (Avalonia 12 full-size
    //    content view: native traffic lights / rounded corners / shadow, with our controls in the bar).
    public class TitleBarPanel : BasePanel
    {
        private readonly List<Form> open_forms = new ();

        public TitleBarPanel ()
        {
            Controls.Add (new Label {
                Text = "Add controls to Form.TitleBar.Controls to draw into the window title bar. "
                     + "On Windows/Linux this is the custom-drawn chrome; on macOS, set "
                     + "ExtendsContentIntoTitleBar = true to merge your content into the native title bar.",
                Left = 10, Top = 10, Width = 780, Height = 50
            });

            // Inline preview of a title bar hosting custom content.
            var preview = Controls.Add (new FormTitleBar {
                Text = "Inline FormTitleBar preview",
                Image = ImageLoader.Get ("swatches.png"),
                Top = 70, Left = 10, Width = 780
            });
            preview.Controls.Add (new TextBox { Placeholder = "Search", Dock = DockStyle.Left, Width = 180 });

            var custom = new Button {
                Text = "Open form with custom title-bar content", Left = 10, Top = 120, Width = 320
            };
            custom.Click += (_, _) => ShowForm (BuildCustomTitleBarForm ());

            var native = new Button {
                Text = "Open form with native chrome", Left = 10, Top = 158, Width = 320
            };
            native.Click += (_, _) => ShowForm (new Form {
                Text = "Native chrome (UseSystemDecorations = true)",
                Size = new Size (480, 320),
                UseSystemDecorations = true
            });

            var fullyCustom = new Button {
                Text = "Open form with fully custom chrome", Left = 10, Top = 196, Width = 320
            };
            fullyCustom.Click += (_, _) => ShowForm (BuildCustomTitleBarForm (forceCustomChrome: true));

            Controls.Add (custom);
            Controls.Add (native);
            Controls.Add (fullyCustom);
        }

        // Builds a form that paints custom content (icon + search box) into its title bar. By default it
        // adapts to the platform: native+merged on macOS, custom chrome on Windows/Linux. Pass
        // forceCustomChrome to use Majorsilence.Forms' own drawn chrome (with our caption buttons) on
        // every platform, including macOS.
        private static Form BuildCustomTitleBarForm (bool forceCustomChrome = false)
        {
            var form = new Form {
                Text = "Custom Title Bar",
                Size = new Size (640, 400)
            };

            if (forceCustomChrome)
                form.UseSystemDecorations = false;
            else if (System.OperatingSystem.IsMacOS ())
                // Keep the native macOS title bar (traffic lights / corners / shadow) but extend our
                // content up into it so the title-bar controls below show through.
                form.ExtendsContentIntoTitleBar = true;

            form.TitleBar.Image = ImageLoader.Get ("swatches.png");

            // Dock=Left places the search box after the window icon (and, on macOS, after the reserved
            // traffic-light inset).
            form.TitleBar.Controls.Add (new TextBox {
                Placeholder = "Search…", Dock = DockStyle.Left, Width = 220
            });

            form.Controls.Add (new Label {
                Text = forceCustomChrome
                    ? "Fully custom chrome: Majorsilence.Forms draws the whole title bar, including the caption buttons."
                    : "The search box above lives in the title bar. On macOS it is merged into the native title bar.",
                Left = 20, Top = 20, Width = 580, Height = 60
            });

            return form;
        }

        private void ShowForm (Form form)
        {
            open_forms.Add (form);
            form.FormClosed += (_, _) => open_forms.Remove (form);
            form.Show ();
        }

        public override void UnloadPanel ()
        {
            // Close any demo windows still open when the user navigates away.
            foreach (var form in open_forms.ToArray ())
                form.Close ();

            open_forms.Clear ();
        }
    }
}
