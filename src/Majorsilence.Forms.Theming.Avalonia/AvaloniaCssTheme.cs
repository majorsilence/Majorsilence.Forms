using System.Collections.Generic;
using System.IO;
using System.Threading;

namespace Majorsilence.Forms.Theming.Avalonia
{
    using global::Avalonia;
    using global::Avalonia.Controls;
    using global::Avalonia.Styling;
    using global::Avalonia.Threading;

    /// <summary>
    /// Applies a Majorsilence.Forms CSS theme (<c>docs/theming.md</c>) to native Avalonia controls, so an
    /// app that mixes Avalonia views with Majorsilence.Forms surfaces — or an Avalonia shell next to a
    /// WinForms client themed by <c>Majorsilence.Forms.Theming.WinForms</c> — is themed from ONE
    /// stylesheet: same tokens, same selectors, same diagnostics.
    /// <para>
    /// Avalonia's Fluent theme reads its colours from named resources, so colours are written as those
    /// resources (<c>ButtonBackgroundPointerOver</c>, <c>TabItemHeaderSelectedPipeFill</c>, ...) into a
    /// dictionary merged into <see cref="Application.Resources"/> — which restyles every state that
    /// uses them, hover and selection included. Geometry and fonts are setters in a generated
    /// <see cref="Styles"/> appended to <see cref="Application.Styles"/>. What Avalonia cannot express is
    /// reported in <see cref="Diagnostics"/> (info for approximations and selectors with no counterpart,
    /// warning for unsupported properties) and skipped — never silently ignored. The full property ×
    /// control matrix is <see cref="AvaloniaThemeSupport"/> / <c>docs/theming-avalonia.md</c>.
    /// </para>
    /// <para>
    /// Every theme token and every author variable of the sheet is also published as a resource under
    /// its PascalCase name (<c>--line2</c> → <c>Line2</c> brush + <c>Line2Color</c>;
    /// <c>--accent-color</c> → <c>AccentColor</c> + <c>AccentBrush</c>), so the app's own views can bind
    /// <c>{DynamicResource Line2}</c> and follow the stylesheet.
    /// </para>
    /// <para>
    /// Resources and styles the app declares itself <em>directly</em> in <c>Application.Resources</c> /
    /// after the generated styles win, as they should. Every apply also goes through
    /// <see cref="Theme.ApplyStyleSheet"/>, so embedded Majorsilence.Forms surfaces change together; a
    /// sheet applied from the Majorsilence.Forms side is mirrored onto Avalonia via
    /// <see cref="Theme.StyleSheetApplied"/> once any member of this class has been used.
    /// </para>
    /// </summary>
    public static class AvaloniaCssTheme
    {
        private static readonly object sync = new ();
        private static bool subscribed;
        private static bool applying_from_here;
        private static IReadOnlyList<ThemeCssDiagnostic> diagnostics = Array.Empty<ThemeCssDiagnostic> ();

        // The two things installed into the application, replaced in place on every apply.
        private static ResourceDictionary? installed_resources;
        private static Styles? installed_styles;

        /// <summary>
        /// The property × control support matrix: for every selector and property the stylesheet grammar
        /// accepts, whether Avalonia expresses it natively, approximately or not at all, and which
        /// resources or style setters it writes. See <see cref="AvaloniaThemeSupport"/>.
        /// </summary>
        public static IReadOnlyList<AvaloniaThemeSupportEntry> Support => AvaloniaThemeSupport.Entries;

        /// <summary>
        /// The Avalonia-side diagnostics of the last apply: selectors with no Avalonia counterpart and
        /// properties that apply approximately (<see cref="ThemeCssSeverity.Info"/>), and properties
        /// Avalonia cannot express (<see cref="ThemeCssSeverity.Warning"/>). Parse problems are on the
        /// sheet itself (<see cref="ThemeStyleSheet.Diagnostics"/>).
        /// </summary>
        public static IReadOnlyList<ThemeCssDiagnostic> Diagnostics {
            get {
                lock (sync)
                    return diagnostics;
            }
        }

        /// <summary>
        /// Whether an apply also sets <see cref="Application.RequestedThemeVariant"/> to Light or Dark from
        /// the theme's background luminance, so Fluent's own light/dark resources match the sheet (the
        /// counterpart of WinForms' <c>SetColorMode</c>). Default true; set false when the app manages
        /// the variant itself.
        /// </summary>
        public static bool SetThemeVariant { get; set; } = true;

