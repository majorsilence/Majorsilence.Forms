using System;
using System.Drawing;
using System.Linq;

namespace Majorsilence.Forms
{
    // RichTextBox, TreeView, MaskedTextBox and AccessibleObject parity
    // (docs/winforms-gap-plan.md).
    //
    // AccessibleObject is the one with real reach. It is the object every control hands to a screen
    // reader, and it was missing the whole navigation and state surface -- State, Value, Navigate,
    // HitTest, DoDefaultAction. State in particular is computed from the owning control rather than
    // returned as None, so an assistive client is told what it is actually looking at.
    //
    // RichTextBox's undo and redo members are the honest exception: there is no undo stack in this
    // layer, so CanRedo is false and the action names are empty rather than describing an edit that
    // could not be reversed.

    public partial class AccessibleObject
    {
        /// <summary>Gets or sets the object's value as text.</summary>
        public virtual string? Value { get; set; }

        /// <summary>Gets the action performed when the object is activated.</summary>
        public virtual string? DefaultAction => null;

        /// <summary>Gets the object's help text.</summary>
        public virtual string? Help => null;

        /// <summary>Gets the keyboard shortcut that activates the object.</summary>
        public virtual string? KeyboardShortcut => null;

        /// <summary>Gets the object's current state.</summary>
        public virtual AccessibleStates State => AccessibleStates.None;

        /// <summary>Performs the object's default action.</summary>
        public virtual void DoDefaultAction () { }

        /// <summary>Returns the child object with the keyboard focus, or null.</summary>
        public virtual AccessibleObject? GetFocused () => null;

        /// <summary>Returns the selected child object, or null.</summary>
        public virtual AccessibleObject? GetSelected () => null;

        /// <summary>Returns the object at the given screen point, or null.</summary>
        public virtual AccessibleObject? HitTest (int x, int y)
            => Bounds.Contains (x, y) ? this : null;

        /// <summary>Returns the object in the given direction from this one, or null.</summary>
        public virtual AccessibleObject? Navigate (AccessibleNavigation navdir) => null;

        /// <summary>Selects this object.</summary>
        public virtual void Select (AccessibleSelection flags) { }

        /// <summary>Gets the help topic for this object.</summary>
        /// <remarks>Returns -1 with an empty file name, which is how WinForms reports "no help file";
        /// there is no help subsystem here to name one.</remarks>
        public virtual int GetHelpTopic (out string? fileName)
        {
            fileName = null;
            return -1;
        }

        /// <summary>Asks the accessibility client to announce the given text.</summary>
        /// <remarks>False, and nothing is announced. UI Automation notifications need a platform
        /// automation provider, which the backends do not expose; returning false is how upstream
        /// reports that the notification was not delivered, so a caller that checks behaves
        /// correctly.</remarks>
        public bool RaiseAutomationNotification (AutomationNotificationKind notificationKind,
            AutomationNotificationProcessing notificationProcessing, string? notificationText) => false;

        /// <inheritdoc cref="RaiseAutomationNotification"/>
        public virtual bool RaiseLiveRegionChanged () => false;
    }

    /// <summary>The kind of change a UI Automation notification describes.</summary>
    public enum AutomationNotificationKind
    {
        /// <summary>An item was added.</summary>
        ItemAdded = 0,
        /// <summary>An item was removed.</summary>
        ItemRemoved = 1,
        /// <summary>An action completed.</summary>
        ActionCompleted = 2,
        /// <summary>An action was aborted.</summary>
        ActionAborted = 3,
        /// <summary>Some other change.</summary>
        Other = 4,
    }

    /// <summary>How a UI Automation notification should be delivered.</summary>
    public enum AutomationNotificationProcessing
    {
        /// <summary>Deliver as soon as possible, dropping any pending notification.</summary>
        ImportantAll = 0,
        /// <summary>Deliver the most recent notification only.</summary>
        ImportantMostRecent = 1,
        /// <summary>Deliver all notifications in order.</summary>
        All = 2,
        /// <summary>Deliver the most recent notification.</summary>
        MostRecent = 3,
        /// <summary>Deliver only when nothing else is pending.</summary>
        CurrentThenMostRecent = 4,
    }

    public partial class TreeView
    {
        /// <summary>Gets or sets the border drawn around the control.</summary>
        public BorderStyle BorderStyle { get; set; } = BorderStyle.Fixed3D;

