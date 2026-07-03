using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace Majorsilence.Forms.Drawing
{
    /// <summary>
    /// Provides CSS font-stack strings and font directory helpers.
    /// Cross-platform replacement for the Reporting project's FontResourceLoader.
    /// Font extraction (<see cref="ExtractAll"/>) is a no-op since this assembly embeds no font resources.
    /// </summary>
    public static class FontResourceLoader
    {
        private static readonly Dictionary<string, string[]> _cssStacks =
            new Dictionary<string, string[]> (StringComparer.OrdinalIgnoreCase)
            {
                ["Arial"]          = ["Arial", "Liberation Sans", "FreeSans", "Helvetica", "sans-serif"],
                ["Helvetica"]      = ["Helvetica", "Liberation Sans", "FreeSans", "sans-serif"],
                ["Times New Roman"]= ["Times New Roman", "Liberation Serif", "FreeSerif", "Times", "serif"],
                ["Courier New"]    = ["Courier New", "Liberation Mono", "FreeMono", "Courier", "monospace"],
                ["Verdana"]        = ["Verdana", "DejaVu Sans", "Liberation Sans", "sans-serif"],
                ["Georgia"]        = ["Georgia", "Liberation Serif", "FreeSerif", "serif"],
                ["Tahoma"]         = ["Tahoma", "DejaVu Sans", "Liberation Sans", "sans-serif"],
                ["Trebuchet MS"]   = ["Trebuchet MS", "Liberation Sans", "sans-serif"],
                ["Calibri"]        = ["Calibri", "Carlito", "Liberation Sans", "sans-serif"],
                ["Cambria"]        = ["Cambria", "Caladea", "Liberation Serif", "serif"],
                ["Impact"]         = ["Impact", "Liberation Sans", "sans-serif"],
                ["Comic Sans MS"]  = ["Comic Sans MS", "Comic Relief", "sans-serif"],
            };

        /// <summary>Returns a CSS font-family stack for the given family name, falling back through
        /// cross-platform alternatives (e.g., Liberation Sans for Arial).</summary>
        public static string GetCssFontStack (string familyName)
        {
            if (string.IsNullOrEmpty (familyName))
                return "sans-serif";

            if (_cssStacks.TryGetValue (familyName, out var stack))
            {
                return string.Join (", ", stack.Select (f => f.Contains (' ') ? $"\"{f}\"" : f));
            }

            return familyName.Contains (' ') ? $"\"{familyName}\", sans-serif" : $"{familyName}, sans-serif";
        }

        /// <summary>Returns the temporary directory used for font extraction (no-op in this assembly).</summary>
        public static string GetFontDirectory () => Path.GetTempPath ();

        /// <summary>Extracts embedded font resources to the specified directory. No-op in this assembly.</summary>
        public static void ExtractAll (string directory) { }
    }
}
