using System.Drawing;
using Majorsilence.Forms.Renderers;

namespace Majorsilence.Forms
{
    /// <summary>
    /// Represents a popup dialog used to inform the user of a message.
    /// </summary>
    public class MessageBoxForm : Form
    {
        private readonly Label label;
        private readonly Panel button_panel;
        private readonly IconBox icon_box;

        // The glyph band at the message's leading edge (W6 mechanisms): a MessageBoxIcon draws one of
        // the standard message glyphs there; None hides the band.
        private sealed class IconBox : Control
        {
            internal MessageGlyph? Glyph { get; set; }

            protected override void OnPaint (PaintEventArgs e)
            {
                base.OnPaint (e);
                using var device = e.DeviceSpace ();   // laid out in device pixels (EVT-37)

                if (Glyph is { } glyph) {
                    var size = LogicalToDeviceUnits (32);
                    MessageGlyphs.Draw (e.Canvas, glyph, new Rectangle (LogicalToDeviceUnits (IconLeft), LogicalToDeviceUnits (MessageMargin), size, size));
                }
            }
        }

        /// <summary>The glyph the dialog shows, or null for <see cref="MessageBoxIcon.None"/>.</summary>
        internal MessageGlyph? Glyph => icon_box.Glyph;

        /// <summary>The label that shows the message, which the browser's accessibility DOM points
        /// <c>aria-describedby</c> at.</summary>
        internal Label MessageLabel => label;

        /// <summary>
        /// Initializes a new instance of the MessageBoxForm class.
        /// </summary>
        public MessageBoxForm ()
        {
            StartPosition = FormStartPosition.CenterParent;
            AllowMinimize = false;
            AllowMaximize = false;

            // A Label, not a read-only TextBox: a TextBox has no word wrap, so it laid the message out
            // as one long line and scrolled to the end of it, cutting off the *start* of the sentence
            // ("...t find mplayer" instead of "Cannot find mplayer"). A multiline Label wraps.
            // Laid out as upstream's (the system message box): the message on a window-coloured area,
            // centred against the glyph, above a control-coloured band holding the buttons at its right.
            label = Controls.Add (new Label {
                Dock = DockStyle.Fill,
                Multiline = true,
                TextAlign = ContentAlignment.MiddleLeft,
                Padding = new Padding (TextMarginNoIcon, MessageMargin, TextMarginRight, MessageMargin)
            });

            label.Style.BackgroundColor = Theme.ControlLowColor;
            label.Style.Border.Width = 0;

            icon_box = Controls.Add (new IconBox { Dock = DockStyle.Left, Width = IconBandWidth, Visible = false });
            icon_box.Style.BackgroundColor = Theme.ControlLowColor;

            button_panel = Controls.Add (new Panel {
                Dock = DockStyle.Bottom,
                Height = ButtonBandHeight
            });

            AddButtons (MessageBoxButtons.OK);

            // Re-place whenever the panel itself is resized. The form's own resize is too early: the
            // docked panel has not been given its final width by then.
            button_panel.SizeChanged += (_, _) => PlaceButtons ();
        }

        /// <summary>
        /// Initializes a new instance of the MessageBoxForm class with the specified title, message, and buttons.
        /// </summary>
        public MessageBoxForm (string title, string message, MessageBoxButtons buttons = MessageBoxButtons.OK) : this ()
        {
            Text = title;
            label.Text = message;
            AddButtons (buttons);
            CalculateDialogSize ();
        }

        /// <summary>
        /// Initializes a new instance with the icon, default button and options a <see cref="MessageBox"/>
        /// call names (W6 mechanisms): the glyph is drawn beside the message, Enter presses the default
        /// button (which starts focused), <see cref="MessageBoxOptions.RightAlign"/> right-aligns the
        /// message and <see cref="MessageBoxOptions.RtlReading"/> lays the dialog out right to left.
        /// </summary>
        public MessageBoxForm (string title, string message, MessageBoxButtons buttons, MessageBoxIcon icon,
            MessageBoxDefaultButton defaultButton, MessageBoxOptions options) : this (title, message, buttons)
        {
            icon_box.Glyph = MessageGlyphs.For (icon);
            icon_box.Visible = icon_box.Glyph is not null;
            label.Padding = new Padding (icon_box.Visible ? 0 : TextMarginNoIcon, MessageMargin, TextMarginRight, MessageMargin);
            CalculateDialogSize ();   // the glyph band widens the dialog

            if ((options & MessageBoxOptions.RightAlign) != 0)
                label.TextAlign = ContentAlignment.MiddleRight;

            if ((options & MessageBoxOptions.RtlReading) != 0)
                RightToLeft = RightToLeft.Yes;

            var buttons_in_order = button_panel.Controls.GetAllControls ().OfType<Button> ().ToList ();
            var index = defaultButton switch {
                MessageBoxDefaultButton.Button2 => 1,
                MessageBoxDefaultButton.Button3 => 2,
                _ => 0,
            };

            // Upstream falls back to the first button when the set has no button at that position.
            var default_button = index < buttons_in_order.Count ? buttons_in_order[index] : buttons_in_order.FirstOrDefault ();

            if (default_button is not null) {
                AcceptButton = default_button;
                Shown += (_, _) => default_button.Select ();
            }
        }

