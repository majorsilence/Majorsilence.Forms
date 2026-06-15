using System.Collections.ObjectModel;
using System.Drawing;

namespace Modern.Forms
{
    /// <summary>
    /// Represents a ToolBar control.
    /// </summary>
    public class ToolBar : MenuBase
    {
        private ToolBarButtonCollection? _buttons;

        /// <summary>Gets the collection of ToolBarButtons in this toolbar.</summary>
        public ToolBarButtonCollection Buttons => _buttons ??= new ToolBarButtonCollection ();

        /// <summary>Fires when a ToolBarButton is clicked.</summary>
        public event EventHandler<ToolBarButtonClickEventArgs>? ButtonClick { add { } remove { } }

        /// <summary>Gets or sets the ImageList used by this toolbar's buttons. Stub in Modern.Forms.</summary>
        public ImageList? ImageList { get; set; }

        /// <summary>Gets or sets the size of the toolbar buttons. Stub in Modern.Forms.</summary>
        public System.Drawing.Size ButtonSize { get; set; } = new System.Drawing.Size (24, 22);
        /// <summary>
        /// Initializes a new instance of the ToolBar class.
        /// </summary>
        public ToolBar ()
        {
            Dock = DockStyle.Top;
        }

        /// <inheritdoc/>
        protected override Size DefaultSize => new Size (600, 34);

        /// <inheritdoc/>
        public new static ControlStyle DefaultStyle = new ControlStyle (Control.DefaultStyle,
          (style) => {
              style.Border.Bottom.Width = 1;
          });

        /// <inheritdoc/>
        protected override bool IsTopLevelMenu => true;

        /// <inheritdoc/>
        protected override void LayoutItems ()
        {
            StackLayoutEngine.HorizontalExpand.Layout (ClientRectangle, Items.Cast<ILayoutable> ());
        }

        /// <inheritdoc/>
        public override ControlStyle Style { get; } = new ControlStyle (DefaultStyle);
    }

    /// <summary>Represents a button on a ToolBar control.</summary>
    public class ToolBarButton
    {
        /// <summary>Gets or sets the text of the button.</summary>
        public string Text { get; set; } = string.Empty;

        /// <summary>Gets or sets the tooltip text of the button.</summary>
        public string ToolTipText { get; set; } = string.Empty;

        /// <summary>Gets or sets the style of the button.</summary>
        public ToolBarButtonStyle Style { get; set; } = ToolBarButtonStyle.PushButton;

        /// <summary>Gets or sets whether the button is enabled.</summary>
        public bool Enabled { get; set; } = true;

        /// <summary>Gets or sets whether the button is visible.</summary>
        public bool Visible { get; set; } = true;

        /// <summary>Gets or sets whether the button is in a pushed state (toggle).</summary>
        public bool Pushed { get; set; }

        /// <summary>Gets or sets whether the button is partially pushed (dropdown).</summary>
        public bool PartialPush { get; set; }

        /// <summary>Gets or sets the image index in the parent ToolBar's ImageList.</summary>
        public int ImageIndex { get; set; } = -1;

        /// <summary>Gets or sets the image key in the parent ToolBar's ImageList.</summary>
        public string ImageKey { get; set; } = string.Empty;

        /// <summary>Gets or sets an object with additional user data about this button.</summary>
        public object? Tag { get; set; }

        /// <summary>Gets or sets the name of the button.</summary>
        public string Name { get; set; } = string.Empty;

        /// <summary>Gets or sets the drop-down menu for DropDownButton-style buttons.</summary>
        public ContextMenu? DropDownMenu { get; set; }
    }

    /// <summary>A collection of ToolBarButton objects.</summary>
    public class ToolBarButtonCollection : Collection<ToolBarButton>
    {
        /// <summary>Adds a button with the specified text.</summary>
        public ToolBarButton Add (string text)
        {
            var button = new ToolBarButton { Text = text };
            Add (button);
            return button;
        }
    }

    /// <summary>Provides data for the ToolBar.ButtonClick event.</summary>
    public class ToolBarButtonClickEventArgs : EventArgs
    {
        /// <summary>Gets the button that was clicked.</summary>
        public ToolBarButton Button { get; }

        /// <summary>Initializes a new instance.</summary>
        public ToolBarButtonClickEventArgs (ToolBarButton button) => Button = button;
    }

    /// <summary>Specifies the style of a ToolBarButton.</summary>
    public enum ToolBarButtonStyle
    {
        /// <summary>A standard push button.</summary>
        PushButton = 1,
        /// <summary>A toggle button.</summary>
        ToggleButton = 2,
        /// <summary>A separator.</summary>
        Separator = 3,
        /// <summary>A drop-down button.</summary>
        DropDownButton = 4
    }
}
