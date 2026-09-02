using System.Drawing;

namespace Majorsilence.Forms.Renderers
{
    /// <summary>
    /// Renders a <see cref="CheckedListBox"/>: a list box row with a check box at its left.
    /// </summary>
    /// <remarks>
    /// Added for W5.7 (finding LST-02, P0). <c>RenderManager</c> routed <c>CheckedListBox</c> to
    /// <see cref="ListBoxRenderer"/>, which draws no glyph — so the control that exists to show check
    /// boxes showed a plain list, the user could not tick anything, and <c>CheckedItems</c> was always
    /// empty unless code pre-checked. The class called itself a "visual only stub"; it was not even
    /// that, because the visual was the part missing.
    /// </remarks>
    public class CheckedListBoxRenderer : ListBoxRenderer
    {
        /// <inheritdoc/>
        /// <remarks>Overridden so <c>RenderManager.SetRenderer&lt;CheckedListBox&gt;</c> accepts this
        /// renderer: registration requires the declared type to match the key, and it would otherwise
        /// inherit <c>typeof (ListBox)</c> from <see cref="ListBoxRenderer"/> and be rejected. Deriving
        /// from that renderer rather than from <c>Renderer&lt;CheckedListBox&gt;</c> is what lets the
        /// row background, selection, hover and focus rectangle stay in one place.</remarks>
        public override Type Type => typeof (CheckedListBox);

        /// <inheritdoc/>
        protected override void RenderItem (ListBox control, object item, int index, Rectangle bounds, PaintEventArgs e)
        {
            if (control is not CheckedListBox checkedList) {
                base.RenderItem (control, item, index, bounds, e);
                return;
            }

            // The row's own background, selection and focus rectangle are the base's job; only the
            // glyph and the text inset belong here.
            var glyph = checkedList.GlyphBounds (bounds);

            base.RenderItem (control, item, index, TextArea (checkedList, bounds), e);

            // The same glyph a CheckBox draws, through the same ControlPaint entry point, so the two
            // cannot drift apart.
            ControlPaint.DrawCheckBox (e, glyph, checkedList.GetItemCheckState (index), !checkedList.Enabled);
        }

        // The row minus the glyph column: passed to the base so the text starts after the box rather
        // than under it.
        private static Rectangle TextArea (CheckedListBox control, Rectangle bounds)
        {
            var inset = control.ScaledGlyphColumnWidth;

            return new Rectangle (bounds.Left + inset, bounds.Top, Math.Max (0, bounds.Width - inset), bounds.Height);
        }
    }
}
