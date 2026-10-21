using SkiaSharp;

namespace Majorsilence.Forms
{
    // A ported WinForms app (one that chose its font with Application.SetDefaultFont) draws a disabled control's
    // text in the colour WinForms uses for that type under the Windows 11 theme, as measured from it: a label's and
    // a group box's TextRenderer.DisabledTextColor (ControlDark on a light background), a check box's and radio
    // button's theme colour and a text or combo box's GrayText (both #6D6D6D), and a button's #838383. The theme's one
    // grey (#AAAAAA) stood for all of them. Apps that have not chosen a font keep the theme's.
    internal static class UpstreamDisabledLook
    {
        internal static readonly SKColor ButtonText = new SKColor (0x83, 0x83, 0x83);

        internal static bool Applies => ControlPaint.UsesUpstreamGlyphs;

        // TextRenderer.DisabledTextColor: on a background darker than Control a darker shade of it, otherwise ControlDark.
        internal static SKColor TextOn (System.Drawing.Color back)
        {
            var darker = new ControlPaint.HLSColor (back).Luminosity < new ControlPaint.HLSColor (SystemColors.Control).Luminosity;
            return (darker ? ControlPaint.Dark (back) : SystemColors.ControlDark).ToSKColor ();
        }
    }

    public partial class Control
    {
        /// <summary>
        /// The colour this control's text is drawn in while it is disabled: the theme's, or under the upstream look
        /// the colour upstream draws the type's disabled text in (see UpstreamDisabledLook).
        /// </summary>
        internal virtual SKColor DisabledForeColor => Theme.ForegroundDisabledColor;
    }

    public partial class Label
    {
        /// <inheritdoc/>
        internal override SKColor DisabledForeColor
            => UpstreamDisabledLook.Applies ? UpstreamDisabledLook.TextOn (GetEffectiveBackgroundColor ().ToDrawingColor ()) : base.DisabledForeColor;
    }

    public partial class GroupBox
    {
        /// <inheritdoc/>
        internal override SKColor DisabledForeColor
            => UpstreamDisabledLook.Applies ? UpstreamDisabledLook.TextOn (GetEffectiveBackgroundColor ().ToDrawingColor ()) : base.DisabledForeColor;
    }

    public partial class CheckBox
    {
        /// <inheritdoc/>
        internal override SKColor DisabledForeColor
            => UpstreamDisabledLook.Applies ? SystemColors.GrayText.ToSKColor () : base.DisabledForeColor;
    }

    public partial class RadioButton
    {
        /// <inheritdoc/>
        internal override SKColor DisabledForeColor
            => UpstreamDisabledLook.Applies ? SystemColors.GrayText.ToSKColor () : base.DisabledForeColor;
    }

    public partial class Button
    {
        /// <inheritdoc/>
        internal override SKColor DisabledForeColor
            => UpstreamDisabledLook.Applies ? UpstreamDisabledLook.ButtonText : base.DisabledForeColor;
    }

    public partial class TextBoxBase
    {
        /// <inheritdoc/>
        internal override SKColor DisabledForeColor
            => UpstreamDisabledLook.Applies ? SystemColors.GrayText.ToSKColor () : base.DisabledForeColor;
    }

    public partial class ComboBox
    {
        /// <inheritdoc/>
        internal override SKColor DisabledForeColor
            => UpstreamDisabledLook.Applies ? SystemColors.GrayText.ToSKColor () : base.DisabledForeColor;
    }
}
