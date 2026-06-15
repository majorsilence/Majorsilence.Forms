using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;

namespace Modern.Forms
{
    /// <summary>
    /// Displays a small popup of descriptive text when the mouse hovers over an associated control.
    /// </summary>
    public class ToolTip : Component
    {
        private readonly Dictionary<Control, string> tips = new ();
        private PopupWindow? popup;
        private Label? popup_label;

        /// <summary>Initializes a new instance of ToolTip.</summary>
        public ToolTip () { }

        /// <summary>Initializes a new instance of ToolTip and adds it to the specified container.</summary>
        public ToolTip (IContainer container) { container.Add (this); }

        /// <summary>Gets or sets whether the ToolTip is currently active.</summary>
        public bool Active { get; set; } = true;

        /// <summary>Gets or sets whether the ToolTip is shown even when its parent form is not active.</summary>
        public bool ShowAlways { get; set; }

        /// <summary>Gets or sets the automatic delay, in milliseconds. Setting this adjusts the other delays.</summary>
        public int AutomaticDelay { get; set; } = 500;

        /// <summary>Gets or sets the period, in milliseconds, that the ToolTip remains visible.</summary>
        public int AutoPopDelay { get; set; } = 5000;

        /// <summary>Gets or sets the delay, in milliseconds, before a subsequent ToolTip appears.</summary>
        public int ReshowDelay { get; set; } = 100;

        /// <summary>Gets or sets the delay, in milliseconds, before the ToolTip first appears.</summary>
        public int InitialDelay { get; set; } = 500;

        /// <summary>Gets or sets the background color of the ToolTip.</summary>
        public Color BackColor { get; set; } = Color.FromArgb (255, 255, 255, 225);

        /// <summary>Gets or sets the foreground color of the ToolTip.</summary>
        public Color ForeColor { get; set; } = Color.Black;

        /// <summary>
        /// Associates ToolTip text with the specified control.
        /// </summary>
        public void SetToolTip (Control control, string caption)
        {
            if (control is null)
                return;

            control.MouseEnter -= Control_MouseEnter;
            control.MouseLeave -= Control_MouseLeave;
            control.MouseDown -= Control_MouseDown;

            if (string.IsNullOrEmpty (caption)) {
                tips.Remove (control);
                return;
            }

            tips[control] = caption;
            control.MouseEnter += Control_MouseEnter;
            control.MouseLeave += Control_MouseLeave;
            control.MouseDown += Control_MouseDown;
        }

        /// <summary>
        /// Returns the ToolTip text associated with the specified control.
        /// </summary>
        public string GetToolTip (Control control)
            => control is not null && tips.TryGetValue (control, out var t) ? t : string.Empty;

        /// <summary>
        /// Removes all ToolTip text associated with this component.
        /// </summary>
        public void RemoveAll ()
        {
            foreach (var control in tips.Keys) {
                control.MouseEnter -= Control_MouseEnter;
                control.MouseLeave -= Control_MouseLeave;
                control.MouseDown -= Control_MouseDown;
            }

            tips.Clear ();
            HidePopup ();
        }

        /// <summary>
        /// Hides any visible ToolTip for the specified control.
        /// </summary>
        public void Hide (Control control) => HidePopup ();

        /// <summary>Gets or sets whether the ToolTip appears as a balloon. Stub in Modern.Forms.</summary>
        public bool IsBalloon { get; set; }

        /// <summary>Gets or sets whether the ToolTip strips ampersands from the text. Stub in Modern.Forms.</summary>
        public bool StripAmpersands { get; set; }

        /// <summary>Gets or sets whether the ToolTip uses animation. Stub in Modern.Forms.</summary>
        public bool UseAnimation { get; set; } = true;

        /// <summary>Gets or sets whether the ToolTip fades in and out. Stub in Modern.Forms.</summary>
        public bool UseFading { get; set; } = true;

        /// <summary>Gets or sets the title text for balloon tooltips. Stub in Modern.Forms.</summary>
        public string ToolTipTitle { get; set; } = string.Empty;

        /// <summary>Gets or sets the ToolTip icon. Stub in Modern.Forms.</summary>
        public ToolTipIcon ToolTipIcon { get; set; } = ToolTipIcon.None;

        /// <summary>Shows a ToolTip with the given text at the given position relative to the control. Stub in Modern.Forms.</summary>
        public void Show (string text, Control control) => SetToolTip (control, text);

        /// <summary>Shows a ToolTip with the given text at the given position relative to the control. Stub in Modern.Forms.</summary>
        public void Show (string text, Control control, int duration) => SetToolTip (control, text);

        /// <summary>Shows a ToolTip with the given text at the given position relative to the control. Stub in Modern.Forms.</summary>
        public void Show (string text, Control control, int x, int y) => SetToolTip (control, text);

        /// <summary>Shows a ToolTip with the given text at the given position relative to the control. Stub in Modern.Forms.</summary>
        public void Show (string text, Control control, int x, int y, int duration) => SetToolTip (control, text);

        private void Control_MouseEnter (object? sender, MouseEventArgs e)
        {
            if (!Active || sender is not Control control)
                return;

            if (!tips.TryGetValue (control, out var text) || string.IsNullOrEmpty (text))
                return;

            ShowPopup (control, text, e);
        }

        private void Control_MouseLeave (object? sender, EventArgs e) => HidePopup ();

        private void Control_MouseDown (object? sender, MouseEventArgs e) => HidePopup ();

        private void ShowPopup (Control control, string text, MouseEventArgs e)
        {
            try {
                var form = control.FindForm ();

                if (form is null)
                    return;

                if (popup is null || popup_label is null) {
                    popup = new PopupWindow (form);
                    popup_label = popup.Controls.Add (new Label { Dock = DockStyle.Fill });
                    popup_label.Style.Border.Width = 1;
                }

                popup_label.Text = text;
                popup_label.Style.BackgroundColor = BackColor.ToSKColor ();
                popup_label.Style.ForegroundColor = ForeColor.ToSKColor ();

                var measured = TextMeasurer.MeasureText (text, Theme.UIFont, Theme.FontSize);
                popup.Size = new Size ((int)measured.Width + 16, (int)measured.Height + 10);

                // Offset below/right of the cursor like a standard tooltip.
                popup.Show (control, e.X + 12, e.Y + 18);
            } catch {
                // A tooltip must never take down the host application.
            }
        }

        private void HidePopup ()
        {
            try {
                popup?.Hide ();
            } catch {
            }
        }

        /// <inheritdoc/>
        protected override void Dispose (bool disposing)
        {
            if (disposing) {
                RemoveAll ();
                popup?.Hide ();
            }

            base.Dispose (disposing);
        }
    }
}
