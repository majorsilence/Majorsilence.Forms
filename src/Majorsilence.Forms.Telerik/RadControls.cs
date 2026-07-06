using System.Drawing;

namespace Majorsilence.Forms.Telerik
{
    /// <summary>Telerik-compat button. Backed by <see cref="Majorsilence.Forms.Button"/>.</summary>
    public class RadButton : Button
    {
        /// <summary>Gets the root element of the control (stub).</summary>
        public RadElement RootElement { get; } = new RadElement ();
        /// <summary>Gets or sets the theme name. No-op stub.</summary>
        public string ThemeName { get; set; } = string.Empty;
    }

    /// <summary>Telerik-compat label. Backed by <see cref="Majorsilence.Forms.Label"/>.</summary>
    public class RadLabel : Label
    {
        /// <summary>Gets the root element of the control (stub).</summary>
        public RadElement RootElement { get; } = new RadElement ();
        /// <summary>Gets or sets the theme name. No-op stub.</summary>
        public string ThemeName { get; set; } = string.Empty;
    }

    /// <summary>Telerik-compat link label. Backed by <see cref="Majorsilence.Forms.LinkLabel"/>.</summary>
    public class RadLinkLabel : LinkLabel { }

    /// <summary>Telerik-compat text box. Backed by <see cref="Majorsilence.Forms.TextBox"/>.</summary>
    public class RadTextBox : TextBox
    {
        /// <summary>Gets the root element of the control (stub).</summary>
        public RadElement RootElement { get; } = new RadElement ();
        /// <summary>Gets or sets the theme name. No-op stub.</summary>
        public string ThemeName { get; set; } = string.Empty;
    }

    /// <summary>Telerik-compat text box control. Backed by <see cref="Majorsilence.Forms.TextBox"/>.</summary>
    public class RadTextBoxControl : TextBox
    {
        /// <summary>Gets the root element of the control (stub).</summary>
        public RadElement RootElement { get; } = new RadElement ();
    }

    /// <summary>Telerik-compat check box. Backed by <see cref="Majorsilence.Forms.CheckBox"/>.</summary>
    public class RadCheckBox : CheckBox
    {
        /// <summary>Initializes a new instance of the RadCheckBox class.</summary>
        public RadCheckBox ()
        {
            CheckedChanged += (_, _) => ToggleStateChanged?.Invoke (this, new StateChangedEventArgs (ToggleState));
        }

        /// <summary>Gets or sets whether the check box is checked (Telerik alias for <see cref="CheckBox.Checked"/>).</summary>
        public bool IsChecked {
            get => Checked;
            set => Checked = value;
        }

        /// <summary>Gets or sets the toggle state.</summary>
        public ToggleState ToggleState {
            get => CheckState switch {
                Majorsilence.Forms.CheckState.Checked => ToggleState.On,
                Majorsilence.Forms.CheckState.Indeterminate => ToggleState.Indeterminate,
                _ => ToggleState.Off
            };
            set => CheckState = value switch {
                ToggleState.On => Majorsilence.Forms.CheckState.Checked,
                ToggleState.Indeterminate => Majorsilence.Forms.CheckState.Indeterminate,
                _ => Majorsilence.Forms.CheckState.Unchecked
            };
        }

        /// <summary>Raised when the toggle state changes.</summary>
        public event EventHandler<StateChangedEventArgs>? ToggleStateChanged;
    }

    /// <summary>Telerik-compat radio button. Backed by <see cref="Majorsilence.Forms.RadioButton"/>.</summary>
    public class RadRadioButton : RadioButton
    {
        /// <summary>Initializes a new instance of the RadRadioButton class.</summary>
        public RadRadioButton ()
        {
            CheckedChanged += (_, _) => ToggleStateChanged?.Invoke (this, new StateChangedEventArgs (Checked ? ToggleState.On : ToggleState.Off));
        }

        /// <summary>Gets or sets whether the radio button is checked (Telerik alias for <see cref="RadioButton.Checked"/>).</summary>
        public bool IsChecked {
            get => Checked;
            set => Checked = value;
        }

        /// <summary>Raised when the toggle state changes.</summary>
        public event EventHandler<StateChangedEventArgs>? ToggleStateChanged;
    }

    /// <summary>Telerik-compat on/off switch. Backed by <see cref="Majorsilence.Forms.CheckBox"/>.</summary>
    public class RadToggleSwitch : CheckBox
    {
        /// <summary>Initializes a new instance of the RadToggleSwitch class.</summary>
        public RadToggleSwitch ()
        {
            CheckedChanged += (_, _) => ValueChanged?.Invoke (this, EventArgs.Empty);
        }

