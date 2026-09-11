using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using WF = System.Windows.Forms;

namespace Majorsilence.Forms.Theming.WinForms
{
    /// <summary>
    /// Applies a Majorsilence.Forms CSS theme (<c>docs/theming.md</c>) to real System.Windows.Forms
    /// controls, so a mixed migration app — Majorsilence.Forms controls embedded in a WinForms shell, or
    /// WinForms controls hosted in Majorsilence.Forms — is themed from ONE stylesheet: same tokens, same
    /// selectors, same diagnostics.
    /// <para>
    /// WinForms has no styling system to hook, only per-control properties and a few renderer seams, so
    /// this walks the control tree of every open (and <see cref="Track">tracked</see>) form and sets
    /// properties: colours and fonts everywhere, <c>FlatAppearance</c> on buttons, cell styles on
    /// <c>DataGridView</c>, a <c>ToolStripProfessionalRenderer</c> built from the tokens for every strip
    /// and drop-down, the Windows 11 title bar via DWM. What WinForms cannot express is reported in
    /// <see cref="Diagnostics"/> (info for approximations and selectors with no counterpart, warning
    /// for unsupported properties) and skipped — never silently ignored. The full property × control
    /// matrix is <see cref="WinFormsThemeSupport"/> / <c>docs/theming-winforms.md</c>.
    /// </para>
    /// <para>
    /// A control whose <c>BackColor</c>, <c>ForeColor</c> or <c>Font</c> the app set explicitly keeps
    /// it (mirroring "explicit per-control values win" on the Majorsilence.Forms side); the applier
    /// only writes properties it owns.
    /// </para>
    /// <para>
    /// Every apply also goes through <see cref="Theme.ApplyStyleSheet"/>, so embedded Majorsilence.Forms
    /// surfaces and native controls change together; a sheet applied from the Majorsilence.Forms side
    /// (<see cref="Theme.LoadFromCss"/>, <see cref="Theme.ApplyTheme"/>) is mirrored onto WinForms via
    /// <see cref="Theme.StyleSheetApplied"/> once any member of this class has been used.
    /// </para>
    /// </summary>
    public static class WinFormsCssTheme
    {
        private static readonly object sync = new ();
        private static readonly List<WeakReference<WF.Form>> tracked_forms = new ();
        private static bool subscribed;
        private static bool applying_from_here;
        private static IReadOnlyList<ThemeCssDiagnostic> diagnostics = Array.Empty<ThemeCssDiagnostic> ();

        /// <summary>
        /// The property × control support matrix: for every selector and property the stylesheet
        /// grammar accepts, whether real WinForms expresses it natively, approximately or not at all.
        /// The same data drives <see cref="Diagnostics"/>. See also <see cref="WinFormsThemeSupport"/>
        /// for lookups and the Markdown rendering.
        /// </summary>
        public static IReadOnlyList<WinFormsThemeSupportEntry> Support => WinFormsThemeSupport.Entries;

        /// <summary>
        /// The WinForms-side diagnostics of the last apply: selectors with no WinForms counterpart and
        /// properties that apply approximately (<see cref="ThemeCssSeverity.Info"/>), properties WinForms
        /// cannot express (<see cref="ThemeCssSeverity.Warning"/>), and process-wide switches that came
        /// too late (info). Parse problems are on the sheet itself (<see cref="ThemeStyleSheet.Diagnostics"/>).
        /// </summary>
        public static IReadOnlyList<ThemeCssDiagnostic> Diagnostics {
            get {
                lock (sync)
                    return diagnostics;
            }
        }

        /// <summary>
        /// Raised after each apply, on the thread that applied. The Theme Studio uses it to refresh
        /// its diagnostics list; <see cref="Diagnostics"/> is current when it fires.
        /// </summary>
        public static event EventHandler? Applied;

