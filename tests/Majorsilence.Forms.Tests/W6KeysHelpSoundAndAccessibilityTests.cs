using Majorsilence.Forms.Automation;
using Majorsilence.Forms.Headless;
using Majorsilence.Forms.Media;
using Xunit;

namespace Majorsilence.Forms.Tests;

// W6 mechanisms, nineteenth chunk: SendKeys, Help.ShowHelp and the help provider's help-file path,
// SoundPlayer.Load, and AccessibilityNotifyClients -- ten empty-bodied methods.
[Collection ("Headless")]
public class W6KeysHelpSoundAndAccessibilityTests
{
    // ── SendKeys ────────────────────────────────────────────────────────────────────────────────────

    private static TextBox FocusedBox (out Form form, List<Keys> downs)
    {
        HeadlessRenderer.Use ();
        form = new Form { Width = 300, Height = 200 };
        var box = new TextBox { Width = 200 };
        form.Controls.Add (box);
        form.Show ();
        box.Select ();
        box.KeyDown += (_, e) => downs.Add (e.KeyData);

        var target = form;
        SendKeys.Target = () => target;
        return box;
    }

    [Fact]
    public void SendKeys_types_into_the_focused_control_as_the_keyboard_would ()
    {
        var downs = new List<Keys> ();
        var box = FocusedBox (out var form, downs);

        try {
            using (form) {
                SendKeys.SendWait ("ab+(cd)E");

                // Each character is a key down and a character, and Shift -- held over a group, or
                // implied by a capital -- types the capital.
                Assert.Equal ("abCDE", box.Text);
                Assert.Equal (new[] { Keys.A, Keys.B, Keys.C | Keys.Shift, Keys.D | Keys.Shift, Keys.E | Keys.Shift }, downs);

                // A named key is a key and no character; a count repeats it.
                downs.Clear ();
                SendKeys.SendWait ("{LEFT 2}{BACKSPACE}");
                Assert.Equal (new[] { Keys.Left, Keys.Left, Keys.Back }, downs);
                Assert.Equal ("abDE", box.Text);

                // Control makes a shortcut, not a character; the braces escape the syntax's own
                // characters.
                downs.Clear ();
                SendKeys.SendWait ("^a");
                Assert.Equal (new[] { Keys.A | Keys.Control }, downs);
                Assert.Equal ("abDE", box.Text);

                box.Text = string.Empty;
                SendKeys.SendWait ("{+}{{}{}}{~}");
                Assert.Equal ("+{}~", box.Text);
            }
        } finally {
            SendKeys.Target = null;
        }
    }

    [Fact]
    public void SendKeys_rejects_malformed_syntax_and_queues_keys_sent_from_a_key_handler ()
    {
        var downs = new List<Keys> ();
        var box = FocusedBox (out var form, downs);

        try {
            using (form) {
                Assert.Throws<ArgumentException> (() => SendKeys.Send ("{NOPE}"));
                Assert.Throws<ArgumentException> (() => SendKeys.Send ("(ab"));
                Assert.Throws<ArgumentException> (() => SendKeys.Send ("ab)"));
                Assert.Throws<ArgumentException> (() => SendKeys.Send ("{ENTER"));

                // A handler that sends a key does not re-enter itself: the key arrives after the
                // handler returns, which is the order upstream's queued Send gives.
                var depth = 0;
                var deepest = 0;

                box.KeyDown += (_, e) => {
                    depth++;
                    deepest = Math.Max (deepest, depth);

                    if (e.KeyCode == Keys.F2)
                        SendKeys.Send ("x");

                    depth--;
                };

                SendKeys.SendWait ("{F2}");

                Assert.Equal (1, deepest);
                Assert.Equal (new[] { Keys.F2, Keys.X }, downs);
                Assert.Equal ("x", box.Text);
            }
        } finally {
            SendKeys.Target = null;
        }
    }

    [Fact]
    public void Assigning_Text_drops_a_selection_the_new_text_cannot_hold ()
    {
        var downs = new List<Keys> ();
        var box = FocusedBox (out var form, downs);

        try {
            using (form) {
                box.Text = "abcd";
                box.SelectAll ();
                Assert.Equal (4, box.SelectionLength);

                box.Text = string.Empty;
                Assert.Equal (0, box.SelectionLength);

                // Typing used to delete through the stale four-character selection and throw.
                SendKeys.SendWait ("z");
                Assert.Equal ("z", box.Text);
            }
        } finally {
            SendKeys.Target = null;
        }
    }

    // ── Help ────────────────────────────────────────────────────────────────────────────────────────

