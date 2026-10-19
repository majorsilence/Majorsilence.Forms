using SkiaSharp;

namespace Majorsilence.Forms
{
    /// <summary>
    /// A shadow painted with a control's own shape (#285) -- the CSS <c>box-shadow</c> property. An outer shadow is a copy of the
    /// control's face (its background and border radius, not its content) moved by (<see cref="OffsetX"/>, <see cref="OffsetY"/>),
    /// grown by <see cref="Spread"/> and softened by <see cref="Blur"/>, painted behind it; an <see cref="Inset"/> shadow is cast inside
    /// the face, over the background.
    /// </summary>
    /// <remarks>
    /// Unlike a browser's, an outer shadow is painted inside the control's own bounds, which cannot draw outside themselves: the face
    /// shrinks by however far the shadow reaches past it on each side, so a control with a shadow should be sized with room for it.
    /// <c>box-shadow: 4px 4px #2b1b4d;</c> -- the hard, offset look -- takes 4px on the right and bottom.
    /// </remarks>
    public readonly struct ControlBoxShadow : IEquatable<ControlBoxShadow>
    {
        /// <summary>Initializes a hard (no blur, no spread) outer shadow.</summary>
        public ControlBoxShadow (int offsetX, int offsetY, SKColor color)
            : this (offsetX, offsetY, 0, 0, color, false)
        {
        }

        /// <summary>Initializes a shadow.</summary>
        /// <param name="offsetX">The horizontal offset in pixels; positive moves the shadow right.</param>
        /// <param name="offsetY">The vertical offset in pixels; positive moves the shadow down.</param>
        /// <param name="blur">The blur radius in pixels: how far the edge fades. Zero is a hard edge; negative is treated as zero.</param>
        /// <param name="spread">How far the shadow grows (positive) or shrinks (negative) before it is blurred.</param>
        /// <param name="color">The shadow's colour.</param>
        /// <param name="inset">Whether it is cast inside the face instead of behind it.</param>
        public ControlBoxShadow (int offsetX, int offsetY, int blur, int spread, SKColor color, bool inset)
        {
            OffsetX = offsetX;
            OffsetY = offsetY;
            Blur = Math.Max (0, blur);
            Spread = spread;
            Color = color;
            Inset = inset;
        }

        /// <summary>The horizontal offset in pixels; positive shifts the shadow right, negative left.</summary>
        public int OffsetX { get; }

        /// <summary>The vertical offset in pixels; positive shifts the shadow down, negative up.</summary>
        public int OffsetY { get; }

        /// <summary>The blur radius in pixels: zero is a hard edge, as the original form of this property drew.</summary>
        public int Blur { get; }

        /// <summary>How far the shadow is grown (positive) or shrunk (negative) before blurring, in pixels.</summary>
        public int Spread { get; }

        /// <summary>The shadow's colour.</summary>
        public SKColor Color { get; }

        /// <summary>Whether the shadow is cast inside the face (CSS <c>inset</c>) rather than behind it.</summary>
        public bool Inset { get; }

        /// <inheritdoc/>
        public bool Equals (ControlBoxShadow other)
            => OffsetX == other.OffsetX && OffsetY == other.OffsetY && Blur == other.Blur && Spread == other.Spread && Color == other.Color && Inset == other.Inset;

        /// <inheritdoc/>
        public override bool Equals (object? obj) => obj is ControlBoxShadow other && Equals (other);

        /// <inheritdoc/>
        public override int GetHashCode () => HashCode.Combine (OffsetX, OffsetY, Blur, Spread, Color, Inset);

        /// <inheritdoc/>
        public override string ToString ()
            => $"{(Inset ? "inset " : "")}{OffsetX}px {OffsetY}px{(Blur != 0 || Spread != 0 ? $" {Blur}px" : "")}{(Spread != 0 ? $" {Spread}px" : "")} {ThemeCssValues.FormatColor (Color)}";

        /// <summary>Equality.</summary>
        public static bool operator == (ControlBoxShadow left, ControlBoxShadow right) => left.Equals (right);

        /// <summary>Inequality.</summary>
        public static bool operator != (ControlBoxShadow left, ControlBoxShadow right) => !left.Equals (right);
    }
}
