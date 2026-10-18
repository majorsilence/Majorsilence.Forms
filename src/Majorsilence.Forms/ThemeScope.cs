using System.Collections.Concurrent;
using SkiaSharp;

namespace Majorsilence.Forms
{
    /// <summary>
    /// A theme for one part of the application: a window, or a control and everything inside it, while
    /// the rest of the application keeps the application-wide <see cref="Theme"/>.
    /// </summary>
    /// <remarks>
    /// <para>Attach a scope with <see cref="Control.ThemeScope"/> or <see cref="WindowBase.ThemeScope"/>.
    /// Child controls inherit it; a dialog inherits its owner's, and a drop-down, menu or tool tip the
    /// scope of the control it opened for.</para>
    /// <para>A scope overrides only the tokens it sets (<see cref="SetBuiltInTheme"/> and
    /// <see cref="ApplyStyleSheet"/> set all of them); the rest follow <see cref="Theme"/>, including later
    /// changes to it. Until the scope has a theme of its own, the application's CSS control rules apply in
    /// it too.</para>
    /// <para>While Majorsilence.Forms paints, lays out, measures or delivers input to a control, the
    /// control's scope is <see cref="Current"/>, and every <see cref="Theme"/> property reads through it --
    /// so custom painting code that reads <c>Theme.AccentColor</c> follows the scope with no changes. Code
    /// running outside those paths (a timer, a background task) sees the application theme; wrap it in
    /// <see cref="Enter"/> to read a scope's values.</para>
    /// </remarks>
    public sealed class ThemeScope
    {
        [ThreadStatic] private static ThemeScope? t_current;

        // Set once any scope is attached or entered, so an application that never uses scopes pays one
        // static read per push instead of a tree walk.
        private static volatile bool s_in_use;

        private static readonly List<WeakReference<ThemeScope>> s_live = new ();

        private readonly ConcurrentDictionary<string, object> values = new ();

        // The type-level styles' values under this scope; see ControlStyle.ScopedTwin.
        private readonly Dictionary<ControlStyle, ControlStyle> twins = new ();

        // The control rules of this scope's own stylesheet, by target style; null inherits the
        // application's rules, empty means none (a built-in theme).
        private Dictionary<ControlStyle, Action<ControlStyle>>? own_rules;
        private IReadOnlyList<ThemeStyleSheet> stylesheets = Array.Empty<ThemeStyleSheet> ();

        // The controls and windows this scope is attached to, to re-theme them when it changes.
        private readonly List<WeakReference<object>> roots = new ();

        private int suspend_count;
        private bool change_pending;

        /// <summary>Initializes a scope that sets nothing yet: everything follows <see cref="Theme"/>.</summary>
        public ThemeScope ()
        {
            lock (s_live)
                s_live.Add (new WeakReference<ThemeScope> (this));
        }

        /// <summary>
        /// The scope Majorsilence.Forms is currently painting, laying out or delivering input under on this
        /// thread, or null for the application theme.
        /// </summary>
        public static ThemeScope? Current => t_current;

        /// <summary>Raised after the scope's values change, once the controls using it have been re-themed.</summary>
        public event EventHandler? Changed;

        /// <summary>
        /// The CSS stylesheets applied with <see cref="ApplyStyleSheet"/>, base first; empty when the scope
        /// has none.
        /// </summary>
        public IReadOnlyList<ThemeStyleSheet> CurrentStyleSheets => stylesheets;

        /// <summary>
        /// Makes this scope <see cref="Current"/> until the result is disposed, for code that paints or
        /// measures outside Majorsilence.Forms' own paint, layout and input paths.
        /// </summary>
        public Entered Enter ()
        {
            s_in_use = true;
            return Push (this);
        }

