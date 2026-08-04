using Xunit;

using Point = System.Drawing.Point;
using Size = System.Drawing.Size;

namespace Majorsilence.Forms.Tests
{
    /// <summary>
    /// Covers the ToolStripItem/ToolStrip parity pass (docs/winforms-gap-plan.md, item 4).
    ///
    /// Most of that surface is stored-and-round-tripped by design, and re-asserting a plain property
    /// would be noise. These tests target the members that genuinely compute something —
    /// <c>Available</c>, <c>Width</c>, <c>CanSelect</c>, <c>GetItemAt</c>, <c>GetNextItem</c>,
    /// <c>IsOnDropDown</c> and the <c>Reset*</c> family — plus the events that are really raised.
    /// </summary>
    public class ToolStripParityTests
    {
        [Fact]
        public void Available_tracks_visibility_and_raises_its_event ()
        {
            var item = new ToolStripButton { Text = "Save" };
            var raised = 0;
            item.AvailableChanged += (_, _) => raised++;

            Assert.True (item.Available);

            item.Available = false;

            Assert.False (item.Available);
            Assert.False (item.Visible);      // Available is the state Visible reflects
            Assert.Equal (1, raised);

            item.Available = false;           // no change, no event
            Assert.Equal (1, raised);
        }

        [Fact]
        public void Width_reads_and_writes_the_items_size ()
        {
            var item = new ToolStripButton { Size = new Size (40, 22) };

            Assert.Equal (40, item.Width);

            item.Width = 100;

            Assert.Equal (100, item.Width);
            Assert.Equal (22, item.Height);   // height is untouched
        }

        [Fact]
        public void CanSelect_is_false_for_a_disabled_or_unavailable_item ()
        {
            var item = new ToolStripButton ();
            Assert.True (item.CanSelect);

            item.Enabled = false;
            Assert.False (item.CanSelect);

            item.Enabled = true;
            item.Available = false;
            Assert.False (item.CanSelect);
        }

        [Fact]
        public void Select_raises_SelectedChanged_only_when_the_item_can_be_selected ()
        {
            var item = new ToolStripButton ();
            var raised = 0;
            item.SelectedChanged += (_, _) => raised++;

            item.Select ();
            Assert.Equal (1, raised);

            item.Enabled = false;
            item.Select ();
            Assert.Equal (1, raised);        // a disabled item is not selectable
        }

        [Fact]
        public void GetCurrentParent_returns_the_strip_the_item_is_on ()
        {
            using var strip = new ToolStrip ();
            var item = new ToolStripButton ();

            Assert.Null (item.GetCurrentParent ());

            strip.Items.Add (item);

            Assert.Same (strip, item.GetCurrentParent ());
            Assert.False (item.IsOnDropDown);   // a plain strip is not a drop-down
        }

        [Fact]
        public void GetItemAt_finds_the_item_under_a_point ()
        {
            using var strip = new ToolStrip ();
            var first = new ToolStripButton { Size = new Size (50, 20) };
            strip.Items.Add (first);
            first.SetBounds (0, 0, 50, 20);

            Assert.Same (first, strip.GetItemAt (new Point (10, 10)));
            Assert.Null (strip.GetItemAt (new Point (500, 500)));
        }

        [Fact]
        public void GetItemAt_skips_unavailable_items ()
        {
            using var strip = new ToolStrip ();
            var hidden = new ToolStripButton { Size = new Size (50, 20), Available = false };
            strip.Items.Add (hidden);
            hidden.SetBounds (0, 0, 50, 20);

            Assert.Null (strip.GetItemAt (new Point (10, 10)));
        }

        [Fact]
        public void GetNextItem_walks_selectable_items_and_wraps ()
        {
            using var strip = new ToolStrip ();
            var a = new ToolStripButton { Text = "a" };
            var b = new ToolStripButton { Text = "b" };
            strip.Items.Add (a);
            strip.Items.Add (b);

            Assert.Same (b, strip.GetNextItem (a, ArrowDirection.Right));
            Assert.Same (a, strip.GetNextItem (b, ArrowDirection.Right));   // wraps
            Assert.Same (a, strip.GetNextItem (b, ArrowDirection.Left));
        }

        [Fact]
        public void GetNextItem_skips_items_that_cannot_be_selected ()
        {
            using var strip = new ToolStrip ();
            var a = new ToolStripButton { Text = "a" };
            var disabled = new ToolStripButton { Text = "x", Enabled = false };
            var c = new ToolStripButton { Text = "c" };
            strip.Items.Add (a);
            strip.Items.Add (disabled);
            strip.Items.Add (c);

            Assert.Same (c, strip.GetNextItem (a, ArrowDirection.Right));
        }

        [Fact]
        public void GetNextItem_returns_null_for_an_empty_strip ()
        {
            using var strip = new ToolStrip ();
            Assert.Null (strip.GetNextItem (null, ArrowDirection.Right));
        }

        [Fact]
        public void The_drag_flag_tracks_begin_and_end ()
        {
            using var strip = new ToolStrip ();

            Assert.False (strip.IsCurrentlyDragging);
            strip.BeginDrag ();
            Assert.True (strip.IsCurrentlyDragging);
            strip.EndDrag ();
            Assert.False (strip.IsCurrentlyDragging);
        }

        [Fact]
        public void Reset_methods_return_properties_to_their_defaults ()
        {
            var item = new ToolStripButton {
                DisplayStyle = ToolStripItemDisplayStyle.Text,
                TextDirection = ToolStripTextDirection.Vertical270,
                Margin = new Padding (9),
            };

            item.ResetDisplayStyle ();
            item.ResetTextDirection ();
            item.ResetMargin ();

            Assert.Equal (ToolStripItemDisplayStyle.ImageAndText, item.DisplayStyle);
            Assert.Equal (ToolStripTextDirection.Inherit, item.TextDirection);
            Assert.Equal (new Padding (0), item.Margin);
        }

        [Fact]
        public void The_accessibility_object_falls_back_to_the_items_text ()
        {
            var item = new ToolStripButton { Text = "Save" };

            Assert.Equal ("Save", item.AccessibilityObject.Name);

            item.AccessibleName = "Save the document";
            Assert.Equal ("Save the document", item.AccessibilityObject.Name);

            // The same instance each time, as WinForms does.
            Assert.Same (item.AccessibilityObject, item.AccessibilityObject);
        }

        [Fact]
        public void DoDragDrop_reports_that_no_drag_occurred ()
        {
            // Honest rather than absent: there is no OS drag source in this layer yet, which is the
            // same position Control.DoDragDrop is in.
            var item = new ToolStripButton ();
            Assert.Equal (DragDropEffects.None, item.DoDragDrop ("payload", DragDropEffects.Copy));
        }
    }
}
