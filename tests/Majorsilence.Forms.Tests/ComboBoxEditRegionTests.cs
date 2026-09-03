using System;
using Majorsilence.Forms.Headless;
using Majorsilence.Forms.Renderers;
using SkiaSharp;
using Xunit;

namespace Majorsilence.Forms.Tests
{
    // W5.10 (findings LST-07, LST-08): a ComboBox had no editable region at all. DropDown is the
    // default style, so the control every WinForms app gets could not be typed into -- DropDown and
    // DropDownList looked and behaved identically -- and the selection API (SelectionStart/Length,
    // SelectedText, SelectAll, Select, MaxLength) was stored ints that nothing read. Text made it
    // worse: the getter returned the selected item's text whenever anything was selected, so
    // restoring a saved free-text value silently showed the previously selected entry.
    //
    // The region is a real child TextBox, so these tests pin the WIRING -- that typing, the selection
    // family, Text and autocompletion all go through it -- rather than re-testing caret and selection
    // mechanics, which are TextBox's own and are covered there.
    [Collection ("Headless")]
    public class ComboBoxEditRegionTests
    {
        private static ComboBox Combo (params string[] items)
        {
            HeadlessRenderer.Use ();
            var combo = new ComboBox { Width = 160, Height = 28 };

            foreach (var item in items)
                combo.Items.Add (item);

            return combo;
        }

        private static void Type (ComboBox combo, string characters)
        {
            foreach (var c in characters)
                combo.RaiseKeyPress (new KeyPressEventArgs (c));
        }

        [Fact]
        public void A_DropDown_combo_takes_typed_text ()
        {
            using var combo = Combo ();
            var updates = 0;
            combo.TextUpdate += (_, _) => updates++;

            Type (combo, "ab");

            Assert.Equal ("ab", combo.Text);
            Assert.Equal ("ab", combo.EditRegion.Text);
            Assert.Equal (2, updates);
        }

        [Fact]
        public void A_DropDownList_combo_refuses_typed_text ()
        {
            // The whole point of the finding: the two styles used to be indistinguishable.
            using var combo = Combo ("item0");
            combo.DropDownStyle = ComboBoxStyle.DropDownList;
            var updates = 0;
            combo.TextUpdate += (_, _) => updates++;

            Type (combo, "ab");

            Assert.Equal (string.Empty, combo.Text);
            Assert.Equal (0, updates);
            Assert.False (combo.IsEditable);
        }

        [Fact]
        public void Switching_style_shows_and_hides_the_edit_region ()
        {
            // On a form, because Control.Visible is ambient: it walks up the parent chain, and a
            // parentless control reports false however its own flag is set.
            using var combo = Combo ();
            using var form = new Form { Width = 300, Height = 200 };
            form.Controls.Add (combo);

            Assert.True (combo.IsEditable);
            Assert.True (combo.EditRegion.Visible);

            combo.DropDownStyle = ComboBoxStyle.DropDownList;
            Assert.False (combo.IsEditable);
            Assert.False (combo.EditRegion.Visible);

            // Simple is editable too -- it is DropDownList that is the odd one out.
            combo.DropDownStyle = ComboBoxStyle.Simple;
            Assert.True (combo.IsEditable);
            Assert.True (combo.EditRegion.Visible);
        }

        [Fact]
        public void TextUpdate_is_raised_before_TextChanged ()
        {
            // Upstream order: CBN_EDITUPDATE (TextUpdate) precedes CBN_EDITCHANGE (TextChanged).
            using var combo = Combo ();
            var order = string.Empty;
            combo.TextUpdate += (_, _) => order += "U";
            combo.TextChanged += (_, _) => order += "C";

            Type (combo, "a");

            Assert.Equal ("UC", order);
        }

        [Fact]
        public void SelectAll_selects_what_was_typed ()
        {
            using var combo = Combo ();

            Type (combo, "abc");
            combo.SelectAll ();

            Assert.Equal (3, combo.SelectionLength);
            Assert.Equal ("abc", combo.SelectedText);
        }

        [Fact]
        public void Select_selects_a_range_and_SelectedText_replaces_it ()
        {
            using var combo = Combo ();
            combo.Text = "hello";

            combo.Select (1, 3);
            Assert.Equal ("ell", combo.SelectedText);

            combo.SelectedText = "ipp";
            Assert.Equal ("hippo", combo.Text);
        }

        [Fact]
        public void MaxLength_limits_typing ()
        {
            using var combo = Combo ();
            combo.MaxLength = 2;

            Type (combo, "abc");

            Assert.Equal ("ab", combo.Text);
        }

        [Fact]
        public void MaxLength_survives_a_style_switch ()
        {
            // GUARD, not proof: no previous version could fail this, because none had two stores to
            // fall out of step. It pins the single-store decision -- the edit region's own document --
            // against a future "keep a stored int for DropDownList" shortcut.
            using var combo = Combo ();
            combo.DropDownStyle = ComboBoxStyle.DropDownList;
            combo.MaxLength = 5;

            combo.DropDownStyle = ComboBoxStyle.DropDown;

            Assert.Equal (5, combo.MaxLength);
        }

        [Fact]
        public void MaxLength_reads_back_the_value_that_was_set ()
        {
            // The document represented "no limit" AS int.MaxValue, so an explicit int.MaxValue read
            // back as 0 -- "no limit". Forwarding ComboBox.MaxLength to the document surfaced it; the
            // defect is TextBox's, so it is asserted on a TextBox too.
            using var combo = Combo ();
            using var box = new TextBox ();

            combo.MaxLength = int.MaxValue;
            box.MaxLength = int.MaxValue;

            Assert.Equal (int.MaxValue, combo.MaxLength);
            Assert.Equal (int.MaxValue, box.MaxLength);
        }

