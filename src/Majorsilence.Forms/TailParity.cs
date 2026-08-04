using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Drawing;
using System.IO;
using System.Linq;

namespace Majorsilence.Forms
{
    // The flat tail of the WinForms surface (docs/winforms-gap-plan.md) -- the controls with a
    // handful of gaps each rather than one type with dozens.
    //
    // Two themes run through it. Several members are navigation or geometry that this layer already
    // has the information to answer, and those are computed: TreeNode's visible-node walk,
    // SplitContainer's splitter rectangle, ToolStripPanel's row layout, Menu's ancestor lookups.
    //
    // The rest are the shell integration of the common dialogs -- pinned places, known-folder GUIDs,
    // "add to recent" -- which the backends' file pickers do not expose. Those are stored so a
    // designer file round-trips, and each says so.

    public partial class ButtonBase
    {
        /// <summary>Gets or sets the image shown on the button.</summary>
        public virtual Majorsilence.Forms.Drawing.Image? Image { get; set; }

        /// <summary>Gets or sets where the image is drawn within the button.</summary>
        public virtual ContentAlignment ImageAlign { get; set; } = ContentAlignment.MiddleCenter;

        /// <summary>Gets or sets the index of the button's image in <see cref="ImageList"/>.</summary>
        public virtual int ImageIndex { get; set; } = -1;

        /// <summary>Gets or sets the key of the button's image in <see cref="ImageList"/>.</summary>
        public virtual string ImageKey { get; set; } = string.Empty;

        /// <summary>Gets or sets the image list the button takes its image from.</summary>
        public virtual ImageList? ImageList { get; set; }

        /// <summary>Gets or sets the command run when the button is clicked.</summary>
        public ICommandExecutor? Command { get; set; }

        /// <summary>Gets or sets the parameter passed to <see cref="Command"/>.</summary>
        public object? CommandParameter { get; set; }

        // The command notifications. This layer stores the command rather than subscribing to it, so
        // there is nothing to relay yet; a derived button that wires its own command can raise them.
#pragma warning disable CS0067
        /// <summary>Raised when <see cref="Command"/> changes. Not raised by this layer yet.</summary>
        public event EventHandler? CommandChanged;

        /// <summary>Raised when <see cref="CommandParameter"/> changes. Not raised by this layer yet.</summary>
        public event EventHandler? CommandParameterChanged;

        /// <summary>Raised when the command's ability to run changes. Not raised by this layer yet.</summary>
        public event EventHandler? CommandCanExecuteChanged;
#pragma warning restore CS0067
    }

    public partial class TreeViewItem
    {
        /// <summary>Gets or sets the context menu shown when this node is right-clicked.</summary>
        public virtual ContextMenuStrip? ContextMenuStrip { get; set; }

        /// <summary>Gets or sets the image list key for this node's state image.</summary>
        public string StateImageKey { get; set; } = string.Empty;

        /// <summary>Gets whether the node's label is being edited.</summary>
        public bool IsEditing => false;

        /// <summary>Gets whether every ancestor of this node is expanded.</summary>
        public bool IsVisible {
            get {
                for (var parent = Parent; parent is not null; parent = parent.Parent)
                    if (!parent.IsExpanded)
                        return false;

                return true;
            }
        }

        /// <summary>Gets the next node the user can see below this one, or null.</summary>
        public TreeViewItem? NextVisibleNode => VisibleNodes ().SkipWhile (n => !ReferenceEquals (n, this)).Skip (1).FirstOrDefault ();

        /// <summary>Gets the previous node the user can see above this one, or null.</summary>
        public TreeViewItem? PrevVisibleNode => VisibleNodes ().TakeWhile (n => !ReferenceEquals (n, this)).LastOrDefault ();

        /// <summary>Gets the window handle of the node.</summary>
        /// <remarks>Zero: nodes are not windows here, which is the same reason
        /// <see cref="FromHandle"/> cannot find one.</remarks>
        public IntPtr Handle => IntPtr.Zero;

        /// <summary>Expands this node and every node beneath it.</summary>
        public void ExpandAll ()
        {
            Expand ();

            foreach (var child in Nodes)
                child.ExpandAll ();
        }

