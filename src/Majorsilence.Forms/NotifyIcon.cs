using System;
using System.ComponentModel;
using System.Drawing;
using SkiaSharp;

#pragma warning disable CA1416  // WinForms compat — intentionally uses Windows-only System.Drawing types

namespace Majorsilence.Forms
{
    /// <summary>
    /// WinForms compatibility: represents a notification-area (system-tray) icon.
    /// </summary>
    /// <remarks>
    /// Backed by the platform backend's <see cref="Backends.ITrayIconBackend"/> where it has one: the
    /// Avalonia desktop backend (<c>Avalonia.Controls.TrayIcon</c>, Windows/macOS/Linux) and the WinForms
    /// and WPF hosts (the real <c>System.Windows.Forms.NotifyIcon</c>). GTK 4, the browser, Android, iOS
    /// and the terminal have no tray, so there the icon is never shown and <see cref="Visible"/> = true
    /// writes a <see cref="System.Diagnostics.Trace"/> warning -- an application that hides its window
    /// and relies on the icon to come back would otherwise be unreachable without a word.
    /// </remarks>
    public partial class NotifyIcon : Component
    {
        private string _text = string.Empty;
        private bool _visible;
        private ToolTipIcon _balloonTipIcon;
        private Majorsilence.Forms.Drawing.Icon? _icon;
        private ContextMenuStrip? _contextMenuStrip;

        // The native icon, created the first time it has to be shown. The two flags keep each
        // limitation warning to one per icon.
        private Backends.ITrayIconHandle? _handle;
        private bool _added;
        private bool _noTrayReported;
        private bool _noBalloonReported;

        // Upstream's _doubleClick: the mouse-up that ends a double-click raises no Click.
        private bool _doubleClick;

        /// <summary>The maximum number of characters allowed in the <see cref="Text"/> property.</summary>
        public const int MaxTextSize = 63;

        /// <summary>Initializes a new instance of NotifyIcon.</summary>
        public NotifyIcon () { }

        /// <summary>Initializes a new instance of NotifyIcon and adds it to the specified container.</summary>
        public NotifyIcon (IContainer container)
        {
            Guard.ThrowIfNull (container);

            container.Add (this);
        }

        /// <summary>Gets or sets the icon displayed in the notification area.</summary>
        /// <remarks>As upstream, the icon only appears while this is set and <see cref="Visible"/> is true.</remarks>
        public Majorsilence.Forms.Drawing.Icon? Icon {
            get => _icon;
            set {
                if (ReferenceEquals (_icon, value))
                    return;

                _icon = value;
                _handle?.SetIcon (ToPng (value));
                UpdateIcon (_visible);
            }
        }

        /// <summary>Gets or sets the ToolTip text displayed when the mouse hovers over the icon.</summary>
        public string Text {
            get => _text;
            set {
                value ??= string.Empty;

                if (value.Length > MaxTextSize)
                    throw new ArgumentOutOfRangeException (nameof (Text), $"'{nameof (Text)}' must be {MaxTextSize} characters or fewer.");

                if (value == _text)
                    return;

                _text = value;
                _handle?.SetText (value);
            }
        }

        /// <summary>Gets or sets an object that contains data about the control.</summary>
        public object? Tag { get; set; }

        /// <summary>Gets or sets whether the icon is visible in the notification area.</summary>
        /// <remarks>
        /// Where the platform has no tray (see the class remarks) setting this to true writes a
        /// <see cref="System.Diagnostics.Trace"/> warning once, and the icon stays absent.
        /// </remarks>
        public bool Visible {
            get => _visible;
            set {
                if (_visible == value)
                    return;

                // Upstream updates the shell before storing (NotifyIcon.cs Visible).
                UpdateIcon (value);
                _visible = value;
            }
        }

        /// <summary>Gets or sets the context menu that appears when the user right-clicks the icon.</summary>
        public ContextMenuStrip? ContextMenuStrip {
            get => _contextMenuStrip;
            set {
                _contextMenuStrip = value;
                _handle?.SetContextMenu (value);
            }
        }

        /// <summary>Occurs when the user clicks the icon.</summary>
        public event EventHandler? Click;

        /// <summary>Occurs when the user double-clicks the icon.</summary>
        public event EventHandler? DoubleClick;

        /// <summary>Occurs when the user clicks the icon with the mouse.</summary>
        public event EventHandler<MouseEventArgs>? MouseClick;

        /// <summary>Occurs when the user double-clicks the icon with the mouse.</summary>
        public event EventHandler<MouseEventArgs>? MouseDoubleClick;

        /// <summary>Occurs when the user moves the mouse over the icon.</summary>
        public event EventHandler<MouseEventArgs>? MouseMove;

