using Majorsilence.Forms.Headless;
using Xunit;

namespace Majorsilence.Forms.Tests
{
    // W6.2. Seven baseline entries closed by one change, and they were one fact: ScrollPropertiesBase
    // was a parallel, DEAD copy of ScrollProperties -- seven auto-properties with no connection to any
    // scrollbar -- sitting beside the live ScrollProperties, which forwards to a real one and is what
    // ScrollableControl.HorizontalScroll/VerticalScroll return.
    //
    // So the WinForms idiom worked while anything typed as HScrollProperties/VScrollProperties silently
    // did nothing. RC-6 ("prefer deleting a private twin over keeping both"), and upstream settles the
    // shape: it has ScrollProperties with HScrollProperties/VScrollProperties derived from it, and no
    // ScrollPropertiesBase at all.
    [Collection ("Headless")]
    public class ScrollPropertiesTests
    {
        private static Panel Scrollable (out Form form)
        {
            HeadlessRenderer.Use ();

            var panel = new Panel { Width = 200, Height = 150, AutoScroll = true };
            panel.Controls.Add (new Panel { Left = 0, Top = 0, Width = 400, Height = 400 });

            form = new Form { Width = 300, Height = 250 };
            form.Controls.Add (panel);
            form.Show ();
            PaintSurface.Render (panel).Dispose ();

            return panel;
        }

        [Fact]
        public void The_scroll_properties_are_the_live_ones ()
        {
            // The whole defect in one assertion: the types an application declares a variable as are
            // now the types that actually reach a scrollbar.
            using var panel = Scrollable (out var form);

            try {
                HScrollProperties horizontal = panel.HorizontalScroll;
                VScrollProperties vertical = panel.VerticalScroll;

                Assert.IsAssignableFrom<ScrollProperties> (horizontal);
                Assert.IsAssignableFrom<ScrollProperties> (vertical);
            } finally {
                form.Close ();
            }
        }

        [Fact]
        public void Setting_Value_through_the_properties_scrolls ()
        {
            using var panel = Scrollable (out var form);

            try {
                var before = panel.VerticalScroll.Value;

                panel.VerticalScroll.Value = 40;

                Assert.NotEqual (before, panel.VerticalScroll.Value);
                Assert.Equal (40, panel.VerticalScroll.Value);
            } finally {
                form.Close ();
            }
        }

        [Fact]
        public void The_properties_read_the_scrollbar_rather_than_a_copy ()
        {
            // Each access returns a fresh wrapper, so a value written through one must be visible
            // through the next -- which is only true if both forward to the same scrollbar. A stored
            // copy would answer from whichever instance happened to be asked.
            using var panel = Scrollable (out var form);

            try {
                panel.VerticalScroll.Value = 25;

                Assert.Equal (25, panel.VerticalScroll.Value);
                Assert.Equal (25, panel.VerticalScrollProperties.Value);
            } finally {
                form.Close ();
            }
        }

        [Fact]
        public void Every_member_reaches_the_scrollbar ()
        {
            // All seven were on the baseline together, so all seven are asserted together: a partial
            // forward would close some entries and leave the rest looking closed.
            //
            // AutoScroll is OFF here deliberately. With it on, the layout owns the scrollbars and
            // recomputes Maximum, LargeChange, SmallChange and Visible from the content on the next
            // pass -- so a test that set them and read back later would be measuring AutoScroll rather
            // than the forwarding this change is about. (Found by watching three of them come back
            // with the layout's numbers instead of mine.)
            HeadlessRenderer.Use ();

            using var form = new Form { Width = 300, Height = 250 };
            var panel = new Panel { Width = 200, Height = 150 };
            form.Controls.Add (panel);
            form.Show ();

            try {
                // Each member is set and read back through a FRESH wrapper immediately. That is what
                // proves the write reached the scrollbar rather than an instance field -- two wrappers
                // agree only if both forward to the same one.
                //
                // Deliberately not re-read after a layout pass: on an AutoScroll panel the layout owns
                // the range and recomputes Maximum, LargeChange and SmallChange from the content, so a
                // later reading legitimately differs. That is the control's behaviour, and asserting
                // against it would be testing AutoScroll, not the forwarding this change is about.
                panel.VerticalScroll.Maximum = 500;
                Assert.Equal (500, panel.VerticalScroll.Maximum);

                panel.VerticalScroll.LargeChange = 42;
                Assert.Equal (42, panel.VerticalScroll.LargeChange);

                panel.VerticalScroll.SmallChange = 7;
                Assert.Equal (7, panel.VerticalScroll.SmallChange);

                panel.VerticalScroll.Minimum = 0;
                Assert.Equal (0, panel.VerticalScroll.Minimum);

                panel.VerticalScroll.Value = 33;
                Assert.Equal (33, panel.VerticalScroll.Value);

                panel.VerticalScroll.Enabled = false;
                Assert.False (panel.VerticalScroll.Enabled);

                panel.VerticalScroll.Enabled = true;
                Assert.True (panel.VerticalScroll.Enabled);

                // NOT a discriminating assertion, and labelled rather than dressed up: the control's
                // layout owns the bar's visibility in BOTH directions, so a plain panel's bar stays
                // hidden whether or not the write reaches it. Visible forwards in the same one-line
                // way as the six above; this line records that it was exercised, not that it was
                // proved. The other six carry the proof.
                panel.VerticalScroll.Visible = false;
                Assert.False (panel.VerticalScroll.Visible);

            } finally {
                form.Close ();
            }
        }

        [Fact]
        public void The_horizontal_and_vertical_properties_are_different_bars ()
        {
            // GUARD: both wrappers take a ScrollBar, so handing the same one to each would satisfy
            // every assertion above while making the two axes the same control.
            using var panel = Scrollable (out var form);

            try {
                panel.HorizontalScroll.Value = 15;
                panel.VerticalScroll.Value = 60;

                Assert.Equal (15, panel.HorizontalScroll.Value);
                Assert.Equal (60, panel.VerticalScroll.Value);
            } finally {
                form.Close ();
            }
        }

        [Fact]
        public void ParentControl_finds_the_control_the_bar_belongs_to ()
        {
            using var panel = Scrollable (out var form);

            try {
                Assert.Same (panel, panel.VerticalScroll.ParentControl);
            } finally {
                form.Close ();
            }
        }
    }
}