        /// <summary>Gets or sets the switch value (Telerik's primary accessor; maps to <see cref="CheckBox.Checked"/>).</summary>
        public bool Value {
            get => Checked;
            set => Checked = value;
        }

        /// <summary>Gets or sets the text shown in the "on" position.</summary>
        public string OnText { get; set; } = "On";
        /// <summary>Gets or sets the text shown in the "off" position.</summary>
        public string OffText { get; set; } = "Off";
        /// <summary>Gets or sets how the switch changes state.</summary>
        public ToggleStateMode ToggleStateMode { get; set; } = ToggleStateMode.Click;
        /// <summary>Gets or sets the thumb thickness (Telerik spelling preserved). Stub.</summary>
        public int ThumbTickness { get; set; }
        /// <summary>Gets the root element of the control (stub).</summary>
        public RadElement RootElement { get; } = new RadElement ();

        /// <summary>Raised when the value changes.</summary>
        public event EventHandler? ValueChanged;
    }

    /// <summary>Telerik-compat group box. Backed by <see cref="Majorsilence.Forms.GroupBox"/>.</summary>
    public class RadGroupBox : GroupBox
    {
        /// <summary>Gets or sets the header text (Telerik alias for <see cref="Control.Text"/>).</summary>
        public string HeaderText {
            get => Text;
            set => Text = value;
        }

        /// <summary>Gets or sets the footer text. Stub (not rendered).</summary>
        public string FooterText { get; set; } = string.Empty;
    }

    /// <summary>Telerik-compat panel. Backed by <see cref="Majorsilence.Forms.Panel"/>.</summary>
    public class RadPanel : Panel
    {
        /// <summary>Gets or sets the header text. Stub.</summary>
        public new string Text { get; set; } = string.Empty;
    }

    /// <summary>Telerik-compat form. Backed by <see cref="Majorsilence.Forms.Form"/>.</summary>
    public class RadForm : Form
    {
        /// <summary>Gets or sets the theme name. No-op stub.</summary>
        public string ThemeName { get; set; } = string.Empty;
        /// <summary>Gets or sets the icon scaling mode. Stub.</summary>
        public object? IconScaling { get; set; }
        /// <summary>Gets the root element of the form (stub).</summary>
        public RadElement RootElement { get; } = new RadElement ();

        /// <summary>Raised when the theme changes. Stub.</summary>
        protected virtual void OnThemeChanged () { }
    }

    /// <summary>Telerik-compat ribbon form. Backed by <see cref="Majorsilence.Forms.Form"/>.</summary>
    public class RadRibbonForm : RadForm { }

    /// <summary>Telerik-compat indeterminate progress / waiting indicator. Backed by <see cref="Majorsilence.Forms.ProgressBar"/>.</summary>
    public class RadWaitingBar : ProgressBar
    {
        /// <summary>Initializes a new instance of the RadWaitingBar class.</summary>
        public RadWaitingBar ()
        {
            Style = ProgressBarStyle.Marquee;
        }

        /// <summary>Gets whether the bar is currently animating.</summary>
        public bool IsWaiting { get; private set; }
        /// <summary>Gets or sets the waiting animation style. Stub.</summary>
        public WaitingBarStyles WaitingStyle { get; set; } = WaitingBarStyles.Dash;
        /// <summary>Gets or sets the animation speed, in ms.</summary>
        public int WaitingSpeed { get; set; } = 100;
        /// <summary>Gets or sets the animation step. Stub.</summary>
        public int WaitingStep { get; set; } = 1;
        /// <summary>Gets the waiting indicator elements (designer-populated; count is informational only). Stub.</summary>
        public List<VisualElement> WaitingIndicators { get; } = new ();
        /// <summary>Gets or sets the size of each indicator. Stub.</summary>
        public Size WaitingIndicatorSize { get; set; }

        /// <summary>Starts the waiting animation.</summary>
        public void StartWaiting ()
        {
            IsWaiting = true;
            MarqueeAnimationSpeed = WaitingSpeed;
        }

        /// <summary>Stops the waiting animation.</summary>
        public void StopWaiting () => IsWaiting = false;

        /// <summary>Gets the waiting bar's element tree root (stub).</summary>
        public RadWaitingBarElement WaitingBarElement { get; } = new RadWaitingBarElement ();

        /// <summary>Returns the child element at the given index. Index 0 is <see cref="WaitingBarElement"/> (stub).</summary>
        public RadElement GetChildAt (int index) => index == 0 ? WaitingBarElement : new RadElement ();
    }

