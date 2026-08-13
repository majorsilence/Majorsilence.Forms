using System.Collections.Generic;
using Majorsilence.Forms.Headless;
using Xunit;

namespace Majorsilence.Forms.Tests;

// Application.AddMessageFilter/RemoveMessageFilter were empty stubs and FilterMessage hard-coded false,
// so a filter registered the WinForms way silently never ran. That matters beyond the API being
// inert: watching input application-wide is what ported code needs in order to dismiss a popup on an
// outside click, and with no working filter the only remaining option is a global OS hook -- which is
// exactly the SetWindowsHookEx P/Invoke that aborts the process off Windows.
public class MessageFilterTests
{
    private sealed class RecordingFilter : IMessageFilter
    {
        public List<int> Seen { get; } = [];
        public bool Consume { get; init; }

        public bool PreFilterMessage (ref Message m)
        {
            Seen.Add (m.Msg);
            return Consume;
        }
    }

    [Fact]
    public void Filter_SeesMouseAndKeyboardInput ()
    {
        HeadlessRenderer.Use ();
        var filter = new RecordingFilter ();
        Application.AddMessageFilter (filter);
        try {
            using var form = new Form { Size = new System.Drawing.Size (200, 150) };
            form.Show ();

            HeadlessRenderer.MouseDown (form, 20, 20);
            HeadlessRenderer.MouseUp (form, 20, 20);
            HeadlessRenderer.KeyDown (form, Keys.A);

            Assert.Contains (WindowMessages.WM_LBUTTONDOWN, filter.Seen);
            Assert.Contains (WindowMessages.WM_LBUTTONUP, filter.Seen);
            Assert.Contains (WindowMessages.WM_KEYDOWN, filter.Seen);
        } finally {
            Application.RemoveMessageFilter (filter);
        }
    }

    [Fact]
    public void Filter_ReportsTheClickCoordinatesInLParam ()
    {
        HeadlessRenderer.Use ();
        var seen = new List<System.IntPtr> ();
        var filter = new DelegateFilter (m => { seen.Add (m.LParam); return false; });
        Application.AddMessageFilter (filter);
        try {
            using var form = new Form { Size = new System.Drawing.Size (200, 150) };
            form.Show ();

            HeadlessRenderer.MouseDown (form, 37, 22);

            // Packed the way MAKELPARAM does, so the usual LOWORD/HIWORD unpacking works.
            Assert.Contains ((System.IntPtr)((22 << 16) | 37), seen);
        } finally {
            Application.RemoveMessageFilter (filter);
        }
    }

    [Fact]
    public void Filter_ReturningTrue_StopsTheInputReachingTheControl ()
    {
        HeadlessRenderer.Use ();
        var filter = new RecordingFilter { Consume = true };
        Application.AddMessageFilter (filter);
        try {
            using var form = new Form { Size = new System.Drawing.Size (200, 150) };
            var clicks = 0;
            var button = new Button { Text = "b", Left = 10, Top = 10, Width = 80, Height = 30 };
            button.Click += (s, e) => clicks++;
            form.Controls.Add (button);
            form.Show ();

            HeadlessRenderer.Click (form, 20, 20);

            Assert.Equal (0, clicks);
        } finally {
            Application.RemoveMessageFilter (filter);
        }
    }

    [Fact]
    public void RemovedFilter_StopsBeingCalled ()
    {
        HeadlessRenderer.Use ();
        var filter = new RecordingFilter ();
        Application.AddMessageFilter (filter);

        using var form = new Form { Size = new System.Drawing.Size (200, 150) };
        form.Show ();
        HeadlessRenderer.MouseDown (form, 20, 20);
        var afterFirst = filter.Seen.Count;
        Assert.True (afterFirst > 0, "filter never ran while registered");

        Application.RemoveMessageFilter (filter);
        HeadlessRenderer.MouseDown (form, 30, 30);

        Assert.Equal (afterFirst, filter.Seen.Count);
    }

    private sealed class DelegateFilter (System.Func<Message, bool> onMessage) : IMessageFilter
    {
        public bool PreFilterMessage (ref Message m) => onMessage (m);
    }
}
