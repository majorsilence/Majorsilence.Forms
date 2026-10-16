using System.Drawing;
using Majorsilence.Forms.Headless;
using Xunit;

namespace Majorsilence.Forms.Tests
{
    [Collection ("Headless")]
    public class MdiChildInitialPlacementTests
    {
        private static (Form parent, MdiClient client) Container ()
        {
            var parent = new Form { IsMdiContainer = true };
            HeadlessRenderer.CapturePng (parent, 900, 700);
            return (parent, parent.MdiClientControl!);
        }

        [Fact]
        public void A_default_child_opens_at_the_top_left_of_the_client_area ()
        {
            // As Win32's MDI client places a child created with the default position
            // (WindowsDefaultLocation): the cascade starts at the client's corner. Every child used to open
            // centred, whatever its StartPosition.
            var (parent, client) = Container ();
            var child = new Form { MdiParent = parent, ClientSize = new Size (300, 200) };
            child.Show ();

            var frame = client.Controls.OfType<MdiChildWindow> ().Single ();
            var area = client.DisplayRectangle;

            Assert.Equal (area.Left, frame.Left);
            Assert.Equal (area.Top, frame.Top);
        }

        [Theory]
        [InlineData (FormStartPosition.CenterParent)]
        [InlineData (FormStartPosition.CenterScreen)]
        public void A_centred_child_opens_centred_in_the_client_area (FormStartPosition position)
        {
            var (parent, client) = Container ();
            var child = new Form { MdiParent = parent, ClientSize = new Size (300, 200), StartPosition = position };
            child.Show ();

            var frame = client.Controls.OfType<MdiChildWindow> ().Single ();
            var area = client.DisplayRectangle;

            Assert.Equal (area.Left + (area.Width - frame.Width) / 2, frame.Left);
            Assert.Equal (area.Top + (area.Height - frame.Height) / 2, frame.Top);
        }

        [Fact]
        public void A_child_gets_the_client_size_it_was_designed_with ()
        {
            var (parent, client) = Container ();
            var child = new Form { MdiParent = parent, ClientSize = new Size (300, 200) };
            var top = new Button { Top = 10, Height = 20 };
            var bottom = new Button { Top = 170, Height = 20, Anchor = AnchorStyles.Bottom | AnchorStyles.Left };
            child.Controls.Add (top);
            child.Controls.Add (bottom);
            child.Show ();

            Assert.Equal (new Size (300, 200), child.ClientSize);

            // ...and its controls stay where they were designed: a bottom-anchored row did not ride up
            // when only the frame was made shorter.
            Assert.Equal (10, top.Top);
            Assert.Equal (170, bottom.Top);
        }

        // A form that calls Show () from its own Load handler is shown once. The re-entrant Show hosted it in a
        // second frame, which stayed behind -- blank, its close box dead -- after the form was closed.
        [Fact]
        public void Showing_a_child_from_its_own_Load_hosts_it_once ()
        {
            var (parent, client) = Container ();
            var child = new Form { MdiParent = parent, ClientSize = new Size (300, 200) };
            child.Load += (_, _) => child.Show ();
            child.Show ();

            Assert.Single (client.Controls.OfType<MdiChildWindow> ());

            child.Close ();
            Assert.Empty (client.Controls.OfType<MdiChildWindow> ());
        }

        // Upstream focuses the first control in tab order when an MDI child is activated with nothing focused,
        // so a newly opened child is ready for typing; here nothing had focus until the user clicked.
        [Fact]
        public void A_new_child_focuses_its_first_control_in_tab_order ()
        {
            var (parent, client) = Container ();
            var child = new Form { MdiParent = parent, ClientSize = new Size (300, 200) };
            var label = new Label { Text = "Name", TabIndex = 0 };
            var box = new TextBox { Top = 30, TabIndex = 1 };
            child.Controls.Add (label);
            child.Controls.Add (box);
            var entered = false;
            box.Enter += (_, _) => entered = true;

            child.Show ();

            Assert.Same (box, child.ActiveControl);
            Assert.True (entered);
        }