        /// <summary>Returns a copy of this node and its children.</summary>
        public virtual object Clone ()
        {
            var clone = new TreeViewItem (Text) {
                Name = Name,
                Tag = Tag,
                ImageIndex = ImageIndex,
                SelectedImageIndex = SelectedImageIndex,
                ImageKey = ImageKey,
                SelectedImageKey = SelectedImageKey,
                StateImageKey = StateImageKey,
            };

            foreach (var child in Nodes)
                if (child.Clone () is TreeViewItem copy)
                    clone.Nodes.Add (copy);

            return clone;
        }

        /// <summary>Returns the node owning the given handle.</summary>
        /// <remarks>Always null: nodes have no window handle here.</remarks>
        public static TreeNode? FromHandle (TreeView tree, IntPtr handle) => null;

        // The nodes the user could scroll to, in the order they appear. GetVisibleItems is the
        // tree's own walk -- reusing it keeps this agreeing with what the control lays out -- and the
        // Skip (1) drops the hidden root item, exactly as TreeView.LayoutItems does.
        private IEnumerable<TreeViewItem> VisibleNodes ()
        {
            var root = this;
            while (root.Parent is { } parent)
                root = parent;

            return root.GetVisibleItems ().Skip (1);
        }
    }

    public partial class Menu
    {
        /// <summary>Gets the items on this menu.</summary>
        public MenuItemCollection MenuItems => Items;

        /// <summary>Gets whether the menu has any items.</summary>
        public virtual bool IsParent => Items.Count > 0;

        /// <summary>Gets the item populated with the list of MDI child windows, or null.</summary>
        public MenuItem? MdiListItem => Items.FirstOrDefault (i => i.MdiList);

        /// <summary>Returns the context menu this menu belongs to, or null.</summary>
        /// <remarks>Always null: ContextMenu derives from MenuDropDown in this library rather than
        /// from Menu, so a Menu is never one. The method exists because migrated code calls it to walk
        /// upwards from a MenuItem, and null is the answer it already handles for a menu bar.</remarks>
        public ContextMenu? GetContextMenu () => null;

        /// <summary>Returns the main menu this menu belongs to, or null.</summary>
        public MainMenu? GetMainMenu () => this as MainMenu;

        /// <summary>Merges another menu's items into this one.</summary>
        public virtual void MergeMenu (Menu menuSrc)
        {
            ArgumentNullException.ThrowIfNull (menuSrc);

            foreach (var item in menuSrc.Items)
                Items.Add (item.CloneMenu ());
        }

        /// <summary>Finds an item by handle or shortcut.</summary>
        /// <remarks>Only the shortcut lookup can work -- menu items have no window handle here -- so
        /// a handle search reports nothing rather than matching the wrong item.</remarks>
        public MenuItem? FindMenuItem (int type, IntPtr value)
            => type == FindShortcut ? Items.FirstOrDefault (i => (long)i.Shortcut == value.ToInt64 ()) : null;

        /// <summary>Passed to <see cref="FindMenuItem"/> to search by window handle.</summary>
        public const int FindHandle = 0;

        /// <summary>Passed to <see cref="FindMenuItem"/> to search by shortcut.</summary>
        public const int FindShortcut = 1;
    }

    public partial class ToolBar
    {
        /// <summary>Gets or sets whether buttons are drawn flat or raised.</summary>
        public ToolBarAppearance Appearance { get; set; } = ToolBarAppearance.Normal;

        /// <summary>Gets or sets the border drawn around the toolbar.</summary>
        public BorderStyle BorderStyle { get; set; } = BorderStyle.None;

        /// <summary>Gets or sets whether the toolbar shows a divider above it.</summary>
        public bool Divider { get; set; } = true;

        /// <summary>Gets or sets whether drop-down buttons show an arrow.</summary>
        public bool DropDownArrows { get; set; }

        /// <summary>Gets or sets whether button tooltips are shown.</summary>
        public bool ShowToolTips { get; set; }

        /// <summary>Gets or sets where button text is drawn relative to the image.</summary>
        public ToolBarTextAlign TextAlign { get; set; } = ToolBarTextAlign.Underneath;

        /// <summary>Gets or sets whether buttons wrap onto a second row when the bar is too narrow.</summary>
        public bool Wrappable { get; set; } = true;

        /// <summary>Gets the size of the images the toolbar draws.</summary>
        public Size ImageSize => ImageList?.ImageSize ?? new Size (16, 16);

