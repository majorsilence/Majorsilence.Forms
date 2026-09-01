using System;
using System.Drawing;
using Majorsilence.Forms.Renderers;

namespace Majorsilence.Forms
{
    /// <summary>
    /// Represents a GroupBox container with a border and title.
    /// </summary>
    public partial class GroupBox : Control
    {
        /// <summary>Initializes a new instance of the GroupBox class.</summary>
        public GroupBox ()
        {
            SetControlBehavior (ControlBehaviors.InvalidateOnTextChanged);
            SetControlBehavior (ControlBehaviors.Selectable, false);
            TabStop = false;
        }

        /// <inheritdoc/>
        protected override Padding DefaultPadding => new Padding (3);

        /// <inheritdoc/>
        protected override Size DefaultSize => new Size (200, 100);

        // GDI's Font.Height for common UI fonts is ~1.216x the em size in pixels (points at 96dpi).
        // Used for the caption band the way real WinForms uses FontHeight; the Font property itself
        // resolves ambiently through the parent chain.
        // PixelSize rather than the hand-rolled points-to-pixels conversion this used to do:
        // identical for a Point-unit font, correct for the others too.
        internal int CaptionHeight => (int) Math.Ceiling (Font.PixelSize * 1.216f);

        /// <summary>
        /// WinForms parity: the display rectangle -- what docked and anchored children fill --
        /// starts BELOW the caption band (FontHeight + Padding.Top) and is inset by Padding on the
        /// other sides. Without this, a Dock=Fill child slides up under the caption text (the
        /// previous fixed 16px top padding matched only the classic 8.25pt default font).
        /// </summary>
        public override Rectangle DisplayRectangle {
            get {
                var b = base.DisplayRectangle;
                var caption = CaptionHeight;
                return new Rectangle (
                    b.X + Padding.Left,
                    b.Y + caption + Padding.Top,
                    Math.Max (0, b.Width - Padding.Horizontal),
                    Math.Max (0, b.Height - caption - Padding.Vertical));
            }
        }

        /// <inheritdoc/>
        public new static ControlStyle DefaultStyle = new ControlStyle (Control.DefaultStyle);

        /// <inheritdoc/>
        protected override void OnPaint (PaintEventArgs e)
        {
            base.OnPaint (e);
            RenderManager.Render (this, e);
        }

        /// <summary>Gets or sets the flat style of the group box. Stub in Majorsilence.Forms.</summary>
        public FlatStyle FlatStyle { get; set; } = FlatStyle.Standard;

        /// <summary>Gets or sets whether compatible text rendering is used. Stub in Majorsilence.Forms.</summary>
        public bool UseCompatibleTextRendering { get; set; } = true;

        /// <summary>Gets or sets whether the group box sizes itself to its contents.</summary>
        /// <remarks>
        /// This was <c>public new bool AutoSize { get; set; }</c> until 2026-08-31 -- a stored-only
        /// shadow (finding <c>LAY-26</c>). Setting it never reached the layout state
        /// <c>CommonProperties.GetAutoSize</c> reads, so nothing ever resized the group box, and
        /// <c>((Control)gb).AutoSize</c> disagreed with <c>gb.AutoSize</c> while
        /// <see cref="AutoSizeMode"/> -- which did go through the real plumbing -- read the other half
        /// of a feature that could not work. Upstream re-declares it only to restore the designer
        /// attributes, which is what this now does.
        /// </remarks>
        public override bool AutoSize {
            get => base.AutoSize;
            set => base.AutoSize = value;
        }

        /// <summary>
        /// Measures the group box: its children through its own layout engine, plus the caption band
        /// and the insets <see cref="DisplayRectangle"/> applies.
        /// </summary>
        /// <remarks>The inset has to be the same one <see cref="DisplayRectangle"/> uses, or an
        /// auto-sized group box reports a size its own children do not fit inside -- the caption band
        /// is the part that is easy to leave out, and it is font-dependent.</remarks>
        internal override Size GetPreferredSizeCore (Size proposedSize)
        {
            var inset = new Size (
                Padding.Horizontal,
                CaptionHeight + Padding.Vertical);

            var content = LayoutEngine.GetPreferredSize (this, proposedSize - inset) + inset;

            // A caption wider than the contents is what sets the width of an otherwise-empty group.
            if (Text.HasValue ()) {
                var measured = TextMeasurer.MeasureText (
                    Text, GetEffectiveFont (), GetEffectiveFontSize (), TextMeasurer.MaxSize);

                content.Width = Math.Max (content.Width, (int)Math.Ceiling (measured.Width) + Padding.Horizontal);
            }

            return content;
        }

        /// <inheritdoc/>
        public override ControlStyle Style { get; } = new ControlStyle (DefaultStyle);
    }
}
