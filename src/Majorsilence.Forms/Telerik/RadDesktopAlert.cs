using System.Drawing;

namespace Majorsilence.Forms.Telerik
{
    /// <summary>
    /// Telerik-compat desktop alert (toast). Backed by a borderless, always-on-top
    /// <see cref="Majorsilence.Forms.Form"/> (<see cref="RadDesktopAlertPopup"/>, exposed as <see cref="Popup"/>)
    /// positioned at the bottom-right of the primary screen. <see cref="Show ()"/> displays the popup and
    /// starts a <see cref="Timer"/> that closes it after <see cref="AutoCloseDelay"/> milliseconds. When more
    /// than one alert is visible at the same time, each is stacked above the previous one so they don't
    /// overlap.
    /// </summary>
    public class RadDesktopAlert : IDisposable
    {
        // Alerts currently on screen, most-recently-shown last. Used to stack new alerts above the
        // ones already visible (bottom-right corner, growing upward).
        private static readonly List<RadDesktopAlert> _activeAlerts = new ();

        private readonly Timer _autoCloseTimer = new ();

        /// <summary>Initializes a new instance of the RadDesktopAlert class.</summary>
        public RadDesktopAlert ()
        {
            Popup = new RadDesktopAlertPopup ();
            Popup.CloseRequested += (_, _) => Close ();

            _autoCloseTimer.Tick += (_, _) => Close ();
        }

        /// <summary>Gets or sets the caption (title) text shown at the top of the alert.</summary>
        public string CaptionText {
            get => Popup.CaptionLabel.Text;
            set => Popup.CaptionLabel.Text = value;
        }

        /// <summary>Gets or sets the content (body) text shown in the alert.</summary>
        public string ContentText {
            get => Popup.ContentLabel.Text;
            set => Popup.ContentLabel.Text = value;
        }

        /// <summary>Gets or sets the delay, in milliseconds, before the alert automatically closes.</summary>
        public int AutoCloseDelay { get; set; } = 5000;

        /// <summary>Gets the popup window hosting the alert's element tree and content.</summary>
        public RadDesktopAlertPopup Popup { get; }

        /// <summary>Shows the alert, positioned bottom-right of the primary screen (stacked above any already-visible alerts), and starts the auto-close timer.</summary>
        public void Show ()
        {
            Popup.ApplyElementStyles ();
            PositionAtBottomRight ();

            _activeAlerts.Add (this);
            Popup.Show ();

            if (AutoCloseDelay > 0) {
                _autoCloseTimer.Interval = AutoCloseDelay;
                _autoCloseTimer.Start ();
            }
        }

        /// <summary>Closes the alert and stops the auto-close timer.</summary>
        public void Close ()
        {
            _autoCloseTimer.Stop ();
            _activeAlerts.Remove (this);

            // Hide (rather than Close/destroy) the popup: WindowBase.Close () never flips Visible back to
            // false (that only happens via Hide ()), and a toast has no other state worth tearing down
            // before Dispose. Hiding also keeps Popup.Visible a reliable "is this alert on screen" signal.
            if (Popup.Visible)
                Popup.Hide ();
        }

        // Bottom-right of the primary screen's working area, stacked above alerts already showing there.
        private void PositionAtBottomRight ()
        {
            var workingArea = Screen.PrimaryScreen?.WorkingArea ?? new Rectangle (0, 0, 800, 600);

            const int margin = 8;
            var stackOffset = 0;
            foreach (var alert in _activeAlerts)
                stackOffset += alert.Popup.Size.Height + margin;

            var x = workingArea.Right - Popup.Size.Width - margin;
            var y = workingArea.Bottom - Popup.Size.Height - margin - stackOffset;

            Popup.Location = new Point (x, y);
        }

        /// <summary>Closes the alert (if open), stops the auto-close timer, and releases its resources.</summary>
        public void Dispose ()
        {
            Close ();
            _autoCloseTimer.Dispose ();
            Popup.Dispose ();
            GC.SuppressFinalize (this);
        }
    }

