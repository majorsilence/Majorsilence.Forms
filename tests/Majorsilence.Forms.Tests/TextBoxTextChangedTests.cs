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
    }
}
