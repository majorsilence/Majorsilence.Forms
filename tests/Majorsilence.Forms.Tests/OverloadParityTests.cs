using Xunit;

using Point = System.Drawing.Point;
using Rectangle = System.Drawing.Rectangle;
using Size = System.Drawing.Size;

namespace Majorsilence.Forms.Tests
{
    /// <summary>
    /// Covers the overload parity pass — the one that turned on
    /// <c>Surface.WinForms.IncludeOverloads</c> and closed all 146 findings.
    ///
    /// The interesting overloads are the ones whose extra parameter does something. A test that only
    /// called each new overload would prove it exists, which the compiler already proves; these check
    /// that the argument changed the answer.
    /// </summary>
    public class OverloadParityTests
    {
        [Fact]
        public void FindItemWithText_can_search_sub_items ()
        {
            using var listView = new ListView ();
            var item = new ListViewItem ("alpha");
            item.SubItems.Add ("hidden treasure");
            listView.Items.Add (item);

            Assert.Null (listView.FindItemWithText ("hidden", includeSubItemsInSearch: false, 0));
            Assert.Same (item, listView.FindItemWithText ("hidden", includeSubItemsInSearch: true, 0));
        }

        [Fact]
        public void FindItemWithText_distinguishes_a_prefix_from_an_exact_match ()
        {
            using var listView = new ListView ();
            listView.Items.Add (new ListViewItem ("alphabet"));

            Assert.NotNull (listView.FindItemWithText ("alpha", false, 0, isPrefixSearch: true));
            Assert.Null (listView.FindItemWithText ("alpha", false, 0, isPrefixSearch: false));
        }

        [Fact]
        public void FindItemWithText_starts_at_the_given_index ()
        {
            using var listView = new ListView ();
            listView.Items.Add (new ListViewItem ("match"));
            listView.Items.Add (new ListViewItem ("match"));

            Assert.Same (listView.Items[1], listView.FindItemWithText ("match", false, 1));
        }

        [Fact]
        public void ValidateChildren_skips_the_children_the_constraints_exclude ()
        {
            using var form = new Form ();
            var child = new TextBox { Enabled = false };
            form.Controls.Add (child);
            var validated = 0;
            child.Validating += (_, _) => validated++;

            form.ValidateChildren (ValidationConstraints.Enabled);

            Assert.Equal (0, validated);      // disabled, so skipped

            child.Enabled = true;
            form.ValidateChildren (ValidationConstraints.Enabled);

            Assert.Equal (1, validated);
        }

        [Fact]
        public void TreeNodeCollection_Add_returns_the_new_index ()
        {
            // Deliberately typed as the base collection. TreeView.Nodes is a TreeViewItemCollection,
            // which declares its own Add (TreeViewItem) returning the item -- so through that type the
            // WinForms-shaped overload is hidden by name. Changing that would alter the return type of
            // an overload callers already chain off, so the int-returning Add lives here, where a
            // TreeNodeCollection-typed reference finds it.
            using var tree = new TreeView ();
            TreeNodeCollection nodes = tree.Nodes;
            var first = new TreeNode ("one");
            var second = new TreeNode ("two");

            Assert.Equal (0, nodes.Add (first));
            Assert.Equal (1, nodes.Add (second));
            Assert.True (nodes.Contains (second));
            Assert.Equal (1, nodes.IndexOf (second));

            nodes.Remove (second);
            Assert.False (nodes.Contains (second));
        }

        [Fact]
        public void TreeNodeCollection_Insert_places_the_node_and_carries_its_images ()
        {
            using var tree = new TreeView ();
            tree.Nodes.Add ("first");

            TreeNodeCollection nodes = tree.Nodes;
            var inserted = nodes.Insert (0, "key", "second", imageIndex: 3, selectedImageIndex: 4);

            Assert.Same (inserted, nodes[0]);
            Assert.Equal ("key", inserted.Name);
            Assert.Equal (3, inserted.ImageIndex);
            Assert.Equal (4, inserted.SelectedImageIndex);
        }

        [Fact]
        public void ToolStripItemCollection_Add_wires_the_click_handler ()
        {
            using var strip = new ToolStrip ();
            var clicked = 0;

            var item = strip.Items.Add ("Save", null!, (_, _) => clicked++);
            item.PerformClick ();

            Assert.Equal ("Save", item.Text);
            Assert.Equal (1, clicked);
        }

        [Fact]
        public void ToolStripItemCollection_AddRange_accepts_another_collection ()
        {
            using var source = new ToolStrip ();
            source.Items.Add ("a");
            source.Items.Add ("b");

            using var target = new ToolStrip ();
            target.Items.AddRange (source.Items);

            Assert.Equal (2, target.Items.Count);
        }

        [Fact]
        public void The_text_data_formats_are_distinct_clipboard_slots ()
        {
            // Storing RTF and reading it back as plain text has to miss, or a paste inserts markup.
            var data = new DataObject ();
            data.SetText ("{\\rtf1}", TextDataFormat.Rtf);

            Assert.True (data.ContainsText (TextDataFormat.Rtf));
            Assert.False (data.ContainsText (TextDataFormat.UnicodeText));
            Assert.Equal ("{\\rtf1}", data.GetText (TextDataFormat.Rtf));
            Assert.Equal (string.Empty, data.GetText (TextDataFormat.Html));
        }

