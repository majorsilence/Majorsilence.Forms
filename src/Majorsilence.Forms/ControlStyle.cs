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
        /// Gets the background color defined anywhere in this style chain (instance style up through
        /// the type's default styles), or null when the chain never sets one. The root
        /// <see cref="Control.DefaultStyle"/> is excluded: its theme color is the definition of
        /// "nothing set", so a null result lets the control resolve its WinForms-style ambient
        /// background from its parent control instead.
        /// </summary>
        internal SKColor? TryGetBackgroundColor ()
            => ReferenceEquals (this, Control.DefaultStyle)
                ? null
                : BackgroundColor ?? _parent?.TryGetBackgroundColor ();

        /// <summary>
        /// Gets the foreground color defined anywhere in this style chain, or null when the chain
        /// never sets one (the root <see cref="Control.DefaultStyle"/> is excluded, as in
        /// <see cref="TryGetBackgroundColor"/>) so the control can resolve its WinForms-style ambient
        /// foreground from its parent control instead.
        /// </summary>
        internal SKColor? TryGetForegroundColor ()
            => ReferenceEquals (this, Control.DefaultStyle)
                ? null
                : ForegroundColor ?? _parent?.TryGetForegroundColor ();

        /// <summary>
        /// Gets the font defined anywhere in this style chain, or null when the chain never sets one
        /// (the root <see cref="Control.DefaultStyle"/> is excluded, as in
        /// <see cref="TryGetBackgroundColor"/>) so the control can resolve its WinForms-style ambient
        /// font from its parent control instead.
        /// </summary>
        internal SKTypeface? TryGetFont ()
            => ReferenceEquals (this, Control.DefaultStyle) ? null : Font ?? _parent?.TryGetFont ();

        /// <summary>Companion to <see cref="TryGetFont"/> for the font size.</summary>
        internal int? TryGetFontSize ()
            => ReferenceEquals (this, Control.DefaultStyle) ? null : FontSize ?? _parent?.TryGetFontSize ();

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
        /// Projects this style onto a <see cref="DataGridViewCellStyle"/>, used when the grid builds a
        /// cell's inherited style (the WinForms style cascade is expressed in DataGridViewCellStyle
        /// terms, while the grid stores its own default styles as ControlStyle). Only the members the
        /// two types share are copied; the Skia typeface is not converted back to a
        /// <see cref="Majorsilence.Forms.Drawing.Font"/>.
        /// </summary>
        internal DataGridViewCellStyle ToDataGridViewCellStyle () => new DataGridViewCellStyle {
            BackColor = BackColor,
            ForeColor = ForeColor,
            SelectionBackColor = SelectionBackColor,
            SelectionForeColor = SelectionForeColor,
            Alignment = Alignment,
            WrapMode = WrapMode,
            Padding = Padding,
        };

        /// <summary>
        /// Gets or sets the padding around content when this style is used as a grid cell style.
        /// </summary>
        /// <remarks>Carried across both conversions alongside <see cref="Alignment"/> and
        /// <see cref="WrapMode"/>. Grid cells read and restore this while they are sited, so it has to
        /// round-trip rather than merely exist.</remarks>
        public Padding Padding { get; set; } = Padding.Empty;

        /// <summary>
        /// Returns a copy of this style.
        /// </summary>
        /// <remarks>
        /// Cells clone a style before mutating it so the change does not leak into the style they inherited
        /// from. The copy is detached from the parent chain deliberately: it carries the values resolved at
        /// the time of the call, which is what a caller mutating a clone expects.
        /// </remarks>
        public ControlStyle Clone () => new ControlStyle (null, _ => { }) {
            BackColor = BackColor,
            ForeColor = ForeColor,
            SelectionBackColor = SelectionBackColor,
            SelectionForeColor = SelectionForeColor,
            Alignment = Alignment,
            WrapMode = WrapMode,
            Padding = Padding,
            Font = Font,
            FontSize = FontSize,
            BackgroundColor = BackgroundColor,
            ForegroundColor = ForegroundColor,
        };

        /// <summary>
        /// Gets or sets how text wraps when this style is used as a grid cell style.
        /// </summary>
        /// <remarks>Stored for <see cref="DataGridViewCellStyle"/> compatibility and carried across both
        /// conversions, the same as <see cref="Alignment"/>; the grid renderers apply their own wrapping.</remarks>
        public DataGridViewTriState WrapMode { get; set; } = DataGridViewTriState.NotSet;

        /// <summary>
        /// Converts a ControlStyle to a <see cref="DataGridViewCellStyle"/>, so WinForms code that reads a
        /// grid's cell-style property back into a DataGridViewCellStyle variable compiles.
        /// </summary>
        /// <remarks>
        /// The companion to the conversion below, which has existed for a while: assigning a
        /// DataGridViewCellStyle to one of the grid's ControlStyle-typed properties worked, and reading it
        /// back did not — so a derived grid could not re-expose <c>DefaultCellStyle</c> with the WinForms
        /// type, which is the first thing a themed grid does. Note it produces a copy, as the reverse
        /// direction always has: mutating the result changes the copy, so assign it back to have it stick.
        /// </remarks>
        public static implicit operator DataGridViewCellStyle (ControlStyle style)
            => style is null ? new DataGridViewCellStyle () : style.ToDataGridViewCellStyle ();

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
                Alignment = style.Alignment,
                WrapMode = style.WrapMode,
                Padding = style.Padding,
            };
            if (style.Font is { } font) {
                result.Font = font.GetSKTypeface ();

                // Pixels, not points -- see the note on Control.Font's setter.
                result.FontSize = (int)System.Math.Round (font.PixelSize);
            }
            return result;
        }
    }
}
