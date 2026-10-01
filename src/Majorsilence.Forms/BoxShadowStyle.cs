using SkiaSharp;

namespace Majorsilence.Forms
{
    /// <summary>
    /// A hard, offset, no-blur shadow (#285): a solid, flat copy of the control's own shape (its
    /// background and border radius, but not its content), shifted by (<see cref="OffsetX"/>,
    /// <see cref="OffsetY"/>) and painted behind it. There is deliberately no blur radius and no
    /// spread -- the CSS subset's <c>box-shadow</c> property accepts only
    /// <c>&lt;horizontal-offset&gt; &lt;vertical-offset&gt; &lt;color&gt;</c>, e.g.
    /// <c>box-shadow: 4px 4px #2b1b4d;</c>, and rejects anything with a blur radius or a spread rather
    /// than silently ignoring the extra component.
    /// </summary>
    public readonly struct ControlBoxShadow : IEquatable<ControlBoxShadow>
    {
        /// <summary>Initializes a new instance of the ControlBoxShadow struct.</summary>
        public ControlBoxShadow (int offsetX, int offsetY, SKColor color)
        {
            OffsetX = offsetX;
            OffsetY = offsetY;
            Color = color;
        }

        /// <summary>The horizontal offset in pixels; positive shifts the shadow right, negative left.</summary>
        public int OffsetX { get; }

        /// <summary>The vertical offset in pixels; positive shifts the shadow down, negative up.</summary>
        public int OffsetY { get; }

        /// <summary>The shadow's flat color -- no gradient, no blur, so one color is all there is.</summary>
        public SKColor Color { get; }

        /// <inheritdoc/>
        public bool Equals (ControlBoxShadow other) => OffsetX == other.OffsetX && OffsetY == other.OffsetY && Color == other.Color;

        /// <inheritdoc/>
        public override bool Equals (object? obj) => obj is ControlBoxShadow other && Equals (other);

        /// <inheritdoc/>
        public override int GetHashCode () => HashCode.Combine (OffsetX, OffsetY, Color);

        /// <inheritdoc/>
        public override string ToString () => $"{OffsetX}px {OffsetY}px {ThemeCssValues.FormatColor (Color)}";

        /// <summary>Equality.</summary>
        public static bool operator == (ControlBoxShadow left, ControlBoxShadow right) => left.Equals (right);

        /// <summary>Inequality.</summary>
        public static bool operator != (ControlBoxShadow left, ControlBoxShadow right) => !left.Equals (right);
    }
}
