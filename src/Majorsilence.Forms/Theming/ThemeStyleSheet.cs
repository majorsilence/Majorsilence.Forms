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
            public ControlRule (ThemeCssSelector selector, ThemeCssPart? part, bool hover, bool active, bool disabled, bool focus, bool focusVisible, Action<ControlStyle> apply, ThemeCssRule model)
            {
                FocusVisible = focusVisible;
                Selector = selector;
                Part = part;
                Hover = hover;
                Active = active;
                Disabled = disabled;
                Focus = focus;
                Apply = apply;
                Model = model;
            }

            public ThemeCssSelector Selector { get; }
            public ThemeCssPart? Part { get; }
            public bool Hover { get; }
            public bool Active { get; }
            public bool Disabled { get; }
            public bool Focus { get; }
            public bool FocusVisible { get; }
            public Action<ControlStyle> Apply { get; }
            public ThemeCssRule Model { get; }

            /// <summary>Whether the rule names a class or an id: it styles individual controls (see Control.RebuildStyleCascade), not a type's defaults.</summary>
            public bool IsScoped => Model.IsScoped;

            public ThemeCssState State => Model.State;

            /// <summary>The type-level style this rule's declarations are installed on. Never called for a scoped rule.</summary>
            public ControlStyle GetTargetStyle ()
            {
                if (Part is not null)
                    return Hover ? Part.GetHoverStyle! () : Part.GetStyle ();

                if (Active)
                    return Selector.GetActiveStyle! ();
                if (Disabled)
                    return Selector.GetDisabledStyle! ();
                if (Focus)
                    return Selector.GetFocusStyle! ();
                if (FocusVisible)
                    return Selector.GetFocusVisibleStyle! ();
                if (Hover)
                    return Selector.GetHoverStyle! ();

                return Selector.GetStyle ();
            }
        }

        // One compilation of the sheet for one media environment. A sheet without @media has exactly one; a sheet with @media has one
        // for each combination of the preferences a query can test (MediaEnvironment.All), and reads the one for the current settings.
        internal sealed class Variant
        {
            public Variant (List<TokenDeclaration> tokens, List<ControlRule> rules, List<ThemeCssVariable> variables, List<ControlKeyframes> keyframes)
            {
                TokenDeclarations = tokens;
                ControlRules = rules;
                Variables = variables;
                Keyframes = keyframes;
                Tokens = tokens.Select (t => t.Model).ToList ();
                Rules = rules.Select (r => r.Model).ToList ();
                HasScopedRules = rules.Any (r => r.IsScoped);
            }

            public bool HasScopedRules { get; }

            public List<TokenDeclaration> TokenDeclarations { get; }
            public List<ControlRule> ControlRules { get; }
            public List<ThemeCssVariable> Variables { get; }
            public List<ControlKeyframes> Keyframes { get; }
            public List<ThemeCssTokenValue> Tokens { get; }
            public List<ThemeCssRule> Rules { get; }
        }

        private readonly Variant[] variants;

        private ThemeStyleSheet (string? name, string? baseName, Variant[] variants, List<ThemeCssDiagnostic> diagnostics)
        {
            Name = name;
            BaseName = baseName;
            this.variants = variants;
            Diagnostics = diagnostics;
        }

        private Variant CurrentVariant => variants.Length == 1 ? variants[0] : variants[MediaEnvironment.Current.Index];

        /// <summary>
        /// Whether the sheet has <c>@media</c> rules, so what it applies depends on the system's colour scheme and reduced-motion
        /// settings. <see cref="Tokens"/>, <see cref="Rules"/> and <see cref="Variables"/> then describe what applies under the
        /// current settings, and <see cref="Theme"/> applies the sheet again when they change.
        /// </summary>
        public bool UsesMediaQueries => variants.Length > 1;

        /// <summary>The name from the <c>@theme</c> header, or null when the sheet has none.</summary>
        public string? Name { get; }

        /// <summary>The base theme from <c>@theme ... extends Base;</c> -- a built-in theme name or a registered theme -- or null.</summary>
        public string? BaseName { get; }

        /// <summary>Every problem found, in source order. Errors mark declarations or rules that were dropped.</summary>
        public IReadOnlyList<ThemeCssDiagnostic> Diagnostics { get; }

        /// <summary>Whether any diagnostic is an error.</summary>
        public bool HasErrors => Diagnostics.Any (d => d.Severity == ThemeCssSeverity.Error);

        /// <summary>How many <c>:root</c> token declarations parsed successfully (under the current media settings).</summary>
        public int TokenCount => TokenDeclarations.Count;

        /// <summary>How many control rules (one per selector in a comma list) parsed successfully.</summary>
        public int RuleCount => ControlRules.Count;

        /// <summary>
        /// The <c>:root</c> token declarations that parsed, in source order, with their resolved values.
        /// Host-neutral: see <see cref="ThemeCssValue"/>. A host other than Majorsilence.Forms' own
        /// renderers (System.Windows.Forms, Avalonia) reads the sheet through this and <see cref="Rules"/>
        /// instead of re-parsing the CSS, so there is one grammar and one set of diagnostics.
        /// </summary>
        public IReadOnlyList<ThemeCssTokenValue> Tokens => CurrentVariant.Tokens;

        /// <summary>
        /// The control rules that parsed -- one per selector in a comma list -- with shorthands expanded
        /// to longhands and <c>var()</c> references resolved (and kept live, see
        /// <see cref="ThemeCssDeclaration.TokenReference"/>).
        /// </summary>
        public IReadOnlyList<ThemeCssRule> Rules => CurrentVariant.Rules;

        /// <summary>
        /// The author variables declared in <c>:root</c> (custom properties that are not theme tokens),
        /// in source order, with their resolved values. Majorsilence.Forms controls ignore them except
        /// through <c>var()</c>; host appliers may publish them (see <see cref="ThemeCssVariable"/>).
        /// </summary>
        public IReadOnlyList<ThemeCssVariable> Variables => CurrentVariant.Variables;

        /// <summary>
        /// The <c>@keyframes</c> blocks that parsed, in source order. A rule names them with <c>animation-name</c> (see
        /// <see cref="ThemeCssRule.Animations"/>); each keyframe's <see cref="ControlKeyframe.Declarations"/> are host-neutral,
        /// so an applier can play the animation with its own toolkit instead of re-parsing the CSS.
        /// </summary>
        public IReadOnlyList<ControlKeyframes> Keyframes => CurrentVariant.Keyframes;

        internal List<TokenDeclaration> TokenDeclarations => CurrentVariant.TokenDeclarations;
        internal List<ControlRule> ControlRules => CurrentVariant.ControlRules;

        /// <summary>
        /// Whether the sheet has a rule that names a class or an id (<c>.primary</c>, <c>Button.primary:hover</c>, <c>#save</c>), in any
        /// <c>@media</c> environment. Such rules style individual controls, so a control reads them through its own cascade
        /// (<see cref="Control.StyleClasses"/>) instead of through its type's default style.
        /// </summary>
        public bool UsesClassesOrIds => variants.Any (v => v.HasScopedRules);

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
            public List<string> Classes = new ();   // the names after '.', without duplicates
            public string? Id;                       // the name after '#'
            public int Line;
            public int Column;
            public string Raw = string.Empty;
            public string? Error;
        }

        private sealed class RawRule
        {
            public List<RawSelector> Selectors = new ();
            public List<RawDeclaration> Declarations = new ();
            public Func<MediaEnvironment, bool>? Media;   // the enclosing @media condition(s), or null
            public string? MediaText;
        }

        private sealed class Parser
        {
            private readonly List<ThemeCssDiagnostic> _diagnostics = new ();
            private readonly List<CssToken> _tokens;
            private int _pos;

            private string? _name;
            private string? _baseName;
            private CssToken? _headerToken;

            // Every rule parsed, and the ones compiled for the media environment at hand (see Parse).
            private readonly List<RawRule> _allRules = new ();
            private List<RawRule> _rules = new ();

            // @keyframes blocks as parsed, and as compiled for the media environment at hand (animation-name looks them up).
            private sealed class RawKeyframes
            {
                public string Name = string.Empty;
                public CssToken At;
                public List<(List<double> Offsets, RawRule Block)> Frames = new ();
            }

            private readonly List<RawKeyframes> _allKeyframes = new ();
            private readonly Dictionary<string, ControlKeyframes> _keyframes = new (StringComparer.Ordinal);

            // The @media condition(s) the rules being parsed sit inside.
            private Func<MediaEnvironment, bool>? _media;
            private string? _mediaText;

            // ':root' custom properties that are not theme tokens: the author's own variables.
            private readonly Dictionary<string, RawDeclaration> _variables = new (StringComparer.OrdinalIgnoreCase);
            private readonly HashSet<string> _usedVariables = new (StringComparer.OrdinalIgnoreCase);

            // Custom properties declared inside control rules, per (type, part, pseudo-class) -- see CompileControlRules -- the scope
            // the declaration being compiled resolves var() in (null: :root only), and which of them anything read.
            private readonly Dictionary<string, Dictionary<string, RawDeclaration>> _ruleVariables = new (StringComparer.Ordinal);
            private Dictionary<string, RawDeclaration>? _scope;
            private readonly HashSet<RawDeclaration> _usedRuleVariables = new ();

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

                // The syntax is read once; what the rules mean is compiled once per media environment, since an @media block can
                // redefine variables and tokens that everything else reads.
                var diagnostics = new List<ThemeCssDiagnostic> (_diagnostics);
                _diagnostics.Clear ();

                var environments = _allRules.Any (r => r.Media is not null) ? MediaEnvironment.All : new[] { default (MediaEnvironment) };
                var variants = new Variant[environments.Length];
                var perVariant = new List<List<ThemeCssDiagnostic>> ();

                for (var i = 0; i < environments.Length; i++) {
                    var environment = environments[i];
                    _rules = _allRules.Where (r => r.Media is null || r.Media (environment)).ToList ();
                    _variables.Clear ();
                    _usedVariables.Clear ();
                    _ruleVariables.Clear ();
                    _scopes.Clear ();
                    _usedRuleVariables.Clear ();
                    _keyframes.Clear ();

                    var tokens = new List<TokenDeclaration> ();
                    var rules = new List<ControlRule> ();

                    CollectVariables ();
                    CompileRoot (tokens);
                    CompileKeyframes ();
                    CompileControlRules (rules);
                    WarnUnusedVariables ();
                    var variables = CompileVariables ();

                    variants[i] = new Variant (tokens, rules, variables, _allKeyframes.Where (k => _keyframes.ContainsKey (k.Name)).Select (k => _keyframes[k.Name]).Distinct ().ToList ());
                    perVariant.Add (new List<ThemeCssDiagnostic> (_diagnostics));
                    _diagnostics.Clear ();
                }

                // A mistake in any environment is reported; a warning only when it holds in every one (a variable read only under
                // dark mode is not "never used").
                static string Key (ThemeCssDiagnostic d) => $"{d.Line}:{d.Column}:{d.Severity}:{d.Message}";
                var seen = new HashSet<string> ();

                foreach (var diagnostic in perVariant.SelectMany (v => v))
                    if ((diagnostic.Severity != ThemeCssSeverity.Warning || perVariant.All (v => v.Any (d => Key (d) == Key (diagnostic)))) && seen.Add (Key (diagnostic)))
                        diagnostics.Add (diagnostic);

                return new ThemeStyleSheet (_name, _baseName, variants, diagnostics.OrderBy (d => d.Line).ThenBy (d => d.Column).ToList ());
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

                if (string.Equals (at.Value, "media", StringComparison.OrdinalIgnoreCase)) {
                    ParseMediaRule (at);
                    return;
                }

                if (string.Equals (at.Value, "keyframes", StringComparison.OrdinalIgnoreCase)) {
                    ParseKeyframesRule (at);
                    return;
                }

                if (at.Value.EndsWith ("-keyframes", StringComparison.OrdinalIgnoreCase)) {
                    Error (at, $"'@{at.Value}' is a vendor-prefixed form; write '@keyframes'.");
                    SkipAtRuleBody ();
                    return;
                }

                if (!string.Equals (at.Value, "theme", StringComparison.OrdinalIgnoreCase)) {
                    Error (at, $"'@{at.Value}' is not supported. The at-rules are '@theme \"Name\" extends Base;', '@media (prefers-color-scheme: dark) {{ ... }}' and '@keyframes name {{ ... }}'. "
                        + "There is no @import (register base themes separately and use 'extends') and no @font-face (use a font family installed on the machine, or PrivateFontCollection).");
                    SkipAtRuleBody ();
                    return;
                }

                if (_media is not null) {
                    Error (at, "'@theme' cannot be inside '@media'; it names the whole sheet. Put it at the top.");
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

            // @keyframes <name> { from { ... } 50% { ... } to { ... } }
            private void ParseKeyframesRule (CssToken at)
            {
                if (_media is not null) {
                    Error (at, "'@keyframes' cannot be inside '@media'; declare it at the top level and choose it with animation-name inside the @media block.");
                    SkipAtRuleBody ();
                    return;
                }

                string name;

                if (Current.Is (CssTokenKind.Ident) || Current.Is (CssTokenKind.String)) {
                    name = Current.Value;
                    Advance ();
                } else {
                    Error (at, "'@keyframes' must be followed by a name: @keyframes pulse { from { ... } to { ... } }.");
                    SkipAtRuleBody ();
                    return;
                }

                if (name.Equals ("none", StringComparison.OrdinalIgnoreCase) || name.Equals ("initial", StringComparison.OrdinalIgnoreCase)) {
                    Error (at, $"'{name}' cannot name keyframes: animation-name: {name} means something else.");
                    SkipAtRuleBody ();
                    return;
                }

                if (!Current.Is (CssTokenKind.OpenBrace)) {
                    Error (Current, $"Expected '{{' after '@keyframes {name}'.");
                    SkipAtRuleBody ();
                    return;
                }

                Advance ();
                var keyframes = new RawKeyframes { Name = name, At = at };

                while (true) {
                    if (Current.Is (CssTokenKind.EndOfFile)) {
                        Error (Current, $"Unexpected end of file: '@keyframes {name}' is missing its '}}'.");
                        break;
                    }

                    if (Current.Is (CssTokenKind.CloseBrace)) {
                        Advance ();
                        break;
                    }

                    var selectorStart = Current;
                    var offsets = new List<double> ();
                    var valid = true;

                    while (!Current.Is (CssTokenKind.OpenBrace) && !Current.Is (CssTokenKind.EndOfFile) && !Current.Is (CssTokenKind.CloseBrace)) {
                        var token = Current;
                        Advance ();

                        if (token.Is (CssTokenKind.Comma))
                            continue;
                        if (token.IsIdent ("from"))
                            offsets.Add (0);
                        else if (token.IsIdent ("to"))
                            offsets.Add (1);
                        else if (token.Is (CssTokenKind.Number) && token.Value == "%" && token.Number is >= 0 and <= 100)
                            offsets.Add (token.Number / 100);
                        else
                            valid = false;
                    }

                    if (!Current.Is (CssTokenKind.OpenBrace)) {
                        Error (selectorStart, $"Expected a keyframe block like '50% {{ opacity: .5; }}' in '@keyframes {name}'.");
                        continue;
                    }

                    Advance ();
                    var block = new RawRule ();
                    ParseDeclarations (block);

                    if (!valid || offsets.Count == 0) {
                        Error (selectorStart, $"A keyframe is selected by 'from', 'to' or a percentage from 0% to 100%, e.g. '0%, 50% {{ ... }}'.");
                        continue;
                    }

                    keyframes.Frames.Add ((offsets, block));
                }

                _allKeyframes.Add (keyframes);
            }

            // Compiles every @keyframes for the media environment at hand: each keyframe's declarations become a style-setting action,
            // and a keyframe that lists several offsets is one keyframe at each.
            private void CompileKeyframes ()
            {
                foreach (var raw in _allKeyframes) {
                    var frames = new List<ControlKeyframe> ();

                    foreach (var (offsets, block) in raw.Frames) {
                        ControlTimingFunction? timing = null;
                        var declarations = new List<RawDeclaration> ();

                        foreach (var declaration in block.Declarations) {
                            if (declaration.Name.Equals ("animation-timing-function", StringComparison.OrdinalIgnoreCase)) {
                                string? timingError = null;
                                if (declaration.Value.Count == 1 && ThemeCssValues.TryParseTimingFunction (declaration.Value[0], out var parsed, out timingError))
                                    timing = parsed;
                                else
                                    Error (declaration.Line, declaration.Column, $"'animation-timing-function': {timingError ?? "one timing function, e.g. ease-out."}");
                                continue;
                            }

                            if (IsCustomProperty (declaration.Name)) {
                                Error (declaration.Line, declaration.Column, $"'{declaration.Name}': a keyframe cannot declare variables; declare them in ':root' or the control's rule.");
                                continue;
                            }

                            declarations.Add (declaration);
                        }

                        var compiled = CompileInScope (declarations, no_rule_variables, report: true);

                        foreach (var item in compiled.Items)
                            if (!ControlTransition.IsAnimatable (item.Model.Property) || item.Model.Property == "all")
                                Error (item.Model.Line, item.Model.Column,
                                    $"'{item.Model.Property}' cannot be animated: a keyframe takes the properties a transition can move -- {string.Join (", ", ControlTransition.AnimatableProperties)}.");

                        var (actions, model) = compiled.Build (property => ControlTransition.IsAnimatable (property) && property != "all");
                        var properties = model.Select (d => d.Property).Distinct ().ToArray ();
                        var captured = actions;
                        Action<ControlStyle> apply = style => {
                            foreach (var action in captured)
                                action (style);
                        };

                        foreach (var offset in offsets)
                            frames.Add (new ControlKeyframe (offset, apply, properties, timing, model));
                    }

                    // Stable: keyframes at the same offset keep their source order, so the later one wins, as in CSS.
                    _keyframes[raw.Name] = new ControlKeyframes (raw.Name, frames.Select ((f, i) => (f, i)).OrderBy (p => p.f.Offset).ThenBy (p => p.i).Select (p => p.f).ToArray ());
                }
            }

            // @media <query> { <rules> } -- the rules inside apply only while the query matches; @media blocks may nest.
            private void ParseMediaRule (CssToken at)
            {
                var query = new List<CssToken> ();

                while (!Current.Is (CssTokenKind.OpenBrace)) {
                    if (Current.Is (CssTokenKind.EndOfFile) || Current.Is (CssTokenKind.Semicolon) || Current.Is (CssTokenKind.CloseBrace)) {
                        Error (at, "'@media' needs a block: @media (prefers-color-scheme: dark) { Button { ... } }.");
                        if (Current.Is (CssTokenKind.Semicolon))
                            Advance ();
                        return;
                    }

                    query.Add (Current);
                    Advance ();
                }

                if (!ThemeCssMedia.TryCompile (query, out var condition, out var error)) {
                    Error (at, error!);
                    SkipBlock ();
                    return;
                }

                var text = string.Join (" ", query.Select (t => t.Raw)).Replace ("( ", "(").Replace (" )", ")").Replace (" :", ":");

                if (!MediaEnvironment.All.Any (condition))
                    Warning (at.Line, at.Column, $"'@media {text}' never matches (a theme is never used for print), so the rules inside never apply.");

                var outerMedia = _media;
                var outerText = _mediaText;
                _media = outerMedia is null ? condition : env => outerMedia (env) && condition (env);
                _mediaText = outerText is null ? text : $"{outerText} and {text}";

                Advance ();   // '{'

                while (true) {
                    if (Current.Is (CssTokenKind.EndOfFile)) {
                        Error (Current, "Unexpected end of file: the '@media' block is missing its '}'.");
                        break;
                    }

                    if (Current.Is (CssTokenKind.CloseBrace)) {
                        Advance ();
                        break;
                    }

                    if (Current.Is (CssTokenKind.Semicolon)) {
                        Advance ();
                        continue;
                    }

                    if (Current.Is (CssTokenKind.AtKeyword)) {
                        ParseAtRule ();
                        continue;
                    }

                    ParseQualifiedRule ();
                }

                _media = outerMedia;
                _mediaText = outerText;
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

                var rule = new RawRule { Selectors = ParseSelectorList (selectorTokens, start), Media = _media, MediaText = _mediaText };

                Advance ();   // '{'
                ParseDeclarations (rule);
                _allRules.Add (rule);
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

                const string Supported = "Supported selectors are ':root', a control type name such as 'Button', 'Button:hover', a class or id such as '.primary', 'Button.primary:hover' or '#saveButton', a part such as 'DataGridView::header' (optionally ':hover'), and comma-separated lists of those.";

                // :root
                if (first.Is (CssTokenKind.Colon) && tokens.Count == 2 && tokens[1].IsIdent ("root")) {
                    selector.IsRoot = true;
                    return selector;
                }

                if (first.IsDelim ('*')) {
                    selector.Error = $"The universal selector '*' is not supported: there is no single style every control inherits from. Set theme-wide colours with ':root' tokens (e.g. --background-color, --foreground-color) and the ambient font with 'Form {{ font-family: ...; }}'. {Supported}";
                    return selector;
                }

                var start = 0;

                if (first.Is (CssTokenKind.Ident)) {
                    selector.TypeName = first.Value;
                    start = 1;
                } else if (!first.IsDelim ('.') && !first.Is (CssTokenKind.Hash)) {
                    selector.Error = $"'{selector.Raw}' is not a valid selector. {Supported}";
                    return selector;
                }

                // Whether tokens[i] follows tokens[i - 1] with nothing between them: whitespace after a compound is a descendant
                // combinator, which is a different selector from the same text without it ('Panel .x' and 'Panel.x').
                bool Adjacent (int i)
                    => i == 0 || (tokens[i - 1].Line == tokens[i].Line && tokens[i - 1].Column + tokens[i - 1].Raw.Length == tokens[i].Column);

                // The compound written so far, for suggesting the corrected spelling.
                string Written ()
                    => (selector.TypeName ?? string.Empty) + string.Concat (selector.Classes.Select (c => "." + c)) + (selector.Id is null ? string.Empty : "#" + selector.Id);

                for (var i = start; i < tokens.Count;) {
                    var token = tokens[i];

                    if (!Adjacent (i) || token.IsDelim ('>') || token.IsDelim ('+') || token.IsDelim ('~')) {
                        selector.Error = $"'{selector.Raw}': descendant and combinator selectors are not supported yet ('Panel Button', 'Panel > Button', 'Button + Button', 'Button ~ Button'): combinators come in a later phase. "
                            + "A rule applies to a control by its own type, classes and id, wherever it sits. Name the controls you mean with a class ('Button.toolbar { ... }') and add it in code with StyleClasses.Add (\"toolbar\").";
                        return selector;
                    }

                    if (token.IsDelim ('[')) {
                        selector.Error = $"'{selector.Raw}': attribute selectors are not supported yet; they come in a later phase together with combinators. Use a class ('Button.primary') to pick controls out. {Supported}";
                        return selector;
                    }

                    if (selector.Pseudo is not null && selector.Part is null && (token.IsDelim ('.') || token.Is (CssTokenKind.Hash))) {
                        selector.Error = $"'{selector.Raw}': put classes and ids before the pseudo-class, e.g. '{Written ()}{(token.IsDelim ('.') && i + 1 < tokens.Count ? "." + tokens[i + 1].Value : "#" + token.Value)}:{selector.Pseudo}'.";
                        return selector;
                    }

                    if (token.IsDelim ('.')) {
                        if (i + 1 >= tokens.Count || !tokens[i + 1].Is (CssTokenKind.Ident) || !Adjacent (i + 1)) {
                            selector.Error = $"'{selector.Raw}': expected a class name after '.', e.g. '.primary' or 'Button.primary'.";
                            return selector;
                        }

                        if (!selector.Classes.Contains (tokens[i + 1].Value))
                            selector.Classes.Add (tokens[i + 1].Value);
                        i += 2;
                        continue;
                    }

                    if (token.Is (CssTokenKind.Hash)) {
                        if (token.Value.Length == 0) {
                            selector.Error = $"'{selector.Raw}': expected an id after '#', e.g. '#saveButton' (a control's Name).";
                            return selector;
                        }

                        if (selector.Id is not null && selector.Id != token.Value) {
                            selector.Error = $"'{selector.Raw}': a selector names one id; no control has two names, so '#{selector.Id}#{token.Value}' could never match.";
                            return selector;
                        }

                        selector.Id = token.Value;
                        i++;
                        continue;
                    }

                    if (!token.Is (CssTokenKind.Colon)) {
                        selector.Error = $"'{selector.Raw}' is not a valid selector. {Supported}";
                        return selector;
                    }

                    // '::part'
                    if (i + 1 < tokens.Count && tokens[i + 1].Is (CssTokenKind.Colon) && Adjacent (i + 1)) {
                        if (selector.Part is not null) {
                            selector.Error = $"'{selector.Raw}': a selector names one part; parts do not nest.";
                            return selector;
                        }

                        if (i + 2 >= tokens.Count || !tokens[i + 2].Is (CssTokenKind.Ident) || !Adjacent (i + 2)) {
                            selector.Error = $"'{selector.Raw}': expected a part name after '::', e.g. 'DataGridView::header' or 'ScrollBar::thumb'.";
                            return selector;
                        }

                        if (selector.Pseudo is not null) {
                            selector.Error = $"'{selector.Raw}': put the part before the pseudo-class -- '{selector.TypeName}::{tokens[i + 2].Value}:{selector.Pseudo}'.";
                            return selector;
                        }

                        selector.Part = tokens[i + 2].Value;
                        i += 3;
                        continue;
                    }

                    // ':hover' and friends
                    if (i + 1 >= tokens.Count || !Adjacent (i + 1)) {
                        selector.Error = $"'{selector.Raw}' is not a valid selector; expected a pseudo-class name after ':' (':hover', ':active', ':disabled', ':focus', ':focus-visible') or a part after '::'.";
                        return selector;
                    }

                    var name = tokens[i + 1];

                    if (name.Is (CssTokenKind.Function)) {
                        selector.Error = name.Value.ToLowerInvariant () is "not" or "is" or "where" or "has"
                            ? $"'{selector.Raw}': ':{name.Value} ()' is not supported yet; selector functions (':not ()', ':is ()', ':where ()', ':has ()') come in a later phase together with combinators. Use a class to pick controls out, e.g. 'Button.primary'."
                            : $"'{selector.Raw}': ':{name.Value} ()' is not supported; the pseudo-classes are ':hover', ':active', ':disabled', ':focus' and ':focus-visible'.";
                        return selector;
                    }

                    if (!name.Is (CssTokenKind.Ident)) {
                        selector.Error = $"'{selector.Raw}' is not a valid selector; expected a pseudo-class name after ':' (only ':hover' is supported) or a part after '::'.";
                        return selector;
                    }

                    if (selector.Pseudo is not null) {
                        selector.Error = selector.Part is not null
                            ? $"'{selector.Raw}' is not a valid selector; after a part only ':hover' may follow, e.g. '{selector.TypeName}::{selector.Part}:hover'."
                            : $"'{selector.Raw}': a selector takes one pseudo-class at a time ('{Written ()}:{selector.Pseudo}' and ':{name.Value}' together are not supported).";
                        return selector;
                    }

                    selector.Pseudo = name.Value;
                    i += 2;
                }

                if (selector.Part is not null && selector.TypeName is null) {
                    selector.Error = $"'{selector.Raw}': a part belongs to a control type -- write the type before it, e.g. 'DataGridView::header'.";
                    return selector;
                }

                if (selector.Part is not null && (selector.Classes.Count > 0 || selector.Id is not null)) {
                    selector.Error = $"'{selector.Raw}': a class or id cannot be combined with a ::part yet -- a part is styled for every control of its type. Style the whole control with '{Written ()}'.";
                    return selector;
                }

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
                        // A parenthesised group, which only means something inside calc(): an anonymous function.
                        return ReadFunction ();
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
                        Error (start, start.Is (CssTokenKind.OpenParen) ? "'(' is missing its closing ')'." : $"'{start.Value}(' is missing its closing ')'.");
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

                return new CssFunction (start.Is (CssTokenKind.OpenParen) ? string.Empty : start.Value, groups, start.Line, start.Column, raw);
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
                            tokens.Add (new TokenDeclaration (token, resolve, new ThemeCssTokenValue (token, model, TokenReferenceOf (value), declaration.Line, declaration.Column) { media_text = rule.MediaText }));
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

            // The key of the rules a custom property declared in a control rule is visible to: the same control type and part, in the
            // same state. A base rule's variables are visible in its state rules too (see ScopeFor), as they cascade in CSS.
            private static string ScopeKey (RawSelector selector, bool withPseudo)
                => $"{selector.TypeName?.ToLowerInvariant ()}{string.Concat (selector.Classes.OrderBy (c => c, StringComparer.Ordinal).Select (c => "." + c))}{(selector.Id is null ? string.Empty : "#" + selector.Id)}::{selector.Part?.ToLowerInvariant ()}:{(withPseudo ? selector.Pseudo?.ToLowerInvariant () : null)}";

            private static readonly Dictionary<string, RawDeclaration> no_rule_variables = new ();
            private readonly Dictionary<string, Dictionary<string, RawDeclaration>> _scopes = new (StringComparer.Ordinal);

            // The variables a selector's declarations see: the base rule's for its type and part, overridden by its own state's.
            private Dictionary<string, RawDeclaration> ScopeFor (RawSelector selector)
            {
                var key = ScopeKey (selector, true);

                if (_scopes.TryGetValue (key, out var scope))
                    return scope;

                _ruleVariables.TryGetValue (ScopeKey (selector, false), out var baseVariables);
                Dictionary<string, RawDeclaration>? stateVariables = null;
                if (selector.Pseudo is not null)
                    _ruleVariables.TryGetValue (key, out stateVariables);

                if (baseVariables is null && stateVariables is null) {
                    scope = no_rule_variables;
                } else {
                    scope = new Dictionary<string, RawDeclaration> (StringComparer.OrdinalIgnoreCase);
                    foreach (var pair in (IEnumerable<KeyValuePair<string, RawDeclaration>>?) baseVariables ?? Array.Empty<KeyValuePair<string, RawDeclaration>> ())
                        scope[pair.Key] = pair.Value;
                    foreach (var pair in (IEnumerable<KeyValuePair<string, RawDeclaration>>?) stateVariables ?? Array.Empty<KeyValuePair<string, RawDeclaration>> ())
                        scope[pair.Key] = pair.Value;
                }

                return _scopes[key] = scope;
            }

            private void CollectRuleVariables ()
            {
                foreach (var rule in _rules) {
                    if (rule.Selectors.Any (s => s.IsRoot))
                        continue;

                    foreach (var declaration in rule.Declarations) {
                        if (!IsCustomProperty (declaration.Name))
                            continue;

                        if (ThemeCssReference.FindToken (declaration.Name) is { } token) {
                            Error (declaration.Line, declaration.Column,
                                $"'{token.Name}' is a theme token, which is theme-wide: set it in ':root {{ {token.Name}: ...; }}'. A rule can declare its own variables under other names, e.g. '--{declaration.Name.Substring (2)}-local'.");
                            continue;
                        }

                        foreach (var selector in rule.Selectors) {
                            if (selector.Error is not null || (selector.TypeName is null && selector.Classes.Count == 0 && selector.Id is null))
                                continue;

                            var key = ScopeKey (selector, true);
                            if (!_ruleVariables.TryGetValue (key, out var variables))
                                _ruleVariables[key] = variables = new Dictionary<string, RawDeclaration> (StringComparer.OrdinalIgnoreCase);
                            variables[declaration.Name] = declaration;
                        }
                    }
                }
            }

            // Compiles `declarations` with `scope` for var(), reporting diagnostics only when `report` is set: a comma-separated selector
            // list compiles once per selector, since each may see different rule variables, but its mistakes are reported once.
            private CompiledDeclarations CompileInScope (List<RawDeclaration> declarations, Dictionary<string, RawDeclaration> scope, bool report)
            {
                var count = _diagnostics.Count;
                _scope = scope;

                try {
                    return CompileDeclarations (declarations);
                } finally {
                    _scope = null;
                    if (!report)
                        _diagnostics.RemoveRange (count, _diagnostics.Count - count);
                }
            }

            // Whether `value` reads one of `names`, directly or through a rule variable in `scope`. :root variables are resolved in :root's
            // own scope, so a rule cannot change what they mean.
            private static bool References (List<CssComponent> value, ICollection<string> names, Dictionary<string, RawDeclaration> scope, HashSet<string> visiting)
            {
                foreach (var component in value) {
                    if (component is not CssFunction fn)
                        continue;

                    if (fn.Name.Equals ("var", StringComparison.OrdinalIgnoreCase) && fn.Arguments.Count > 0 && fn.Arguments[0].Count == 1 && fn.Arguments[0][0] is CssIdent { Name: var name }) {
                        if (names.Contains (name))
                            return true;
                        if (scope.TryGetValue (name, out var variable) && visiting.Add (name) && References (variable.Value, names, scope, visiting))
                            return true;
                        if (fn.Arguments.Count > 1 && References (fn.Arguments[1], names, scope, visiting))
                            return true;
                        continue;
                    }

                    foreach (var group in fn.Arguments)
                        if (References (group, names, scope, visiting))
                            return true;
                }

                return false;
            }

            private void CompileControlRules (List<ControlRule> rules)
            {
                CollectRuleVariables ();

                // The state variants (selector key with its pseudo-class) whose own variables have already been folded back into the base
                // rule's declarations; see below.
                var cascaded = new HashSet<string> (StringComparer.Ordinal);

                foreach (var rule in _rules) {
                    if (rule.Selectors.Any (s => s.IsRoot))
                        continue;

                    var declarations = rule.Declarations.Where (d => !IsCustomProperty (d.Name)).ToList ();
                    var firstScope = ScopeFor (rule.Selectors[0]);

                    // Compile the declarations once, reporting their mistakes, then again for any selector in the list that sees other variables.
                    var first = CompileInScope (declarations, firstScope, report: true);

                    foreach (var raw in rule.Selectors) {
                        var scope = raw.Error is null ? ScopeFor (raw) : firstScope;
                        var compiled = ReferenceEquals (scope, firstScope) ? first : CompileInScope (declarations, scope, report: false);

                        if (raw.Error is not null) {
                            Error (raw.Line, raw.Column, raw.Error);
                            continue;
                        }

                        var selector = raw.TypeName is null ? ThemeCssSelector.Any : ThemeCssReference.FindSelector (raw.TypeName);

                        if (selector is null) {
                            Error (raw.Line, raw.Column,
                                $"Unknown selector '{raw.TypeName}'.{ThemeCssValues.Suggest (raw.TypeName!, ThemeCssReference.Selectors.Select (s => s.Name))} "
                                + $"Selectors are Majorsilence.Forms control type names: {string.Join (", ", ThemeCssReference.Selectors.Select (s => s.Name))}.");
                            continue;
                        }

                        var scoped = raw.Classes.Count > 0 || raw.Id is not null;

                        // Classes and ids belong to controls (Control.StyleClasses, Control.Name); a window has neither.
                        if (scoped && !selector.IsAny && selector.ControlType is { } styledType && typeof (WindowBase).IsAssignableFrom (styledType)) {
                            Error (raw.Line, raw.Column, $"'{raw.Raw}': {selector.Name} is a window, and a window has no style classes or id -- they belong to controls. Style every {selector.Name} with '{selector.Name} {{ ... }}'.");
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
                        var active = false;
                        var disabled = false;
                        var focus = false;
                        var focusVisible = false;

                        // The public model of one compiled rule: the longhand declarations plus the selector's classes, id and specificity.
                        ThemeCssRule NewModel (List<ThemeCssDeclaration> declarations) => new ThemeCssRule (selector, part, hover, active, disabled, focus, declarations, raw.Line, raw.Column) {
                            FocusVisible = focusVisible,
                            media_text = rule.MediaText,
                            Classes = raw.Classes.ToArray (),
                            Id = raw.Id,
                            Specificity = new ThemeCssSpecificity (raw.Id is null ? 0 : 1, raw.Classes.Count + (raw.Pseudo is null ? 0 : 1), (raw.TypeName is null ? 0 : 1) + (part is null ? 0 : 1)),
                        };

                        if (raw.Pseudo is not null) {
                            var pseudo = raw.Pseudo.ToLowerInvariant ();

                            if (pseudo is not ("hover" or "active" or "disabled" or "focus" or "focus-visible")) {
                                var hint = pseudo switch {
                                    "focus-within" => " A rule styles one control type, not its container; use ':focus' on the control.",
                                    "pressed" => " Use ':active'.",
                                    "checked" => " A checked state is not styleable yet.",
                                    _ => string.Empty
                                };
                                Error (raw.Line, raw.Column, $"':{raw.Pseudo}' is not supported; the pseudo-classes are ':hover', ':active', ':disabled', ':focus' and ':focus-visible'.{hint}");
                                continue;
                            }

                            if (part is not null) {
                                // A part is a piece painted inside a control (a header, a hovered item);
                                // it has no independent pressed/disabled/focus identity of its own, only
                                // hover (#285).
                                if (pseudo != "hover") {
                                    Error (raw.Line, raw.Column, $"'{selector.Name}::{part.Name}:{pseudo}' is not supported; a part only supports ':hover'.");
                                    continue;
                                }

                                if (!part.SupportsHover) {
                                    var hoverableParts = string.Join (", ", ThemeCssReference.Parts.Where (p => p.Part.SupportsHover).Select (p => $"{p.Selector.Name}::{p.Part.Name}"));
                                    Error (raw.Line, raw.Column, $"'{selector.Name}::{part.Name}:hover' is not supported: the {part.Name} does not change when hovered. Parts with ':hover': {hoverableParts}.");
                                    continue;
                                }

                                hover = true;
                            } else {
                                bool Supports (ThemeCssSelector s) => pseudo switch {
                                    "hover" => s.SupportsHover,
                                    "active" => s.SupportsActive,
                                    "disabled" => s.SupportsDisabled,
                                    "focus-visible" => s.SupportsFocusVisible,
                                    _ => s.SupportsFocus,
                                };

                                if (!selector.IsAny && !Supports (selector)) {
                                    var withIt = string.Join (", ", ThemeCssReference.Selectors.Where (Supports).Select (s => s.Name));
                                    Error (raw.Line, raw.Column, $"'{selector.Name}:{pseudo}' is not supported: {selector.Name} does not change appearance for that state. ':{pseudo}' is available on: {withIt}.");
                                    continue;
                                }

                                hover = pseudo == "hover";
                                active = pseudo == "active";
                                disabled = pseudo == "disabled";
                                focus = pseudo == "focus";
                                focusVisible = pseudo == "focus-visible";
                            }
                        }

                        // A part honours only the properties its renderer reads; anything else is dropped
                        // for that selector with an error that names the accepted list.
                        if (part is not null)
                            foreach (var item in compiled.Items)
                                if (!part.Accepts (item.Model.Property))
                                    Error (item.Model.Line, item.Model.Column, $"'{item.Model.Property}' does not apply to {selector.Name}::{part.Name}; it accepts: {string.Join (", ", part.Properties)}.");

                        // A transition animates a change of state style; a control that never changes state would never run it.
                        Func<string, bool>? accepts = part is null ? null : part.Accepts;

                        // A window is not composited by a parent, so it has no opacity of its own to fade and nothing draws an outline
                        // around it.
                        if (part is null && selector.ControlType is { } windowType && typeof (WindowBase).IsAssignableFrom (windowType)) {
                            static bool Composited (string property)
                                => property == "opacity" || property.StartsWith ("outline", StringComparison.Ordinal) || property.StartsWith ("animation", StringComparison.Ordinal);
                            var reported = new HashSet<(int, int)> ();
                            foreach (var item in compiled.Items)
                                if (Composited (item.Model.Property) && reported.Add ((item.Model.Line, item.Model.Column)))
                                    Error (item.Model.Line, item.Model.Column, item.Model.Property == "opacity"
                                        ? $"'opacity' does not apply to {selector.Name}, a window: nothing composites a window onto another surface. Set Form.Opacity in code."
                                        : item.Model.Property.StartsWith ("animation", StringComparison.Ordinal)
                                            ? $"'animation' does not apply to {selector.Name}, a window: a window's own style does not animate. Animate the controls on it."
                                            : $"'outline' does not apply to {selector.Name}, a window: an outline is drawn by the parent, and a window has none.");
                            var inner = accepts;
                            accepts = property => !Composited (property) && (inner is null || inner (property));
                        }
                        if (part is null && !selector.IsAny && !selector.HasStates) {
                            // The shorthand expands to four longhands from one declaration; report it once.
                            var reported = new HashSet<(int, int)> ();
                            foreach (var item in compiled.Items)
                                if (item.Model.Property.StartsWith ("transition", StringComparison.Ordinal) && reported.Add ((item.Model.Line, item.Model.Column)))
                                    Error (item.Model.Line, item.Model.Column,
                                        $"'{(compiled.Items.Count (i => i.Model.Line == item.Model.Line && i.Model.Column == item.Model.Column && i.Model.Property.StartsWith ("transition", StringComparison.Ordinal)) > 1 ? "transition" : item.Model.Property)}' has no effect on {selector.Name}: a transition animates a change between state styles, and {selector.Name} has none. "
                                        + $"Transitions run on: {string.Join (", ", ThemeCssReference.Selectors.Where (s => s.HasStates).Select (s => s.Name))}.");
                            var before = accepts;
                            accepts = property => !property.StartsWith ("transition", StringComparison.Ordinal) && (before is null || before (property));
                        }

                        // A state rule that overrides a variable changes every base declaration that reads it, as the cascade does in CSS:
                        // 'Button { background-color: var(--bg); } Button:hover { --bg: navy; }' makes the hovered button navy. Those
                        // declarations are compiled again with the state's variables and applied to the state style ahead of its own rule.
                        if (raw.Pseudo is not null && _ruleVariables.TryGetValue (ScopeKey (raw, true), out var overridden) && cascaded.Add (ScopeKey (raw, true))) {
                            var baseKey = ScopeKey (raw, false);
                            var inherited = _rules
                                .Where (r => !r.Selectors.Any (s => s.IsRoot) && r.Selectors.Any (s => s.Error is null && s.Pseudo is null && ScopeKey (s, false) == baseKey))
                                .SelectMany (r => r.Declarations)
                                .Where (d => !IsCustomProperty (d.Name) && References (d.Value, overridden.Keys, scope, new HashSet<string> (StringComparer.OrdinalIgnoreCase)))
                                .ToList ();

                            if (inherited.Count > 0) {
                                var (cascadeActions, cascadeModel) = CompileInScope (inherited, scope, report: false).Build (accepts);

                                if (cascadeActions.Count > 0)
                                    rules.Add (new ControlRule (selector, part, hover, active, disabled, focus, focusVisible, style => {
                                        foreach (var action in cascadeActions)
                                            action (style);
                                    }, NewModel (cascadeModel)));
                            }
                        }

                        var (actions, model) = compiled.Build (accepts);

                        if (actions.Count == 0)
                            continue;

                        var captured = actions;
                        rules.Add (new ControlRule (selector, part, hover, active, disabled, focus, focusVisible, style => {
                            foreach (var action in captured)
                                action (style);
                        }, NewModel (model)));
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

                    // A rule's own custom properties are its variables (CollectRuleVariables), not declarations.
                    if (IsCustomProperty (declaration.Name))
                        continue;

                    if (name == "transition-behavior") {
                        Error (declaration.Line, declaration.Column,
                            "'transition-behavior' is not supported: it only matters for properties that change in one step (display, visibility), and no themable property does. Leave it out.");
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
                            if (value.Count > 1) {
                                CompileRadiusShorthand (value, declaration, compiled);
                            } else if (ThemeCssValues.TryParseLength (value, out var radius, out error)) {
                                // The one-value form is "all four corners": it also drops any corner set
                                // earlier in the cascade, as CSS's shorthand does (#286).
                                Add (() => ThemeCssValue.Length (radius ()), s => {
                                    s.Border.Radius = radius ();
                                    s.Border.TopLeftRadius = s.Border.TopRightRadius = s.Border.BottomRightRadius = s.Border.BottomLeftRadius = null;
                                });
                            } else
                                Error (declaration.Line, declaration.Column, $"'{name}': {error}");
                            break;

                        case "border-top-left-radius":
                        case "border-top-right-radius":
                        case "border-bottom-right-radius":
                        case "border-bottom-left-radius":
                            if (ThemeCssValues.TryParseLength (value, out var cornerRadius, out error))
                                Add (() => ThemeCssValue.Length (cornerRadius ()), BorderCornerSetter (name, cornerRadius));
                            else
                                Error (declaration.Line, declaration.Column, $"'{name}': {error}");
                            break;

                        case "border-style":
                            if (TryParseBorderLineStyle (value, out var lineStyle, out error))
                                Add (() => ThemeCssValue.KeywordOf (lineStyle == ControlBorderLineStyle.Dashed ? "dashed" : "solid"), s => s.Border.LineStyle = lineStyle);
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

                        case "box-shadow":
                            if (ThemeCssValues.TryParseBoxShadows (value, out var boxShadows, out error))
                                Add (() => ThemeCssValue.Shadows (boxShadows ()), s => s.BoxShadows = boxShadows ());
                            else
                                Error (declaration.Line, declaration.Column, $"'{name}': {error}");
                            break;

                        case "background-image":
                            if (ThemeCssGradients.TryParse (value, out var gradients, out error))
                                Add (() => ThemeCssValue.Images (gradients ()), s => s.BackgroundGradients = gradients ());
                            else
                                Error (declaration.Line, declaration.Column, $"'{name}': {error}");
                            break;

                        case "outline":
                            CompileOutlineShorthand (value, declaration, compiled);
                            break;

                        case "outline-width":
                            if (TryParseOutlineWidth (value, out var outlineWidth, out error))
                                Add (() => ThemeCssValue.Length (outlineWidth ()), s => s.OutlineWidth = outlineWidth ());
                            else
                                Error (declaration.Line, declaration.Column, $"'{name}': {error}");
                            break;

                        case "outline-style":
                            error = null;
                            if (value.Count == 1 && TryParseOutlineStyle (value[0], out var outlineStyle, out error))
                                Add (() => ThemeCssValue.KeywordOf (OutlineKeyword (outlineStyle)), s => s.OutlineStyle = outlineStyle);
                            else
                                Error (declaration.Line, declaration.Column, $"'{name}': {error ?? "one keyword: solid, dashed, auto or none."}");
                            break;

                        case "outline-color":
                            if (ThemeCssValues.TryParseColor (value, out var outlineColor, out error))
                                Add (() => ThemeCssValue.Color ((uint) outlineColor ()), s => s.OutlineColor = outlineColor ());
                            else
                                Error (declaration.Line, declaration.Column, $"'{name}': {error}");
                            break;

                        case "outline-offset":
                            if (ThemeCssValues.TryParseLength (value, out var outlineOffset, out error, allowNegative: true))
                                Add (() => ThemeCssValue.Length (outlineOffset ()), s => s.OutlineOffset = outlineOffset ());
                            else
                                Error (declaration.Line, declaration.Column, $"'{name}': {error}");
                            break;

                        case "opacity":
                            if (ThemeCssValues.TryParseOpacity (value, out var opacity, out error))
                                Add (() => ThemeCssValue.NumberOf (opacity ()), s => s.Opacity = opacity ());
                            else
                                Error (declaration.Line, declaration.Column, $"'{name}': {error}");
                            break;

                        case "transition":
                            CompileTransitionShorthand (value, declaration, compiled);
                            break;

                        case "animation":
                            CompileAnimationShorthand (value, declaration, compiled);
                            break;

                        case "animation-name":
                        case "animation-duration":
                        case "animation-timing-function":
                        case "animation-delay":
                        case "animation-iteration-count":
                        case "animation-direction":
                        case "animation-fill-mode":
                        case "animation-play-state":
                            CompileAnimationLonghand (name, value, declaration, compiled);
                            break;

                        case "transition-property":
                        case "transition-duration":
                        case "transition-timing-function":
                        case "transition-delay":
                            CompileTransitionLonghand (name, value, declaration, compiled);
                            break;

                    }
                }

                return compiled;
            }

            // ---- outline ------------------------------------------------------------------------

            private static string OutlineKeyword (ControlOutlineStyle style) => style switch {
                ControlOutlineStyle.Solid => "solid",
                ControlOutlineStyle.Dashed => "dashed",
                _ => "none"
            };

            private static bool TryParseOutlineStyle (CssComponent component, out ControlOutlineStyle style, out string? error)
            {
                style = ControlOutlineStyle.None;
                error = null;

                switch ((component as CssIdent)?.Name.ToLowerInvariant ()) {
                    case "none": return true;
                    case "solid": case "auto": style = ControlOutlineStyle.Solid; return true;
                    case "dashed": style = ControlOutlineStyle.Dashed; return true;
                    case "dotted": case "double": case "groove": case "ridge": case "inset": case "outset":
                        error = $"the '{component.Raw}' outline style is not supported; outlines are solid, dashed, auto (drawn solid) or none.";
                        return false;
                    default:
                        error = $"'{component.Raw}' is not an outline style; use solid, dashed, auto or none.";
                        return false;
                }
            }

            private static bool TryParseOutlineWidth (List<CssComponent> value, out Func<int> width, out string? error)
            {
                if (value.Count == 1 && value[0] is CssIdent { Name: var keyword }) {
                    int? px = keyword.ToLowerInvariant () switch { "thin" => 1, "medium" => 3, "thick" => 5, _ => null };
                    if (px is { } fixedWidth) {
                        width = () => fixedWidth;
                        error = null;
                        return true;
                    }
                }

                return ThemeCssValues.TryParseLength (value, out width, out error);
            }

            // outline: [width] [style] [color], in any order; what it leaves out goes back to CSS's initial value.
            private void CompileOutlineShorthand (List<CssComponent> value, RawDeclaration declaration, CompiledDeclarations compiled)
            {
                Func<int>? width = null;
                ControlOutlineStyle? style = null;
                Func<SKColor>? color = null;

                foreach (var component in value) {
                    if (style is null && component is CssIdent && TryParseOutlineStyle (component, out var parsedStyle, out var styleError)) {
                        style = parsedStyle;
                        continue;
                    }

                    if (width is null && (component is CssNumber || ThemeCssCalc.IsMathFunction (component) || component is CssIdent { Name: "thin" or "medium" or "thick" })) {
                        if (!TryParseOutlineWidth (new List<CssComponent> { component }, out var parsedWidth, out var widthError)) {
                            Error (declaration.Line, declaration.Column, $"'outline': {widthError}");
                            return;
                        }
                        width = parsedWidth;
                        continue;
                    }

                    if (color is null && ThemeCssValues.TryParseColor (new List<CssComponent> { component }, out var parsedColor, out var colorError)) {
                        color = parsedColor;
                        continue;
                    }

                    var unknown = component is CssIdent && !ThemeCssValues.TryParseColor (new List<CssComponent> { component }, out _, out _)
                        && !TryParseOutlineStyle (component, out _, out var reason) ? reason : null;
                    Error (declaration.Line, declaration.Column, unknown is not null
                        ? $"'outline': {unknown}"
                        : $"'outline': '{component.Raw}' is not an outline width, style or colour. The form is 'outline: 2px solid #0d6efd;'.");
                    return;
                }

                var resolvedWidth = width ?? (() => 3);
                var resolvedStyle = style ?? ControlOutlineStyle.None;
                var reference = TokenReferenceOf (value);

                void Add (string property, Func<ThemeCssValue> model, Action<ControlStyle> action)
                    => compiled.Items.Add (new CompiledDeclaration (new ThemeCssDeclaration (property, model, reference, declaration.Line, declaration.Column)) { Action = action });

                Add ("outline-width", () => ThemeCssValue.Length (resolvedWidth ()), s => s.OutlineWidth = resolvedWidth ());
                Add ("outline-style", () => ThemeCssValue.KeywordOf (OutlineKeyword (resolvedStyle)), s => s.OutlineStyle = resolvedStyle);

                if (color is { } c)
                    Add ("outline-color", () => ThemeCssValue.Color ((uint) c ()), s => s.OutlineColor = c ());
            }

            // ---- transitions ---------------------------------------------------------------------

            // The four longhands of one transition, after parsing; the shorthand fills every one (with CSS's initial values where it
            // leaves one out), a longhand fills only its own.
            private sealed class TransitionLists
            {
                public List<string>? Properties;
                public List<TimeSpan>? Durations;
                public List<ControlTimingFunction>? Timings;
                public List<TimeSpan>? Delays;
            }

            private void CompileTransitionLonghand (string name, List<CssComponent> value, RawDeclaration declaration, CompiledDeclarations compiled)
            {
                var groups = ThemeCssValues.SplitCommas (value);
                var lists = new TransitionLists ();
                string? error = null;

                if (groups.Any (g => g.Count != 1)) {
                    Error (declaration.Line, declaration.Column, $"'{name}': '{ThemeCssValues.Join (value)}' is not a comma-separated list of single values, e.g. '{name}: {Example (name)};'.");
                    return;
                }

                switch (name) {
                    case "transition-property":
                        lists.Properties = new List<string> ();
                        if (groups.Count == 1 && groups[0][0] is CssIdent { Name: var only } && only.Equals ("none", StringComparison.OrdinalIgnoreCase))
                            break;
                        foreach (var group in groups)
                            if (!TryParseTransitionProperty (group[0], out var property, out error))
                                break;
                            else
                                lists.Properties.Add (property);
                        break;

                    case "transition-duration":
                    case "transition-delay":
                        var times = new List<TimeSpan> ();
                        foreach (var group in groups) {
                            if (!ThemeCssValues.TryParseTime (group[0], name == "transition-delay", out var time, out error))
                                break;
                            times.Add (time);
                        }
                        if (name == "transition-delay")
                            lists.Delays = times;
                        else
                            lists.Durations = times;
                        break;

                    default:
                        lists.Timings = new List<ControlTimingFunction> ();
                        foreach (var group in groups) {
                            if (!ThemeCssValues.TryParseTimingFunction (group[0], out var timing, out error))
                                break;
                            lists.Timings.Add (timing);
                        }
                        break;
                }

                if (error is not null) {
                    Error (declaration.Line, declaration.Column, $"'{name}': {error}");
                    return;
                }

                AddTransition (lists, declaration, compiled);
            }

            private static string Example (string name) => name switch {
                "transition-property" => "background-color, border-color",
                "transition-duration" => "150ms, 300ms",
                "transition-delay" => "0s, 50ms",
                _ => "ease-out, linear"
            };

            private bool TryParseTransitionProperty (CssComponent component, out string property, out string? error)
            {
                property = string.Empty;
                error = null;

                if (component is not CssIdent ident) {
                    error = $"'{component.Raw}' is not a property name.";
                    return false;
                }

                var name = ident.Name.ToLowerInvariant ();

                if (name == "none") {
                    error = "'none' can only be the whole value, meaning no property transitions; it cannot be one entry of a list.";
                    return false;
                }

                if (ControlTransition.IsAnimatable (name)) {
                    property = name;
                    return true;
                }

                if (name is "border-width" or "border-top-width" or "border-right-width" or "border-bottom-width" or "border-left-width" or "border") {
                    error = $"'{ident.Name}' cannot transition: a border's width decides where a control's client area starts, so animating it would move the control's children every frame. Transition border-color instead.";
                    return false;
                }

                if (ThemeCssReference.PropertyNames.Contains (name)) {
                    error = $"'{ident.Name}' cannot transition: it has no in-between values a theme can paint (fonts and border-style change in one step). Animatable: all, {string.Join (", ", ControlTransition.AnimatableProperties)}.";
                    return false;
                }

                error = $"Unknown property '{ident.Name}'.{ThemeCssValues.Suggest (name, ControlTransition.AnimatableProperties.Append ("all"))} Animatable: all, {string.Join (", ", ControlTransition.AnimatableProperties)}.";
                return false;
            }

            // transition: [ <property> || <duration> || <timing-function> || <delay> ]#
            private void CompileTransitionShorthand (List<CssComponent> value, RawDeclaration declaration, CompiledDeclarations compiled)
            {
                var groups = ThemeCssValues.SplitCommas (value);
                var lists = new TransitionLists {
                    Properties = new List<string> (),
                    Durations = new List<TimeSpan> (),
                    Timings = new List<ControlTimingFunction> (),
                    Delays = new List<TimeSpan> (),
                };

                void Fail (string message) => Error (declaration.Line, declaration.Column, $"'transition': {message}");

                foreach (var group in groups) {
                    if (group.Count == 0) {
                        Fail ("an entry of the list is empty; write e.g. 'transition: background-color 150ms ease-out, color 150ms;'.");
                        return;
                    }

                    string? property = null;
                    TimeSpan? duration = null, delay = null;
                    ControlTimingFunction? timing = null;
                    var isNone = false;

                    foreach (var component in group) {
                        string? error;

                        if (ThemeCssValues.IsTime (component)) {
                            if (duration is null) {
                                if (!ThemeCssValues.TryParseTime (component, false, out var d, out error)) {
                                    Fail (error!);
                                    return;
                                }
                                duration = d;
                            } else if (delay is null) {
                                ThemeCssValues.TryParseTime (component, true, out var l, out _);
                                delay = l;
                            } else {
                                Fail ($"'{component.Raw}' is a third time in one entry; an entry takes a duration and then, optionally, a delay.");
                                return;
                            }
                            continue;
                        }

                        if (ThemeCssValues.LooksLikeTiming (component)) {
                            if (timing is not null) {
                                Fail ($"'{component.Raw}' is a second timing function in one entry.");
                                return;
                            }
                            if (!ThemeCssValues.TryParseTimingFunction (component, out var t, out error)) {
                                Fail (error!);
                                return;
                            }
                            timing = t;
                            continue;
                        }

                        if (component is CssIdent { Name: var word } && (word.Equals ("allow-discrete", StringComparison.OrdinalIgnoreCase) || word.Equals ("normal", StringComparison.OrdinalIgnoreCase))) {
                            Fail ($"'{word}' (transition-behavior) is not supported: it only matters for properties that change in one step, and no themable property does. Leave it out.");
                            return;
                        }

                        if (component is CssNumber { Unit.Length: 0 } bare) {
                            Fail ($"'{bare.Raw}' has no unit; a time needs one: '{bare.Raw}ms' or '{bare.Raw}s'.");
                            return;
                        }

                        if (property is not null || isNone) {
                            Fail ($"'{component.Raw}' is a second property name in one entry; separate transitions with commas.");
                            return;
                        }

                        if (component is CssIdent { Name: var none } && none.Equals ("none", StringComparison.OrdinalIgnoreCase)) {
                            if (groups.Count > 1) {
                                Fail ("'none' cannot be one entry of a list; write 'transition: none;' on its own to turn transitions off.");
                                return;
                            }
                            isNone = true;
                            continue;
                        }

                        if (!TryParseTransitionProperty (component, out var p, out var propertyError)) {
                            Fail (propertyError!);
                            return;
                        }
                        property = p;
                    }

                    if (!isNone)
                        lists.Properties.Add (property ?? "all");
                    lists.Durations.Add (duration ?? TimeSpan.Zero);
                    lists.Timings.Add (timing ?? ControlTimingFunction.Ease);
                    lists.Delays.Add (delay ?? TimeSpan.Zero);
                }

                AddTransition (lists, declaration, compiled);
            }

            // One declaration per longhand set, so a host applier sees the four longhands, never the shorthand.
            // ---- animations ----------------------------------------------------------------------

            private sealed class AnimationLists
            {
                public List<ControlKeyframes>? Names;
                public List<TimeSpan>? Durations;
                public List<ControlTimingFunction>? Timings;
                public List<TimeSpan>? Delays;
                public List<double>? Iterations;
                public List<ControlAnimationDirection>? Directions;
                public List<ControlAnimationFillMode>? Fills;
                public List<bool>? Paused;
            }

            private static ControlAnimationDirection? DirectionOf (string word) => word.ToLowerInvariant () switch {
                "normal" => ControlAnimationDirection.Normal,
                "reverse" => ControlAnimationDirection.Reverse,
                "alternate" => ControlAnimationDirection.Alternate,
                "alternate-reverse" => ControlAnimationDirection.AlternateReverse,
                _ => null
            };

            private static ControlAnimationFillMode? FillOf (string word) => word.ToLowerInvariant () switch {
                "none" => ControlAnimationFillMode.None,
                "forwards" => ControlAnimationFillMode.Forwards,
                "backwards" => ControlAnimationFillMode.Backwards,
                "both" => ControlAnimationFillMode.Both,
                _ => null
            };

            private static bool? PausedOf (string word) => word.ToLowerInvariant () switch {
                "running" => false,
                "paused" => true,
                _ => null
            };

            private bool TryFindKeyframes (CssComponent component, out ControlKeyframes? keyframes, out string? error)
            {
                keyframes = null;
                error = null;
                var name = component switch { CssIdent ident => ident.Name, CssString text => text.Value, _ => null };

                if (name is null) {
                    error = $"'{component.Raw}' is not a keyframes name.";
                    return false;
                }

                if (_keyframes.TryGetValue (name, out keyframes))
                    return true;

                error = $"there is no '@keyframes {name}' in this sheet.{ThemeCssValues.Suggest (name, _keyframes.Keys)} Declare it in the same sheet: @keyframes {name} {{ from {{ ... }} to {{ ... }} }}.";
                return false;
            }

            private static bool TryParseIterations (CssComponent component, out double count, out string? error)
            {
                error = null;
                count = 1;

                if (component is CssIdent { Name: var word } && word.Equals ("infinite", StringComparison.OrdinalIgnoreCase)) {
                    count = double.PositiveInfinity;
                    return true;
                }

                if (component is CssNumber { Unit.Length: 0, Value: >= 0 } number) {
                    count = number.Value;
                    return true;
                }

                error = $"'{component.Raw}' is not an iteration count: a number of times (0 or more, fractions allowed) or 'infinite'.";
                return false;
            }

            private void CompileAnimationLonghand (string name, List<CssComponent> value, RawDeclaration declaration, CompiledDeclarations compiled)
            {
                var groups = ThemeCssValues.SplitCommas (value);
                var lists = new AnimationLists ();
                string? error = null;

                if (groups.Any (g => g.Count != 1)) {
                    Error (declaration.Line, declaration.Column, $"'{name}': '{ThemeCssValues.Join (value)}' is not a comma-separated list of single values.");
                    return;
                }

                foreach (var group in groups) {
                    var component = group[0];
                    var word = (component as CssIdent)?.Name ?? string.Empty;

                    switch (name) {
                        case "animation-name":
                            lists.Names ??= new ();
                            if (word.Equals ("none", StringComparison.OrdinalIgnoreCase) && groups.Count == 1)
                                break;
                            if (TryFindKeyframes (component, out var frames, out error))
                                lists.Names.Add (frames!);
                            break;
                        case "animation-duration":
                        case "animation-delay":
                            if (ThemeCssValues.TryParseTime (component, name == "animation-delay", out var time, out error))
                                (name == "animation-delay" ? lists.Delays ??= new () : lists.Durations ??= new ()).Add (time);
                            break;
                        case "animation-timing-function":
                            if (ThemeCssValues.TryParseTimingFunction (component, out var timing, out error))
                                (lists.Timings ??= new ()).Add (timing);
                            break;
                        case "animation-iteration-count":
                            if (TryParseIterations (component, out var count, out error))
                                (lists.Iterations ??= new ()).Add (count);
                            break;
                        case "animation-direction":
                            if (DirectionOf (word) is { } direction)
                                (lists.Directions ??= new ()).Add (direction);
                            else
                                error = $"'{component.Raw}' is not a direction: normal, reverse, alternate or alternate-reverse.";
                            break;
                        case "animation-fill-mode":
                            if (FillOf (word) is { } fill)
                                (lists.Fills ??= new ()).Add (fill);
                            else
                                error = $"'{component.Raw}' is not a fill mode: none, forwards, backwards or both.";
                            break;
                        default:
                            if (PausedOf (word) is { } paused)
                                (lists.Paused ??= new ()).Add (paused);
                            else
                                error = $"'{component.Raw}' is not a play state: running or paused.";
                            break;
                    }

                    if (error is not null) {
                        Error (declaration.Line, declaration.Column, $"'{name}': {error}");
                        return;
                    }
                }

                AddAnimation (lists, declaration, compiled);
            }

            // animation: [ <duration> || <timing-function> || <delay> || <iteration-count> || <direction> || <fill-mode> || <play-state> || <name> ]#
            private void CompileAnimationShorthand (List<CssComponent> value, RawDeclaration declaration, CompiledDeclarations compiled)
            {
                var lists = new AnimationLists {
                    Names = new (), Durations = new (), Timings = new (), Delays = new (), Iterations = new (), Directions = new (), Fills = new (), Paused = new ()
                };
                var groups = ThemeCssValues.SplitCommas (value);

                void Fail (string message) => Error (declaration.Line, declaration.Column, $"'animation': {message}");

                foreach (var group in groups) {
                    ControlKeyframes? frames = null;
                    var none = false;
                    TimeSpan? duration = null, delay = null;
                    ControlTimingFunction? timing = null;
                    double? iterations = null;
                    ControlAnimationDirection? direction = null;
                    ControlAnimationFillMode? fill = null;
                    bool? paused = null;

                    foreach (var component in group) {
                        string? error;
                        var word = (component as CssIdent)?.Name ?? string.Empty;

                        if (ThemeCssValues.IsTime (component)) {
                            if (duration is null) {
                                if (!ThemeCssValues.TryParseTime (component, false, out var d, out error)) {
                                    Fail (error!);
                                    return;
                                }
                                duration = d;
                            } else if (delay is null) {
                                ThemeCssValues.TryParseTime (component, true, out var l, out _);
                                delay = l;
                            } else {
                                Fail ($"'{component.Raw}' is a third time in one entry; an entry takes a duration and then, optionally, a delay.");
                                return;
                            }
                            continue;
                        }

                        if (ThemeCssValues.LooksLikeTiming (component) && timing is null) {
                            if (!ThemeCssValues.TryParseTimingFunction (component, out var t, out error)) {
                                Fail (error!);
                                return;
                            }
                            timing = t;
                            continue;
                        }

                        // A bare number or 'infinite' is the iteration count; the keywords are taken before a name, as CSS does.
                        if (iterations is null && (component is CssNumber { Unit.Length: 0 } || word.Equals ("infinite", StringComparison.OrdinalIgnoreCase))) {
                            if (!TryParseIterations (component, out var count, out error)) {
                                Fail (error!);
                                return;
                            }
                            iterations = count;
                            continue;
                        }

                        if (direction is null && DirectionOf (word) is { } dir) {
                            direction = dir;
                            continue;
                        }

                        if (fill is null && FillOf (word) is { } f && !word.Equals ("none", StringComparison.OrdinalIgnoreCase)) {
                            fill = f;
                            continue;
                        }

                        if (paused is null && PausedOf (word) is { } p) {
                            paused = p;
                            continue;
                        }

                        if (word.Equals ("none", StringComparison.OrdinalIgnoreCase) && frames is null && !none) {
                            none = true;
                            continue;
                        }

                        if (frames is not null || none) {
                            Fail ($"'{component.Raw}' is a second name in one entry; separate animations with commas.");
                            return;
                        }

                        if (!TryFindKeyframes (component, out frames, out error)) {
                            Fail (error!);
                            return;
                        }
                    }

                    if (frames is null && !none) {
                        Fail ($"'{ThemeCssValues.Join (group)}' names no keyframes; an entry is e.g. 'pulse 1s ease-in-out infinite'.");
                        return;
                    }

                    if (frames is not null)
                        lists.Names.Add (frames);
                    lists.Durations.Add (duration ?? TimeSpan.Zero);
                    lists.Timings.Add (timing ?? ControlTimingFunction.Ease);
                    lists.Delays.Add (delay ?? TimeSpan.Zero);
                    lists.Iterations.Add (iterations ?? 1);
                    lists.Directions.Add (direction ?? ControlAnimationDirection.Normal);
                    lists.Fills.Add (fill ?? ControlAnimationFillMode.None);
                    lists.Paused.Add (paused ?? false);
                }

                AddAnimation (lists, declaration, compiled);
            }

            private static string Keyword (ControlAnimationDirection direction) => direction switch {
                ControlAnimationDirection.Reverse => "reverse",
                ControlAnimationDirection.Alternate => "alternate",
                ControlAnimationDirection.AlternateReverse => "alternate-reverse",
                _ => "normal"
            };

            private static string Keyword (ControlAnimationFillMode fill) => fill.ToString ().ToLowerInvariant ();

            // One declaration per longhand set, so a host applier sees the longhands, never the shorthand.
            private static void AddAnimation (AnimationLists lists, RawDeclaration declaration, CompiledDeclarations compiled)
            {
                void Add (string property, ThemeCssValue model, Action<ControlStyle> action)
                    => compiled.Items.Add (new CompiledDeclaration (new ThemeCssDeclaration (property, () => model, null, declaration.Line, declaration.Column)) { Action = action });

                static ControlAnimation Current (ControlStyle s) => s.Animation ?? new ControlAnimation ();
                static ThemeCssValue Words (IEnumerable<string> words) => ThemeCssValue.ListOf (words.Select (ThemeCssValue.KeywordOf).ToArray ());

                if (lists.Names is { } names) {
                    var items = names.ToArray ();
                    Add ("animation-name", Words (items.Select (k => k.Name)), s => s.Animation = Current (s).With (keyframes: items));
                }
                if (lists.Durations is { } durations) {
                    var items = durations.ToArray ();
                    Add ("animation-duration", ThemeCssValue.ListOf (items.Select (ThemeCssValue.Time).ToArray ()), s => s.Animation = Current (s).With (durations: items));
                }
                if (lists.Timings is { } timings) {
                    var items = timings.ToArray ();
                    Add ("animation-timing-function", ThemeCssValue.ListOf (items.Select (ThemeCssValue.Timing).ToArray ()), s => s.Animation = Current (s).With (timingFunctions: items));
                }
                if (lists.Delays is { } delays) {
                    var items = delays.ToArray ();
                    Add ("animation-delay", ThemeCssValue.ListOf (items.Select (ThemeCssValue.Time).ToArray ()), s => s.Animation = Current (s).With (delays: items));
                }
                if (lists.Iterations is { } iterations) {
                    var items = iterations.ToArray ();
                    Add ("animation-iteration-count", ThemeCssValue.ListOf (items.Select (c => double.IsPositiveInfinity (c) ? ThemeCssValue.KeywordOf ("infinite") : ThemeCssValue.NumberOf (c)).ToArray ()),
                        s => s.Animation = Current (s).With (iterationCounts: items));
                }
                if (lists.Directions is { } directions) {
                    var items = directions.ToArray ();
                    Add ("animation-direction", Words (items.Select (Keyword)), s => s.Animation = Current (s).With (directions: items));
                }
                if (lists.Fills is { } fills) {
                    var items = fills.ToArray ();
                    Add ("animation-fill-mode", Words (items.Select (Keyword)), s => s.Animation = Current (s).With (fillModes: items));
                }
                if (lists.Paused is { } paused) {
                    var items = paused.ToArray ();
                    Add ("animation-play-state", Words (items.Select (p => p ? "paused" : "running")), s => s.Animation = Current (s).With (paused: items));
                }
            }

            private static void AddTransition (TransitionLists lists, RawDeclaration declaration, CompiledDeclarations compiled)
            {
                void Add (string property, ThemeCssValue model, Action<ControlStyle> action)
                    => compiled.Items.Add (new CompiledDeclaration (new ThemeCssDeclaration (property, () => model, null, declaration.Line, declaration.Column)) { Action = action });

                static ControlTransition Current (ControlStyle s) => s.Transition ?? new ControlTransition ();

                if (lists.Properties is { } properties) {
                    var items = properties.ToArray ();
                    Add ("transition-property", ThemeCssValue.ListOf (items.Select (ThemeCssValue.KeywordOf).ToArray ()),
                        s => s.Transition = Current (s).With (properties: items));
                }

                if (lists.Durations is { } durations) {
                    var items = durations.ToArray ();
                    Add ("transition-duration", ThemeCssValue.ListOf (items.Select (ThemeCssValue.Time).ToArray ()),
                        s => s.Transition = Current (s).With (durations: items));
                }

                if (lists.Timings is { } timings) {
                    var items = timings.ToArray ();
                    Add ("transition-timing-function", ThemeCssValue.ListOf (items.Select (ThemeCssValue.Timing).ToArray ()),
                        s => s.Transition = Current (s).With (timingFunctions: items));
                }

                if (lists.Delays is { } delays) {
                    var items = delays.ToArray ();
                    Add ("transition-delay", ThemeCssValue.ListOf (items.Select (ThemeCssValue.Time).ToArray ()),
                        s => s.Transition = Current (s).With (delays: items));
                }
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
                "dotted", "double", "groove", "ridge", "inset", "outset"
            };

            private static Action<ControlStyle> BorderCornerSetter (string property, Func<int> radius) => property switch {
                "border-top-left-radius" => s => s.Border.TopLeftRadius = radius (),
                "border-top-right-radius" => s => s.Border.TopRightRadius = radius (),
                "border-bottom-right-radius" => s => s.Border.BottomRightRadius = radius (),
                _ => s => s.Border.BottomLeftRadius = radius (),
            };

            // 'solid' or 'dashed'; 'none' and 'hidden' are width 0 and belong to the 'border' shorthand.
            private static bool TryParseBorderLineStyle (List<CssComponent> value, out ControlBorderLineStyle style, out string? error)
            {
                style = ControlBorderLineStyle.Solid;
                error = null;

                if (value.Count == 1 && value[0] is CssIdent ident) {
                    if (ident.Name.Equals ("solid", StringComparison.OrdinalIgnoreCase))
                        return true;

                    if (ident.Name.Equals ("dashed", StringComparison.OrdinalIgnoreCase)) {
                        style = ControlBorderLineStyle.Dashed;
                        return true;
                    }

                    error = unsupported_border_styles.Contains (ident.Name)
                        ? $"the '{ident.Name}' border style is not supported; borders are 'solid' or 'dashed'. Write e.g. 'border-style: dashed;'."
                        : $"'{ident.Name}' is not a border style. Use 'solid' or 'dashed'; for no border write 'border-width: 0;' or 'border: none;'.";
                    return false;
                }

                error = $"'{string.Join (" ", value.Select (c => c.Raw))}' is not a border style. Use 'solid' or 'dashed'.";
                return false;
            }

            // border-radius with two to four values: top-left, top-right, bottom-right, bottom-left, the
            // missing ones filled in the CSS way (#286). A host sees the four longhands the shorthand
            // stands for, like it does for 'border'.
            private void CompileRadiusShorthand (List<CssComponent> value, RawDeclaration declaration, CompiledDeclarations compiled)
            {
                if (value.Count > 4) {
                    Error (declaration.Line, declaration.Column, "'border-radius': at most four values (top-left, top-right, bottom-right, bottom-left) are accepted; elliptical radii written with '/' are not supported.");
                    return;
                }

                var lengths = new List<Func<int>> ();
                var references = new List<ThemeCssToken?> ();

                foreach (var component in value) {
                    if (!ThemeCssValues.TryParseLength (new List<CssComponent> { component }, out var length, out var lengthError)) {
                        Error (component, $"'border-radius': {lengthError}");
                        return;
                    }

                    lengths.Add (length);
                    references.Add ((component as CssTokenRef)?.Token);
                }

                // 2 values: a = TL+BR, b = TR+BL.  3: a = TL, b = TR+BL, c = BR.  4: as written.
                int[] pick = lengths.Count switch {
                    2 => new[] { 0, 1, 0, 1 },
                    3 => new[] { 0, 1, 2, 1 },
                    _ => new[] { 0, 1, 2, 3 },
                };
                var names = new[] { "border-top-left-radius", "border-top-right-radius", "border-bottom-right-radius", "border-bottom-left-radius" };

                for (var i = 0; i < 4; i++) {
                    var length = lengths[pick[i]];
                    var corner = names[i];

                    compiled.Items.Add (new CompiledDeclaration (new ThemeCssDeclaration (corner, () => ThemeCssValue.Length (length ()), references[pick[i]], declaration.Line, declaration.Column)) {
                        // The first corner also drops the one-value radius set earlier, as the shorthand resets it.
                        Action = i == 0
                            ? s => { s.Border.Radius = null; BorderCornerSetter (corner, length) (s); }
                            : BorderCornerSetter (corner, length)
                    });
                }
            }

            private void CompileBorderShorthand (List<CssComponent> value, RawDeclaration declaration, CompiledDeclarations compiled)
            {
                Func<int>? width = null;
                Func<SKColor>? color = null;
                ThemeCssToken? widthReference = null;
                ThemeCssToken? colorReference = null;
                ControlBorderLineStyle? lineStyle = null;

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

                        if (ident.Name.Equals ("solid", StringComparison.OrdinalIgnoreCase)) {
                            lineStyle = ControlBorderLineStyle.Solid;
                            continue;
                        }

                        if (ident.Name.Equals ("dashed", StringComparison.OrdinalIgnoreCase)) {
                            lineStyle = ControlBorderLineStyle.Dashed;
                            continue;
                        }

                        if (unsupported_border_styles.Contains (ident.Name)) {
                            Error (component, $"'border': the '{ident.Name}' border style is not supported; borders are 'solid' or 'dashed'. Write e.g. 'border: 1px dashed #808080;'.");
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
                        Error (component, $"'border': unexpected '{component.Raw}' after the color. The form is 'border: [width] [solid|dashed|none] [color]'.");
                        return;
                    }

                    if (!ThemeCssValues.TryParseColor (new List<CssComponent> { component }, out color, out var colorError)) {
                        Error (component, $"'border': {colorError}");
                        return;
                    }

                    colorReference = (component as CssTokenRef)?.Token;
                }

                if (width is null && color is null && lineStyle != ControlBorderLineStyle.Dashed) {
                    Error (declaration.Line, declaration.Column, "'border' needs a width, a style and/or a color, e.g. 'border: 1px solid #808080;' or 'border: none;'.");
                    return;
                }

                // The model sees the longhands the shorthand stands for, in the order a host would apply them.
                if (width is not null) {
                    var w = width;
                    compiled.Items.Add (new CompiledDeclaration (new ThemeCssDeclaration ("border-width", () => ThemeCssValue.Length (w ()), widthReference, declaration.Line, declaration.Column)) {
                        Action = lineStyle == ControlBorderLineStyle.Solid
                            ? s => { s.Border.Width = w (); s.Border.LineStyle = ControlBorderLineStyle.Solid; }
                            : s => s.Border.Width = w ()
                    });
                }

                // Only 'dashed' is a model declaration: a host that cannot draw it must be told, but every
                // existing 'border: 1px solid ...' would otherwise report an unsupported property. An
                // explicit 'solid' still resets an earlier dashed style, folded into the width action.
                if (lineStyle == ControlBorderLineStyle.Dashed)
                    compiled.Items.Add (new CompiledDeclaration (new ThemeCssDeclaration ("border-style", () => ThemeCssValue.KeywordOf ("dashed"), null, declaration.Line, declaration.Column)) {
                        Action = s => s.Border.LineStyle = ControlBorderLineStyle.Dashed
                    });

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

                    if (_scope is not null && _scope.TryGetValue (name, out var ruleVariable)) {
                        if (!visiting.Add (name)) {
                            Error (fn, $"'{name}' refers to itself (directly or through other variables).");
                            return null;
                        }

                        _usedRuleVariables.Add (ruleVariable);
                        var substitutedLocal = Substitute (ruleVariable.Value, visiting, declaration);
                        visiting.Remove (name);

                        if (substitutedLocal is null)
                            return null;

                        result.AddRange (substitutedLocal);
                        continue;
                    }

                    if (_variables.TryGetValue (name, out var variable)) {
                        if (!visiting.Add (name)) {
                            Error (fn, $"'{name}' refers to itself (directly or through other variables).");
                            return null;
                        }

                        _usedVariables.Add (name);

                        // A :root variable means what it means on :root, whatever rule reads it.
                        var outer = _scope;
                        _scope = null;
                        var substituted = Substitute (variable.Value, visiting, declaration);
                        _scope = outer;
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

            // The public model of the author variables. Runs after every other pass and resolves
            // silently: a broken variable already produced its error where it is used (or its
            // "never used" warning), so resolving it again must not report it twice or mark others used.
            private List<ThemeCssVariable> CompileVariables ()
            {
                var result = new List<ThemeCssVariable> ();
                var diagnosticCount = _diagnostics.Count;
                var used = new HashSet<string> (_usedVariables, StringComparer.OrdinalIgnoreCase);

                foreach (var variable in _variables.Values) {
                    Func<ThemeCssValue>? resolve = null;
                    ThemeCssToken? reference = null;
                    var value = Substitute (variable.Value, new HashSet<string> (StringComparer.OrdinalIgnoreCase) { variable.Name }, variable);

                    if (value is not null) {
                        reference = TokenReferenceOf (value);

                        if (ThemeCssValues.TryParseColor (value, out var color, out _))
                            resolve = () => ThemeCssValue.Color ((uint) color ());
                        else if (ThemeCssValues.TryParseLength (value, out var length, out _))
                            resolve = () => ThemeCssValue.Length (length ());
                        else if (ThemeCssValues.TryParseFontFamilies (value, out var families, out _))
                            resolve = () => ThemeCssValue.Families (families ());
                    }

                    result.Add (new ThemeCssVariable (variable.Name, resolve, reference, variable.Line, variable.Column));
                }

                _diagnostics.RemoveRange (diagnosticCount, _diagnostics.Count - diagnosticCount);
                _usedVariables.Clear ();
                _usedVariables.UnionWith (used);

                return result;
            }

            private void WarnUnusedVariables ()
            {
                foreach (var declaration in _ruleVariables.Values.SelectMany (v => v.Values).Distinct ()) {
                    if (_usedRuleVariables.Contains (declaration))
                        continue;

                    Warning (declaration.Line, declaration.Column,
                        $"'{declaration.Name}' is declared but never used: nothing in this rule, its control's other rules or their states reads var({declaration.Name}).");
                }

                foreach (var variable in _variables.Values) {
                    if (_usedVariables.Contains (variable.Name))
                        continue;

                    var closest = ThemeCssValues.FindClosest (variable.Name, ThemeCssReference.Tokens.Select (t => t.Name));

                    // Not "no effect": host appliers publish author variables as named resources
                    // (ThemeStyleSheet.Variables), so a variable only a host's own views read is legitimate.
                    const string Consequence = "so Majorsilence.Forms controls ignore it (a host applier may still publish it as a named resource).";
                    Warning (variable.Line, variable.Column, closest is null
                        ? $"'{variable.Name}' is declared but never used, and it is not a theme token, {Consequence}"
                        : $"'{variable.Name}' is declared but never used, and it is not a theme token, {Consequence} Did you mean '{closest}'?");
                }
            }
        }
    }
}
