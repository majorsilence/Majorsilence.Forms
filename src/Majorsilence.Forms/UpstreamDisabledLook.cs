using SkiaSharp;

namespace Majorsilence.Forms
{
    // A ported WinForms app (one that chose its font with Application.SetDefaultFont) draws a disabled control's
    // text in the colour WinForms uses for that type under the Windows 11 theme, as measured from it: a label's and
    // a group box's TextRenderer.DisabledTextColor (ControlDark on a light background), a check box's and radio
    // button's shadow colour of the background it sits on (ControlDark on Control; GrayText for a FlatStyle.System
    // one), a text or combo box's GrayText (#6D6D6D), and a button's #A0A0A0 (#838383 for a FlatStyle.System one).
    // The theme's one grey (#AAAAAA) stood for all of them. Apps that have not chosen a font keep the theme's.
    internal static class UpstreamDisabledLook
    {
        // A button's: #A0A0A0 for a Standard one, themed or not, and #838383 for the native FlatStyle.System one.
        internal static readonly SKColor ButtonText = new SKColor (0xA0, 0xA0, 0xA0);
        internal static readonly SKColor SystemButtonText = new SKColor (0x83, 0x83, 0x83);

        internal static bool Applies => ControlPaint.UsesUpstreamGlyphs;

        // TextRenderer.DisabledTextColor: on a background darker than Control a darker shade of it, otherwise ControlDark.
        internal static SKColor TextOn (System.Drawing.Color back)
        {
            var darker = new ControlPaint.HLSColor (back).Luminosity < new ControlPaint.HLSColor (SystemColors.Control).Luminosity;
            return (darker ? ControlPaint.Dark (back) : SystemColors.ControlDark).ToSKColor ();
        }

        // A check box's or radio button's: the shadow colour of its background, as ButtonBase paints one -- ControlDark
        // on Control, otherwise ControlPaint.Dark of it -- and the native control's GrayText for a FlatStyle.System one.
        // Measured (.NET 10, Windows 11): #A0A0A0 on a form, #555555 on a themed (white) tab page, and #234A7E on a
        // tab page coloured (233, 240, 249); Standard, Flat and Popup alike, themed or not.
        internal static SKColor CheckTextOn (System.Drawing.Color back, FlatStyle style)
        {
            if (style == FlatStyle.System)
                return SystemColors.GrayText.ToSKColor ();

            return (back.ToArgb () == SystemColors.Control.ToArgb () ? SystemColors.ControlDark : ControlPaint.Dark (back)).ToSKColor ();
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
            => UpstreamDisabledLook.Applies ? UpstreamDisabledLook.CheckTextOn (GetEffectiveBackgroundColor ().ToDrawingColor (), FlatStyle) : base.DisabledForeColor;
    }

    public partial class RadioButton
    {
        /// <inheritdoc/>
        internal override SKColor DisabledForeColor
            => UpstreamDisabledLook.Applies ? UpstreamDisabledLook.CheckTextOn (GetEffectiveBackgroundColor ().ToDrawingColor (), FlatStyle) : base.DisabledForeColor;
    }

    public partial class Button
    {
        /// <inheritdoc/>
        internal override SKColor DisabledForeColor
            => !UpstreamDisabledLook.Applies ? base.DisabledForeColor
                : FlatStyle == FlatStyle.System ? UpstreamDisabledLook.SystemButtonText
                : UpstreamDisabledLook.ButtonText;
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
