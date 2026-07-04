using System.Collections.ObjectModel;
using System.Diagnostics.CodeAnalysis;
using System.Drawing;

namespace Majorsilence.Forms
{
    /// <summary>Represents a method that contains no parameters and does not return a value. Used with Control.Invoke().</summary>
    public delegate void MethodInvoker ();

    /// <summary>
    /// Provides information that accessibility clients use to adjust the user interface.
    /// Stub in Majorsilence.Forms.
    /// </summary>
    public class AccessibleObject
    {
        /// <summary>Gets or sets the accessible object name.</summary>
        public virtual string? Name { get; set; }

        /// <summary>Gets or sets the accessible description.</summary>
        public virtual string? Description { get; set; }

        /// <summary>Gets the accessible role.</summary>
        public virtual AccessibleRole Role => AccessibleRole.Default;

        /// <summary>Gets the parent accessible object.</summary>
        public virtual AccessibleObject? Parent => null;

        /// <summary>Gets the number of accessible children.</summary>
        public virtual int GetChildCount () => 0;

        /// <summary>Gets the child accessible object at the specified index.</summary>
        public virtual AccessibleObject? GetChild (int index) => null;

        /// <summary>Gets the bounds of the accessible object.</summary>
        public virtual System.Drawing.Rectangle Bounds => System.Drawing.Rectangle.Empty;
    }

    /// <summary>
    /// Provides a low-level encapsulation of a window handle and associated Windows message processing.
    /// Stub in Majorsilence.Forms — all operations are no-ops.
    /// </summary>
    public class NativeWindow
    {
        /// <summary>Gets the handle to the window. Always IntPtr.Zero in Majorsilence.Forms.</summary>
        public IntPtr Handle => IntPtr.Zero;

        /// <summary>Assigns a handle to this NativeWindow. No-op stub in Majorsilence.Forms.</summary>
        public virtual void AssignHandle (IntPtr handle) { }

        /// <summary>Creates a window with the specified creation parameters. No-op stub in Majorsilence.Forms.</summary>
        public virtual void CreateHandle (CreateParams cp) { }

        /// <summary>Destroys the associated window handle. No-op stub in Majorsilence.Forms.</summary>
        public virtual void DestroyHandle () { }

        /// <summary>Releases the associated window handle. No-op stub in Majorsilence.Forms.</summary>
        public virtual void ReleaseHandle () { }

        /// <summary>Processes messages sent to the window. No-op stub in Majorsilence.Forms.</summary>
        protected virtual void WndProc (ref Message m) { }
    }

    /// <summary>Encapsulates the information needed to create a window. Stub in Majorsilence.Forms.</summary>
    public class CreateParams
    {
        /// <summary>Gets or sets the window class name.</summary>
        public string? ClassName { get; set; }

        /// <summary>Gets or sets the window caption.</summary>
        public string? Caption { get; set; }

        /// <summary>Gets or sets the window styles.</summary>
        public int Style { get; set; }

        /// <summary>Gets or sets the extended window styles.</summary>
        public int ExStyle { get; set; }

        /// <summary>Gets or sets the class style bits.</summary>
        public int ClassStyle { get; set; }

        /// <summary>Gets or sets the left edge of the initial window position.</summary>
        public int X { get; set; }

        /// <summary>Gets or sets the top edge of the initial window position.</summary>
        public int Y { get; set; }

        /// <summary>Gets or sets the width of the initial window.</summary>
        public int Width { get; set; }

        /// <summary>Gets or sets the height of the initial window.</summary>
        public int Height { get; set; }

        /// <summary>Gets or sets the parent window handle.</summary>
        public IntPtr Parent { get; set; }

        /// <summary>Gets or sets extra parameter data.</summary>
        public object? Param { get; set; }
    }

    /// <summary>Represents a data binding between a control property and a data source property. Stub in Majorsilence.Forms.</summary>
    public class Binding
    {
        /// <summary>Initializes a new Binding stub.</summary>
        public Binding (string propertyName, object? dataSource, string? dataMember, bool formattingEnabled = false)
        {
            PropertyName = propertyName;
            DataSource = dataSource;
            BindingMemberInfo = new BindingMemberInfo (dataMember ?? string.Empty);
        }

        /// <summary>Gets the control property name.</summary>
        public string PropertyName { get; }

        /// <summary>Gets the data source.</summary>
        public object? DataSource { get; }

        /// <summary>Gets binding member information.</summary>
        public BindingMemberInfo BindingMemberInfo { get; }

        /// <summary>Gets or sets the format string. Stub in Majorsilence.Forms.</summary>
        public string FormatString { get; set; } = string.Empty;

        /// <summary>Raised when the control value is formatted. Stub in Majorsilence.Forms.</summary>
        public event ConvertEventHandler? Format { add { } remove { } }

        /// <summary>Raised when the data source value is parsed. Stub in Majorsilence.Forms.</summary>
        public event ConvertEventHandler? Parse { add { } remove { } }
    }

    /// <summary>Provides data for Binding.Format and Binding.Parse events.</summary>
    public class ConvertEventArgs : EventArgs
    {
        /// <summary>Initializes a new instance.</summary>
        public ConvertEventArgs (object? value, Type? desiredType) { Value = value; DesiredType = desiredType; }

        /// <summary>Gets or sets the value to convert.</summary>
        public object? Value { get; set; }

        /// <summary>Gets the desired target type.</summary>
        public Type? DesiredType { get; }
    }

    /// <summary>Represents a method that handles Binding.Format and Binding.Parse events.</summary>
#pragma warning disable CA1711  // WinForms compat — name must match WinForms API
    public delegate void ConvertEventHandler (object? sender, ConvertEventArgs e);

    /// <summary>Represents a method that handles mouse events.</summary>
    public delegate void MouseEventHandler (object? sender, MouseEventArgs e);

    /// <summary>Represents a method that handles key press events.</summary>
    public delegate void KeyPressEventHandler (object? sender, KeyPressEventArgs e);

    /// <summary>Represents a method that handles paint events.</summary>
    public delegate void PaintEventHandler (object? sender, PaintEventArgs e);

    /// <summary>Represents a method that handles TreeView events.</summary>
    public delegate void TreeViewEventHandler (object sender, TreeViewEventArgs e);

    /// <summary>Represents a method that handles TreeView cancel events.</summary>
    public delegate void TreeViewCancelEventHandler (object sender, TreeViewCancelEventArgs e);

    /// <summary>Represents a method that handles splitter events.</summary>
    public delegate void SplitterEventHandler (object sender, SplitterEventArgs e);

    /// <summary>Represents a method that handles splitter cancel events.</summary>
    public delegate void SplitterCancelEventHandler (object sender, SplitterCancelEventArgs e);

    /// <summary>Represents a method that handles node label edit events.</summary>
    public delegate void NodeLabelEditEventHandler (object sender, NodeLabelEditEventArgs e);

    /// <summary>Represents a method that handles draw item events.</summary>
    public delegate void DrawItemEventHandler (object sender, DrawItemEventArgs e);

    /// <summary>Represents a method that handles measure item events.</summary>
    public delegate void MeasureItemEventHandler (object sender, MeasureItemEventArgs e);

    /// <summary>Represents a method that handles TabControl events.</summary>
    public delegate void TabControlEventHandler (object sender, TabControlEventArgs e);

    /// <summary>Represents a method that handles TabControl cancel events.</summary>
    public delegate void TabControlCancelEventHandler (object sender, TabControlCancelEventArgs e);

    /// <summary>Represents a method that handles drag-and-drop events.</summary>
    public delegate void DragEventHandler (object sender, DragEventArgs e);

    /// <summary>Represents a method that handles give-feedback events.</summary>
    public delegate void GiveFeedbackEventHandler (object sender, GiveFeedbackEventArgs e);

    /// <summary>Represents a method that handles query-continue-drag events.</summary>
    public delegate void QueryContinueDragEventHandler (object sender, QueryContinueDragEventArgs e);
#pragma warning restore CA1711

    /// <summary>Provides information about a data member path.</summary>
    public class BindingMemberInfo
    {
        /// <summary>Initializes a new instance.</summary>
        public BindingMemberInfo (string dataMember) { BindingMember = dataMember; }

        /// <summary>Gets the binding member path.</summary>
        public string BindingMember { get; }

        /// <summary>Gets the field name portion of the binding member.</summary>
        public string BindingField => BindingMember.Contains ('.') ? BindingMember.Substring (BindingMember.LastIndexOf ('.') + 1) : BindingMember;

        /// <summary>Gets the path portion of the binding member.</summary>
        public string BindingPath => BindingMember.Contains ('.') ? BindingMember.Substring (0, BindingMember.LastIndexOf ('.')) : string.Empty;
    }

    /// <summary>Represents the collection of data bindings for a control. Stub in Majorsilence.Forms.</summary>
    public class ControlBindingsCollection : System.Collections.ObjectModel.Collection<Binding>
    {
        private readonly Control _control;

        /// <summary>Initializes a new instance for the given control.</summary>
        public ControlBindingsCollection (Control control) { _control = control; }

        /// <summary>Adds a new binding to the collection.</summary>
        public Binding Add (string propertyName, object? dataSource, string? dataMember, bool formattingEnabled = false)
        {
            var binding = new Binding (propertyName, dataSource, dataMember, formattingEnabled);
            Add (binding);
            return binding;
        }

        /// <summary>Gets the control that owns this collection.</summary>
        public Control Control => _control;

        /// <summary>Gets or sets the default data source update mode.</summary>
        public DataSourceUpdateMode DefaultDataSourceUpdateMode { get; set; }
    }

    /// <summary>Specifies when a data source is updated when binding.</summary>
    public enum DataSourceUpdateMode
    {
        /// <summary>Update data source when the property changes.</summary>
        OnPropertyChanged,
        /// <summary>Update data source when the control loses focus.</summary>
        OnValidation,
        /// <summary>Data source is never updated automatically.</summary>
        Never
    }

    /// <summary>Provides data for the PreviewKeyDown event.</summary>
    public class PreviewKeyDownEventArgs : EventArgs
    {
        /// <summary>Initializes a new instance.</summary>
        public PreviewKeyDownEventArgs (Keys keyData) { KeyData = keyData; }

        /// <summary>Gets the key data for this event.</summary>
        public Keys KeyData { get; }

        /// <summary>Gets the key code (without modifier keys).</summary>
        public Keys KeyCode => KeyData & Keys.KeyCode;

        /// <summary>Gets the modifier keys held at the time of the event.</summary>
        public Keys Modifiers => KeyData & Keys.Modifiers;

        /// <summary>Gets whether the Alt key was held.</summary>
        public bool Alt => (Modifiers & Keys.Alt) != 0;

        /// <summary>Gets whether the Ctrl key was held.</summary>
        public bool Control => (Modifiers & Keys.Control) != 0;

        /// <summary>Gets whether the Shift key was held.</summary>
        public bool Shift => (Modifiers & Keys.Shift) != 0;

        /// <summary>Gets or sets whether the key should be treated as an input key.</summary>
        public bool IsInputKey { get; set; }
    }

    /// <summary>Specifies the sort order for items in a PropertyGrid.</summary>
    public enum PropertySort
    {
        /// <summary>Properties are not sorted.</summary>
        NoSort = 0,
        /// <summary>Properties are sorted alphabetically.</summary>
        Alphabetical = 1,
        /// <summary>Properties are sorted by category.</summary>
        Categorized = 2,
        /// <summary>Properties are sorted by category, then alphabetically within each category.</summary>
        CategorizedAlphabetical = 3
    }

    /// <summary>
    /// Provides data for the FormClosing event.
    /// </summary>
    /// <summary>Provides data for the Form.FormClosing event.</summary>
    public class FormClosingEventArgs : System.ComponentModel.CancelEventArgs
    {
        /// <summary>Initializes a new instance.</summary>
        public FormClosingEventArgs () { }

        /// <summary>Initializes a new instance with a reason.</summary>
        public FormClosingEventArgs (CloseReason closeReason, bool cancel) : base (cancel) { CloseReason = closeReason; }

        /// <summary>Gets the reason the form is closing.</summary>
        public CloseReason CloseReason { get; } = CloseReason.UserClosing;
    }

    /// <summary>Provides data for the Form.FormClosed event.</summary>
    public class FormClosedEventArgs : EventArgs
    {
        /// <summary>Initializes a new instance.</summary>
        public FormClosedEventArgs () { }

        /// <summary>Initializes a new instance with a reason.</summary>
        public FormClosedEventArgs (CloseReason closeReason) { CloseReason = closeReason; }

        /// <summary>Gets the reason the form was closed.</summary>
        public CloseReason CloseReason { get; } = CloseReason.UserClosing;
    }

    /// <summary>Specifies the reason the form was closed.</summary>
    public enum CloseReason
    {
        /// <summary>The cause of the closure was not defined or could not be determined.</summary>
        None = 0,
        /// <summary>The Windows Task Manager is closing the application.</summary>
        TaskManagerClosing = 1,
        /// <summary>The parent form of this MDI form is closing.</summary>
        MdiFormClosing = 2,
        /// <summary>The user closed the form through the user interface.</summary>
        UserClosing = 3,
        /// <summary>The Microsoft Windows operating system is closing all applications before a system shutdown.</summary>
        WindowsShutDown = 4,
        /// <summary>The Application.Exit method was invoked.</summary>
        ApplicationExitCall = 5,
        /// <summary>The Form.Close method was called.</summary>
        FormOwnerClosing = 6
    }

    /// <summary>
    /// Delegate for the FormClosed event.
    /// </summary>
#pragma warning disable CA1711
    public delegate void FormClosedEventHandler (object sender, FormClosedEventArgs e);

    /// <summary>
    /// Delegate for the FormClosing event.
    /// </summary>
    public delegate void FormClosingEventHandler (object sender, FormClosingEventArgs e);

    /// <summary>
    /// Delegate for keyboard key events.
    /// </summary>
    public delegate void KeyEventHandler (object sender, KeyEventArgs e);
#pragma warning restore CA1711


    /// <summary>
    /// Specifies the border style of a form.
    /// </summary>
    public enum FormBorderStyle
    {
        /// <summary>No border.</summary>
        None,
        /// <summary>Fixed single-line border.</summary>
        FixedSingle,
        /// <summary>Fixed 3D border.</summary>
        Fixed3D,
        /// <summary>Fixed dialog-style border.</summary>
        FixedDialog,
        /// <summary>Sizable border (default).</summary>
        Sizable,
        /// <summary>Fixed tool window border.</summary>
        FixedToolWindow,
        /// <summary>Sizable tool window border.</summary>
        SizableToolWindow
    }