        /// <summary>Occurs when the balloon tip is clicked.</summary>
        public event EventHandler? BalloonTipClicked;

        /// <summary>Occurs when the balloon tip closes.</summary>
        public event EventHandler? BalloonTipClosed;

        /// <summary>Occurs when the balloon tip is shown.</summary>
        public event EventHandler? BalloonTipShown;

        private string _balloonTipTitle = string.Empty;
        private string _balloonTipText = string.Empty;

        /// <summary>Gets or sets the title displayed on the balloon tooltip.</summary>
        public string BalloonTipTitle {
            get => _balloonTipTitle;
            set => _balloonTipTitle = value ?? string.Empty;
        }

        /// <summary>Gets or sets the text displayed on the balloon tooltip.</summary>
        public string BalloonTipText {
            get => _balloonTipText;
            set => _balloonTipText = value ?? string.Empty;
        }

        /// <summary>Gets or sets the icon shown on the balloon tooltip.</summary>
        public ToolTipIcon BalloonTipIcon {
            get => _balloonTipIcon;
            set {
                if (value < ToolTipIcon.None || value > ToolTipIcon.Error)
                    throw new InvalidEnumArgumentException (nameof (value), (int)value, typeof (ToolTipIcon));

                _balloonTipIcon = value;
            }
        }

        /// <summary>
        /// Displays a balloon notification for the specified duration using the current
        /// <see cref="BalloonTipTitle"/>, <see cref="BalloonTipText"/> and <see cref="BalloonTipIcon"/>.
        /// </summary>
        /// <remarks>See <see cref="ShowBalloonTip(int, string, string, ToolTipIcon)"/>.</remarks>
        public void ShowBalloonTip (int timeout)
        {
            ShowBalloonTip (timeout, BalloonTipTitle, BalloonTipText, BalloonTipIcon);
        }

        /// <summary>Displays a balloon notification with the specified title and text.</summary>
        /// <remarks>
        /// As upstream, nothing is shown while the icon is not in the tray. The WinForms and WPF hosts
        /// show the shell balloon (a toast on Windows 10+) and raise the <c>BalloonTip*</c> events from
        /// it. Avalonia has no notification API, so there (and on GTK 4, browser, mobile and terminal)
        /// nothing is shown and a <see cref="System.Diagnostics.Trace"/> warning says so.
        /// </remarks>
        public void ShowBalloonTip (int timeout, string tipTitle, string tipText, ToolTipIcon tipIcon)
        {
            if (timeout < 0)
                throw new ArgumentOutOfRangeException (nameof (timeout), timeout, $"'{nameof (timeout)}' must be greater than or equal to 0.");

            if (string.IsNullOrEmpty (tipText))
                throw new ArgumentException ($"'{nameof (tipText)}' must not be null or empty.", nameof (tipText));

            if (tipIcon < ToolTipIcon.None || tipIcon > ToolTipIcon.Error)
                throw new InvalidEnumArgumentException (nameof (tipIcon), (int)tipIcon, typeof (ToolTipIcon));

            if (!_added || _handle is null)
                return;

            if (!_handle.ShowBalloonTip (timeout, tipTitle ?? string.Empty, tipText, tipIcon))
                ReportLimitation ("cannot show a balloon notification on this platform; the tip was dropped", ref _noBalloonReported);
        }

        /// <inheritdoc/>
        protected override void Dispose (bool disposing)
        {
            if (disposing) {
                // Upstream removes the shell icon here (NotifyIcon.cs Dispose).
                UpdateIcon (false);
                _handle?.Dispose ();
                _handle = null;
                _contextMenuStrip = null;

                _icon?.Dispose ();
                _icon = null;
            }

            base.Dispose (disposing);
        }

        // Upstream's UpdateIcon: the icon is in the tray only while it is visible AND has an image
        // (NotifyIcon.cs UpdateIcon -- 'showIconInTray && _icon is not null').
        private void UpdateIcon (bool showIconInTray)
        {
            var show = showIconInTray && _icon is not null;

            if (show && _handle is null && !TryCreateHandle ())
                return;

            if (_handle is null || show == _added)
                return;

            _handle.SetVisible (show);
            _added = show;
        }

        private bool TryCreateHandle ()
        {
            Backends.ITrayIconBackend? tray;

            try {
                tray = Backends.Platform.Backend as Backends.ITrayIconBackend;
            } catch (InvalidOperationException) {
                tray = null;   // no backend referenced at all
            }

            if (tray is not null) {
                // A tray-only application can make its icon visible before any window has initialised
                // the platform; Initialize is idempotent by contract.
                Backends.Platform.Backend.Initialize ();
                _handle = tray.CreateTrayIcon (this);
            }

            if (_handle is null) {
                ReportLimitation ("has no notification area on this platform, so the icon is not shown. A window hidden behind it cannot be restored from the tray", ref _noTrayReported);
                return false;
            }

            _handle.SetIcon (ToPng (_icon));
            _handle.SetText (_text);
            _handle.SetContextMenu (_contextMenuStrip);
            return true;
        }

