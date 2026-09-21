using System.Drawing;
using Majorsilence.Forms.Headless;
using Xunit;

namespace Majorsilence.Forms.Tests
{
    // W6.2, the stored-only sweep (RC-7) -- the ToolStripLabel link family (TSM-42). Six consecutive
    // baseline entries, one cause: nothing in the renderer had ever heard of IsLink, so a label built
    // as a hyperlink drew as a plain caption and all six were indistinguishable from each other.
    //
    //   IsLink            the switch; read by nothing at all
    //   LinkColor         the normal colour
    //   VisitedLinkColor  used when LinkVisited
    //   LinkVisited       picks between the two above
    //   LinkBehavior      whether the underline is always / on hover / never
    //   ActiveLinkColor   NOT wired -- see the last test
    //
    // These were reachable only because the framework-written marker stopped counting constructor
    // writes; the four with initialisers had been classified as outbound state.
    [Collection ("Headless")]
    public class ToolStripLabelLinkTests
    {
        private static ToolStripLabel Label (out Form form, out ToolStrip strip)
        {
            HeadlessRenderer.Use ();

            var label = new ToolStripLabel ("Open the manual");

            strip = new ToolStrip { Width = 300, Height = 30 };
            strip.Items.Add (label);

            form = new Form { Width = 400, Height = 200 };
            form.Controls.Add (strip);
            form.Show ();

            return label;
        }

        [Fact]
        public void IsLink_changes_how_the_label_is_drawn ()
        {
            var label = Label (out var form, out var strip);

            using (form) {
                var plain = Ink (strip, label);

                label.IsLink = true;

                Assert.NotEqual (plain, Ink (strip, label));
            }
        }

        [Fact]
        public void LinkColor_is_what_gets_drawn ()
        {
            var label = Label (out var form, out var strip);

            using (form) {
                label.IsLink = true;
                label.LinkColor = Color.Red;

                Assert.True (HasPixel (strip, label, p => p.Red > 180 && p.Green < 90 && p.Blue < 90),
                    "No red pixel in the label -- LinkColor was not used.");
            }
        }

        // The visited colour is a separate property AND a separate flag; a renderer that read only
        // LinkColor would pass the test above and still show every visited link as unvisited.
        [Fact]
        public void LinkVisited_switches_to_VisitedLinkColor ()
        {
            var label = Label (out var form, out var strip);

            using (form) {
                label.IsLink = true;
                label.LinkColor = Color.Red;
                label.VisitedLinkColor = Color.Lime;

                Assert.True (HasPixel (strip, label, p => p.Red > 180 && p.Green < 90 && p.Blue < 90));

                label.LinkVisited = true;

                Assert.True (HasPixel (strip, label, p => p.Green > 180 && p.Red < 90 && p.Blue < 90),
                    "A visited link did not switch to VisitedLinkColor.");
                Assert.False (HasPixel (strip, label, p => p.Red > 180 && p.Green < 90 && p.Blue < 90),
                    "The unvisited colour is still being drawn.");
            }
        }

        // AlwaysUnderline vs NeverUnderline, measured in the row of pixels under the text where only
        // an underline can appear.
        [Fact]
        public void LinkBehavior_controls_the_underline ()
        {
            var label = Label (out var form, out var strip);

            using (form) {
                label.IsLink = true;
                label.LinkBehavior = LinkBehavior.AlwaysUnderline;
                var underlined = Ink (strip, label);

                label.LinkBehavior = LinkBehavior.NeverUnderline;
                var bare = Ink (strip, label);

                Assert.True (underlined > bare, $"NeverUnderline drew as much ink ({bare}) as AlwaysUnderline ({underlined}).");
            }
        }

        // SystemDefault is the DEFAULT value of the property, so if it were treated as "no underline"
        // the common case would silently be the wrong one.
        [Fact]
        public void SystemDefault_underlines ()
        {
            var label = Label (out var form, out var strip);

            using (form) {
                label.IsLink = true;

                Assert.Equal (LinkBehavior.SystemDefault, label.LinkBehavior);
                var system_default = Ink (strip, label);

                label.LinkBehavior = LinkBehavior.NeverUnderline;

                Assert.True (system_default > Ink (strip, label), "SystemDefault did not underline.");
            }
        }