        /// <summary>
        /// Parses a CSS theme and applies it to the Majorsilence.Forms theme and to every open and
        /// tracked WinForms form. Forms opened later must be passed to <see cref="Track"/> (or be open
        /// at the next apply).
        /// </summary>
        /// <exception cref="ThemeCssException">The stylesheet has errors; nothing is applied. See its
        /// <see cref="ThemeCssException.Diagnostics"/> for every problem found.</exception>
        public static void Apply (string css)
        {
            if (string.IsNullOrWhiteSpace (css))
                throw new ArgumentException ("Theme CSS cannot be null or empty.", nameof (css));

            var sheet = ThemeStyleSheet.Parse (css);

            if (sheet.HasErrors)
                throw new ThemeCssException (sheet.Diagnostics);

            Apply (sheet);
        }

        /// <summary>
        /// Applies an already parsed stylesheet. Like <see cref="Theme.ApplyStyleSheet"/> this does not
        /// refuse a sheet with errors — whatever parsed is applied, which is what a live editor wants
        /// while the author is mid-edit.
        /// </summary>
        public static void Apply (ThemeStyleSheet sheet)
        {
            ArgumentNullException.ThrowIfNull (sheet);

            EnsureSubscribed ();

            lock (sync)
                applying_from_here = true;

            try {
                Theme.ApplyStyleSheet (sheet);
            } finally {
                lock (sync)
                    applying_from_here = false;
            }

            ApplyChain (Theme.CurrentStyleSheets);
        }

        /// <summary>
        /// Applies the stylesheet at <paramref name="path"/> now and re-applies it whenever the file
        /// changes — the Theme Studio workflow against a real WinForms app. Parse errors do not stop
        /// the watch: the valid remainder is applied and the problems are on the returned watcher's
        /// <see cref="WinFormsCssThemeWatcher.SheetDiagnostics"/>. Dispose the result to stop watching.
        /// </summary>
        public static WinFormsCssThemeWatcher Watch (string path)
        {
            if (string.IsNullOrWhiteSpace (path))
                throw new ArgumentException ("Path cannot be null or empty.", nameof (path));

            EnsureSubscribed ();
            return new WinFormsCssThemeWatcher (Path.GetFullPath (path));
        }

        /// <summary>
        /// Styles a form now (if a theme has been applied) and keeps styling it: controls added to it or
        /// to any of its containers later are themed as they arrive, and its title bar is themed when
        /// its handle is created. Forms open at the time of an <see cref="Apply(ThemeStyleSheet)"/> are
        /// tracked automatically; call this for forms you construct afterwards, before showing them.
        /// </summary>
        public static void Track (WF.Form form)
        {
            ArgumentNullException.ThrowIfNull (form);

            EnsureSubscribed ();
            Register (form);
            WinFormsThemeApplier.ApplyTree (form);
        }

        private static void Register (WF.Form form)
        {
            lock (sync) {
                tracked_forms.RemoveAll (w => !w.TryGetTarget (out var f) || f.IsDisposed);
                if (!tracked_forms.Any (w => w.TryGetTarget (out var f) && ReferenceEquals (f, form)))
                    tracked_forms.Add (new WeakReference<WF.Form> (form));
            }
        }

        // ---- internals ------------------------------------------------------------------------

        private static void EnsureSubscribed ()
        {
            lock (sync) {
                if (subscribed)
                    return;
                subscribed = true;
            }

            Theme.StyleSheetApplied += (_, e) => {
                bool skip;
                lock (sync)
                    skip = applying_from_here;

                if (!skip)
                    ApplyChain (e.Chain);
            };
        }

        // Applies an already-installed chain (Theme.* already holds the resolved tokens) to WinForms.
        internal static void ApplyChain (IReadOnlyList<ThemeStyleSheet> chain)
        {
            RunOnUiThread (() => {
                var tokens = TokenSnapshot.Read ();
                var rules = ThemeRuleSet.Build (chain);
                var strips = StripColors.Resolve (rules, tokens);
                var context = new ApplyContext (rules, tokens, strips);
                var found = BuildDiagnostics (chain);

                WinFormsThemeApplier.Current = context;
                WF.ToolStripManager.Renderer = new ThemeToolStripRenderer (strips);

                ApplyProcessWide (context, chain, found);

                foreach (var form in WF.Application.OpenForms.Cast<WF.Form> ().ToArray ())
                    Register (form);

                List<WF.Form> forms;
                lock (sync) {
                    tracked_forms.RemoveAll (w => !w.TryGetTarget (out var f) || f.IsDisposed);
                    forms = tracked_forms.Select (w => w.TryGetTarget (out var f) ? f : null).Where (f => f is not null).ToList ()!;
                }

                foreach (var form in forms)
                    WinFormsThemeApplier.ApplyTree (form);

                lock (sync)
                    diagnostics = found;

                Applied?.Invoke (null, EventArgs.Empty);
            });
        }