        // Trace, not Debug: a release build of a "minimise to tray" application is exactly where a
        // missing tray needs to be visible to whoever reads its log.
        private void ReportLimitation (string what, ref bool reported)
        {
            if (reported)
                return;

            reported = true;
            var backend = Backends.Platform.ConfiguredBackend?.Name ?? "no backend";
            System.Diagnostics.Trace.TraceWarning ($"Majorsilence.Forms.NotifyIcon ('{_text}'): the {backend} backend {what}.");
        }

        private static byte[]? ToPng (Majorsilence.Forms.Drawing.Icon? icon)
        {
            if (icon is null)
                return null;

            using var bitmap = icon.ToBitmap ();
            using var sk = bitmap.ToSKBitmap ();

            if (sk is null)
                return null;

            using var ms = new System.IO.MemoryStream ();
            sk.Encode (ms, SKEncodedImageFormat.Png, 100);
            return ms.ToArray ();
        }

        // ── Push side: the backend reports the tray's own mouse messages here, as a window backend
        //    calls WindowBase.HandlePointerPressed. The sequencing is upstream's WndProc / WmMouseDown /
        //    WmMouseUp (NotifyIcon.cs).

        /// <summary>A mouse button went down on the icon; <paramref name="clicks"/> is 2 for the second press of a double-click.</summary>
        internal void HandleTrayMouseDown (MouseButtons button, int clicks)
        {
            if (clicks == 2) {
                DoubleClick?.Invoke (this, new MouseEventArgs (button, 2, 0, 0, 0));
                MouseDoubleClick?.Invoke (this, new MouseEventArgs (button, 2, 0, 0, 0));
                _doubleClick = true;
            }

            OnMouseDown (new MouseEventArgs (button, clicks, 0, 0, 0));
        }

        /// <summary>
        /// A mouse button came up on the icon, at <paramref name="screenLocation"/> (screen pixels). A
        /// right button shows <see cref="ContextMenuStrip"/> there first, as upstream's WM_RBUTTONUP does.
        /// </summary>
        internal void HandleTrayMouseUp (MouseButtons button, System.Drawing.Point screenLocation)
        {
            if (button == MouseButtons.Right && _contextMenuStrip is not null)
                ShowContextMenu (screenLocation);

            OnMouseUp (new MouseEventArgs (button, 0, 0, 0, 0));

            if (!_doubleClick) {
                Click?.Invoke (this, new MouseEventArgs (button, 0, 0, 0, 0));
                MouseClick?.Invoke (this, new MouseEventArgs (button, 0, 0, 0, 0));
            }

            _doubleClick = false;
        }

        /// <summary>The pointer moved over the icon.</summary>
        internal void HandleTrayMouseMove () => MouseMove?.Invoke (this, new MouseEventArgs (Control.MouseButtons, 0, 0, 0, 0));

        /// <summary>The balloon / notification appeared.</summary>
        internal void HandleBalloonTipShown () => BalloonTipShown?.Invoke (this, EventArgs.Empty);

        /// <summary>The balloon / notification was dismissed or timed out.</summary>
        internal void HandleBalloonTipClosed () => BalloonTipClosed?.Invoke (this, EventArgs.Empty);

        /// <summary>The user clicked the balloon / notification.</summary>
        internal void HandleBalloonTipClicked () => BalloonTipClicked?.Invoke (this, EventArgs.Empty);

        private void ShowContextMenu (System.Drawing.Point screenLocation)
        {
            // The popup hangs off a window (ContextMenu.Show (Point) uses the active or main form, which
            // a minimised-to-tray application still has, hidden). With no form at all there is nothing
            // to host it, so that is reported rather than thrown out of a tray click.
            try {
                _contextMenuStrip!.Show (screenLocation);
            } catch (InvalidOperationException ex) {
                System.Diagnostics.Trace.TraceWarning ($"Majorsilence.Forms.NotifyIcon ('{_text}'): the context menu could not be shown: {ex.Message}");
            }
        }
    }

    /// <summary>
    /// Specifies the icon shown on a balloon tooltip from a NotifyIcon.
    /// </summary>
    public enum ToolTipIcon
    {
        /// <summary>No icon.</summary>
        None,
        /// <summary>An information icon.</summary>
        Info,
        /// <summary>A warning icon.</summary>
        Warning,
        /// <summary>An error icon.</summary>
        Error
    }
}
