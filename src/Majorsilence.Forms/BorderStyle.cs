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
        private int? radius, top_left_radius, top_right_radius, bottom_right_radius, bottom_left_radius;
        private ControlBorderLineStyle? line_style;
        private readonly BorderSideStyle left, top, right, bottom;

        /// <summary>
        /// Initializes a new instance of the ControlBorderStyle class.
        /// </summary>
        public ControlBorderStyle (ControlBorderStyle? parent)
        {
            _parent = parent;

            // the raw sides: the public getters can answer for a ThemeScope (see Active)
            left = new BorderSideStyle (_parent?.left) { OwnerBorder = this, Side = 0 };
            top = new BorderSideStyle (_parent?.top) { OwnerBorder = this, Side = 1 };
            right = new BorderSideStyle (_parent?.right) { OwnerBorder = this, Side = 2 };
            bottom = new BorderSideStyle (_parent?.bottom) { OwnerBorder = this, Side = 3 };
        }

        // The type-level ControlStyle this border belongs to, or null (see ControlStyle.ScopedTwin).
        internal ControlStyle? Owner { get; set; }

        // This border, or under a ThemeScope the scope's twin of it, whose values reads return.
        private ControlBorderStyle Active => Owner?.ScopedTwin?.Border ?? this;

        internal BorderSideStyle SideAt (int side) => side switch { 0 => left, 1 => top, 2 => right, _ => bottom };

        // For ControlStyle.RebuildAsTwin: this border's own values become `source`'s (or none).
        internal void CopyValuesFrom (ControlBorderStyle? source)
        {
            color = source?.color;
            width = source?.width;
            radius = source?.radius;
            top_left_radius = source?.top_left_radius;
            top_right_radius = source?.top_right_radius;
            bottom_right_radius = source?.bottom_right_radius;
            bottom_left_radius = source?.bottom_left_radius;
            line_style = source?.line_style;
            for (int i = 0; i < 4; i++)
                SideAt (i).CopyValuesFrom (source?.SideAt (i));
        }

        /// <summary>
        /// Gets the styles for the bottom border.
        /// </summary>
        public BorderSideStyle Bottom => Active.bottom;

        /// <summary>
        /// Gets or sets the color of all sides of the border.
        /// </summary>
        public SKColor? Color {
            get => Active.color;
            set {
                color = value;
                left.Color = value;
                right.Color = value;
                top.Color = value;
                bottom.Color = value;
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
        public BorderSideStyle Left => Active.left;

        /// <summary>
        /// Gets or sets the radius for all border corners.
        /// </summary>
        public int? Radius { get => Active.radius; set => radius = value; }

        /// <summary>Gets or sets the radius of the top-left corner only; wins over <see cref="Radius"/> (#286).</summary>
        public int? TopLeftRadius { get => Active.top_left_radius; set => top_left_radius = value; }

        /// <summary>Gets or sets the radius of the top-right corner only; wins over <see cref="Radius"/> (#286).</summary>
        public int? TopRightRadius { get => Active.top_right_radius; set => top_right_radius = value; }

        /// <summary>Gets or sets the radius of the bottom-right corner only; wins over <see cref="Radius"/> (#286).</summary>
        public int? BottomRightRadius { get => Active.bottom_right_radius; set => bottom_right_radius = value; }

        /// <summary>Gets or sets the radius of the bottom-left corner only; wins over <see cref="Radius"/> (#286).</summary>
        public int? BottomLeftRadius { get => Active.bottom_left_radius; set => bottom_left_radius = value; }

        /// <summary>Gets or sets whether the border is drawn solid or dashed; null inherits (#286).</summary>
        public ControlBorderLineStyle? LineStyle { get => Active.line_style; set => line_style = value; }

        /// <summary>
        /// Gets the styles for the right border.
        /// </summary>
        public BorderSideStyle Right => Active.right;

        /// <summary>
        /// Gets the styles for the top border.
        /// </summary>
        public BorderSideStyle Top => Active.top;

        /// <summary>
        /// Gets or sets the width of all sides of the border.
        /// </summary>
        public int? Width {
            get => Active.width;
            set {
                width = value;
                left.Width = value;
                right.Width = value;
                top.Width = value;
                bottom.Width = value;
            }
        }
    }

    /// <summary>Whether and how a control's outline is drawn (the CSS <c>outline-style</c> subset a theme accepts).</summary>
    public enum ControlOutlineStyle
    {
        /// <summary>No outline (the default).</summary>
        None,

        /// <summary>A continuous line. CSS's <c>auto</c> draws this too.</summary>
        Solid,

        /// <summary>A dashed line, dash and gap each three times the outline width.</summary>
        Dashed
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

        private SKColor? color;
        private int? width;

        // The border this side belongs to and which side it is, so a ThemeScope can answer for it.
        internal ControlBorderStyle? OwnerBorder { get; set; }
        internal int Side { get; set; }

        // This side, or under a ThemeScope the same side of the scope's twin border.
        private BorderSideStyle Active => OwnerBorder?.Owner?.ScopedTwin?.Border.SideAt (Side) ?? this;

        internal void CopyValuesFrom (BorderSideStyle? source)
        {
            color = source?.color;
            width = source?.width;
        }

        /// <summary>
        /// Gets or sets the color of this side of the border.
        /// </summary>
        public SKColor? Color { get => Active.color; set => color = value; }

        /// <summary>
        /// Gets the computed color of this side of the border.
        /// </summary>
        public SKColor GetColor () => Color ?? _parent?.GetColor () ?? Theme.BorderLowColor;

        /// <summary>
        /// Gets or sets the width of this side of the border.
        /// </summary>
        public int? Width { get => Active.width; set => width = value; }

        /// <summary>
        /// Gets the computed width of this side of the border.
        /// </summary>
        public int GetWidth () => Width ?? _parent?.GetWidth () ?? 0;
    }
}
