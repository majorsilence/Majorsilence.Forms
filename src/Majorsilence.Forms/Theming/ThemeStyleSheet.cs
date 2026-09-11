using System.Collections.Generic;
using System.Linq;
using SkiaSharp;

namespace Majorsilence.Forms
{
    /// <summary>
    /// A parsed CSS theme. The accepted language is a deliberately small, fully documented subset of CSS
    /// (see <c>docs/theming.md</c> and <see cref="ThemeCssReference"/>): an optional
    /// <c>@theme "Name" extends Base;</c> header, a <c>:root { --token: value; }</c> block that sets
    /// <see cref="Theme"/> properties, and <c>TypeName { property: value; }</c> rules that set the
    /// default <see cref="ControlStyle"/> of a control type. Anything outside the subset is reported
    /// in <see cref="Diagnostics"/> with a message that says what to write instead, and the rest of the
    /// sheet still parses -- so a live editor can apply what is valid while the author fixes the rest.
    /// </summary>
    public sealed class ThemeStyleSheet
    {
        // The compiled (fast-path) form of a token declaration, paired with its public model so the two
        // are built from the same parse and cannot disagree.
        internal sealed class TokenDeclaration
        {
            public TokenDeclaration (ThemeCssToken token, Func<object> resolve, ThemeCssTokenValue model)
            {
                Token = token;
                Resolve = resolve;
                Model = model;
            }

            public ThemeCssToken Token { get; }
            public Func<object> Resolve { get; }
            public ThemeCssTokenValue Model { get; }
        }

        internal sealed class ControlRule
        {
            public ControlRule (ThemeCssSelector selector, ThemeCssPart? part, bool hover, Action<ControlStyle> apply, ThemeCssRule model)
            {
                Selector = selector;
                Part = part;
                Hover = hover;
                Apply = apply;
                Model = model;
            }

            public ThemeCssSelector Selector { get; }
            public ThemeCssPart? Part { get; }
            public bool Hover { get; }
            public Action<ControlStyle> Apply { get; }
            public ThemeCssRule Model { get; }

            /// <summary>The type-level style this rule's declarations are installed on.</summary>
            public ControlStyle GetTargetStyle ()
                => Part is not null
                    ? (Hover ? Part.GetHoverStyle! () : Part.GetStyle ())
                    : (Hover ? Selector.GetHoverStyle! () : Selector.GetStyle ());
        }

        private ThemeStyleSheet (string? name, string? baseName, List<TokenDeclaration> tokens, List<ControlRule> rules, List<ThemeCssDiagnostic> diagnostics)
        {
            Name = name;
            BaseName = baseName;
            TokenDeclarations = tokens;
            ControlRules = rules;
            Diagnostics = diagnostics;
            Tokens = tokens.Select (t => t.Model).ToList ();
            Rules = rules.Select (r => r.Model).ToList ();
        }

        /// <summary>The name from the <c>@theme</c> header, or null when the sheet has none.</summary>
        public string? Name { get; }

        /// <summary>The base theme from <c>@theme ... extends Base;</c> -- a built-in theme name or a registered theme -- or null.</summary>
        public string? BaseName { get; }

        /// <summary>Every problem found, in source order. Errors mark declarations or rules that were dropped.</summary>
        public IReadOnlyList<ThemeCssDiagnostic> Diagnostics { get; }

        /// <summary>Whether any diagnostic is an error.</summary>
        public bool HasErrors => Diagnostics.Any (d => d.Severity == ThemeCssSeverity.Error);

        /// <summary>How many <c>:root</c> token declarations parsed successfully.</summary>
        public int TokenCount => TokenDeclarations.Count;

        /// <summary>How many control rules (one per selector in a comma list) parsed successfully.</summary>
        public int RuleCount => ControlRules.Count;

        /// <summary>
        /// The <c>:root</c> token declarations that parsed, in source order, with their resolved values.
        /// Host-neutral: see <see cref="ThemeCssValue"/>. A host other than Majorsilence.Forms' own
        /// renderers (System.Windows.Forms, Avalonia) reads the sheet through this and <see cref="Rules"/>
        /// instead of re-parsing the CSS, so there is one grammar and one set of diagnostics.
        /// </summary>
        public IReadOnlyList<ThemeCssTokenValue> Tokens { get; }

        /// <summary>
        /// The control rules that parsed -- one per selector in a comma list -- with shorthands expanded
        /// to longhands and <c>var()</c> references resolved (and kept live, see
        /// <see cref="ThemeCssDeclaration.TokenReference"/>).
        /// </summary>
        public IReadOnlyList<ThemeCssRule> Rules { get; }

        internal List<TokenDeclaration> TokenDeclarations { get; }
        internal List<ControlRule> ControlRules { get; }

        /// <summary>
        /// Parses a CSS theme. Never throws for bad input: problems are collected in
        /// <see cref="Diagnostics"/> and the valid remainder is kept.
        /// </summary>
        public static ThemeStyleSheet Parse (string css)
        {
            Guard.ThrowIfNull (css);

            var parser = new Parser (css);
            return parser.Parse ();
        }

        // ---- the parser --------------------------------------------------------------------------

        private sealed class RawDeclaration
        {
            public string Name = string.Empty;
            public int Line;
            public int Column;
            public List<CssComponent> Value = new ();
            public bool Important;
        }

        private sealed class RawSelector
        {
            public bool IsRoot;
            public string? TypeName;
            public string? Part;      // the name after '::', when present
            public string? Pseudo;
            public int Line;
            public int Column;
            public string Raw = string.Empty;
            public string? Error;
        }

        private sealed class RawRule
        {
            public List<RawSelector> Selectors = new ();
            public List<RawDeclaration> Declarations = new ();
        }

        private sealed class Parser
        {
            private readonly List<ThemeCssDiagnostic> _diagnostics = new ();
            private readonly List<CssToken> _tokens;
            private int _pos;

