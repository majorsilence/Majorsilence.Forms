using System.Collections.Generic;
using System.IO;
using System.Text;
using SkiaSharp;

namespace Majorsilence.Forms
{
    // CSS-defined themes. A theme is a stylesheet in the restricted subset ThemeStyleSheet accepts:
    //
    //   @theme "Ocean" extends Dark;
    //
    //   :root {
    //     --accent-color: #1e90ff;          /* one token per Theme property */
    //     --background-color: #0a1929;
    //   }
    //
    //   Button { border-radius: 4px; }      /* one rule per control type */
    //   Button:hover { background-color: var(--accent-color); }
    //
    // Tokens layer onto the current Theme values exactly like an XML theme's elements do. Control rules
    // are different: they are a property of the stylesheet chain being applied, so applying a sheet
    // REPLACES every previously applied control rule (a rule that disappears from the sheet returns the
    // control type to its declared default), and SetBuiltInTheme clears them along with the colors.
    public static partial class Theme
    {
        // The type-level styles currently carrying stylesheet rules, so the next apply (or a built-in
        // reset) can return the ones no longer targeted to their declared defaults.
        private static readonly HashSet<ControlStyle> stylesheet_styles = new ();

        // The stylesheet chain whose rules are installed (base first), and the chain a host bridge has
        // not yet been told about. The notification is deferred to InvokeThemeChanged so it always
        // follows ThemeChanged -- a bridge that mirrors the theme onto another toolkit must see the
        // final Theme values, not the half-applied ones inside a BeginUpdate block.
        private static IReadOnlyList<ThemeStyleSheet> current_stylesheets = Array.Empty<ThemeStyleSheet> ();
        private static IReadOnlyList<ThemeStyleSheet>? pending_stylesheet_notification;

        /// <summary>
        /// Raised, after <see cref="ThemeChanged"/>, whenever a CSS stylesheet (or a theme whose base
        /// chain contains one) has been applied. Host bridges -- code that mirrors the theme onto
        /// System.Windows.Forms or Avalonia controls -- subscribe here and read the sheet through
        /// <see cref="ThemeStyleSheet.Tokens"/> / <see cref="ThemeStyleSheet.Rules"/>. Not raised by
        /// <see cref="SetBuiltInTheme"/>, which clears the stylesheets; watch <see cref="ThemeChanged"/>
        /// and <see cref="CurrentStyleSheets"/> for that.
        /// </summary>
        public static event EventHandler<ThemeStyleSheetEventArgs>? StyleSheetApplied;

        /// <summary>
        /// The CSS stylesheets whose control rules are currently installed, base first (the last is the
        /// one applied). Empty when no CSS theme is in effect, including after
        /// <see cref="SetBuiltInTheme"/>.
        /// </summary>
        public static IReadOnlyList<ThemeStyleSheet> CurrentStyleSheets {
            get {
                lock (_lock)
                    return current_stylesheets;
            }
        }

        /// <summary>
        /// Registers a CSS theme so it can later be applied by name with <see cref="ApplyTheme"/> or
        /// used as the base of another theme. The stylesheet must start with an
        /// <c>@theme "Name"</c> declaration; a theme registered under an existing name replaces it.
        /// Returns the theme's name.
        /// </summary>
        /// <exception cref="ThemeCssException">The stylesheet has errors; see its
        /// <see cref="ThemeCssException.Diagnostics"/> for every problem found.</exception>
        public static string RegisterThemeCss (string css)
        {
            if (string.IsNullOrWhiteSpace (css))
                throw new ArgumentException ("Theme CSS cannot be null or empty.", nameof (css));

            var sheet = ThemeStyleSheet.Parse (css);

            if (sheet.HasErrors)
                throw new ThemeCssException (sheet.Diagnostics);

            if (string.IsNullOrWhiteSpace (sheet.Name))
                throw new ThemeCssException ("A registered CSS theme must start with an @theme declaration naming it, e.g. @theme \"Ocean\" extends Dark;");

            var name = sheet.Name!;

            lock (_lock)
                registered_themes[name] = sheet;

            return name;
        }