    /// <summary>
    /// The borderless, always-on-top popup window that displays a <see cref="RadDesktopAlert"/>. Exposes
    /// the Telerik element tree (<see cref="AlertElement"/>) whose <c>ForeColor</c>/<c>BackColor</c>/
    /// <c>BorderColor</c> feed this window's real background/border/text rendering.
    /// </summary>
    public class RadDesktopAlertPopup : Form
    {
        /// <summary>Initializes a new instance of the RadDesktopAlertPopup class.</summary>
        public RadDesktopAlertPopup ()
        {
            FormBorderStyle = FormBorderStyle.None;
            ShowInTaskbar = false;
            TopMost = true;
            StartPosition = FormStartPosition.Manual;
            Size = new Size (300, 100);

            CaptionLabel = new Label {
                Dock = DockStyle.Fill,
                Text = string.Empty,
                TextAlign = ContentAlignment.MiddleLeft,
            };

            ContentLabel = new Label {
                Dock = DockStyle.Fill,
                Text = string.Empty,
                TextAlign = ContentAlignment.TopLeft,
            };

            CloseButton = new Button {
                Text = "x",
                Dock = DockStyle.Right,
                Width = 24,
            };
            CloseButton.Click += (_, _) => CloseRequested?.Invoke (this, EventArgs.Empty);

            // The caption bar is its own Top-docked panel containing the caption label (Fill) and the
            // close button (Right), so the close button only spans the caption bar's height rather than
            // the whole popup (docking within a Form is processed highest-child-index-first: adding the
            // caption panel and content label directly to the Form, rather than the individual caption
            // controls, keeps the Right dock scoped to the small caption panel).
            CaptionPanel = new Panel { Dock = DockStyle.Top, Height = 28 };
            CaptionPanel.Controls.Add (CaptionLabel);
            CaptionPanel.Controls.Add (CloseButton);

            AlertElement.CaptionElement.CaptionGrip.Controls.Add (CloseButton);
            AlertElement.CaptionElement.TextAndButtonsElement.TextElement.Label = CaptionLabel;

            Controls.Add (ContentLabel);
            Controls.Add (CaptionPanel);
        }

        /// <summary>Gets the caption bar panel (hosts <see cref="CaptionLabel"/> and <see cref="CloseButton"/>).</summary>
        internal Panel CaptionPanel { get; }

        /// <summary>Gets the label displaying <see cref="RadDesktopAlert.CaptionText"/>.</summary>
        internal Label CaptionLabel { get; }

        /// <summary>Gets the label displaying <see cref="RadDesktopAlert.ContentText"/>.</summary>
        internal Label ContentLabel { get; }

        /// <summary>Gets the alert's close button.</summary>
        internal Button CloseButton { get; }

        /// <summary>Gets the root of the alert's Telerik-compat element tree.</summary>
        public RadDesktopAlertElement AlertElement { get; } = new ();

        /// <summary>Raised when the close button is clicked.</summary>
        internal event EventHandler? CloseRequested;

        /// <summary>
        /// Pushes <see cref="AlertElement"/>'s ForeColor/BackColor/BorderColor onto the window's real
        /// rendering: the caption text color, the window background, and the window border. Called by
        /// <see cref="RadDesktopAlert.Show ()"/> just before the popup is shown, so styling assigned any
        /// time before <c>Show()</c> (the documented usage pattern) takes effect.
        /// </summary>
        internal void ApplyElementStyles ()
        {
            var textElement = AlertElement.CaptionElement.TextAndButtonsElement.TextElement;
            if (!textElement.ForeColor.IsEmpty)
                CaptionLabel.ForeColor = textElement.ForeColor;

            var grip = AlertElement.CaptionElement.CaptionGrip;
            if (!grip.BackColor.IsEmpty) {
                CaptionPanel.BackColor = grip.BackColor;
                CaptionLabel.BackColor = grip.BackColor;
            }

            if (!AlertElement.BackColor.IsEmpty) {
                Style.BackColor = AlertElement.BackColor;
                ContentLabel.BackColor = AlertElement.BackColor;
            }

            if (!AlertElement.BorderColor.IsEmpty) {
                Style.Border.Color = new SkiaSharp.SKColor (AlertElement.BorderColor.R, AlertElement.BorderColor.G, AlertElement.BorderColor.B, AlertElement.BorderColor.A);
                Style.Border.Width = 2;
            }
        }
    }

