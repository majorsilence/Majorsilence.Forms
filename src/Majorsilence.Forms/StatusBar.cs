using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Drawing;
using Majorsilence.Forms.Renderers;

namespace Majorsilence.Forms
{
    /// <summary>
    /// Represents a StatusBar control.
    /// </summary>
    public partial class StatusBar : Control
    {
        private StatusBarPanelCollection? _panels;

        /// <summary>Gets the collection of panels displayed in this status bar.</summary>
        public StatusBarPanelCollection Panels => _panels ??= new StatusBarPanelCollection (this);

        /// <summary>Gets or sets whether panels are shown. When false, the status bar shows only the Text property.</summary>
        /// <remarks>Read by the renderer as of W6 mechanisms; see <see cref="StatusBarPanel"/>.</remarks>
        public bool ShowPanels {
            get => show_panels;
            set {
                if (show_panels == value)
                    return;

                show_panels = value;
                Invalidate ();
            }
        }

        private bool show_panels;

        // ── panel layout (W6 mechanisms) ──────────────────────────────────────────────────────────
        // Device pixels throughout, like every other renderer-facing rectangle. Fixed panels take their
        // Width, Contents panels fit their icon and text, and the Spring panels share what is left;
        // every panel is at least MinWidth wide. The grip square at the trailing edge is reserved first.

        /// <summary>The width the sizing grip reserves at the trailing edge, in device pixels.</summary>
        internal int ScaledGripWidth => SizingGrip ? LogicalToDeviceUnits (16) : 0;

        /// <summary>Lays the panels out across the padded client area and records each one's bounds.</summary>
        internal void LayoutPanels ()
        {
            var area = PaddedClientRectangle;
            var available = Math.Max (0, area.Width - ScaledGripWidth);
            var widths = new int[Panels.Count];
            var springs = 0;
            var used = 0;

            for (var i = 0; i < Panels.Count; i++) {
                var panel = Panels[i];
                var minimum = LogicalToDeviceUnits (panel.MinWidth);

                widths[i] = panel.AutoSize switch {
                    StatusBarPanelAutoSize.Contents => Math.Max (minimum, MeasurePanelContents (panel)),
                    StatusBarPanelAutoSize.Spring => minimum,
                    _ => Math.Max (minimum, LogicalToDeviceUnits (panel.Width)),
                };

                if (panel.AutoSize == StatusBarPanelAutoSize.Spring)
                    springs++;

                used += widths[i];
            }

            if (springs > 0 && available > used) {
                var share = (available - used) / springs;

                for (var i = 0; i < Panels.Count; i++)
                    if (Panels[i].AutoSize == StatusBarPanelAutoSize.Spring)
                        widths[i] += share;
            }

            var x = area.Left;

            for (var i = 0; i < Panels.Count; i++) {
                Panels[i].DeviceBounds = new Rectangle (x, area.Top, widths[i], area.Height);
                x += widths[i];
            }
        }

        private int MeasurePanelContents (StatusBarPanel panel)
        {
            var width = (int)TextMeasurer.MeasureText (panel.Text, this).Width + LogicalToDeviceUnits (8);

            if (panel.Icon is not null)
                width += LogicalToDeviceUnits (panel.Icon.Width + 4);

            return width;
        }

        /// <summary>The panel under a device-space point, or null.</summary>
        internal StatusBarPanel? PanelAt (Point device)
        {
            if (!ShowPanels)
                return null;

            LayoutPanels ();

            foreach (var panel in Panels)
                if (panel.DeviceBounds.Contains (device))
                    return panel;

            return null;
        }

        /// <inheritdoc/>
        protected override void OnMouseClick (MouseEventArgs e)
        {
            base.OnMouseClick (e);

            // PanelClick (W6 mechanisms): the panel under the click, when panels are shown.
            var device = new Point (LogicalToDeviceUnits (e.X), LogicalToDeviceUnits (e.Y));

            if (PanelAt (device) is { } panel)
                OnPanelClick (new StatusBarPanelClickEventArgs (panel, e.Button, e.Clicks, e.X, e.Y));
        }

