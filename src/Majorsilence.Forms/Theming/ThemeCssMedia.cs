using System.Collections.Generic;
using System.Linq;

namespace Majorsilence.Forms
{
    // The two user preferences a media query can test. There are only four combinations, so a sheet that uses @media is compiled once
    // for each (ThemeStyleSheet.Parse) and applying it picks the one for the system's current settings; the theme applies its sheet
    // again when either preference changes (Theme.Css.cs).
    internal readonly struct MediaEnvironment
    {
        public MediaEnvironment (bool dark, bool reducedMotion)
        {
            Dark = dark;
            ReducedMotion = reducedMotion;
        }

        public bool Dark { get; }
        public bool ReducedMotion { get; }

        public int Index => (Dark ? 1 : 0) | (ReducedMotion ? 2 : 0);

        public static MediaEnvironment Current => new (SystemInformation.PrefersDarkColorScheme, SystemInformation.PrefersReducedMotion);

        public static readonly MediaEnvironment[] All = { new (false, false), new (true, false), new (false, true), new (true, true) };
    }

    // @media queries over what a desktop theme can know about its user: prefers-color-scheme and prefers-reduced-motion, combined with
    // and / or / not and comma lists, after an optional 'all', 'screen' or 'print' media type.
    internal static class ThemeCssMedia
    {
        public const string Supported = "Supported: (prefers-color-scheme: light | dark) and (prefers-reduced-motion: no-preference | reduce), combined with and, or, not and commas, after an optional media type (all, screen, print).";

        public static bool TryCompile (List<CssToken> tokens, out Func<MediaEnvironment, bool> condition, out string? error)
        {
            condition = _ => true;
            var position = 0;

            if (tokens.Count == 0) {
                error = $"'@media' needs a query before its '{{'. {Supported}";
                return false;
            }

            var queries = new List<Func<MediaEnvironment, bool>> ();

            while (true) {
                if (!TryParseQuery (tokens, ref position, out var query, out error))
                    return false;

                queries.Add (query);

                if (position >= tokens.Count)
                    break;

                if (!tokens[position].Is (CssTokenKind.Comma)) {
                    error = $"Unexpected '{tokens[position].Raw}' in the media query. {Supported}";
                    return false;
                }

                position++;
            }

            condition = queries.Count == 1 ? queries[0] : env => queries.Any (q => q (env));
            error = null;
            return true;
        }

        // [not | only]? <media-type> [and <condition>]?  |  <condition>
        private static bool TryParseQuery (List<CssToken> tokens, ref int position, out Func<MediaEnvironment, bool> query, out string? error)
        {
            query = _ => true;

            // A media type, possibly after 'only' or 'not' ('not screen and ...'); a bare 'not (...)' is a condition instead.
            if (position < tokens.Count && tokens[position].Is (CssTokenKind.Ident)
                && (!IsKeyword (tokens[position], "not") || (position + 1 < tokens.Count && tokens[position + 1].Is (CssTokenKind.Ident)))) {
                var negate = false;

                if (IsKeyword (tokens[position], "only")) {
                    position++;
                } else if (IsKeyword (tokens[position], "not") && position + 1 < tokens.Count && tokens[position + 1].Is (CssTokenKind.Ident)) {
                    negate = true;
                    position++;
                }

                if (position >= tokens.Count || !tokens[position].Is (CssTokenKind.Ident)) {
                    error = $"Expected a media type (all, screen or print) in the media query. {Supported}";
                    return false;
                }

                var type = tokens[position].Value.ToLowerInvariant ();
                position++;

                bool matches;
                switch (type) {
                    case "all":
                    case "screen":
                        matches = true;
                        break;
                    case "print":
                        // A theme styles the screen; printing goes through PrintDocument, which a theme does not reach.
                        matches = false;
                        break;
                    default:
                        error = $"'{type}' is not a media type a theme can match; use all, screen or print.";
                        return false;
                }

                Func<MediaEnvironment, bool> typeMatches = negate ? _ => !matches : _ => matches;

                if (position < tokens.Count && IsKeyword (tokens[position], "and")) {
                    position++;
                    if (!TryParseCondition (tokens, ref position, out var rest, out error))
                        return false;
                    query = negate ? env => !(matches && rest (env)) : env => matches && rest (env);
                } else {
                    query = typeMatches;
                }

                error = null;
                return true;
            }

            return TryParseCondition (tokens, ref position, out query, out error);
        }

        // <in-parens> [and <in-parens>]* | <in-parens> [or <in-parens>]* | not <in-parens>
        private static bool TryParseCondition (List<CssToken> tokens, ref int position, out Func<MediaEnvironment, bool> condition, out string? error)
        {
            condition = _ => true;

            if (position < tokens.Count && IsKeyword (tokens[position], "not")) {
                position++;
                if (!TryParseInParens (tokens, ref position, out var inner, out error))
                    return false;
                condition = env => !inner (env);
                return true;
            }

            if (!TryParseInParens (tokens, ref position, out var first, out error))
                return false;

            var terms = new List<Func<MediaEnvironment, bool>> { first };
            string? connective = null;

            while (position < tokens.Count && (IsKeyword (tokens[position], "and") || IsKeyword (tokens[position], "or"))) {
                var word = tokens[position].Value.ToLowerInvariant ();

                if (connective is not null && connective != word) {
                    error = "A media condition cannot mix 'and' and 'or' without parentheses: write '(a) and ((b) or (c))'.";
                    return false;
                }

                connective = word;
                position++;

                if (!TryParseInParens (tokens, ref position, out var term, out error))
                    return false;
                terms.Add (term);
            }

            condition = terms.Count == 1 ? first : connective == "or" ? env => terms.Any (t => t (env)) : env => terms.All (t => t (env));
            error = null;
            return true;
        }

