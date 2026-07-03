using Majorsilence.Forms.Telerik;
using Xunit;

namespace Majorsilence.Forms.Tests
{
    // Exercises the RichTextEditorRibbonBar element-tree stub that exists solely so
    // Code/libSettings/Forms/frmEmailTemplates.Designer.vb's chained GetChildAt(...) casts succeed at
    // runtime instead of throwing InvalidCastException (see src/Majorsilence.Forms/Telerik/RadRichTextEditorRibbon.cs).
    public class RichTextEditorRibbonBarTests
    {
        [Fact]
        public void GetChildAt_zero_returns_the_RadRibbonBarElement ()
        {
            using var bar = new RichTextEditorRibbonBar ();

            var root = bar.GetChildAt (0);

            Assert.IsType<RadRibbonBarElement> (root);
            Assert.Same (bar.RibbonBarElement, root);
        }

        [Fact]
        public void Save_button_path_casts_to_RadButtonElement ()
        {
            // EmailTemplateBodyRibbonBar.GetChildAt(0).GetChildAt(2).GetChildAt(0).GetChildAt(2).GetChildAt(0)
            using var bar = new RichTextEditorRibbonBar ();

            var node = bar.GetChildAt (0).GetChildAt (2).GetChildAt (0).GetChildAt (2).GetChildAt (0);

            Assert.IsType<RadButtonElement> (node);
        }

        [Fact]
        public void Tab_strip_container_path_casts_to_StripViewItemContainer ()
        {
            // EmailTemplateBodyRibbonBar.GetChildAt(0).GetChildAt(4).GetChildAt(0)
            using var bar = new RichTextEditorRibbonBar ();

            var node = bar.GetChildAt (0).GetChildAt (4).GetChildAt (0);

            Assert.IsType<StripViewItemContainer> (node);
        }

        [Theory]
        [InlineData (1)]
        [InlineData (2)]
        [InlineData (3)]
        [InlineData (4)]
        [InlineData (5)]
        [InlineData (6)]
        public void Ribbon_tab_paths_cast_to_RichTextEditorRibbonTab (int tabIndex)
        {
            // EmailTemplateBodyRibbonBar.GetChildAt(0).GetChildAt(4).GetChildAt(0).GetChildAt(0).GetChildAt(N)
            using var bar = new RichTextEditorRibbonBar ();

            var node = bar.GetChildAt (0).GetChildAt (4).GetChildAt (0).GetChildAt (0).GetChildAt (tabIndex);

            Assert.IsType<RichTextEditorRibbonTab> (node);
        }

        [Fact]
        public void Repeated_calls_along_the_same_path_return_the_same_instance ()
        {
            // Designer code sets multiple properties on "the same" element via repeated GetChildAt chains
            // (one CType per property) — each call must resolve to the same object, not a fresh stub.
            using var bar = new RichTextEditorRibbonBar ();

            var first = bar.GetChildAt (0).GetChildAt (2).GetChildAt (0).GetChildAt (2).GetChildAt (0);
            var second = bar.GetChildAt (0).GetChildAt (2).GetChildAt (0).GetChildAt (2).GetChildAt (0);

            Assert.Same (first, second);
        }

        [Fact]
        public void AssociatedRichTextEditor_round_trips ()
        {
            using var bar = new RichTextEditorRibbonBar ();
            using var editor = new RadRichTextEditor ();

            bar.AssociatedRichTextEditor = editor;

            Assert.Same (editor, bar.AssociatedRichTextEditor);
        }

        [Fact]
        public void SimplifiedHeight_defaults_to_toolbar_height ()
        {
            using var bar = new RichTextEditorRibbonBar ();
            Assert.Equal (46, bar.SimplifiedHeight);
        }

        [Fact]
        public void LayoutMode_defaults_to_Simplified ()
        {
            using var bar = new RichTextEditorRibbonBar ();
            Assert.Equal (RibbonLayout.Simplified, bar.LayoutMode);
        }
    }
}
