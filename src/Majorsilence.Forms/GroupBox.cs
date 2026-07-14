using System;
using System.Drawing;
using Majorsilence.Forms.Renderers;

namespace Majorsilence.Forms
{
    /// <summary>
    /// Represents a GroupBox container with a border and title.
    /// </summary>
    public class GroupBox : Control
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
        internal int CaptionHeight => (int) Math.Ceiling (Font.SizeInPoints * 96f / 72f * 1.216f);

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

        /// <summary>Gets or sets whether the group box is auto-sized. Stub in Majorsilence.Forms.</summary>
        public new bool AutoSize { get; set; }

        /// <inheritdoc/>
        public override ControlStyle Style { get; } = new ControlStyle (DefaultStyle);
    }
}
