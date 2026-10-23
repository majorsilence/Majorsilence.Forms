namespace Majorsilence.Forms
{
    /// <summary>
    /// A selector's CSS specificity: how many ids, how many classes and pseudo-classes, and how many types it names. When two rules
    /// set the same property on a control, the more specific one wins whatever their order; equal specificity goes to the later rule.
    /// <c>Button</c> is (0,0,1), <c>.primary</c> (0,1,0), <c>Button:hover</c> and <c>Button.primary</c> (0,1,1), <c>.primary:hover</c>
    /// (0,2,0) and <c>#save</c> (1,0,0).
    /// </summary>
    public readonly struct ThemeCssSpecificity : IComparable<ThemeCssSpecificity>, IEquatable<ThemeCssSpecificity>
    {
        /// <summary>Creates a specificity.</summary>
        /// <param name="ids">The number of <c>#id</c> selectors.</param>
        /// <param name="classes">The number of <c>.class</c> selectors and pseudo-classes.</param>
        /// <param name="types">The number of type selectors and <c>::part</c> pseudo-elements.</param>
        public ThemeCssSpecificity (int ids, int classes, int types)
        {
            Ids = ids;
            Classes = classes;
            Types = types;
        }

        /// <summary>The number of <c>#id</c> selectors (the most significant).</summary>
        public int Ids { get; }

        /// <summary>The number of <c>.class</c> selectors and pseudo-classes.</summary>
        public int Classes { get; }

        /// <summary>The number of type selectors and <c>::part</c> pseudo-elements (the least significant).</summary>
        public int Types { get; }

        /// <inheritdoc/>
        public int CompareTo (ThemeCssSpecificity other)
        {
            var ids = Ids.CompareTo (other.Ids);
            if (ids != 0)
                return ids;

            var classes = Classes.CompareTo (other.Classes);
            return classes != 0 ? classes : Types.CompareTo (other.Types);
        }

        /// <inheritdoc/>
        public bool Equals (ThemeCssSpecificity other) => CompareTo (other) == 0;

        /// <inheritdoc/>
        public override bool Equals (object? obj) => obj is ThemeCssSpecificity other && Equals (other);

        /// <inheritdoc/>
        public override int GetHashCode () => (Ids * 397 ^ Classes) * 397 ^ Types;

        /// <summary>Whether <paramref name="left"/> is less specific than <paramref name="right"/>.</summary>
        public static bool operator < (ThemeCssSpecificity left, ThemeCssSpecificity right) => left.CompareTo (right) < 0;

        /// <summary>Whether <paramref name="left"/> is more specific than <paramref name="right"/>.</summary>
        public static bool operator > (ThemeCssSpecificity left, ThemeCssSpecificity right) => left.CompareTo (right) > 0;

        /// <summary>Whether <paramref name="left"/> is at most as specific as <paramref name="right"/>.</summary>
        public static bool operator <= (ThemeCssSpecificity left, ThemeCssSpecificity right) => left.CompareTo (right) <= 0;

        /// <summary>Whether <paramref name="left"/> is at least as specific as <paramref name="right"/>.</summary>
        public static bool operator >= (ThemeCssSpecificity left, ThemeCssSpecificity right) => left.CompareTo (right) >= 0;

        /// <summary>Whether the two specificities are equal.</summary>
        public static bool operator == (ThemeCssSpecificity left, ThemeCssSpecificity right) => left.Equals (right);

        /// <summary>Whether the two specificities differ.</summary>
        public static bool operator != (ThemeCssSpecificity left, ThemeCssSpecificity right) => !left.Equals (right);

        /// <summary>The specificity as CSS writes it, e.g. <c>(0,1,1)</c>.</summary>
        public override string ToString () => $"({Ids},{Classes},{Types})";
    }
}