            private string? _name;
            private string? _baseName;
            private CssToken? _headerToken;

            private readonly List<RawRule> _rules = new ();

            // ':root' custom properties that are not theme tokens: the author's own variables.
            private readonly Dictionary<string, RawDeclaration> _variables = new (StringComparer.OrdinalIgnoreCase);
            private readonly HashSet<string> _usedVariables = new (StringComparer.OrdinalIgnoreCase);

            public Parser (string css)
            {
                _tokens = new ThemeCssTokenizer (css, _diagnostics).Tokenize ();
            }

            private CssToken Current => _tokens[_pos];
            private CssToken PeekToken (int offset = 1) => _tokens[Math.Min (_pos + offset, _tokens.Count - 1)];
            private void Advance () { if (_pos < _tokens.Count - 1) _pos++; }

            private void Error (int line, int column, string message) => _diagnostics.Add (new ThemeCssDiagnostic (ThemeCssSeverity.Error, line, column, message));
            private void Error (CssToken at, string message) => Error (at.Line, at.Column, message);
            private void Error (CssComponent at, string message) => Error (at.Line, at.Column, message);
            private void Warning (int line, int column, string message) => _diagnostics.Add (new ThemeCssDiagnostic (ThemeCssSeverity.Warning, line, column, message));

            public ThemeStyleSheet Parse ()
            {
                ParseRules ();

                var tokens = new List<TokenDeclaration> ();
                var rules = new List<ControlRule> ();

                CollectVariables ();
                CompileRoot (tokens);
                CompileControlRules (rules);
                WarnUnusedVariables ();

                var diagnostics = _diagnostics.OrderBy (d => d.Line).ThenBy (d => d.Column).ToList ();

                return new ThemeStyleSheet (_name, _baseName, tokens, rules, diagnostics);
            }

            // ---- syntax --------------------------------------------------------------------------

            private void ParseRules ()
            {
                while (!Current.Is (CssTokenKind.EndOfFile)) {
                    if (Current.Is (CssTokenKind.Semicolon)) {
                        Advance ();
                        continue;
                    }

                    if (Current.Is (CssTokenKind.CloseBrace)) {
                        Error (Current, "Unexpected '}' with no matching '{'.");
                        Advance ();
                        continue;
                    }

                    if (Current.Is (CssTokenKind.AtKeyword)) {
                        ParseAtRule ();
                        continue;
                    }

                    ParseQualifiedRule ();
                }
            }

            private void ParseAtRule ()
            {
                var at = Current;
                Advance ();

                if (!string.Equals (at.Value, "theme", StringComparison.OrdinalIgnoreCase)) {
                    Error (at, $"'@{at.Value}' is not supported. The only at-rule is '@theme \"Name\" extends Base;'. "
                        + "There is no @import (register base themes separately and use 'extends'), no @media, and no @font-face (use a font family installed on the machine).");
                    SkipAtRuleBody ();
                    return;
                }

                if (_headerToken is not null) {
                    Error (at, "Only one '@theme' declaration is allowed per stylesheet.");
                    SkipAtRuleBody ();
                    return;
                }

                _headerToken = at;

                if (!TryReadName (out var name)) {
                    Error (Current, "'@theme' must be followed by the theme's name, e.g. @theme \"Ocean\"; or @theme Ocean extends Dark;");
                    SkipAtRuleBody ();
                    return;
                }

                _name = name;

                if (Current.IsIdent ("extends")) {
                    Advance ();

                    if (!TryReadName (out var baseName)) {
                        Error (Current, "'extends' must be followed by the base theme's name: a built-in theme (Light, Dark, Classic, Aero, PointOfSale, HotDog) or a registered theme, e.g. @theme \"Ocean\" extends Dark;");
                        SkipAtRuleBody ();
                        return;
                    }

                    _baseName = baseName;
                }

                if (Current.Is (CssTokenKind.Semicolon)) {
                    Advance ();
                    return;
                }

                if (Current.Is (CssTokenKind.OpenBrace)) {
                    Error (Current, "'@theme' takes no block. Write it on one line ending in ';' -- @theme \"Ocean\" extends Dark; -- and put tokens in ':root { ... }'.");
                    SkipBlock ();
                    return;
                }

                Error (Current, $"Unexpected '{Current.Raw}' in the '@theme' declaration; expected ';'. The form is @theme \"Name\" [extends Base];");
                SkipAtRuleBody ();
            }

            private bool TryReadName (out string name)
            {
                name = string.Empty;

                if (Current.Is (CssTokenKind.String) || Current.Is (CssTokenKind.Ident)) {
                    name = Current.Value.Trim ();
                    Advance ();
                    return name.Length > 0;
                }

                return false;
            }

            private void SkipAtRuleBody ()
            {
                while (!Current.Is (CssTokenKind.EndOfFile)) {
                    if (Current.Is (CssTokenKind.Semicolon)) {
                        Advance ();
                        return;
                    }

                    if (Current.Is (CssTokenKind.OpenBrace)) {
                        SkipBlock ();
                        return;
                    }

                    Advance ();
                }
            }

            // Skips from the current '{' to its matching '}' inclusive.
            private void SkipBlock ()
            {
                var depth = 0;

                while (!Current.Is (CssTokenKind.EndOfFile)) {
                    if (Current.Is (CssTokenKind.OpenBrace))
                        depth++;
                    else if (Current.Is (CssTokenKind.CloseBrace)) {
                        depth--;
                        if (depth <= 0) {
                            Advance ();
                            return;
                        }
                    }

                    Advance ();
                }
            }

