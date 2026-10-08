using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Reflection;
using Majorsilence.Forms.Headless;
using Xunit;

namespace Majorsilence.Forms.Tests
{
    // EVT-30: Scroll is a ScrollEventHandler on ScrollableControl, ScrollBar and DataGridView, as upstream
    // declares it (Scrolling/ScrollableControl.cs, Scrolling/ScrollBar.cs, DataGridView.cs), and Control
    // itself has none. ScrollableControl's was EventHandler<ScrollEventArgs>, so the designer's
    // `panel1.Scroll += new ScrollEventHandler (panel1_Scroll)` did not compile, and a stub Control.Scroll
    // swallowed the handler on every other control.
    [Collection ("Headless")]
    public class ScrollEventShapeTests
    {
        [Theory]
        [InlineData (typeof (ScrollableControl))]
        [InlineData (typeof (Panel))]
        [InlineData (typeof (ScrollBar))]
        [InlineData (typeof (DataGridView))]
        public void Scroll_is_a_ScrollEventHandler (System.Type type)
            => Assert.Equal (typeof (ScrollEventHandler), type.GetEvent ("Scroll")!.EventHandlerType);

        [Fact]
        public void Control_declares_no_Scroll_event ()
        {
            // A Control.Scroll that nothing raised accepted handlers on a Button or a ListBox and dropped
            // them. Upstream's Control has no Scroll, so such code does not compile there either.
            Assert.Null (typeof (Control).GetEvent ("Scroll", BindingFlags.Public | BindingFlags.Instance));
            Assert.Null (typeof (Button).GetEvent ("Scroll", BindingFlags.Public | BindingFlags.Instance));
        }

        [Fact]
        public void Dragging_the_thumb_of_a_panel_raises_its_Scroll_with_ThumbTrack ()
        {
            HeadlessRenderer.Use ();
            using var form = new Form { Size = new Size (320, 400) };
            var panel = new Panel { AutoScroll = true, Size = new Size (150, 150) };
            panel.Controls.Add (new Button { Location = new Point (0, 900), Size = new Size (20, 20) });
            form.Controls.Add (panel);
            form.Show ();
            HeadlessRenderer.CapturePng (form);

            var bar = (VerticalScrollBar) panel.Controls.GetAllControls (true).First (c => c is VerticalScrollBar);
            Assert.True (bar.Visible);

            var seen = new List<(object Sender, ScrollEventArgs Args)> ();

            // The designer's exact form, which did not compile against EventHandler<ScrollEventArgs>.
            panel.Scroll += new ScrollEventHandler ((s, e) => seen.Add ((s, e)));

            // The renderer's own thumb bounds, in the logical units the mouse is in, so the press lands
            // on the thumb at any scale.
            var thumb = Renderers.RenderManager.GetRenderer<Renderers.ScrollBarRenderer> ()!.GetThumbDragBounds (bar);
            var x = thumb.Left + thumb.Width / 2;
            var y = thumb.Top + thumb.Height / 2;
            bar.RaiseMouseDown (new MouseEventArgs (MouseButtons.Left, 1, x, y, 0));
            bar.RaiseMouseMove (new MouseEventArgs (MouseButtons.Left, 0, x, y + 20, 0));
            bar.RaiseMouseUp (new MouseEventArgs (MouseButtons.Left, 1, x, y + 20, 0));

            var track = seen.FindAll (p => p.Args.Type == ScrollEventType.ThumbTrack);
            Assert.Single (track);
            Assert.Same (panel, track[0].Sender);
            Assert.Equal (ScrollOrientation.VerticalScroll, track[0].Args.ScrollOrientation);
            Assert.True (track[0].Args.NewValue > track[0].Args.OldValue);
        }
    }
}