    /// <summary>
    /// Specifies the size-grip style for a form.
    /// </summary>
    public enum SizeGripStyle
    {
        /// <summary>Show the size grip when applicable.</summary>
        Auto,
        /// <summary>Always show the size grip.</summary>
        Show,
        /// <summary>Never show the size grip.</summary>
        Hide
    }

    /// <summary>Provides data for the SplitContainer.SplitterMoved event.</summary>
    public class SplitterEventArgs : EventArgs
    {
        /// <summary>Initializes a new instance.</summary>
        public SplitterEventArgs (int x, int y, int splitX, int splitY) { X = x; Y = y; SplitX = splitX; SplitY = splitY; }

        /// <summary>Gets the x-coordinate of the mouse pointer.</summary>
        public int X { get; }

        /// <summary>Gets the y-coordinate of the mouse pointer.</summary>
        public int Y { get; }

        /// <summary>Gets the x-coordinate of the upper-left corner of the splitter.</summary>
        public int SplitX { get; }

        /// <summary>Gets the y-coordinate of the upper-left corner of the splitter.</summary>
        public int SplitY { get; }
    }

    /// <summary>Provides data for the SplitContainer.SplitterMoving event.</summary>
    public class SplitterCancelEventArgs : System.ComponentModel.CancelEventArgs
    {
        /// <summary>Initializes a new instance.</summary>
        public SplitterCancelEventArgs (int mouseX, int mouseY, int splitX, int splitY)
            { MouseCursorX = mouseX; MouseCursorY = mouseY; SplitX = splitX; SplitY = splitY; }

        /// <summary>Gets the x-coordinate of the mouse pointer.</summary>
        public int MouseCursorX { get; }

        /// <summary>Gets the y-coordinate of the mouse pointer.</summary>
        public int MouseCursorY { get; }

        /// <summary>Gets or sets the x-coordinate of the upper-left corner of the splitter.</summary>
        public int SplitX { get; set; }

        /// <summary>Gets or sets the y-coordinate of the upper-left corner of the splitter.</summary>
        public int SplitY { get; set; }
    }

    /// <summary>Specifies which panel of a SplitContainer retains its size when the container is resized.</summary>
    public enum FixedPanel
    {
        /// <summary>Neither panel is fixed.</summary>
        None,
        /// <summary>Panel1 retains its size.</summary>
        Panel1,
        /// <summary>Panel2 retains its size.</summary>
        Panel2
    }

    /// <summary>Specifies the direction in which text and other elements are laid out.</summary>
    public enum RightToLeft
    {
        /// <summary>Elements are laid out from left to right.</summary>
        No,
        /// <summary>Elements are laid out from right to left.</summary>
        Yes,
        /// <summary>Inherits from the parent control.</summary>
        Inherit
    }

    /// <summary>Specifies constants representing accessible events. Stub in Majorsilence.Forms.</summary>
    public enum AccessibleEvents
    {
        /// <summary>No event.</summary>
        None = 0,
        /// <summary>Object created.</summary>
        Create = 0x8000,
        /// <summary>Object destroyed.</summary>
        Destroy = 0x8001,
        /// <summary>Object shown.</summary>
        Show = 0x8002,
        /// <summary>Object hidden.</summary>
        Hide = 0x8003,
        /// <summary>Object focus received.</summary>
        Focus = 0x8005,
        /// <summary>Object selection changed.</summary>
        Selection = 0x8006,
        /// <summary>Object value changed.</summary>
        ValueChange = 0x800E,
        /// <summary>Object name changed.</summary>
        NameChange = 0x800C,
        /// <summary>Object state changed.</summary>
        StateChange = 0x800A,
        /// <summary>Object location changed.</summary>
        LocationChange = 0x800B
    }

#pragma warning disable CA1711
    /// <summary>Delegate for the Control.HelpRequested event.</summary>
    public delegate void HelpEventHandler (object sender, HelpEventArgs e);

    /// <summary>Delegate for the Control.QueryAccessibilityHelp event.</summary>
    public delegate void QueryAccessibilityHelpEventHandler (object sender, QueryAccessibilityHelpEventArgs e);
#pragma warning restore CA1711

    /// <summary>Provides data for the Control.HelpRequested event.</summary>
    public class HelpEventArgs : EventArgs
    {
        /// <summary>Initializes a new instance.</summary>
        public HelpEventArgs (System.Drawing.Point mousePos) { MousePos = mousePos; }

        /// <summary>Gets the mouse cursor position at the time of the request.</summary>
        public System.Drawing.Point MousePos { get; }

        /// <summary>Gets or sets whether the event was handled.</summary>
        public bool Handled { get; set; }
    }

    /// <summary>Provides data for the Control.QueryAccessibilityHelp event.</summary>
    public class QueryAccessibilityHelpEventArgs : EventArgs
    {
        /// <summary>Gets or sets the help namespace.</summary>
        public string? HelpNamespace { get; set; }

        /// <summary>Gets or sets the help keyword.</summary>
        public string? HelpKeyword { get; set; }

        /// <summary>Gets or sets the help string.</summary>
        public string? HelpString { get; set; }
    }

    /// <summary>Provides data for TreeView node label-edit events.</summary>
    public class NodeLabelEditEventArgs : System.ComponentModel.CancelEventArgs
    {
        /// <summary>Initializes a new instance.</summary>
        public NodeLabelEditEventArgs (TreeViewItem node) { Node = node; }

        /// <summary>Initializes a new instance with a new label.</summary>
        public NodeLabelEditEventArgs (TreeViewItem node, string? label) { Node = node; Label = label; }

        /// <summary>Gets the node whose label is being edited.</summary>
        public TreeViewItem Node { get; }

        /// <summary>Gets the new label text, or null if the edit was cancelled.</summary>
        public string? Label { get; }
    }

    /// <summary>Provides data for TreeView node events.</summary>
    public class TreeViewEventArgs : EventArgs
    {
        /// <summary>Initializes a new instance.</summary>
        public TreeViewEventArgs (TreeViewItem node) { Node = node; }

        /// <summary>Initializes a new instance with an action.</summary>
        public TreeViewEventArgs (TreeViewItem node, TreeViewAction action) { Node = node; Action = action; }

        /// <summary>Gets the tree node that raised the event.</summary>
        public TreeViewItem Node { get; }

        /// <summary>Gets the action that raised the event.</summary>
        public TreeViewAction Action { get; }
    }

    /// <summary>Provides data for cancelable TreeView node events.</summary>
    public class TreeViewCancelEventArgs : System.ComponentModel.CancelEventArgs
    {
        /// <summary>Initializes a new instance.</summary>
        public TreeViewCancelEventArgs (TreeViewItem node, bool cancel, TreeViewAction action) : base (cancel)
        {
            Node = node;
            Action = action;
        }

        /// <summary>Gets the tree node that raised the event.</summary>
        public TreeViewItem Node { get; }

        /// <summary>Gets the action that caused the event.</summary>
        public TreeViewAction Action { get; }
    }

    /// <summary>Specifies the action that raised a TreeView event.</summary>
    public enum TreeViewAction
    {
        /// <summary>The event was caused by an unknown action.</summary>
        Unknown,
        /// <summary>The event was caused by a mouse click.</summary>
        ByMouse,
        /// <summary>The event was caused by a keyboard action.</summary>
        ByKeyboard,
        /// <summary>The event was caused by a collapse.</summary>
        Collapse,
        /// <summary>The event was caused by an expand.</summary>
        Expand
    }

    /// <summary>Specifies character casing for a TextBox.</summary>
    public enum CharacterCasing
    {
        /// <summary>No casing is applied.</summary>
        Normal,
        /// <summary>All characters are converted to uppercase.</summary>
        Upper,
        /// <summary>All characters are converted to lowercase.</summary>
        Lower
    }

    /// <summary>Specifies the auto-complete mode for a TextBox.</summary>
    public enum AutoCompleteMode
    {
        /// <summary>No auto-complete.</summary>
        None,
        /// <summary>Suggests completions.</summary>
        Suggest,
        /// <summary>Appends completions.</summary>
        Append,
        /// <summary>Suggests and appends completions.</summary>
        SuggestAppend
    }

    /// <summary>Specifies the source for auto-complete entries.</summary>
    public enum AutoCompleteSource
    {
        /// <summary>No auto-complete source.</summary>
        None,
        /// <summary>Uses the file system.</summary>
        FileSystem,
        /// <summary>Uses the history list.</summary>
        HistoryList,
        /// <summary>Uses recently used URLs.</summary>
        RecentlyUsedList,
        /// <summary>Uses all system sources.</summary>
        AllSystemSources,
        /// <summary>Uses the file system directories.</summary>
        FileSystemDirectories,
        /// <summary>Uses a custom list.</summary>
        CustomSource,
        /// <summary>Uses all URLs.</summary>
        AllUrl
    }

    /// <summary>Specifies the column header style for a ListView.</summary>
    public enum ColumnHeaderStyle
    {
        /// <summary>No column headers are displayed.</summary>
        None,
        /// <summary>Column headers do not respond to mouse clicks.</summary>
        Nonclickable,
        /// <summary>Column headers respond to mouse clicks (default).</summary>
        Clickable
    }

    /// <summary>Specifies the type of action the user must take to activate an item in a ListView.</summary>
    public enum ItemActivation
    {
        /// <summary>The user must double-click to activate an item.</summary>
        Standard,
        /// <summary>The user must single-click to activate an item.</summary>
        OneClick,
        /// <summary>The user must double-click to activate an item; item changes appearance when hovered.</summary>
        TwoClick
    }

    /// <summary>A collection of strings used for auto-complete suggestions. Stub in Majorsilence.Forms.</summary>
    public class AutoCompleteStringCollection : System.Collections.Specialized.StringCollection { }

    /// <summary>
    /// WinForms compatibility alias for <see cref="TreeViewItem"/>. In WinForms, tree nodes are called TreeNode.
    /// In Majorsilence.Forms, this class is called TreeViewItem — TreeNode is an alias for easy migration.
    /// </summary>
    public class TreeNode : TreeViewItem
    {
        /// <summary>Initializes a new instance of TreeNode.</summary>
        public TreeNode () { }

        /// <summary>Initializes a new instance of TreeNode with the specified text.</summary>
        public TreeNode (string text) : base (text) { }

        /// <summary>Initializes a new instance of TreeNode with text and child nodes.</summary>
        public TreeNode (string text, TreeNode[] children) : base (text, children) { }

        /// <summary>Initializes a new instance of TreeNode with text and image indices.</summary>
        public TreeNode (string text, int imageIndex, int selectedImageIndex) : base (text)
        {
            ImageIndex = imageIndex;
            SelectedImageIndex = selectedImageIndex;
        }

        /// <summary>Initializes a new instance of TreeNode with text, image indices, and child nodes.</summary>
        public TreeNode (string text, int imageIndex, int selectedImageIndex, TreeNode[] children) : base (text, children)
        {
            ImageIndex = imageIndex;
            SelectedImageIndex = selectedImageIndex;
        }
    }

    /// <summary>Specifies how a container control validates its children when it loses input focus.</summary>
    public enum AutoValidate
    {
        /// <summary>Implicit validation is disabled.</summary>
        Disable,
        /// <summary>Implicit validation is enabled (validates when focus leaves a child control).</summary>
        EnablePreventFocusChange,
        /// <summary>Implicit validation is enabled but allows the focus change even when validation fails.</summary>
        EnableAllowFocusChange,
        /// <summary>Inherited from the parent container.</summary>
        Inherit = -1
    }

    /// <summary>Specifies the color depth (bits per pixel) used by an ImageList.</summary>
    public enum ColorDepth
    {
        /// <summary>4-bit color (16 colors).</summary>
        Depth4Bit = 4,
        /// <summary>8-bit color (256 colors).</summary>
        Depth8Bit = 8,
        /// <summary>16-bit color.</summary>
        Depth16Bit = 16,
        /// <summary>24-bit color.</summary>
        Depth24Bit = 24,
        /// <summary>32-bit color with alpha.</summary>
        Depth32Bit = 32
    }

    /// <summary>
    /// Specifies how a form scales its contents.
    /// </summary>
    public enum AutoScaleMode
    {
        /// <summary>Scaling is disabled.</summary>
        None,
        /// <summary>Scale relative to the font.</summary>
        Font,
        /// <summary>Scale relative to the display DPI.</summary>
        Dpi,
        /// <summary>Inherited from the parent.</summary>
        Inherit
    }

    /// <summary>
    /// Specifies which buttons to display on a MessageBox.
    /// </summary>
    public enum MessageBoxButtons
    {
        /// <summary>OK button.</summary>
        OK,
        /// <summary>OK and Cancel buttons.</summary>
        OKCancel,
        /// <summary>Abort, Retry, and Ignore buttons.</summary>
        AbortRetryIgnore,
        /// <summary>Yes, No, and Cancel buttons.</summary>
        YesNoCancel,
        /// <summary>Yes and No buttons.</summary>
        YesNo,
        /// <summary>Retry and Cancel buttons.</summary>
        RetryCancel
    }

    /// <summary>
    /// Specifies the icon to display on a MessageBox.
    /// </summary>
    public enum MessageBoxIcon
    {
        /// <summary>No icon.</summary>
        None = 0,
        /// <summary>Error icon.</summary>
        Error = 16,
        /// <summary>Warning icon.</summary>
        Warning = 48,
        /// <summary>Information icon.</summary>
        Information = 64,
        /// <summary>Question icon.</summary>
        Question = 32
    }

    /// <summary>
    /// Displays a message box with a specified message, title, buttons, and icon.
    /// </summary>
    public static class MessageBox
    {
        /// <summary>Shows a message box with the specified text.</summary>
        public static DialogResult Show (string text)
            => Show (text, string.Empty, MessageBoxButtons.OK, MessageBoxIcon.None);

        /// <summary>Shows a message box with the specified text and caption.</summary>
        public static DialogResult Show (string text, string caption)
            => Show (text, caption, MessageBoxButtons.OK, MessageBoxIcon.None);

        /// <summary>Shows a message box with the specified text, caption, and buttons.</summary>
        public static DialogResult Show (string text, string caption, MessageBoxButtons buttons)
            => Show (text, caption, buttons, MessageBoxIcon.None);

        /// <summary>Shows a message box with the specified text, caption, buttons, and icon.</summary>
        public static DialogResult Show (string text, string caption, MessageBoxButtons buttons, MessageBoxIcon icon)
        {
            var parent = Application.OpenForms.FirstOrDefault ();
            var form = new MessageBoxForm (caption, text, buttons);

            if (parent != null)
                return Form.RunModal (form.ShowDialog (parent));

            form.Show ();
            return DialogResult.OK;
        }

