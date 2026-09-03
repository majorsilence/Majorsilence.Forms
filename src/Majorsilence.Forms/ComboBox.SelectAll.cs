namespace Majorsilence.Forms
{
    public partial class ComboBox
    {
        /// <summary>Selects all text in the editable portion.</summary>
        /// <remarks>Was an empty stub, so the standard "focus and select everything so the next
        /// keystroke replaces it" gesture did nothing (finding <c>LST-07</c>).</remarks>
        public void SelectAll () => EditRegion.SelectAll ();
    }
}
