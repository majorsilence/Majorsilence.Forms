using System.Collections.Generic;
using System.Linq;

namespace Majorsilence.Forms
{
    /// <summary>
    /// A styleable piece *inside* a control -- a grid's column headers, the selected tab, a hovered menu
    /// item, a scroll bar's thumb -- addressed in CSS as a pseudo-element on the control's selector:
    /// <c>DataGridView::header { background-color: #222; }</c>. Each part is a named type-level
    /// <see cref="ControlStyle"/> the renderer reads with the theme tokens as its defaults, so a theme
    /// that sets only tokens is unaffected, and a rule on the part overrides just that piece.
    /// </summary>
    public sealed class ThemeCssPart
    {
        internal ThemeCssPart (string name, string description, Func<ControlStyle> getStyle, Func<ControlStyle>? getHoverStyle, params string[] properties)
        {
            Name = name;
            Description = description;
            GetStyle = getStyle;
            GetHoverStyle = getHoverStyle;
            Properties = properties;
        }

        /// <summary>The part name after <c>::</c>, e.g. <c>header</c>, <c>selection</c>, <c>thumb</c>.</summary>
        public string Name { get; }

        /// <summary>What the part is and what each accepted property changes on it.</summary>
        public string Description { get; }

        /// <summary>
        /// The longhand properties the part honours. Anything else is an error naming this list: a part
        /// only paints what its renderer reads, and a silently ignored declaration is the one thing the
        /// subset is designed never to have.
        /// </summary>
        public IReadOnlyList<string> Properties { get; }

        /// <summary>Whether <c>Type::part:hover</c> is meaningful -- only parts whose renderer tracks item hover.</summary>
        public bool SupportsHover => GetHoverStyle is not null;

        internal Func<ControlStyle> GetStyle { get; }
        internal Func<ControlStyle>? GetHoverStyle { get; }

        /// <summary>The type-level style a <c>Type::part</c> rule sets and the renderer reads.</summary>
        public ControlStyle Style => GetStyle ();

        /// <summary>The style a <c>Type::part:hover</c> rule sets, or null when the part has no hover state.</summary>
        public ControlStyle? HoverStyle => GetHoverStyle?.Invoke ();

        /// <summary>Whether the part honours a (longhand) property.</summary>
        public bool Accepts (string property) => Properties.Contains (property, StringComparer.OrdinalIgnoreCase);

        /// <inheritdoc/>
        public override string ToString () => "::" + Name;
    }
}