        /// <summary>
        /// Registers a CSS theme read from a file. Returns the theme's name.
        /// </summary>
        public static string RegisterThemeCssFromFile (string path)
        {
            if (string.IsNullOrWhiteSpace (path))
                throw new ArgumentException ("Path cannot be null or empty.", nameof (path));

            return RegisterThemeCss (File.ReadAllText (path));
        }

        /// <summary>
        /// Registers a CSS theme read from a stream. Returns the theme's name.
        /// </summary>
        public static string RegisterThemeCssFromStream (Stream stream)
        {
            Guard.ThrowIfNull (stream);

            using var reader = new StreamReader (stream, Encoding.UTF8, detectEncodingFromByteOrderMarks: true, bufferSize: 4096, leaveOpen: true);
            return RegisterThemeCss (reader.ReadToEnd ());
        }

        /// <summary>
        /// Parses a CSS theme and applies it immediately without registering it, raising
        /// <see cref="ThemeChanged"/> once. The <c>@theme</c> header is optional here.
        /// </summary>
        /// <exception cref="ThemeCssException">The stylesheet has errors. Nothing is applied.</exception>
        public static void LoadFromCss (string css)
        {
            if (string.IsNullOrWhiteSpace (css))
                throw new ArgumentException ("Theme CSS cannot be null or empty.", nameof (css));

            var sheet = ThemeStyleSheet.Parse (css);

            if (sheet.HasErrors)
                throw new ThemeCssException (sheet.Diagnostics);

            ApplyStyleSheet (sheet);
        }

        /// <summary>
        /// Reads a CSS theme from a file and applies it immediately without registering it.
        /// </summary>
        public static void LoadFromCssFile (string path)
        {
            if (string.IsNullOrWhiteSpace (path))
                throw new ArgumentException ("Path cannot be null or empty.", nameof (path));

            LoadFromCss (File.ReadAllText (path));
        }

        /// <summary>
        /// Applies an already parsed stylesheet: its base theme (if any), then its tokens, then its
        /// control rules -- replacing the control rules of any previously applied stylesheet -- and
        /// raises <see cref="ThemeChanged"/> once. Unlike <see cref="LoadFromCss"/> this does not
        /// refuse a sheet with errors: whatever parsed is applied, which is what a live editor wants
        /// while the author is mid-edit.
        /// </summary>
        public static void ApplyStyleSheet (ThemeStyleSheet sheet)
        {
            Guard.ThrowIfNull (sheet);

            BeginUpdate ();
            try {
                var chain = new List<ThemeStyleSheet> ();
                var visiting = new HashSet<string> (StringComparer.OrdinalIgnoreCase);

                if (!string.IsNullOrEmpty (sheet.Name))
                    visiting.Add (sheet.Name!);

                ApplyStyleSheetLayer (sheet, visiting, chain);
                ApplyControlRules (chain);
                RaiseThemeChanged ();
            } finally {
                EndUpdate ();
            }
        }

        /// <summary>
        /// Writes the current theme's tokens as a CSS theme -- the natural starting point for a new
        /// theme: export, delete the tokens you do not want to change, edit the rest.
        /// </summary>
        /// <param name="name">A name for the <c>@theme</c> header, or null to omit the header.</param>
        /// <param name="baseTheme">A base theme for <c>extends</c>, or null. Ignored without a name.</param>
        public static string ExportCss (string? name = null, string? baseTheme = null)
        {
            var sb = new StringBuilder ();

            sb.AppendLine ("/* Majorsilence.Forms theme. Every token is documented in docs/theming.md.");
            sb.AppendLine ("   Delete a token to keep the base theme's value for it. */");

            if (!string.IsNullOrWhiteSpace (name)) {
                sb.Append ("@theme \"").Append (name).Append ('"');
                if (!string.IsNullOrWhiteSpace (baseTheme))
                    sb.Append (" extends ").Append (baseTheme);
                sb.AppendLine (";");
            }

            sb.AppendLine ();
            sb.AppendLine (":root {");

            foreach (var token in ThemeCssReference.Tokens) {
                sb.Append ("  /* ").Append (token.Description).AppendLine (" */");
                sb.Append ("  ").Append (token.Name).Append (": ").Append (FormatTokenValue (token)).AppendLine (";");
            }

            sb.AppendLine ("}");

            return sb.ToString ();
        }

