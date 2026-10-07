using System;
using System.Linq;
using System.Threading.Tasks;
using Majorsilence.Forms;
// The Android head's implicit usings bring Android.App and Android.Widget in, which have their own.
using Application = Majorsilence.Forms.Application;
using Button = Majorsilence.Forms.Button;
using CheckBox = Majorsilence.Forms.CheckBox;

namespace Gallery.Checks
{
    /// <summary>
    /// A head check for the blocking WinForms modal patterns (issue #406), shared by the browser, Android
    /// and iOS heads (the mobile heads link this file). Started with a check name -- <c>?check=&lt;name&gt;</c>
    /// in the browser, an intent extra on Android, an environment variable on iOS; see docs/samples.md --
    /// this form runs that one check instead of the gallery, writing <c>MFCHECK</c> lines to the browser
    /// console, logcat (tag <c>MFCHECK</c>) or the app's stdout:
    ///
    /// <list type="bullet">
    /// <item><c>START</c> just before the call, so a call that never returns is visible as a START
    /// with nothing after it -- in the browser a hang blocks the tab's only thread, so the page cannot
    /// report it itself; <c>tools/modal-check.mjs</c> reports it as a timeout.</item>
    /// <item><c>RETURNED</c> with the result when the call comes back.</item>
    /// <item><c>THREW</c> with the exception type and message when it fails.</item>
    /// <item><c>HUNG</c>, on a platform with threads, from a watchdog when the call has not come back
    /// after <see cref="HangSeconds"/>, with how often the UI thread's timers ran meanwhile: none means
    /// the thread is stuck; some means a nested loop is running but nothing closed the dialog.</item>
    /// <item><c>PUMPED</c> after the call, the same count over the whole call -- a blocking call that
    /// returned with a non-zero count ran a nested loop that kept the UI alive.</item>
    /// </list>
    ///
    /// One check per launch, because a hang would stop every check after it. Each blocking check
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
                Text = "Running check '" + check + "'. Results go to the console or device log (MFCHECK lines).",
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

        // THREW, then where it came from: the innermost frames say whether the framework's own check or
        // the platform's dispatcher refused.
        private static void LogThrew (Exception ex)
        {
            Log ("THREW", ex.GetType ().FullName + ": " + ex.Message);

            var frames = (ex.StackTrace ?? string.Empty).Split ('\n', StringSplitOptions.RemoveEmptyEntries);
            Log ("STACK", string.Join (" | ", frames.Take (4).Select (f => f.Trim ())));
        }

        /// <summary>How long a call may take before the watchdog reports it as HUNG.</summary>
        internal const int HangSeconds = 10;

        private void Measure (Func<string> call)
        {
            using var watch = new Watch (check);

            try {
                Log ("RETURNED", call ());
            } catch (Exception ex) {
                LogThrew (ex);
            }

            watch.Finish ();
            LogLeftovers ();
        }

        private async Task MeasureAsync (Func<Task<string>> call)
        {
            using var watch = new Watch (check);

            try {
                Log ("RETURNED", await call ());
            } catch (Exception ex) {
                LogThrew (ex);
            }

            watch.Finish ();
            LogLeftovers ();
        }

        /// <summary>
        /// Logs START, then watches the call from both sides: a UI-thread timer counts how often the UI
        /// thread ran while the call was in progress (it can only tick if a loop is pumping), and a
        /// thread-pool watchdog reports HUNG if the call has not finished after <see cref="HangSeconds"/>.
        /// </summary>
        /// <remarks>
        /// The watchdog needs a second thread, so it does nothing in the browser -- there a hang stops the
        /// one thread it would run on, and the harness reports the missing outcome instead.
        /// </remarks>
        private sealed class Watch : IDisposable
        {
            private readonly Majorsilence.Forms.Timer pump = new () { Interval = 100 };
            private readonly System.Threading.Timer? watchdog;
            private int ticks;
            private int finished;

            public Watch (string check)
            {
                pump.Tick += (_, _) => System.Threading.Interlocked.Increment (ref ticks);
                pump.Start ();
                Log ("START", check);

                if (!OperatingSystem.IsBrowser ())
                    watchdog = new System.Threading.Timer (_ => {
                        if (System.Threading.Volatile.Read (ref finished) == 0)
                            Log ("HUNG", check + ": no return after " + HangSeconds + "s, ui ticks during the wait=" + System.Threading.Volatile.Read (ref ticks));
                    }, null, HangSeconds * 1000, System.Threading.Timeout.Infinite);
            }

            public void Finish ()
            {
                System.Threading.Volatile.Write (ref finished, 1);
                Log ("PUMPED", "ui ticks during the call=" + System.Threading.Volatile.Read (ref ticks));
            }

            public void Dispose ()
            {
                System.Threading.Volatile.Write (ref finished, 1);
                watchdog?.Dispose ();
                pump.Stop ();
                pump.Dispose ();
            }
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

        // On Android the line goes to logcat under its own tag, so `adb logcat -s MFCHECK` finds it;
        // everywhere else (the browser console, the iOS app's stdout) the console is the log.
        private static void Log (string what, string detail)
        {
            var line = "MFCHECK " + what + " " + detail;
#if ANDROID
            global::Android.Util.Log.Info ("MFCHECK", line);
#else
            Console.WriteLine (line);
#endif
        }
    }
}
