using System;
using System.Linq;
using Xunit;

namespace Majorsilence.Forms.Tests
{
    // Enum values, not enum names. Designer and .resx code persists these as raw integers, so a
    // member with the right name and the wrong number corrupts data on round-trip rather than
    // failing to compile -- which is why these are asserted as numbers.
    public class EnumValueParityTests
    {
        [Theory]
        [InlineData (AccessibleRole.SplitButton, 62)]
        [InlineData (AccessibleRole.IpAddress, 63)]
        [InlineData (AccessibleRole.OutlineButton, 64)]
        [InlineData (AutoCompleteSource.ListItems, 256)]
        [InlineData (ControlStyles.EnableNotifyMessage, 0x8000)]
        [InlineData (ControlStyles.ApplyThemingImplicitly, 0x80000)]
        [InlineData (DataGridViewAutoSizeRowsMode.AllHeaders, 5)]
        [InlineData (DataGridViewAutoSizeRowsMode.DisplayedHeaders, 9)]
        [InlineData (DataGridViewDataErrorContexts.RowDeletion, 8)]
        [InlineData (DataGridViewDataErrorContexts.ClipboardContent, 0x4000)]
        [InlineData (DataGridViewEditMode.EditOnF2, 3)]
        [InlineData (DialogResult.TryAgain, 10)]
        [InlineData (DialogResult.Continue, 11)]
        [InlineData (ImeMode.OnHalf, 12)]
        [InlineData (MessageBoxButtons.CancelTryContinue, 6)]
        [InlineData (MessageBoxDefaultButton.Button4, 768)]
        [InlineData (TreeViewDrawMode.OwnerDrawText, 1)]
        [InlineData (TreeViewDrawMode.OwnerDrawAll, 2)]
        public void An_enum_member_has_the_number_winforms_persists (object member, int expected)
        {
            Assert.Equal (expected, Convert.ToInt32 (member));
        }

        [Fact]
        public void The_old_tree_view_draw_mode_name_is_the_same_value ()
        {
#pragma warning disable CS0618 // The alias is deliberately kept so existing code still compiles.
            Assert.Equal (TreeViewDrawMode.OwnerDrawText, TreeViewDrawMode.OwnerDrawContent);
#pragma warning restore CS0618
        }

        [Fact]
        public void No_data_error_context_shares_a_number_with_another ()
        {
            // Two members with one value make ToString() pick arbitrarily, which is how a persisted
            // context comes back naming something the writer never chose.
            var values = Enum.GetValues<DataGridViewDataErrorContexts> ();

            Assert.Equal (values.Length, values.Distinct ().Count ());
        }
    }
}
