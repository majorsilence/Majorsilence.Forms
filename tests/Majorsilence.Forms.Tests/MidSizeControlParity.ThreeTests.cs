using System;
using System.Collections.Specialized;
using Xunit;

using Point = System.Drawing.Point;
using Size = System.Drawing.Size;

namespace Majorsilence.Forms.Tests
{
    /// <summary>
    /// Covers the third mid-size batch (docs/winforms-gap-plan.md): ControlPaint, DataFormats,
    /// DataObject, ListBox and the two composite ToolStrip items.
    ///
    /// DataObject's typed accessors carry the weight here — they are how ordinary drag-and-drop and
    /// clipboard code is written, and they are real rather than stored-and-ignored.
    /// </summary>
    public class MidSizeControlParityThreeTests
    {
        [Fact]
        public void DataObject_round_trips_a_file_drop_list ()
        {
            var data = new DataObject ();
            var paths = new StringCollection { @"/tmp/one.txt", @"/tmp/two.txt" };

            Assert.False (data.ContainsFileDropList ());

            data.SetFileDropList (paths);

            Assert.True (data.ContainsFileDropList ());
            Assert.Equal (2, data.GetFileDropList ().Count);
            Assert.Equal (@"/tmp/one.txt", data.GetFileDropList ()[0]);
        }

        [Fact]
        public void GetFileDropList_returns_an_empty_collection_rather_than_null ()
            => Assert.Empty (new DataObject ().GetFileDropList ());

        [Fact]
        public void DataObject_round_trips_an_image ()
        {
            var data = new DataObject ();
            using var image = new Majorsilence.Forms.Drawing.Bitmap (4, 4);

            Assert.False (data.ContainsImage ());

            data.SetImage (image);

            Assert.True (data.ContainsImage ());
            Assert.Same (image, data.GetImage ());
        }

        [Fact]
        public void DataObject_round_trips_audio ()
        {
            var data = new DataObject ();

            data.SetAudio (new byte[] { 1, 2, 3, 4 });

            Assert.True (data.ContainsAudio ());
            Assert.Equal (4, data.GetAudioStream ()!.Length);
        }

        [Fact]
        public void TryGetData_reports_a_miss_rather_than_throwing ()
        {
            var data = new DataObject ();
            data.SetData ("custom", "stored");

            Assert.True (data.TryGetData<string> ("custom", out var found));
            Assert.Equal ("stored", found);

            Assert.False (data.TryGetData<int> ("custom", out var wrongType));
            Assert.Equal (0, wrongType);

            Assert.False (data.TryGetData<string> ("absent", out _));
        }

        [Fact]
        public void The_clipboard_formats_have_their_Win32_identifiers ()
        {
            // These numbers are the CF_* constants; code that round-trips a format by id needs them
            // to be the real ones.
            Assert.Equal (3, DataFormats.MetafilePict.Id);
            Assert.Equal (4, DataFormats.SymbolicLink.Id);
            Assert.Equal (8, DataFormats.Dib.Id);
            Assert.Equal (9, DataFormats.Palette.Id);
            Assert.Equal (14, DataFormats.EnhancedMetafile.Id);
            Assert.Equal (16, DataFormats.Locale.Id);
        }

        [Fact]
        public void GetFormat_by_id_finds_the_newly_added_formats ()
        {
            Assert.Equal (DataFormats.Dib.Name, DataFormats.GetFormat (DataFormats.Dib.Id).Name);
        }

        [Fact]
        public void ListBox_IndexFromPoint_maps_a_point_to_a_row ()
        {
            using var list = new ListBox { Size = new Size (120, 100), ItemHeight = 20 };
            list.Items.Add ("a");
            list.Items.Add ("b");
            list.Items.Add ("c");

            Assert.Equal (0, list.IndexFromPoint (new Point (5, 5)));
            Assert.Equal (1, list.IndexFromPoint (new Point (5, 25)));
            Assert.Equal (-1, list.IndexFromPoint (new Point (5, 500)));   // past the last item
            Assert.Equal (-1, list.IndexFromPoint (new Point (5000, 5)));  // outside the control
        }

        [Fact]
        public void ListBox_PreferredHeight_grows_with_the_item_count ()
        {
            using var list = new ListBox { ItemHeight = 20 };

            Assert.Equal (0, list.PreferredHeight);

            list.Items.Add ("a");
            list.Items.Add ("b");

            Assert.Equal (40, list.PreferredHeight);
        }

        [Fact]
        public void ListBox_GetItemHeight_rejects_a_negative_index ()
        {
            using var list = new ListBox ();

            Assert.Equal (list.ItemHeight, list.GetItemHeight (0));
            Assert.Throws<ArgumentOutOfRangeException> (() => list.GetItemHeight (-1));
        }

        [Fact]
        public void ListBox_CustomTabOffsets_is_a_usable_collection ()
        {
            using var list = new ListBox ();

            list.CustomTabOffsets.AddRange (10, 20, 30);

            Assert.Equal (3, list.CustomTabOffsets.Count);
            Assert.Equal (20, list.CustomTabOffsets[1]);
            Assert.Contains (30, list.CustomTabOffsets);

            list.CustomTabOffsets.Remove (20);
            Assert.Equal (2, list.CustomTabOffsets.Count);
        }

