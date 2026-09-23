using System.Drawing;

namespace Majorsilence.Forms.Renderers
{
    /// <summary>
    /// Represents a class that can render a StatusBar.
    /// </summary>
    /// <remarks>
    /// Panel-aware as of W6 mechanisms. It drew <see cref="Control.Text"/> and nothing else, so
    /// <see cref="StatusBar.ShowPanels"/> and every <see cref="StatusBarPanel"/> property were stored
    /// and read by nothing. With panels shown it lays them out through
    /// <see cref="StatusBar.LayoutPanels"/>, draws each panel's border, icon and aligned text -- or hands
    /// an owner-drawn panel to <see cref="StatusBar.DrawItem"/> -- and draws the sizing grip.
    /// </remarks>
    public class StatusBarRenderer : Renderer<StatusBar>
    {
        /// <inheritdoc/>
        protected override void Render (StatusBar control, PaintEventArgs e)
        {
            if (!control.ShowPanels) {
                var text_area = control.PaddedClientRectangle;
                text_area.Width = System.Math.Max (0, text_area.Width - control.ScaledGripWidth);

                e.Canvas.DrawText (control.Text, text_area, control, ContentAlignment.MiddleLeft, maxLines: 1);
            } else {
                control.LayoutPanels ();

                for (var i = 0; i < control.Panels.Count; i++)
                    RenderPanel (control, control.Panels[i], i, e);
            }

            if (control.SizingGrip)
                RenderSizingGrip (control, e);
        }

        /// <summary>Renders one panel: its border, then its content.</summary>
        protected virtual void RenderPanel (StatusBar control, StatusBarPanel panel, int index, PaintEventArgs e)
        {
            var bounds = panel.DeviceBounds;

            if (bounds.Width <= 0 || bounds.Height <= 0)
                return;

            e.Canvas.Save ();
            e.Canvas.Clip (bounds);

            RenderPanelBorder (panel, bounds, e);

            if (panel.Style == StatusBarPanelStyle.OwnerDraw)
                control.RaiseDrawItem (panel, index, bounds, e);
            else
                RenderPanelContent (control, panel, bounds, e);

            e.Canvas.Restore ();
        }

        /// <summary>Draws the panel's 3D border: sunken, raised or none.</summary>
        protected virtual void RenderPanelBorder (StatusBarPanel panel, Rectangle bounds, PaintEventArgs e)
        {
            if (panel.BorderStyle == StatusBarPanelBorderStyle.None)
                return;

            // Sunken: dark top-left, light bottom-right. Raised is the reverse.
            var sunken = panel.BorderStyle == StatusBarPanelBorderStyle.Sunken;
            var top_left = sunken ? Theme.BorderMidColor : Theme.ControlHighColor;
            var bottom_right = sunken ? Theme.ControlHighColor : Theme.BorderMidColor;

            e.Canvas.DrawLine (bounds.Left, bounds.Top, bounds.Right - 1, bounds.Top, top_left);
            e.Canvas.DrawLine (bounds.Left, bounds.Top, bounds.Left, bounds.Bottom - 1, top_left);
            e.Canvas.DrawLine (bounds.Left, bounds.Bottom - 1, bounds.Right - 1, bounds.Bottom - 1, bottom_right);
            e.Canvas.DrawLine (bounds.Right - 1, bounds.Top, bounds.Right - 1, bounds.Bottom - 1, bottom_right);
        }

        /// <summary>Draws the panel's icon and its text, aligned as the panel asks.</summary>
        protected virtual void RenderPanelContent (StatusBar control, StatusBarPanel panel, Rectangle bounds, PaintEventArgs e)
        {
            var inset = e.LogicalToDeviceUnits (3);
            var content = new Rectangle (bounds.Left + inset, bounds.Top, System.Math.Max (0, bounds.Width - inset * 2), bounds.Height);

            if (panel.Icon?.GetSKBitmap () is { } icon) {
                var side = System.Math.Min (e.LogicalToDeviceUnits (panel.Icon.Height), bounds.Height - inset * 2);
                var icon_bounds = new Rectangle (content.Left, bounds.Top + (bounds.Height - side) / 2, side, side);

                e.Canvas.DrawBitmap (icon, icon_bounds);

                content = new Rectangle (icon_bounds.Right + inset, content.Top, System.Math.Max (0, content.Right - icon_bounds.Right - inset), content.Height);
            }

            var alignment = panel.Alignment switch {
                HorizontalAlignment.Center => ContentAlignment.MiddleCenter,
                HorizontalAlignment.Right => ContentAlignment.MiddleRight,
                _ => ContentAlignment.MiddleLeft,
            };

            e.Canvas.DrawText (panel.Text, content, control, alignment, maxLines: 1);
        }

        /// <summary>Draws the diagonal dot grip in the trailing corner.</summary>
        protected virtual void RenderSizingGrip (StatusBar control, PaintEventArgs e)
        {
            var client = control.ClientRectangle;
            var dot = System.Math.Max (1, e.LogicalToDeviceUnits (2));
            var step = dot * 2;

            // Three diagonal runs of dots, the standard grip shape: 3, 2 and 1 dots from the corner.
            for (var run = 0; run < 3; run++) {
                for (var n = 0; n <= run; n++) {
                    var x = client.Right - step * (run + 1) + step * n;
                    var y = client.Bottom - step * (n + 1);

                    e.Canvas.FillRectangle (new Rectangle (x, y, dot, dot), Theme.BorderMidColor);
                }
            }
        }
    }
}
