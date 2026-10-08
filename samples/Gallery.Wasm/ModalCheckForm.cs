using System;
using System.Drawing;
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
    /// The rendering checks (timeradd, dialogvisual, messageboxvisual, owneddialogvisual) also log a
    /// <c>SNAP</c> line saying what the screen should show at that moment; the harness screenshots the
    /// page and checks the pixels. Like <c>a11y</c>, they need that browser harness, so the mobile
    /// heads' scripts do not run them.
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

            // The rendering checks read pixels off a screenshot, so the owner is one flat colour the
            // harness can tell apart from anything a dialog or a late control draws.
            if (check is "timeradd" or "dialogvisual" or "messageboxvisual" or "owneddialogvisual")
                BackColor = OwnerColor;

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
            "timeradd", "dialogvisual", "messageboxvisual", "owneddialogvisual",
        ];

        // The rendering checks' colours, which tools/modal-check.mjs looks for in the screenshot.
        private static readonly Color OwnerColor = Color.FromArgb (0, 128, 255);
        private static readonly Color LateColor = Color.FromArgb (255, 0, 255);
        private static readonly Color SecondOwnerColor = Color.FromArgb (0, 160, 0);

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
                case "timeradd":
                    await TimerAddAsync ();
                    break;
                case "dialogvisual":
                    await DialogVisualAsync (this, async () => {
                        using var dialog = NewClickableDialog ();
                        return (await dialog.ShowDialogAsync (this)).ToString ();
                    });
                    break;
                case "messageboxvisual":
                    await DialogVisualAsync (this, async () =>
                        (await MessageBox.ShowAsync (this, "An awaited message box", "check")).ToString ());
                    break;
                case "owneddialogvisual": {
                    // The dialog's owner is itself a second top-level form, not the page's root one --
                    // sized the way designer code does it, one dimension at a time.
                    using var owner = new Form { Text = "Owner", BackColor = SecondOwnerColor, StartPosition = FormStartPosition.Manual };
                    owner.Width = 420;
                    owner.Height = 300;
                    owner.Location = new Point (40, 60);
                    owner.Show (this);
                    await Task.Delay (300);
                    await DialogVisualAsync (owner, async () => {
                        using var dialog = NewClickableDialog ();
                        return (await dialog.ShowDialogAsync (owner)).ToString ();
                    });
                    owner.Close ();
                    break;
                }
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

        // A control added to the already-shown form from a Timer.Tick has to reach the screen (and the
        // accessibility DOM, which follows painting) without anything else causing a repaint. Nothing
        // else here changes after the tick, so a missed invalidation stays missed.
        private async Task TimerAddAsync ()
        {
            using var watch = new Watch (check);

            var added = new TaskCompletionSource ();
            var timer = new Majorsilence.Forms.Timer { Interval = 300 };
            timer.Tick += (_, _) => {
                timer.Stop ();
                Controls.Add (new Panel { Name = "latePanel", BackColor = LateColor, Left = 12, Top = 80, Width = 160, Height = 60 });
                Controls.Add (new Button { Name = "lateButton", Text = "Late button", Left = 200, Top = 80, Width = 120 });
                Log ("ADDED", "latePanel, lateButton");
                added.SetResult ();
            };
            timer.Start ();
            await added.Task;

            // Long enough for a scheduled frame (and the mirror's sync after it) to land, if one was.
            await Task.Delay (1000);

            var panel = Controls.Find ("latePanel", false)[0];
            var center = panel.PointToScreen (new Point (panel.Width / 2, panel.Height / 2));
            Snap ("{\"probes\":[{\"what\":\"late panel centre\",\"x\":" + center.X + ",\"y\":" + center.Y
                + ",\"rgb\":\"" + Hex (LateColor) + "\"}],\"a11y\":\"Late button\"}");
            await Task.Delay (1500);
            Log ("RETURNED", "added");
            watch.Finish ();
        }

        // While an awaited dialog is open, the owner has to stay drawn behind it, the dialog's frame
        // has to be drawn whole, and the dialog has to take a click. The harness snaps the page while
        // the dialog is up and checks, from the owner's flat colour: a point on the owner outside the
        // dialog is still the owner's colour, and scanning in from beyond the dialog's right and bottom
        // edges, the first non-owner pixel is the dialog's outer edge -- not some way inside it, which
        // is what a clipped frame looks like. Then it clicks the dialog's OK button; if that click does
        // not reach the dialog, the fallback timer closes it with Cancel instead, and the check fails.
        private async Task DialogVisualAsync (Form owner, Func<Task<string>> call)
        {
            using var watch = new Watch (check);

            var snap = new Majorsilence.Forms.Timer { Interval = 1000 };
            snap.Tick += (_, _) => {
                snap.Stop ();

                if (Application.OpenForms.Cast<Form> ().LastOrDefault () is not { } dialog || dialog == owner) {
                    Log ("THREW", "no dialog open to snap");
                    return;
                }

                var colour = Hex (owner.BackColor);
                var b = dialog.Bounds;
                // Bottom-left of the owner's client area: clear of a dialog centred on it.
                var probe = owner.PointToScreen (new Point (8, owner.ClientSize.Height - 8));
                var ok = FindButton (dialog, "OK");
                var click = ok?.PointToScreen (new Point (ok.Width / 2, ok.Height / 2));
                Log ("GEOMETRY", "dialog bounds=" + b + " client=" + dialog.ClientSize + "; owner bounds=" + owner.Bounds);
                Snap ("{\"probes\":[{\"what\":\"owner behind the dialog\",\"x\":" + probe.X + ",\"y\":" + probe.Y
                    + ",\"rgb\":\"" + colour + "\"}],\"dialog\":{\"x\":" + b.X + ",\"y\":" + b.Y
                    + ",\"w\":" + b.Width + ",\"h\":" + b.Height + "},\"owner\":\"" + colour + "\""
                    + (click is { } c ? ",\"click\":{\"x\":" + c.X + ",\"y\":" + c.Y + "},\"result\":\"OK\"" : "") + "}");

                // The fallback, should the harness's click not get through.
                var fallback = new Majorsilence.Forms.Timer { Interval = 3000 };
                fallback.Tick += (_, _) => {
                    fallback.Stop ();
                    if (dialog.Visible) {
                        Log ("ANSWERING", "click did not close the dialog; cancelling it");
                        dialog.DialogResult = DialogResult.Cancel;
                        dialog.Close ();
                    }
                };
                fallback.Start ();
            };
            snap.Start ();

            try {
                Log ("RETURNED", await call ());
            } catch (Exception ex) {
                LogThrew (ex);
            }

            watch.Finish ();
            LogLeftovers ();
        }

        private static Button? FindButton (Form form, string text) =>
            form.Controls.Cast<Control> ().SelectMany (Descendants).OfType<Button> ().FirstOrDefault (b => b.Text.Replace ("&", "") == text);

        private static System.Collections.Generic.IEnumerable<Control> Descendants (Control c) =>
            new[] { c }.Concat (c.Controls.Cast<Control> ().SelectMany (Descendants));

        // Sized one dimension at a time, as designer code does, then given an OK button to click.
        private static Form NewClickableDialog ()
        {
            var dialog = new Form { Text = "A modal dialog", StartPosition = FormStartPosition.CenterParent };
            dialog.Width = 300;
            dialog.Height = 160;
            var ok = new Button { Text = "OK", Left = 100, Top = 60, Width = 90 };
            ok.Click += (_, _) => dialog.DialogResult = DialogResult.OK;
            dialog.Controls.Add (ok);
            return dialog;
        }

        // Asks the harness to screenshot the page now; the JSON says what to look for in it.
        private static void Snap (string json) => Log ("SNAP", json);

        private static string Hex (Color c) => c.R.ToString ("x2") + c.G.ToString ("x2") + c.B.ToString ("x2");

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