            private void ParseQualifiedRule ()
            {
                var start = Current;
                var selectorTokens = new List<CssToken> ();

                while (!Current.Is (CssTokenKind.OpenBrace)) {
                    if (Current.Is (CssTokenKind.EndOfFile) || Current.Is (CssTokenKind.Semicolon) || Current.Is (CssTokenKind.CloseBrace)) {
                        Error (start, $"Expected a rule like 'Button {{ ... }}' but found '{Describe (selectorTokens, start)}' with no '{{' block. "
                            + "Declarations must be inside a selector block; theme tokens go in ':root { --accent-color: ...; }'.");
                        if (!Current.Is (CssTokenKind.EndOfFile))
                            Advance ();
                        return;
                    }

                    selectorTokens.Add (Current);
                    Advance ();
                }

                var rule = new RawRule { Selectors = ParseSelectorList (selectorTokens, start) };

                Advance ();   // '{'
                ParseDeclarations (rule);
                _rules.Add (rule);
            }

            private static string Describe (List<CssToken> tokens, CssToken fallback)
                => tokens.Count == 0 ? fallback.Raw : string.Join (" ", tokens.Select (t => t.Raw));

            private List<RawSelector> ParseSelectorList (List<CssToken> tokens, CssToken start)
            {
                var result = new List<RawSelector> ();
                var group = new List<CssToken> ();

                void Flush ()
                {
                    if (group.Count > 0)
                        result.Add (ParseSelector (group));
                    else
                        Error (start, "Empty selector before ','.");
                    group = new List<CssToken> ();
                }

                foreach (var token in tokens) {
                    if (token.Is (CssTokenKind.Comma))
                        Flush ();
                    else
                        group.Add (token);
                }

                if (group.Count > 0 || result.Count == 0)
                    Flush ();

                return result;
            }

            private RawSelector ParseSelector (List<CssToken> tokens)
            {
                var first = tokens[0];
                var selector = new RawSelector {
                    Line = first.Line,
                    Column = first.Column,
                    Raw = string.Join ("", tokens.Select (t => t.Raw)),
                };

                const string Supported = "Supported selectors are ':root', a control type name such as 'Button', 'Button:hover', a part such as 'DataGridView::header' (optionally ':hover'), and comma-separated lists of those.";

                // :root
                if (first.Is (CssTokenKind.Colon) && tokens.Count == 2 && tokens[1].IsIdent ("root")) {
                    selector.IsRoot = true;
                    return selector;
                }

                if (first.IsDelim ('*')) {
                    selector.Error = $"The universal selector '*' is not supported: there is no single style every control inherits from. Set theme-wide colours with ':root' tokens (e.g. --background-color, --foreground-color) and the ambient font with 'Form {{ font-family: ...; }}'. {Supported}";
                    return selector;
                }

                if (first.IsDelim ('.') || first.Is (CssTokenKind.Hash)) {
                    selector.Error = $"'{selector.Raw}' looks like a class or id selector. Controls have no classes or ids; rules target control TYPES, e.g. 'Button {{ ... }}'. {Supported}";
                    return selector;
                }

                if (!first.Is (CssTokenKind.Ident)) {
                    selector.Error = $"'{selector.Raw}' is not a valid selector. {Supported}";
                    return selector;
                }

                selector.TypeName = first.Value;

                if (tokens.Count == 1)
                    return selector;

                if (tokens[1].Is (CssTokenKind.Colon)) {
                    // Type::part and Type::part:hover -- a styleable piece inside the control.
                    if (tokens.Count >= 3 && tokens[2].Is (CssTokenKind.Colon)) {
                        if (tokens.Count < 4 || !tokens[3].Is (CssTokenKind.Ident)) {
                            selector.Error = $"'{selector.Raw}': expected a part name after '::', e.g. 'DataGridView::header' or 'ScrollBar::thumb'.";
                            return selector;
                        }

                        selector.Part = tokens[3].Value;

                        if (tokens.Count == 4)
                            return selector;

                        if (tokens.Count == 6 && tokens[4].Is (CssTokenKind.Colon) && tokens[5].Is (CssTokenKind.Ident)) {
                            selector.Pseudo = tokens[5].Value;
                            return selector;
                        }

                        if (tokens.Count >= 6 && tokens[4].Is (CssTokenKind.Colon) && tokens[5].Is (CssTokenKind.Colon)) {
                            selector.Error = $"'{selector.Raw}': a selector names one part; parts do not nest.";
                            return selector;
                        }

                        selector.Error = $"'{selector.Raw}' is not a valid selector; after a part only ':hover' may follow, e.g. '{first.Value}::{selector.Part}:hover'.";
                        return selector;
                    }

                    if (tokens.Count == 3 && tokens[2].Is (CssTokenKind.Ident)) {
                        selector.Pseudo = tokens[2].Value;
                        return selector;
                    }

                    if (tokens.Count >= 5 && tokens[2].Is (CssTokenKind.Ident) && tokens[3].Is (CssTokenKind.Colon) && tokens[4].Is (CssTokenKind.Colon)) {
                        selector.Error = $"'{selector.Raw}': put the part before the pseudo-class -- '{first.Value}::{(tokens.Count > 5 ? tokens[5].Value : "part")}:{tokens[2].Value}'.";
                        return selector;
                    }

                    selector.Error = $"'{selector.Raw}' is not a valid selector; expected a pseudo-class name after ':' (only ':hover' is supported) or a part after '::'.";
                    return selector;
                }

                if (tokens[1].Is (CssTokenKind.Ident) || tokens[1].IsDelim ('>') || tokens[1].IsDelim ('+') || tokens[1].IsDelim ('~')) {
                    selector.Error = $"'{selector.Raw}': descendant and combinator selectors are not supported -- a rule applies to every control of the type, wherever it sits. Use one type name per selector, e.g. '{first.Value} {{ ... }}'.";
                    return selector;
                }

                if (tokens[1].IsDelim ('[')) {
                    selector.Error = $"'{selector.Raw}': attribute selectors are not supported. {Supported}";
                    return selector;
                }

                selector.Error = $"'{selector.Raw}' is not a valid selector. {Supported}";
                return selector;
            }