        // HoverUnderline is the one behaviour that depends on state the strip actually tracks, which
        // is why it can be tested and ActiveLinkColor cannot.
        [Fact]
        public void HoverUnderline_only_underlines_under_the_pointer ()
        {
            var label = Label (out var form, out var strip);

            using (form) {
                label.IsLink = true;

                // BOTH samples are taken hovered. Comparing hovered against unhovered is what the
                // first version did, and it passed with the whole link feature removed -- hovering
                // swaps the item's background style, so the ink count moved for a reason that has
                // nothing to do with the underline.
                label.Hovered = true;

                label.LinkBehavior = LinkBehavior.NeverUnderline;
                var bare = Ink (strip, label);

                label.LinkBehavior = LinkBehavior.HoverUnderline;
                var underlined = Ink (strip, label);

                Assert.True (underlined > bare, "A hovered HoverUnderline link was not underlined.");

                // And it must not underline when the pointer is away, which is the whole distinction
                // from AlwaysUnderline.
                label.Hovered = false;
                var away = Ink (strip, label);

                label.LinkBehavior = LinkBehavior.AlwaysUnderline;

                Assert.True (Ink (strip, label) > away, "HoverUnderline underlined with no pointer on it.");
            }
        }

        // A disabled link is drawn disabled, not blue-and-underlined: it advertises an action that
        // cannot be taken. The guard matters because the link colour is resolved before the enabled
        // check in the obvious implementation, and that ordering is easy to get backwards.
        [Fact]
        public void A_disabled_link_does_not_draw_as_one ()
        {
            var label = Label (out var form, out var strip);

            using (form) {
                label.LinkColor = Color.Red;
                label.Enabled = false;

                label.IsLink = false;
                var plain = Ink (strip, label);

                label.IsLink = true;

                Assert.False (HasPixel (strip, label, p => p.Red > 180 && p.Green < 90 && p.Blue < 90),
                    "A disabled link still drew in its link colour.");

                // And the UNDERLINE too, which the colour assertion alone does not cover: the colour
                // is guarded a second time by the enabled check in the font-colour expression, so a
                // renderer that forgot the guard here would still pass on colour and quietly underline
                // a link that cannot be clicked.
                Assert.Equal (plain, Ink (strip, label));
            }
        }

        // ActiveLinkColor stays in the stored-only baseline ON PURPOSE. WinForms uses it while the
        // link is held down, and nothing in this layer tracks a pressed strip item -- MenuBase handles
        // MouseMove and MouseLeave and no button state at all. This test pins the reason rather than
        // the pixel: if a press state is ever added, it should fail and be replaced by a real one.
        [Fact]
        public void ActiveLinkColor_has_no_pressed_state_to_apply_to ()
        {
            var label = Label (out var form, out var strip);

            using (form) {
                label.IsLink = true;
                label.LinkColor = Color.Red;
                label.ActiveLinkColor = Color.Lime;

                Assert.False (HasPixel (strip, label, p => p.Green > 180 && p.Red < 90 && p.Blue < 90),
                    "ActiveLinkColor was drawn -- if a pressed state now exists, wire it properly and " +
                    "replace this test.");

                Assert.DoesNotContain (typeof (MenuItem).GetProperties (), p => p.Name is "Pressed");
            }
        }

        // MenuItem.Bounds is DEVICE, and so is the bitmap, so no conversion belongs here.
        private static bool HasPixel (ToolStrip strip, ToolStripLabel label, System.Func<SkiaSharp.SKColor, bool> match)
        {
            using var bitmap = PaintSurface.Render (strip);
            var bounds = label.Bounds;

            for (var y = bounds.Top; y < bounds.Bottom && y < bitmap.Height; y++)
                for (var x = bounds.Left; x < bounds.Right && x < bitmap.Width; x++)
                    if (match (bitmap.GetPixel (x, y)))
                        return true;

            return false;
        }

        // The background is sampled from INSIDE the item -- its own top-left corner, above the
        // vertically-centred text -- not from the strip beside it. A hovered item is filled with the
        // hover style, so measuring against the strip's background counts every pixel of the item as
        // ink and saturates: NeverUnderline and HoverUnderline both came back as the full item area,
        // and the underline could not move the number.
        private static int Ink (ToolStrip strip, ToolStripLabel label)
        {
            using var bitmap = PaintSurface.Render (strip);
            var bounds = label.Bounds;
            var background = bitmap.GetPixel (bounds.Left + 1, bounds.Top + 1);
            var ink = 0;

            for (var y = bounds.Top; y < bounds.Bottom && y < bitmap.Height; y++)
                for (var x = bounds.Left; x < bounds.Right && x < bitmap.Width; x++)
                    if (bitmap.GetPixel (x, y) != background)
                        ink++;

            return ink;
        }
    }
}