        [Fact]
        public void RichTextBox_Find_over_a_character_set_honours_the_range ()
        {
            using var rich = new RichTextBox { Text = "abcXdefX" };

            Assert.Equal (3, rich.Find (['X']));
            Assert.Equal (7, rich.Find (['X'], 4));
            Assert.Equal (-1, rich.Find (['X'], 4, 7));     // the end bound excludes index 7
            Assert.Equal (-1, rich.Find (['Z']));
        }

        [Fact]
        public void Application_Exit_honours_a_cancelled_request ()
        {
            var e = new System.ComponentModel.CancelEventArgs { Cancel = true };

            Application.Exit (e);       // must not tear the application down

            Assert.True (e.Cancel);
        }

        [Fact]
        public void GetFormat_by_id_returns_the_standard_format_rather_than_a_new_one ()
        {
            Assert.Same (DataFormats.Rtf, DataFormats.GetFormat (DataFormats.Rtf.Id));
            Assert.Equal (DataFormats.Text.Id, DataFormats.GetFormat (DataFormats.Text.Id).Id);

            var unknown = DataFormats.GetFormat (999999);
            Assert.Equal (999999, unknown.Id);
        }

        [Fact]
        public void DataGridCell_remembers_the_row_and_column_it_was_built_with ()
        {
            // Both were discarded, which made the two-argument constructor useless.
            var cell = new DataGridCell (3, 7);

            Assert.Equal (3, cell.RowNumber);
            Assert.Equal (7, cell.ColumnNumber);
        }

        [Fact]
        public void DataGridViewRowCollection_Insert_adds_the_requested_number_of_rows ()
        {
            using var grid = new DataGridView ();
            grid.Columns.Add (new DataGridViewTextBoxColumn ());
            grid.Rows.Add ("a");

            grid.Rows.Insert (0, 3);

            Assert.Equal (4, grid.Rows.Count);
            Assert.Throws<System.ArgumentOutOfRangeException> (() => grid.Rows.Insert (0, 0));
        }

        [Fact]
        public void TextBox_Paste_of_a_string_replaces_the_selection ()
        {
            var box = new TextBox { Text = "hello world" };
            box.Select (0, 5);

            box.Paste ("goodbye");

            Assert.Equal ("goodbye world", box.Text);
        }

        [Fact]
        public void Graphics_is_a_device_context ()
        {
            // What lets TextRenderer take an IDeviceContext, as it does upstream, while callers keep
            // passing a Graphics.
            using var image = new Majorsilence.Forms.Drawing.Bitmap (10, 10);
            using var g = Graphics.FromImage (image);

            Assert.IsAssignableFrom<Majorsilence.Forms.Drawing.IDeviceContext> (g);

            Assert.Equal (System.IntPtr.Zero, g.GetHdc ());
            g.ReleaseHdc ();                        // must not throw
        }

        [Fact]
        public void MeasureText_with_SingleLine_does_not_wrap_to_the_proposed_width ()
        {
            using var font = new Majorsilence.Forms.Drawing.Font ("Arial", 10f);
            const string Text = "a reasonably long sentence that would wrap";

            var wrapped = TextRenderer.MeasureText (Text, font, new Size (40, int.MaxValue), TextFormatFlags.Left);
            var single = TextRenderer.MeasureText (Text, font, new Size (40, int.MaxValue), TextFormatFlags.SingleLine);

            Assert.True (single.Width > wrapped.Width);
            Assert.True (single.Height < wrapped.Height);
        }

        [Fact]
        public void DrawText_at_a_point_measures_the_text_rather_than_drawing_into_nothing ()
        {
            // The point overloads used to pass a zero-sized rectangle straight through, so nothing
            // was ever drawn.
            using var image = new Majorsilence.Forms.Drawing.Bitmap (60, 30);
            using var g = Graphics.FromImage (image);
            using var font = new Majorsilence.Forms.Drawing.Font ("Arial", 12f);

            TextRenderer.DrawText (g, "Hi", font, new Point (2, 2), System.Drawing.Color.Red);

            var painted = false;
            for (var y = 0; y < 30 && !painted; y++)
                for (var x = 0; x < 60 && !painted; x++)
                    painted = image.GetPixel (x, y).A != 0;

            Assert.True (painted);
        }

        [Fact]
        public void DrawText_with_a_back_colour_fills_the_box_first ()
        {
            using var image = new Majorsilence.Forms.Drawing.Bitmap (20, 20);
            using var g = Graphics.FromImage (image);
            using var font = new Majorsilence.Forms.Drawing.Font ("Arial", 8f);

            TextRenderer.DrawText (g, "x", font, new Rectangle (0, 0, 20, 20),
                System.Drawing.Color.Black, System.Drawing.Color.Blue);

            // A corner the glyph cannot reach still carries the background.
            Assert.Equal (System.Drawing.Color.Blue.ToArgb (), image.GetPixel (19, 19).ToArgb ());
        }
    }
}
