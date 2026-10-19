using System;
using System.IO;
using Majorsilence.Forms;
using Majorsilence.Forms.Headless;

namespace CssShowcase
{
    // CSS Showcase: a small window styled entirely by showcase.css, which uses the newer theme CSS --
    // transitions, rule variables, oklch / color-mix / relative colours, rem, gradients, blurred and
    // inset shadows, outlines, :focus-visible, @media and @keyframes. See docs/theming.md.
    //
    //   dotnet run --project samples/CssShowcase                                      -- the copy beside the app
    //   dotnet run --project samples/CssShowcase -- samples/CssShowcase/showcase.css  -- watch and re-apply on save
    //   dotnet run --project samples/CssShowcase -- --render-headless out.png [--dark] [--hover] [--focus]
    internal static class Program
    {
        [STAThread]
        private static int Main (string[] args)
        {
            var renderIndex = Array.IndexOf (args, "--render-headless");

            if (renderIndex >= 0) {
                if (renderIndex + 1 >= args.Length) {
                    Console.Error.WriteLine ("usage: --render-headless <out.png> [--dark] [--hover] [--focus]");
                    return 2;
                }

                return RenderHeadless (args[renderIndex + 1], Has (args, "--dark"), Has (args, "--hover"), Has (args, "--focus"));
            }

            var path = args.Length > 0 && !args[0].StartsWith ('-') ? Path.GetFullPath (args[0]) : DefaultSheetPath;

            Application.Run (new MainForm (path));
            return 0;
        }

        private static bool Has (string[] args, string flag) => Array.IndexOf (args, flag) >= 0;

        private static string DefaultSheetPath => Path.Combine (AppContext.BaseDirectory, "showcase.css");

        private static int RenderHeadless (string output, bool dark, bool hover, bool focus)
        {
            HeadlessRenderer.Use ();

            // The sheet's @media (prefers-color-scheme: dark) block follows this, as it follows the OS in a window.
            HeadlessRenderer.PrefersDarkColorScheme = dark;

            var form = new MainForm (DefaultSheetPath);
            form.Show ();   // a shown window receives the pointer input below
            HeadlessRenderer.CapturePng (form, MainForm.InitialWidth, MainForm.InitialHeight);   // lay out

            if (hover) {
                // Hover the first button, then run the clock past its 180ms transition.
                var target = form.PrimaryButtonCenter;
                HeadlessRenderer.MouseMove (form, target.X, target.Y);
                HeadlessRenderer.CapturePng (form, MainForm.InitialWidth, MainForm.InitialHeight);
                HeadlessRenderer.AnimationClock.Step (20);
            }

            if (focus) {
                // Tab moves focus by keyboard, which is what :focus-visible (the ring) asks for.
                HeadlessRenderer.KeyDown (form, Keys.Tab);
                HeadlessRenderer.KeyUp (form, Keys.Tab);
                HeadlessRenderer.CapturePng (form, MainForm.InitialWidth, MainForm.InitialHeight);
                HeadlessRenderer.AnimationClock.Step (20);
            }

            var png = HeadlessRenderer.CapturePng (form, MainForm.InitialWidth, MainForm.InitialHeight);
            File.WriteAllBytes (output, png);
            Console.WriteLine ($"Rendered CSS Showcase ({(dark ? "dark" : "light")}{(hover ? ", hovered" : "")}{(focus ? ", focused" : "")}) → {output}");

            foreach (var diagnostic in form.Diagnostics)
                Console.WriteLine (diagnostic);

            return form.HasErrors ? 1 : 0;
        }
    }
}