    /// <summary>Telerik-compat root element of a <see cref="RadWaitingBar"/>'s element tree. Designer-only stub.</summary>
    public class RadWaitingBarElement : VisualElement
    {
        /// <summary>Gets the content element hosting the waiting indicators.</summary>
        public WaitingBarContentElement ContentElement { get; } = new WaitingBarContentElement ();
        /// <summary>Gets or sets the animation speed, in ms. Stub — mirrors the value on the owning RadWaitingBar.</summary>
        public int WaitingSpeed { get; set; } = 100;
    }

    /// <summary>Telerik-compat waiting-bar content element (hosts the indicator/separator elements). Designer-only stub.</summary>
    public class WaitingBarContentElement : VisualElement
    {
        /// <summary>Gets or sets the waiting animation style. Stub — mirrors the value on the owning RadWaitingBar.</summary>
        public WaitingBarStyles WaitingStyle { get; set; } = WaitingBarStyles.Dash;
    }

    /// <summary>Telerik-compat waiting-bar separator element. Designer-only stub.</summary>
    public class WaitingBarSeparatorElement : VisualElement
    {
        /// <summary>Gets or sets whether a dash separator is drawn. Stub.</summary>
        public bool Dash { get; set; }
    }

    /// <summary>Telerik-compat "dots" waiting-bar indicator element. Designer-only stub.</summary>
    public class DotsSpinnerWaitingBarIndicatorElement : VisualElement { }

    /// <summary>Telerik-compat "segmented ring" waiting-bar indicator element. Designer-only stub.</summary>
    public class SegmentedRingWaitingBarIndicatorElement : VisualElement { }

    /// <summary>Telerik-compat list control. Backed by <see cref="Majorsilence.Forms.ListBox"/>.</summary>
    public class RadListControl : ListBox { }

    /// <summary>Telerik-compat list data item.</summary>
    public class RadListDataItem
    {
        /// <summary>The underlying data object this item represents.</summary>
        public object? DataBoundItem { get; set; }

        /// <summary>Initializes a new, empty instance.</summary>
        public RadListDataItem () { }
        /// <summary>Initializes a new instance with the specified text.</summary>
        public RadListDataItem (string text) { Text = text; }
        /// <summary>Initializes a new instance with the specified text and value.</summary>
        public RadListDataItem (string text, object? value) { Text = text; Value = value; }

        /// <summary>Gets or sets the display text.</summary>
        public string Text { get; set; } = string.Empty;
        /// <summary>Gets or sets the value.</summary>
        public object? Value { get; set; }
        /// <summary>Gets or sets whether the item is checked.</summary>
        public bool Checked { get; set; }
        /// <summary>Gets or sets the item tag.</summary>
        public object? Tag { get; set; }

        /// <inheritdoc/>
        public override string ToString () => Text;
    }

    /// <summary>Telerik-compat date/time picker. Backed by <see cref="Majorsilence.Forms.DateTimePicker"/>.</summary>
    public class RadDateTimePicker : DateTimePicker
    {
        /// <summary>Initializes a new instance of the RadDateTimePicker class.</summary>
        public RadDateTimePicker ()
        {
            ValueChanged += (_, _) => ValueChanging?.Invoke (this, new ValueChangingEventArgs { NewValue = Value });
        }

        /// <summary>Gets the picker element (stub).</summary>
        public RadElement DateTimePickerElement { get; } = new RadElement ();
        /// <summary>Raised before the value changes. Stub (fires alongside ValueChanged).</summary>
        public event EventHandler<ValueChangingEventArgs>? ValueChanging;
        /// <summary>Raised when the drop-down calendar opens. Stub.</summary>
        public event EventHandler? Opened { add { } remove { } }
    }

    /// <summary>Provides data for a Telerik toggle state change.</summary>
    public class StateChangedEventArgs : EventArgs
    {
        /// <summary>Initializes a new instance with the specified toggle state.</summary>
        public StateChangedEventArgs (ToggleState toggleState) => ToggleState = toggleState;
        /// <summary>Gets the new toggle state.</summary>
        public ToggleState ToggleState { get; }
    }

