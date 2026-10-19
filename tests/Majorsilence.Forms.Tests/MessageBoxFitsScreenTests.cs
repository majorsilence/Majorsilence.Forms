using System;
using System.Drawing;
using System.Linq;
using Majorsilence.Forms.Backends;
using Majorsilence.Forms.Headless;
using Xunit;

namespace Majorsilence.Forms.Tests
{
    // A message box wrapped its text at 560 units whatever the screen, so on a phone (about 410 across) it ran
    // off the right edge with its buttons. It now wraps within the owner's screen, and stays on it.
    [Collection ("Headless")]
    public sealed class MessageBoxFitsScreenTests : IDisposable
    {
        public MessageBoxFitsScreenTests () => HeadlessRenderer.Use ();

        public void Dispose () => Headless.Screens = null;

        private static HeadlessPlatformBackend Headless => (HeadlessPlatformBackend) Platform.Backend;

        [Fact]
        public void A_long_message_on_a_phone_sized_screen_stays_on_it ()
        {
            var phone = new Rectangle (0, 0, 411, 880);
            Headless.Screens = [new ScreenInfo ("Phone", phone, new Rectangle (0, 24, 411, 832), isPrimary: true)];
            using var owner = new Form { Location = Point.Empty, Size = phone.Size, StartPosition = FormStartPosition.Manual };
            owner.Show ();

            var answer = MessageBox.ShowAsync (owner,
                "Download Recital's voices?\n\nAbout 117 MB, once, from huggingface.co (the Kokoro voice model). Nothing about you or your books is sent, and afterwards they work offline.",
                "Recital voices", MessageBoxButtons.OKCancel);
            var box = Application.OpenForms.OfType<MessageBoxForm> ().Single ();
            try {
                var bounds = box.ScreenBounds;
                Assert.True (bounds.Width <= 411, $"the box is {bounds.Width} wide on a 411 screen");
                Assert.True (bounds.Left >= 0 && bounds.Right <= 411, $"the box spans {bounds.Left}..{bounds.Right}");
                var buttons = Descendants (box.Controls.Cast<Control> ()).OfType<Button> ().ToList ();
                Assert.Equal (2, buttons.Count);
                Assert.All (buttons, b => Assert.True (box.PointToClient (b.PointToScreen (new Point (b.Width, 0))).X <= box.ClientSize.Width,
                    $"'{b.Text}' runs past the {box.ClientSize.Width}-wide box"));
            } finally {
                box.Close ();
            }
            Assert.True (answer.IsCompleted);
        }

        private static System.Collections.Generic.IEnumerable<Control> Descendants (System.Collections.Generic.IEnumerable<Control> controls)
            => controls.SelectMany (c => new[] { c }.Concat (Descendants (c.Controls.Cast<Control> ())));

        // MessageBox.ShowAsync (text) with no owner, as an app on a single view calls it: the box fits the screen.
        [Fact]
        public void Without_an_owner_it_fits_the_screen_too ()
        {
            var phone = new Rectangle (0, 0, 411, 880);
            Headless.Screens = [new ScreenInfo ("Phone", phone, new Rectangle (0, 24, 411, 832), isPrimary: true)];
            using var main = new Form { Location = Point.Empty, Size = phone.Size, StartPosition = FormStartPosition.Manual };
            main.Show ();
            Cursor.Position = new Point (200, 400);

            _ = MessageBox.ShowAsync (string.Join (" ", Enumerable.Repeat ("A sentence that goes on.", 12)), "Long", MessageBoxButtons.OKCancel);
            var box = Application.OpenForms.OfType<MessageBoxForm> ().Single ();
            try {
                Assert.True (box.ScreenBounds.Width <= 411, $"the box is {box.ScreenBounds.Width} wide on a 411 screen");
            } finally {
                box.Close ();
            }
        }

        [Fact]
        public void A_desktop_screen_keeps_the_usual_width ()
        {
            using var owner = new Form ();
            owner.Show ();
            var text = string.Join (" ", Enumerable.Repeat ("word", 200));
            _ = MessageBox.ShowAsync (owner, text, "Wide");
            var box = Application.OpenForms.OfType<MessageBoxForm> ().Single ();
            try {
                Assert.True (box.ClientSize.Width > 500, $"{box.ClientSize.Width}");
            } finally {
                box.Close ();
            }
        }
    }
}
