using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Majorsilence.Forms;
using SkiaSharp;

namespace ThemeStudio
{
    // The Studio window: a CSS editor on the left, the control preview on the right, and the parser's
    // diagnostics under the editor. Every edit re-parses and re-applies the sheet after a short pause;
    // an opened file is also watched on disk so an external editor (or an assistant) drives the
    // preview too. The window itself is themed by the sheet -- that is the point -- except the editor,
    // whose colours are pinned so a theme that goes wrong cannot make the text used to fix it unreadable.
    public class MainForm : Form
    {
        private const int DebounceMilliseconds = 250;

        private readonly TextBox editor;
        private readonly ListBox diagnostics;
        private readonly Label status;
        private readonly PreviewPanel preview;
        private readonly Majorsilence.Forms.Timer debounce = new () { Interval = DebounceMilliseconds };

        private string? file_path;
        private FileSystemWatcher? watcher;
        private string last_applied = string.Empty;
        private bool suppress_editor_events;

        public MainForm (string? path)
        {
            Text = "Theme Studio";
            Size = new System.Drawing.Size (1280, 860);
            StartPosition = FormStartPosition.CenterScreen;

            // Docking resolves in reverse z-order (the last control added docks first), so the Fill
            // control goes in before the strips that dock around it -- here and in every pane below.
            var split = Controls.Add (new SplitContainer { Dock = DockStyle.Fill, FixedPanel = FixedPanel.Panel1, Panel1MinimumSize = 320, Panel2MinimumSize = 400 });

            // SplitterDistance is clamped against the container's current size, and the container has
            // no real size until the first layout pass docks it, so set it the first time it is sized.
            EventHandler? sized = null;
            sized = (_, _) => {
                if (split.Width < 800)
                    return;
                split.SplitterDistance = 460;
                split.SizeChanged -= sized;
            };
            split.SizeChanged += sized;

            BuildMenu ();

            // --- left: editor, diagnostics, status ---
            editor = split.Panel1.Controls.Add (new TextBox {
                Dock = DockStyle.Fill,
                Multiline = true,
                WordWrap = false,
                AcceptsReturn = true,
                AcceptsTab = true,
                ScrollBars = ScrollBars.Both,
            });

            var toolbar = split.Panel1.Controls.Add (new Panel { Dock = DockStyle.Top, Height = 34 });
            var open = toolbar.Controls.Add (new Button { Text = "Open…", Left = 4, Top = 3, Width = 80, Height = 28 });
            var save = toolbar.Controls.Add (new Button { Text = "Save", Left = 88, Top = 3, Width = 80, Height = 28 });
            var copyRef = toolbar.Controls.Add (new Button { Text = "Copy reference for AI", Left = 172, Top = 3, Width = 170, Height = 28 });
            var apply = toolbar.Controls.Add (new Button { Text = "Apply", Left = 346, Top = 3, Width = 80, Height = 28 });
            open.Click += async (_, _) => await OpenAsync ();
            save.Click += async (_, _) => await SaveAsync (saveAs: false);
            copyRef.Click += (_, _) => CopyReference ();
            apply.Click += (_, _) => ApplyEditorText ();

            diagnostics = split.Panel1.Controls.Add (new ListBox { Dock = DockStyle.Bottom, Height = 150 });
            diagnostics.Style.Border.Width = 0;
            diagnostics.Style.Border.Top.Width = 1;

            status = split.Panel1.Controls.Add (new Label { Dock = DockStyle.Bottom, Height = 26, Text = "Ready", Padding = new Padding (6, 0, 0, 0) });

            PinEditorStyle ();
            editor.TextChanged += (_, _) => {
                if (suppress_editor_events)
                    return;
                debounce.Stop ();
                debounce.Start ();
            };

            debounce.Tick += (_, _) => {
                debounce.Stop ();
                ApplyEditorText ();
            };

            // --- right: the preview ---
            preview = split.Panel2.Controls.Add (new PreviewPanel { Dock = DockStyle.Fill });

            if (path is not null && File.Exists (path))
                LoadFile (path);
            else
                SetEditorText (Theme.ExportCss ("MyTheme", "Light"), apply: true);
        }

        /// <summary>Shows one of the preview tabs; used by the headless renderer's <c>--tab</c> switch.</summary>
        public void SelectPreviewTab (int index) => preview.SelectTab (index);

        /// <summary>The diagnostics of the last applied sheet (for the headless renderer's report).</summary>
        public IReadOnlyList<ThemeCssDiagnostic> LastDiagnostics { get; private set; } = Array.Empty<ThemeCssDiagnostic> ();

        /// <summary>Whether the last applied sheet had errors.</summary>
        public bool LastHadErrors { get; private set; }

