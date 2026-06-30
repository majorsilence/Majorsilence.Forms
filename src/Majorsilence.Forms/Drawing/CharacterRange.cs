namespace Majorsilence.Drawing
{
    /// <summary>
    /// Specifies a range of character positions within a string. Cross-platform replacement for
    /// <c>System.Drawing.CharacterRange</c>.
    /// </summary>
    public class CharacterRange
    {
        /// <summary>Gets or sets the index of the first character in the range.</summary>
        public int First { get; set; }

        /// <summary>Gets or sets the number of characters in the range.</summary>
        public int Length { get; set; }

        /// <summary>Initializes a new CharacterRange with the specified first index and length.</summary>
        public CharacterRange (int first, int length)
        {
            First = first;
            Length = length;
        }
    }
}