        /// <summary>Restores the previous <see cref="Current"/> scope when disposed.</summary>
        public readonly struct Entered : IDisposable
        {
            private readonly ThemeScope? previous;
            private readonly bool active;

            internal Entered (ThemeScope? previous)
            {
                this.previous = previous;
                active = true;
            }

            /// <inheritdoc/>
            public void Dispose ()
            {
                if (active)
                    t_current = previous;
            }
        }

        internal static bool InUse => s_in_use;

        internal static void MarkInUse () => s_in_use = true;

        internal static Entered Push (ThemeScope? scope)
        {
            if (!s_in_use)
                return default;

            var previous = t_current;
            t_current = scope;
            return new Entered (previous);
        }

        /// <summary>Makes <paramref name="control"/>'s scope current; a no-op while no scope is in use.</summary>
        internal static Entered EnterFor (Control control) => s_in_use ? Push (control.ResolveThemeScope ()) : default;

        /// <summary>Makes <paramref name="window"/>'s scope current; a no-op while no scope is in use.</summary>
        internal static Entered EnterFor (WindowBase window) => s_in_use ? Push (window.ResolveThemeScope ()) : default;

        // --- tokens ------------------------------------------------------------------------------

        internal bool TryGetValue (string name, out object value) => values.TryGetValue (name, out value!);

        private T Get<T> (string name) => values.TryGetValue (name, out var v) ? (T)v : (T)Theme.GetGlobalValue (name);

        private void Set (string name, object value)
        {
            values[name] = value;
            OnChanged ();
        }

        /// <summary>True when this scope sets the token named by the <see cref="Theme"/> property <paramref name="propertyName"/>.</summary>
        public bool IsSet (string propertyName) => values.ContainsKey (propertyName);

        /// <summary>Stops overriding one token, e.g. <c>Reset (nameof (Theme.AccentColor))</c>; it follows <see cref="Theme"/> again.</summary>
        public void Reset (string propertyName)
        {
            if (values.TryRemove (propertyName, out _))
                OnChanged ();
        }

        /// <summary>Stops overriding anything, CSS control rules included: the scope follows <see cref="Theme"/> entirely.</summary>
        public void ResetAll ()
        {
            values.Clear ();
            own_rules = null;
            stylesheets = Array.Empty<ThemeStyleSheet> ();
            OnChanged ();
        }

        /// <summary>
        /// Sets every token to a built-in theme's, and turns off the application's CSS control rules in this
        /// scope -- the scoped equivalent of <see cref="Theme.SetBuiltInTheme"/>.
        /// </summary>
        public void SetBuiltInTheme (BuiltInTheme theme)
        {
            values.Clear ();
            Theme.FillBuiltInTheme (values, theme);
            own_rules = new Dictionary<ControlStyle, Action<ControlStyle>> ();
            stylesheets = Array.Empty<ThemeStyleSheet> ();
            OnChanged ();
        }

        /// <summary>Pauses <see cref="Changed"/> (and re-theming) while setting several tokens; resume with <see cref="EndUpdate"/>.</summary>
        public void BeginUpdate () => suspend_count++;

        /// <summary>Resumes after <see cref="BeginUpdate"/>, applying the changes made meanwhile once.</summary>
        public void EndUpdate ()
        {
            if (suspend_count > 0 && --suspend_count == 0 && change_pending)
                OnChanged ();
        }

        // --- CSS -------------------------------------------------------------------------------

        /// <summary>
        /// Parses a CSS theme (the language of docs/theming.md) and applies it to this scope.
        /// </summary>
        /// <exception cref="ThemeCssException">The stylesheet has errors. Nothing is applied.</exception>
        public void LoadFromCss (string css)
        {
            if (string.IsNullOrWhiteSpace (css))
                throw new ArgumentException ("Theme CSS cannot be null or empty.", nameof (css));

            var sheet = ThemeStyleSheet.Parse (css);

            if (sheet.HasErrors)
                throw new ThemeCssException (sheet.Diagnostics);

            ApplyStyleSheet (sheet);
        }