        /// <summary>Shows a message box with the specified owner form and text.</summary>
        public static DialogResult Show (Form owner, string text)
            => Show (owner, text, string.Empty, MessageBoxButtons.OK, MessageBoxIcon.None);

        /// <summary>Shows a message box with the specified owner form, text, and caption.</summary>
        public static DialogResult Show (Form owner, string text, string caption)
            => Show (owner, text, caption, MessageBoxButtons.OK, MessageBoxIcon.None);

        /// <summary>Shows a message box with the specified owner form, text, caption, and buttons.</summary>
        public static DialogResult Show (Form owner, string text, string caption, MessageBoxButtons buttons)
            => Show (owner, text, caption, buttons, MessageBoxIcon.None);

        /// <summary>Shows a message box with the specified owner form, text, caption, buttons, and icon.</summary>
        public static DialogResult Show (Form owner, string text, string caption, MessageBoxButtons buttons, MessageBoxIcon icon)
        {
            var form = new MessageBoxForm (caption, text, buttons);
            return Form.RunModal (form.ShowDialog (owner));
        }

        /// <summary>Shows a message box with IWin32Window owner, text, caption, buttons, and icon.</summary>
        public static DialogResult Show (IWin32Window owner, string text, string caption, MessageBoxButtons buttons, MessageBoxIcon icon)
        {
            var form = owner as Form ?? Application.OpenForms.FirstOrDefault ();
            var msgForm = new MessageBoxForm (caption, text, buttons);
            return form is not null ? Form.RunModal (msgForm.ShowDialog (form)) : msgForm.ShowDialog ();
        }

        /// <summary>Shows a message box with IWin32Window owner.</summary>
        public static DialogResult Show (IWin32Window owner, string text)
            => Show (owner, text, string.Empty, MessageBoxButtons.OK, MessageBoxIcon.None);

        /// <summary>Shows a message box with IWin32Window owner and caption.</summary>
        public static DialogResult Show (IWin32Window owner, string text, string caption)
            => Show (owner, text, caption, MessageBoxButtons.OK, MessageBoxIcon.None);

        /// <summary>Shows a message box with IWin32Window owner, text, caption, and buttons.</summary>
        public static DialogResult Show (IWin32Window owner, string text, string caption, MessageBoxButtons buttons)
            => Show (owner, text, caption, buttons, MessageBoxIcon.None);

        /// <summary>Shows a message box with text, caption, buttons, icon, and default button. Default button is ignored in Majorsilence.Forms.</summary>
        public static DialogResult Show (string text, string caption, MessageBoxButtons buttons, MessageBoxIcon icon, MessageBoxDefaultButton defaultButton)
            => Show (text, caption, buttons, icon);

        /// <summary>Shows a message box with text, caption, buttons, icon, default button, and options. Extras are ignored in Majorsilence.Forms.</summary>
        public static DialogResult Show (string text, string caption, MessageBoxButtons buttons, MessageBoxIcon icon, MessageBoxDefaultButton defaultButton, MessageBoxOptions options)
            => Show (text, caption, buttons, icon);

        /// <summary>Shows a message box with owner, text, caption, buttons, icon, and default button. Default button is ignored in Majorsilence.Forms.</summary>
        public static DialogResult Show (IWin32Window owner, string text, string caption, MessageBoxButtons buttons, MessageBoxIcon icon, MessageBoxDefaultButton defaultButton)
            => Show (owner, text, caption, buttons, icon);

        /// <summary>Gets or sets the default desktop on which message boxes appear. Stub in Majorsilence.Forms.</summary>
        public static MessageBoxDefaultButton DefaultButton { get; set; } = MessageBoxDefaultButton.Button1;
    }

    /// <summary>Provides options for a MessageBox. Stub in Majorsilence.Forms — options are accepted but ignored.</summary>
    [Flags]
    public enum MessageBoxOptions
    {
        /// <summary>No options.</summary>
        None = 0,
        /// <summary>The message box is displayed on the default desktop of the interactive window station.</summary>
        DefaultDesktopOnly = 0x20000,
        /// <summary>The message box text is right-aligned.</summary>
        RightAlign = 0x80000,
        /// <summary>The message box text is displayed with right-to-left reading order.</summary>
        RtlReading = 0x100000,
        /// <summary>The message box is displayed on the currently active desktop.</summary>
        ServiceNotification = 0x200000
    }

    /// <summary>Specifies the default button for a MessageBox.</summary>
    public enum MessageBoxDefaultButton
    {
        /// <summary>The first button is the default.</summary>
        Button1,
        /// <summary>The second button is the default.</summary>
        Button2,
        /// <summary>The third button is the default.</summary>
        Button3
    }

    /// <summary>
    /// Base class for items that appear in a MenuStrip or StatusStrip.
    /// </summary>
    public class ToolStripItem : MenuItem
    {
        /// <summary>Gets or sets the name of this item.</summary>
        public string Name { get; set; } = string.Empty;

        /// <summary>Gets or sets the size of this item (informational only).</summary>
        public virtual Size Size { get; set; }

        /// <summary>Gets or sets whether this item is visible.</summary>
        public bool Visible { get; set; } = true;

        /// <summary>Gets or sets the tooltip text shown for this item.</summary>
        public string ToolTipText { get; set; } = string.Empty;

        /// <summary>Gets or sets the display style for the item.</summary>
        public ToolStripItemDisplayStyle DisplayStyle { get; set; } = ToolStripItemDisplayStyle.ImageAndText;

        /// <summary>Gets or sets the alignment of the item within the strip.</summary>
        public ToolStripItemAlignment Alignment { get; set; } = ToolStripItemAlignment.Left;

        /// <summary>Gets or sets whether the item is available (enabled).</summary>
        public new bool Enabled { get; set; } = true;

        /// <summary>Gets or sets an object tag associated with this item.</summary>
        public new object? Tag { get; set; }

        /// <summary>
        /// Gets or sets the image displayed on this item. Accepts <see cref="Majorsilence.Forms.Drawing.Image"/> for WinForms compatibility.
        /// The converted <see cref="SkiaSharp.SKBitmap"/> is synced to the base class so renderers can access it.
        /// </summary>
#pragma warning disable CA1416
        public new virtual Majorsilence.Forms.Drawing.Image? Image {
            get => _toolStripImage;
            set {
                _toolStripImage = value;
                ((MenuItem)this).SetImageSK (value?.ToSKBitmap ());
            }
        }
#pragma warning restore CA1416

        private Majorsilence.Forms.Drawing.Image? _toolStripImage;

        /// <summary>Gets or sets how the image is aligned on the item.</summary>
        public ContentAlignment ImageAlign { get; set; } = ContentAlignment.MiddleLeft;

        /// <summary>Gets or sets an integer that identifies the image from an ImageList. Stub in Majorsilence.Forms.</summary>
        public int ImageIndex { get; set; } = -1;

        /// <summary>Gets or sets the image scaling size. Stub in Majorsilence.Forms.</summary>
        public System.Drawing.Size ImageScaling { get; set; } = new System.Drawing.Size (16, 16);

        /// <summary>Gets or sets whether the item auto-sizes itself. Stub in Majorsilence.Forms.</summary>
        public bool AutoSize { get; set; } = true;

        /// <summary>Gets or sets the overflow behavior. Stub in Majorsilence.Forms.</summary>
        public ToolStripItemOverflow Overflow { get; set; } = ToolStripItemOverflow.AsNeeded;

        /// <summary>Gets or sets the text align within the item. Stub in Majorsilence.Forms.</summary>
        public virtual ContentAlignment TextAlign { get; set; } = ContentAlignment.MiddleCenter;

        /// <summary>Gets or sets the right-to-left mode. Stub in Majorsilence.Forms.</summary>
        public RightToLeft RightToLeft { get; set; } = RightToLeft.No;

        /// <summary>Gets or sets the foreground color of this item. Stub in Majorsilence.Forms.</summary>
        public System.Drawing.Color ForeColor { get; set; } = System.Drawing.Color.Empty;

        /// <summary>Gets or sets the background color of this item. Stub in Majorsilence.Forms.</summary>
        public System.Drawing.Color BackColor { get; set; } = System.Drawing.Color.Empty;

        /// <summary>Gets or sets the font for this item. Stub in Majorsilence.Forms.</summary>
        public Majorsilence.Forms.Drawing.Font? Font { get; set; }

        /// <summary>Gets or sets the image list key for this item. Stub in Majorsilence.Forms.</summary>
        public string ImageKey { get; set; } = string.Empty;

        /// <summary>Gets the owner ToolStrip. Stub in Majorsilence.Forms.</summary>
        public ToolStrip? Owner => ParentControl as ToolStrip;

        /// <summary>Gets the owner item (parent ToolStripItem). Stub in Majorsilence.Forms.</summary>
        public ToolStripItem? OwnerItem => Parent as ToolStripItem;

        /// <summary>Programmatically triggers a click on this item.</summary>
        public void PerformClick () => OnClick (new MouseEventArgs (MouseButtons.Left, 1, 0, 0, Point.Empty));

        /// <summary>Gets or sets the accessible role. Stub in Majorsilence.Forms.</summary>
        public AccessibleRole AccessibleRole { get; set; } = AccessibleRole.Default;

        /// <summary>Gets or sets whether this item is selected. Stub in Majorsilence.Forms.</summary>
        public new bool Selected => Hovered;
    }

    /// <summary>Specifies the overflow behavior of a ToolStripItem.</summary>
    public enum ToolStripItemOverflow
    {
        /// <summary>Item never overflows.</summary>
        Never,
        /// <summary>Item always overflows.</summary>
        Always,
        /// <summary>Item overflows as needed.</summary>
        AsNeeded
    }

    /// <summary>Specifies how a ToolStripItem displays its text and image.</summary>
    public enum ToolStripItemDisplayStyle
    {
        /// <summary>Neither text nor image is shown.</summary>
        None,
        /// <summary>Only the text is shown.</summary>
        Text,
        /// <summary>Only the image is shown.</summary>
        Image,
        /// <summary>Both text and image are shown.</summary>
        ImageAndText
    }

    /// <summary>
    /// Represents a clickable button on a ToolStrip / toolbar.
    /// </summary>
    public class ToolStripButton : ToolStripItem
    {
        /// <summary>Initializes a new instance of the ToolStripButton class.</summary>
        public ToolStripButton () { }

        /// <summary>Initializes a new instance of the ToolStripButton class with the specified text.</summary>
        public ToolStripButton (string text)
        {
            Text = text;
        }

        /// <summary>Initializes a new instance of the ToolStripButton class with text, image, and click handler.</summary>
        public ToolStripButton (string text, SkiaSharp.SKBitmap? image, EventHandler<MouseEventArgs>? onClick = null)
        {
            Text = text;
            ((MenuItem)this).SetImageSK (image);

            if (onClick is not null)
                Click += onClick;
        }

        /// <summary>Initializes a new instance of the ToolStripButton class with text, a Majorsilence.Forms.Drawing.Image, and click handler.</summary>
#pragma warning disable CA1416
        public ToolStripButton (string text, Majorsilence.Forms.Drawing.Image? image, EventHandler<MouseEventArgs>? onClick = null)
            : this (text, image?.ToSKBitmap (), onClick) { }
#pragma warning restore CA1416

        private bool _checked;

        /// <summary>Gets or sets whether the button is in the checked (pressed) state.</summary>
        public new bool Checked {
            get => _checked;
            set {
                if (_checked == value) return;
                _checked = value;
                CheckStateChanged?.Invoke(this, EventArgs.Empty);
            }
        }

        /// <summary>Raised when Checked changes.</summary>
        public event EventHandler? CheckStateChanged;

        /// <summary>Gets or sets whether clicking the button toggles its checked state.</summary>
        public bool CheckOnClick { get; set; }
    }

    /// <summary>
    /// Represents a non-interactive text label on a ToolStrip / toolbar.
    /// </summary>
    public class ToolStripLabel : ToolStripItem
    {
        /// <summary>Initializes a new instance of the ToolStripLabel class.</summary>
        public ToolStripLabel () { }

        /// <summary>Initializes a new instance of the ToolStripLabel class with the specified text.</summary>
        public ToolStripLabel (string text)
        {
            Text = text;
        }

        /// <summary>Gets or sets a value indicating whether the label acts as a hyperlink. Stub in Majorsilence.Forms.</summary>
        public bool IsLink { get; set; }

        /// <summary>Gets or sets the link behavior. Stub in Majorsilence.Forms.</summary>
        public LinkBehavior LinkBehavior { get; set; } = LinkBehavior.SystemDefault;

        /// <summary>Gets or sets the color used when displaying a normal link. Stub in Majorsilence.Forms.</summary>
        public System.Drawing.Color LinkColor { get; set; } = System.Drawing.Color.Blue;

        /// <summary>Gets or sets the color used when displaying a visited link. Stub in Majorsilence.Forms.</summary>
        public System.Drawing.Color VisitedLinkColor { get; set; } = System.Drawing.Color.Purple;

        /// <summary>Gets or sets a value indicating whether the link has been visited. Stub in Majorsilence.Forms.</summary>
        public bool LinkVisited { get; set; }

        /// <summary>Raised when the pointer enters the label. Stub in Majorsilence.Forms (never fires).</summary>
        public event EventHandler? MouseEnter { add { } remove { } }

        /// <summary>Raised when the pointer leaves the label. Stub in Majorsilence.Forms (never fires).</summary>
        public event EventHandler? MouseLeave { add { } remove { } }
    }

    /// <summary>
    /// Represents an editable text box embedded on a ToolStrip / toolbar.
    /// </summary>
    public class ToolStripTextBox : ToolStripItem
    {
        private string text = string.Empty;

        /// <summary>Initializes a new instance of the ToolStripTextBox class.</summary>
        public ToolStripTextBox () { }

        /// <summary>Initializes a new instance of the ToolStripTextBox class with the specified name.</summary>
        public ToolStripTextBox (string name)
        {
            Name = name;
        }

        /// <summary>Gets or sets the text contained in the text box.</summary>
        public new string Text {
            get => text;
            set {
                if (text != value) {
                    text = value ?? string.Empty;
                    TextChanged?.Invoke (this, EventArgs.Empty);
                }
            }
        }

        /// <summary>Raised when the text changes.</summary>
        public event EventHandler? TextChanged;

        /// <summary>Gets a reference to the underlying TextBox. Stub returns a detached TextBox in Majorsilence.Forms.</summary>
        public TextBox TextBox { get; } = new TextBox ();

        /// <summary>Gets or sets a value indicating whether pressing Enter in the text box creates a new line. Stub in Majorsilence.Forms.</summary>
        public bool AcceptsReturn { get; set; }