    /// <summary>
    /// Root element of a <see cref="RadDesktopAlert"/>'s element tree (<c>desktopAlert.Popup.AlertElement</c>
    /// in Telerik). <see cref="RadElement.BackColor"/> feeds the popup's window background and
    /// <see cref="BorderColor"/> feeds its border; <see cref="GradientStyle"/> is stored but not rendered
    /// (Majorsilence.Forms paints solid fills).
    /// </summary>
    public class RadDesktopAlertElement : RadElement
    {
        /// <summary>Gets or sets the border color. Feeds the popup window's real border color.</summary>
        public Color BorderColor { get; set; } = Color.Empty;

        /// <summary>Gets or sets the gradient fill style. Stored for API compatibility; Majorsilence.Forms always paints a solid fill.</summary>
        public GradientStyles GradientStyle { get; set; } = GradientStyles.Solid;

        /// <summary>Gets the caption (title bar) element.</summary>
        public RadDesktopAlertCaptionElement CaptionElement { get; } = new ();
    }

    /// <summary>Caption (title bar) element of a <see cref="RadDesktopAlert"/> (<c>AlertElement.CaptionElement</c>).</summary>
    public class RadDesktopAlertCaptionElement : RadElement
    {
        /// <summary>Gets the element hosting the caption text and buttons.</summary>
        public RadDesktopAlertTextAndButtonsElement TextAndButtonsElement { get; } = new ();

        /// <summary>Gets the caption's grip/drag-handle element. Its BackColor/GradientStyle feed the caption bar's background.</summary>
        public RadDesktopAlertCaptionGrip CaptionGrip { get; } = new ();
    }

    /// <summary>Hosts the caption text and any caption buttons (<c>CaptionElement.TextAndButtonsElement</c>).</summary>
    public class RadDesktopAlertTextAndButtonsElement : RadElement
    {
        /// <summary>Gets the text element. Its ForeColor feeds the real caption text color.</summary>
        public RadDesktopAlertTextElement TextElement { get; } = new ();
    }

    /// <summary>The caption's text element (<c>TextAndButtonsElement.TextElement</c>). <see cref="RadElement.ForeColor"/> feeds the real caption text color.</summary>
    public class RadDesktopAlertTextElement : RadElement
    {
        // Bound to RadDesktopAlertPopup.CaptionLabel so setting ForeColor takes effect immediately as
        // well as when RadDesktopAlertPopup.ApplyElementStyles () runs at Show ().
        internal Label? Label { get; set; }

        /// <inheritdoc/>
        public new Color ForeColor {
            get => base.ForeColor;
            set {
                base.ForeColor = value;
                if (Label != null && !value.IsEmpty)
                    Label.ForeColor = value;
            }
        }
    }

    /// <summary>The caption's grip/drag-handle element (<c>CaptionElement.CaptionGrip</c>). BackColor/GradientStyle feed the caption bar's background.</summary>
    public class RadDesktopAlertCaptionGrip : RadElement
    {
        /// <summary>Gets or sets the gradient fill style. Stored for API compatibility; Majorsilence.Forms always paints a solid fill.</summary>
        public GradientStyles GradientStyle { get; set; } = GradientStyles.Solid;

        // Controls hosted on the caption bar (the close button), so RadDesktopAlertPopup can add the
        // close button to the actual caption label's area without exposing Majorsilence.Forms.Control
        // details on the public element type.
        internal List<Control> Controls { get; } = new ();
    }
}