        private void BuildMenu ()
        {
            var menu = new Menu ();

            var file = menu.Items.Add ("File");
            var newFrom = file.Items.Add ("New from built-in theme");
            foreach (var builtIn in Enum.GetValues<BuiltInTheme> ()) {
                if (builtIn == BuiltInTheme.Default)
                    continue;
                var captured = builtIn;
                newFrom.Items.Add (captured.ToString (), null, (_, _) => NewFrom (captured));
            }
            file.Items.Add ("Open…", null, async (_, _) => await OpenAsync ());
            file.Items.Add ("Save", null, async (_, _) => await SaveAsync (saveAs: false));
            file.Items.Add ("Save As…", null, async (_, _) => await SaveAsync (saveAs: true));
            file.Items.Add (new MenuSeparatorItem ());
            file.Items.Add ("Exit", null, (_, _) => Close ());

            var help = menu.Items.Add ("Help");
            help.Items.Add ("Copy reference for AI (Markdown + prompt)", null, (_, _) => CopyReference ());
            help.Items.Add ("Copy current theme as CSS", null, (_, _) => Clipboard.SetText (Theme.ExportCss ("MyTheme")));
            help.Items.Add ("Insert example: rounded accent buttons", null, (_, _) => AppendToEditor (Examples.RoundedButtons));
            help.Items.Add ("Insert example: flat inputs", null, (_, _) => AppendToEditor (Examples.FlatInputs));

            Controls.Add (menu);
        }

        // The editor keeps its own colours whatever the theme says, so a mistake in the sheet (white on
        // white, say) never takes the editor down with it.
        private void PinEditorStyle ()
        {
            editor.Style.BackgroundColor = new SKColor (0x1e, 0x1e, 0x1e);
            editor.Style.ForegroundColor = new SKColor (0xd4, 0xd4, 0xd4);
            editor.Style.Border.Width = 0;
            editor.Style.FontSize = 13;
            editor.Style.Font = FirstInstalled ("Cascadia Code", "Menlo", "Consolas", "DejaVu Sans Mono", "monospace");
        }

        private static SKTypeface FirstInstalled (params string[] families)
        {
            foreach (var family in families) {
                var typeface = SKTypeface.FromFamilyName (family);
                if (typeface is not null && string.Equals (typeface.FamilyName, family, StringComparison.OrdinalIgnoreCase))
                    return typeface;
            }

            return SKTypeface.FromFamilyName (families[families.Length - 1]) ?? SKTypeface.Default;
        }

        // ---- applying ------------------------------------------------------------------------------

        private void ApplyEditorText ()
        {
            var css = editor.Text ?? string.Empty;

            if (css == last_applied)
                return;

            last_applied = css;

            var sheet = ThemeStyleSheet.Parse (css);
            LastDiagnostics = sheet.Diagnostics;
            LastHadErrors = sheet.HasErrors;

            try {
                // Apply whatever parsed, even mid-edit: the preview should track the author's intent,
                // and the diagnostics list says what was dropped.
                Theme.ApplyStyleSheet (sheet);
            } catch (ThemeCssException ex) {
                // An unknown or cyclic 'extends' is only discoverable at apply time.
                LastDiagnostics = sheet.Diagnostics.Concat (ex.Diagnostics).ToList ();
                LastHadErrors = true;
            }

            // Re-pin after ApplyStyleSheet: a `TextBox { ... }` rule changes the type default, not the
            // editor's own values, so this is belt and braces -- but a theme change also re-runs the type
            // defaults, and the pinned instance values are what keep the editor legible.
            PinEditorStyle ();

            diagnostics.Items.Clear ();
            foreach (var diagnostic in LastDiagnostics)
                diagnostics.Items.Add (diagnostic.ToString ());

            var errors = LastDiagnostics.Count (d => d.Severity == ThemeCssSeverity.Error);
            var warnings = LastDiagnostics.Count (d => d.Severity == ThemeCssSeverity.Warning);
            var name = sheet.Name is null ? string.Empty : $"\"{sheet.Name}\"" + (sheet.BaseName is null ? string.Empty : $" extends {sheet.BaseName}") + " — ";

            status.Text = errors == 0 && warnings == 0
                ? $"{name}Applied: {sheet.TokenCount} tokens, {sheet.RuleCount} control rules."
                : $"{name}Applied with {errors} error(s), {warnings} warning(s): {sheet.TokenCount} tokens, {sheet.RuleCount} control rules.";
            status.Style.ForegroundColor = errors > 0 ? Theme.WarningHighlightColor : (SKColor?) null;

            preview.RefreshTokens ();
            Text = file_path is null ? "Theme Studio" : $"Theme Studio — {Path.GetFileName (file_path)}";
        }

        private void SetEditorText (string text, bool apply)
        {
            suppress_editor_events = true;
            try {
                editor.Text = text;
                editor.SelectionStart = 0;   // show the start of a loaded file, not its end
            } finally {
                suppress_editor_events = false;
            }

            if (apply)
                ApplyEditorText ();
        }

        private void AppendToEditor (string snippet)
        {
            SetEditorText ((editor.Text ?? string.Empty).TrimEnd () + "\n\n" + snippet.Trim () + "\n", apply: true);
        }