        /// <summary>Gets or sets a value indicating whether pressing Tab moves focus or inserts a tab character. Stub in Majorsilence.Forms.</summary>
        public bool AcceptsTab { get; set; }

        /// <summary>Gets or sets the maximum number of characters that can be entered. Stub in Majorsilence.Forms.</summary>
        public int MaxLength { get; set; } = 32767;

        /// <summary>Gets or sets a value indicating whether the text in the text box is read-only. Stub in Majorsilence.Forms.</summary>
        public bool ReadOnly { get; set; }

        /// <summary>Gets whether the text box currently has input focus. Delegates to the underlying TextBox.</summary>
        public bool Focused => TextBox.Focused;

        /// <summary>Raised when the control loses focus and validation completes. Delegates to the underlying TextBox.</summary>
        public event EventHandler? Validated { add => TextBox.Validated += value; remove => TextBox.Validated -= value; }

        /// <summary>Raised when a key is pressed. Delegates to the underlying TextBox.</summary>
        public event EventHandler<KeyEventArgs>? KeyDown { add => TextBox.KeyDown += value; remove => TextBox.KeyDown -= value; }

        /// <summary>Copies the current selection to the clipboard. Delegates to the underlying TextBox.</summary>
        public void Cut() => TextBox.Cut();

        /// <summary>Copies the current selection to the clipboard. Delegates to the underlying TextBox.</summary>
        public void Copy() => TextBox.Copy();

        /// <summary>Pastes the clipboard contents. Delegates to the underlying TextBox.</summary>
        public void Paste() => TextBox.Paste();

        /// <summary>Undoes the last edit. Delegates to the underlying TextBox.</summary>
        public void Undo() => TextBox.Undo();

        /// <summary>Selects all text. Delegates to the underlying TextBox.</summary>
        public void SelectAll() => TextBox.SelectAll();
    }

    /// <summary>Hosts an arbitrary Control inside a ToolStrip. Stub in Majorsilence.Forms.</summary>
    public class ToolStripControlHost : ToolStripItem
    {
        /// <summary>Initializes a new instance hosting the specified control.</summary>
        public ToolStripControlHost (Control control) { Control = control; }

        /// <summary>Initializes a new instance hosting the specified control with the given name.</summary>
        public ToolStripControlHost (Control control, string name) { Control = control; Name = name; }

        /// <summary>Gets the hosted control.</summary>
        public Control Control { get; }

        /// <summary>Gets or sets the size of the hosted control.</summary>
        public new System.Drawing.Size Size {
            get => Control.Size;
            set => Control.Size = value;
        }

        /// <summary>Raised when the hosted control's content changes. Stub in Majorsilence.Forms.</summary>
        public event EventHandler? ContentChanged { add { } remove { } }

        /// <summary>Raised when the hosted control receives focus. Stub in Majorsilence.Forms.</summary>
        public event EventHandler? GotFocus { add { } remove { } }

        /// <summary>Raised when the hosted control loses focus. Stub in Majorsilence.Forms.</summary>
        public event EventHandler? LostFocus { add { } remove { } }

        /// <summary>Raised when a key is pressed while focus is on the hosted control. Stub in Majorsilence.Forms.</summary>
        public event EventHandler<KeyEventArgs>? KeyDown { add { } remove { } }

        /// <summary>Raised when a key is released while focus is on the hosted control. Stub in Majorsilence.Forms.</summary>
        public event EventHandler<KeyEventArgs>? KeyUp { add { } remove { } }
    }

    /// <summary>Represents a ToolStrip-hosted drop-down control. Stub in Majorsilence.Forms.</summary>
    public class ToolStripDropDown : ContextMenu
    {
        /// <summary>Gets the items in this drop-down.</summary>
        public new MenuItemCollection Items => base.Items;

        /// <summary>Shows the drop-down at the specified screen location.</summary>
        public void Show (System.Drawing.Point screenLocation) { }

        /// <summary>Shows the drop-down at the specified location relative to a control.</summary>
        public new void Show (Control control, System.Drawing.Point location) => base.Show (control, location);
    }

    /// <summary>
    /// Represents a button on a ToolStrip that displays a drop-down menu when clicked.
    /// </summary>
    public class ToolStripDropDownButton : ToolStripItem
    {
        /// <summary>Initializes a new instance of the ToolStripDropDownButton class.</summary>
        public ToolStripDropDownButton () { }

        /// <summary>Initializes a new instance of the ToolStripDropDownButton class with the specified text.</summary>
        public ToolStripDropDownButton (string text)
        {
            Text = text;
        }

        /// <summary>Gets the collection of items shown in the drop-down.</summary>
        public MenuItemCollection DropDownItems => Items;

        /// <summary>Programmatically opens the drop-down. Stub in Majorsilence.Forms.</summary>
        public new void ShowDropDown () { }

        /// <summary>Raised before the drop-down is opened. Stub in Majorsilence.Forms.</summary>
        public event EventHandler? DropDownOpening { add { } remove { } }

        /// <summary>Raised after the drop-down is opened. Stub in Majorsilence.Forms.</summary>
        public event EventHandler? DropDownOpened { add { } remove { } }

        /// <summary>Raised after the drop-down is closed. Stub in Majorsilence.Forms.</summary>
        public event EventHandler? DropDownClosed { add { } remove { } }
    }

    /// <summary>Represents a button with both a clickable portion and a drop-down arrow. Stub in Majorsilence.Forms.</summary>
    public class ToolStripSplitButton : ToolStripDropDownButton
    {
        /// <summary>Initializes a new instance of the ToolStripSplitButton class.</summary>
        public ToolStripSplitButton () { }

        /// <summary>Initializes a new instance with the specified text.</summary>
        public ToolStripSplitButton (string text) { Text = text; }

        /// <summary>Initializes a new instance with the specified text and image.</summary>
        public ToolStripSplitButton (string text, Majorsilence.Forms.Drawing.Image? image) { Text = text; Image = image; }

        /// <summary>Gets or sets the default item clicked when the button portion is clicked. Stub in Majorsilence.Forms.</summary>
        public new ToolStripItem? DefaultItem { get; set; }

        /// <summary>Gets the width of the drop-down button portion. Stub in Majorsilence.Forms.</summary>
        public int DropDownButtonWidth { get; set; } = 11;

        /// <summary>Raised when the button portion of the item is clicked. Stub in Majorsilence.Forms.</summary>
        public event EventHandler? ButtonClick { add { } remove { } }

        /// <summary>Raised when the button portion of the item is double-clicked. Stub in Majorsilence.Forms.</summary>
        public event EventHandler? ButtonDoubleClick { add { } remove { } }
    }

    /// <summary>
    /// Represents a menu item in a MenuStrip.
    /// </summary>
    public class ToolStripMenuItem : ToolStripItem
    {
        /// <summary>Initializes a new instance of the ToolStripMenuItem class.</summary>
        public ToolStripMenuItem () { }

        /// <summary>Initializes a new instance with the specified text.</summary>
        public ToolStripMenuItem (string text) { Text = text; }

        /// <summary>Initializes a new instance with text, image (SKBitmap), and click handler.</summary>
        public ToolStripMenuItem (string text, SkiaSharp.SKBitmap? image, EventHandler<MouseEventArgs>? onClick = null)
        {
            Text = text;
            ((MenuItem)this).SetImageSK (image);

            if (onClick is not null)
                Click += onClick;
        }

        /// <summary>Initializes a new instance with text, a Majorsilence.Forms.Drawing.Image, and click handler.</summary>
#pragma warning disable CA1416
        public ToolStripMenuItem (string text, Majorsilence.Forms.Drawing.Image? image, EventHandler<MouseEventArgs>? onClick = null)
        {
            Text = text;
            Image = image;

            if (onClick is not null)
                Click += onClick;
        }

        /// <summary>Initializes a new instance with text, a Majorsilence.Forms.Drawing.Image, click handler, and subitems.</summary>
        public ToolStripMenuItem (string text, Majorsilence.Forms.Drawing.Image? image, EventHandler<MouseEventArgs>? onClick, params ToolStripMenuItem[] dropDownItems)
            : this (text, image?.ToSKBitmap (), onClick)
        {
            foreach (var item in dropDownItems)
                Items.Add (item);
        }

        /// <summary>Initializes a new instance with text, a Majorsilence.Forms.Drawing.Image, and subitems.</summary>
        public ToolStripMenuItem (string text, Majorsilence.Forms.Drawing.Image? image, params ToolStripMenuItem[] dropDownItems)
            : this (text, image?.ToSKBitmap ())
        {
            foreach (var item in dropDownItems)
                Items.Add (item);
        }
#pragma warning restore CA1416

        /// <summary>Gets the collection of sub-items for this menu item.</summary>
        public MenuItemCollection DropDownItems => Items;

        /// <summary>Gets or sets whether the item appears with a check mark.</summary>
        public new bool Checked { get; set; }

        /// <summary>Gets or sets whether clicking the item toggles its checked state.</summary>
        public bool CheckOnClick { get; set; }

        /// <summary>Gets or sets the shortcut key combination for this item. Stub in Majorsilence.Forms.</summary>
        public Keys ShortcutKeys { get; set; } = Keys.None;

        /// <summary>Gets or sets the string to display for the shortcut key. Stub in Majorsilence.Forms.</summary>
        public string ShortcutKeyDisplayString { get; set; } = string.Empty;

        /// <summary>Gets or sets whether the shortcut key is shown in the menu item. Stub in Majorsilence.Forms.</summary>
        public bool ShowShortcutKeys { get; set; } = true;

        /// <summary>Gets whether this item has any drop-down items.</summary>
        public bool HasDropDownItems => Items.Count > 0;

        /// <summary>Gets or sets the check state of the item. Stub in Majorsilence.Forms.</summary>
        public CheckState CheckState {
            get => Checked ? CheckState.Checked : CheckState.Unchecked;
            set => Checked = value == CheckState.Checked;
        }

        /// <summary>Raised when the drop-down is about to open. Stub in Majorsilence.Forms.</summary>
        public event EventHandler? DropDownOpening { add { } remove { } }

        /// <summary>Raised when the drop-down has been opened. Stub in Majorsilence.Forms.</summary>
        public event EventHandler? DropDownOpened { add { } remove { } }

        /// <summary>Raised when the drop-down has been closed. Stub in Majorsilence.Forms.</summary>
        public event EventHandler? DropDownClosed { add { } remove { } }

        /// <summary>Raised when the Checked property changes. Stub in Majorsilence.Forms.</summary>
        public event EventHandler? CheckedChanged { add { } remove { } }

        /// <summary>Gets or sets how this item behaves when toolstrips are merged. Stub in Majorsilence.Forms.</summary>
        public MergeAction MergeAction { get; set; } = MergeAction.Append;

        /// <summary>Gets or sets the merge index for this item. Stub in Majorsilence.Forms.</summary>
        public int MergeIndex { get; set; } = -1;
    }

    /// <summary>Specifies how a ToolStripMenuItem behaves when toolstrips are merged.</summary>
    public enum MergeAction
    {
        /// <summary>Append to the end of the collection.</summary>
        Append = 0,
        /// <summary>Insert at the position specified by MergeIndex.</summary>
        Insert = 1,
        /// <summary>Replace the item at MergeIndex.</summary>
        Replace = 2,
        /// <summary>Remove the item at MergeIndex.</summary>
        Remove = 3,
        /// <summary>Match the item and merge its children.</summary>
        MatchOnly = 4
    }

    /// <summary>
    /// Represents a separator in a MenuStrip or ContextMenuStrip.
    /// </summary>
    public class ToolStripSeparator : ToolStripItem
    {
        /// <summary>Initializes a new instance of the ToolStripSeparator class.</summary>
        public ToolStripSeparator ()
        {
            Padding = new Padding (3);
        }
    }

    /// <summary>
    /// Represents a combo box embedded in a MenuStrip.
    /// </summary>
    public class ToolStripComboBox : ToolStripItem, IDisposable
    {
        private readonly CompatComboBox combo_box = new CompatComboBox ();
        private bool _disposed;

        /// <inheritdoc/>
        public void Dispose ()
        {
            if (_disposed) return;
            _disposed = true;
            combo_box.Dispose ();
            GC.SuppressFinalize (this);
        }

        /// <summary>Gets the underlying ComboBox control.</summary>
        public CompatComboBox ComboBox => combo_box;

        /// <summary>Gets or sets the selected index.</summary>
        public int SelectedIndex {
            get => combo_box.SelectedIndex;
            set => combo_box.SelectedIndex = value;
        }

        /// <summary>Gets or sets the drop-down style of the underlying combo box.</summary>
        public ComboBoxStyle DropDownStyle {
            get => combo_box.DropDownStyle;
            set => combo_box.DropDownStyle = value;
        }

        /// <summary>Gets the items collection of the underlying combo box.</summary>
        public new ListBoxItemCollection Items => combo_box.Items;

        /// <summary>Raised when the selected index changes.</summary>
        public event EventHandler? SelectedIndexChanged {
            add => combo_box.SelectedIndexChanged += value;
            remove => combo_box.SelectedIndexChanged -= value;
        }

        /// <summary>Raised when the control loses focus and validation completes. Delegates to the underlying ComboBox.</summary>
        public event EventHandler? Validated { add => combo_box.Validated += value; remove => combo_box.Validated -= value; }

        /// <summary>Gets or sets the width of the underlying combo box.</summary>
        public int Width { get => combo_box.Width; set => combo_box.Width = value; }
    }

    /// <summary>
    /// A ComboBox with basic data-binding support for WinForms compatibility.
    /// </summary>
    public class CompatComboBox : ComboBox
    {
        private object? data_source;
        private string display_member = string.Empty;
        private string value_member = string.Empty;

        /// <summary>Gets or sets the data source for this combo box.</summary>
        public new object? DataSource {
            get => data_source;
            set {
                data_source = value;
                RefreshItems ();
            }
        }

        /// <summary>Gets or sets the property name to use for display.</summary>
        public new string DisplayMember {
            get => display_member;
            set {
                display_member = value ?? string.Empty;
                RefreshItems ();
            }
        }

        /// <summary>Gets or sets the property name to use as the value.</summary>
        public new string ValueMember {
            get => value_member;
            set {
                value_member = value ?? string.Empty;
                RefreshItems ();
            }
        }

        [UnconditionalSuppressMessage ("Trimming", "IL2075", Justification = "DataSource item types are not trim-safe by design — same as WinForms.")]
        private static object? GetItemPropertyValue (object? item, string propertyName)
            => item?.GetType ().GetProperty (propertyName)?.GetValue (item);