        /// <inheritdoc/>
        /// <remarks><see cref="StatusBarPanel.ToolTipText"/>, shown per panel (W6 mechanisms).</remarks>
        internal override string? GetToolTipText (Point location)
        {
            var device = new Point (LogicalToDeviceUnits (location.X), LogicalToDeviceUnits (location.Y));
            var text = PanelAt (device)?.ToolTipText;

            return string.IsNullOrEmpty (text) ? null : text;
        }

        // The renderer's door to DrawItem for an owner-drawn panel; the event is declared in FinalParity.cs.
        internal void RaiseDrawItem (StatusBarPanel panel, int index, Rectangle bounds, PaintEventArgs e)
        {
            var args = new StatusBarDrawItemEventArgs (e.Graphics, Font, bounds, index, DrawItemState.None, panel,
                ForeColor, BackColor);

            OnDrawItem (args);
        }

        /// <summary>Raises the <see cref="DrawItem"/> event.</summary>
        protected virtual void OnDrawItem (StatusBarDrawItemEventArgs sbdievent) => DrawItem?.Invoke (this, sbdievent);

        /// <summary>Raises the <see cref="PanelClick"/> event.</summary>
        protected virtual void OnPanelClick (StatusBarPanelClickEventArgs e) => PanelClick?.Invoke (this, e);
        /// <summary>
        /// Initializes a new instance of the StatusBar class.
        /// </summary>
        public StatusBar ()
        {
            Dock = DockStyle.Bottom;
            SetControlBehavior (ControlBehaviors.InvalidateOnTextChanged);
        }

        /// <inheritdoc/>
        protected override Padding DefaultPadding => new Padding (3);

        /// <inheritdoc/>
        protected override Size DefaultSize => new Size (600, 25);

        /// <inheritdoc/>
        public new static ControlStyle DefaultStyle = new ControlStyle (Control.DefaultStyle,
            (style) => {
                style.Border.Top.Width = 1;
                style.FontSize = 13;
            });

        /// <inheritdoc/>
        protected override void OnPaint (PaintEventArgs e)
        {
            base.OnPaint (e);

            RenderManager.Render (this, e);
        }

        /// <inheritdoc/>
        public override ControlStyle Style { get; } = new ControlStyle (DefaultStyle);
    }

    /// <summary>Represents a panel in a StatusBar control.</summary>
    /// <remarks>
    /// Drawn as of W6 mechanisms: <see cref="StatusBar.ShowPanels"/> lays these out and
    /// <see cref="Renderers.StatusBarRenderer"/> paints each one -- border, icon, aligned text, or the
    /// <see cref="StatusBar.DrawItem"/> event for an owner-drawn panel. Every property below used to be
    /// stored and read by nothing, so a panelled status bar showed its <c>Text</c> and nothing else.
    /// Each setter repaints the owning bar.
    /// </remarks>
    public partial class StatusBarPanel : System.ComponentModel.Component
    {
        private string text = string.Empty;
        private string tool_tip_text = string.Empty;
        private int width = 100;
        private int min_width = 10;
        private HorizontalAlignment alignment = HorizontalAlignment.Left;
        private StatusBarPanelAutoSize auto_size = StatusBarPanelAutoSize.None;
        private StatusBarPanelBorderStyle border_style = StatusBarPanelBorderStyle.Sunken;
        private Majorsilence.Forms.Drawing.Icon? icon;
        private StatusBarPanelStyle style = StatusBarPanelStyle.Text;

        /// <summary>Gets or sets the text of the panel.</summary>
        public string Text {
            get => text;
            set => Set (ref text, value ?? string.Empty);
        }

        /// <summary>Gets or sets the tooltip text of the panel.</summary>
        /// <remarks>Shown when the pointer rests over the panel, through the bar's item-tip path.</remarks>
        public string ToolTipText {
            get => tool_tip_text;
            set => tool_tip_text = value ?? string.Empty;
        }

