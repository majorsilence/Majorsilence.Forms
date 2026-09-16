using System;
using System.Drawing;
using Majorsilence.Forms.Headless;
using Xunit;

namespace Majorsilence.Forms.Tests
{
    // Written to settle a question rather than to fix a known bug.
    //
    // LAY-38 shipped a regression where ListView's hit-tests compared a LOGICAL mouse point against
    // DEVICE item bounds, so clicking an item selected nothing at any display scale other than 1.
    // Review and CI both passed it: the MF_HEADLESS_SCALE=2 gate only catches what a test exercises,
    // and nothing drove a ListView click.
    //
    // Ribbon.GetItemAtLocation has the same shape -- a logical e.Location tested against item.Bounds,
    // which RibbonRenderer treats as device (it adds device-scaled padding to them) -- and Ribbon had
    // no tests at all. MenuBase reads the same way and turned out to be CORRECT, so the only way to
    // know is to drive a real click at a scale other than 1 and watch.
    //
    // Application.UiScale multiplies into the window's scale factor, so these assert in every gate
    // configuration rather than only under MF_HEADLESS_SCALE=2.
    [Collection ("Headless")]
    public sealed class RibbonHitTestTests : IDisposable
    {
        private readonly double original = Application.UiScale;

        public RibbonHitTestTests () => HeadlessRenderer.Use ();

        public void Dispose () => Application.UiScale = original;

        // `scale` is the EFFECTIVE scale wanted, not the value to assign. Application.UiScale
        // multiplies with the backend's own factor, so assigning 2 under MF_HEADLESS_SCALE=2 gives 4
        // and assigning 1 gives 2 -- which made the first version of this fixture fail in exactly the
        // gate it was written for. Dividing the desktop scaling out pins the effective scale in every
        // configuration.
        private static Ribbon Populated (double scale, out Form form)
        {
            Application.UiScale = 1;

            using (var probe = new Form ())
                Application.UiScale = probe.DesktopScaling > 0 ? scale / probe.DesktopScaling : scale;

            var ribbon = new Ribbon { Width = 600, Height = 140 };
            var tab = ribbon.TabPages.Add ("Home");
            var group = tab.Groups.Add ("Actions");

            group.Items.Add ("New");
            group.Items.Add ("Open");

            form = new Form { Width = 700, Height = 300 };
            form.Controls.Add (ribbon);
            form.Show ();

            // Laid out on paint, like every other item-owning control here.
            PaintSurface.Render (ribbon).Dispose ();

            return ribbon;
        }

        private static MenuItem FirstItem (Ribbon ribbon)
            => ribbon.TabPages[0].Groups[0].Items[0];

        [Fact]
        public void A_click_at_scale_one_reaches_the_item_under_it ()
        {
            // The control case. If this fails the fixture is wrong, not the hit-test, and the scaled
            // test below would be meaningless.
            var ribbon = Populated (1, out var form);

            try {
                var item = FirstItem (ribbon);
                var clicked = 0;
                item.Click += (_, _) => clicked++;

                var bounds = item.Bounds;
                ribbon.DriveClick (new Point (bounds.Left + bounds.Width / 2, bounds.Top + bounds.Height / 2));

                Assert.Equal (1, clicked);
            } finally {
                form.Close ();
            }
        }

        [Fact]
        public void A_click_reaches_the_item_under_it_when_scaled ()
        {
            // The question this file exists to answer. A logical point aimed at the item's own
            // rectangle has to reach it whatever the display scale.
            var ribbon = Populated (2, out var form);

            try {
                // The SECOND item, and asserting WHICH item fires. Two earlier versions of this test
                // passed without proving anything: one built the click from item.Bounds directly (those
                // bounds are device, so the point matched them whatever the hit-test did), and one used
                // the first item, whose logical centre happens to fall inside its own device rectangle
                // because that rectangle is tall and near the origin. Only a point whose logical and
                // device readings land on DIFFERENT items can tell a correct hit-test from a broken one.
                Assert.True (ribbon.DeviceDpi > 96, "the fixture must actually be scaled");

                var first = FirstItem (ribbon);
                var second = ribbon.TabPages[0].Groups[0].Items[1];
                var firstClicks = 0;
                var secondClicks = 0;
                first.Click += (_, _) => firstClicks++;
                second.Click += (_, _) => secondClicks++;

                var logical = ribbon.DeviceToLogicalUnits (second.Bounds);
                var point = new Point (logical.Left + logical.Width / 2, logical.Top + logical.Height / 2);

                Assert.True (first.Bounds.Contains (point),
                    "the fixture must be one where the logical point reads as the WRONG item in device space");

                ribbon.DriveClick (point);

                Assert.Equal (1, secondClicks);
                Assert.Equal (0, firstClicks);
            } finally {
                form.Close ();
            }
        }
    }
}