        // The two process-wide switches WinForms has. Both only take effect before the first window
        // exists, so a late apply reports that instead of failing.
        private static void ApplyProcessWide (ApplyContext ctx, IReadOnlyList<ThemeStyleSheet> chain, List<ThemeCssDiagnostic> found)
        {
            var formFont = ctx.Rules.Font ("Form");
            var sheetSetsFontTokens = chain.SelectMany (s => s.Tokens).Any (t => t.Token.PropertyName is nameof (Theme.UIFont) or nameof (Theme.FontSize));

            if (!formFont.IsEmpty || sheetSetsFontTokens) {
                var spec = formFont.IsEmpty
                    ? new FontSpec { Families = new[] { ctx.Tokens.UIFontFamily }, SizePixels = ctx.Tokens.FontSize }
                    : formFont;

                try {
                    WF.Application.SetDefaultFont (WinFormsThemeApplier.BuildFont (WF.Control.DefaultFont, spec));
                } catch (InvalidOperationException) {
                    found.Add (new ThemeCssDiagnostic (ThemeCssSeverity.Info, 0, 0,
                        "Application.SetDefaultFont can only run before the first window is created; the process default font is unchanged. Open forms still get the Form { font-family / font-size } rule per control, and controls that inherit their font follow."));
                }
            }

#if NET9_0_OR_GREATER
            try {
#pragma warning disable WFO5001 // SetColorMode is experimental in WinForms 9/10 and documented as such in the matrix.
                WF.Application.SetColorMode (ctx.Tokens.IsDark ? WF.SystemColorMode.Dark : WF.SystemColorMode.Classic);
#pragma warning restore WFO5001
            } catch (InvalidOperationException) {
                found.Add (new ThemeCssDiagnostic (ThemeCssSeverity.Info, 0, 0,
                    "Application.SetColorMode can only run before the first window is created; SystemColors keep the mode the process started with. Explicit colours from the theme still apply."));
            }
#endif
        }

        // Turns the support matrix into per-declaration diagnostics for the sheets actually applied.
        private static List<ThemeCssDiagnostic> BuildDiagnostics (IReadOnlyList<ThemeStyleSheet> chain)
        {
            var found = new List<ThemeCssDiagnostic> ();
            var seen = new HashSet<string> (StringComparer.Ordinal);

            foreach (var sheet in chain)
                foreach (var rule in sheet.Rules) {
                    var mapping = WinFormsThemeSupport.FindMapping (rule.Selector.Name);
                    if (mapping is null)
                        continue;

                    var ruleText = rule.Selector.Name + (rule.Part is null ? "" : "::" + rule.Part.Name) + (rule.Hover ? ":hover" : "");

                    if (mapping.WinFormsTypes is null) {
                        if (seen.Add ("selector:" + rule.Selector.Name))
                            found.Add (new ThemeCssDiagnostic (ThemeCssSeverity.Info, rule.Line, rule.Column,
                                $"'{rule.Selector.Name}' has no System.Windows.Forms counterpart; the rule styles Majorsilence.Forms controls only. {mapping.Notes}"));
                        continue;
                    }

                    foreach (var declaration in rule.Declarations) {
                        var entry = WinFormsThemeSupport.Find (rule.Selector.Name, rule.Part?.Name, rule.Hover, declaration.Property);
                        var level = entry?.Level ?? WinFormsThemeSupportLevel.Unsupported;

                        if (level == WinFormsThemeSupportLevel.Native || !seen.Add (ruleText + "|" + declaration.Property))
                            continue;

                        var notes = entry?.Notes ?? "WinForms has no property for it.";

                        found.Add (level == WinFormsThemeSupportLevel.Approximate
                            ? new ThemeCssDiagnostic (ThemeCssSeverity.Info, declaration.Line, declaration.Column,
                                $"'{ruleText} {{ {declaration.Property} }}' applies approximately on WinForms ({mapping.WinFormsTypes}): {notes}")
                            : new ThemeCssDiagnostic (ThemeCssSeverity.Warning, declaration.Line, declaration.Column,
                                $"'{ruleText} {{ {declaration.Property} }}' is not supported on WinForms ({mapping.WinFormsTypes}) and was skipped there: {notes} It still applies to Majorsilence.Forms controls."));
                    }
                }

            return found;
        }

