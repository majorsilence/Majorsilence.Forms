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
        /// border a Win32 panel has. The analogue here is <see cref="BorderStyle"/>, which since
        /// LAY-28 maps onto the instance style's border -- and <see cref="Control.ClientRectangle"/>
        /// already deflates by that, so the engine is asked about the area inside the frame and the
        /// border is added back below.
        /// </para>
        /// </remarks>
        internal override Size GetPreferredSizeCore (Size proposedSize)
        {
            var border = new Padding (
                Style.Border.Left.GetWidth (),
                Style.Border.Top.GetWidth (),
                Style.Border.Right.GetWidth (),
                Style.Border.Bottom.GetWidth ());
            var totalPadding = Padding.Size + new Size (border.Horizontal, border.Vertical);

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
        /// <remarks>
        /// LAY-28: the setter validated and invalidated, and then nothing drew anything --
        /// <c>PanelRenderer.Render</c> was an empty method body -- so <c>panel1.BorderStyle =
        /// FixedSingle</c>, the standard way to group controls visually without a GroupBox, showed
        /// nothing at all. Mapping it onto the instance style's border draws the frame AND insets the
        /// client area, because <see cref="Control.ClientRectangle"/> and
        /// <see cref="Control.DisplayRectangle"/> both deflate by <c>CurrentStyle.Border</c> -- which
        /// is what Win32 does for <c>WS_BORDER</c>/<c>WS_EX_CLIENTEDGE</c>, and why a <c>Dock =
        /// Fill</c> child used to sit 1-2px out from where Windows puts it.
        /// <see cref="BorderStyle.None"/> clears the override rather than forcing zero, so a CSS theme
        /// rule for <c>Panel</c> still decides.
        /// </remarks>
        public BorderStyle BorderStyle {
            get => border_style;
            set {
                if (!EnumCompat.IsDefined (value))
                    throw new InvalidEnumArgumentException (nameof (value), (int)value, typeof (BorderStyle));

                if (border_style != value) {
                    border_style = value;
                    Style.Border.Width = value switch {
                        BorderStyle.FixedSingle => 1,
                        BorderStyle.Fixed3D => 2,
                        _ => null,
                    };

                    // The client area just changed size, so the children have to be placed again.
                    PerformLayout ();
                    Invalidate ();
                }
            }
        }

        /// <inheritdoc/>
        public override ControlStyle Style { get; } = new ControlStyle (DefaultStyle);
    }
}