        /// <summary>Gets or sets the selected value using ValueMember.</summary>
        public new object? SelectedValue {
            get {
                if (SelectedIndex < 0 || data_source == null)
                    return null;

                var list = data_source as System.Collections.IList;

                if (list == null || SelectedIndex >= list.Count)
                    return null;

                var item = list[SelectedIndex];

                if (string.IsNullOrEmpty (value_member))
                    return item;

                return GetItemPropertyValue (item, value_member);
            }
            set {
                if (data_source == null || value == null)
                    return;

                var list = data_source as System.Collections.IList;

                if (list == null)
                    return;

                for (int i = 0; i < list.Count; i++) {
                    var item = list[i];
                    var item_value = string.IsNullOrEmpty (value_member)
                        ? item
                        : GetItemPropertyValue (item, value_member);

                    if (Equals (item_value, value)) {
                        SelectedIndex = i;
                        return;
                    }
                }
            }
        }

        [UnconditionalSuppressMessage ("Trimming", "IL2075", Justification = "DataSource item types are not trim-safe by design — same as WinForms.")]
        private void RefreshItems ()
        {
            Items.Clear ();

            var list = data_source as System.Collections.IList;

            if (list == null)
                return;

            foreach (var item in list) {
                string display;

                if (!string.IsNullOrEmpty (display_member))
                    display = item?.GetType ().GetProperty (display_member)?.GetValue (item)?.ToString () ?? string.Empty;
                else
                    display = item?.ToString () ?? string.Empty;

                Items.Add (display);
            }
        }
    }

    /// <summary>
    /// A collection of ToolStripItem objects.
    /// </summary>
    public class ToolStripItemCollection : Collection<ToolStripItem>
    {
        /// <summary>Adds a range of items to the collection.</summary>
        public void AddRange (IEnumerable<ToolStripItem> items)
        {
            foreach (var item in items)
                Add (item);
        }
    }

    /// <summary>
    /// Represents a text label in a StatusStrip.
    /// </summary>
    public class ToolStripStatusLabel : ToolStripItem
    {
        /// <summary>Gets or sets how the label is aligned within the strip.</summary>
        public new ToolStripItemAlignment Alignment { get; set; }

        /// <summary>Gets or sets whether the item stretches to fill remaining strip space.</summary>
        public bool Spring { get; set; }

        /// <summary>Gets or sets the border style of the label. Stub in Majorsilence.Forms.</summary>
        public Border3DStyle BorderStyle { get; set; } = Border3DStyle.None;

        /// <summary>Gets or sets which sides of the label show a border. Stub in Majorsilence.Forms.</summary>
        public ToolStripStatusLabelBorderSides BorderSides { get; set; } = ToolStripStatusLabelBorderSides.None;
    }

    /// <summary>Specifies which sides of a ToolStripStatusLabel display a border.</summary>
    [System.Flags]
    public enum ToolStripStatusLabelBorderSides
    {
        /// <summary>No border.</summary>
        None = 0,
        /// <summary>Left border.</summary>
        Left = 1,
        /// <summary>Right border.</summary>
        Right = 2,
        /// <summary>Top border.</summary>
        Top = 4,
        /// <summary>Bottom border.</summary>
        Bottom = 8,
        /// <summary>All sides.</summary>
        All = Left | Right | Top | Bottom
    }

    /// <summary>
    /// Represents a progress bar embedded in a StatusStrip.
    /// </summary>
    public class ToolStripProgressBar : ToolStripItem
    {
        /// <summary>Gets or sets the current value.</summary>
        public int Value { get; set; }

        /// <summary>Gets or sets the maximum value.</summary>
        public int Maximum { get; set; } = 100;

        /// <summary>Gets or sets the minimum value.</summary>
        public int Minimum { get; set; }

        /// <summary>Gets or sets the display style (Blocks, Continuous, or Marquee).</summary>
        public ProgressBarStyle Style { get; set; } = ProgressBarStyle.Blocks;
    }

    /// <summary>
    /// Represents a main menu bar for a Form (legacy WinForms component). Alias for Menu.
    /// </summary>
    public class MainMenu : Menu
    {
        /// <summary>Initializes a new instance of the MainMenu class.</summary>
        public MainMenu () { }

        /// <summary>Initializes a new instance of the MainMenu class and adds it to the specified container.</summary>
        public MainMenu (System.ComponentModel.IContainer container)
        {
            container.Add (this);
        }

        /// <summary>Clones the MainMenu. Returns a new MainMenu with no items.</summary>
        public MainMenu CloneMenu () => new MainMenu ();
    }

    /// <summary>
    /// Represents a menu bar docked at the top of a form. Alias for Menu.
    /// </summary>
    public class MenuStrip : Menu
    {
        /// <summary>Gets or sets the ToolStripMenuItem for the MDI window list. Stub in Majorsilence.Forms.</summary>
        public ToolStripMenuItem? MdiWindowListItem { get; set; }

        /// <summary>Gets or sets the size used to scale item images. Stub in Majorsilence.Forms.</summary>
        public System.Drawing.Size ImageScalingSize { get; set; } = new System.Drawing.Size(16, 16);
    }

    /// <summary>
    /// Represents a shortcut (context) menu. WinForms compatibility alias for <see cref="ContextMenu"/>.
    /// </summary>
    public class ContextMenuStrip : ContextMenu
    {
        /// <summary>Gets or sets the size used to scale item images. Stub in Majorsilence.Forms.</summary>
        public System.Drawing.Size ImageScalingSize { get; set; } = new System.Drawing.Size(16, 16);
    }

    /// <summary>
    /// Represents a toolbar strip that can contain ToolStripButton, ToolStripLabel, and other ToolStripItem types.
    /// Maps to Majorsilence.Forms' ToolBar (MenuBase) with a ToolStripItemCollection facade.
    /// </summary>
    public class ToolStrip : ToolBar
    {
        private readonly ToolStripItemCollection _items;

        /// <summary>Initializes a new instance of the ToolStrip class.</summary>
        public ToolStrip ()
        {
            _items = new ToolStripItemCollection ();
        }

        /// <summary>Gets the collection of items in this ToolStrip.</summary>
        public new ToolStripItemCollection Items => _items;

        /// <summary>Gets or sets whether the ToolStrip can be moved. Stub in Majorsilence.Forms.</summary>
        public bool GripVisible { get; set; } = true;

        /// <summary>Gets or sets the grip style for the ToolStrip. Stub in Majorsilence.Forms.</summary>
        public ToolStripGripStyle GripStyle { get; set; } = ToolStripGripStyle.Visible;

        /// <summary>Gets or sets the text direction for the ToolStrip. Stub in Majorsilence.Forms.</summary>
        public ToolStripTextDirection TextDirection { get; set; } = ToolStripTextDirection.Horizontal;

        /// <summary>Gets or sets the image scaling size for the ToolStrip. Stub in Majorsilence.Forms.</summary>
        public System.Drawing.Size ImageScalingSize { get; set; } = new System.Drawing.Size (16, 16);

        /// <summary>Gets or sets whether the ToolStrip renders in a specific style. Stub in Majorsilence.Forms.</summary>
        public bool ShowItemToolTips { get; set; } = true;

        /// <summary>Gets or sets the rendering mode for the ToolStrip. Stub in Majorsilence.Forms.</summary>
        public ToolStripRenderMode RenderMode { get; set; } = ToolStripRenderMode.ManagerRenderMode;

        /// <summary>Gets or sets the renderer for the ToolStrip. Stub in Majorsilence.Forms.</summary>
        public ToolStripRenderer? Renderer { get; set; }

        /// <summary>Gets or sets whether items can overflow to a dropdown. Stub in Majorsilence.Forms.</summary>
        public bool CanOverflow { get; set; } = true;

        /// <summary>Gets or sets the layout style. Stub in Majorsilence.Forms.</summary>
        public ToolStripLayoutStyle LayoutStyle { get; set; } = ToolStripLayoutStyle.HorizontalStackWithOverflow;

        /// <summary>Gets or sets the ImageList associated with this ToolStrip. Stub in Majorsilence.Forms.</summary>
        public new ImageList? ImageList { get; set; }

        /// <summary>Gets or sets whether the ToolStrip stretches to fill its container. Stub in Majorsilence.Forms.</summary>
        public bool Stretch { get; set; }
    }

    /// <summary>Represents a panel that can host ToolStrip controls. Stub in Majorsilence.Forms.</summary>
    public class ToolStripPanel : Panel
    {
        /// <summary>Gets or sets whether the panel is locked. Stub in Majorsilence.Forms.</summary>
        public bool Locked { get; set; }

        /// <summary>Gets or sets the orientation of the ToolStripPanel. Stub in Majorsilence.Forms.</summary>
        public Orientation Orientation { get; set; } = Orientation.Horizontal;
    }

    /// <summary>Represents the overflow button on a ToolStrip. Stub in Majorsilence.Forms.</summary>
    public class ToolStripOverflowButton : ToolStripDropDownButton
    {
        /// <summary>Initializes a new instance of ToolStripOverflowButton.</summary>
        public ToolStripOverflowButton () { }
    }

    /// <summary>
    /// Stub container for dockable toolbars. In Majorsilence.Forms, toolbars are not dockable;
    /// ToolStripContainer is provided for compilation compatibility only.
    /// </summary>
    public class ToolStripContainer : Panel
    {
        /// <summary>Gets the top ToolStripPanel.</summary>
        public Panel TopToolStripPanel { get; } = new Panel { Dock = DockStyle.Top };

        /// <summary>Gets the bottom ToolStripPanel.</summary>
        public Panel BottomToolStripPanel { get; } = new Panel { Dock = DockStyle.Bottom };

        /// <summary>Gets the left ToolStripPanel.</summary>
        public Panel LeftToolStripPanel { get; } = new Panel { Dock = DockStyle.Left };

        /// <summary>Gets the right ToolStripPanel.</summary>
        public Panel RightToolStripPanel { get; } = new Panel { Dock = DockStyle.Right };

        /// <summary>Gets the content panel in the center.</summary>
        public Panel ContentPanel { get; } = new Panel { Dock = DockStyle.Fill };

        /// <summary>Gets or sets whether the top panel is visible.</summary>
        public bool TopToolStripPanelVisible { get; set; } = true;

        /// <summary>Gets or sets whether the bottom panel is visible.</summary>
        public bool BottomToolStripPanelVisible { get; set; } = true;

        /// <summary>Gets or sets whether the left panel is visible.</summary>
        public bool LeftToolStripPanelVisible { get; set; } = true;

        /// <summary>Gets or sets whether the right panel is visible.</summary>
        public bool RightToolStripPanelVisible { get; set; } = true;
    }

    /// <summary>Provides navigation UI for a BindingSource. Stub in Majorsilence.Forms.</summary>
    public class BindingNavigator : ToolStrip
    {
        /// <summary>Initializes a new BindingNavigator.</summary>
        public BindingNavigator () { }

        /// <summary>Initializes a new BindingNavigator, optionally adding it to a container.</summary>
        public BindingNavigator (System.ComponentModel.IContainer container) { container.Add (this); }

        /// <summary>Gets or sets the BindingSource this navigator is bound to. Stub in Majorsilence.Forms.</summary>
        public BindingSource? BindingSource { get; set; }

        /// <summary>Stub move-first button.</summary>
        public ToolStripButton? MoveFirstItem { get; set; }

        /// <summary>Stub move-previous button.</summary>
        public ToolStripButton? MovePreviousItem { get; set; }

        /// <summary>Stub move-next button.</summary>
        public ToolStripButton? MoveNextItem { get; set; }

        /// <summary>Stub move-last button.</summary>
        public ToolStripButton? MoveLastItem { get; set; }

        /// <summary>Stub add-new button.</summary>
        public ToolStripButton? AddNewItem { get; set; }

        /// <summary>Stub delete button.</summary>
        public ToolStripButton? DeleteItem { get; set; }

        /// <summary>Stub position text box.</summary>
        public ToolStripTextBox? PositionItem { get; set; }

        /// <summary>Stub count label.</summary>
        public ToolStripLabel? CountItem { get; set; }
    }

    /// <summary>Represents a control that allows the user to select a string from a collection by scrolling. Stub in Majorsilence.Forms.</summary>
    public class DomainUpDown : NumericUpDown
    {
        private int _selectedIndex = -1;

        /// <summary>Gets or sets the collection of items displayed in the control.</summary>
        public System.Collections.Specialized.StringCollection Items { get; } = new System.Collections.Specialized.StringCollection ();

        /// <summary>Gets or sets the index of the currently selected item.</summary>
        public int SelectedIndex {
            get => _selectedIndex;
            set {
                _selectedIndex = value;
                Text = (value >= 0 && value < Items.Count) ? Items[value] ?? string.Empty : string.Empty;
            }
        }

        /// <summary>Gets or sets the selected item.</summary>
        public object? SelectedItem {
            get => _selectedIndex >= 0 && _selectedIndex < Items.Count ? Items[_selectedIndex] : null;
            set {
                if (value is string s) {
                    for (int i = 0; i < Items.Count; i++) {
                        if (Items[i] == s) { SelectedIndex = i; return; }
                    }
                }
            }
        }

        /// <summary>Raised when the selected item changes.</summary>
#pragma warning disable CS0067 // Event is part of the WinForms-compat surface; not yet raised (stub).
        public event EventHandler? SelectedItemChanged;
#pragma warning restore CS0067
    }

    /// <summary>Specifies the rendering mode for a ToolStrip.</summary>
    public enum ToolStripRenderMode
    {
        /// <summary>Use the ToolStripManager renderer.</summary>
        ManagerRenderMode,
        /// <summary>Use a custom renderer.</summary>
        Custom,
        /// <summary>Use the system renderer.</summary>
        System,
        /// <summary>Use the professional renderer.</summary>
        Professional
    }

    /// <summary>Specifies the grip style of a ToolStrip.</summary>
    public enum ToolStripGripStyle
    {
        /// <summary>The grip is hidden.</summary>
        Hidden,
        /// <summary>The grip is visible.</summary>
        Visible
    }

    /// <summary>Specifies the text direction in a ToolStrip.</summary>
    public enum ToolStripTextDirection
    {
        /// <summary>Text is horizontal (default).</summary>
        Horizontal,
        /// <summary>Text is inherited from parent.</summary>
        Inherit,
        /// <summary>Text is vertical going up (90° rotated).</summary>
        Vertical90,
        /// <summary>Text is vertical going down (270° rotated).</summary>
        Vertical270
    }


