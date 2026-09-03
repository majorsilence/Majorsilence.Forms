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
        /// <summary>Gets or sets how the panel behaves when its AutoSize property is enabled.</summary>
        public virtual AutoSizeMode AutoSizeMode {
            get => GetAutoSizeMode ();
            set {
                if (!EnumCompat.IsDefined (value))
                    throw new InvalidEnumArgumentException (nameof (value), (int)value, typeof (AutoSizeMode));

                if (GetAutoSizeMode () != value)
                    SetAutoSizeMode (value);
            }
        }

        /// <inheritdoc/>
        protected override Size DefaultSize => new Size (200, 100);

        /// <inheritdoc/>
        public new static ControlStyle DefaultStyle = new ControlStyle (Control.DefaultStyle);

        /// <summary>
        /// Asks this panel's own layout engine what size it needs, as upstream's
        /// <c>Panel.GetPreferredSizeCore</c> does.
        /// </summary>
        /// <remarks>
        /// <para>
        /// This used to override the **public** <see cref="Control.GetPreferredSize"/> with a
        /// hand-rolled union of where the children currently sat (finding <c>LAY-25</c>, P0). Three
        /// things followed from that, all silent. It never consulted <see cref="Control.LayoutEngine"/>,
        /// so an <c>AutoSize</c> <see cref="FlowLayoutPanel"/> or <see cref="TableLayoutPanel"/> --
        /// both of which inherit this and have correctly-ported engines -- sized to the children's
        /// *stale* positions instead of asking the engine that was about to move them. It discarded
        /// <c>proposedSize</c>, so a wrapping <c>FlowLayoutPanel</c> could not compute a height for a
        /// given width and reported a single row. And because it overrode the public method rather than
        /// the core, it bypassed <c>ApplySizeConstraints</c> and the preferred-size cache, so
        /// <see cref="Control.MinimumSize"/>/<see cref="Control.MaximumSize"/> were not applied to
        /// <c>PreferredSize</c> at all.
        /// </para>
        /// <para>
        /// Upstream subtracts <c>SizeFromClientSize (Size.Empty)</c> as well, for the non-client
        /// border a Win32 panel has. There is no analogue here: a panel's <see cref="BorderStyle"/> is
        /// painted inside the client rectangle, so <see cref="Control.Padding"/> is the whole inset.
        /// </para>
        /// </remarks>
        internal override Size GetPreferredSizeCore (Size proposedSize)
        {
            var totalPadding = Padding.Size;

            return LayoutEngine.GetPreferredSize (this, proposedSize - totalPadding) + totalPadding;
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
                if (!EnumCompat.IsDefined (value))
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
