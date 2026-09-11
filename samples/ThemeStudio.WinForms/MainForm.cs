using System.Drawing;
using Majorsilence.Forms;
using Majorsilence.Forms.Theming.WinForms;
using WF = System.Windows.Forms;

namespace ThemeStudio.WinForms
{
    // The Studio window: a CSS editor on the left, the WinForms control preview on the right, and the
    // diagnostics -- the parser's and the WinForms applier's, one list -- under the editor. Every edit
    // re-parses and re-applies after a short pause; an opened file is also watched on disk so an
    // external editor (or an assistant) drives the preview too. The window itself is themed by the
    // sheet through WinFormsCssTheme -- that is the point -- except the editor, whose colours are set
    // explicitly here and therefore kept by the applier ("explicit per-control values win"), so a
    // theme that goes wrong cannot make the text used to fix it unreadable.
    public sealed class MainForm : WF.Form
    {
        private const int DebounceMilliseconds = 250;

        private readonly WF.TextBox editor;
        private readonly WF.ListBox diagnostics;
        private readonly WF.Label status;
        private readonly PreviewPanel preview;
        private readonly WF.Timer debounce = new () { Interval = DebounceMilliseconds };

        private string? file_path;
        private FileSystemWatcher? watcher;
        private string last_applied = string.Empty;
        private bool suppress_editor_events;

        // The newline style of the opened file, restored on save. The Win32 edit control only breaks
        // lines on CRLF -- an LF-only file (every theme in the repo) shows as one long line -- so the
        // editor always holds CRLF and the file keeps whatever it had.
        private string file_newline = "\n";

        public MainForm (string? path)
        {
            Text = "Theme Studio (WinForms)";
            Size = new Size (1280, 860);
            StartPosition = WF.FormStartPosition.CenterScreen;

            var split = new WF.SplitContainer { Dock = WF.DockStyle.Fill, FixedPanel = WF.FixedPanel.Panel1 };
            Controls.Add (split);

            // Panel min sizes and SplitterDistance are validated against the container's CURRENT width,
            // which is the default 150 until docking lays it out inside the form -- setting them in the
            // initializer throws. Set them once the container has the form's width.
            if (split.Width >= 720) {
                split.Panel1MinSize = 320;
                split.Panel2MinSize = 400;
                split.SplitterDistance = 460;
            }

            // --- left: toolbar, editor, diagnostics, status ---
            editor = new WF.TextBox {
                Dock = WF.DockStyle.Fill,
                Multiline = true,
                WordWrap = false,
                AcceptsReturn = true,
                AcceptsTab = true,
                ScrollBars = WF.ScrollBars.Both,
                // Explicit values: the applier leaves them alone whatever the theme says.
                BackColor = Color.FromArgb (0x1e, 0x1e, 0x1e),
                ForeColor = Color.FromArgb (0xd4, 0xd4, 0xd4),
                Font = new Font (FirstInstalled ("Cascadia Code", "Consolas", "Courier New"), 10f),
                BorderStyle = WF.BorderStyle.None,
            };

            var toolbar = new WF.Panel { Dock = WF.DockStyle.Top, Height = 34 };
            var open = new WF.Button { Text = "Open…", Left = 4, Top = 3, Width = 80, Height = 28 };
            var save = new WF.Button { Text = "Save", Left = 88, Top = 3, Width = 80, Height = 28 };
            var copyRef = new WF.Button { Text = "Copy reference for AI", Left = 172, Top = 3, Width = 170, Height = 28 };
            var apply = new WF.Button { Text = "Apply", Left = 346, Top = 3, Width = 80, Height = 28 };
            open.Click += (_, _) => Open ();
            save.Click += (_, _) => Save (saveAs: false);
            copyRef.Click += (_, _) => CopyReference ();
            apply.Click += (_, _) => ApplyEditorText ();
            toolbar.Controls.AddRange (new WF.Control[] { open, save, copyRef, apply });

            diagnostics = new WF.ListBox { Dock = WF.DockStyle.Bottom, Height = 170, HorizontalScrollbar = true };
            status = new WF.Label {
                Dock = WF.DockStyle.Bottom, Height = 26, Padding = new WF.Padding (6, 6, 0, 0), AutoEllipsis = true,
                Text = "Ready. The editor keeps its own dark colours on purpose, so a theme that goes wrong cannot make the text used to fix it unreadable.",
            };

            // Docking resolves in reverse z-order: the Fill control goes in first.
            split.Panel1.Controls.Add (editor);
            split.Panel1.Controls.Add (toolbar);
            split.Panel1.Controls.Add (diagnostics);
            split.Panel1.Controls.Add (status);

            BuildMenu ();

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
            preview = new PreviewPanel { Dock = WF.DockStyle.Fill };
            split.Panel2.Controls.Add (preview);

            // Style this window now and keep styling what gets added to it.
            WinFormsCssTheme.Track (this);

            if (path is not null && File.Exists (path))
                LoadFile (path);
            else
                SetEditorText (Theme.ExportCss ("MyTheme", "Light"), apply: true);
        }

