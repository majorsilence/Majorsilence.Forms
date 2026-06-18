using System.Text;

namespace Modern.Forms
{
    /// <summary>
    /// WinForms-compatible mnemonic (access key) parsing. An ampersand marks the following
    /// character as the mnemonic, which is drawn underlined; a doubled ampersand (<c>&amp;&amp;</c>)
    /// is a literal ampersand.
    /// </summary>
    internal static class Mnemonics
    {
        /// <summary>
        /// Parses mnemonic text, returning the display text (with prefixes removed) and the index
        /// of the mnemonic character within that display text (-1 if there is none).
        /// </summary>
        public static string Parse (string? text, out int mnemonicIndex)
        {
            mnemonicIndex = -1;

            if (string.IsNullOrEmpty (text) || text!.IndexOf ('&') < 0)
                return text ?? string.Empty;

            var sb = new StringBuilder (text.Length);

            for (var i = 0; i < text.Length; i++) {
                var c = text[i];

                if (c == '&') {
                    // Doubled ampersand → literal '&'.
                    if (i + 1 < text.Length && text[i + 1] == '&') {
                        sb.Append ('&');
                        i++;
                        continue;
                    }

                    // A trailing '&' is dropped; otherwise the next character is the mnemonic.
                    if (i + 1 < text.Length && mnemonicIndex < 0)
                        mnemonicIndex = sb.Length;

                    continue;
                }

                sb.Append (c);
            }

            return sb.ToString ();
        }

        /// <summary>Returns the display text with mnemonic prefixes removed.</summary>
        public static string Strip (string? text) => Parse (text, out _);
    }
}