        // WinForms properties must be set on the UI thread. A file watcher or a cross-thread
        // Theme.LoadFromCss lands here; if any tracked form has a handle, marshal through it.
        private static void RunOnUiThread (Action action)
        {
            WF.Form? target = null;

            lock (sync) {
                foreach (var weak in tracked_forms)
                    if (weak.TryGetTarget (out var f) && !f.IsDisposed && f.IsHandleCreated) {
                        target = f;
                        break;
                    }
            }

            if (target is null && WF.Application.OpenForms.Count > 0)
                target = WF.Application.OpenForms[0];

            if (target is { IsHandleCreated: true } && target.InvokeRequired)
                target.BeginInvoke (action);
            else
                action ();
        }
    }

    /// <summary>
    /// A live re-apply of one stylesheet file, returned by <see cref="WinFormsCssTheme.Watch"/>.
    /// Dispose it to stop watching.
    /// </summary>
    public sealed class WinFormsCssThemeWatcher : IDisposable
    {
        private readonly FileSystemWatcher watcher;
        private readonly System.Threading.Timer debounce;
        private IReadOnlyList<ThemeCssDiagnostic> sheet_diagnostics = Array.Empty<ThemeCssDiagnostic> ();
        private bool disposed;

        internal WinFormsCssThemeWatcher (string path)
        {
            Path = path;

            debounce = new System.Threading.Timer (_ => Reload (), null, Timeout.Infinite, Timeout.Infinite);

            watcher = new FileSystemWatcher (System.IO.Path.GetDirectoryName (path)!, System.IO.Path.GetFileName (path)) {
                NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.FileName | NotifyFilters.Size | NotifyFilters.CreationTime,
            };
            watcher.Changed += (_, _) => Schedule ();
            watcher.Created += (_, _) => Schedule ();
            watcher.Renamed += (_, _) => Schedule ();
            watcher.EnableRaisingEvents = true;

            Reload ();
        }

        /// <summary>The full path of the watched stylesheet.</summary>
        public string Path { get; }

        /// <summary>The parse diagnostics of the last load (errors mark declarations that were dropped).</summary>
        public IReadOnlyList<ThemeCssDiagnostic> SheetDiagnostics => sheet_diagnostics;

        /// <summary>Raised after every (re)load, once the sheet has been applied.</summary>
        public event EventHandler? Reloaded;

        /// <summary>How many times the file has been loaded, the initial load included.</summary>
        public int LoadCount { get; private set; }

        // Editors save in several writes; collapse them into one apply.
        private void Schedule ()
        {
            if (!disposed)
                debounce.Change (150, Timeout.Infinite);
        }

        private void Reload ()
        {
            if (disposed)
                return;

            string? css = null;

            // The editor may still hold the file open; a few short retries cover that.
            for (var attempt = 0; attempt < 5 && css is null; attempt++) {
                try {
                    css = File.ReadAllText (Path);
                } catch (IOException) {
                    Thread.Sleep (40);
                }
            }

            if (css is null)
                return;

            var sheet = ThemeStyleSheet.Parse (css);
            sheet_diagnostics = sheet.Diagnostics;

            WinFormsCssTheme.Apply (sheet);
            LoadCount++;
            Reloaded?.Invoke (this, EventArgs.Empty);
        }

        /// <inheritdoc/>
        public void Dispose ()
        {
            if (disposed)
                return;

            disposed = true;
            watcher.EnableRaisingEvents = false;
            watcher.Dispose ();
            debounce.Dispose ();
        }
    }
}