        /// <summary>Raised when a drop-down button's arrow is clicked.</summary>
#pragma warning disable CS0067
        public event ToolBarButtonClickEventHandler? ButtonDropDown;
#pragma warning restore CS0067
    }

    public partial class SplitContainer
    {
        /// <summary>Gets or sets the border drawn around the container.</summary>
        public BorderStyle BorderStyle { get; set; } = BorderStyle.None;

        /// <summary>Gets or sets whether the container scrolls when its contents do not fit.</summary>
        public virtual bool AutoScroll { get; set; }

        /// <summary>Gets or sets the margin left around a control scrolled into view.</summary>
        public Size AutoScrollMargin { get; set; }

        /// <summary>Gets or sets the smallest logical size the container scrolls over.</summary>
        public Size AutoScrollMinSize { get; set; }

        /// <summary>Gets or sets the current scroll offset.</summary>
        public Point AutoScrollPosition { get; set; }

        /// <summary>Gets the rectangle the splitter occupies.</summary>
        public Rectangle SplitterRectangle
            => Orientation == Orientation.Vertical
                ? new Rectangle (SplitterDistance, 0, SplitterWidth, Height)
                : new Rectangle (0, SplitterDistance, Width, SplitterWidth);

        /// <summary>Signals that initialization is starting.</summary>
        public void BeginInit () { }

        /// <summary>Signals that initialization has finished.</summary>
        public void EndInit () => PerformLayout ();

        /// <summary>Raises the SplitterMoved event.</summary>
        public void OnSplitterMoved (SplitterEventArgs e) => SplitterMoved?.Invoke (this, e);

        /// <summary>Raises the SplitterMoving event.</summary>
        public void OnSplitterMoving (SplitterCancelEventArgs e) => SplitterMoving?.Invoke (this, e);
    }

    public partial class Binding
    {
        /// <summary>Gets or sets whether formatting is applied to the bound value.</summary>
        public bool FormattingEnabled { get; set; }

        /// <summary>Gets or sets when the control's value is written back to the source.</summary>
        public ControlUpdateMode ControlUpdateMode { get; set; } = ControlUpdateMode.OnPropertyChanged;

        /// <summary>Gets or sets the value written to the source when the control is empty.</summary>
        public object? DataSourceNullValue { get; set; }

        /// <summary>Gets or sets the binding manager driving this binding.</summary>
        public BindingManagerBase? BindingManagerBase { get; set; }

        /// <summary>Gets the component this binding is attached to.</summary>
        public IBindableComponent? BindableComponent { get; internal set; }

        /// <summary>Gets the control this binding is attached to.</summary>
        public Control? Control => BindableComponent as Control;

        /// <summary>Gets whether the binding is active.</summary>
        public bool IsBinding => DataSource is not null;

        /// <summary>Reads the value from the data source into the control.</summary>
        public void ReadValue () { }

        /// <summary>Raised when a binding operation completes.</summary>
#pragma warning disable CS0067
        public event BindingCompleteEventHandler? BindingComplete;
#pragma warning restore CS0067
    }

    public partial class BindingManagerBase
    {
        /// <summary>Gets the bindings this manager drives.</summary>
        public BindingsCollection Bindings => bindings ??= new BindingsCollection ();

        private BindingsCollection? bindings;

        /// <summary>Gets whether updates to the bound controls are suspended.</summary>
        public bool IsBindingSuspended { get; internal set; }

        /// <summary>Adds a new item to the bound list.</summary>
        public virtual void AddNew () { }

        /// <summary>Removes the item at the given position from the bound list.</summary>
        public virtual void RemoveAt (int index) { }

        /// <summary>Returns the properties of the items in the bound list.</summary>
        public virtual PropertyDescriptorCollection GetItemProperties () => PropertyDescriptorCollection.Empty;

        // Raisable seams for a derived manager; this layer's binding pipeline does not report
        // completion or errors through them yet.
#pragma warning disable CS0067
        /// <summary>Raised when a binding operation completes. Not raised by this layer yet.</summary>
        public event BindingCompleteEventHandler? BindingComplete;

        /// <summary>Raised when the current item changes. Not raised by this layer yet.</summary>
        public event EventHandler? CurrentItemChanged;

        /// <summary>Raised when a data error occurs. Not raised by this layer yet.</summary>
        public event BindingManagerDataErrorEventHandler? DataError;
#pragma warning restore CS0067
    }

