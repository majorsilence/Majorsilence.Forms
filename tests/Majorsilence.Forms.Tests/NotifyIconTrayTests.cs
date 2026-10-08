using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Linq;
using Majorsilence.Forms.Backends;
using Majorsilence.Forms.Headless;
using SkiaSharp;
using Xunit;

namespace Majorsilence.Forms.Tests;

// TSM-19 (docs/behaviour-gap/toolstrip.md, #351): NotifyIcon reaches the platform's tray through
// ITrayIconBackend. These drive the Headless recording fake, which reports the tray's own mouse messages
// through the same push path a real backend uses; the event order is upstream's NotifyIcon WndProc /
// WmMouseDown / WmMouseUp.
[Collection ("Headless")]
public sealed class NotifyIconTrayTests : IDisposable
{
    private readonly HeadlessPlatformBackend backend;

    public NotifyIconTrayTests ()
    {
        HeadlessRenderer.Use ();
        backend = (HeadlessPlatformBackend) Platform.Backend;
        backend.ClearTrayIcons ();
    }

    public void Dispose ()
    {
        backend.TrayIconsSupported = true;
        backend.ClearTrayIcons ();
    }

    private static Majorsilence.Forms.Drawing.Icon NewIcon ()
    {
        var bitmap = new SKBitmap (16, 16);
        bitmap.Erase (SKColors.Red);
        return new Majorsilence.Forms.Drawing.Icon (bitmap);
    }

    private HeadlessTrayIcon Tray => Assert.Single (backend.TrayIcons);

    [Fact]
    public void Visible_with_an_icon_puts_it_in_the_tray ()
    {
        using var notify = new NotifyIcon { Text = "App", Icon = NewIcon () };
        Assert.Empty (backend.TrayIcons);   // nothing is created until it has to be shown

        notify.Visible = true;

        Assert.True (Tray.IsVisible);
        Assert.Equal (1, Tray.ShowCount);
        Assert.Equal ("App", Tray.Text);
        Assert.NotNull (Tray.IconPng);

        notify.Text = "Renamed";
        Assert.Equal ("Renamed", Tray.Text);

        notify.Visible = false;
        Assert.False (Tray.IsVisible);
    }

    [Fact]
    public void Visible_without_an_icon_adds_nothing_as_upstream ()
    {
        using var notify = new NotifyIcon { Visible = true };

        Assert.Empty (backend.TrayIcons);

        notify.Icon = NewIcon ();   // the icon arriving later is what adds it
        Assert.True (Tray.IsVisible);

        notify.Icon = null;
        Assert.False (Tray.IsVisible);
    }

    private static List<string> Record (NotifyIcon notify)
    {
        var log = new List<string> ();
        notify.MouseDown += (_, e) => log.Add ($"MouseDown {e.Button} {e.Clicks}");
        notify.MouseUp += (_, e) => log.Add ($"MouseUp {e.Button}");
        notify.Click += (_, e) => log.Add ($"Click {((MouseEventArgs) e).Button}");
        notify.MouseClick += (_, e) => log.Add ($"MouseClick {e.Button}");
        notify.DoubleClick += (_, _) => log.Add ("DoubleClick");
        notify.MouseDoubleClick += (_, e) => log.Add ($"MouseDoubleClick {e.Button}");
        return log;
    }

    [Fact]
    public void A_click_on_the_tray_raises_Click_and_MouseClick ()
    {
        using var notify = new NotifyIcon { Icon = NewIcon (), Visible = true };
        var log = Record (notify);

        Tray.Click ();

        Assert.Equal (new[] { "MouseDown Left 1", "MouseUp Left", "Click Left", "MouseClick Left" }, log);
    }

    [Fact]
    public void A_double_click_raises_DoubleClick_and_no_second_Click ()
    {
        using var notify = new NotifyIcon { Icon = NewIcon (), Visible = true };
        var log = Record (notify);

        Tray.DoubleClick ();

        Assert.Equal (new[] {
            "MouseDown Left 1", "MouseUp Left", "Click Left", "MouseClick Left",
            "DoubleClick", "MouseDoubleClick Left", "MouseDown Left 2", "MouseUp Left",
        }, log);
    }

