namespace Majorsilence.Forms
{
    /// <summary>
    /// The in-place editor behind <see cref="ListView.LabelEdit"/> and <see cref="TreeView.LabelEdit"/>
    /// (W6 mechanisms): a single-line text box the owning control positions over the label. Enter
    /// commits, Escape cancels, and losing focus commits, which is the upstream native edit control's
    /// behaviour. The owner supplies the two callbacks and decides what "commit" means.
    /// </summary>
    internal sealed class LabelEditBox : TextBox
    {
        internal System.Action? Commit { get; set; }
        internal System.Action? Cancel { get; set; }

        // Enter and Escape are this control's, not the form's default or cancel button's: the same
        // claim the native edit control makes while a label is being edited.
        protected override bool IsInputKey (Keys keyData)
            => (keyData & Keys.KeyCode) is Keys.Return or Keys.Escape || base.IsInputKey (keyData);

        protected override void OnKeyDown (KeyEventArgs e)
        {
            switch (e.KeyCode) {
            case Keys.Return:
                e.Handled = true;
                Commit?.Invoke ();
                return;
            case Keys.Escape:
                e.Handled = true;
                Cancel?.Invoke ();
                return;
            }

            base.OnKeyDown (e);
        }

        protected override void OnLostFocus (System.EventArgs e)
        {
            base.OnLostFocus (e);
            Commit?.Invoke ();
        }
    }
}
