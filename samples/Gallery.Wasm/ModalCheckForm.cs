using System;
using System.Linq;
using System.Threading.Tasks;
using Majorsilence.Forms;

namespace Gallery.Wasm
{
    /// <summary>
    /// A browser-head check for the blocking WinForms modal patterns (issue #406). Open the published
    /// gallery with <c>?check=&lt;name&gt;</c> and this form runs that one check instead of the gallery,
    /// writing <c>MFCHECK</c> lines to the browser console:
    ///
    /// <list type="bullet">
    /// <item><c>START</c> just before the call, so a call that never returns is visible as a START
    /// with nothing after it -- a hang blocks the tab's only thread, so the page cannot report it
    /// itself; <c>tools/modal-check.mjs</c> reports it as a timeout.</item>
    /// <item><c>RETURNED</c> with the result when the call comes back.</item>
    /// <item><c>THREW</c> with the exception type and message when it fails.</item>
    /// </list>
    ///
    /// One check per page load, because a hang would stop every check after it. Each blocking check
    /// arms a timer that answers the dialog after half a second, so on a platform where the nested
    /// loop runs the call comes back on its own with no one clicking anything.
    /// </summary>
    internal sealed class ModalCheckForm : Form
    {
        private readonly string check;

        public ModalCheckForm (string check)
        {
            this.check = check;
            Text = "Modal check: " + check;
            Width = 640;
            Height = 400;

            Controls.Add (new Label {
                Name = "status",
                Text = "Running check '" + check + "'. Results go to the browser console (MFCHECK lines).",
                Left = 12, Top = 12, Width = 600, Height = 40,
            });

            // Built with the form, so they are in its first frame -- the accessibility DOM follows painting.
            if (check == "a11y")
                AddA11yTargets ();

            // Start once the page has painted the main form: a modal call from inside the startup path
            // would measure the bootstrap rather than the modal loop.
            var start = new Majorsilence.Forms.Timer { Interval = 750 };
            start.Tick += async (_, _) => {
                start.Stop ();
                await RunAsync ();
            };
            start.Start ();
        }

        // The names the harness passes; also the list it prints when an unknown one is asked for.
        internal static readonly string[] Checks = [
            "showdialog", "messagebox", "commondialog", "taskdialog",
            "showdialogasync", "messageboxasync", "taskdialogasync", "a11y",
        ];

        private async Task RunAsync ()
        {
            Log ("BEGIN", OperatingSystem.IsBrowser () ? "browser" : "not-browser");

            try {
                // The blocking calls are what this form measures, so the analyzer that flags them in
                // browser code (MFB001) is silenced for exactly these.
#pragma warning disable MFB001
                switch (check) {
                case "showdialog":
                    Measure (() => {
                        using var dialog = NewDialog ();
                        AnswerSoon (() => dialog.DialogResult = DialogResult.OK);
                        return dialog.ShowDialog (this).ToString ();
                    });
                    break;
                case "messagebox":
                    Measure (() => {
                        AnswerSoon (CloseTopDialog);
                        return MessageBox.Show (this, "A blocking message box", "check").ToString ();
                    });
                    break;
                case "commondialog":
                    // The platform file picker cannot be answered from code, and in a browser it also
                    // needs a user gesture; what this measures is whether the blocking wrapper itself
                    // can run, not whether a file gets picked.
                    Measure (() => {
                        using var dialog = new OpenFileDialog ();
                        return dialog.ShowDialog (this).ToString ();
                    });
                    break;
                case "taskdialog":
                    Measure (() => {
                        AnswerSoon (CloseTopDialog);
                        return TaskDialog.ShowDialog (this, new TaskDialogPage { Text = "A blocking task dialog" }).ToString ();
                    });
                    break;
#pragma warning restore MFB001
                case "showdialogasync":
                    await MeasureAsync (async () => {
                        using var dialog = NewDialog ();
                        AnswerSoon (() => dialog.DialogResult = DialogResult.OK);
                        return (await dialog.ShowDialogAsync (this)).ToString ();
                    });
                    break;
                case "messageboxasync":
                    await MeasureAsync (async () => {
                        AnswerSoon (CloseTopDialog);
                        return (await MessageBox.ShowAsync (this, "An awaited message box", "check")).ToString ();
                    });
                    break;
                case "taskdialogasync":
                    await MeasureAsync (async () => {
                        AnswerSoon (CloseTopDialog);
                        return (await TaskDialog.ShowDialogAsync (this, new TaskDialogPage { Text = "An awaited task dialog" })).ToString ();
                    });
                    break;
                case "a11y":
                    // Nothing to call: the harness reads the accessibility DOM next to the canvas, which
                    // has one control of each common kind to find (AddA11yTargets). Focus one, so the
                    // host's aria-activedescendant has something to point at.
                    Controls.Find ("nameBox", false).FirstOrDefault ()?.Focus ();
                    Log ("READY", "a11y");
                    break;
                default:
                    Log ("UNKNOWN", check + " (known: " + string.Join (", ", Checks) + ")");
                    break;
                }
            } finally {
                Log ("END", check);
            }
        }

        private void Measure (Func<string> call)
        {
            Log ("START", check);

            try {
                Log ("RETURNED", call ());
            } catch (Exception ex) {
                Log ("THREW", ex.GetType ().FullName + ": " + ex.Message);
            }

            LogLeftovers ();
        }

        private async Task MeasureAsync (Func<Task<string>> call)
        {
            Log ("START", check);

            try {
                Log ("RETURNED", await call ());
            } catch (Exception ex) {
                Log ("THREW", ex.GetType ().FullName + ": " + ex.Message);
            }

            LogLeftovers ();
        }

        // What a failed call left behind: a dialog still open, or this form still disabled, is a UI the
        // user can no longer get out of.
        private void LogLeftovers () =>
            Log ("AFTER", "open forms=" + Application.OpenForms.Count + ", owner enabled=" + Enabled);

        private static Form NewDialog () => new () { Text = "A modal dialog", Width = 300, Height = 160 };

        // Answers whatever dialog the check opened, as a user clicking it would.
        private static void AnswerSoon (Action answer)
        {
            var timer = new Majorsilence.Forms.Timer { Interval = 500 };
            timer.Tick += (_, _) => {
                timer.Stop ();
                Log ("ANSWERING", "timer fired");
                answer ();
            };
            timer.Start ();
        }

        private static void CloseTopDialog ()
        {
            // The newest open form is the dialog the check just opened.
            if (Application.OpenForms.Cast<Form> ().LastOrDefault () is { } top and not ModalCheckForm)
                top.Close ();
        }

        private void AddA11yTargets ()
        {
            Controls.Add (new Button { Name = "okButton", Text = "&OK", Left = 12, Top = 60, Width = 90 });
            Controls.Add (new CheckBox { Name = "agree", Text = "I agree", Checked = true, Left = 12, Top = 100, Width = 160 });
            Controls.Add (new TextBox { Name = "nameBox", AccessibleName = "Customer name", Text = "Ada", Left = 12, Top = 140, Width = 200 });
            Controls.Add (new Button { Name = "disabledButton", Text = "Unavailable", Enabled = false, Left = 120, Top = 60, Width = 120 });
        }

        private static void Log (string what, string detail) => Console.WriteLine ("MFCHECK " + what + " " + detail);
    }
}
