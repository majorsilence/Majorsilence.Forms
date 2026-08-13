using Xunit;

namespace Majorsilence.Forms.Tests
{
    // AutoSize measured nothing: GetPreferredSize returned the size the label already had, so an
    // auto-sized label kept whatever the designer left on it. That is not a cosmetic difference --
    // an over-wide label is opaque to the mouse, so it swallows the clicks of the container beneath
    // it (a custom title bar, a toolbar strip) and the container stops responding.
    public class LabelAutoSizeTests
    {
        // WinForms' Label.AutoSize property defaults to false; the designer opts new labels in
        // explicitly, which is why generated code always carries "AutoSize = true".
        [Fact]
        public void AutoSize_defaults_to_false ()
        {
            using var label = new Label ();

            Assert.False (label.AutoSize);
        }

        [Fact]
        public void PreferredSize_measures_the_text_rather_than_the_current_size ()
        {
            using var panel = new Panel { Width = 400, Height = 100 };
            var label = new Label { Bounds = new System.Drawing.Rectangle (0, 0, 300, 80), Text = "Hi" };
            panel.Controls.Add (label);

            var preferred = label.GetPreferredSize (System.Drawing.Size.Empty);

            Assert.True (preferred.Width < 300, $"preferred width should reflect the text, not the 300px bounds (got {preferred.Width})");
            Assert.True (preferred.Height < 80, $"preferred height should reflect the text, not the 80px bounds (got {preferred.Height})");
            Assert.True (preferred.Width > 0 && preferred.Height > 0);
        }

        // The case designer code actually produces: AutoSize = true, then the size the label happened
        // to have at design time. The layout engine's auto-size pass only grows, so this needs the
        // label's own AdjustSize to shrink it back.
        [Fact]
        public void An_autosized_label_shrinks_to_its_text ()
        {
            using var panel = new Panel { Width = 400, Height = 100 };
            var label = new Label ();
            label.AutoSize = true;
            label.Size = new System.Drawing.Size (300, 80);
            label.Text = "Hi";
            panel.Controls.Add (label);

            Assert.True (label.Width < 300, $"label should have shrunk to its text (width {label.Width})");
            Assert.True (label.Height < 80, $"label should have shrunk to its text (height {label.Height})");
        }

        [Fact]
        public void An_autosized_label_grows_with_longer_text ()
        {
            using var panel = new Panel { Width = 600, Height = 100 };
            var label = new Label { AutoSize = true, Text = "Hi" };
            panel.Controls.Add (label);

            var narrow = label.Width;
            label.Text = "A considerably longer caption than before";

            Assert.True (label.Width > narrow, $"label should have grown ({narrow} -> {label.Width})");
        }

        [Fact]
        public void A_label_that_is_not_autosized_keeps_the_size_it_was_given ()
        {
            using var panel = new Panel { Width = 400, Height = 100 };
            var label = new Label { AutoSize = false, Text = "Hi", Bounds = new System.Drawing.Rectangle (0, 0, 270, 29) };
            panel.Controls.Add (label);

            Assert.Equal (270, label.Width);
            Assert.Equal (29, label.Height);

            label.Text = "Something else entirely";

            Assert.Equal (270, label.Width);
            Assert.Equal (29, label.Height);
        }

        // Nothing to measure must not collapse the label to nothing -- an empty auto-sized label is a
        // common placeholder that later gets text.
        [Fact]
        public void An_empty_autosized_label_keeps_its_size ()
        {
            using var panel = new Panel { Width = 400, Height = 100 };
            var label = new Label { AutoSize = true, Bounds = new System.Drawing.Rectangle (0, 0, 270, 29) };
            panel.Controls.Add (label);

            Assert.Equal (270, label.Width);
            Assert.Equal (29, label.Height);
        }
    }
}