        /// <summary>
        /// Applies a parsed stylesheet to this scope: its <c>extends</c> base (a built-in theme or a CSS
        /// theme registered with <see cref="Theme.RegisterThemeCss"/>), its tokens, and its control rules,
        /// which replace the application's rules in this scope. Like <see cref="Theme.ApplyStyleSheet"/>,
        /// whatever parsed is applied even when the sheet has errors. Without <c>extends</c> the tokens
        /// layer onto the scope's current values.
        /// </summary>
        public void ApplyStyleSheet (ThemeStyleSheet sheet)
        {
            Guard.ThrowIfNull (sheet);

            var chain = new List<ThemeStyleSheet> ();
            var visiting = new HashSet<string> (StringComparer.OrdinalIgnoreCase);

            if (!string.IsNullOrEmpty (sheet.Name))
                visiting.Add (sheet.Name!);

            BeginUpdate ();
            try {
                ApplyLayer (sheet, visiting, chain);

                var rules = new Dictionary<ControlStyle, Action<ControlStyle>> ();

                foreach (var layer in chain)
                    foreach (var rule in layer.ControlRules) {
                        var target = rule.GetTargetStyle ();
                        rules[target] = rules.TryGetValue (target, out var before) ? before + rule.Apply : rule.Apply;
                    }

                own_rules = rules;
                stylesheets = chain.ToArray ();
                change_pending = true;
            } finally {
                EndUpdate ();
            }
        }

        private void ApplyLayer (ThemeStyleSheet sheet, HashSet<string> visiting, List<ThemeStyleSheet> chain)
        {
            if (!string.IsNullOrEmpty (sheet.BaseName)) {
                if (Enum.TryParse<BuiltInTheme> (sheet.BaseName, ignoreCase: true, out var builtIn)) {
                    values.Clear ();
                    Theme.FillBuiltInTheme (values, builtIn);
                } else {
                    if (Theme.GetRegisteredTheme (sheet.BaseName!) is not ThemeStyleSheet based)
                        throw new ThemeCssException ($"Base theme '{sheet.BaseName}' is not a built-in theme or a registered CSS theme. A ThemeScope cannot extend an XML theme.");

                    var name = based.Name ?? sheet.BaseName!;

                    if (!visiting.Add (name))
                        throw new ThemeCssException ($"Cyclic theme inheritance detected at '{name}'.");

                    ApplyLayer (based, visiting, chain);
                    visiting.Remove (name);
                }
            }

            // var(--token) references resolve against this scope, so the scope is current while they do
            using (Push (this))
                foreach (var declaration in sheet.TokenDeclarations)
                    values[declaration.Token.PropertyName] = declaration.Resolve ();

            chain.Add (sheet);
        }

        // --- styles ------------------------------------------------------------------------------

        /// <summary>The value bag that stands in for the type-level style <paramref name="original"/> under this scope.</summary>
        internal ControlStyle TwinOf (ControlStyle original)
        {
            if (twins.TryGetValue (original, out var twin))
                return twin;

            twin = ControlStyle.CreateScopedTwin (original);
            twins[original] = twin;
            Build (twin, original);
            return twin;
        }

        private void Build (ControlStyle twin, ControlStyle original)
        {
            Action<ControlStyle>? rules = own_rules is null
                ? original.StyleSheetRules
                : own_rules.TryGetValue (original, out var own) ? own : null;

            // The original's own values carry over (an application may set Button.DefaultStyle values
            // directly), except when they include application CSS this scope replaces with its own.
            bool copyOriginal = own_rules is null || original.StyleSheetRules is null;

            using (Push (this))
                twin.RebuildAsTwin (original, rules, copyOriginal);
        }

        private void RebuildTwins ()
        {
            foreach (var pair in twins.ToArray ())
                Build (pair.Value, pair.Key);
        }

        // --- attachment and change ---------------------------------------------------------------

