using System;
using System.ComponentModel;
using System.Drawing;
using SkiaSharp;

#pragma warning disable CA1416  // WinForms compat — intentionally uses Windows-only System.Drawing types

namespace Majorsilence.Forms
{
    /// <summary>
    /// WinForms compatibility: represents a notification-area (system-tray) icon.
    /// Majorsilence.Forms has no native tray support, so this is a stub that keeps the API surface
    /// compatible without crashing. ShowBalloonTip is a no-op; Click events never fire.
    /// </summary>
    public class NotifyIcon : Component
    {
        private string _text = string.Empty;
        private bool _visible;
        private ToolTipIcon _balloonTipIcon;

        /// <summary>The maximum number of characters allowed in the <see cref="Text"/> property.</summary>
        public const int MaxTextSize = 63;

        /// <summary>Initializes a new instance of NotifyIcon.</summary>
        public NotifyIcon () { }

        /// <summary>Initializes a new instance of NotifyIcon and adds it to the specified container.</summary>
        public NotifyIcon (IContainer container)
        {
            if (container is null)
                throw new ArgumentNullException (nameof (container));

            container.Add (this);
        }

        /// <summary>Gets or sets the icon displayed in the notification area.</summary>
        public Majorsilence.Drawing.Icon? Icon { get; set; }

        /// <summary>Gets or sets the ToolTip text displayed when the mouse hovers over the icon.</summary>
        public string Text {
            get => _text;
            set {
                value ??= string.Empty;

                if (value.Length > MaxTextSize)
                    throw new ArgumentOutOfRangeException (nameof (Text), $"'{nameof (Text)}' must be {MaxTextSize} characters or fewer.");

                _text = value;
            }
        }

        /// <summary>Gets or sets an object that contains data about the control.</summary>
        public object? Tag { get; set; }

        /// <summary>Gets or sets whether the icon is visible in the notification area.</summary>
        public bool Visible {
            get => _visible;
            set => _visible = value;
        }

        /// <summary>Gets or sets the context menu that appears when the user right-clicks the icon.</summary>
        public ContextMenuStrip? ContextMenuStrip { get; set; }

        /// <summary>Occurs when the user clicks the icon.</summary>
        public event EventHandler? Click { add { } remove { } }

        /// <summary>Occurs when the user double-clicks the icon.</summary>
        public event EventHandler? DoubleClick { add { } remove { } }

        /// <summary>Occurs when the user clicks the icon with the mouse.</summary>
        public event EventHandler<MouseEventArgs>? MouseClick { add { } remove { } }

        /// <summary>Occurs when the user double-clicks the icon with the mouse.</summary>
        public event EventHandler<MouseEventArgs>? MouseDoubleClick { add { } remove { } }

        /// <summary>Occurs when the user moves the mouse over the icon.</summary>
        public event EventHandler<MouseEventArgs>? MouseMove { add { } remove { } }

        /// <summary>Occurs when the balloon tip is clicked.</summary>
        public event EventHandler? BalloonTipClicked { add { } remove { } }

        /// <summary>Occurs when the balloon tip closes.</summary>
        public event EventHandler? BalloonTipClosed { add { } remove { } }

        /// <summary>Occurs when the balloon tip is shown.</summary>
        public event EventHandler? BalloonTipShown { add { } remove { } }

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
        /// Majorsilence.Forms has no native tray support, so no balloon is shown, but argument validation
        /// matches WinForms.
        /// </summary>
        public void ShowBalloonTip (int timeout)
        {
            ShowBalloonTip (timeout, BalloonTipTitle, BalloonTipText, BalloonTipIcon);
        }

        /// <summary>
        /// Displays a balloon notification with the specified title and text. Majorsilence.Forms has no
        /// native tray support, so no balloon is shown, but argument validation matches WinForms.
        /// </summary>
        public void ShowBalloonTip (int timeout, string tipTitle, string tipText, ToolTipIcon tipIcon)
        {
            if (timeout < 0)
                throw new ArgumentOutOfRangeException (nameof (timeout), timeout, $"'{nameof (timeout)}' must be greater than or equal to 0.");

            if (string.IsNullOrEmpty (tipText))
                throw new ArgumentException ($"'{nameof (tipText)}' must not be null or empty.", nameof (tipText));

            if (tipIcon < ToolTipIcon.None || tipIcon > ToolTipIcon.Error)
                throw new InvalidEnumArgumentException (nameof (tipIcon), (int)tipIcon, typeof (ToolTipIcon));
        }

        /// <inheritdoc/>
        protected override void Dispose (bool disposing)
        {
            if (disposing) {
                Icon?.Dispose ();
                Icon = null;
            }

            base.Dispose (disposing);
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
