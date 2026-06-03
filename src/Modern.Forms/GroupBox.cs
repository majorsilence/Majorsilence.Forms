using System.Drawing;
using Modern.Forms.Renderers;

namespace Modern.Forms
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
        protected override Padding DefaultPadding => new Padding (3, 16, 3, 3);

        /// <inheritdoc/>
        protected override Size DefaultSize => new Size (200, 100);

        /// <inheritdoc/>
        public new static ControlStyle DefaultStyle = new ControlStyle (Control.DefaultStyle);

        /// <inheritdoc/>
        protected override void OnPaint (PaintEventArgs e)
        {
            base.OnPaint (e);
            RenderManager.Render (this, e);
        }

        /// <inheritdoc/>
        public override ControlStyle Style { get; } = new ControlStyle (DefaultStyle);
    }
}