    /// <summary>
    /// Telerik-compat tree view. Backed by <see cref="Majorsilence.Forms.TreeView"/>, which already supplies
    /// <c>Nodes</c>/<c>SelectedNode</c>/<c>GetNodeAt</c>/<c>CheckBoxes</c> as WinForms-compat aliases; this
    /// type layers the Telerik-specific data-binding members, node type (<see cref="RadTreeNode"/>), and
    /// formatting/check events on top.
    /// </summary>
    public class RadTreeView : Majorsilence.Forms.TreeView
    {
        /// <summary>Gets or sets the theme name. No-op stub.</summary>
        public string ThemeName { get; set; } = string.Empty;
        /// <summary>Gets the root element of the control (stub).</summary>
        public RadElement RootElement { get; } = new RadElement ();

        /// <summary>Gets or sets the data source used for hierarchical (self-referencing) data binding. Stub (binding is not performed; add <see cref="RadTreeNode"/>s to <see cref="Nodes"/> directly).</summary>
        public object? DataSource { get; set; }
        /// <summary>Gets or sets the member supplying each node's display text, when data-bound. Stub.</summary>
        public string DisplayMember { get; set; } = string.Empty;
        /// <summary>Gets or sets the member supplying each node's own key, when data-bound. Stub.</summary>
        public string ChildMember { get; set; } = string.Empty;
        /// <summary>Gets or sets the member supplying each node's value, when data-bound. Stub.</summary>
        public string ValueMember { get; set; } = string.Empty;
        /// <summary>Gets or sets the member supplying each node's parent key, when data-bound. Stub.</summary>
        public string ParentMember { get; set; } = string.Empty;

        /// <summary>Gets the root-level nodes, typed as <see cref="RadTreeNode"/> (Telerik alias for <see cref="Majorsilence.Forms.TreeView.Nodes"/>).</summary>
        public new RadTreeNodeCollection Nodes => new RadTreeNodeCollection (base.Nodes);

        /// <summary>Gets or sets the selected node, typed as <see cref="RadTreeNode"/>.</summary>
        public new RadTreeNode? SelectedNode {
            get => base.SelectedNode as RadTreeNode;
            set => base.SelectedNode = value;
        }

        /// <summary>Returns all nodes (at any depth) matching the specified predicate.</summary>
        public RadTreeNode[] FindNodes (Predicate<RadTreeNode> match)
        {
            var result = new List<RadTreeNode> ();
            void Visit (System.Collections.Generic.IEnumerable<TreeViewItem> items)
            {
                foreach (var item in items) {
                    if (item is RadTreeNode node && match (node))
                        result.Add (node);
                    Visit (item.Items);
                }
            }
            Visit (base.Nodes);
            return result.ToArray ();
        }

        /// <summary>Raised when a node is being formatted. Set element appearance from <c>e.Node</c>/<c>e.VisualElement</c>.</summary>
        public event EventHandler<TreeNodeFormattingEventArgs>? NodeFormatting;

        /// <summary>Raised after a node's checked state changes.</summary>
        public event EventHandler<TreeNodeCheckedEventArgs>? NodeCheckedChanged;

        /// <summary>Raised before a node's checked state changes. Set <c>e.Cancel</c> to veto.</summary>
        public event EventHandler<RadTreeViewCancelEventArgs>? NodeCheckedChanging;

        /// <summary>Raises <see cref="NodeFormatting"/> for the specified node.</summary>
        protected internal virtual void OnNodeFormatting (TreeNodeFormattingEventArgs e) => NodeFormatting?.Invoke (this, e);

        /// <summary>Raises <see cref="NodeCheckedChanging"/> for the specified node. Returns true if the change should proceed (was not cancelled).</summary>
        protected internal virtual bool OnNodeCheckedChanging (RadTreeNode node)
        {
            var e = new RadTreeViewCancelEventArgs (node);
            NodeCheckedChanging?.Invoke (this, e);
            return !e.Cancel;
        }

        /// <summary>Raises <see cref="NodeCheckedChanged"/> for the specified node.</summary>
        protected internal virtual void OnNodeCheckedChanged (RadTreeNode node) => NodeCheckedChanged?.Invoke (this, new TreeNodeCheckedEventArgs (node));
    }

    /// <summary>Telerik-compat tree node. Backed by <see cref="Majorsilence.Forms.TreeNode"/>.</summary>
    public class RadTreeNode : Majorsilence.Forms.TreeNode
    {
        /// <summary>Initializes a new instance of the RadTreeNode class.</summary>
        public RadTreeNode () { }
        /// <summary>Initializes a new instance of the RadTreeNode class with the specified text.</summary>
        public RadTreeNode (string text) : base (text) { }

        /// <summary>Gets or sets the value associated with this node (from data binding, or set directly).</summary>
        public object? Value { get; set; }

        /// <summary>Gets the parent node, typed as <see cref="RadTreeNode"/> (or null for a root-level or detached node).</summary>
        public new RadTreeNode? Parent => base.Parent as RadTreeNode;

