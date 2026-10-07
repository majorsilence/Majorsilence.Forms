using System.Drawing;
using Majorsilence.Forms.Headless;
using Xunit;

namespace Majorsilence.Forms.Tests
{
    // Upstream underlines access keys only once the user has pressed Alt; until then "&File" draws as
    // "File". An app that has not chosen a font keeps the theme's look, with the underline always shown.
    [Collection ("Headless")]
    public class KeyboardCuesTests
    {
        private sealed class CueLabel : Label
        {
            internal bool Cues => ShowKeyboardCues;
        }

        private static (Form form, CueLabel label) Shell ()
        {
            HeadlessRenderer.Use ();
            var form = new Form { ClientSize = new Size (300, 200) };
            var label = new CueLabel { Text = "&Name:", AutoSize = true };
            form.Controls.Add (label);
            form.Show ();
            return (form, label);
        }

        [Fact]
        public void Without_a_chosen_font_access_keys_are_always_underlined ()
        {
            var (form, label) = Shell ();

            using (form) {
                Assert.True (form.ShowKeyboardCues);
                Assert.True (label.Cues);
            }
        }

        [Fact]
        public void With_a_chosen_font_access_keys_appear_when_Alt_is_pressed ()
        {
            try {
                Application.SetDefaultFont (new Majorsilence.Forms.Drawing.Font (Control.DefaultFont.Name, 9f));
                var (form, label) = Shell ();

                using (form) {
                    Assert.False (form.ShowKeyboardCues);
                    Assert.False (label.Cues);

                    HeadlessRenderer.KeyDown (form, Keys.Menu);

                    Assert.True (form.ShowKeyboardCues);
                    Assert.True (label.Cues);
                }
            } finally {
                Application.SetDefaultFont (null!);
            }
        }

        [Fact]
        public void An_MDI_child_follows_its_parent ()
        {
            try {
                Application.SetDefaultFont (new Majorsilence.Forms.Drawing.Font (Control.DefaultFont.Name, 9f));
                HeadlessRenderer.Use ();
                using var parent = new Form { IsMdiContainer = true, ClientSize = new Size (600, 400) };
                parent.Show ();
                var child = new Form { MdiParent = parent, ClientSize = new Size (200, 100) };
                child.Show ();

                Assert.False (child.ShowKeyboardCues);

                HeadlessRenderer.KeyDown (parent, Keys.Menu);

                Assert.True (child.ShowKeyboardCues);
            } finally {
                Application.SetDefaultFont (null!);
            }
        }
    }
}
