using System.ComponentModel;
using System.Drawing;
using SkiaSharp;

#pragma warning disable CA1416  // WinForms compat — intentionally uses Windows-only System.Drawing types

namespace Modern.Forms
{
    /// <summary>
    /// WinForms compatibility: represents a notification-area (system-tray) icon.
    /// Modern.Forms has no native tray support, so this is a stub that keeps the API surface
    /// compatible without crashing. ShowBalloonTip is a no-op; Click events never fire.
    /// </summary>
    public class NotifyIcon : Component
    {
        private string _text = string.Empty;
        private bool _visible;

        /// <summary>Initializes a new instance of NotifyIcon.</summary>
        public NotifyIcon () { }

        /// <summary>Initializes a new instance of NotifyIcon and adds it to the specified container.</summary>
        public NotifyIcon (IContainer container) { container.Add (this); }

        /// <summary>Gets or sets the icon displayed in the notification area.</summary>
        public Modern.Drawing.Icon? Icon { get; set; }

        /// <summary>Gets or sets the ToolTip text displayed when the mouse hovers over the icon.</summary>
        public string Text {
            get => _text;
            set => _text = value ?? string.Empty;
        }

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

        /// <summary>Gets or sets the title displayed on the balloon tooltip.</summary>
        public string BalloonTipTitle { get; set; } = string.Empty;

        /// <summary>Gets or sets the text displayed on the balloon tooltip.</summary>
        public string BalloonTipText { get; set; } = string.Empty;

        /// <summary>Gets or sets the icon shown on the balloon tooltip.</summary>
        public ToolTipIcon BalloonTipIcon { get; set; }

        /// <summary>Displays a balloon notification for the specified duration. No-op in Modern.Forms.</summary>
        public void ShowBalloonTip (int timeout) { }

        /// <summary>Displays a balloon notification with the specified title and text. No-op in Modern.Forms.</summary>
        public void ShowBalloonTip (int timeout, string tipTitle, string tipText, ToolTipIcon tipIcon) { }

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