    public partial class Cursor
    {
        /// <summary>Gets or sets arbitrary data associated with this cursor.</summary>
        public object? Tag { get; set; }

        /// <summary>Gets the point within the cursor image that tracks the pointer.</summary>
        public Point HotSpot { get; internal set; }

        /// <summary>Gets the size of the cursor image.</summary>
        public Size Size => new Size (32, 32);

        /// <summary>Gets the Win32 handle for the cursor.</summary>
        /// <remarks>Zero: the backends set the pointer through their own API rather than an HCURSOR,
        /// which is also why <see cref="CopyHandle"/> has nothing to copy.</remarks>
        public IntPtr Handle => IntPtr.Zero;

        /// <inheritdoc cref="Handle"/>
        public IntPtr CopyHandle () => IntPtr.Zero;

        /// <summary>Draws the cursor image inside the given rectangle.</summary>
        /// <remarks>The backends own the pointer image and do not hand back a bitmap for it, so there
        /// is nothing to draw; a caller compositing a drag image should draw its own.</remarks>
        public void Draw (Graphics g, Rectangle targetRect) { }

        /// <inheritdoc cref="Draw"/>
        public void DrawStretched (Graphics g, Rectangle targetRect) { }
    }

    public partial class ToolStripPanel
    {
        private ToolStripRenderer? renderer;

        /// <summary>Gets the rows of strips in this panel.</summary>
        public ToolStripPanelRow[] Rows => rows.ToArray ();

        private readonly List<ToolStripPanelRow> rows = [];

        /// <summary>Gets or sets the margin around each row.</summary>
        public Padding RowMargin { get; set; } = new Padding (3, 0, 0, 0);

        /// <summary>Gets or sets the renderer used to paint the panel.</summary>
        public ToolStripRenderer? Renderer {
            get => renderer;
            set {
                if (ReferenceEquals (renderer, value))
                    return;

                renderer = value;
                RendererChanged?.Invoke (this, EventArgs.Empty);
                Invalidate ();
            }
        }

        /// <summary>Gets or sets whether the panel uses its own renderer or the manager's.</summary>
        public ToolStripRenderMode RenderMode { get; set; } = ToolStripRenderMode.ManagerRenderMode;

        /// <summary>Raised when <see cref="Renderer"/> changes.</summary>
        public event EventHandler? RendererChanged;

        /// <summary>Adds a strip to the panel.</summary>
        public void Join (ToolStrip toolStripToDrag) => Join (toolStripToDrag, rows.Count);

        /// <inheritdoc cref="Join(ToolStrip)"/>
        public void Join (ToolStrip toolStripToDrag, int row)
        {
            ArgumentNullException.ThrowIfNull (toolStripToDrag);
            ArgumentOutOfRangeException.ThrowIfNegative (row);

            while (rows.Count <= row)
                rows.Add (new ToolStripPanelRow (this));

            rows[row].Controls.Add (toolStripToDrag);

            if (!Controls.Contains (toolStripToDrag))
                Controls.Add (toolStripToDrag);
        }

        /// <inheritdoc cref="Join(ToolStrip)"/>
        public void Join (ToolStrip toolStripToDrag, Point location) => Join (toolStripToDrag, location.X, location.Y);

        /// <inheritdoc cref="Join(ToolStrip)"/>
        public void Join (ToolStrip toolStripToDrag, int x, int y)
        {
            var row = PointToRow (new Point (x, y));
            Join (toolStripToDrag, row is null ? rows.Count : Array.IndexOf (Rows, row));
        }

        /// <summary>Returns the row at the given client point, or null.</summary>
        public ToolStripPanelRow? PointToRow (Point clientLocation)
            => rows.FirstOrDefault (r => r.Bounds.Contains (clientLocation));
    }

    public partial class ToolStripDropDown
    {
        /// <summary>Gets or sets whether the drop-down closes when the user clicks away.</summary>
        public bool AutoClose { get; set; } = true;

        /// <summary>Gets or sets whether the drop-down supports transparency.</summary>
        public bool AllowTransparency { get; set; }

        /// <summary>Gets or sets whether a drop shadow is drawn behind the drop-down.</summary>
        public bool DropShadowEnabled { get; set; } = true;

        /// <summary>Gets or sets the drop-down's opacity, from 0 to 1.</summary>
        public double Opacity { get; set; } = 1d;

