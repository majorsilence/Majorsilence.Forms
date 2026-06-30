using System;
using System.Collections.Generic;
using SkiaSharp;

namespace Majorsilence.Drawing
{
    internal static class FontSubstitution
    {
        private static readonly Dictionary<string, string[]> _table =
            new Dictionary<string, string[]> (StringComparer.OrdinalIgnoreCase)
            {
                ["Arial"]            = ["Liberation Sans", "FreeSans", "DejaVu Sans", "Nimbus Sans L", "Helvetica"],
                ["Arial Narrow"]     = ["Liberation Sans Narrow", "FreeSans"],
                ["Helvetica"]        = ["Liberation Sans", "FreeSans", "DejaVu Sans"],
                ["Times New Roman"]  = ["Liberation Serif", "FreeSerif", "DejaVu Serif", "Nimbus Roman No9 L"],
                ["Times"]            = ["Liberation Serif", "FreeSerif", "DejaVu Serif"],
                ["Georgia"]          = ["Liberation Serif", "FreeSerif", "DejaVu Serif"],
                ["Courier New"]      = ["Liberation Mono", "FreeMono", "DejaVu Sans Mono", "Courier"],
                ["Courier"]          = ["Liberation Mono", "FreeMono", "DejaVu Sans Mono"],
                ["Verdana"]          = ["DejaVu Sans", "Liberation Sans"],
                ["Trebuchet MS"]     = ["Liberation Sans", "DejaVu Sans"],
                ["Tahoma"]           = ["DejaVu Sans", "Liberation Sans"],
                ["Calibri"]          = ["Carlito", "Liberation Sans", "DejaVu Sans"],
                ["Cambria"]          = ["Caladea", "Liberation Serif", "FreeSerif"],
                ["Comic Sans MS"]    = ["Comic Relief", "DejaVu Sans"],
                ["Impact"]           = ["Liberation Sans", "DejaVu Sans"],
                ["MS Gothic"]        = ["Noto Sans CJK JP", "VL Gothic"],
                ["MS Mincho"]        = ["Noto Serif CJK JP", "IPAMincho"],
                ["SimSun"]           = ["Noto Serif CJK SC", "WenQuanYi Bitmap Song"],
                ["SimHei"]           = ["Noto Sans CJK SC", "WenQuanYi Zen Hei"],
                ["Microsoft YaHei"]  = ["Noto Sans CJK SC", "WenQuanYi Micro Hei"],
                ["Arial Unicode MS"] = ["Noto Sans", "DejaVu Sans"],
                ["Symbol"]           = ["DejaVu Sans"],
                ["Wingdings"]        = ["DejaVu Sans"],
                ["Marlett"]          = ["DejaVu Sans"],
            };

        internal static SKTypeface Resolve (string familyName, SKFontStyle style)
        {
            var tf = SKTypeface.FromFamilyName (familyName, style);
            if (tf != null && !string.Equals (tf.FamilyName, "Skia", StringComparison.OrdinalIgnoreCase))
                return tf;

            if (_table.TryGetValue (familyName, out var alternatives))
            {
                foreach (var alt in alternatives)
                {
                    tf = SKTypeface.FromFamilyName (alt, style);
                    if (tf != null && !string.Equals (tf.FamilyName, "Skia", StringComparison.OrdinalIgnoreCase))
                        return tf;
                }
            }

            return SKTypeface.FromFamilyName (null, style) ?? SKTypeface.Default;
        }
    }
}
