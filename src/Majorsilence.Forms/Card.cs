using System.Drawing;

namespace Majorsilence.Forms
{
    /// <summary>
    /// A container drawn as a card: a rounded, bordered surface with padding, for grouping a post, a result or a setting on a
    /// phone-style screen. It is a <see cref="Panel"/>, so children are added, docked and anchored the usual way, and a
    /// <see cref="FlowLayoutPanel"/> or <see cref="TableLayoutPanel"/> inside it lays them out.
    /// </summary>
    /// <remarks>
    /// The look comes from the theme (<see cref="Theme.ControlLowColor"/> surface, <see cref="Theme.ControlMidHighColor"/> border), so
    /// it follows a light or dark theme. Everything it sets is ordinary <see cref="Control.Style"/> (<c>Border.Radius</c>,
    /// <c>BackgroundColor</c>, <c>Border.Color</c>), so a theme rule or code can restyle it like any other control.
    /// </remarks>
    public class Card : Panel
    {
        /// <summary>The corner radius, in logical pixels, a new card starts with.</summary>
        public const int DefaultCornerRadius = 12;

        /// <summary>The padding, in logical pixels, a new card starts with.</summary>
        public const int DefaultPaddingSize = 12;

        /// <summary>Initializes a new instance of the <see cref="Card"/> class.</summary>
        public Card ()
        {
            Padding = new Padding (DefaultPaddingSize);
        }

        /// <inheritdoc/>
        protected override Size DefaultSize => new Size (240, 96);

        /// <summary>The default style for all cards.</summary>
        public new static ControlStyle DefaultStyle = new ControlStyle (Panel.DefaultStyle,
            (style) => {
                style.BackgroundColor = Theme.ControlLowColor;
                style.Border.Width = 1;
                style.Border.Color = Theme.ControlMidHighColor;
                style.Border.Radius = DefaultCornerRadius;
            });

        /// <inheritdoc/>
        public override ControlStyle Style { get; } = new ControlStyle (DefaultStyle);

        /// <summary>Gets or sets the corner radius in logical pixels. Zero gives square corners.</summary>
        public int CornerRadius {
            get => Style.Border.GetRadius ();
            set {
                if (value < 0)
                    throw new System.ArgumentOutOfRangeException (nameof (value), "The corner radius cannot be negative.");

                Style.Border.Radius = value;
                Invalidate ();
            }
        }
    }
}
