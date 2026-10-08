using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Drawing;
using Majorsilence.Forms.Backends;

namespace Majorsilence.Forms.Headless
{
    public sealed partial class HeadlessPlatformBackend : ITrayIconBackend
    {
        private readonly ConcurrentQueue<HeadlessTrayIcon> _trayIcons = new ();

        /// <summary>
        /// Gets or sets whether this backend offers a notification area. True by default; a test sets it
        /// false to stand in for a platform without one (GTK 4, browser, mobile, terminal), where
        /// <see cref="CreateTrayIcon"/> returns null and <see cref="NotifyIcon"/> reports the limitation.
        /// </summary>
        public bool TrayIconsSupported { get; set; } = true;

        /// <summary>Gets every tray icon this backend has created, in order, including disposed ones.</summary>
        public IReadOnlyList<HeadlessTrayIcon> TrayIcons => _trayIcons.ToArray ();

        /// <summary>Forgets <see cref="TrayIcons"/> between tests.</summary>
        public void ClearTrayIcons ()
        {
            while (_trayIcons.TryDequeue (out _)) { }
        }

        /// <inheritdoc/>
        public ITrayIconHandle? CreateTrayIcon (NotifyIcon owner)
        {
            if (!TrayIconsSupported)
                return null;

            var icon = new HeadlessTrayIcon (owner);
            _trayIcons.Enqueue (icon);
            return icon;
        }
    }

    /// <summary>One balloon tip a <see cref="HeadlessTrayIcon"/> was asked to show.</summary>
    public readonly record struct HeadlessBalloonTip (int Timeout, string Title, string Text, ToolTipIcon Icon);

    /// <summary>
    /// A recording fake of a notification-area icon: it keeps what <see cref="NotifyIcon"/> asked of it
    /// and lets a test raise the tray's own mouse messages and balloon notifications, which reach the
    /// owner through the same push path a real backend uses.
    /// </summary>
    public sealed class HeadlessTrayIcon : ITrayIconHandle
    {
        private readonly NotifyIcon _owner;
        private readonly List<HeadlessBalloonTip> _balloons = new ();

        internal HeadlessTrayIcon (NotifyIcon owner) => _owner = owner;

        /// <summary>Gets the <see cref="NotifyIcon"/> this icon belongs to.</summary>
        public NotifyIcon Owner => _owner;

        /// <summary>Gets whether the icon is currently in the (imaginary) tray.</summary>
        public bool IsVisible { get; private set; }

        /// <summary>Gets how many times the icon was added to the tray.</summary>
        public int ShowCount { get; private set; }

        /// <summary>Gets the PNG bytes of the current icon image, or null.</summary>
        public byte[]? IconPng { get; private set; }

        /// <summary>Gets the current tooltip text.</summary>
        public string Text { get; private set; } = string.Empty;

        /// <summary>Gets the menu handed over for native display (<see cref="ITrayIconHandle.SetContextMenu"/>).</summary>
        public ContextMenuStrip? ContextMenu { get; private set; }

        /// <summary>Gets every balloon tip shown, in order.</summary>
        public IReadOnlyList<HeadlessBalloonTip> Balloons => _balloons;

        /// <summary>Gets or sets whether <see cref="ITrayIconHandle.ShowBalloonTip"/> succeeds. True by default.</summary>
        public bool BalloonsSupported { get; set; } = true;

        /// <summary>Gets whether the icon has been disposed (removed for good).</summary>
        public bool IsDisposed { get; private set; }

        /// <inheritdoc/>
        public void SetVisible (bool visible)
        {
            if (visible && !IsVisible)
                ShowCount++;

            IsVisible = visible;
        }

        /// <inheritdoc/>
        public void SetIcon (byte[]? iconPng) => IconPng = iconPng;

        /// <inheritdoc/>
        public void SetText (string text) => Text = text;

        /// <inheritdoc/>
        public void SetContextMenu (ContextMenuStrip? menu) => ContextMenu = menu;

        /// <inheritdoc/>
        public bool ShowBalloonTip (int timeout, string title, string text, ToolTipIcon icon)
        {
            if (!BalloonsSupported)
                return false;

            _balloons.Add (new HeadlessBalloonTip (timeout, title, text, icon));
            _owner.HandleBalloonTipShown ();
            return true;
        }

        /// <inheritdoc/>
        public void Dispose ()
        {
            IsVisible = false;
            IsDisposed = true;
        }

        /// <summary>Clicks the icon with <paramref name="button"/>: the press and release a shell reports.</summary>
        public void Click (MouseButtons button = MouseButtons.Left) => Click (button, Point.Empty);

        /// <summary>Clicks the icon with <paramref name="button"/>, the release at <paramref name="screenLocation"/> (where a right-click's menu opens).</summary>
        public void Click (MouseButtons button, Point screenLocation)
        {
            _owner.HandleTrayMouseDown (button, 1);
            _owner.HandleTrayMouseUp (button, screenLocation);
        }

        /// <summary>Double-clicks the icon: the shell's down, up, double-click down, up.</summary>
        public void DoubleClick (MouseButtons button = MouseButtons.Left)
        {
            Click (button);
            _owner.HandleTrayMouseDown (button, 2);
            _owner.HandleTrayMouseUp (button, Point.Empty);
        }

        /// <summary>Moves the pointer over the icon.</summary>
        public void MouseMove () => _owner.HandleTrayMouseMove ();

        /// <summary>Clicks the balloon tip that is showing.</summary>
        public void ClickBalloon () => _owner.HandleBalloonTipClicked ();

        /// <summary>Closes (or times out) the balloon tip that is showing.</summary>
        public void CloseBalloon () => _owner.HandleBalloonTipClosed ();
    }
}