    [Fact]
    public void ShowHelp_opens_the_file_and_the_provider_prefers_its_help_file_to_the_popup ()
    {
        HeadlessRenderer.Use ();
        var opened = new List<string> ();
        var previous = Help.Launcher;
        Help.Launcher = opened.Add;

        try {
            Help.ShowHelp (null, "manual.chm");
            Help.ShowHelp (null, "manual.chm", "Customers");
            Help.ShowHelpIndex (null, "https://example.invalid/help/");
            Help.ShowHelp (null, string.Empty);

            Assert.Equal (new[] { "manual.chm", "manual.chm", "https://example.invalid/help/" }, opened);

            using var form = new Form { Width = 300, Height = 200 };
            var box = new TextBox { Width = 120 };
            form.Controls.Add (box);
            form.Show ();

            using var provider = new HelpProvider ();
            provider.SetHelpString (box, "Popup text.");

            // Without a help file the string pops up ...
            opened.Clear ();
            box.RaiseKeyDown (new KeyEventArgs (Keys.F1));
            Assert.Empty (opened);
            Assert.Equal ("Popup text.", Help.PopupText);

            // ... and with one, the file opens instead, as upstream orders it.
            provider.HelpNamespace = "manual.chm";
            provider.SetHelpKeyword (box, "Customers");
            box.RaiseKeyDown (new KeyEventArgs (Keys.F1));
            Assert.Equal (new[] { "manual.chm" }, opened);
        } finally {
            Help.Launcher = previous;
        }
    }

    // ── SoundPlayer.Load ────────────────────────────────────────────────────────────────────────────

    private static byte[] Wave ()
    {
        using var buffer = new MemoryStream ();
        using var writer = new BinaryWriter (buffer);
        writer.Write ("RIFF"u8.ToArray ());
        writer.Write (36);
        writer.Write ("WAVEfmt "u8.ToArray ());
        writer.Write (16);
        writer.Write ((short)1);
        writer.Write ((short)1);
        writer.Write (8000);
        writer.Write (8000);
        writer.Write ((short)1);
        writer.Write ((short)8);
        writer.Write ("data"u8.ToArray ());
        writer.Write (0);
        writer.Flush ();
        return buffer.ToArray ();
    }

    [Fact]
    public void Load_throws_where_upstream_would_and_LoadAsync_reports_it ()
    {
        using (var nothing = new SoundPlayer ())
            nothing.Load ();   // nothing to load is not an error

        using (var missing = new SoundPlayer (Path.Combine (Path.GetTempPath (), "majorsilence-no-such-" + Guid.NewGuid ().ToString ("N") + ".wav")))
            Assert.Throws<FileNotFoundException> (missing.Load);

        using (var not_wave = new SoundPlayer (new MemoryStream ("not a wave file at all"u8.ToArray ())))
            Assert.Throws<InvalidOperationException> (not_wave.Load);

        // A real header loads, and leaves the stream where it was for the play that follows.
        var stream = new MemoryStream (Wave ());

        using (var good = new SoundPlayer (stream)) {
            good.Load ();
            Assert.Equal (0, stream.Position);
        }

        using var failing = new SoundPlayer (new MemoryStream ("RIFF....WAVX"u8.ToArray ()));
        Exception? reported = null;
        failing.LoadCompleted += (_, e) => reported = e.Error;
        failing.LoadAsync ();
        Assert.IsType<InvalidOperationException> (reported);
    }

    // ── AccessibilityNotifyClients ──────────────────────────────────────────────────────────────────

    [Fact]
    public void A_control_announcing_a_change_reaches_the_observer_watching_its_window ()
    {
        HeadlessRenderer.Use ();
        using var form = new Form { Width = 300, Height = 200 };
        var gauge = new Panel { Name = "gauge", Width = 80, Height = 40 };
        form.Controls.Add (gauge);
        HeadlessRenderer.CapturePng (form, 300, 200);

        using var other_form = new Form { Width = 100, Height = 100 };

        using var observer = new AutomationObserver (form);
        using var other_observer = new AutomationObserver (other_form);

        var values = new List<string?> ();
        var focuses = new List<string?> ();
        var strays = 0;
        observer.ValueChanged += (_, e) => values.Add (e?.AutomationId);
        observer.FocusChanged += (_, e) => focuses.Add (e?.AutomationId);
        other_observer.ValueChanged += (_, _) => strays++;

        // A panel has no text or checked state for the observer to watch, so without the call the
        // change is invisible to it.
        gauge.AccessibilityNotifyClients (AccessibleEvents.ValueChange, -1);
        gauge.AccessibilityNotifyClients (AccessibleEvents.Focus, -1);
        gauge.AccessibilityNotifyClients (AccessibleEvents.LocationChange, -1);

        Assert.Equal (new[] { "gauge" }, values);
        Assert.Equal (new[] { "gauge" }, focuses);
        Assert.Equal (0, strays);

        // The window's own overload announces through its root control.
        form.AccessibilityNotifyClients (AccessibleEvents.NameChange, -1);
        Assert.Equal (2, values.Count);
    }
}