        // An MDI child with no font of its own takes its MDI parent's, as upstream's does (Font is ambient through
        // the MDI client the child sits in): a parent set to 12pt has children whose fontless controls draw at
        // 12pt. Here they fell back to the default font, so their text was visibly smaller.
        [Fact]
        public void A_child_without_a_font_inherits_the_mdi_parents ()
        {
            HeadlessRenderer.Use ();
            var parent = new Form { IsMdiContainer = true, Font = new Majorsilence.Forms.Drawing.Font ("Microsoft Sans Serif", 12f) };
            HeadlessRenderer.CapturePng (parent, 900, 700);

            var child = new Form { MdiParent = parent, ClientSize = new Size (300, 200) };
            var label = new Label { Text = "x" };
            var grid = new DataGridView { Top = 30 };
            child.Controls.Add (label);
            child.Controls.Add (grid);
            child.Show ();

            Assert.Equal (16, label.GetEffectiveFontSize ());
            Assert.Equal (16, grid.CellFallbackFont ().Size);
        }

        // MinimumSize bounds the whole window, caption included, as upstream's does. A designer form whose
        // MinimumSize sat just above its ClientSize was clamped back up when hosted, opening a caption's height
        // taller than designed. With native chrome (macOS) the caption is not in Size at all, so the minimum grew the
        // form before it was hosted and the designed client size was lost.
        [Theory]
        [InlineData (false)]
        [InlineData (true)]
        public void A_minimum_size_counts_the_caption_it_no_longer_draws (bool nativeChrome)
        {
            var (parent, client) = Container ();
            var child = new Form { UseSystemDecorations = nativeChrome };
            child.MdiParent = parent;
            child.ClientSize = new Size (300, 200);
            child.MinimumSize = new Size (300, 220);   // whole-window size: below ClientSize plus the caption
            child.Show ();

            Assert.Equal (new Size (300, 200), child.ClientSize);
        }

        // An MDI child's Size is its whole window -- caption and borders -- and ClientSize the content inside, as
        // upstream's are. Size reported the content, so an app that saved form.Size and restored it on the next
        // open grew the child by its chrome every time.
        [Fact]
        public void A_childs_size_is_its_whole_window ()
        {
            var (parent, client) = Container ();
            var child = new Form { MdiParent = parent, Size = new Size (400, 300) };
            child.Show ();

            var chrome = new Size (2 * MdiChildWindow.FrameBorder, MdiChildWindow.CaptionHeight + 2 * MdiChildWindow.FrameBorder);
            Assert.Equal (new Size (400, 300), child.Size);
            Assert.Equal (new Size (400 - chrome.Width, 300 - chrome.Height), child.ClientSize);

            // Saved and restored: the same window comes back.
            var saved = child.Size;
            child.Size = new Size (500, 350);
            child.Size = saved;
            Assert.Equal (saved, child.Size);
            Assert.Equal (saved, child.MdiHost!.Size);
        }

        // Upstream opens a Manual-positioned child at the Location assigned before Show; here every child
        // was centred regardless, so code placing a child explicitly had no effect.
        [Fact]
        public void A_manual_child_opens_at_its_assigned_location ()
        {
            var (parent, client) = Container ();
            var child = new Form {
                MdiParent = parent, ClientSize = new Size (300, 200),
                StartPosition = FormStartPosition.Manual, Location = new Point (40, 30),
            };
            child.Show ();

            var frame = client.Controls.OfType<MdiChildWindow> ().Single ();
            Assert.Equal (new Point (40, 30), frame.Location);
            Assert.Equal (new Point (40, 30), child.Location);
        }

        [Fact]
        public void Successive_children_cascade_without_leaving_the_client_area ()
        {
            var (parent, client) = Container ();

            for (var i = 0; i < 4; i++)
                new Form { MdiParent = parent, ClientSize = new Size (300, 200) }.Show ();

            var frames = client.Controls.OfType<MdiChildWindow> ().ToList ();
            var area = client.DisplayRectangle;

            Assert.Equal (4, frames.Count);

            // Distinct positions, so a stack stays individually reachable. Compared as a set rather
            // than by index: Controls order tracks z-order, not creation order.
            var origins = frames.Select (f => (f.Left, f.Top)).ToList ();
            Assert.Equal (origins.Count, origins.Distinct ().Count ());

            // ...and none pushed outside the client area.
            foreach (var f in frames) {
                Assert.True (f.Left >= 0 && f.Top >= 0, $"frame at {f.Left},{f.Top} is off the top/left");
                Assert.True (f.Left + f.Width <= area.Width, $"frame right edge {f.Left + f.Width} exceeds {area.Width}");
                Assert.True (f.Top + f.Height <= area.Height, $"frame bottom edge {f.Top + f.Height} exceeds {area.Height}");
            }
        }
    }
}