        [Fact]
        public void Text_keeps_free_text_over_a_selection ()
        {
            // LST-08: the getter used to answer the selected item, so this read back "item1".
            using var combo = Combo ("item0", "item1", "item2");
            combo.SelectedIndex = 1;

            combo.Text = "zzz";

            Assert.Equal ("zzz", combo.Text);
            Assert.Equal ("zzz", combo.EditRegion.Text);
            Assert.Equal (1, combo.SelectedIndex);
        }

        [Fact]
        public void Text_set_to_null_clears_the_selection ()
        {
            // The documented WinForms idiom for clearing a combo.
            using var combo = Combo ("item0", "item1");
            combo.SelectedIndex = 1;

            combo.Text = null!;

            Assert.Equal (-1, combo.SelectedIndex);
            Assert.Equal (string.Empty, combo.Text);
        }

        [Fact]
        public void Text_matching_an_item_selects_it_regardless_of_case ()
        {
            // GUARD, not proof: the old setter resolved an exact match too, so this passes against it.
            // It is here because the LST-08 rewrite could easily have dropped the half that worked.
            using var combo = Combo ("item0", "item1", "item2");

            combo.Text = "ITEM2";

            Assert.Equal (2, combo.SelectedIndex);
        }

        [Fact]
        public void Selecting_an_item_shows_it_in_the_edit_region ()
        {
            using var combo = Combo ("item0", "item1", "item2");

            combo.SelectedIndex = 2;

            Assert.Equal ("item2", combo.EditRegion.Text);
            Assert.Equal ("item2", combo.Text);
        }

        [Fact]
        public void Enter_commits_typed_text_to_a_selection ()
        {
            using var combo = Combo ("item0", "item1", "item2");

            Type (combo, "item1");
            Assert.Equal (-1, combo.SelectedIndex);   // typing alone does not select

            combo.RaiseKeyUp (new KeyEventArgs (Keys.Enter));

            Assert.Equal (1, combo.SelectedIndex);
        }

        [Fact]
        public void AutoComplete_Append_completes_and_selects_the_remainder ()
        {
            using var combo = Combo ("apple", "apricot");
            combo.AutoCompleteSource = AutoCompleteSource.ListItems;
            combo.AutoCompleteMode = AutoCompleteMode.Append;

            Type (combo, "ap");

            // The second keystroke replaces the selected remainder rather than appending to it, which
            // is what makes typing through a completion work.
            Assert.Equal ("apple", combo.Text);
            Assert.Equal (2, combo.SelectionStart);
            Assert.Equal (3, combo.SelectionLength);
        }

        [Fact]
        public void AutoComplete_reads_a_custom_source ()
        {
            using var combo = Combo ("apple");
            combo.AutoCompleteCustomSource.Add ("banana");
            combo.AutoCompleteSource = AutoCompleteSource.CustomSource;
            combo.AutoCompleteMode = AutoCompleteMode.SuggestAppend;

            Type (combo, "b");

            // From the custom source, not the items -- "banana" is not an item.
            Assert.Equal ("banana", combo.Text);
        }

        [Fact]
        public void AutoComplete_stays_off_unless_it_is_asked_for ()
        {
            // Both halves have to be set, as upstream; a mode with no source completes nothing.
            using var combo = Combo ("apple");
            combo.AutoCompleteMode = AutoCompleteMode.Append;

            Type (combo, "a");
            Assert.Equal ("a", combo.Text);

            using var off = Combo ("apple");
            off.AutoCompleteSource = AutoCompleteSource.ListItems;

            Type (off, "a");
            Assert.Equal ("a", off.Text);
        }

        [Fact]
        public void The_edit_region_tracks_the_text_area_and_leaves_the_glyph_alone ()
        {
            using var combo = Combo ();
            combo.PerformLayout ();

            var width = combo.EditRegion.Width;

            combo.Width += 40;
            combo.PerformLayout ();

            // Derived on every layout, not stored once: a stored rectangle fails this.
            Assert.Equal (width + 40, combo.EditRegion.Width);

            // And it stops short of the drop-down glyph, or the caret would sit under the arrow.
            Assert.True (combo.EditRegion.Right + ComboBox.DropDownGlyphWidth <= combo.Width,
                $"edit right {combo.EditRegion.Right} + glyph {ComboBox.DropDownGlyphWidth} > width {combo.Width}");
        }

        [Fact]
        public void The_renderer_paints_the_area_the_edit_region_occupies ()
        {
            // GUARD, not proof: the arithmetic this replaced computed the same rectangle, so this
            // cannot fail against it. It pins ONE definition of the text area, on the control -- two,
            // the renderer's own and the control's, is how a caret ends up drawn under the glyph.
            using var combo = Combo ();
            var info = new SKImageInfo (combo.Width, combo.Height, SKImageInfo.PlatformColorType, SKAlphaType.Premul);
            using var bitmap = new SKBitmap (info);
            using var canvas = new SKCanvas (bitmap);
            var renderer = new ProbeRenderer ();

            var painted = renderer.TextArea (combo, new PaintEventArgs (info, canvas, 1.0));

            Assert.Equal (combo.EditAreaDeviceBounds, painted);
        }

        private sealed class ProbeRenderer : ComboBoxRenderer
        {
            internal System.Drawing.Rectangle TextArea (ComboBox control, PaintEventArgs e) => GetTextArea (control, e);
        }
    }
}