        /// <summary>Gets or sets the colour of the lines joining nodes.</summary>
        public Color LineColor { get; set; } = Color.Empty;

        /// <summary>Gets or sets the image list key for a node's default image.</summary>
        public string ImageKey { get; set; } = string.Empty;

        /// <summary>Gets or sets the image list key for a selected node's image.</summary>
        public string SelectedImageKey { get; set; } = string.Empty;

        /// <summary>Gets or sets whether the control shows scroll bars when it needs them.</summary>
        public bool Scrollable { get; set; } = true;

        /// <summary>Gets or sets whether a node's ToolTipText is shown on hover.</summary>
        public bool ShowNodeToolTips { get; set; }

        /// <summary>Gets or sets whether nodes are kept in alphabetical order.</summary>
        public bool Sorted { get; set; }

        /// <summary>Gets or sets whether the control lays out right to left when RightToLeft is set.</summary>
        public virtual bool RightToLeftLayout {
            get => right_to_left_layout;
            set {
                if (right_to_left_layout == value)
                    return;

                right_to_left_layout = value;
                RightToLeftLayoutChanged?.Invoke (this, EventArgs.Empty);
                Invalidate ();
            }
        }

        private bool right_to_left_layout;

        /// <summary>Raised when <see cref="RightToLeftLayout"/> changes.</summary>
        public event EventHandler? RightToLeftLayoutChanged;

        /// <summary>Gets how many nodes fit in the control's height.</summary>
        public int VisibleCount {
            get {
                var itemHeight = Math.Max (1, ItemHeight);
                return Math.Max (0, ClientRectangle.Height / itemHeight);
            }
        }

        /// <summary>Returns what part of the control is at the given point.</summary>
        public TreeViewHitTestInfo HitTest (int x, int y) => HitTest (new Point (x, y));

        /// <inheritdoc cref="HitTest(int,int)"/>
        public TreeViewHitTestInfo HitTest (Point pt)
        {
            if (!ClientRectangle.Contains (pt))
                return new TreeViewHitTestInfo (null, TreeViewHitTestLocations.None);

            // GetNodeAt returns the library's TreeNode; TreeNode is the WinForms-named subclass
            // that the string-based Nodes.Add overloads produce, so a tree built the WinForms way
            // reports its node and one built through TreeNode reports the location only.
            if (GetNodeAt (pt) is not { } item)
                return new TreeViewHitTestInfo (null, TreeViewHitTestLocations.AboveClientArea);

            var node = item as TreeNode;

            // The indent before a node's label is where the expand glyph and the state image live,
            // which is what lets a caller tell a click on the plus sign from a click on the label.
            var indent = item.Bounds.Left;

            if (pt.X < indent)
                return new TreeViewHitTestInfo (node, TreeViewHitTestLocations.PlusMinus);

            return new TreeViewHitTestInfo (node, TreeViewHitTestLocations.Label);
        }
    }

    /// <summary>Describes what is at a particular point in a <see cref="TreeView"/>.</summary>
    public class TreeViewHitTestInfo
    {
        /// <summary>Initializes a new instance of the <see cref="TreeViewHitTestInfo"/> class.</summary>
        public TreeViewHitTestInfo (TreeNode? hitNode, TreeViewHitTestLocations hitLocation)
        {
            Node = hitNode;
            Location = hitLocation;
        }

        /// <summary>Gets the node at the tested point, or null.</summary>
        public TreeNode? Node { get; }

        /// <summary>Gets what part of the control the point was over.</summary>
        public TreeViewHitTestLocations Location { get; }
    }

    public partial class RichTextBox
    {
        /// <summary>Gets or sets whether a double-click selects a whole word.</summary>
        public bool AutoWordSelection { get; set; }

        /// <summary>Gets or sets whether the control's own keyboard shortcuts are enabled.</summary>
        public bool RichTextShortcutsEnabled { get; set; } = true;

        /// <summary>Gets or sets whether the selection margin is shown.</summary>
        public bool ShowSelectionMargin { get; set; }

        /// <summary>Gets or sets the position of the right margin, in pixels; zero for none.</summary>
        public int RightMargin { get; set; }

        /// <summary>Gets or sets the input-method options for the control.</summary>
        public RichTextBoxLanguageOptions LanguageOption { get; set; }
            = RichTextBoxLanguageOptions.AutoFont | RichTextBoxLanguageOptions.DualFont;