        /// <summary>The diagnostics of the last apply: the parser's, then the WinForms applier's.</summary>
        public IReadOnlyList<ThemeCssDiagnostic> LastDiagnostics { get; private set; } = Array.Empty<ThemeCssDiagnostic> ();

        /// <summary>Whether the last applied sheet had parse errors.</summary>
        public bool LastHadErrors { get; private set; }

        /// <summary>
        /// Shows the window invisibly (zero opacity, off-screen, no taskbar entry) so every child has a
        /// handle and a settled layout for <c>--screenshot</c>. <see cref="WF.Control.DrawToBitmap"/> prints
        /// child windows, and a child only gets a window once its form is shown — <c>CreateControl()</c>
        /// is a no-op while the form is not visible.
        /// </summary>
        public void ShowForScreenshot ()
        {
            StartPosition = WF.FormStartPosition.Manual;
            Location = new Point (-32000, -32000);
            ShowInTaskbar = false;
            Opacity = 0;
            Show ();
            WF.Application.DoEvents ();
        }

        /// <summary>Renders the preview panel with <see cref="WF.Control.DrawToBitmap"/>.</summary>
        public Bitmap CapturePreview ()
        {
            var bitmap = new Bitmap (Math.Max (1, preview.Width), Math.Max (1, preview.Height));
            preview.DrawToBitmap (bitmap, new Rectangle (0, 0, bitmap.Width, bitmap.Height));
            return bitmap;
        }

        private void BuildMenu ()
        {
            var menu = new WF.MenuStrip ();

            var file = new WF.ToolStripMenuItem ("File");
            var newFrom = new WF.ToolStripMenuItem ("New from built-in theme");
            foreach (var builtIn in Enum.GetValues<BuiltInTheme> ()) {
                if (builtIn == BuiltInTheme.Default)
                    continue;
                var captured = builtIn;
                newFrom.DropDownItems.Add (captured.ToString (), null, (_, _) => NewFrom (captured));
            }
            file.DropDownItems.Add (newFrom);
            file.DropDownItems.Add ("Open…", null, (_, _) => Open ());
            file.DropDownItems.Add ("Save", null, (_, _) => Save (saveAs: false));
            file.DropDownItems.Add ("Save As…", null, (_, _) => Save (saveAs: true));
            file.DropDownItems.Add (new WF.ToolStripSeparator ());
            file.DropDownItems.Add ("Exit", null, (_, _) => Close ());

            var help = new WF.ToolStripMenuItem ("Help");
            help.DropDownItems.Add ("Copy reference for AI (Markdown + prompt)", null, (_, _) => CopyReference ());
            help.DropDownItems.Add ("Copy current theme as CSS", null, (_, _) => WF.Clipboard.SetText (Theme.ExportCss ("MyTheme")));
            help.DropDownItems.Add ("Copy WinForms support matrix (Markdown)", null, (_, _) => WF.Clipboard.SetText (WinFormsThemeSupport.ToMarkdown ()));

            menu.Items.Add (file);
            menu.Items.Add (help);
            MainMenuStrip = menu;
            Controls.Add (menu);
        }

        private static string FirstInstalled (params string[] families)
        {
            foreach (var family in families) {
                try {
                    using var probe = new FontFamily (family);
                    return family;
                } catch (ArgumentException) {
                    // not installed; try the next one
                }
            }

            return FontFamily.GenericMonospace.Name;
        }

        // ---- applying ------------------------------------------------------------------------------

        private void ApplyEditorText ()
        {
            var css = editor.Text ?? string.Empty;

            if (css == last_applied)
                return;

            last_applied = css;

            var sheet = ThemeStyleSheet.Parse (css);
            var found = new List<ThemeCssDiagnostic> (sheet.Diagnostics);
            LastHadErrors = sheet.HasErrors;

            try {
                // Apply whatever parsed, even mid-edit: the preview should track the author's intent,
                // and the diagnostics list says what was dropped -- and what WinForms could not do.
                WinFormsCssTheme.Apply (sheet);
            } catch (ThemeCssException ex) {
                // An unknown or cyclic 'extends' is only discoverable at apply time.
                found.AddRange (ex.Diagnostics);
                LastHadErrors = true;
            }

            found.AddRange (WinFormsCssTheme.Diagnostics);
            LastDiagnostics = found;

            diagnostics.Items.Clear ();
            foreach (var diagnostic in found)
                diagnostics.Items.Add (diagnostic.ToString ());

            var errors = found.Count (d => d.Severity == ThemeCssSeverity.Error);
            var warnings = found.Count (d => d.Severity == ThemeCssSeverity.Warning);
            var infos = found.Count (d => d.Severity == ThemeCssSeverity.Info);
            var name = sheet.Name is null ? string.Empty : $"\"{sheet.Name}\"" + (sheet.BaseName is null ? string.Empty : $" extends {sheet.BaseName}") + " — ";

            // Short on purpose: the label is 460px wide and truncates (with an ellipsis) past that.
            status.Text = $"{name}{sheet.TokenCount} tokens, {sheet.RuleCount} rules · {errors} errors, {warnings} warnings, {infos} info";

            preview.RefreshFromTheme ();
            Text = file_path is null ? "Theme Studio (WinForms)" : $"Theme Studio (WinForms) — {Path.GetFileName (file_path)}";
        }