        // ( <feature> ) | ( <condition> )
        private static bool TryParseInParens (List<CssToken> tokens, ref int position, out Func<MediaEnvironment, bool> condition, out string? error)
        {
            condition = _ => true;

            if (position >= tokens.Count || !tokens[position].Is (CssTokenKind.OpenParen)) {
                error = $"Expected '(' in the media query{(position < tokens.Count ? $", found '{tokens[position].Raw}'" : "")}. {Supported}";
                return false;
            }

            // Find the matching ')'.
            var depth = 0;
            var close = -1;
            for (var i = position; i < tokens.Count; i++) {
                if (tokens[i].Is (CssTokenKind.OpenParen))
                    depth++;
                else if (tokens[i].Is (CssTokenKind.CloseParen) && --depth == 0) {
                    close = i;
                    break;
                }
            }

            if (close < 0) {
                error = "A '(' in the media query is missing its ')'.";
                return false;
            }

            var inside = tokens.GetRange (position + 1, close - position - 1);
            position = close + 1;

            if (inside.Count > 0 && (inside[0].Is (CssTokenKind.OpenParen) || IsKeyword (inside[0], "not"))) {
                var inner = 0;
                if (!TryParseCondition (inside, ref inner, out condition, out error))
                    return false;
                if (inner != inside.Count) {
                    error = $"Unexpected '{inside[inner].Raw}' in the media query.";
                    return false;
                }
                return true;
            }

            return TryParseFeature (inside, out condition, out error);
        }

        private static bool TryParseFeature (List<CssToken> tokens, out Func<MediaEnvironment, bool> condition, out string? error)
        {
            condition = _ => true;

            if (tokens.Count == 0 || !tokens[0].Is (CssTokenKind.Ident)) {
                error = $"Expected a media feature such as (prefers-color-scheme: dark). {Supported}";
                return false;
            }

            var name = tokens[0].Value.ToLowerInvariant ();
            string? value = null;

            if (tokens.Count == 3 && tokens[1].Is (CssTokenKind.Colon) && tokens[2].Is (CssTokenKind.Ident))
                value = tokens[2].Value.ToLowerInvariant ();
            else if (tokens.Count != 1) {
                if (Unsupported (name) is { } why) {
                    error = why;
                    return false;
                }
                error = $"'({string.Join (" ", tokens.Select (t => t.Raw))})' is not a media feature a theme can test. {Supported}";
                return false;
            }

            switch (name) {
                case "prefers-color-scheme":
                    if (value is "dark") {
                        condition = env => env.Dark;
                        break;
                    }
                    if (value is "light") {
                        condition = env => !env.Dark;
                        break;
                    }
                    error = "prefers-color-scheme is 'light' or 'dark', e.g. @media (prefers-color-scheme: dark).";
                    return false;

                case "prefers-reduced-motion":
                    // The boolean form (prefers-reduced-motion) is true exactly when the value is not no-preference.
                    if (value is null or "reduce") {
                        condition = env => env.ReducedMotion;
                        break;
                    }
                    if (value is "no-preference") {
                        condition = env => !env.ReducedMotion;
                        break;
                    }
                    error = "prefers-reduced-motion is 'reduce' or 'no-preference'.";
                    return false;

                default:
                    error = Unsupported (name) ?? $"'{name}' is not a media feature.{ThemeCssValues.Suggest (name, feature_names)} {Supported}";
                    return false;
            }

            error = null;
            return true;
        }

        // The media features web stylesheets use that a theme cannot honour, each with the reason.
        private static string? Unsupported (string name)
        {
            switch (name) {
                case "width": case "min-width": case "max-width":
                case "height": case "min-height": case "max-height":
                case "aspect-ratio": case "min-aspect-ratio": case "max-aspect-ratio":
                case "orientation":
                    return $"'{name}' is not supported: a theme applies to every window at once, so there is no one viewport size to test. Size-dependent appearance belongs in layout code (Form.SizeClass, Resize). {Supported}";
                case "hover": case "any-hover": case "pointer": case "any-pointer":
                    return $"'{name}' is not supported: the input devices are not detected. {Supported}";
                case "prefers-contrast": case "forced-colors": case "inverted-colors": case "prefers-reduced-transparency":
                case "prefers-reduced-data": case "color-gamut": case "dynamic-range": case "resolution": case "min-resolution": case "max-resolution":
                case "color": case "monochrome": case "display-mode": case "scripting": case "update":
                    return $"'{name}' is not supported: no backend reports it yet. {Supported}";
                default:
                    return null;
            }
        }

        private static readonly string[] feature_names = { "prefers-color-scheme", "prefers-reduced-motion" };

        private static bool IsKeyword (CssToken token, string word) => token.Is (CssTokenKind.Ident) && token.Value.Equals (word, StringComparison.OrdinalIgnoreCase);
    }
}