        [Fact]
        public void SplitButton_bounds_divide_the_item_between_its_two_halves ()
        {
            var button = new ToolStripSplitButton { Size = new Size (100, 24) };

            var buttonHalf = button.ButtonBounds;
            var dropDownHalf = button.DropDownButtonBounds;

            Assert.Equal (0, buttonHalf.X);
            Assert.Equal (100, buttonHalf.Width + dropDownHalf.Width);   // together they cover the item
            Assert.Equal (buttonHalf.Right, dropDownHalf.Left);
            Assert.Equal (dropDownHalf.Left, button.SplitterBounds.Left);
        }

        [Fact]
        public void ResetDropDownButtonWidth_restores_the_default ()
        {
            var button = new ToolStripSplitButton { DropDownButtonWidth = 40 };

            button.ResetDropDownButtonWidth ();

            Assert.Equal (11, button.DropDownButtonWidth);
        }

        [Fact]
        public void PerformButtonClick_does_nothing_when_the_item_is_disabled ()
        {
            var button = new ToolStripSplitButton { Enabled = false };
            var clicked = 0;
            button.Click += (_, _) => clicked++;

            button.PerformButtonClick ();
            Assert.Equal (0, clicked);

            button.Enabled = true;
            button.PerformButtonClick ();
            Assert.Equal (1, clicked);
        }

        [Fact]
        public void ToolStripProgressBar_forwards_to_the_hosted_bar ()
        {
            var item = new ToolStripProgressBar ();

            item.Step = 5;
            item.Value = 10;

            Assert.Equal (5, item.ProgressBar.Step);

            item.PerformStep ();
            Assert.Equal (15, item.ProgressBar.Value);

            item.Increment (10);
            Assert.Equal (25, item.ProgressBar.Value);
        }

        [Fact]
        public void ToolStripProgressBar_RightToLeftLayout_notifies_once ()
        {
            var item = new ToolStripProgressBar ();
            var raised = 0;
            item.RightToLeftLayoutChanged += (_, _) => raised++;

            item.RightToLeftLayout = true;
            item.RightToLeftLayout = true;

            Assert.Equal (1, raised);
        }

        [Fact]
        public void TextFormatFlags_carry_their_Win32_values ()
        {
            // These are DrawText's DT_* constants; a wrong number silently changes the layout rather
            // than failing to compile.
            Assert.Equal (0, (int)TextFormatFlags.Default);
            Assert.Equal (64, (int)TextFormatFlags.ExpandTabs);
            Assert.Equal (512, (int)TextFormatFlags.ExternalLeading);
            Assert.Equal (8192, (int)TextFormatFlags.TextBoxControl);
            Assert.Equal (16384, (int)TextFormatFlags.PathEllipsis);
            Assert.Equal (131072, (int)TextFormatFlags.RightToLeft);
        }

        [Fact]
        public void The_reversible_drawing_family_does_nothing_rather_than_leaving_artefacts ()
        {
            // XOR drawing onto a screen DC has no counterpart on a Skia surface, and drawing
            // something that could never be erased would be worse than drawing nothing.
            ControlPaint.DrawReversibleFrame (new System.Drawing.Rectangle (0, 0, 10, 10),
                System.Drawing.Color.Black, FrameStyle.Dashed);
            ControlPaint.DrawReversibleLine (Point.Empty, new Point (10, 10), System.Drawing.Color.Black);
            ControlPaint.FillReversibleRectangle (new System.Drawing.Rectangle (0, 0, 10, 10), System.Drawing.Color.Black);
        }

        [Fact]
        public void The_HBitmap_factories_report_that_there_is_no_handle ()
        {
            using var bitmap = new Majorsilence.Forms.Drawing.Bitmap (4, 4);

            Assert.Equal (IntPtr.Zero, ControlPaint.CreateHBitmap16Bit (bitmap, System.Drawing.Color.White));
            Assert.Equal (IntPtr.Zero, ControlPaint.CreateHBitmapTransparencyMask (bitmap));
            Assert.Equal (IntPtr.Zero, ControlPaint.CreateHBitmapColorMask (bitmap, IntPtr.Zero));
        }

        [Fact]
        public void DrawImageDisabled_actually_draws ()
        {
            using var target = new Majorsilence.Forms.Drawing.Bitmap (10, 10);
            using var graphics = Graphics.FromImage (target);
            using var source = new Majorsilence.Forms.Drawing.Bitmap (4, 4);
            source.SetPixel (1, 1, System.Drawing.Color.Red);

            ControlPaint.DrawImageDisabled (graphics, source, 0, 0, System.Drawing.Color.White);

            var painted = false;
            for (var y = 0; y < 10 && !painted; y++)
                for (var x = 0; x < 10 && !painted; x++)
                    painted = target.GetPixel (x, y).A != 0;

            Assert.True (painted);
        }
    }
}