        internal void Attach (object root)
        {
            s_in_use = true;
            lock (roots) {
                roots.RemoveAll (r => !r.TryGetTarget (out _));
                roots.Add (new WeakReference<object> (root));
            }
        }

        internal void Detach (object root)
        {
            lock (roots)
                roots.RemoveAll (r => !r.TryGetTarget (out var t) || ReferenceEquals (t, root));
        }

        private void OnChanged ()
        {
            if (suspend_count > 0) {
                change_pending = true;
                return;
            }

            change_pending = false;

            TextMeasurer.ClearTextBlockCache ();
            RebuildTwins ();

            object[] attached;
            lock (roots)
                attached = roots.Select (r => r.TryGetTarget (out var t) ? t : null).Where (t => t is not null).ToArray ()!;

            foreach (var root in attached)
                NotifyThemeChanged (root);

            Changed?.Invoke (this, EventArgs.Empty);
        }

        internal static void NotifyThemeChanged (object root)
        {
            switch (root) {
                case Form form:
                    form.OnThemeChanged (EventArgs.Empty);
                    break;
                case HostedSurface surface:
                    surface.OnThemeChanged (EventArgs.Empty);
                    break;
                case WindowBase window:
                    window.Invalidate ();
                    break;
                case Control control:
                    control.OnThemeChanged (EventArgs.Empty);
                    control.Invalidate ();
                    break;
            }
        }

        // The application theme changed: every scope's styles are rebuilt, since what a scope does not set
        // comes from it. The windows repaint through Application.DoThemeChanged as usual.
        internal static void RefreshAll ()
        {
            ThemeScope[] live;

            lock (s_live) {
                s_live.RemoveAll (r => !r.TryGetTarget (out _));
                live = s_live.Select (r => r.TryGetTarget (out var s) ? s : null).Where (s => s is not null).ToArray ()!;
            }

            foreach (var scope in live)
                scope.RebuildTwins ();
        }

        // --- typed tokens (one per Theme property) -----------------------------------------------

        /// <summary>See <see cref="Theme.AccentColor"/>.</summary>
        public SKColor AccentColor { get => Get<SKColor> (nameof (AccentColor)); set => Set (nameof (AccentColor), value); }

        /// <summary>See <see cref="Theme.AccentColor2"/>.</summary>
        public SKColor AccentColor2 { get => Get<SKColor> (nameof (AccentColor2)); set => Set (nameof (AccentColor2), value); }

        /// <summary>See <see cref="Theme.BackgroundColor"/>.</summary>
        public SKColor BackgroundColor { get => Get<SKColor> (nameof (BackgroundColor)); set => Set (nameof (BackgroundColor), value); }

        /// <summary>See <see cref="Theme.BorderLowColor"/>.</summary>
        public SKColor BorderLowColor { get => Get<SKColor> (nameof (BorderLowColor)); set => Set (nameof (BorderLowColor), value); }

        /// <summary>See <see cref="Theme.BorderMidColor"/>.</summary>
        public SKColor BorderMidColor { get => Get<SKColor> (nameof (BorderMidColor)); set => Set (nameof (BorderMidColor), value); }

        /// <summary>See <see cref="Theme.BorderHighColor"/>.</summary>
        public SKColor BorderHighColor { get => Get<SKColor> (nameof (BorderHighColor)); set => Set (nameof (BorderHighColor), value); }

        /// <summary>See <see cref="Theme.ControlLowColor"/>.</summary>
        public SKColor ControlLowColor { get => Get<SKColor> (nameof (ControlLowColor)); set => Set (nameof (ControlLowColor), value); }

        /// <summary>See <see cref="Theme.ControlMidColor"/>.</summary>
        public SKColor ControlMidColor { get => Get<SKColor> (nameof (ControlMidColor)); set => Set (nameof (ControlMidColor), value); }

        /// <summary>See <see cref="Theme.ControlMidHighColor"/>.</summary>
        public SKColor ControlMidHighColor { get => Get<SKColor> (nameof (ControlMidHighColor)); set => Set (nameof (ControlMidHighColor), value); }