    [Fact]
    public void A_right_click_shows_the_ContextMenuStrip_at_the_pointer ()
    {
        using var form = new Form { Width = 300, Height = 200 };
        form.Show ();
        var menu = new ContextMenuStrip ();
        menu.Items.Add ("Restore");
        using var notify = new NotifyIcon { Icon = NewIcon (), Visible = true, ContextMenuStrip = menu };
        var log = Record (notify);
        menu.Opened += (_, _) => log.Add ("Opened");

        Tray.Click (MouseButtons.Right, new Point (500, 400));

        try {
            Assert.Null (menu.SourceControl);
            // Upstream shows the menu before the release's own events (WM_RBUTTONUP).
            Assert.Equal (new[] { "MouseDown Right 1", "Opened", "MouseUp Right", "Click Right", "MouseClick Right" }, log);
        } finally {
            menu.Close ();
        }
    }

    [Fact]
    public void The_ContextMenuStrip_is_handed_to_the_backend ()
    {
        var menu = new ContextMenuStrip ();
        using var notify = new NotifyIcon { ContextMenuStrip = menu, Icon = NewIcon (), Visible = true };

        Assert.Same (menu, Tray.ContextMenu);

        notify.ContextMenuStrip = null;
        Assert.Null (Tray.ContextMenu);
    }

    [Fact]
    public void ShowBalloonTip_reaches_the_tray_and_its_events_come_back ()
    {
        using var notify = new NotifyIcon { Icon = NewIcon (), Visible = true };
        var log = new List<string> ();
        notify.BalloonTipShown += (_, _) => log.Add ("Shown");
        notify.BalloonTipClicked += (_, _) => log.Add ("Clicked");
        notify.BalloonTipClosed += (_, _) => log.Add ("Closed");

        notify.ShowBalloonTip (3000, "Title", "Body", ToolTipIcon.Warning);
        Tray.ClickBalloon ();
        Tray.CloseBalloon ();

        Assert.Equal (new HeadlessBalloonTip (3000, "Title", "Body", ToolTipIcon.Warning), Assert.Single (Tray.Balloons));
        Assert.Equal (new[] { "Shown", "Clicked", "Closed" }, log);
    }

    [Fact]
    public void ShowBalloonTip_while_not_in_the_tray_shows_nothing ()
    {
        using var notify = new NotifyIcon { Icon = NewIcon (), Visible = true };
        notify.Visible = false;

        notify.ShowBalloonTip (0, "Title", "Body", ToolTipIcon.Info);

        Assert.Empty (Tray.Balloons);
    }

    [Fact]
    public void Dispose_removes_the_icon ()
    {
        var notify = new NotifyIcon { Icon = NewIcon (), Visible = true };

        notify.Dispose ();

        Assert.False (Tray.IsVisible);
        Assert.True (Tray.IsDisposed);
    }

    private sealed class Capture : TraceListener
    {
        public readonly List<string> Lines = new ();
        public override void Write (string? message) { }
        public override void WriteLine (string? message) { if (message is not null) Lines.Add (message); }
        public override void TraceEvent (TraceEventCache? eventCache, string source, TraceEventType eventType, int id, string? message)
        {
            if (message is not null)
                Lines.Add (message);
        }
    }

    private static List<string> CaptureTrace (Action action)
    {
        var capture = new Capture ();
        Trace.Listeners.Add (capture);

        try {
            action ();
        } finally {
            Trace.Listeners.Remove (capture);
        }

        return capture.Lines.Where (l => l.Contains ("NotifyIcon")).ToList ();
    }

    [Fact]
    public void Without_a_tray_Visible_says_so_through_Trace ()
    {
        backend.TrayIconsSupported = false;
        using var notify = new NotifyIcon { Text = "Hidden app", Icon = NewIcon () };

        var lines = CaptureTrace (() => {
            notify.Visible = true;
            notify.Visible = false;
            notify.Visible = true;   // reported once per icon, not on every toggle
        });

        var line = Assert.Single (lines);
        Assert.Contains ("Hidden app", line);
        Assert.Contains ("notification area", line);
        Assert.Empty (backend.TrayIcons);
        Assert.True (notify.Visible);   // the property still round-trips
    }

    [Fact]
    public void A_balloon_the_platform_cannot_show_is_reported ()
    {
        using var notify = new NotifyIcon { Icon = NewIcon (), Visible = true };
        Tray.BalloonsSupported = false;
        var shown = 0;
        notify.BalloonTipShown += (_, _) => shown++;

        var lines = CaptureTrace (() => notify.ShowBalloonTip (0, "Title", "Body", ToolTipIcon.None));

        Assert.Contains ("balloon", Assert.Single (lines));
        Assert.Equal (0, shown);
    }
}
