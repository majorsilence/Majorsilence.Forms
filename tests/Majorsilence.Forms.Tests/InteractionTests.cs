using Microsoft.VisualBasic;
using Xunit;

namespace Majorsilence.Forms.Tests
{
    // Majorsilence.Forms.Interaction.MsgBox maps VB's MsgBoxStyle onto MessageBoxButtons/MessageBoxIcon and
    // DialogResult back onto MsgBoxResult by a straight numeric cast. That is only correct because the
    // enum values line up exactly -- pin that assumption so a future enum edit can't silently break MsgBox.
    public class InteractionTests
    {
        [Theory]
        [InlineData (MsgBoxStyle.OkOnly, MessageBoxButtons.OK)]
        [InlineData (MsgBoxStyle.OkCancel, MessageBoxButtons.OKCancel)]
        [InlineData (MsgBoxStyle.AbortRetryIgnore, MessageBoxButtons.AbortRetryIgnore)]
        [InlineData (MsgBoxStyle.YesNoCancel, MessageBoxButtons.YesNoCancel)]
        [InlineData (MsgBoxStyle.YesNo, MessageBoxButtons.YesNo)]
        [InlineData (MsgBoxStyle.RetryCancel, MessageBoxButtons.RetryCancel)]
        public void MsgBoxStyle_button_bits_match_MessageBoxButtons (MsgBoxStyle style, MessageBoxButtons expected)
            => Assert.Equal ((int) expected, (int) style & 0xF);

        [Theory]
        [InlineData (MsgBoxStyle.Critical, MessageBoxIcon.Error)]
        [InlineData (MsgBoxStyle.Question, MessageBoxIcon.Question)]
        [InlineData (MsgBoxStyle.Exclamation, MessageBoxIcon.Warning)]
        [InlineData (MsgBoxStyle.Information, MessageBoxIcon.Information)]
        public void MsgBoxStyle_icon_bits_match_MessageBoxIcon (MsgBoxStyle style, MessageBoxIcon expected)
            => Assert.Equal ((int) expected, (int) style & 0xF0);

        [Theory]
        [InlineData (DialogResult.OK, MsgBoxResult.Ok)]
        [InlineData (DialogResult.Cancel, MsgBoxResult.Cancel)]
        [InlineData (DialogResult.Abort, MsgBoxResult.Abort)]
        [InlineData (DialogResult.Retry, MsgBoxResult.Retry)]
        [InlineData (DialogResult.Ignore, MsgBoxResult.Ignore)]
        [InlineData (DialogResult.Yes, MsgBoxResult.Yes)]
        [InlineData (DialogResult.No, MsgBoxResult.No)]
        public void DialogResult_matches_MsgBoxResult (DialogResult dr, MsgBoxResult expected)
            => Assert.Equal ((int) expected, (int) dr);

        [Fact]
        public void Interaction_exposes_MsgBox_and_InputBox ()
        {
            // Signature/shape guard: the migrator rewrites bare MsgBox(...)/InputBox(...) to these, so they
            // must exist with VB-compatible signatures (MsgBoxStyle in / MsgBoxResult out; string InputBox).
            var msgBox = typeof (Interaction).GetMethod (nameof (Interaction.MsgBox));
            Assert.NotNull (msgBox);
            Assert.Equal (typeof (MsgBoxResult), msgBox!.ReturnType);

            var inputBox = typeof (Interaction).GetMethod (nameof (Interaction.InputBox));
            Assert.NotNull (inputBox);
            Assert.Equal (typeof (string), inputBox!.ReturnType);
        }
    }
}