        private void SetEditorText (string text, bool apply)
        {
            suppress_editor_events = true;
            try {
                editor.Text = ToEditorNewlines (text);
                editor.SelectionStart = 0;
                editor.SelectionLength = 0;
            } finally {
                suppress_editor_events = false;
            }

            if (apply)
                ApplyEditorText ();
        }

        private void NewFrom (BuiltInTheme builtIn)
        {
            StopWatching ();
            file_path = null;

            Theme.SetBuiltInTheme (builtIn);
            SetEditorText (Theme.ExportCss ("MyTheme", builtIn.ToString ()), apply: true);
        }

        // ---- files ---------------------------------------------------------------------------------

        private static string ToEditorNewlines (string text) => text.Replace ("\r\n", "\n").Replace ("\n", "\r\n");

        private void LoadFile (string path)
        {
            file_path = path;
            var text = File.ReadAllText (path);
            file_newline = text.Contains ("\r\n", StringComparison.Ordinal) ? "\r\n" : "\n";
            SetEditorText (text, apply: true);
            StartWatching (path);
        }

        private void Open ()
        {
            using var dialog = new WF.OpenFileDialog { Title = "Open a theme", Filter = "CSS themes|*.css|All files|*.*" };

            if (dialog.ShowDialog (this) == WF.DialogResult.OK)
                LoadFile (dialog.FileName);
        }

        private void Save (bool saveAs)
        {
            if (saveAs || file_path is null) {
                using var dialog = new WF.SaveFileDialog { Title = "Save the theme", DefaultExt = "css", Filter = "CSS themes|*.css" };

                if (dialog.ShowDialog (this) != WF.DialogResult.OK)
                    return;

                file_path = dialog.FileName;
            }

            // Writing the file we are watching would echo back as a reload; pause the watcher around it.
            StopWatching ();
            File.WriteAllText (file_path, (editor.Text ?? string.Empty).Replace ("\r\n", file_newline));
            StartWatching (file_path);
            Text = $"Theme Studio (WinForms) — {Path.GetFileName (file_path)}";
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
                        Thread.Sleep (40);
                        continue;
                    }
                    if (ToEditorNewlines (text) != editor.Text)
                        SetEditorText (text, apply: true);
                    return;
                } catch (IOException) {
                    Thread.Sleep (40);
                }
            }
        }

        // ---- reference -----------------------------------------------------------------------------

        private void CopyReference ()
        {
            WF.Clipboard.SetText (
                AssistantPrompt + "\n\n" + ThemeCssReference.ToMarkdown ()
                + "\n## What real System.Windows.Forms can show\n\n" + WinFormsThemeSupport.ToMarkdown ()
                + "\nCurrent theme, for reference:\n\n```css\n" + Theme.ExportCss () + "```\n");
            status.Text = "Reference copied to the clipboard — paste it into a chat with your coding assistant along with the look you want.";
        }

        private const string AssistantPrompt =
            "Write a Majorsilence.Forms CSS theme that will also be applied to real System.Windows.Forms controls. Use ONLY " +
            "the tokens, selectors and properties listed in the reference below -- nothing else is supported and anything " +
            "else is rejected with an error. Start the file with `@theme \"Name\" extends Light;` (or Dark). Set the :root " +
            "tokens first, then add control rules only where the defaults are not what I want. Prefer properties the " +
            "WinForms support matrix marks native; approximate ones are fine; unsupported ones are skipped on WinForms. " +
            "Keep text readable: --foreground-color on --background-color and --control-low-color, " +
            "--foreground-color-on-accent on --accent-color. Colours are #rrggbb, lengths are whole pixels.";

        protected override void Dispose (bool disposing)
        {
            if (disposing) {
                StopWatching ();
                debounce.Dispose ();
            }

            base.Dispose (disposing);
        }
    }
}
