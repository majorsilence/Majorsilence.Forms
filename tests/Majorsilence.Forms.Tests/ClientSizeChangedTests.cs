using System.Drawing;
using Majorsilence.Forms.Headless;
using Xunit;

namespace Majorsilence.Forms.Tests
{
    // W6.1, the dead-event sweep (RC-5). ClientSizeChanged was declared with a working raiser that
    // nothing called -- the raise had been PORTED and then commented out, next to a `// TESTING` line
    // someone never restored:
    //
    //     OnSizeChanged (EventArgs.Empty);
    //     // OnClientSizeChanged (EventArgs.Empty);
    //     //PerformLayout (this, nameof (Bounds)); // TESTING
    //
    // dotnet/winforms' Control.SetBoundsCore has exactly that `if (newSize)` block, in that order,
    // with both raises live. This is the first event the reachability fix (#222) surfaced to be wired:
    // the old gate could not see it, because OnClientSizeChanged reads the backing field and that
    // counted as "raised".
    [Collection ("Headless")]
    public class ClientSizeChangedTests
    {
        [Fact]
        public void Resizing_a_control_raises_ClientSizeChanged ()
        {
            HeadlessRenderer.Use ();

            using var form = new Form { Width = 400, Height = 300 };
            var panel = new Panel { Width = 100, Height = 50 };
            form.Controls.Add (panel);
            form.Show ();

            var raised = 0;
            panel.ClientSizeChanged += (_, _) => raised++;

            panel.Size = new Size (150, 80);

            Assert.Equal (1, raised);
        }

        // ClientSize's setter routes through Size, which is upstream's other raise site
        // (SetClientSizeCore). It has no separate implementation here, so the one raise has to serve
        // both paths -- if it did not, the property most obviously connected to the event would be
        // the one that did not fire it.
        [Fact]
        public void Setting_ClientSize_raises_it_too ()
        {
            HeadlessRenderer.Use ();

            using var form = new Form { Width = 400, Height = 300 };
            var panel = new Panel { Width = 100, Height = 50 };
            form.Controls.Add (panel);
            form.Show ();

            var raised = 0;
            panel.ClientSizeChanged += (_, _) => raised++;

            panel.ClientSize = new Size (140, 90);

            Assert.True (raised > 0, "Setting ClientSize did not raise ClientSizeChanged.");
        }

        // Paired with SizeChanged, which was already raised from the same block: both fire, and the
        // new one must not have displaced the old.
        [Fact]
        public void It_is_raised_alongside_SizeChanged_not_instead_of_it ()
        {
            HeadlessRenderer.Use ();

            using var form = new Form { Width = 400, Height = 300 };
            var panel = new Panel { Width = 100, Height = 50 };
            form.Controls.Add (panel);
            form.Show ();

            var order = string.Empty;
            panel.SizeChanged += (_, _) => order += "S";
            panel.ClientSizeChanged += (_, _) => order += "C";

            panel.Size = new Size (150, 80);

            Assert.Equal ("SC", order);
        }

        // A move is not a resize. The raise sits inside `if (newSize)`, so relocating the control
        // must not fire it -- the guard is what makes the event mean something.
        [Fact]
        public void Moving_a_control_does_not_raise_it ()
        {
            HeadlessRenderer.Use ();

            using var form = new Form { Width = 400, Height = 300 };
            var panel = new Panel { Width = 100, Height = 50, Left = 10, Top = 10 };
            form.Controls.Add (panel);
            form.Show ();

            var raised = 0;
            panel.ClientSizeChanged += (_, _) => raised++;

            panel.Left = 40;
            panel.Top = 40;

            Assert.Equal (0, raised);
        }

        // And setting the same size again is not a change.
        [Fact]
        public void Re_setting_the_same_size_does_not_raise_it ()
        {
            HeadlessRenderer.Use ();

            using var form = new Form { Width = 400, Height = 300 };
            var panel = new Panel { Width = 100, Height = 50 };
            form.Controls.Add (panel);
            form.Show ();

            var raised = 0;
            panel.ClientSizeChanged += (_, _) => raised++;

            panel.Size = new Size (100, 50);

            Assert.Equal (0, raised);
        }
    }
}
