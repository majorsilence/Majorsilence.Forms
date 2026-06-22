using System;
using System.ComponentModel;
using System.Drawing;
using Majorsilence.Forms.Renderers;

namespace Majorsilence.Forms
{
    /// <summary>
    /// Represents a Panel control.
    /// </summary>
    public class Panel : ScrollableControl
    {
        /// <summary>
        /// Initializes a new instance of the Panel class.
        /// </summary>
        public Panel ()
        {
            TabStop = false;

            SetControlBehavior (ControlBehaviors.Selectable, false);
        }
        /// <inheritdoc/>
        protected override Size DefaultSize => new Size (200, 100);

        /// <inheritdoc/>
        public new static ControlStyle DefaultStyle = new ControlStyle (Control.DefaultStyle);

        /// <inheritdoc/>
        public override Size GetPreferredSize (Size proposedSize)
        {
            var size = Size.Empty;

            foreach (var child in Controls) {
                if (child.Dock == DockStyle.Fill) {
                    if (child.Bounds.Right > size.Width)
                        size.Width = child.Bounds.Right;
                } else if (child.Dock != DockStyle.Top && child.Dock != DockStyle.Bottom && (child.Anchor & AnchorStyles.Right) == 0 && (child.Bounds.Right + child.Margin.Right) > size.Width)
                    size.Width = child.Bounds.Right + child.Margin.Right;

                if (child.Dock == DockStyle.Fill) {
                    if (child.Bounds.Bottom > size.Height)
                        size.Height = child.Bounds.Bottom;
                } else if (child.Dock != DockStyle.Left && child.Dock != DockStyle.Right && (child.Anchor & AnchorStyles.Bottom) == 0 && (child.Bounds.Bottom + child.Margin.Bottom) > size.Height)
                    size.Height = child.Bounds.Bottom + child.Margin.Bottom;
            }

            return size;
        }

        /// <inheritdoc/>
        protected override void OnPaint (PaintEventArgs e)
        {
            base.OnPaint (e);

            RenderManager.Render (this, e);
        }

        private BorderStyle border_style = BorderStyle.None;

        /// <summary>Gets or sets the border style of the panel.</summary>
        public BorderStyle BorderStyle {
            get => border_style;
            set {
                if (!Enum.IsDefined (typeof (BorderStyle), value))
                    throw new InvalidEnumArgumentException (nameof (value), (int)value, typeof (BorderStyle));

                if (border_style != value) {
                    border_style = value;
                    Invalidate ();
                }
            }
        }

        /// <inheritdoc/>
        public override ControlStyle Style { get; } = new ControlStyle (DefaultStyle);
    }
}