        /// <summary>Gets the child nodes, typed as <see cref="RadTreeNode"/> (Telerik alias for <see cref="Majorsilence.Forms.TreeViewItem.Nodes"/>).</summary>
        public new RadTreeNodeCollection Nodes => new RadTreeNodeCollection (base.Nodes);
    }

    /// <summary>
    /// Telerik-compat typed view over a <see cref="TreeViewItemCollection"/>, yielding/adding <see cref="RadTreeNode"/>s.
    /// </summary>
    public class RadTreeNodeCollection : System.Collections.Generic.IEnumerable<RadTreeNode>
    {
        private readonly TreeViewItemCollection _items;

        internal RadTreeNodeCollection (TreeViewItemCollection items) => _items = items;

        /// <summary>Gets the number of nodes in the collection.</summary>
        public int Count => _items.Count;

        /// <summary>Gets the node at the specified index, typed as <see cref="RadTreeNode"/> (or null if the item at that index isn't one).</summary>
        public RadTreeNode? this[int index] => _items[index] as RadTreeNode;

        /// <summary>Adds the specified node.</summary>
        public RadTreeNode Add (RadTreeNode node) { _items.Add (node); return node; }

        /// <summary>Adds a new node with the specified text.</summary>
        public RadTreeNode Add (string text) { var node = new RadTreeNode (text); _items.Add (node); return node; }

        /// <summary>Removes all nodes.</summary>
        public void Clear () => _items.Clear ();

        /// <inheritdoc/>
        public System.Collections.Generic.IEnumerator<RadTreeNode> GetEnumerator ()
        {
            foreach (var item in _items)
                if (item is RadTreeNode node)
                    yield return node;
        }

        System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator () => GetEnumerator ();
    }

    /// <summary>Provides data for the <see cref="RadTreeView.NodeFormatting"/> event.</summary>
    public class TreeNodeFormattingEventArgs : EventArgs
    {
        /// <summary>Initializes a new instance for the specified node.</summary>
        public TreeNodeFormattingEventArgs (RadTreeNode node) => Node = node;
        /// <summary>Gets the node being formatted.</summary>
        public RadTreeNode Node { get; }
        /// <summary>Gets the visual element for the node (stub).</summary>
        public RadItem VisualElement { get; } = new RadLabelElement ();
    }

    /// <summary>Provides data for a Telerik tree-view checked-state-changed event.</summary>
    public class TreeNodeCheckedEventArgs : EventArgs
    {
        /// <summary>Initializes a new instance for the specified node.</summary>
        public TreeNodeCheckedEventArgs (RadTreeNode node) => Node = node;
        /// <summary>Gets the node whose checked state changed.</summary>
        public RadTreeNode Node { get; }
    }

    /// <summary>Provides data for a general (non-cancelable) Telerik tree-view event.</summary>
    public class RadTreeViewEventArgs : EventArgs
    {
        /// <summary>Initializes a new instance for the specified node.</summary>
        public RadTreeViewEventArgs (RadTreeNode node) => Node = node;
        /// <summary>Gets the affected node.</summary>
        public RadTreeNode Node { get; }
    }

    /// <summary>Provides data for a cancelable Telerik tree-view event (e.g. NodeCheckedChanging).</summary>
    public class RadTreeViewCancelEventArgs : System.ComponentModel.CancelEventArgs
    {
        /// <summary>Initializes a new instance for the specified node.</summary>
        public RadTreeViewCancelEventArgs (RadTreeNode node) => Node = node;
        /// <summary>Gets the affected node.</summary>
        public RadTreeNode Node { get; }
    }

    /// <summary>Telerik-compat calendar. Backed by <see cref="Majorsilence.Forms.MonthCalendar"/>.</summary>
    public class RadCalendar : MonthCalendar
    {
        /// <summary>Gets or sets the theme name. No-op stub.</summary>
        public string ThemeName { get; set; } = string.Empty;
        /// <summary>Gets the root element of the control (stub).</summary>
        public RadElement RootElement { get; } = new RadElement ();
    }

    /// <summary>Telerik-compat time picker. Backed by <see cref="Majorsilence.Forms.TimePicker"/>.</summary>
    public class RadTimePicker : TimePicker
    {
        /// <summary>Gets or sets the theme name. No-op stub.</summary>
        public string ThemeName { get; set; } = string.Empty;
        /// <summary>Gets the root element of the control (stub).</summary>
        public RadElement RootElement { get; } = new RadElement ();
    }
}