        /// <summary>See <see cref="Theme.ControlHighColor"/>.</summary>
        public SKColor ControlHighColor { get => Get<SKColor> (nameof (ControlHighColor)); set => Set (nameof (ControlHighColor), value); }

        /// <summary>See <see cref="Theme.ControlVeryHighColor"/>.</summary>
        public SKColor ControlVeryHighColor { get => Get<SKColor> (nameof (ControlVeryHighColor)); set => Set (nameof (ControlVeryHighColor), value); }

        /// <summary>See <see cref="Theme.ControlHighlightLowColor"/>.</summary>
        public SKColor ControlHighlightLowColor { get => Get<SKColor> (nameof (ControlHighlightLowColor)); set => Set (nameof (ControlHighlightLowColor), value); }

        /// <summary>See <see cref="Theme.ControlHighlightMidColor"/>.</summary>
        public SKColor ControlHighlightMidColor { get => Get<SKColor> (nameof (ControlHighlightMidColor)); set => Set (nameof (ControlHighlightMidColor), value); }

        /// <summary>See <see cref="Theme.ControlHighlightHighColor"/>.</summary>
        public SKColor ControlHighlightHighColor { get => Get<SKColor> (nameof (ControlHighlightHighColor)); set => Set (nameof (ControlHighlightHighColor), value); }

        /// <summary>See <see cref="Theme.FontSize"/>.</summary>
        public int FontSize { get => Get<int> (nameof (FontSize)); set => Set (nameof (FontSize), value); }

        /// <summary>See <see cref="Theme.ForegroundColor"/>.</summary>
        public SKColor ForegroundColor { get => Get<SKColor> (nameof (ForegroundColor)); set => Set (nameof (ForegroundColor), value); }

        /// <summary>See <see cref="Theme.ForegroundColorOnAccent"/>.</summary>
        public SKColor ForegroundColorOnAccent { get => Get<SKColor> (nameof (ForegroundColorOnAccent)); set => Set (nameof (ForegroundColorOnAccent), value); }

        /// <summary>See <see cref="Theme.ForegroundDisabledColor"/>.</summary>
        public SKColor ForegroundDisabledColor { get => Get<SKColor> (nameof (ForegroundDisabledColor)); set => Set (nameof (ForegroundDisabledColor), value); }

        /// <summary>See <see cref="Theme.ItemFontSize"/>.</summary>
        public int ItemFontSize { get => Get<int> (nameof (ItemFontSize)); set => Set (nameof (ItemFontSize), value); }

        /// <summary>See <see cref="Theme.RootFontSize"/>.</summary>
        public int RootFontSize { get => Get<int> (nameof (RootFontSize)); set => Set (nameof (RootFontSize), value); }

        /// <summary>See <see cref="Theme.TextSelectionBackgroundColor"/>.</summary>
        public SKColor TextSelectionBackgroundColor { get => Get<SKColor> (nameof (TextSelectionBackgroundColor)); set => Set (nameof (TextSelectionBackgroundColor), value); }

        /// <summary>See <see cref="Theme.WarningHighlightColor"/>.</summary>
        public SKColor WarningHighlightColor { get => Get<SKColor> (nameof (WarningHighlightColor)); set => Set (nameof (WarningHighlightColor), value); }

        /// <summary>See <see cref="Theme.UIFont"/>. A typeface the system cannot find by name is registered privately, as there.</summary>
        public SKTypeface UIFont { get => Get<SKTypeface> (nameof (UIFont)); set => Set (nameof (UIFont), Theme.RegisterScopedFont (value)); }

        /// <summary>See <see cref="Theme.UIFontBold"/>.</summary>
        public SKTypeface UIFontBold { get => Get<SKTypeface> (nameof (UIFontBold)); set => Set (nameof (UIFontBold), Theme.RegisterScopedFont (value)); }
    }
}