            private void ParseDeclarations (RawRule rule)
            {
                while (true) {
                    if (Current.Is (CssTokenKind.EndOfFile)) {
                        Error (Current, "Unexpected end of file: a '}' is missing.");
                        return;
                    }

                    if (Current.Is (CssTokenKind.CloseBrace)) {
                        Advance ();
                        return;
                    }

                    if (Current.Is (CssTokenKind.Semicolon)) {
                        Advance ();
                        continue;
                    }

                    if (Current.Is (CssTokenKind.OpenBrace)) {
                        Error (Current, "Nested blocks are not supported; close the current rule with '}' before starting another.");
                        SkipBlock ();
                        continue;
                    }

                    if (!Current.Is (CssTokenKind.Ident)) {
                        Error (Current, $"Expected a property name but found '{Current.Raw}'. A declaration is 'property: value;', e.g. 'background-color: #222;'.");
                        SkipDeclaration ();
                        continue;
                    }

                    var declaration = new RawDeclaration { Name = Current.Value, Line = Current.Line, Column = Current.Column };
                    Advance ();

                    // 'Button { Label { ... } }' -- an identifier followed by a block is an attempt at nesting.
                    if (Current.Is (CssTokenKind.OpenBrace) || (Current.Is (CssTokenKind.Colon) && PeekToken ().Is (CssTokenKind.Ident) && PeekToken (2).Is (CssTokenKind.OpenBrace))) {
                        Error (declaration.Line, declaration.Column, $"Nested blocks are not supported ('{declaration.Name}' starts a rule inside a rule); close the current rule with '}}' before starting another.");
                        while (!Current.Is (CssTokenKind.OpenBrace) && !Current.Is (CssTokenKind.EndOfFile))
                            Advance ();
                        SkipBlock ();
                        continue;
                    }

                    if (!Current.Is (CssTokenKind.Colon)) {
                        Error (Current, $"Expected ':' after property '{declaration.Name}'.");
                        SkipDeclaration ();
                        continue;
                    }

                    Advance ();
                    declaration.Value = ParseComponentValues (declaration);

                    if (declaration.Value.Count == 0 && !declaration.Important)
                        Error (declaration.Line, declaration.Column, $"Property '{declaration.Name}' has no value.");
                    else
                        rule.Declarations.Add (declaration);
                }
            }

            private void SkipDeclaration ()
            {
                while (!Current.Is (CssTokenKind.EndOfFile) && !Current.Is (CssTokenKind.Semicolon) && !Current.Is (CssTokenKind.CloseBrace)) {
                    if (Current.Is (CssTokenKind.OpenBrace)) {
                        SkipBlock ();
                        continue;
                    }
                    Advance ();
                }

                if (Current.Is (CssTokenKind.Semicolon))
                    Advance ();
            }

            // Reads a declaration's value up to ';' or '}' (neither consumed), turning tokens into
            // components and folding function calls into CssFunction nodes.
            private List<CssComponent> ParseComponentValues (RawDeclaration declaration)
            {
                var result = new List<CssComponent> ();

                while (!Current.Is (CssTokenKind.EndOfFile) && !Current.Is (CssTokenKind.Semicolon) && !Current.Is (CssTokenKind.CloseBrace)) {
                    if (Current.IsDelim ('!')) {
                        var bang = Current;
                        Advance ();

                        if (Current.IsIdent ("important")) {
                            Advance ();
                            declaration.Important = true;
                            Error (bang, "'!important' is not supported and was ignored. There is no cascade to override: later declarations simply replace earlier ones.");
                            continue;
                        }

                        Error (bang, "Unexpected '!'.");
                        continue;
                    }

                    if (Current.Is (CssTokenKind.OpenBrace)) {
                        Error (Current, "Unexpected '{' inside a declaration value.");
                        SkipBlock ();
                        continue;
                    }

                    var component = ReadComponent ();

                    if (component is not null)
                        result.Add (component);
                }

                return result;
            }

            private CssComponent? ReadComponent ()
            {
                var token = Current;

                switch (token.Kind) {
                    case CssTokenKind.Ident:
                        Advance ();
                        return new CssIdent (token.Value, token.Line, token.Column);
                    case CssTokenKind.Hash:
                        Advance ();
                        return new CssHash (token.Value, token.Line, token.Column);
                    case CssTokenKind.String:
                        Advance ();
                        return new CssString (token.Value, token.Line, token.Column, token.Raw);
                    case CssTokenKind.Number:
                        Advance ();
                        return new CssNumber (token.Number, token.Value, token.Line, token.Column, token.Raw);
                    case CssTokenKind.Comma:
                        Advance ();
                        return new CssDelim (',', token.Line, token.Column);
                    case CssTokenKind.Delim:
                        Advance ();
                        return new CssDelim (token.Value[0], token.Line, token.Column);
                    case CssTokenKind.Colon:
                        Advance ();
                        return new CssDelim (':', token.Line, token.Column);
                    case CssTokenKind.CloseParen:
                        Error (token, "Unexpected ')'.");
                        Advance ();
                        return null;
                    case CssTokenKind.OpenParen:
                        Error (token, "Unexpected '('.");
                        Advance ();
                        return null;
                    case CssTokenKind.AtKeyword:
                        Error (token, $"Unexpected '@{token.Value}' inside a declaration value.");
                        Advance ();
                        return null;
                    case CssTokenKind.Function:
                        return ReadFunction ();
                    default:
                        Advance ();
                        return null;
                }
            }

            private CssFunction? ReadFunction ()
            {
                var start = Current;
                Advance ();

                var groups = new List<List<CssComponent>> ();
                var group = new List<CssComponent> ();
                var raw = start.Raw;

                while (true) {
                    if (Current.Is (CssTokenKind.EndOfFile) || Current.Is (CssTokenKind.Semicolon) || Current.Is (CssTokenKind.CloseBrace)) {
                        Error (start, $"'{start.Value}(' is missing its closing ')'.");
                        return null;
                    }

                    if (Current.Is (CssTokenKind.CloseParen)) {
                        raw += ")";
                        Advance ();
                        break;
                    }

                    if (Current.Is (CssTokenKind.Comma)) {
                        raw += ", ";
                        Advance ();
                        groups.Add (group);
                        group = new List<CssComponent> ();
                        continue;
                    }

                    var component = ReadComponent ();

                    if (component is not null) {
                        raw += (group.Count > 0 ? " " : string.Empty) + component.Raw;
                        group.Add (component);
                    }
                }

                if (group.Count > 0 || groups.Count > 0)
                    groups.Add (group);

                return new CssFunction (start.Value, groups, start.Line, start.Column, raw);
            }

