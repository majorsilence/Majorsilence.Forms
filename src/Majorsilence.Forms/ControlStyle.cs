using SkiaSharp;

namespace Majorsilence.Forms
{
    /// <summary>
    /// Defines the style of a control.
    /// </summary>
    public class ControlStyle
    {
        internal readonly ControlStyle? _parent;

        /// <summary>
        /// Initializes a new instance of the ControlStyle class.  This constructor is
        /// generally used by the static DefaultStyle property.
        /// </summary>
        public ControlStyle (ControlStyle? parent, Action<ControlStyle> setDefaults)
        {
            _parent = parent;

            Border = new ControlBorderStyle (parent?.Border);

            setDefaults (this);

            Theme.ThemeChanged += (o, e) => setDefaults (this);
        }

        /// <summary>
        /// Initializes a new instance of the ControlStyle class.  This constructor is
        /// generally used by the instance Style property.
        /// </summary>
        public ControlStyle (ControlStyle parent)
        {
            _parent = parent;

            Border = new ControlBorderStyle (parent?.Border);
        }

        /// <summary>
        /// Gets or sets how cell content is aligned when this style is used as a grid cell style.
        /// Stored for WinForms DataGridViewCellStyle compatibility; grid renderers currently apply
        /// their own per-column alignment.
        /// </summary>
        public DataGridViewContentAlignment Alignment { get; set; } = DataGridViewContentAlignment.NotSet;

        /// <summary>
        /// Gets or sets the background color.
        /// </summary>
        public SKColor? BackgroundColor { get; set; }

        /// <summary>
        /// Provides access to border style properties.
        /// </summary>
        public ControlBorderStyle Border { get; }

        /// <summary>
        /// Gets or sets the font.
        /// </summary>
        public SKTypeface? Font { get; set; }

        /// <summary>
        /// Gets or sets the font size.
        /// </summary>
        public int? FontSize { get; set; }

        /// <summary>
        /// Gets or sets the foreground color.
        /// </summary>
        public SKColor? ForegroundColor { get; set; }

        /// <summary>
        /// Gets the computed background color.
        /// </summary>
        public SKColor GetBackgroundColor () => BackgroundColor ?? _parent?.GetBackgroundColor () ?? Theme.ControlMidColor;

        /// <summary>
        /// Gets the computed font.
        /// </summary>
        public SKTypeface GetFont () => Font ?? _parent?.GetFont () ?? Theme.UIFont;

        /// <summary>
        /// Gets the computed font size.
        /// </summary>
        public int GetFontSize () => FontSize ?? _parent?.GetFontSize () ?? Theme.FontSize;

        /// <summary>
        /// Gets the computed foreground color.
        /// </summary>
        public SKColor GetForegroundColor () => ForegroundColor ?? _parent?.GetForegroundColor () ?? Theme.ForegroundColor;

        // WinForms compatibility (DataGridViewCellStyle surface): System.Drawing.Color accessors.
        // BackColor/ForeColor bridge to the underlying SkiaSharp colors; the Selection* colors are
        // stored for compatibility (Majorsilence.Forms paints selection via the theme).

        /// <summary>Gets or sets the background color as a <see cref="System.Drawing.Color"/>. WinForms compatibility.</summary>
        public System.Drawing.Color BackColor {
            get => BackgroundColor is { } c ? System.Drawing.Color.FromArgb (c.Alpha, c.Red, c.Green, c.Blue) : System.Drawing.Color.Empty;
            set => BackgroundColor = value.IsEmpty ? null : new SKColor (value.R, value.G, value.B, value.A);
        }

        /// <summary>Gets or sets the foreground color as a <see cref="System.Drawing.Color"/>. WinForms compatibility.</summary>
        public System.Drawing.Color ForeColor {
            get => ForegroundColor is { } c ? System.Drawing.Color.FromArgb (c.Alpha, c.Red, c.Green, c.Blue) : System.Drawing.Color.Empty;
            set => ForegroundColor = value.IsEmpty ? null : new SKColor (value.R, value.G, value.B, value.A);
        }

        /// <summary>Gets or sets the selection background color. WinForms compatibility stub (stored, not rendered).</summary>
        public System.Drawing.Color SelectionBackColor { get; set; } = System.Drawing.Color.Empty;

        /// <summary>Gets or sets the selection foreground color. WinForms compatibility stub (stored, not rendered).</summary>
        public System.Drawing.Color SelectionForeColor { get; set; } = System.Drawing.Color.Empty;

        /// <summary>
        /// Converts a DataGridViewCellStyle to a ControlStyle, so WinForms-style designer code
        /// (`grid.ColumnHeadersDefaultCellStyle = new DataGridViewCellStyle { BackColor = ...,
        /// Font = ..., ... };`) can assign directly to a ControlStyle-typed property.
        /// </summary>
        public static implicit operator ControlStyle (DataGridViewCellStyle style)
        {
            var result = new ControlStyle (null, _ => { }) {
                BackColor = style.BackColor,
                ForeColor = style.ForeColor,
                SelectionBackColor = style.SelectionBackColor,
                SelectionForeColor = style.SelectionForeColor,
            };
            if (style.Font is { } font) {
                result.Font = font.GetSKTypeface ();
                result.FontSize = (int)font.SizeInPoints;
            }
            return result;
        }
    }
}
