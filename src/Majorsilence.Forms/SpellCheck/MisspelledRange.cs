namespace Majorsilence.Forms.SpellCheck
{
    /// <summary>A misspelled word found in a body of text, identified by its character range.</summary>
    internal readonly struct MisspelledRange
    {
        /// <summary>Initializes a new instance of the <see cref="MisspelledRange"/> struct.</summary>
        internal MisspelledRange (string word, int start, int end)
        {
            Word = word;
            Start = start;
            End = end;
        }

        /// <summary>Gets the misspelled word's text.</summary>
        internal string Word { get; }

        /// <summary>Gets the index of the word's first character within the source text.</summary>
        internal int Start { get; }

        /// <summary>Gets the index one past the word's last character within the source text.</summary>
        internal int End { get; }
    }
}
