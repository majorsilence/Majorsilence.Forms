using System.Collections.ObjectModel;
using System.Drawing;

namespace Majorsilence.Forms
{
    /// <summary>
    /// Represents a ToolBar control.
    /// </summary>
    public partial class ToolBar : MenuBase
    {
        private ToolBarButtonCollection? _buttons;

        /// <summary>Gets the collection of ToolBarButtons in this toolbar.</summary>
        public ToolBarButtonCollection Buttons => _buttons ??= new ToolBarButtonCollection ();

        /// <summary>Fires when a ToolBarButton is clicked.</summary>
        public event EventHandler<ToolBarButtonClickEventArgs>? ButtonClick { add { } remove { } }

        private ImageList? image_list;

        /// <summary>
        /// Gets or sets the ImageList this strip's items index into through
        /// <see cref="ToolStripItem.ImageIndex"/> / <see cref="ToolStripItem.ImageKey"/>.
        /// </summary>
        /// <remarks>
        /// Assigning this re-lays out the strip as well as repainting it, because an item's preferred
        /// size is measured from the image it will draw.
        /// </remarks>
        public ImageList? ImageList {
            get => image_list;
            set {
                if (ReferenceEquals (image_list, value))
                    return;

                image_list = value;

                PerformLayout ();
                Invalidate ();
            }
        }

        /// <summary>Gets or sets the size of the toolbar buttons. Stub in Majorsilence.Forms.</summary>
        public System.Drawing.Size ButtonSize { get; set; } = new System.Drawing.Size (24, 22);
        /// <summary>
        /// Initializes a new instance of the ToolBar class.
        /// </summary>
        public ToolBar ()
        {
            Dock = DockStyle.Top;
        }

        /// <summary>
        /// Initializes a new instance of the ToolBar class with the provided root MenuItem. Exists so
        /// derived strips further down the chain (ToolStrip -> MenuDropDown) can still reach
        /// <see cref="MenuBase(MenuItem)"/>, which is what backs every item's sub-menu drop down.
        /// </summary>
        protected ToolBar (MenuItem root) : base (root)
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
            // Hidden items are excluded, as Menu, MenuDropDown and StatusStrip all already do. Laying
            // them out left a button that was painted but skipped by hit-testing -- a dead, visible
            // button -- which is what permission-based toolbar trimming produced (TSM-04).
            StackLayoutEngine.HorizontalExpand.Layout (
                LogicalClientRectangle,
                Items.Cast<MenuItem> ().Where (i => i.Visible).Cast<ILayoutable> ());
        }

        /// <inheritdoc/>
        public override ControlStyle Style { get; } = new ControlStyle (DefaultStyle);
    }

    /// <summary>Represents a button on a ToolBar control.</summary>
    public partial class ToolBarButton
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
