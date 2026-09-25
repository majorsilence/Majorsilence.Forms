using System.Drawing;

namespace Majorsilence.Forms.Renderers
{
    /// <summary>
    /// Represents a class that can render a GroupBox.
    /// </summary>
    public class GroupBoxRenderer : Renderer<GroupBox>
    {
        /// <inheritdoc/>
        protected override void Render (GroupBox control, PaintEventArgs e)
        {
            var bounds = control.ClientRectangle;

            // Caption font/color resolve like any control text: the ambient effective font, not
            // the theme chrome font. The title band matches the caption inset DisplayRectangle
            // reserves, so docked children start below the caption and the border midline splits
            // the caption text like real WinForms.
            var font = control.GetEffectiveFont ();
            var font_size = control.LogicalToDeviceUnits (control.GetEffectiveFontSize ());
            var title_height = control.LogicalToDeviceUnits (control.CaptionHeight);
            var border_top = title_height / 2;

            // Border rect starts below the midpoint of the title text
            var border = new Rectangle (bounds.X + 1, bounds.Y + border_top, bounds.Width - 2, bounds.Height - border_top - 1);
            e.Canvas.DrawRectangle (border, Theme.BorderLowColor);

            // FlatStyle (W6 mechanisms): the etched frame is the dark line with a highlight line one
            // pixel inside it, as GroupBoxRenderer upstream draws for every style but Flat.
            if (control.FlatStyle != FlatStyle.Flat) {
                var inner = new Rectangle (border.X + 1, border.Y + 1, border.Width - 2, border.Height - 2);

                if (inner.Width > 0 && inner.Height > 0)
                    e.Canvas.DrawRectangle (inner, Theme.ControlHighColor);
            }

            if (!string.IsNullOrEmpty (control.Text)) {
                var text_size = TextMeasurer.MeasureText (control.Text, font, font_size);
                var text_width = (int)text_size.Width + 6;
                var text_x = bounds.X + 10;

                // Clear border behind title
                e.Canvas.FillRectangle (text_x - 2, bounds.Y, text_width + 4, title_height, control.GetEffectiveBackgroundColor ());

                var text_bounds = new Rectangle (text_x, bounds.Y, text_width, title_height);
                // A GroupBox caption interprets the '&' mnemonic prefix.
                e.Canvas.DrawMnemonicText (control.Text, font, font_size, text_bounds,
                    control.Enabled ? control.GetEffectiveForegroundColor () : Theme.ForegroundDisabledColor,
                    ContentAlignment.MiddleLeft, maxLines: 1);
            }
        }
    }
}
