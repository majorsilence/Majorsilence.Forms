using System;
using System.Runtime.InteropServices;
using Majorsilence.Forms.Backends;
using WF = System.Windows.Forms;
using MF = Majorsilence.Forms;

// Compiled into both Windows hosts: Majorsilence.Forms.WinForms owns it and Majorsilence.Forms.Wpf links
// it (WPF has no tray icon of its own; WPF applications use this same WinForms type).
namespace Majorsilence.Forms.Backends.Shell
{
    /// <summary>
    /// A <see cref="ITrayIconHandle"/> over the real <see cref="WF.NotifyIcon"/>, i.e. the shell's
    /// <c>Shell_NotifyIcon</c>. The WinForms icon raises nothing itself that reaches the application:
    /// its raw mouse-down / mouse-up and balloon notifications are forwarded to the owning
    /// <see cref="MF.NotifyIcon"/>, which derives Click / DoubleClick / MouseClick and shows its own
    /// <see cref="MF.ContextMenuStrip"/> exactly as upstream's WndProc does.
    /// </summary>
    internal sealed class ShellTrayIcon : ITrayIconHandle
    {
        private readonly MF.NotifyIcon _owner;
        private readonly WF.NotifyIcon _icon = new ();
        private System.Drawing.Icon? _image;

        internal ShellTrayIcon (MF.NotifyIcon owner)
        {
            _owner = owner;

            // WF.NotifyIcon's MouseDown carries Clicks = 2 for WM_xBUTTONDBLCLK (NotifyIcon.cs WmMouseDown),
            // which is all the owner needs to rebuild the double-click sequence.
            _icon.MouseDown += (_, e) => _owner.HandleTrayMouseDown ((MF.MouseButtons) (int) e.Button, e.Clicks);
            _icon.MouseUp += (_, e) => {
                // The tray message carries no point; upstream's ShowContextMenu reads GetCursorPos too.
                var at = WF.Control.MousePosition;
                _owner.HandleTrayMouseUp ((MF.MouseButtons) (int) e.Button, new System.Drawing.Point (at.X, at.Y));
            };
            _icon.MouseMove += (_, _) => _owner.HandleTrayMouseMove ();
            _icon.BalloonTipShown += (_, _) => _owner.HandleBalloonTipShown ();
            _icon.BalloonTipClosed += (_, _) => _owner.HandleBalloonTipClosed ();
            _icon.BalloonTipClicked += (_, _) => _owner.HandleBalloonTipClicked ();
        }

        public void SetVisible (bool visible) => _icon.Visible = visible;

        public void SetIcon (byte[]? iconPng)
        {
            var previous = _image;
            _image = FromPng (iconPng);
            _icon.Icon = _image;
            previous?.Dispose ();
        }

        public void SetText (string text) => _icon.Text = text;

        // The owner shows its own Majorsilence.Forms menu on the right-button release.
        public void SetContextMenu (MF.ContextMenuStrip? menu) { }

        public bool ShowBalloonTip (int timeout, string title, string text, MF.ToolTipIcon icon)
        {
            // ToolTipIcon has the same four values in the same order in both toolkits.
            _icon.ShowBalloonTip (timeout, title, text, (WF.ToolTipIcon) (int) icon);
            return true;
        }

        public void Dispose ()
        {
            _icon.Visible = false;
            _icon.Dispose ();
            _image?.Dispose ();
            _image = null;
        }

        // The same PNG -> HICON route WinFormsWindowHost.SetIcon takes for a window icon.
        private static System.Drawing.Icon? FromPng (byte[]? png)
        {
            if (png is null || png.Length == 0)
                return null;

            try {
                using var stream = new System.IO.MemoryStream (png);
                using var bitmap = new System.Drawing.Bitmap (stream);
                var hIcon = bitmap.GetHicon ();

                try {
                    using var icon = System.Drawing.Icon.FromHandle (hIcon);
                    return (System.Drawing.Icon) icon.Clone ();   // own our copy; free the GDI handle below
                } finally {
                    _ = DestroyIcon (hIcon);
                }
            } catch (Exception) {
                return null;   // an undecodable image leaves the icon blank, as a window icon does
            }
        }

        [DllImport ("user32.dll", SetLastError = true)]
        private static extern bool DestroyIcon (IntPtr hIcon);
    }
}
