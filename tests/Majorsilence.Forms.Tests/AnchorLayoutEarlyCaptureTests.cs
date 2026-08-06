using Majorsilence.Forms.Headless;
using Xunit;

namespace Majorsilence.Forms.Tests
{
    // A control gets its Anchor/Location/Size assigned (in that order, as generated designer code
    // does) before it is parented, then added to a Form whose own size isn't fully established yet
    // -- both true of ordinary designer-generated InitializeComponent methods. The anchor engine's
    // very first real capture of "distance from parent edges" can land while the parent's
    // DisplayRectangle still reports a degenerate size, before the parent's own size has propagated.
    // A later redundant re-init (harmless on its own) must not treat "this element's own bounds
    // haven't changed" as license to skip forever once the parent's real size does show up --
    // otherwise the next real anchor stretch computes from a garbage snapshot.
    public class AnchorLayoutEarlyCaptureTests
    {
        [Fact]
        public void Anchored_child_does_not_balloon_after_an_early_degenerate_capture ()
        {
            HeadlessRenderer.Use ();

            using var form = new Form ();
            var anchored = new Panel {
                Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right,
                Location = new System.Drawing.Point (10, 10),
                Size = new System.Drawing.Size (200, 200),
            };
            form.Controls.Add (anchored);
            // Only now does the form reach its real, final size -- mirrors designer code that adds
            // children before the trailing `Me.ClientSize = ...` line runs.
            form.ClientSize = new System.Drawing.Size (400, 400);

            form.Show ();
            HeadlessRenderer.CapturePng (form);
            HeadlessRenderer.CapturePng (form); // second cycle: let layout settle

            Assert.True (anchored.Width <= 380 && anchored.Height <= 380,
                $"Anchored child ballooned to {anchored.Size} inside a 400x400 form " +
                "(expected roughly 380x380 for a 10px margin on every side) -- its anchor " +
                "deltas were likely captured against the parent's pre-ClientSize (0x0) rectangle.");
        }
    }
}