    /// <summary>Specifies the layout style for a ToolStrip.</summary>
    public enum ToolStripLayoutStyle
    {
        /// <summary>Stack horizontally with overflow.</summary>
        HorizontalStackWithOverflow,
        /// <summary>Stack vertically with overflow.</summary>
        VerticalStackWithOverflow,
        /// <summary>Stack horizontally without overflow.</summary>
        StackWithOverflow,
        /// <summary>Flow layout.</summary>
        Flow,
        /// <summary>Table layout.</summary>
        Table
    }

    /// <summary>Provides a base class for rendering ToolStrip controls. Stub in Majorsilence.Forms.</summary>
    public abstract class ToolStripRenderer
    {
    }

    /// <summary>Provides a professional-style renderer for ToolStrip. Stub in Majorsilence.Forms.</summary>
    public class ToolStripProfessionalRenderer : ToolStripRenderer
    {
        /// <summary>Initializes a new instance.</summary>
        public ToolStripProfessionalRenderer () { }

        /// <summary>Gets or sets whether rounded borders are used. Stub in Majorsilence.Forms.</summary>
        public bool RoundedEdges { get; set; } = true;
    }

    /// <summary>Provides a system-style renderer for ToolStrip. Stub in Majorsilence.Forms.</summary>
    public class ToolStripSystemRenderer : ToolStripRenderer
    {
        /// <summary>Initializes a new instance.</summary>
        public ToolStripSystemRenderer () { }
    }

    /// <summary>Provides global settings for all ToolStrip controls. Stub in Majorsilence.Forms.</summary>
    public static class ToolStripManager
    {
        /// <summary>Gets or sets the global render mode for ToolStrip controls. Stub in Majorsilence.Forms.</summary>
        public static ToolStripRenderMode RenderMode { get; set; } = ToolStripRenderMode.Professional;

        /// <summary>Gets or sets the global renderer for ToolStrip controls. Stub in Majorsilence.Forms.</summary>
        public static ToolStripRenderer? Renderer { get; set; }

        /// <summary>Merges the source toolstrip into the target toolstrip. Stub in Majorsilence.Forms.</summary>
        public static bool Merge (ToolStrip sourceToolStrip, ToolStrip targetToolStrip) => false;

        /// <summary>Merges the source toolstrip into the named target. Stub in Majorsilence.Forms.</summary>
        public static bool Merge (ToolStrip sourceToolStrip, string targetName) => false;

        /// <summary>Reverts a previous merge on the target toolstrip. Stub in Majorsilence.Forms.</summary>
        public static bool RevertMerge (ToolStrip targetToolStrip) => false;

        /// <summary>Reverts a previous merge on the named target. Stub in Majorsilence.Forms.</summary>
        public static bool RevertMerge (ToolStrip targetToolStrip, ToolStrip sourceToolStrip) => false;
    }

    /// <summary>
    /// Represents a status bar with items docked at the bottom of a form.
    /// </summary>
    public class StatusStrip : Control
    {
        /// <summary>Initializes a new instance of the StatusStrip class.</summary>
        public StatusStrip ()
        {
            Dock = DockStyle.Bottom;
            Items = new ToolStripItemCollection ();
            SetControlBehavior (ControlBehaviors.InvalidateOnTextChanged);
        }

        /// <summary>Gets the collection of items displayed in this StatusStrip.</summary>
        public ToolStripItemCollection Items { get; }

        /// <summary>Gets or sets the size used to scale item images. Stub in Majorsilence.Forms.</summary>
        public System.Drawing.Size ImageScalingSize { get; set; } = new System.Drawing.Size(16, 16);

        /// <inheritdoc/>
        protected override Padding DefaultPadding => new Padding (1, 0, 16, 0);

        /// <inheritdoc/>
        protected override Size DefaultSize => new Size (600, 22);

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
            Renderers.RenderManager.Render (this, e);
        }

