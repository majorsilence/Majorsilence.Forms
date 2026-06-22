using System;
using System.IO;
using Majorsilence.Forms;
using Majorsilence.Forms.Headless;

namespace ControlGallery
{
    public class Program
    {
        static void Main (string[] args)
        {
            // Headless render mode: "--render-headless <path.png> [width height]" renders the whole
            // gallery on the dependency-free Headless backend and writes a PNG — no display needed.
            // Demonstrates the gallery displaying on a non-Avalonia backend through the same seam.
            var renderIndex = Array.IndexOf (args, "--render-headless");
            if (renderIndex >= 0 && renderIndex + 1 < args.Length) {
                var path = args[renderIndex + 1];
                var width = renderIndex + 2 < args.Length && int.TryParse (args[renderIndex + 2], out var w) ? w : 1100;
                var height = renderIndex + 3 < args.Length && int.TryParse (args[renderIndex + 3], out var h) ? h : 750;

                HeadlessRenderer.Use ();
                var form = new MainForm ();
                HeadlessRenderer.CapturePng (form, width, height);   // initial layout pass

                // Optionally select a nav-tree row via simulated input so a control's detail panel renders
                // ("--select-row N", default 0) — proves the panels render too, not just the nav pane.
                var selRow = 0;
                var selIndex = Array.IndexOf (args, "--select-row");
                if (selIndex >= 0 && selIndex + 1 < args.Length)
                    _ = int.TryParse (args[selIndex + 1], out selRow);
                if (selIndex >= 0) {
                    const int rowHeight = 24, firstRowCenterY = 12, treeX = 60;
                    HeadlessRenderer.Click (form, treeX, firstRowCenterY + selRow * rowHeight);
                }

                var png = HeadlessRenderer.CapturePng (form, width, height);
                File.WriteAllBytes (path, png);
                Console.WriteLine ($"Rendered ControlGallery on the {Majorsilence.Forms.Backends.Platform.Backend.Name} backend → {path} ({png.Length} bytes, {width}x{height}).");
                return;
            }

            // Headless input self-test: proves the neutral input path works on a non-Avalonia backend
            // by injecting a click and confirming the Button's Click event fires.
            if (Array.IndexOf (args, "--headless-selftest") >= 0) {
                HeadlessRenderer.Use ();
                var form = new Form { Text = "selftest" };
                var clicks = 0;
                var button = new Button { Text = "Click", Left = 20, Top = 20, Width = 120, Height = 40 };
                button.Click += (_, _) => clicks++;
                form.Controls.Add (button);

                HeadlessRenderer.CapturePng (form, 300, 200);   // force a layout pass
                HeadlessRenderer.Click (form, 80, 40);          // center of the button

                var backend = Majorsilence.Forms.Backends.Platform.Backend.Name;
                Console.WriteLine ($"[headless-selftest] backend={backend} Button.Click fired {clicks}x → {(clicks > 0 ? "PASS" : "FAIL")}");
                Environment.ExitCode = clicks > 0 ? 0 : 1;
                return;
            }

            // Default: run windowed on the Avalonia backend.
            Application.Run (new MainForm ());
        }
    }
}
