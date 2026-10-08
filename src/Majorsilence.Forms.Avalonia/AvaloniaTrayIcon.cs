#if !SINGLEVIEW
using System;
using System.Diagnostics;
using Avalonia.Controls;

namespace Majorsilence.Forms.Backends
{
    /// <summary>
    /// A <see cref="ITrayIconHandle"/> over Avalonia's <see cref="TrayIcon"/>: the Windows notification
    /// area, the macOS menu bar (NSStatusItem) and a Linux StatusNotifier host (TSM-19).
    /// </summary>
    /// <remarks>
    /// Avalonia reports far less than the shell does, and the gaps are documented rather than invented:
    /// <list type="bullet">
    /// <item><c>TrayIcon.Clicked</c> is a left-button release on Windows and Linux only, so a click
    /// raises MouseDown, MouseUp, Click and MouseClick for the left button; a second one within the
    /// double-click time raises DoubleClick and MouseDoubleClick instead, as the shell's sequence does.
    /// macOS never raises it: there the icon only opens its menu.</item>
    /// <item>The right-click is not reported at all; Avalonia opens <c>TrayIcon.Menu</c> itself. So
    /// <see cref="NotifyIcon.ContextMenuStrip"/> is converted to a <see cref="NativeMenu"/> (rebuilt
    /// when Avalonia asks, raising the strip's Opening first) and each item's click is forwarded to
    /// <see cref="MenuItem.PerformClick"/>.</item>
    /// <item>No pointer movement and no balloon API: ShowBalloonTip reports failure on every platform, so
    /// <see cref="NotifyIcon"/> writes its trace warning (use the WinForms or WPF host for shell balloons).
    /// It does not shell out to a notification utility: a UI library starting processes surprises
    /// sandboxed apps (Mac App Store, Flatpak) and puts caller text on a command line.</item>
    /// <item>On Linux the icon appears only where a StatusNotifier host runs (KDE, most other desktops;
    /// GNOME needs the AppIndicator extension). Avalonia cannot tell, so neither can this.</item>
    /// </list>
    /// </remarks>
    internal sealed class AvaloniaTrayIcon : ITrayIconHandle
    {
        private readonly NotifyIcon _owner;
        private readonly TrayIcon _tray;
        private ContextMenuStrip? _menu;
        private long _lastClickTicks;
        private bool _macMenuWarned;

        private AvaloniaTrayIcon (NotifyIcon owner)
        {
            _owner = owner;
            _tray = new TrayIcon { IsVisible = false };
            _tray.Clicked += OnClicked;
        }

        internal static ITrayIconHandle Create (NotifyIcon owner) => new AvaloniaTrayIcon (owner);

        public void SetVisible (bool visible)
        {
            _tray.IsVisible = visible;

            // macOS gives a status item no click event; with no menu the icon would do nothing at all.
            if (visible && _menu is null && OperatingSystem.IsMacOS () && !_macMenuWarned) {
                _macMenuWarned = true;
                Trace.TraceWarning ($"Majorsilence.Forms.NotifyIcon ('{_owner.Text}'): on macOS the tray icon raises no Click; give it a ContextMenuStrip so it can do something.");
            }
        }

        public void SetIcon (byte[]? iconPng)
            => _tray.Icon = iconPng is null ? null : new WindowIcon (new System.IO.MemoryStream (iconPng));

        public void SetText (string text) => _tray.ToolTipText = text;

        public void SetContextMenu (ContextMenuStrip? menu)
        {
            _menu = menu;

            if (menu is null) {
                _tray.Menu = null;
                return;
            }

            var native = new NativeMenu ();

            // Opening is where an application fills a dynamic menu; NeedsUpdate is Avalonia's "about to
            // show" for native menus, so the strip is re-read there.
            native.NeedsUpdate += (_, _) => {
                if (!ReferenceEquals (_menu, menu))
                    return;

                menu.RaiseOpeningCancelled ();
                Fill (native, menu.Items);
            };

            Fill (native, menu.Items);
            _tray.Menu = native;
        }

        private static void Fill (NativeMenu native, MenuItemCollection items)
        {
            native.Items.Clear ();

            foreach (var item in items) {
                if (item is ToolStripSeparator || item.Text == "-") {
                    native.Items.Add (new NativeMenuItemSeparator ());
                    continue;
                }

                var entry = new NativeMenuItem {
                    Header = ToAccessText (item.Text),
                    IsEnabled = item.Enabled,
                    IsVisible = item.Visible,
                };

                if (item.Checked) {
                    entry.ToggleType = MenuItemToggleType.CheckBox;
                    entry.IsChecked = true;
                }

                if (item.HasItems) {
                    var sub = new NativeMenu ();
                    Fill (sub, item.Items);
                    entry.Menu = sub;
                } else {
                    var source = item;
                    entry.Click += (_, _) => source.PerformClick ();
                }

                native.Items.Add (entry);
            }
        }

        // WinForms marks the access key with '&' ("&&" is a literal one); Avalonia with '_'.
        private static string ToAccessText (string text)
        {
            var sb = new System.Text.StringBuilder (text.Length);

            for (var i = 0; i < text.Length; i++) {
                var c = text[i];

                if (c == '&') {
                    if (i + 1 < text.Length && text[i + 1] == '&') {
                        sb.Append ('&');
                        i++;
                    } else {
                        sb.Append ('_');
                    }
                } else if (c == '_') {
                    sb.Append ("__");
                } else {
                    sb.Append (c);
                }
            }

            return sb.ToString ();
        }

        private void OnClicked (object? sender, EventArgs e)
        {
            var now = Environment.TickCount64;
            var second = now - _lastClickTicks <= SystemInformation.DoubleClickTime;
            _lastClickTicks = second ? 0 : now;

            _owner.HandleTrayMouseDown (MouseButtons.Left, second ? 2 : 1);
            _owner.HandleTrayMouseUp (MouseButtons.Left, System.Drawing.Point.Empty);
        }

        // Avalonia has no notification API; see the class remarks for why this does not start a process.
        public bool ShowBalloonTip (int timeout, string title, string text, ToolTipIcon icon) => false;

        public void Dispose ()
        {
            _tray.Clicked -= OnClicked;
            _tray.IsVisible = false;
            _tray.Dispose ();
        }
    }
}
#endif