            // ---- semantics -----------------------------------------------------------------------

            private static bool IsCustomProperty (string name) => name.StartsWith ("--", StringComparison.Ordinal);

            private void CollectVariables ()
            {
                foreach (var rule in _rules)
                    if (rule.Selectors.Any (s => s.IsRoot))
                        foreach (var declaration in rule.Declarations)
                            if (IsCustomProperty (declaration.Name) && ThemeCssReference.FindToken (declaration.Name) is null)
                                _variables[declaration.Name] = declaration;
            }

            private void CompileRoot (List<TokenDeclaration> tokens)
            {
                foreach (var rule in _rules) {
                    var root = rule.Selectors.FirstOrDefault (s => s.IsRoot);

                    if (root is null)
                        continue;

                    if (rule.Selectors.Count > 1)
                        Error (root.Line, root.Column, "':root' cannot be combined with other selectors in one rule; give the tokens their own ':root { ... }' block.");

                    foreach (var declaration in rule.Declarations) {
                        if (!IsCustomProperty (declaration.Name)) {
                            Error (declaration.Line, declaration.Column,
                                $"':root' only accepts theme tokens (custom properties starting with '--', e.g. '--accent-color: #2a8ad0;'). "
                                + $"'{declaration.Name}' is a control property; put it in a control rule such as 'Button {{ {declaration.Name}: ...; }}'.");
                            continue;
                        }

                        var token = ThemeCssReference.FindToken (declaration.Name);

                        if (token is null)
                            continue;   // an author variable; validated when used, warned if never used

                        var value = Substitute (declaration.Value, new HashSet<string> (StringComparer.OrdinalIgnoreCase), declaration);

                        if (value is null)
                            continue;

                        var resolve = CompileTokenValue (token, value, declaration, out var model);

                        if (resolve is not null && model is not null)
                            tokens.Add (new TokenDeclaration (token, resolve, new ThemeCssTokenValue (token, model, TokenReferenceOf (value), declaration.Line, declaration.Column)));
                    }
                }
            }

            private Func<object>? CompileTokenValue (ThemeCssToken token, List<CssComponent> value, RawDeclaration declaration, out Func<ThemeCssValue>? model)
            {
                string? error;
                model = null;

                switch (token.Kind) {
                    case ThemeCssValueKind.Color:
                        if (ThemeCssValues.TryParseColor (value, out var color, out error)) {
                            model = () => ThemeCssValue.Color ((uint) color ());
                            return () => color ();
                        }
                        Error (declaration.Line, declaration.Column, $"'{token.Name}' expects a color. {error}");
                        return null;

                    case ThemeCssValueKind.Length:
                        if (ThemeCssValues.TryParseLength (value, out var length, out error)) {
                            model = () => ThemeCssValue.Length (length ());
                            return () => length ();
                        }
                        Error (declaration.Line, declaration.Column, $"'{token.Name}' expects a length in pixels, e.g. '14px'. {error}");
                        return null;

                    default:
                        if (ThemeCssValues.TryParseFontFamilies (value, out var families, out error)) {
                            var weight = token.PropertyName == nameof (Theme.UIFontBold) ? SKFontStyleWeight.Bold : SKFontStyleWeight.Normal;
                            model = () => ThemeCssValue.Families (families ());
                            return () => ThemeCssValues.GetTypeface (families (), weight, SKFontStyleSlant.Upright);
                        }
                        Error (declaration.Line, declaration.Column, $"'{token.Name}' expects a font family list, e.g. \"Segoe UI\", sans-serif. {error}");
                        return null;
                }
            }

            // The token a value was written as var(--token) of, when it is exactly that and nothing else.
            private static ThemeCssToken? TokenReferenceOf (List<CssComponent> value)
                => value.Count == 1 && value[0] is CssTokenRef reference ? reference.Token : null;

