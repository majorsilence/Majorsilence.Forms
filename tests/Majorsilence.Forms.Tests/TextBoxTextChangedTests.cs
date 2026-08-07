using Xunit;

namespace Majorsilence.Forms.Tests
{
    // WinForms parity: TextBox raises TextChanged on every content change -- a programmatic Text set and
    // each edit -- and not when Text is set to the same value. The fork's overridden Text setter wrote
    // straight to the backing document, which only invalidated (repainted) and never raised TextChanged.
    public class TextBoxTextChangedTests
    {
        [Fact]
        public void Setting_Text_raises_TextChanged_once_per_change ()
        {
            using var tb = new TextBox ();
            var count = 0;
            tb.TextChanged += (s, e) => count++;

            tb.Text = "hello";
            Assert.Equal (1, count);

            tb.Text = "hello";       // same value -> no event
            Assert.Equal (1, count);

            tb.Text = "world";
            Assert.Equal (2, count);
        }

        [Fact]
        public void Editing_text_raises_TextChanged ()
        {
            using var tb = new TextBox ();
            var count = 0;
            tb.TextChanged += (s, e) => count++;

            tb.AppendText ("a");
            Assert.True (count >= 1);
            Assert.Equal ("a", tb.Text);
        }

        // Regression: the tests above drive the Text setter, which always raised the event. Real user
        // input takes a different route -- the document's own insert/remove -- and that route never
        // raised it at all, so typing, Enter and Backspace changed the text silently. An editor's dirty
        // flag hangs off this event, so it stayed false no matter what was typed.
        [Fact]
        public void Typing_raises_TextChanged ()
        {
            using var form = new Form ();
            var tb = new TextBox { Multiline = true, Dock = DockStyle.Fill };
            form.Controls.Add (tb);
            Headless.HeadlessRenderer.CapturePng (form, 300, 200);
            tb.Select ();

            var count = 0;
            tb.TextChanged += (s, e) => count++;

            Headless.HeadlessRenderer.TextInput (form, "a");

            Assert.Equal (1, count);
            Assert.Equal ("a", tb.Text);
        }

        [Fact]
        public void Enter_raises_TextChanged ()
        {
            using var form = new Form ();
            var tb = new TextBox { Multiline = true, Dock = DockStyle.Fill };
            form.Controls.Add (tb);
            Headless.HeadlessRenderer.CapturePng (form, 300, 200);
            tb.Select ();
            Headless.HeadlessRenderer.TextInput (form, "a");

            var count = 0;
            tb.TextChanged += (s, e) => count++;

            Headless.HeadlessRenderer.KeyDown (form, Keys.Return);

            Assert.Equal (1, count);
            Assert.Equal ("a\n", tb.Text);
        }

        [Fact]
        public void Backspace_raises_TextChanged ()
        {
            using var form = new Form ();
            var tb = new TextBox { Multiline = true, Dock = DockStyle.Fill };
            form.Controls.Add (tb);
            Headless.HeadlessRenderer.CapturePng (form, 300, 200);
            tb.Select ();
            Headless.HeadlessRenderer.TextInput (form, "ab");

            var count = 0;
            tb.TextChanged += (s, e) => count++;

            Headless.HeadlessRenderer.KeyDown (form, Keys.Back);

            Assert.Equal (1, count);
            Assert.Equal ("a", tb.Text);
        }
    }
}
