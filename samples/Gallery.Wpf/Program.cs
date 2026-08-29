using System;
using ControlGallery;
using Majorsilence.Forms;
using Majorsilence.Forms.Backends;
using Majorsilence.Forms.Wpf;

namespace Gallery.Wpf
{
    public static class Program
    {
        [STAThread]
        private static void Main ()
        {
            // WPF is not the auto-resolved default backend (Avalonia is), so install it explicitly
            // before the first window.
            Platform.Backend = new WpfPlatformBackend ();
            Application.Run (new MainForm ());
        }
    }
}