            private void CompileControlRules (List<ControlRule> rules)
            {
                foreach (var rule in _rules) {
                    if (rule.Selectors.Any (s => s.IsRoot))
                        continue;

                    // Compile the declarations once, then attach them to every selector in the list.
                    var compiled = CompileDeclarations (rule.Declarations);

                    foreach (var raw in rule.Selectors) {
                        if (raw.Error is not null) {
                            Error (raw.Line, raw.Column, raw.Error);
                            continue;
                        }

                        var selector = ThemeCssReference.FindSelector (raw.TypeName!);

                        if (selector is null) {
                            Error (raw.Line, raw.Column,
                                $"Unknown selector '{raw.TypeName}'.{ThemeCssValues.Suggest (raw.TypeName!, ThemeCssReference.Selectors.Select (s => s.Name))} "
                                + $"Selectors are Majorsilence.Forms control type names: {string.Join (", ", ThemeCssReference.Selectors.Select (s => s.Name))}.");
                            continue;
                        }

                        ThemeCssPart? part = null;

                        if (raw.Part is not null) {
                            if (selector.Parts.Count == 0) {
                                var withParts = string.Join (", ", ThemeCssReference.Selectors.Where (s => s.Parts.Count > 0).Select (s => s.Name));
                                Error (raw.Line, raw.Column, $"'{selector.Name}::{raw.Part}': {selector.Name} has no separately styleable parts; style the whole control with '{selector.Name} {{ ... }}'. Controls with parts: {withParts}.");
                                continue;
                            }

                            part = selector.FindPart (raw.Part);

                            if (part is null) {
                                var known = string.Join (", ", selector.Parts.Select (p => $"::{p.Name}"));
                                Error (raw.Line, raw.Column, $"'{selector.Name}::{raw.Part}' is not a part of {selector.Name}.{ThemeCssValues.Suggest (raw.Part, selector.Parts.Select (p => p.Name))} {selector.Name} parts: {known}.");
                                continue;
                            }
                        }

                        var hover = false;

                        if (raw.Pseudo is not null) {
                            if (!string.Equals (raw.Pseudo, "hover", StringComparison.OrdinalIgnoreCase)) {
                                var hint = raw.Pseudo.ToLowerInvariant () switch {
                                    "disabled" => " Disabled text is drawn with the '--foreground-disabled-color' token.",
                                    "focus" or "focus-visible" => " The focus indicator is not themable.",
                                    "active" or "pressed" => " Pressed state is not themable.",
                                    _ => string.Empty
                                };
                                Error (raw.Line, raw.Column, $"':{raw.Pseudo}' is not supported; the only pseudo-class is ':hover'.{hint}");
                                continue;
                            }

                            if (part is not null && !part.SupportsHover) {
                                var hoverableParts = string.Join (", ", ThemeCssReference.Parts.Where (p => p.Part.SupportsHover).Select (p => $"{p.Selector.Name}::{p.Part.Name}"));
                                Error (raw.Line, raw.Column, $"'{selector.Name}::{part.Name}:hover' is not supported: the {part.Name} does not change when hovered. Parts with ':hover': {hoverableParts}.");
                                continue;
                            }

                            if (part is null && !selector.SupportsHover) {
                                var hoverable = string.Join (", ", ThemeCssReference.Selectors.Where (s => s.SupportsHover).Select (s => s.Name));
                                Error (raw.Line, raw.Column, $"'{selector.Name}:hover' is not supported: {selector.Name} does not change appearance when hovered. ':hover' is available on: {hoverable}.");
                                continue;
                            }

                            hover = true;
                        }

                        // A part honours only the properties its renderer reads; anything else is dropped
                        // for that selector with an error that names the accepted list.
                        if (part is not null)
                            foreach (var item in compiled.Items)
                                if (!part.Accepts (item.Model.Property))
                                    Error (item.Model.Line, item.Model.Column, $"'{item.Model.Property}' does not apply to {selector.Name}::{part.Name}; it accepts: {string.Join (", ", part.Properties)}.");

                        var (actions, model) = compiled.Build (part is null ? null : part.Accepts);

                        if (actions.Count == 0)
                            continue;

                        var captured = actions;
                        rules.Add (new ControlRule (selector, part, hover, style => {
                            foreach (var action in captured)
                                action (style);
                        }, new ThemeCssRule (selector, part, hover, model, raw.Line, raw.Column)));
                    }
                }
            }

            // One compiled declaration: its public longhand model plus how to apply it. The three font
            // properties describe a single typeface, so they carry their parsed piece here and are merged
            // into one assignment when a rule is built.
            private sealed class CompiledDeclaration
            {
                public CompiledDeclaration (ThemeCssDeclaration model) => Model = model;

                public ThemeCssDeclaration Model { get; }
                public Action<ControlStyle>? Action { get; set; }
                public Func<IReadOnlyList<string>>? Families { get; set; }
                public SKFontStyleWeight? Weight { get; set; }
                public SKFontStyleSlant? Slant { get; set; }
            }

            // A rule's declarations, compiled once and then attached to every selector in its comma list.
            // Build () takes the subset a target accepts -- everything for a control, the part's list for
            // a part -- so `Menu::item, Button { color: red; font-size: 12px }` gives the part only the
            // colour while the button gets both.
            private sealed class CompiledDeclarations
            {
                public List<CompiledDeclaration> Items { get; } = new ();

                public (List<Action<ControlStyle>> Actions, List<ThemeCssDeclaration> Model) Build (Func<string, bool>? accepts)
                {
                    var actions = new List<Action<ControlStyle>> ();
                    var model = new List<ThemeCssDeclaration> ();

                    Func<IReadOnlyList<string>>? families = null;
                    SKFontStyleWeight? weight = null;
                    SKFontStyleSlant? slant = null;
                    var hasFont = false;

                    foreach (var item in Items) {
                        if (accepts is not null && !accepts (item.Model.Property))
                            continue;

                        model.Add (item.Model);

                        if (item.Action is not null) {
                            actions.Add (item.Action);
                            continue;
                        }

                        hasFont = true;
                        families ??= item.Families;
                        if (item.Families is not null)
                            families = item.Families;
                        if (item.Weight is not null)
                            weight = item.Weight;
                        if (item.Slant is not null)
                            slant = item.Slant;
                    }

                    if (hasFont) {
                        var w = weight ?? SKFontStyleWeight.Normal;
                        var sl = slant ?? SKFontStyleSlant.Upright;
                        var fam = families;
                        actions.Add (s => s.Font = ThemeCssValues.GetTypeface (
                            fam?.Invoke () ?? new[] { Majorsilence.Forms.SystemFonts.DefaultTypeface.FamilyName }, w, sl));
                    }

                    return (actions, model);
                }
            }

