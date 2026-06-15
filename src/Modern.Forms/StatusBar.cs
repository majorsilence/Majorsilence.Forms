using System.Collections.ObjectModel;
using System.Drawing;
using Modern.Forms.Renderers;

namespace Modern.Forms
{
    /// <summary>
    /// Represents a StatusBar control.
    /// </summary>
    public class StatusBar : Control
    {
        private StatusBarPanelCollection? _panels;

        /// <summary>Gets the collection of panels displayed in this status bar.</summary>
        public StatusBarPanelCollection Panels => _panels ??= new StatusBarPanelCollection ();

        /// <summary>Gets or sets whether panels are shown. When false, the status bar shows only the Text property.</summary>
        public bool ShowPanels { get; set; }
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
    public class StatusBarPanel : System.ComponentModel.Component
    {
        /// <summary>Gets or sets the text of the panel.</summary>
        public string Text { get; set; } = string.Empty;

        /// <summary>Gets or sets the tooltip text of the panel.</summary>
        public string ToolTipText { get; set; } = string.Empty;

        /// <summary>Gets or sets the width of the panel in pixels.</summary>
        public int Width { get; set; } = 100;

        /// <summary>Gets or sets the minimum width of the panel.</summary>
        public int MinWidth { get; set; } = 10;

        /// <summary>Gets or sets the alignment of the text in the panel.</summary>
        public HorizontalAlignment Alignment { get; set; } = HorizontalAlignment.Left;

        /// <summary>Gets or sets the auto-size mode for this panel.</summary>
        public StatusBarPanelAutoSize AutoSize { get; set; } = StatusBarPanelAutoSize.None;

        /// <summary>Gets or sets the border style of the panel.</summary>
        public StatusBarPanelBorderStyle BorderStyle { get; set; } = StatusBarPanelBorderStyle.Sunken;

        /// <summary>Gets or sets the icon displayed in this panel.</summary>
        public Modern.Drawing.Icon? Icon { get; set; }

        /// <summary>Gets or sets an object with additional user data about this panel.</summary>
        public object? Tag { get; set; }

        /// <summary>Gets or sets the name of the panel.</summary>
        public string Name { get; set; } = string.Empty;

        /// <summary>Gets or sets the style of the panel. Stub in Modern.Forms.</summary>
        public StatusBarPanelStyle Style { get; set; } = StatusBarPanelStyle.Text;
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
