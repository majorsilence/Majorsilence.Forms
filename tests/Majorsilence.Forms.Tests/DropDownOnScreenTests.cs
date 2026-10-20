using System;
using System.Drawing;
using Majorsilence.Forms.Backends;
using Majorsilence.Forms.Headless;
using Xunit;

namespace Majorsilence.Forms.Tests
{
    // A drop-down opened from a button near the screen's right edge stayed where it was put and ran off it (a phone's
    // toolbar: Recital's Sync menu). It is now pulled back inside the working area, as WinForms' drop-downs are.
    [Collection ("Headless")]
    public sealed class DropDownOnScreenTests : IDisposable
    {
        public DropDownOnScreenTests () => HeadlessRenderer.Use ();

        public void Dispose () => Headless.Screens = null;

        private static HeadlessPlatformBackend Headless => (HeadlessPlatformBackend) Platform.Backend;

        [Fact]
        public void A_drop_down_at_the_right_edge_stays_on_the_screen ()
        {
            var phone = new Rectangle (0, 0, 411, 880);
            Headless.Screens = [new ScreenInfo ("Phone", phone, phone, isPrimary: true)];
            using var form = new Form { Location = Point.Empty, Size = phone.Size, StartPosition = FormStartPosition.Manual };
            var strip = new ToolStrip ();
            var sync = new ToolStripDropDownButton ("Sync") { Alignment = ToolStripItemAlignment.Right };
            foreach (var text in new[] { "WebDAV…", "Google Drive…", "Dropbox…", "OneDrive…", "Sync now", "Stop syncing" })
                sync.DropDownItems.Add (new ToolStripMenuItem (text));
            strip.Items.Add (sync);
            form.Controls.Add (strip);
            form.Show ();
            using var painted = PaintSurface.Render (strip);   // lays the strip out: Sync at the right edge

            sync.ShowDropDown ();
            try {
                var shown = sync.OpenDropDown!;
                var at = shown.LastShowLocation;
                Assert.True (at.X >= 0 && at.X + shown.Width <= phone.Right, $"the drop-down spans {at.X}..{at.X + shown.Width} on a {phone.Width}-wide screen");
            } finally {
                sync.HideDropDown ();
            }
        }
    }
}
