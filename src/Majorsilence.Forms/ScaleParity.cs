namespace Majorsilence.Forms
{
    // Which controls keep their size along an axis when a form is auto-scaled (see
    // Control.ScaleControl). Upstream expresses these as ControlStyles.FixedWidth/FixedHeight, set as
    // each control's state changes; here they are answered from the current state.

    public partial class ComboBox
    {
        // As tall as its font makes it, unless it is the Simple style with its list always showing.
        internal override bool IsFixedHeightForScaling => DropDownStyle != ComboBoxStyle.Simple;
    }

    public partial class Label
    {
        // AutoSize: as big as its text, whatever the factor (re-measured after the scale).
        internal override bool SizesToContentWhenScaled => AutoSize;
    }

    public partial class CheckBox
    {
        internal override bool SizesToContentWhenScaled => AutoSize;
    }

    public partial class RadioButton
    {
        internal override bool SizesToContentWhenScaled => AutoSize;
    }

    public abstract partial class TextBoxBase
    {
        // A single-line box is as tall as its font makes it; a multiline one is sized by its owner.
        internal override bool IsFixedHeightForScaling => !Multiline;
    }

    public partial class ToolStrip
    {
        // An auto-sized strip's thickness comes from its items, not from the factor.
        internal override bool IsFixedHeightForScaling => Orientation == Orientation.Horizontal;
        internal override bool IsFixedWidthForScaling => Orientation == Orientation.Vertical;

        /// <inheritdoc/>
        /// <remarks>
        /// The strip's thickness is left alone by the scale itself (above) and then grown to whatever its
        /// items -- some of them hosted controls that were just scaled -- now need, which is what
        /// upstream's AutoSize layout does. Scaled with the rest, a legacy form's toolbars and status
        /// strip came out a third thicker than under WinForms.
        /// </remarks>
        protected override void ScaleCore (float dx, float dy)
        {
            base.ScaleCore (dx, dy);

            var preferred = GetPreferredSize (System.Drawing.Size.Empty);

            if (Orientation == Orientation.Horizontal) {
                if (preferred.Height > Height)
                    Height = preferred.Height;
            } else if (preferred.Width > Width) {
                Width = preferred.Width;
            }
        }
    }
}