        /// <inheritdoc/>
        public override ControlStyle Style { get; } = new ControlStyle (DefaultStyle);
    }

    /// <summary>Provides data for the ListView.ItemChecked event.</summary>
    public class ItemCheckedEventArgs : EventArgs
    {
        /// <summary>Initializes a new instance.</summary>
        public ItemCheckedEventArgs (ListViewItem item) { Item = item; }

        /// <summary>Gets the ListViewItem that was checked or unchecked.</summary>
        public ListViewItem Item { get; }
    }

    /// <summary>Provides data for the ListView.BeforeLabelEdit and AfterLabelEdit events.</summary>
    public class LabelEditEventArgs : System.ComponentModel.CancelEventArgs
    {
        /// <summary>Initializes a new instance.</summary>
        public LabelEditEventArgs (int item) { Item = item; }

        /// <summary>Initializes a new instance with new label text.</summary>
        public LabelEditEventArgs (int item, string? label) { Item = item; Label = label; }

        /// <summary>Gets the zero-based index of the item being edited.</summary>
        public int Item { get; }

        /// <summary>Gets the text assigned to the item's label during label editing, or null if cancelled.</summary>
        public string? Label { get; }
    }

    /// <summary>Provides data for the ListView.RetrieveVirtualItem event.</summary>
    public class RetrieveVirtualItemEventArgs : EventArgs
    {
        /// <summary>Initializes a new instance.</summary>
        public RetrieveVirtualItemEventArgs (int itemIndex) { ItemIndex = itemIndex; }

        /// <summary>Gets the index of the item to retrieve.</summary>
        public int ItemIndex { get; }

        /// <summary>Gets or sets the item retrieved for the given index.</summary>
        public ListViewItem? Item { get; set; }
    }

    /// <summary>Provides data for the ListView.CacheVirtualItems event.</summary>
    public class CacheVirtualItemsEventArgs : EventArgs
    {
        /// <summary>Initializes a new instance.</summary>
        public CacheVirtualItemsEventArgs (int startIndex, int endIndex) { StartIndex = startIndex; EndIndex = endIndex; }

        /// <summary>Gets the start of the requested item range.</summary>
        public int StartIndex { get; }

        /// <summary>Gets the end of the requested item range.</summary>
        public int EndIndex { get; }
    }

    /// <summary>Specifies the style of a 3D border drawn by ControlPaint.</summary>
    public enum Border3DStyle
    {
        /// <summary>No border.</summary>
        None = 0,
        /// <summary>A sunken inner border and a raised outer border.</summary>
        Adjust = 8192,
        /// <summary>A flat border.</summary>
        Flat = 0x4010,
        /// <summary>A raised inner border and a sunken outer border.</summary>
        Info = 0x0400,
        /// <summary>A raised inner border only.</summary>
        Raised = 2,
        /// <summary>A raised inner border and a raised outer border (default).</summary>
        RaisedInner = 4,
        /// <summary>A raised outer border only.</summary>
        RaisedOuter = 1,
        /// <summary>A sunken inner and outer border.</summary>
        Sunken = 10,
        /// <summary>A sunken inner border only.</summary>
        SunkenInner = 8,
        /// <summary>A sunken outer border only.</summary>
        SunkenOuter = 2,
        /// <summary>The border has no three-dimensional effect.</summary>
        Bump = 0x0409,
        /// <summary>The border looks etched into the form.</summary>
        Etched = 6
    }

    /// <summary>Specifies the border style for a button drawn by ControlPaint.</summary>
    public enum ButtonBorderStyle
    {
        /// <summary>No border.</summary>
        None,
        /// <summary>A dotted-line border.</summary>
        Dotted,
        /// <summary>A dashed border.</summary>
        Dashed,
        /// <summary>A solid border.</summary>
        Solid,
        /// <summary>A sunken border.</summary>
        Inset,
        /// <summary>A raised border.</summary>
        Outset
    }

    /// <summary>Specifies the state of a button drawn by ControlPaint.</summary>
    [Flags]
    public enum ButtonState
    {
        /// <summary>Normal state.</summary>
        Normal = 0,
        /// <summary>The button is inactive (disabled).</summary>
        Inactive = 256,
        /// <summary>The button appears pushed.</summary>
        Pushed = 512,
        /// <summary>The button is checked.</summary>
        Checked = 1024,
        /// <summary>The button has a flat, two-dimensional border.</summary>
        Flat = 16384,
        /// <summary>All flags are set.</summary>
        All = Flat | Checked | Pushed | Inactive
    }

    /// <summary>Specifies the image used to paint a menu glyph.</summary>
    public enum MenuGlyph
    {
        /// <summary>An arrow.</summary>
        Arrow = 16,
        /// <summary>A bullet.</summary>
        Bullet = 18,
        /// <summary>A check mark.</summary>
        Checkmark = 17,
        /// <summary>A minimum value.</summary>
        Min = 16,
        /// <summary>A maximum value.</summary>
        Max = 18
    }

    /// <summary>Specifies the direction of a scroll button.</summary>
    public enum ScrollButton
    {
        /// <summary>Scroll up.</summary>
        Up,
        /// <summary>Scroll down.</summary>
        Down,
        /// <summary>Scroll left.</summary>
        Left,
        /// <summary>Scroll right.</summary>
        Right,
        /// <summary>Minimum value.</summary>
        Min = Up,
        /// <summary>Maximum value.</summary>
        Max = Right
    }

    /// <summary>Specifies how items are drawn in a ListBox or ComboBox.</summary>
    public enum DrawMode
    {
        /// <summary>Items are drawn by the operating system (default).</summary>
        Normal,
        /// <summary>Items are drawn by the owner at a fixed height.</summary>
        OwnerDrawFixed,
        /// <summary>Items are drawn by the owner at variable heights.</summary>
        OwnerDrawVariable
    }

    /// <summary>WinForms compat: specifies the alignment of a ToolStrip item.</summary>
    public enum ToolStripItemAlignment
    {
        /// <summary>Item is aligned to the left.</summary>
        Left,
        /// <summary>Item is aligned to the right.</summary>
        Right
    }

    /// <summary>Specifies the accessible role for a control.</summary>
    public enum AccessibleRole
    {
        /// <summary>No accessible role.</summary>
        None = -1,
        /// <summary>Default accessible role.</summary>
        Default = 0,
        /// <summary>A title bar.</summary>
        TitleBar = 1,
        /// <summary>A menu bar.</summary>
        MenuBar = 2,
        /// <summary>A scroll bar.</summary>
        ScrollBar = 3,
        /// <summary>A grip handle.</summary>
        Grip = 4,
        /// <summary>A sound object.</summary>
        Sound = 5,
        /// <summary>A cursor.</summary>
        Cursor = 6,
        /// <summary>A caret.</summary>
        Caret = 7,
        /// <summary>An alert or condition notification.</summary>
        Alert = 8,
        /// <summary>A window.</summary>
        Window = 9,
        /// <summary>A client window.</summary>
        Client = 10,
        /// <summary>A menu pop-up.</summary>
        MenuPopup = 11,
        /// <summary>A menu item.</summary>
        MenuItem = 12,
        /// <summary>A tool tip.</summary>
        ToolTip = 13,
        /// <summary>An application.</summary>
        Application = 14,
        /// <summary>A document.</summary>
        Document = 15,
        /// <summary>A pane.</summary>
        Pane = 16,
        /// <summary>A chart.</summary>
        Chart = 17,
        /// <summary>A dialog box.</summary>
        Dialog = 18,
        /// <summary>A border.</summary>
        Border = 19,
        /// <summary>A group box.</summary>
        Grouping = 20,
        /// <summary>A separator.</summary>
        Separator = 21,
        /// <summary>A toolbar.</summary>
        ToolBar = 22,
        /// <summary>A status bar.</summary>
        StatusBar = 23,
        /// <summary>A table.</summary>
        Table = 24,
        /// <summary>A column header.</summary>
        ColumnHeader = 25,
        /// <summary>A row header.</summary>
        RowHeader = 26,
        /// <summary>A column.</summary>
        Column = 27,
        /// <summary>A row.</summary>
        Row = 28,
        /// <summary>A cell.</summary>
        Cell = 29,
        /// <summary>A link.</summary>
        Link = 30,
        /// <summary>A help balloon.</summary>
        HelpBalloon = 31,
        /// <summary>A character.</summary>
        Character = 32,
        /// <summary>A list.</summary>
        List = 33,
        /// <summary>A list item.</summary>
        ListItem = 34,
        /// <summary>An outline (tree).</summary>
        Outline = 35,
        /// <summary>An outline item (tree node).</summary>
        OutlineItem = 36,
        /// <summary>A page tab.</summary>
        PageTab = 37,
        /// <summary>A property page.</summary>
        PropertyPage = 38,
        /// <summary>An indicator.</summary>
        Indicator = 39,
        /// <summary>An image.</summary>
        Graphic = 40,
        /// <summary>A static text label.</summary>
        StaticText = 41,
        /// <summary>An editable text field.</summary>
        Text = 42,
        /// <summary>A push button.</summary>
        PushButton = 43,
        /// <summary>A check button.</summary>
        CheckButton = 44,
        /// <summary>An option (radio) button.</summary>
        RadioButton = 45,
        /// <summary>A combo box.</summary>
        ComboBox = 46,
        /// <summary>A drop-down list.</summary>
        DropList = 47,
        /// <summary>A progress bar.</summary>
        ProgressBar = 48,
        /// <summary>A dial.</summary>
        Dial = 49,
        /// <summary>A hot-key field.</summary>
        HotkeyField = 50,
        /// <summary>A slider.</summary>
        Slider = 51,
        /// <summary>A spin box.</summary>
        SpinButton = 52,
        /// <summary>A diagram.</summary>
        Diagram = 53,
        /// <summary>An animation.</summary>
        Animation = 54,
        /// <summary>An equation.</summary>
        Equation = 55,
        /// <summary>A drop-down button.</summary>
        ButtonDropDown = 56,
        /// <summary>A drop-down button that shows a menu.</summary>
        ButtonMenu = 57,
        /// <summary>A drop-down button that shows a grid.</summary>
        ButtonDropDownGrid = 58,
        /// <summary>A whitespace area.</summary>
        WhiteSpace = 59,
        /// <summary>A page tab list.</summary>
        PageTabList = 60,
        /// <summary>A clock.</summary>
        Clock = 61
    }

    /// <summary>WinForms compatibility: provides access to a Win32 HWND. Stub in Majorsilence.Forms.</summary>
    public interface IWin32Window
    {
        /// <summary>Gets the Win32 handle. Always IntPtr.Zero in Majorsilence.Forms.</summary>
        IntPtr Handle { get; }
    }

    /// <summary>WinForms compatibility: contains environment and resource information. Stub in Majorsilence.Forms.</summary>
    public static class NativeMethods
    {
        /// <summary>Stub — Majorsilence.Forms has no Win32 HWND; always returns 0.</summary>
        public static nint SendMessage (nint hWnd, int msg, nint wParam, nint lParam) => 0;

        /// <summary>Stub — Majorsilence.Forms has no Win32 HWND; always returns false.</summary>
        public static bool PostMessage (nint hWnd, int msg, nint wParam, nint lParam) => false;
    }

    /// <summary>Specifies the appearance of a CheckBox or RadioButton.</summary>
    public enum Appearance
    {
        /// <summary>Standard appearance (checkbox/radio with label).</summary>
        Normal,
        /// <summary>Button-like appearance.</summary>
        Button
    }

    /// <summary>Describes the visual appearance of a Button in the Flat style.</summary>
    public class FlatButtonAppearance
    {
        /// <summary>Gets or sets the border color of the button. Stub in Majorsilence.Forms.</summary>
        public System.Drawing.Color BorderColor { get; set; } = System.Drawing.Color.Empty;

        /// <summary>Gets or sets the border size. Stub in Majorsilence.Forms.</summary>
        public int BorderSize { get; set; } = 1;

        /// <summary>Gets or sets the background color when the mouse button is held down. Stub in Majorsilence.Forms.</summary>
        public System.Drawing.Color MouseDownBackColor { get; set; } = System.Drawing.Color.Empty;

        /// <summary>Gets or sets the background color when the mouse is over the button. Stub in Majorsilence.Forms.</summary>
        public System.Drawing.Color MouseOverBackColor { get; set; } = System.Drawing.Color.Empty;

        /// <summary>Gets or sets the background color when the button is checked. Stub in Majorsilence.Forms.</summary>
        public System.Drawing.Color CheckedBackColor { get; set; } = System.Drawing.Color.Empty;
    }

    /// <summary>Specifies the high DPI mode for a Windows Forms application.</summary>
    public enum HighDpiMode
    {
        /// <summary>DPI is not set programmatically.</summary>
        DpiUnaware = 0,
        /// <summary>Application is system DPI-aware.</summary>
        SystemAware = 1,
        /// <summary>Application is per-monitor DPI-aware (v1).</summary>
        PerMonitor = 2,
        /// <summary>Application is per-monitor DPI-aware (v2).</summary>
        PerMonitorV2 = 3,
        /// <summary>DPI awareness is inherited from the system.</summary>
        DpiUnawareGdiScaled = 4
    }

    /// <summary>Specifies the left or right alignment of a UI element.</summary>
    public enum LeftRightAlignment
    {
        /// <summary>Aligned to the left.</summary>
        Left,
        /// <summary>Aligned to the right.</summary>
        Right
    }

    /// <summary>Specifies the alignment of the tabs in a TabControl.</summary>
    public enum TabAlignment
    {
        /// <summary>Tabs are aligned to the top.</summary>
        Top,
        /// <summary>Tabs are aligned to the bottom.</summary>
        Bottom,
        /// <summary>Tabs are aligned to the left.</summary>
        Left,
        /// <summary>Tabs are aligned to the right.</summary>
        Right
    }

    /// <summary>Specifies the draw mode for a TabControl.</summary>
    public enum TabDrawMode
    {
        /// <summary>All tabs are drawn by the operating system.</summary>
        Normal,
        /// <summary>All tabs are drawn by the owner (custom drawing).</summary>
        OwnerDrawFixed
    }

    /// <summary>Specifies the visual appearance of a TabControl.</summary>
    public enum TabAppearance
    {
        /// <summary>Tabs appear as standard buttons.</summary>
        Normal,
        /// <summary>Tabs appear as 3D buttons.</summary>
        Buttons,
        /// <summary>Tabs appear as flat buttons.</summary>
        FlatButtons
    }

    /// <summary>Provides data for the TabControl.Selecting and TabControl.Deselecting events.</summary>
    public class TabControlCancelEventArgs : System.ComponentModel.CancelEventArgs
    {
        /// <summary>Initializes a new instance.</summary>
        public TabControlCancelEventArgs (TabPage? tabPage, int tabPageIndex, bool cancel, TabControlAction action)
            : base (cancel)
        {
            TabPage = tabPage;
            TabPageIndex = tabPageIndex;
            Action = action;
        }

        /// <summary>Gets the tab page being selected or deselected.</summary>
        public TabPage? TabPage { get; }

        /// <summary>Gets the index of the tab page.</summary>
        public int TabPageIndex { get; }

        /// <summary>Gets the action that caused the event.</summary>
        public TabControlAction Action { get; }
    }

    /// <summary>Provides data for the TabControl.Selected and TabControl.Deselected events.</summary>
    public class TabControlEventArgs : EventArgs
    {
        /// <summary>Initializes a new instance.</summary>
        public TabControlEventArgs (TabPage? tabPage, int tabPageIndex, TabControlAction action)
        {
            TabPage = tabPage;
            TabPageIndex = tabPageIndex;
            Action = action;
        }

        /// <summary>Gets the tab page being selected or deselected.</summary>
        public TabPage? TabPage { get; }

        /// <summary>Gets the index of the tab page.</summary>
        public int TabPageIndex { get; }

        /// <summary>Gets the action that caused the event.</summary>
        public TabControlAction Action { get; }
    }

    /// <summary>Specifies the action that caused a TabControl event.</summary>
    public enum TabControlAction
    {
        /// <summary>A tab page was selected.</summary>
        Selecting,
        /// <summary>A tab page was selected (after).</summary>
        Selected,
        /// <summary>A tab page was deselected.</summary>
        Deselecting,
        /// <summary>A tab page was deselected (after).</summary>
        Deselected
    }

    /// <summary>Specifies how tabs are sized in a TabControl.</summary>
    public enum TabSizeMode
    {
        /// <summary>Tabs are sized to fit their label.</summary>
        Normal,
        /// <summary>Tabs are fixed-size.</summary>
        Fixed,
        /// <summary>Tabs are stretched to fill the available width.</summary>
        FillToRight
    }

    /// <summary>Provides data for an owner-draw event.</summary>
    public class DrawItemEventArgs : EventArgs, IDisposable
    {
        private readonly Graphics _graphics;

#pragma warning disable CA1416
        /// <summary>Initializes a new instance using a Skia canvas.</summary>
        public DrawItemEventArgs (SkiaSharp.SKCanvas canvas, System.Drawing.Rectangle bounds, int index, DrawItemState state)
        {
            _graphics = new Graphics (canvas);
            Bounds = bounds;
            Index = index;
            State = state;
            Font = Majorsilence.Forms.SystemFonts.DefaultFont ?? Majorsilence.Forms.SystemFonts.SmallCaptionFont ?? new Majorsilence.Forms.Drawing.Font ("Arial", 9);
            ForeColor = Majorsilence.Forms.SystemColors.WindowText;
            BackColor = (state & DrawItemState.Selected) != 0 ? Majorsilence.Forms.SystemColors.Highlight : Majorsilence.Forms.SystemColors.Window;
        }
#pragma warning restore CA1416

        /// <summary>Gets the graphics object for drawing.</summary>
        public Graphics Graphics => _graphics;

        /// <summary>Gets the bounding rectangle of the item to draw.</summary>
        public System.Drawing.Rectangle Bounds { get; }

        /// <summary>Gets the index of the item to draw.</summary>
        public int Index { get; }

        /// <summary>Gets the state of the item to draw.</summary>
        public DrawItemState State { get; }

        /// <summary>Gets the font to use for drawing text.</summary>
        public Majorsilence.Forms.Drawing.Font Font { get; }

        /// <summary>Gets the foreground color for drawing.</summary>
        public System.Drawing.Color ForeColor { get; }

        /// <summary>Gets the background color for drawing.</summary>
        public System.Drawing.Color BackColor { get; }

        /// <summary>Draws the background using the default appearance.</summary>
        public void DrawBackground ()
        {
#pragma warning disable CA1416
            using var brush = new Majorsilence.Forms.Drawing.SolidBrush (BackColor);
#pragma warning restore CA1416
            _graphics.FillRectangle (brush, Bounds);
        }

        /// <summary>Draws the focus rectangle using the default appearance.</summary>
        public void DrawFocusRectangle () => _graphics.DrawFocusRectangle (Bounds);

        /// <inheritdoc/>
        public void Dispose ()
        {
            _graphics.Dispose ();
            GC.SuppressFinalize (this);
        }
    }

    /// <summary>Represents the state of an owner-drawn item.</summary>
    [System.Flags]
    public enum DrawItemState
    {
        /// <summary>Default state.</summary>
        None = 0,
        /// <summary>The item is selected.</summary>
        Selected = 1,
        /// <summary>The item has focus.</summary>
        Focus = 16,
        /// <summary>The item is disabled.</summary>
        Disabled = 4,
        /// <summary>The item is checked.</summary>
        Checked = 8,
        /// <summary>The item is the default item.</summary>
        Default = 32
    }

    /// <summary>Provides data for the MeasureItem event.</summary>
    public class MeasureItemEventArgs : EventArgs
    {
        /// <summary>Initializes a new instance.</summary>
        public MeasureItemEventArgs (SkiaSharp.SKCanvas graphics, int index)
        {
            Graphics = graphics;
            Index = index;
        }

        /// <summary>Gets the graphics canvas.</summary>
        public SkiaSharp.SKCanvas Graphics { get; }

        /// <summary>Gets the index of the item to measure.</summary>
        public int Index { get; }

        /// <summary>Gets or sets the height of the item.</summary>
        public int ItemHeight { get; set; }

        /// <summary>Gets or sets the width of the item.</summary>
        public int ItemWidth { get; set; }
    }

    /// <summary>
    /// WinForms compatibility: provides methods to simulate keyboard input.
    /// All methods are stubs in Majorsilence.Forms.
    /// </summary>
    public static class SendKeys
    {
        /// <summary>Sends keystrokes to the active application. Stub in Majorsilence.Forms.</summary>
        public static void Send (string keys) { }

        /// <summary>Sends keystrokes to the active application and waits for processing. Stub in Majorsilence.Forms.</summary>
        public static void SendWait (string keys) { }

        /// <summary>Flushes the SendKeys buffer. Stub in Majorsilence.Forms.</summary>
        public static void Flush () { }
    }

    /// <summary>Specifies the possible effects of a drag-and-drop operation.</summary>
    [System.Flags]
    public enum DragDropEffects
    {
        /// <summary>The drop target does not accept the data.</summary>
        None = 0,
        /// <summary>The data from the drag source is copied to the drop target.</summary>
        Copy = 1,
        /// <summary>The data from the drag source is moved to the drop target.</summary>
        Move = 2,
        /// <summary>The data from the drag source is linked to the drop target.</summary>
        Link = 4,
        /// <summary>The combination of Copy and Move.</summary>
        Scroll = -2147483648,
        /// <summary>All drop effects are allowed.</summary>
        All = -2147483645
    }

    /// <summary>Provides data for drag-and-drop events.</summary>
    public class DragEventArgs : EventArgs
    {
        /// <summary>Initializes a new instance.</summary>
        public DragEventArgs (object? data, int keyState, int x, int y, DragDropEffects allowedEffect, DragDropEffects effect)
        {
            Data = data;
            KeyState = keyState;
            X = x;
            Y = y;
            AllowedEffect = allowedEffect;
            Effect = effect;
        }

        /// <summary>Gets the data object that contains the data associated with this event.</summary>
        public object? Data { get; }

        /// <summary>Gets the current state of the keyboard modifier keys.</summary>
        public int KeyState { get; }

        /// <summary>Gets the x-coordinate of the mouse pointer.</summary>
        public int X { get; }

        /// <summary>Gets the y-coordinate of the mouse pointer.</summary>
        public int Y { get; }

        /// <summary>Gets the allowed drag-and-drop operations.</summary>
        public DragDropEffects AllowedEffect { get; }

        /// <summary>Gets or sets the target drop effect in a drag-and-drop operation.</summary>
        public DragDropEffects Effect { get; set; }
    }

    /// <summary>Provides data for the GiveFeedback drag-and-drop event.</summary>
    public class GiveFeedbackEventArgs : EventArgs
    {
        /// <summary>Initializes a new instance.</summary>
        public GiveFeedbackEventArgs (DragDropEffects effect, bool useDefaultCursors)
        {
            Effect = effect;
            UseDefaultCursors = useDefaultCursors;
        }

        /// <summary>Gets the type of drag-and-drop operation.</summary>
        public DragDropEffects Effect { get; }

        /// <summary>Gets or sets whether to use the default drag cursor.</summary>
        public bool UseDefaultCursors { get; set; }
    }

    /// <summary>Provides data for the QueryContinueDrag drag-and-drop event.</summary>
    public class QueryContinueDragEventArgs : EventArgs
    {
        /// <summary>Initializes a new instance.</summary>
        public QueryContinueDragEventArgs (int keyState, bool escapePressed, DragAction action)
        {
            KeyState = keyState;
            EscapePressed = escapePressed;
            Action = action;
        }

        /// <summary>Gets the state of keyboard modifier keys.</summary>
        public int KeyState { get; }

        /// <summary>Gets whether the Escape key was pressed.</summary>
        public bool EscapePressed { get; }

        /// <summary>Gets or sets the drag action to perform.</summary>
        public DragAction Action { get; set; }
    }

    /// <summary>Specifies how a drag-and-drop operation should proceed.</summary>
    public enum DragAction
    {
        /// <summary>The drag-and-drop operation should continue.</summary>
        Continue,
        /// <summary>The drag-and-drop operation should stop with a drop.</summary>
        Drop,
        /// <summary>The drag-and-drop operation should be cancelled with no drop.</summary>
        Cancel
    }

    /// <summary>Provides data for the TreeView.NodeMouseClick and NodeMouseDoubleClick events.</summary>
    public class TreeNodeMouseClickEventArgs : MouseEventArgs
    {
        /// <summary>Gets the tree node that was clicked.</summary>
        public TreeViewItem Node { get; }

        /// <summary>Initializes a new instance.</summary>
        public TreeNodeMouseClickEventArgs (TreeViewItem node, MouseButtons button, int clicks, int x, int y)
            : base (button, clicks, x, y, System.Drawing.Point.Empty)
        {
            Node = node;
        }
    }

    /// <summary>Provides data for the TreeView.NodeMouseHover event.</summary>
    public class TreeNodeMouseHoverEventArgs : EventArgs
    {
        /// <summary>Gets the tree node that the mouse is hovering over.</summary>
        public TreeViewItem Node { get; }

        /// <summary>Initializes a new instance.</summary>
        public TreeNodeMouseHoverEventArgs (TreeViewItem node) => Node = node;
    }

    /// <summary>Provides data for the TreeView.ItemDrag and ListView.ItemDrag events.</summary>
    public class ItemDragEventArgs : EventArgs
    {
        /// <summary>Initializes a new instance.</summary>
        public ItemDragEventArgs (MouseButtons button, object? item) { Button = button; Item = item; }

        /// <summary>Gets the mouse button that was pressed during the drag.</summary>
        public MouseButtons Button { get; }

        /// <summary>Gets the item being dragged.</summary>
        public object? Item { get; }
    }

    /// <summary>Specifies why a ToolStripDropDown was closed.</summary>
    public enum ToolStripDropDownCloseReason
    {
        /// <summary>Closed because the application focus changed.</summary>
        AppFocusChange = 0,
        /// <summary>Closed because the application clicked somewhere outside the ToolStrip.</summary>
        AppClicked = 1,
        /// <summary>Closed because an item was clicked.</summary>
        ItemClicked = 2,
        /// <summary>Closed programmatically by calling Close().</summary>
        CloseCalled = 3,
        /// <summary>Closed because the keyboard was used to select an item.</summary>
        Keyboard = 4
    }

    /// <summary>Provides data for the ToolStripDropDown.Closed event.</summary>
    public class ToolStripDropDownClosedEventArgs : EventArgs
    {
        /// <summary>Initializes a new instance.</summary>
        public ToolStripDropDownClosedEventArgs (ToolStripDropDownCloseReason closeReason) { CloseReason = closeReason; }

        /// <summary>Gets the reason the drop-down was closed.</summary>
        public ToolStripDropDownCloseReason CloseReason { get; }
    }

    /// <summary>Provides data for the ToolStripDropDown.Closing event.</summary>
    public class ToolStripDropDownClosingEventArgs : System.ComponentModel.CancelEventArgs
    {
        /// <summary>Initializes a new instance.</summary>
        public ToolStripDropDownClosingEventArgs (ToolStripDropDownCloseReason closeReason) { CloseReason = closeReason; }

        /// <summary>Gets the reason the drop-down is closing.</summary>
        public ToolStripDropDownCloseReason CloseReason { get; }
    }

    /// <summary>Provides data for the RichTextBox.ContentsResized event.</summary>
    public class ContentsResizedEventArgs : EventArgs
    {
        /// <summary>Initializes a new instance of ContentsResizedEventArgs.</summary>
        public ContentsResizedEventArgs (System.Drawing.Rectangle newRectangle) { NewRectangle = newRectangle; }

        /// <summary>Gets the requested size of the RichTextBox control.</summary>
        public System.Drawing.Rectangle NewRectangle { get; }
    }

    /// <summary>Provides data for the RichTextBox.LinkClicked event.</summary>
    public class LinkClickedEventArgs : EventArgs
    {
        /// <summary>Initializes a new instance of LinkClickedEventArgs.</summary>
        public LinkClickedEventArgs (string linkText) { LinkText = linkText; }

        /// <summary>Gets the text of the link that was clicked.</summary>
        public string LinkText { get; }
    }

    /// <summary>Provides data for events that contain a Control reference.</summary>
    public class ControlEventArgs : EventArgs
    {
        /// <summary>Initializes a new instance of ControlEventArgs.</summary>
        public ControlEventArgs (Control? control) { Control = control; }

        /// <summary>Gets the control referenced by this event.</summary>
        public Control? Control { get; }
    }

    /// <summary>Provides data for binding completion events.</summary>
    public class BindingCompleteEventArgs : System.ComponentModel.CancelEventArgs
    {
        /// <summary>Initializes a new instance of BindingCompleteEventArgs.</summary>
        public BindingCompleteEventArgs (Binding? binding, BindingCompleteContext context)
        {
            Binding = binding;
            BindingCompleteContext = context;
        }

        /// <summary>Gets the binding associated with this event.</summary>
        public Binding? Binding { get; }

        /// <summary>Gets the context in which the binding completed.</summary>
        public BindingCompleteContext BindingCompleteContext { get; }

        /// <summary>Gets or sets the error text. Stub in Majorsilence.Forms.</summary>
        public string ErrorText { get; set; } = string.Empty;

        /// <summary>Gets or sets the exception. Stub in Majorsilence.Forms.</summary>
        public Exception? Exception { get; set; }
    }

    /// <summary>Specifies the context in which a binding completes.</summary>
    public enum BindingCompleteContext
    {
        /// <summary>A control property value was pushed to the data source.</summary>
        ControlUpdate,
        /// <summary>A data source value was pushed to the control.</summary>
        DataSourceUpdate
    }

    /// <summary>Provides data for the Form.InputLanguageChanged event.</summary>
    public class InputLanguageChangedEventArgs : EventArgs
    {
        /// <summary>Initializes a new instance.</summary>
        public InputLanguageChangedEventArgs (System.Globalization.CultureInfo culture, byte charSet)
        {
            Culture = culture;
            CharSet = charSet;
        }

        /// <summary>Gets the culture of the new input language.</summary>
        public System.Globalization.CultureInfo Culture { get; }

        /// <summary>Gets the character set associated with the new input language.</summary>
        public byte CharSet { get; }

        /// <summary>Gets the input language. Stub in Majorsilence.Forms.</summary>
        public object? InputLanguage => null;
    }

    /// <summary>Provides data for the Form.InputLanguageChanging event.</summary>
    public class InputLanguageChangingEventArgs : System.ComponentModel.CancelEventArgs
    {
        /// <summary>Initializes a new instance.</summary>
        public InputLanguageChangingEventArgs (System.Globalization.CultureInfo culture, bool sysCharSet)
        {
            Culture = culture;
            SysCharSet = sysCharSet;
        }

        /// <summary>Gets the culture of the new input language.</summary>
        public System.Globalization.CultureInfo Culture { get; }

        /// <summary>Gets whether the system default font supports the character set required by the new input language.</summary>
        public bool SysCharSet { get; }

        /// <summary>Gets the input language. Stub in Majorsilence.Forms.</summary>
        public object? InputLanguage => null;
    }

    /// <summary>Specifies the layout of MDI child windows in a parent window.</summary>
    public enum MdiLayout
    {
        /// <summary>All MDI child windows are cascaded within the client region of the MDI parent form.</summary>
        Cascade = 0,
        /// <summary>All MDI child windows are tiled horizontally within the client region of the MDI parent form.</summary>
        TileHorizontal = 1,
        /// <summary>All MDI child windows are tiled vertically within the client region of the MDI parent form.</summary>
        TileVertical = 2,
        /// <summary>All MDI child windows are arranged as icons at the bottom of the MDI parent form.</summary>
        ArrangeIcons = 3
    }

    /// <summary>WinForms compatibility alias for <see cref="HorizontalScrollBar"/>.</summary>
    public class HScrollBar : HorizontalScrollBar { }

    /// <summary>WinForms compatibility alias for <see cref="VerticalScrollBar"/>.</summary>
    public class VScrollBar : VerticalScrollBar { }

    /// <summary>Translates colors from one representation to another. Stub wraps System.Drawing.ColorTranslator where available.</summary>
    public static class ColorTranslator
    {
        /// <summary>Translates an HTML color string to a Color. E.g. "#FF0000" → Color.Red.</summary>
        public static Color FromHtml (string htmlColor)
        {
            if (string.IsNullOrWhiteSpace (htmlColor))
                return Color.Empty;

            try {
                var s = htmlColor.Trim ();

                if (s.StartsWith ('#')) {
                    s = s.Substring (1);

                    if (s.Length == 3)
                        s = $"{s[0]}{s[0]}{s[1]}{s[1]}{s[2]}{s[2]}";

                    if (s.Length == 6)
                        return Color.FromArgb (Convert.ToInt32 (s, 16) | unchecked((int)0xFF000000));

                    if (s.Length == 8)
                        return Color.FromArgb (Convert.ToInt32 (s, 16));
                }

                return Color.FromName (htmlColor);
            } catch {
                return Color.Empty;
            }
        }

        /// <summary>Converts a Color to an HTML color string (#RRGGBB).</summary>
        public static string ToHtml (Color color) => $"#{color.R:X2}{color.G:X2}{color.B:X2}";

        /// <summary>Translates an OLE color value to a Color.</summary>
        public static Color FromOle (int oleColor) => Color.FromArgb (0xFF, oleColor & 0xFF, (oleColor >> 8) & 0xFF, (oleColor >> 16) & 0xFF);

        /// <summary>Converts a Color to an OLE color value.</summary>
        public static int ToOle (Color color) => color.R | (color.G << 8) | (color.B << 16);

        /// <summary>Translates a Win32 color value to a Color.</summary>
        public static Color FromWin32 (int win32Color) => FromOle (win32Color);

        /// <summary>Converts a Color to a Win32 color value.</summary>
        public static int ToWin32 (Color color) => ToOle (color);
    }

    /// <summary>WinForms compatibility: provides help for controls on a form. Stub in Majorsilence.Forms.
    /// Targets are keyed as objects so both <see cref="Control"/>s and <see cref="WindowBase"/>-derived
    /// forms are accepted — in real WinForms a Form IS a Control, and designer code routinely calls
    /// SetHelpKeyword(Me, …) on the form itself.</summary>
    public class HelpProvider : System.ComponentModel.Component
    {
        private readonly System.Collections.Generic.Dictionary<object, string> _helpStrings = new ();
        private readonly System.Collections.Generic.Dictionary<object, string> _helpKeywords = new ();

        /// <summary>Initializes a new instance of HelpProvider.</summary>
        public HelpProvider () { }

        /// <summary>Initializes a new instance of HelpProvider and adds it to the specified container.</summary>
        public HelpProvider (System.ComponentModel.IContainer container) { container.Add (this); }

        /// <summary>Gets or sets the path to the help file. Stub in Majorsilence.Forms.</summary>
        public string HelpNamespace { get; set; } = string.Empty;

        /// <summary>Sets the help string for the specified control.</summary>
        public void SetHelpString (Control ctl, string helpString) => _helpStrings[ctl] = helpString;

        /// <summary>Sets the help string for the specified form or window.</summary>
        public void SetHelpString (WindowBase window, string helpString) => _helpStrings[window] = helpString;

        /// <summary>Gets the help string for the specified control.</summary>
        public string GetHelpString (Control ctl) => _helpStrings.TryGetValue (ctl, out var s) ? s : string.Empty;

        /// <summary>Gets the help string for the specified form or window.</summary>
        public string GetHelpString (WindowBase window) => _helpStrings.TryGetValue (window, out var s) ? s : string.Empty;

        /// <summary>Sets the help keyword for the specified control.</summary>
        public void SetHelpKeyword (Control ctl, string keyword) => _helpKeywords[ctl] = keyword;

        /// <summary>Sets the help keyword for the specified form or window.</summary>
        public void SetHelpKeyword (WindowBase window, string keyword) => _helpKeywords[window] = keyword;

        /// <summary>Gets the help keyword for the specified control.</summary>
        public string GetHelpKeyword (Control ctl) => _helpKeywords.TryGetValue (ctl, out var k) ? k : string.Empty;

        /// <summary>Gets the help keyword for the specified form or window.</summary>
        public string GetHelpKeyword (WindowBase window) => _helpKeywords.TryGetValue (window, out var k) ? k : string.Empty;

        /// <summary>Sets whether the help provider is enabled for the specified control. Stub in Majorsilence.Forms.</summary>
        public void SetShowHelp (Control ctl, bool value) { }

        /// <summary>Sets whether the help provider is enabled for the specified form or window. Stub in Majorsilence.Forms.</summary>
        public void SetShowHelp (WindowBase window, bool value) { }

        /// <summary>Gets whether the help provider is enabled for the specified control. Stub in Majorsilence.Forms.</summary>
        public bool GetShowHelp (Control ctl) => true;

        /// <summary>Gets whether the help provider is enabled for the specified form or window. Stub in Majorsilence.Forms.</summary>
        public bool GetShowHelp (WindowBase window) => true;

        /// <summary>Sets the navigator type for the specified control. Stub in Majorsilence.Forms.</summary>
        public void SetHelpNavigator (Control ctl, HelpNavigator navigator) { }

        /// <summary>Sets the navigator type for the specified form or window. Stub in Majorsilence.Forms.</summary>
        public void SetHelpNavigator (WindowBase window, HelpNavigator navigator) { }

        /// <summary>Gets or sets user-defined data associated with this component. Stub in Majorsilence.Forms.</summary>
        public object? Tag { get; set; }
    }

    /// <summary>Specifies constants indicating which elements of the Help file to display.</summary>
    public enum HelpNavigator
    {
        /// <summary>The Help file opens to the topic corresponding to the specified keyword.</summary>
        AssociateIndex = -2147483647,
        /// <summary>The Help file opens to the Find tab in the navigation pane.</summary>
        Find = -2147483644,
        /// <summary>The Help file opens to the index tab.</summary>
        Index = -2147483645,
        /// <summary>The Help file opens to the keywords tab.</summary>
        KeywordIndex = -2147483643,
        /// <summary>The Help file opens to a specified topic.</summary>
        Topic = -2147483646,
        /// <summary>The Help file opens to the table of contents.</summary>
        TableOfContents = -2147483642,
        /// <summary>The Help file opens to a specified topic.</summary>
        TopicId = -2147483641,
    }

    /// <summary>WinForms compatibility: provides methods for sending keystrokes to an application. Stub in Majorsilence.Forms.</summary>
    public static class Help
    {
        /// <summary>Displays the contents of a Help file. Stub in Majorsilence.Forms.</summary>
        public static void ShowHelp (Control? parent, string? url) { }

        /// <summary>Displays a Help pop-up window. Stub in Majorsilence.Forms.</summary>
        public static void ShowPopup (Control parent, string caption, System.Drawing.Point location) { }
    }

    /// <summary>
    /// Specifies the contextual information about an application thread, such as the main form,
    /// used when calling <see cref="Application.Run(ApplicationContext)"/>.
    /// </summary>
    public class ApplicationContext : IDisposable
    {
        /// <summary>Initializes a new instance of the ApplicationContext class.</summary>
        public ApplicationContext () { }

        /// <summary>Initializes a new instance of the ApplicationContext class with the specified Form as the main form.</summary>
        public ApplicationContext (Form mainForm) { MainForm = mainForm; }

        /// <summary>Gets or sets the Form to use as context for this thread.</summary>
        public Form? MainForm { get; set; }

        /// <summary>Occurs when the message loop of the thread should exit.</summary>
        public event EventHandler? ThreadExit;

        /// <summary>Terminates the message loop of the thread.</summary>
        public void ExitThread ()
        {
            ThreadExit?.Invoke (this, EventArgs.Empty);
            Application.Exit ();
        }

        /// <inheritdoc/>
        public void Dispose () { GC.SuppressFinalize (this); }
    }

}
