using SkiaSharp;

namespace Majorsilence.Forms
{
    /// <summary>
    /// Defines the border style of a control or form.
    /// </summary>
    public class ControlBorderStyle
    {
        private readonly ControlBorderStyle? _parent;

        private SKColor? color;
        private int? width;

        /// <summary>
        /// Initializes a new instance of the ControlBorderStyle class.
        /// </summary>
        public ControlBorderStyle (ControlBorderStyle? parent)
        {
            _parent = parent;

            Left = new BorderSideStyle (_parent?.Left);
            Top = new BorderSideStyle (_parent?.Top);
            Right = new BorderSideStyle (_parent?.Right);
            Bottom = new BorderSideStyle (_parent?.Bottom);
        }

        /// <summary>
        /// Gets the styles for the bottom border.
        /// </summary>
        public BorderSideStyle Bottom { get; }

        /// <summary>
        /// Gets or sets the color of all sides of the border.
        /// </summary>
        public SKColor? Color {
            get => color;
            set {
                color = value;
                Left.Color = value;
                Right.Color = value;
                Top.Color = value;
                Bottom.Color = value;
            }
        }

        // Drops every value set on this border (all sides and the radius) so the owning style can be
        // rebuilt from its defaults; see ControlStyle.ResetWithStyleSheetRules.
        internal void ClearValues ()
        {
            Color = null;
            Width = null;
            Radius = null;
            TopLeftRadius = null;
            TopRightRadius = null;
            BottomRightRadius = null;
            BottomLeftRadius = null;
            LineStyle = null;
        }

        /// <summary>
        /// Gets the computed color of all sides of the border.
        /// </summary>
        public SKColor GetColor () => Color ?? _parent?.GetColor () ?? SKColor.Empty;

        /// <summary>
        /// Gets the computed radius for all border corners.
        /// </summary>
        public int GetRadius () => Radius ?? _parent?.GetRadius () ?? 0;

        /// <summary>
        /// Gets the computed radius of the top-left corner: its own value, else <see cref="Radius"/>,
        /// else the parent border's (#286).
        /// </summary>
        public int GetTopLeftRadius () => TopLeftRadius ?? Radius ?? _parent?.GetTopLeftRadius () ?? 0;

        /// <summary>Gets the computed radius of the top-right corner (see <see cref="GetTopLeftRadius"/>).</summary>
        public int GetTopRightRadius () => TopRightRadius ?? Radius ?? _parent?.GetTopRightRadius () ?? 0;

        /// <summary>Gets the computed radius of the bottom-right corner (see <see cref="GetTopLeftRadius"/>).</summary>
        public int GetBottomRightRadius () => BottomRightRadius ?? Radius ?? _parent?.GetBottomRightRadius () ?? 0;

        /// <summary>Gets the computed radius of the bottom-left corner (see <see cref="GetTopLeftRadius"/>).</summary>
        public int GetBottomLeftRadius () => BottomLeftRadius ?? Radius ?? _parent?.GetBottomLeftRadius () ?? 0;

        /// <summary>Gets whether any corner resolves to a radius greater than zero.</summary>
        public bool HasRadius => GetTopLeftRadius () > 0 || GetTopRightRadius () > 0 || GetBottomRightRadius () > 0 || GetBottomLeftRadius () > 0;

        /// <summary>
        /// Gets the computed line style of the border (#286): solid unless a style set it, or a parent did.
        /// </summary>
        public ControlBorderLineStyle GetLineStyle () => LineStyle ?? _parent?.GetLineStyle () ?? ControlBorderLineStyle.Solid;

        /// <summary>
        /// Gets the computed width for all border.
        /// </summary>
        public int GetWidth () => Width ?? _parent?.GetWidth () ?? 0;

        /// <summary>
        /// Gets the styles for the left border.
        /// </summary>
        public BorderSideStyle Left { get; }

        /// <summary>
        /// Gets or sets the radius for all border corners.
        /// </summary>
        public int? Radius { get; set; }

        /// <summary>Gets or sets the radius of the top-left corner only; wins over <see cref="Radius"/> (#286).</summary>
        public int? TopLeftRadius { get; set; }

        /// <summary>Gets or sets the radius of the top-right corner only; wins over <see cref="Radius"/> (#286).</summary>
        public int? TopRightRadius { get; set; }

        /// <summary>Gets or sets the radius of the bottom-right corner only; wins over <see cref="Radius"/> (#286).</summary>
        public int? BottomRightRadius { get; set; }

        /// <summary>Gets or sets the radius of the bottom-left corner only; wins over <see cref="Radius"/> (#286).</summary>
        public int? BottomLeftRadius { get; set; }

        /// <summary>Gets or sets whether the border is drawn solid or dashed; null inherits (#286).</summary>
        public ControlBorderLineStyle? LineStyle { get; set; }

        /// <summary>
        /// Gets the styles for the right border.
        /// </summary>
        public BorderSideStyle Right { get; }

        /// <summary>
        /// Gets the styles for the top border.
        /// </summary>
        public BorderSideStyle Top { get; }

        /// <summary>
        /// Gets or sets the width of all sides of the border.
        /// </summary>
        public int? Width {
            get => width;
            set {
                width = value;
                Left.Width = value;
                Right.Width = value;
                Top.Width = value;
                Bottom.Width = value;
            }
        }
    }

    /// <summary>How a border's line is drawn (the CSS <c>border-style</c> subset a theme accepts, #286).</summary>
    public enum ControlBorderLineStyle
    {
        /// <summary>A continuous line.</summary>
        Solid,

        /// <summary>A dashed line, dash and gap each three times the border width.</summary>
        Dashed
    }

    /// <summary>
    /// Defines the border style for a single side of a control or form.
    /// </summary>
    public class BorderSideStyle
    {
        private readonly BorderSideStyle? _parent;

        /// <summary>
        /// Initializes a new instance of the BorderSideStyle class.
        /// </summary>
        public BorderSideStyle (BorderSideStyle? parent) => _parent = parent;

        /// <summary>
        /// Gets or sets the color of this side of the border.
        /// </summary>
        public SKColor? Color { get; set; }

        /// <summary>
        /// Gets the computed color of this side of the border.
        /// </summary>
        public SKColor GetColor () => Color ?? _parent?.GetColor () ?? Theme.BorderLowColor;

        /// <summary>
        /// Gets or sets the width of this side of the border.
        /// </summary>
        public int? Width { get; set; }

        /// <summary>
        /// Gets the computed width of this side of the border.
        /// </summary>
        public int GetWidth () => Width ?? _parent?.GetWidth () ?? 0;
    }
}