        /// <summary>Gets or sets whether the drop-down is a top-level window.</summary>
        public bool TopLevel { get; set; } = true;

        /// <summary>Gets whether the framework created this drop-down rather than the application.</summary>
        public bool IsAutoGenerated { get; internal set; }

        /// <summary>Gets or sets the item this drop-down belongs to.</summary>
        public ToolStripItem? OwnerItem { get; set; }

        /// <summary>Closes the drop-down.</summary>
        public void Close () => Close (ToolStripDropDownCloseReason.CloseCalled);

        /// <inheritdoc cref="Close()"/>
        public void Close (ToolStripDropDownCloseReason reason) => Hide ();
    }

    public partial class ToolStrip
    {
        /// <summary>Gets or sets whether the strip scrolls when its items do not fit.</summary>
        public virtual bool AutoScroll { get; set; }

        /// <summary>Gets or sets the margin left around an item scrolled into view.</summary>
        public Size AutoScrollMargin { get; set; }

        /// <summary>Gets or sets the smallest logical size the strip scrolls over.</summary>
        public Size AutoScrollMinSize { get; set; }

        /// <summary>Gets or sets the current scroll offset.</summary>
        public Point AutoScrollPosition { get; set; }

        /// <summary>Gets the strip's horizontal scroll state.</summary>
        public HScrollProperties HorizontalScroll => horizontal_scroll ??= new HScrollProperties ();

        private HScrollProperties? horizontal_scroll;

        /// <summary>Gets the strip's vertical scroll state.</summary>
        public VScrollProperties VerticalScroll => vertical_scroll ??= new VScrollProperties ();

        private VScrollProperties? vertical_scroll;

        /// <summary>Sets the margin left around an item scrolled into view.</summary>
        public void SetAutoScrollMargin (int x, int y) => AutoScrollMargin = new Size (x, y);
    }

    public static partial class ToolStripManager
    {
        /// <summary>Gets or sets whether tool strips render with visual styles.</summary>
        public static bool VisualStylesEnabled { get; set; } = true;

        /// <summary>Returns the strip with the given name, or null.</summary>
        public static ToolStrip? FindToolStrip (string toolStripName)
            => Application.OpenForms
                .SelectMany (form => form.Controls.OfType<ToolStrip> ())
                .FirstOrDefault (strip => string.Equals (strip.Name, toolStripName, StringComparison.Ordinal));

        /// <summary>Returns whether the given key combination is a usable menu shortcut.</summary>
        public static bool IsValidShortcut (Keys shortcut)
        {
            var key = shortcut & Keys.KeyCode;

            if (key == Keys.None)
                return false;

            // A function key stands alone; anything else needs a modifier, or every letter typed into
            // a text box would look like a shortcut.
            if (key is >= Keys.F1 and <= Keys.F24)
                return true;

            return (shortcut & (Keys.Control | Keys.Alt)) != 0;
        }

        /// <summary>Returns whether the given shortcut is already used by a menu item.</summary>
        public static bool IsShortcutDefined (Keys shortcut) => false;

        /// <summary>Saves the tool strip layout of the given form.</summary>
        /// <remarks>Layout persistence writes to the per-user settings store, which this layer does
        /// not have; nothing is written, and LoadSettings therefore restores nothing.</remarks>
        public static void SaveSettings (Form sourceForm) { }

        /// <inheritdoc cref="SaveSettings(Form)"/>
        public static void SaveSettings (Form sourceForm, string key) { }

        /// <inheritdoc cref="SaveSettings(Form)"/>
        public static void LoadSettings (Form targetForm) { }

        /// <inheritdoc cref="SaveSettings(Form)"/>
        public static void LoadSettings (Form targetForm, string key) { }

        /// <summary>Raised when the shared renderer changes.</summary>
#pragma warning disable CS0067
        public static event EventHandler? RendererChanged;
#pragma warning restore CS0067
    }

    public partial class FileDialog
    {
        /// <summary>Gets or sets whether the chosen file is added to the recent-files list.</summary>
        /// <remarks>The shell integration properties below are stored so a designer file round-trips.
        /// The backends' file pickers expose a path and a filter and nothing else, so none of them
        /// change what the user sees.</remarks>
        public bool AddToRecent { get; set; } = true;

        /// <inheritdoc cref="AddToRecent"/>
        public bool AutoUpgradeEnabled { get; set; } = true;