        /// <summary>Raised after each apply, on the UI thread. <see cref="Diagnostics"/> is current when it fires.</summary>
        public static event EventHandler? Applied;

        /// <summary>
        /// Parses a CSS theme and applies it to the Majorsilence.Forms theme and to the current Avalonia
        /// application. Call it once the application exists (for example in
        /// <c>OnFrameworkInitializationCompleted</c>, or after <c>AppBuilder.SetupWithoutStarting</c>).
        /// </summary>
        /// <exception cref="ThemeCssException">The stylesheet has errors; nothing is applied. See its
        /// <see cref="ThemeCssException.Diagnostics"/> for every problem found.</exception>
        /// <exception cref="InvalidOperationException">There is no current Avalonia application.</exception>
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
        /// <exception cref="InvalidOperationException">There is no current Avalonia application.</exception>
        public static void Apply (ThemeStyleSheet sheet)
        {
            ArgumentNullException.ThrowIfNull (sheet);

            if (Application.Current is null)
                throw new InvalidOperationException ("AvaloniaCssTheme needs a running Avalonia application: apply the theme from Application.OnFrameworkInitializationCompleted or later.");

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
        /// changes. Parse errors do not stop the watch: the valid remainder is applied and the problems
        /// are on the returned watcher's <see cref="AvaloniaCssThemeWatcher.SheetDiagnostics"/>. Dispose
        /// the result to stop watching.
        /// </summary>
        public static AvaloniaCssThemeWatcher Watch (string path)
        {
            if (string.IsNullOrWhiteSpace (path))
                throw new ArgumentException ("Path cannot be null or empty.", nameof (path));

            EnsureSubscribed ();
            return new AvaloniaCssThemeWatcher (Path.GetFullPath (path));
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

                if (!skip && Application.Current is not null)
                    ApplyChain (e.Chain);
            };
        }

        // Applies an already-installed chain (Theme.* already holds the resolved tokens) to Avalonia.
        internal static void ApplyChain (IReadOnlyList<ThemeStyleSheet> chain)
        {
            if (Dispatcher.UIThread.CheckAccess ())
                Install (AvaloniaThemeApplier.Build (chain));
            else
                Dispatcher.UIThread.Post (() => Install (AvaloniaThemeApplier.Build (chain)));
        }

        private static void Install (AvaloniaThemeBuild build)
        {
            var app = Application.Current;
            if (app is null)
                return;

            // Replace the dictionary, not its contents: removing then adding a merged dictionary raises
            // one ResourcesChanged, so every {DynamicResource} re-resolves once. It goes last, which
            // is searched first among the merged dictionaries.
            if (installed_resources is not null)
                app.Resources.MergedDictionaries.Remove (installed_resources);
            installed_resources = build.Resources;
            app.Resources.MergedDictionaries.Add (installed_resources);

            // Likewise swap the Styles object at the same position rather than clearing it: a Styles
            // that is cleared and refilled while attached is not re-applied to controls created later.
            var styles = new Styles ();
            styles.AddRange (build.Styles);
            var position = installed_styles is null ? -1 : app.Styles.IndexOf (installed_styles);
            if (position >= 0)
                app.Styles[position] = styles;
            else
                app.Styles.Add (styles);
            installed_styles = styles;

            if (SetThemeVariant)
                app.RequestedThemeVariant = build.IsDark ? ThemeVariant.Dark : ThemeVariant.Light;

            lock (sync)
                diagnostics = build.Diagnostics;

            Applied?.Invoke (null, EventArgs.Empty);
        }
    }

    /// <summary>
    /// A live re-apply of one stylesheet file, returned by <see cref="AvaloniaCssTheme.Watch"/>.
    /// Dispose it to stop watching.
    /// </summary>
    public sealed class AvaloniaCssThemeWatcher : IDisposable
    {
        private readonly FileSystemWatcher watcher;
        private readonly System.Threading.Timer debounce;
        private IReadOnlyList<ThemeCssDiagnostic> sheet_diagnostics = Array.Empty<ThemeCssDiagnostic> ();
        private bool disposed;

        internal AvaloniaCssThemeWatcher (string path)
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

        /// <summary>Raised after every (re)load, once the sheet has been applied (on the UI thread for reloads).</summary>
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

            void ApplyNow ()
            {
                AvaloniaCssTheme.Apply (sheet);
                LoadCount++;
                Reloaded?.Invoke (this, EventArgs.Empty);
            }

            if (Dispatcher.UIThread.CheckAccess ())
                ApplyNow ();
            else
                Dispatcher.UIThread.Post (ApplyNow);
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