        /// <summary>Gets or sets the tab stops of the selected paragraphs.</summary>
        public int[] SelectionTabs { get; set; } = [];

        /// <summary>Gets or sets the selection as RTF.</summary>
        /// <remarks>Reads and writes plain text: this control stores text, not a rich-text document,
        /// so an RTF round trip would lose the markup it appeared to accept.</remarks>
        public string SelectedRtf {
            get => SelectedText;
            set => SelectedText = value ?? string.Empty;
        }

        /// <summary>Gets what kind of content the selection holds.</summary>
        public RichTextBoxSelectionTypes SelectionType
            => SelectionLength == 0 ? RichTextBoxSelectionTypes.Empty : RichTextBoxSelectionTypes.Text;

        /// <summary>Gets whether the clipboard holds data this control could paste.</summary>
        public bool CanPaste (DataFormats.Format clipFormat)
        {
            Guard.ThrowIfNull (clipFormat);
            return !string.IsNullOrEmpty (Clipboard.GetText ());
        }

        /// <summary>Gets whether there is an edit <see cref="Redo"/> would reapply.</summary>
        /// <remarks>False, with <see cref="RedoActionName"/> empty to match: there is no undo stack in
        /// this layer, and reporting true would enable a Redo menu item that then does nothing.</remarks>
        public bool CanRedo => false;

        /// <summary>Gets the name of the action <see cref="Redo"/> would reapply.</summary>
        public string RedoActionName => string.Empty;

        /// <summary>Gets the name of the action <c>Undo</c> would reverse.</summary>
        public string UndoActionName => string.Empty;

        /// <summary>Reapplies the last undone edit.</summary>
        public void Redo () { }

        // Neither notification has a source: the input-method and protected-range hooks are part of
        // the native rich edit control that this one does not wrap.
#pragma warning disable CS0067
        /// <summary>Raised when the input method changes. Not raised by this layer.</summary>
        public event EventHandler? ImeChange;

        /// <summary>Raised when the user tries to edit protected text. Not raised by this layer.</summary>
        public event EventHandler? Protected;
#pragma warning restore CS0067
    }

    public partial class MaskedTextBox
    {
        /// <summary>Gets or sets whether the prompt character is accepted as input.</summary>
        public bool AllowPromptAsInput { get; set; } = true;

        /// <summary>Gets or sets the culture used to format and parse the value.</summary>
        public IFormatProvider? FormatProvider { get; set; }

        /// <summary>Gets or sets whether typing inserts or overwrites.</summary>
        public InsertKeyMode InsertKeyMode { get; set; } = InsertKeyMode.Default;

        /// <summary>Gets whether typing currently overwrites.</summary>
        /// <remarks>Default follows the keyboard's Insert state, which this layer does not track, so
        /// it resolves to insert -- the state a text box starts in.</remarks>
        public bool IsOverwriteMode => InsertKeyMode == InsertKeyMode.Overwrite;

        /// <summary>Gets or sets whether input stops at the first character the mask rejects.</summary>
        public bool RejectInputOnFirstFailure { get; set; }

        /// <summary>Gets or sets whether typing the prompt character clears that position.</summary>
        public bool ResetOnPrompt { get; set; } = true;

        /// <summary>Gets or sets whether typing a space clears that position.</summary>
        public bool ResetOnSpace { get; set; } = true;

        /// <summary>Gets or sets whether typing a literal moves past it rather than rejecting it.</summary>
        public bool SkipLiterals { get; set; } = true;

        /// <summary>Returns the text converted to <c>ValidatingType</c>, or null when it does not convert.</summary>
        public object? ValidateText ()
        {
            if (ValidatingType is null)
                return null;

            try {
                return Convert.ChangeType (Text, ValidatingType, FormatProvider ?? System.Globalization.CultureInfo.CurrentCulture);
            } catch (Exception ex) when (ex is FormatException or InvalidCastException or OverflowException) {
                // WinForms reports a value that does not convert as null rather than throwing, because
                // the caller's next move is to show a validation message either way.
                return null;
            }
        }

        // Both describe state this layer does not change after construction.
#pragma warning disable CS0067
        /// <summary>Raised when <see cref="IsOverwriteMode"/> changes. Not raised by this layer yet.</summary>
        public event EventHandler? IsOverwriteModeChanged;

        /// <summary>Raised when the mask changes. Not raised by this layer yet.</summary>
        public event EventHandler? MaskChanged;
#pragma warning restore CS0067
    }
}