        /// <summary>The buttons each <see cref="MessageBoxButtons"/> set shows, in left-to-right order.</summary>
        /// <remarks>
        /// Four of the seven sets used to fall through to a lone OK button that returned
        /// <see cref="DialogResult.OK"/>: <c>YesNoCancel</c>, <c>AbortRetryIgnore</c>,
        /// <c>RetryCancel</c> and <c>CancelTryContinue</c>. A "Save changes? Yes / No / Cancel" prompt
        /// therefore offered one button and told the caller Yes — silently taking the destructive
        /// branch of a three-way decision. That is why this is a table rather than a switch: a set
        /// nobody wrote a case for is now a compile-time hole, not a wrong answer at runtime.
        /// </remarks>
        private static (string Text, DialogResult Result)[] ButtonsFor (MessageBoxButtons buttons) => buttons switch {
            MessageBoxButtons.OK => [("OK", DialogResult.OK)],
            MessageBoxButtons.OKCancel => [("OK", DialogResult.OK), ("Cancel", DialogResult.Cancel)],
            MessageBoxButtons.AbortRetryIgnore =>
                [("Abort", DialogResult.Abort), ("Retry", DialogResult.Retry), ("Ignore", DialogResult.Ignore)],
            MessageBoxButtons.YesNoCancel =>
                [("Yes", DialogResult.Yes), ("No", DialogResult.No), ("Cancel", DialogResult.Cancel)],
            MessageBoxButtons.YesNo => [("Yes", DialogResult.Yes), ("No", DialogResult.No)],
            MessageBoxButtons.RetryCancel => [("Retry", DialogResult.Retry), ("Cancel", DialogResult.Cancel)],
            MessageBoxButtons.CancelTryContinue =>
                [("Cancel", DialogResult.Cancel), ("Try Again", DialogResult.TryAgain), ("Continue", DialogResult.Continue)],
            _ => [("OK", DialogResult.OK)],
        };

        private void AddButtons (MessageBoxButtons buttons)
        {
            button_panel.Controls.Clear ();

            // Added in reading order; PlaceButtons lays them out left to right in collection order, so
            // the collection order is the visual order. The previous hand-written cases added the
            // secondary button first, which put No before Yes and Cancel before OK.
            var left = ButtonMargin;

            foreach (var (text, result) in ButtonsFor (buttons)) {
                var button = button_panel.Controls.Add (new Button {
                    Text = text, Width = ButtonWidth, Height = ButtonHeight, Top = (ButtonBandHeight - ButtonHeight) / 2, Left = left
                });
                button.Click += (_, _) => DialogResult = result;
                left += ButtonWidth + ButtonGap;
            }
        }

        // The widest the message runs before it wraps, and the tallest the dialog grows.
        private const int MaxTextWidth = 560;
        private const int MaxClientHeight = 700;

        // Upstream's metrics, measured from the system message box at 96 DPI: the 32px glyph sits 21px
        // in and 23px down, the text starts 9px past it and is centred on it, and the 42px band holds
        // 75x23 buttons 15px in from the right edge.
        private const int MessageMargin = 23;
        private const int IconLeft = 21;
        private const int IconBandWidth = IconLeft + 32 + 9;
        private const int TextMarginNoIcon = IconLeft;
        private const int TextMarginRight = 30;
        private const int ButtonBandHeight = 42;
        private const int ButtonWidth = 75;
        private const int ButtonHeight = 23;
        private const int ButtonGap = 6;
        private const int ButtonMargin = 15;

        // Sizes the dialog to its message, as upstream's message box does: the text's own width up to a
        // wrapping limit, its wrapped height, and never narrower than the button row. A fixed 400x200
        // (stepped up by line count) left a one-line error floating in empty space, and still cut off
        // a long line that had no newlines in it.
        private void CalculateDialogSize ()
        {
            var icon_width = icon_box.Visible ? icon_box.Width : 0;
            var text = label.GetPreferredSize (new Size (MaxTextWidth - label.Padding.Horizontal, 0));

            var buttons = button_panel.Controls.GetAllControls ().OfType<Button> ().ToList ();
            var buttons_width = buttons.Sum (b => b.Width + ButtonGap) - ButtonGap + 2 * ButtonMargin;

            // The glyph is 32px with a margin above and below it, so the area is at least that tall
            // whatever the text's height; the label's padding already holds the text's own margins.
            var width = Math.Max (icon_width + text.Width, buttons_width);
            var body = Math.Max (text.Height, icon_box.Visible ? 32 + 2 * MessageMargin : 0);

            ClientSize = new Size (Math.Max (width, 180), Math.Min (body + button_panel.Height, MaxClientHeight));

            PlaceButtons ();
        }

        // Lines the buttons up at the right of their band, as upstream's message box does.
        //
        // Called on every resize, not once at construction. The dialog is built before it has a window,
        // so the docked children are laid out against a client area that does not exist yet -- the Fill
        // label comes out 0 x -45 and the Bottom panel sits above the top edge. That is corrected by the
        // layout pass the backend runs when the window appears, and positions computed before it are
        // simply wrong: measured against a zero-width panel the buttons landed at negative X, off the
        // left edge, so the message box showed its text and no buttons at all.
        private void PlaceButtons ()
        {
            // Buttons only: GetAllControls also yields the panel's implicit chrome -- its scrollbars --
            // which were being counted into the total width and, worse, repositioned along with the
            // buttons.
            var buttons = button_panel.Controls.GetAllControls ().OfType<Button> ().ToList ();
            if (buttons.Count == 0 || button_panel.Width <= 0)
                return;

            var totalW = buttons.Sum (c => c.Width + ButtonGap) - ButtonGap;
            var startX = Math.Max (0, button_panel.Width - ButtonMargin - totalW);

            foreach (var btn in buttons) {
                btn.Left = startX;
                startX += btn.Width + ButtonGap;
            }
        }


        /// <inheritdoc/>
        protected override Size DefaultSize => new Size (400, 200);

        /// <summary>
        /// Gets or sets the message of the dialog.
        /// </summary>
        public string Message {
            get => label.Text;
            set {
                if (label.Text != value) {
                    label.Text = value;
                    CalculateDialogSize ();
                }
            }
        }
    }
}
