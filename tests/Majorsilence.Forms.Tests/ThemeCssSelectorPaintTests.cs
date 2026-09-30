using System;
using System.Linq;
using Majorsilence.Forms.Headless;
using Majorsilence.Forms.Telerik;
using SkiaSharp;
using Xunit;

namespace Majorsilence.Forms.Tests
{
    // #100: the selectors added so every control is reachable. Each one is proved by paint, not by the
    // parser accepting it -- a rule on a style the control never reads is the silent no-op the subset
    // forbids. Magenta appears nowhere in the built-in themes, so any magenta is the rule's.
    [Collection ("Headless")]
    public class ThemeCssSelectorPaintTests : IDisposable
    {
        private static readonly SKColor Magenta = new SKColor (255, 0, 255);

        public ThemeCssSelectorPaintTests () => Theme.SetBuiltInTheme (BuiltInTheme.Light);

        public void Dispose ()
        {
            GC.SuppressFinalize (this);
            Theme.SetBuiltInTheme (BuiltInTheme.Light);
        }

        public static TheoryData<string> ControlSelectors => new () {
            "DateTimePicker", "MdiClient", "PrintPreviewControl", "ProgressBar", "StatusStrip",
            "DockWindowBase", "RadCommandBar", "RadPdfViewerNavigator", "RadRibbonBar", "RadScheduler",
            "RadSchedulerNavigator", "RadStatusStrip", "RichTextEditorRibbonBar",
        };

        private static Control Create (string selector) => selector switch {
            "DateTimePicker" => new DateTimePicker (),
            "MdiClient" => new MdiClient (),
            "PrintPreviewControl" => new PrintPreviewControl (),
            "ProgressBar" => new ProgressBar { Value = 0 },
            "StatusStrip" => new StatusStrip (),
            "DockWindowBase" => new ToolWindow (),
            "RadCommandBar" => new RadCommandBar (),
            "RadPdfViewerNavigator" => new RadPdfViewerNavigator (),
            "RadRibbonBar" => new RadRibbonBar (),
            "RadScheduler" => new RadScheduler (),
            "RadSchedulerNavigator" => new RadSchedulerNavigator (),
            "RadStatusStrip" => new RadStatusStrip (),
            "RichTextEditorRibbonBar" => new RichTextEditorRibbonBar (),
            _ => throw new ArgumentOutOfRangeException (nameof (selector)),
        };

        [Theory]
        [MemberData (nameof (ControlSelectors))]
        public void Rule_background_is_what_is_painted (string selector)
        {
            HeadlessRenderer.Use ();

            var before = Render (selector);
            Assert.Equal (0, before);

            Theme.LoadFromCss ($"{selector} {{ background-color: #ff00ff; }}");

            var after = Render (selector);
            Assert.True (after > 0.25, $"a {selector} rule's background should paint the control; {after:P0} of it is magenta");
        }

        [Fact]
        public void FormTitleBar_rule_paints_the_bar_and_the_caption ()
        {
            HeadlessRenderer.Use ();
            Theme.LoadFromCss ("FormTitleBar { background-color: #ff00ff; color: #00ff00; }");

            using var form = new Form { UseSystemDecorations = false, Text = "Caption", Size = new System.Drawing.Size (400, 300) };
            Assert.True (form.TitleBar.Visible);
            Assert.False (form.TitleBar.NativeOverlay);

            var bar = RenderBackBuffer (form.TitleBar, form);

            Assert.True (Coverage (bar, Magenta) > 0.5, "the bar should take the rule's background");
            Assert.True (Count (bar, c => c.Green > 200 && c.Red < 80 && c.Blue < 80) > 0, "the caption should take the rule's color");
        }

        [Fact]
        public void PopupWindow_rule_paints_the_popup ()
        {
            HeadlessRenderer.Use ();
            Theme.LoadFromCss ("PopupWindow { background-color: #ff00ff; }");

            using var parent = new Form ();
            parent.Show ();

            using var popup = new PopupWindow (parent) { Size = new System.Drawing.Size (80, 60) };
            popup.Show (10, 10);

            using var bitmap = SKBitmap.Decode (HeadlessRenderer.CapturePng (popup));

            Assert.True (Coverage (bitmap, Magenta) > 0.9, "an empty popup is its background");
        }

        private static double Render (string selector)
        {
            using var form = new Form { Size = new System.Drawing.Size (400, 300) };
            var control = Create (selector);
            control.Dock = DockStyle.Fill;
            form.Controls.Add (control);

            return Coverage (RenderBackBuffer (control, form), Magenta);
        }

        private static SKBitmap RenderBackBuffer (Control control, Form form)
        {
            form.Show ();
            HeadlessRenderer.CapturePng (form);
            HeadlessRenderer.CapturePng (form);

            var buffer = typeof (Control).GetMethod ("GetBackBuffer",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!;
            return (SKBitmap) buffer.Invoke (control, null)!;
        }

        private static double Coverage (SKBitmap bitmap, SKColor color)
            => (double) Count (bitmap, c => c.Red == color.Red && c.Green == color.Green && c.Blue == color.Blue) / (bitmap.Width * bitmap.Height);

        private static int Count (SKBitmap bitmap, Func<SKColor, bool> match)
        {
            var hits = 0;
            for (var x = 0; x < bitmap.Width; x++)
                for (var y = 0; y < bitmap.Height; y++)
                    if (match (bitmap.GetPixel (x, y)))
                        hits++;
            return hits;
        }
    }
}