            private CompiledDeclarations CompileDeclarations (List<RawDeclaration> declarations)
            {
                var compiled = new CompiledDeclarations ();

                foreach (var declaration in declarations) {
                    var name = declaration.Name.ToLowerInvariant ();

                    if (IsCustomProperty (declaration.Name)) {
                        Error (declaration.Line, declaration.Column,
                            $"'{declaration.Name}': custom properties can only be declared in ':root'. To use one here, write var({declaration.Name}) as a value.");
                        continue;
                    }

                    if (!ThemeCssReference.PropertyNames.Contains (name)) {
                        Error (declaration.Line, declaration.Column,
                            $"Unknown property '{declaration.Name}'.{ThemeCssValues.Suggest (name, ThemeCssReference.PropertyNames)} "
                            + $"Supported properties: {string.Join (", ", ThemeCssReference.PropertyNames)}. Layout properties (margin, padding, width, height, display) have no meaning in a theme.");
                        continue;
                    }

                    var value = Substitute (declaration.Value, new HashSet<string> (StringComparer.OrdinalIgnoreCase), declaration);

                    if (value is null)
                        continue;

                    string? error;
                    var reference = TokenReferenceOf (value);
                    void Add (Func<ThemeCssValue> resolve, Action<ControlStyle> action)
                        => compiled.Items.Add (new CompiledDeclaration (new ThemeCssDeclaration (name, resolve, reference, declaration.Line, declaration.Column)) { Action = action });
                    CompiledDeclaration AddFont (Func<ThemeCssValue> resolve)
                    {
                        var item = new CompiledDeclaration (new ThemeCssDeclaration (name, resolve, reference, declaration.Line, declaration.Column));
                        compiled.Items.Add (item);
                        return item;
                    }

                    switch (name) {
                        case "background-color":
                            if (ThemeCssValues.TryParseColor (value, out var background, out error))
                                Add (() => ThemeCssValue.Color ((uint) background ()), s => s.BackgroundColor = background ());
                            else
                                Error (declaration.Line, declaration.Column, $"'{name}': {error}");
                            break;

                        case "color":
                            if (ThemeCssValues.TryParseColor (value, out var foreground, out error))
                                Add (() => ThemeCssValue.Color ((uint) foreground ()), s => s.ForegroundColor = foreground ());
                            else
                                Error (declaration.Line, declaration.Column, $"'{name}': {error}");
                            break;

                        case "border-color":
                        case "border-top-color":
                        case "border-right-color":
                        case "border-bottom-color":
                        case "border-left-color":
                            if (ThemeCssValues.TryParseColor (value, out var borderColor, out error))
                                Add (() => ThemeCssValue.Color ((uint) borderColor ()), BorderColorSetter (name, borderColor));
                            else
                                Error (declaration.Line, declaration.Column, $"'{name}': {error}");
                            break;

                        case "border-width":
                        case "border-top-width":
                        case "border-right-width":
                        case "border-bottom-width":
                        case "border-left-width":
                            if (ThemeCssValues.TryParseLength (value, out var borderWidth, out error))
                                Add (() => ThemeCssValue.Length (borderWidth ()), BorderWidthSetter (name, borderWidth));
                            else
                                Error (declaration.Line, declaration.Column, $"'{name}': {error}");
                            break;

                        case "border-radius":
                            if (ThemeCssValues.TryParseLength (value, out var radius, out error))
                                Add (() => ThemeCssValue.Length (radius ()), s => s.Border.Radius = radius ());
                            else
                                Error (declaration.Line, declaration.Column, $"'{name}': {error}");
                            break;

                        case "border":
                            CompileBorderShorthand (value, declaration, compiled);
                            break;

                        case "font-size":
                            if (ThemeCssValues.TryParseLength (value, out var fontSize, out error))
                                Add (() => ThemeCssValue.Length (fontSize ()), s => s.FontSize = fontSize ());
                            else
                                Error (declaration.Line, declaration.Column, $"'{name}': {error}");
                            break;

                        case "font-family":
                            if (ThemeCssValues.TryParseFontFamilies (value, out var parsedFamilies, out error))
                                AddFont (() => ThemeCssValue.Families (parsedFamilies ())).Families = parsedFamilies;
                            else
                                Error (declaration.Line, declaration.Column, $"'{name}': {error}");
                            break;

                        case "font-weight":
                            if (ThemeCssValues.TryParseFontWeight (value, out var parsedWeight, out error)) {
                                var weightValue = ThemeCssValue.Weight ((int) parsedWeight);
                                AddFont (() => weightValue).Weight = parsedWeight;
                            } else
                                Error (declaration.Line, declaration.Column, $"'{name}': {error}");
                            break;

                        case "font-style":
                            if (ThemeCssValues.TryParseFontStyle (value, out var parsedSlant, out error)) {
                                var styleValue = ThemeCssValue.Style (parsedSlant switch {
                                    SKFontStyleSlant.Italic => "italic",
                                    SKFontStyleSlant.Oblique => "oblique",
                                    _ => "normal"
                                });
                                AddFont (() => styleValue).Slant = parsedSlant;
                            } else
                                Error (declaration.Line, declaration.Column, $"'{name}': {error}");
                            break;
                    }
                }

                return compiled;
            }

            private static Action<ControlStyle> BorderColorSetter (string property, Func<SKColor> color) => property switch {
                "border-top-color" => s => s.Border.Top.Color = color (),
                "border-right-color" => s => s.Border.Right.Color = color (),
                "border-bottom-color" => s => s.Border.Bottom.Color = color (),
                "border-left-color" => s => s.Border.Left.Color = color (),
                _ => s => s.Border.Color = color (),
            };

            private static Action<ControlStyle> BorderWidthSetter (string property, Func<int> width) => property switch {
                "border-top-width" => s => s.Border.Top.Width = width (),
                "border-right-width" => s => s.Border.Right.Width = width (),
                "border-bottom-width" => s => s.Border.Bottom.Width = width (),
                "border-left-width" => s => s.Border.Left.Width = width (),
                _ => s => s.Border.Width = width (),
            };

            private static readonly HashSet<string> unsupported_border_styles = new (StringComparer.OrdinalIgnoreCase) {
                "dashed", "dotted", "double", "groove", "ridge", "inset", "outset"
            };