        private void NewFrom (BuiltInTheme builtIn)
        {
            StopWatching ();
            file_path = null;

            // Export from the chosen built-in, then restore whatever was current: ExportCss reads the
            // live theme, and the editor's own apply is what should change the preview.
            Theme.SetBuiltInTheme (builtIn);
            var css = Theme.ExportCss ("MyTheme", builtIn.ToString ());

            SetEditorText (css, apply: true);
        }

        // ---- files ---------------------------------------------------------------------------------

        private void LoadFile (string path)
        {
            file_path = path;
            SetEditorText (File.ReadAllText (path), apply: true);
            StartWatching (path);
        }

        private async System.Threading.Tasks.Task OpenAsync ()
        {
            var dialog = new OpenFileDialog { Title = "Open a theme" };
            dialog.AddFilter ("CSS themes", "*.css");
            dialog.AddFilter ("All files", "*.*");

            if (await dialog.ShowDialogAsync (this) == DialogResult.OK && dialog.FileName is { } chosen)
                LoadFile (chosen);
        }

        private async System.Threading.Tasks.Task SaveAsync (bool saveAs)
        {
            if (saveAs || file_path is null) {
                var dialog = new SaveFileDialog { Title = "Save the theme", DefaultExtension = "css" };
                dialog.AddFilter ("CSS themes", "*.css");

                if (await dialog.ShowDialogAsync (this) != DialogResult.OK || dialog.FileName is not { } chosen)
                    return;

                file_path = chosen;
            }

            // Writing the file we are watching would echo back as a reload; pause the watcher around it.
            StopWatching ();
            File.WriteAllText (file_path, editor.Text ?? string.Empty);
            StartWatching (file_path);
            Text = $"Theme Studio — {Path.GetFileName (file_path)}";
        }

        private void StartWatching (string path)
        {
            StopWatching ();

            var directory = Path.GetDirectoryName (path);
            if (string.IsNullOrEmpty (directory))
                return;

            watcher = new FileSystemWatcher (directory, Path.GetFileName (path)) {
                NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.Size | NotifyFilters.FileName,
                EnableRaisingEvents = true,
            };

            FileSystemEventHandler onChanged = (_, _) => BeginInvoke (ReloadFromDisk);
            watcher.Changed += onChanged;
            watcher.Created += onChanged;
            watcher.Renamed += (_, _) => BeginInvoke (ReloadFromDisk);
        }

        private void StopWatching ()
        {
            watcher?.Dispose ();
            watcher = null;
        }

        private void ReloadFromDisk ()
        {
            if (file_path is null || !File.Exists (file_path))
                return;

            // Editors save in two steps (truncate, write); a read that lands between them sees an empty
            // file. Retry briefly rather than flashing an empty theme.
            for (var attempt = 0; attempt < 5; attempt++) {
                try {
                    var text = File.ReadAllText (file_path);
                    if (text.Length == 0 && attempt < 4) {
                        System.Threading.Thread.Sleep (40);
                        continue;
                    }
                    if (text != editor.Text)
                        SetEditorText (text, apply: true);
                    return;
                } catch (IOException) {
                    System.Threading.Thread.Sleep (40);
                }
            }
        }

        // ---- reference -----------------------------------------------------------------------------

        private void CopyReference ()
        {
            Clipboard.SetText (Examples.AssistantPrompt + "\n\n" + ThemeCssReference.ToMarkdown () + "\nCurrent theme, for reference:\n\n```css\n" + Theme.ExportCss () + "```\n");
            status.Text = "Reference copied to the clipboard — paste it into a chat with your coding assistant along with the look you want.";
        }

        protected override void Dispose (bool disposing)
        {
            if (disposing) {
                StopWatching ();
                debounce.Dispose ();
            }

            base.Dispose (disposing);
        }
    }

    internal static class Examples
    {
        public const string AssistantPrompt =
            "Write a Majorsilence.Forms CSS theme. Use ONLY the tokens, selectors and properties listed in the reference " +
            "below -- nothing else is supported and anything else is rejected with an error. Start the file with " +
            "`@theme \"Name\" extends Light;` (or Dark). Set the :root tokens first, then add control rules only where " +
            "the defaults are not what I want. Keep text readable: --foreground-color on --background-color and " +
            "--control-low-color, --foreground-color-on-accent on --accent-color. Colours are #rrggbb (alpha last if " +
            "any), lengths are whole pixels.";

        public const string RoundedButtons = @"/* Rounded buttons that fill with the accent on hover. */
Button {
  border: 1px solid var(--border-low-color);
  border-radius: 6px;
}

Button:hover {
  background-color: var(--accent-color);
  border-color: var(--accent-color-2);
  color: var(--foreground-color-on-accent);
}";

        public const string FlatInputs = @"/* Flat inputs: no frame, just an underline in the accent colour. */
TextBox, ComboBox, NumericUpDown {
  background-color: var(--control-low-color);
  border-width: 0;
  border-bottom-width: 2px;
  border-bottom-color: var(--accent-color-2);
}";
    }
}
