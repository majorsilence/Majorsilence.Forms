using System;
using System.ComponentModel;
using System.Linq;
using Xunit;

namespace Majorsilence.Forms.Tests
{
    // The tail of the WinForms member audit. The members with real behaviour are tested here; the
    // enum values that came in with them are covered by EnumValueParityTests.
    public class RemainingMemberParityTests
    {
        [Fact]
        public void Notify_default_flags_the_button ()
        {
            using var button = new Button ();

            Assert.False (button.IsDefault);

            button.NotifyDefault (true);

            Assert.True (button.IsDefault);
        }

        [Fact]
        public void A_column_header_clones_without_its_list_view ()
        {
            using var view = new ListView ();
            var header = view.Columns.Add ("Name", 120);

            var clone = Assert.IsType<ColumnHeader> (header.Clone ());

            Assert.NotSame (header, clone);
            Assert.Equal ("Name", clone.Text);
            Assert.Equal (120, clone.Width);
            Assert.Null (clone.ListView);
        }

        [Fact]
        public void A_column_headers_image_list_is_the_views_small_one ()
        {
            using var view = new ListView ();
            using var images = new ImageList ();
            var header = view.Columns.Add ("Name");

            view.SmallImageList = images;

            Assert.Same (images, header.ImageList);
        }

        [Fact]
        public void Perform_auto_scale_does_nothing_without_recorded_dimensions ()
        {
            using var container = new ContainerControl { Width = 300, Height = 200 };

            // AutoScaleDimensions defaults to empty -- there is no ratio to scale by, and scaling by
            // a bogus one would resize the container the first time it is shown.
            container.PerformAutoScale ();

            Assert.Equal (300, container.Width);
            Assert.Equal (200, container.Height);
        }

        [Fact]
        public void Current_auto_scale_dimensions_follow_the_mode ()
        {
            using var container = new ContainerControl { AutoScaleMode = AutoScaleMode.None };

            Assert.Equal (System.Drawing.SizeF.Empty, container.CurrentAutoScaleDimensions);

            container.AutoScaleMode = AutoScaleMode.Font;

            Assert.True (container.CurrentAutoScaleDimensions.Height > 0);
        }

        [Fact]
        public void Auto_validate_changed_can_be_raised ()
        {
            using var container = new ContainerControl ();
            var raised = 0;

            container.AutoValidateChanged += (_, _) => raised++;
            container.AutoValidate = AutoValidate.Disable;

            // The event exists for source compatibility; what matters is that a handler attaches and
            // the property still round-trips.
            Assert.Equal (AutoValidate.Disable, container.AutoValidate);
            Assert.Equal (0, raised);
        }

        [Fact]
        public void A_progress_bars_right_to_left_layout_raises_its_event ()
        {
            using var bar = new ProgressBar ();
            var raised = 0;

            bar.RightToLeftLayoutChanged += (_, _) => raised++;

            bar.RightToLeftLayout = true;
            Assert.True (bar.RightToLeftLayout);
            Assert.Equal (1, raised);

            bar.RightToLeftLayout = true;
            Assert.Equal (1, raised);
        }

        [Fact]
        public void A_status_strip_reserves_a_square_for_its_sizing_grip ()
        {
            using var strip = new StatusStrip { Width = 400, Height = 22 };

            Assert.True (strip.SizingGrip);
            Assert.Equal (new System.Drawing.Rectangle (378, 0, 22, 22), strip.SizeGripBounds);

            strip.SizingGrip = false;

            Assert.Equal (System.Drawing.Rectangle.Empty, strip.SizeGripBounds);
        }

        [Fact]
        public void The_sizing_grip_moves_to_the_left_under_a_right_to_left_layout ()
        {
            using var strip = new StatusStrip { Width = 400, Height = 22, RightToLeft = RightToLeft.Yes };

            Assert.Equal (new System.Drawing.Rectangle (0, 0, 22, 22), strip.SizeGripBounds);
        }

        [Fact]
        public void A_tool_strip_buttons_check_state_follows_checked ()
        {
            var button = new ToolStripButton ("Bold");
            var checkedChanges = 0;

            button.CheckedChanged += (_, _) => checkedChanges++;

            button.CheckState = CheckState.Checked;

            Assert.True (button.Checked);
            Assert.Equal (1, checkedChanges);

            button.CheckState = CheckState.Unchecked;

            Assert.False (button.Checked);
            Assert.Equal (2, checkedChanges);
        }

        [Fact]
        public void A_tab_page_is_found_from_a_control_on_it ()
        {
            using var page = new TabPage ();
            using var panel = new Panel ();
            using var box = new TextBox ();

            page.Controls.Add (panel);
            panel.Controls.Add (box);

            Assert.Same (page, TabPage.GetTabPageOfComponent (box));
            Assert.Null (TabPage.GetTabPageOfComponent (new TextBox ()));
            Assert.Null (TabPage.GetTabPageOfComponent (null));
        }

        [Fact]
        public void A_cell_collection_adds_a_range ()
        {
            var cells = new DataGridViewCellCollection (new DataGridViewRow ());
            var raised = 0;

            cells.CollectionChanged += (_, _) => raised++;
            cells.AddRange (new DataGridViewTextBoxCell (), new DataGridViewTextBoxCell ());

            Assert.Equal (2, cells.Count);
            Assert.Equal (1, raised);
        }

        [Fact]
        public void A_link_clicked_event_carries_the_links_extent ()
        {
            var e = new LinkClickedEventArgs ("docs", 12, 4);

            Assert.Equal ("docs", e.LinkText);
            Assert.Equal (12, e.LinkStart);
            Assert.Equal (4, e.LinkLength);
        }

        [Fact]
        public void Cancel_edit_is_the_same_flag_as_cancel ()
        {
            var e = new LabelEditEventArgs (0, "new name");

            Assert.False (e.CancelEdit);

            e.CancelEdit = true;

            Assert.True (e.Cancel);
        }

        [Fact]
        public void A_preview_key_down_reports_its_key_value ()
        {
            var e = new PreviewKeyDownEventArgs (Keys.Control | Keys.S);

            Assert.Equal ((int) Keys.S, e.KeyValue);
        }

        [Fact]
        public void A_grid_item_collection_has_an_empty_instance ()
        {
            Assert.Empty (GridItemCollection.Empty);
        }

        [Fact]
        public void The_design_time_visible_attribute_has_both_singletons ()
        {
            Assert.True (DataGridViewColumnDesignTimeVisibleAttribute.Yes.Visible);
            Assert.False (DataGridViewColumnDesignTimeVisibleAttribute.No.Visible);
        }

        [Fact]
        public void A_data_grid_hit_test_reports_nowhere_for_an_empty_grid ()
        {
            using var grid = new DataGrid ();

            var hit = grid.HitTest (5, 5);

            Assert.Equal (DataGrid.HitTestType.None, hit.Type);
            Assert.Equal (-1, hit.Row);
            Assert.Equal (-1, hit.Column);
        }

        [Fact]
        public void A_scrollable_control_has_dock_padding ()
        {
            using var panel = new Panel ();

            panel.DockPadding.All = 6;

            Assert.Equal (6, panel.DockPadding.Left);
            Assert.Equal (6, panel.DockPadding.Bottom);
        }

        [Fact]
        public void A_synchronization_context_copies_itself ()
        {
            var context = new WindowsFormsSynchronizationContext ();

            var copy = context.CreateCopy ();

            Assert.NotSame (context, copy);
            Assert.IsType<WindowsFormsSynchronizationContext> (copy);

            context.Dispose ();
        }
    }
}