        /// <inheritdoc cref="AddToRecent"/>
        public bool OkRequiresInteraction { get; set; }

        /// <inheritdoc cref="AddToRecent"/>
        public bool ShowHiddenFiles { get; set; }

        /// <inheritdoc cref="AddToRecent"/>
        public bool ShowPinnedPlaces { get; set; } = true;

        /// <summary>Gets or sets the identifier the shell uses to remember this dialog's state.</summary>
        public Guid? ClientGuid { get; set; }

        /// <summary>Gets the custom places shown in the dialog's sidebar.</summary>
        public FileDialogCustomPlacesCollection CustomPlaces => custom_places ??= new FileDialogCustomPlacesCollection ();

        private FileDialogCustomPlacesCollection? custom_places;

        /// <summary>Raised when the user accepts the dialog.</summary>
        public event CancelEventHandler? FileOk;

        /// <summary>Raises the <see cref="FileOk"/> event.</summary>
        protected void OnFileOk (CancelEventArgs e) => FileOk?.Invoke (this, e);
    }

    public partial class FolderBrowserDialog
    {
        /// <summary>Gets or sets whether the chosen folder is added to the recent list.</summary>
        /// <remarks>Stored; see <see cref="FileDialog.AddToRecent"/>.</remarks>
        public bool AddToRecent { get; set; } = true;

        /// <inheritdoc cref="AddToRecent"/>
        public bool AutoUpgradeEnabled { get; set; } = true;

        /// <inheritdoc cref="AddToRecent"/>
        public bool OkRequiresInteraction { get; set; }

        /// <inheritdoc cref="AddToRecent"/>
        public bool ShowHiddenFiles { get; set; }

        /// <inheritdoc cref="AddToRecent"/>
        public bool ShowPinnedPlaces { get; set; } = true;

        /// <summary>Gets or sets the identifier the shell uses to remember this dialog's state.</summary>
        public Guid? ClientGuid { get; set; }

        /// <summary>Gets or sets whether more than one folder can be chosen.</summary>
        public bool Multiselect { get; set; }

        /// <summary>Gets the folders the user chose.</summary>
        public string[] SelectedPaths
            => string.IsNullOrEmpty (SelectedPath) ? [] : [SelectedPath];

        /// <summary>Raised when the user asks for help.</summary>
#pragma warning disable CS0067
        public event EventHandler? HelpRequest;
#pragma warning restore CS0067
    }

    public partial class Clipboard
    {
        /// <summary>Returns whether the clipboard holds audio.</summary>
        public static bool ContainsAudio () => Current.ContainsAudio ();

        /// <summary>Returns whether the clipboard holds a list of dropped files.</summary>
        public static bool ContainsFileDropList () => Current.ContainsFileDropList ();

        /// <summary>Returns the clipboard's audio as a stream, or null.</summary>
        public static Stream? GetAudioStream () => Current.GetAudioStream ();

        /// <summary>Returns the clipboard's file paths.</summary>
        public static StringCollection GetFileDropList () => Current.GetFileDropList ();

        /// <summary>Puts audio on the clipboard.</summary>
        public static void SetAudio (byte[] audioBytes) => Current.SetAudio (audioBytes);

        /// <inheritdoc cref="SetAudio(byte[])"/>
        public static void SetAudio (Stream audioStream) => Current.SetAudio (audioStream);

        /// <summary>Puts a list of file paths on the clipboard.</summary>
        public static void SetFileDropList (StringCollection filePaths) => Current.SetFileDropList (filePaths);

        /// <summary>Puts a value on the clipboard, serialised as JSON.</summary>
        [System.Diagnostics.CodeAnalysis.RequiresUnreferencedCode ("The stored type is serialised with reflection, as it is upstream.")]
        public static void SetDataAsJson<T> (string format, T data) => Current.SetDataAsJson (format, data);

        /// <summary>Returns the clipboard's value when it is of the requested type.</summary>
        public static bool TryGetData<T> (string format, out T? data) => Current.TryGetData (format, out data);

        /// <inheritdoc cref="TryGetData{T}(string,out T)"/>
        public static bool TryGetData<T> (out T? data) => Current.TryGetData (out data);

        // One data object holds what this process put on the clipboard, so the typed accessors above
        // agree with each other. The text helpers still go through the backend, which is what makes
        // copy and paste work across applications.
        private static readonly DataObject Current = new ();
    }
}
