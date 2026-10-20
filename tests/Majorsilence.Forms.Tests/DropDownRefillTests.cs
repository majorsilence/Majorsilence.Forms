using System;
using System.Drawing;
using System.Linq;
using Majorsilence.Forms.Headless;
using Xunit;

namespace Majorsilence.Forms.Tests
{
    [Collection ("Headless")]
    public sealed class DropDownRefillTests : IDisposable
    {
        public DropDownRefillTests () => HeadlessRenderer.Use ();

        public void Dispose () { }

        // An app that rebuilds a drop-down's items from one of its items' Click (an Engine menu that lists what was
        // just chosen) got items measured as tool bar items -- a separator 8 wide, labels without their check
        // margin -- so the reopened drop-down was too narrow for them.
        [Fact]
        public void Items_rebuilt_from_a_click_are_measured_as_menu_items ()
        {
            var strip = new ToolStrip { Dock = DockStyle.Bottom };
            var engine = new ToolStripDropDownButton ("Engine");
            strip.Items.Add (engine);
            using var form = new Form { Width = 900, Height = 600 };
            form.Controls.Add (strip);
            form.Show ();
            using var painted = PaintSurface.Render (strip);

            void Fill (string chosen)
            {
                engine.DropDownItems.Clear ();
                foreach (var name in new[] { "Device voices", "Recital voices (offline)" }) {
                    var item = new ToolStripMenuItem (name) { Checked = name == chosen };
                    item.Click += (_, _) => Fill (name);
                    engine.DropDownItems.Add (item);
                }
                engine.DropDownItems.Add (new ToolStripSeparator ());
                engine.DropDownItems.Add (new ToolStripMenuItem ("Voice settings…"));
            }

            Fill ("Recital voices (offline)");
            engine.ShowDropDown ();
            var before = engine.OpenDropDown!.Width;
            var separatorBefore = engine.DropDownItems[2].GetPreferredSize (Size.Empty).Width;
            ((ToolStripMenuItem) engine.DropDownItems[0]).PerformClick ();   // rebuilds the items
            engine.HideDropDown ();

            engine.ShowDropDown ();
            try {
                var after = engine.OpenDropDown!.Width;
                var separatorAfter = engine.DropDownItems[2].GetPreferredSize (Size.Empty).Width;
                Assert.True (after >= before, $"the drop-down was {before} wide, {after} after its items were rebuilt");
                Assert.Equal (separatorBefore, separatorAfter);
            } finally {
                engine.HideDropDown ();
            }
        }
    }
}