            private void CompileBorderShorthand (List<CssComponent> value, RawDeclaration declaration, CompiledDeclarations compiled)
            {
                Func<int>? width = null;
                Func<SKColor>? color = null;
                ThemeCssToken? widthReference = null;
                ThemeCssToken? colorReference = null;

                foreach (var component in value) {
                    if (component is CssNumber) {
                        if (width is not null) {
                            Error (component, "'border': more than one width was given.");
                            return;
                        }
                        if (!ThemeCssValues.TryParseLength (new List<CssComponent> { component }, out width, out var lengthError)) {
                            Error (component, $"'border': {lengthError}");
                            return;
                        }
                        continue;
                    }

                    if (component is CssIdent ident) {
                        if (ident.Name.Equals ("none", StringComparison.OrdinalIgnoreCase) || ident.Name.Equals ("hidden", StringComparison.OrdinalIgnoreCase)) {
                            width = () => 0;
                            continue;
                        }

                        if (ident.Name.Equals ("solid", StringComparison.OrdinalIgnoreCase))
                            continue;

                        if (unsupported_border_styles.Contains (ident.Name)) {
                            Error (component, $"'border': the '{ident.Name}' border style is not supported; borders are always solid. Write e.g. 'border: 1px solid #808080;'.");
                            return;
                        }
                    }

                    if (component is CssTokenRef { Token.Kind: ThemeCssValueKind.Length } tokenRef) {
                        if (width is not null) {
                            Error (component, "'border': more than one width was given.");
                            return;
                        }
                        if (!ThemeCssValues.TryParseLength (new List<CssComponent> { tokenRef }, out width, out var tokenLengthError)) {
                            Error (component, $"'border': {tokenLengthError}");
                            return;
                        }
                        widthReference = tokenRef.Token;
                        continue;
                    }

                    if (color is not null) {
                        Error (component, $"'border': unexpected '{component.Raw}' after the color. The form is 'border: [width] [solid|none] [color]'.");
                        return;
                    }

                    if (!ThemeCssValues.TryParseColor (new List<CssComponent> { component }, out color, out var colorError)) {
                        Error (component, $"'border': {colorError}");
                        return;
                    }

                    colorReference = (component as CssTokenRef)?.Token;
                }

                if (width is null && color is null) {
                    Error (declaration.Line, declaration.Column, "'border' needs a width and/or a color, e.g. 'border: 1px solid #808080;' or 'border: none;'.");
                    return;
                }

                // The model sees the longhands the shorthand stands for, in the order a host would apply them.
                if (width is not null) {
                    var w = width;
                    compiled.Items.Add (new CompiledDeclaration (new ThemeCssDeclaration ("border-width", () => ThemeCssValue.Length (w ()), widthReference, declaration.Line, declaration.Column)) {
                        Action = s => s.Border.Width = w ()
                    });
                }

                if (color is not null) {
                    var c = color;
                    compiled.Items.Add (new CompiledDeclaration (new ThemeCssDeclaration ("border-color", () => ThemeCssValue.Color ((uint) c ()), colorReference, declaration.Line, declaration.Column)) {
                        Action = s => s.Border.Color = c ()
                    });
                }
            }

            // Replaces every var() with the variable's components (author variables are spliced in;
            // theme tokens become CssTokenRef so they stay live). Returns null after reporting an error.
            private List<CssComponent>? Substitute (List<CssComponent> components, HashSet<string> visiting, RawDeclaration declaration)
            {
                var result = new List<CssComponent> ();

                foreach (var component in components) {
                    if (component is not CssFunction fn) {
                        result.Add (component);
                        continue;
                    }

                    if (!string.Equals (fn.Name, "var", StringComparison.OrdinalIgnoreCase)) {
                        var arguments = new List<List<CssComponent>> ();

                        foreach (var group in fn.Arguments) {
                            var substituted = Substitute (group, visiting, declaration);
                            if (substituted is null)
                                return null;
                            arguments.Add (substituted);
                        }

                        result.Add (new CssFunction (fn.Name, arguments, fn.Line, fn.Column, fn.Raw));
                        continue;
                    }

                    if (fn.Arguments.Count == 0 || fn.Arguments[0].Count != 1 || fn.Arguments[0][0] is not CssIdent { } nameIdent || !IsCustomProperty (nameIdent.Name)) {
                        Error (fn, $"'{fn.Raw}' is not a valid var() reference. Write var(--name), optionally with a fallback: var(--name, #333).");
                        return null;
                    }

                    var name = nameIdent.Name;

                    if (_variables.TryGetValue (name, out var variable)) {
                        if (!visiting.Add (name)) {
                            Error (fn, $"'{name}' refers to itself (directly or through other variables).");
                            return null;
                        }

                        _usedVariables.Add (name);
                        var substituted = Substitute (variable.Value, visiting, declaration);
                        visiting.Remove (name);

                        if (substituted is null)
                            return null;

                        result.AddRange (substituted);
                        continue;
                    }

                    var token = ThemeCssReference.FindToken (name);

                    if (token is not null) {
                        result.Add (new CssTokenRef (token, fn.Line, fn.Column));
                        continue;
                    }

                    if (fn.Arguments.Count >= 2) {
                        var fallback = Substitute (fn.Arguments[1], visiting, declaration);
                        if (fallback is null)
                            return null;
                        result.AddRange (fallback);
                        continue;
                    }

                    var candidates = _variables.Keys.Concat (ThemeCssReference.Tokens.Select (t => t.Name));
                    Error (fn, $"Unknown variable '{name}'.{ThemeCssValues.Suggest (name, candidates)} Declare it in ':root {{ {name}: ...; }}' or use a theme token such as var(--accent-color).");
                    return null;
                }

                return result;
            }

            private void WarnUnusedVariables ()
            {
                foreach (var variable in _variables.Values) {
                    if (_usedVariables.Contains (variable.Name))
                        continue;

                    var closest = ThemeCssValues.FindClosest (variable.Name, ThemeCssReference.Tokens.Select (t => t.Name));

                    Warning (variable.Line, variable.Column, closest is null
                        ? $"'{variable.Name}' is declared but never used, and it is not a theme token, so it has no effect."
                        : $"'{variable.Name}' is declared but never used, and it is not a theme token, so it has no effect. Did you mean '{closest}'?");
                }
            }
        }
    }
}