        /// <summary>Formats a token's current value the way a stylesheet would spell it.</summary>
        public static string FormatTokenValue (ThemeCssToken token)
        {
            Guard.ThrowIfNull (token);

            var value = GetTokenValue (token);

            return token.Kind switch {
                ThemeCssValueKind.Color => ThemeCssValues.FormatColor ((SKColor) value),
                ThemeCssValueKind.Length => $"{(int) value}px",
                _ => $"\"{((SKTypeface) value).FamilyName}\"",
            };
        }

        // --- internals -------------------------------------------------------------------------

        internal static object GetTokenValue (ThemeCssToken token) => values[token.PropertyName];

        internal static void SetTokenValue (ThemeCssToken token, object value) => values[token.PropertyName] = value;

        // Applies the base (recursively) and then this sheet's tokens, and appends the sheet to the
        // chain whose control rules are applied afterwards, base first so a derived sheet wins.
        private static void ApplyStyleSheetLayer (ThemeStyleSheet sheet, HashSet<string> visiting, List<ThemeStyleSheet> chain)
        {
            if (!string.IsNullOrEmpty (sheet.BaseName))
                ApplyBase (sheet.BaseName!, visiting, chain, message => new ThemeCssException (message));

            foreach (var declaration in sheet.TokenDeclarations)
                SetTokenValue (declaration.Token, declaration.Resolve ());

            chain.Add (sheet);
        }

        // Installs the chain's control rules on the type-level styles they target and returns every
        // previously targeted style that is no longer mentioned to its declared defaults.
        private static void ApplyControlRules (List<ThemeStyleSheet> chain)
        {
            var targets = new Dictionary<ControlStyle, List<Action<ControlStyle>>> ();
            var order = new List<ControlStyle> ();

            foreach (var sheet in chain)
                foreach (var rule in sheet.ControlRules) {
                    var style = rule.Hover ? rule.Selector.GetHoverStyle! () : rule.Selector.GetStyle ();

                    if (!targets.TryGetValue (style, out var actions)) {
                        actions = new List<Action<ControlStyle>> ();
                        targets[style] = actions;
                        order.Add (style);
                    }

                    actions.Add (rule.Apply);
                }

            foreach (var stale in stylesheet_styles)
                if (!targets.ContainsKey (stale))
                    stale.ResetWithStyleSheetRules (null);

            stylesheet_styles.Clear ();

            foreach (var style in order) {
                var actions = targets[style];

                style.ResetWithStyleSheetRules (s => {
                    foreach (var action in actions)
                        action (s);
                });

                stylesheet_styles.Add (style);
            }

            var applied = chain.ToArray ();

            lock (_lock) {
                current_stylesheets = applied;
                pending_stylesheet_notification = applied;
            }
        }

        // Called from InvokeThemeChanged, after ThemeChanged and the repaint marking.
        private static void RaiseStyleSheetApplied ()
        {
            IReadOnlyList<ThemeStyleSheet>? chain;

            lock (_lock) {
                chain = pending_stylesheet_notification;
                pending_stylesheet_notification = null;
            }

            if (chain is not null && chain.Count > 0)
                StyleSheetApplied?.Invoke (null, new ThemeStyleSheetEventArgs (chain));
        }

        // Called by SetBuiltInTheme: a built-in theme is a full reset, control rules included.
        private static void ClearStyleSheetRules ()
        {
            foreach (var style in stylesheet_styles)
                style.ResetWithStyleSheetRules (null);

            stylesheet_styles.Clear ();

            lock (_lock) {
                current_stylesheets = Array.Empty<ThemeStyleSheet> ();
                pending_stylesheet_notification = null;
            }
        }
    }
}