        /// <summary>Gets or sets the width of the panel in pixels.</summary>
        /// <remarks>The laid-out width when <see cref="AutoSize"/> is <see cref="StatusBarPanelAutoSize.None"/>;
        /// never less than <see cref="MinWidth"/>.</remarks>
        public int Width {
            get => width;
            set => Set (ref width, Math.Max (0, value));
        }

        /// <summary>Gets or sets the minimum width of the panel.</summary>
        public int MinWidth {
            get => min_width;
            set => Set (ref min_width, Math.Max (0, value));
        }

        /// <summary>Gets or sets the alignment of the text in the panel.</summary>
        public HorizontalAlignment Alignment {
            get => alignment;
            set => Set (ref alignment, value);
        }

        /// <summary>Gets or sets the auto-size mode for this panel.</summary>
        /// <remarks><see cref="StatusBarPanelAutoSize.Contents"/> fits the icon and text;
        /// <see cref="StatusBarPanelAutoSize.Spring"/> shares whatever width the fixed panels leave.</remarks>
        public StatusBarPanelAutoSize AutoSize {
            get => auto_size;
            set => Set (ref auto_size, value);
        }

        /// <summary>Gets or sets the border style of the panel.</summary>
        public StatusBarPanelBorderStyle BorderStyle {
            get => border_style;
            set => Set (ref border_style, value);
        }

        /// <summary>Gets or sets the icon displayed in this panel.</summary>
        public Majorsilence.Forms.Drawing.Icon? Icon {
            get => icon;
            set => Set (ref icon, value);
        }

        /// <summary>Gets or sets an object with additional user data about this panel.</summary>
        public object? Tag { get; set; }

        /// <summary>Gets or sets the name of the panel.</summary>
        public string Name { get; set; } = string.Empty;

        /// <summary>Gets or sets the style of the panel.</summary>
        /// <remarks><see cref="StatusBarPanelStyle.OwnerDraw"/> hands the panel's rectangle to the bar's
        /// <see cref="StatusBar.DrawItem"/> event instead of painting the text.</remarks>
        public StatusBarPanelStyle Style {
            get => style;
            set => Set (ref style, value);
        }

        /// <summary>The panel's laid-out rectangle in device pixels, from the bar's last layout.</summary>
        internal Rectangle DeviceBounds { get; set; }

        private void Set<T> (ref T field, T value)
        {
            if (EqualityComparer<T>.Default.Equals (field, value))
                return;

            field = value;
            Parent?.Invalidate ();
        }
    }

    /// <summary>A collection of StatusBarPanel objects.</summary>
    public class StatusBarPanelCollection : Collection<StatusBarPanel>
    {
        /// <summary>Adds a panel with the specified text.</summary>
        public StatusBarPanel Add (string text)
        {
            var panel = new StatusBarPanel { Text = text };
            Add (panel);
            return panel;
        }
    }

    /// <summary>Specifies the auto-size mode of a StatusBarPanel.</summary>
    public enum StatusBarPanelAutoSize
    {
        /// <summary>The panel is not auto-sized.</summary>
        None = 1,
        /// <summary>The panel springs to fill available space.</summary>
        Spring = 2,
        /// <summary>The panel width is set to the width of its contents.</summary>
        Contents = 3
    }

    /// <summary>Specifies the style of content displayed in a StatusBarPanel.</summary>
    public enum StatusBarPanelStyle
    {
        /// <summary>The panel displays text and an optional icon.</summary>
        Text = 1,
        /// <summary>The panel is owner-drawn.</summary>
        OwnerDraw = 2
    }

    /// <summary>Specifies the border style of a StatusBarPanel.</summary>
    public enum StatusBarPanelBorderStyle
    {
        /// <summary>No border.</summary>
        None = 1,
        /// <summary>A raised border.</summary>
        Raised = 2,
        /// <summary>A sunken border.</summary>
        Sunken = 3
    }
}
