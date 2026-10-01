using System.Drawing;

namespace Majorsilence.Forms.Telerik
{
    /// <summary>Telerik-compat button. Backed by <see cref="Majorsilence.Forms.Button"/>.</summary>
    public class RadButton : Button, ISupportInitializeCompat
    {
        /// <summary>Gets the root element of the control (stub).</summary>
        public RadElement RootElement { get; } = new RadElement ();
        /// <summary>Gets or sets the theme name. No-op stub.</summary>
        public string ThemeName { get; set; } = string.Empty;
    }

    /// <summary>Telerik-compat label. Backed by <see cref="Majorsilence.Forms.Label"/>.</summary>
    public class RadLabel : Label, ISupportInitializeCompat
    {
        /// <summary>Gets the root element of the control (stub).</summary>
        public RadElement RootElement { get; } = new RadElement ();
        /// <summary>Gets or sets the theme name. No-op stub.</summary>
        public string ThemeName { get; set; } = string.Empty;
    }

    /// <summary>Telerik-compat link label. Backed by <see cref="Majorsilence.Forms.LinkLabel"/>.</summary>
    public class RadLinkLabel : LinkLabel, ISupportInitializeCompat 
    {
        /// <summary>Gets the root element of the label (stub).</summary>
        public RadElement RootElement { get; } = new RadElement ();
    }

    /// <summary>Telerik-compat text box. Backed by <see cref="Majorsilence.Forms.TextBox"/>.</summary>
    public class RadTextBox : TextBox, ISupportInitializeCompat
    {
        /// <summary>Gets the root element of the control (stub).</summary>
        public RadElement RootElement { get; } = new RadElement ();
        /// <summary>Gets or sets the theme name. No-op stub.</summary>
        public string ThemeName { get; set; } = string.Empty;
    }

    /// <summary>Telerik-compat text box control. Backed by <see cref="Majorsilence.Forms.TextBox"/>.</summary>
    public class RadTextBoxControl : TextBox, ISupportInitializeCompat
    {
        /// <summary>Gets the root element of the control (stub).</summary>
        public RadElement RootElement { get; } = new RadElement ();
    }

    /// <summary>Telerik-compat check box. Backed by <see cref="Majorsilence.Forms.CheckBox"/>.</summary>
    public class RadCheckBox : CheckBox, ISupportInitializeCompat
    {
        /// <summary>Gets the root element of the check box (stub).</summary>
        public RadElement RootElement { get; } = new RadElement ();
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
    public class RadRadioButton : RadioButton, ISupportInitializeCompat
    {
        /// <summary>Gets the root element of the radio button (stub).</summary>
        public RadElement RootElement { get; } = new RadElement ();
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
    public class RadToggleSwitch : CheckBox, ISupportInitializeCompat
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
        /// <remarks>Drawn as of W6 mechanisms (#176): the switch paints as a track and thumb, with this
        /// in the half the thumb has left. It used to paint as a plain check box and show neither text.</remarks>
        public string OnText {
            get => on_text;
            set {
                on_text = value ?? string.Empty;
                Invalidate ();
            }
        }

        private string on_text = "On";

        /// <summary>Gets or sets the text shown in the "off" position.</summary>
        /// <remarks>See <see cref="OnText"/>.</remarks>
        public string OffText {
            get => off_text;
            set {
                off_text = value ?? string.Empty;
                Invalidate ();
            }
        }

        private string off_text = "Off";

        /// <summary>Gets or sets whether a click (press and release) or a press alone toggles the switch.</summary>
        /// <remarks>Real as of W6 mechanisms (#176): with <see cref="Telerik.ToggleStateMode.Press"/> the
        /// switch toggles on mouse down, and the click that follows does not toggle it back.</remarks>
        public ToggleStateMode ToggleStateMode { get; set; } = ToggleStateMode.Click;

        /// <summary>Gets or sets the thumb's width in logical pixels (Telerik spelling preserved); zero for a square thumb.</summary>
        /// <remarks>Real as of W6 mechanisms (#176).</remarks>
        public int ThumbTickness {
            get => thumb_thickness;
            set {
                thumb_thickness = Math.Max (0, value);
                Invalidate ();
            }
        }

        private int thumb_thickness;

        static RadToggleSwitch ()
            => Majorsilence.Forms.Renderers.RenderManager.SetRenderer<RadToggleSwitch> (new Majorsilence.Forms.Renderers.RadToggleSwitchRenderer ());

        // Press mode: the press has toggled, so the click it becomes must not toggle again.
        private bool toggled_on_press;

        /// <inheritdoc/>
        protected override void OnMouseDown (MouseEventArgs e)
        {
            base.OnMouseDown (e);

            if (ToggleStateMode == ToggleStateMode.Press && e.Button == MouseButtons.Left && Enabled && AutoCheck) {
                Checked = !Checked;
                toggled_on_press = true;
            }
        }

        /// <inheritdoc/>
        protected override void OnClick (EventArgs e)
        {
            if (!toggled_on_press) {
                base.OnClick (e);
                return;
            }

            toggled_on_press = false;

            // The click event still happens; only the toggle it would make is skipped.
            var auto = AutoCheck;
            AutoCheck = false;

            try {
                base.OnClick (e);
            } finally {
                AutoCheck = auto;
            }
        }

        /// <summary>The track and thumb rectangles, in device pixels. Shared by the renderer and tests.</summary>
        internal (Rectangle Track, Rectangle Thumb) SwitchGeometry ()
        {
            var client = DeviceClientRectangle;
            var track = new Rectangle (client.X + 1, client.Y + 1, Math.Max (0, client.Width - 2), Math.Max (0, client.Height - 2));
            var inset = Math.Max (1, LogicalToDeviceUnits (2));
            var height = Math.Max (0, track.Height - (2 * inset));
            var width = thumb_thickness > 0 ? Math.Min (LogicalToDeviceUnits (thumb_thickness), track.Width / 2) : height;
            var left = Checked ? track.Right - inset - width : track.Left + inset;

            return (track, new Rectangle (left, track.Top + inset, width, height));
        }
        /// <summary>Gets the root element of the control (stub).</summary>
        public RadElement RootElement { get; } = new RadElement ();

        /// <summary>Raised when the value changes.</summary>
        public event EventHandler? ValueChanged;
    }

    /// <summary>Telerik-compat group box. Backed by <see cref="Majorsilence.Forms.GroupBox"/>.</summary>
    public class RadGroupBox : GroupBox, ISupportInitializeCompat
    {
        /// <summary>Gets the root element of the group box (stub).</summary>
        public RadElement RootElement { get; } = new RadElement ();
        /// <summary>Gets or sets the header text (Telerik alias for <see cref="Control.Text"/>).</summary>
        public string HeaderText {
            get => Text;
            set => Text = value;
        }

        /// <summary>Gets or sets the footer text. Stub (not rendered).</summary>
        public string FooterText { get; set; } = string.Empty;
    }

    /// <summary>Telerik-compat panel. Backed by <see cref="Majorsilence.Forms.Panel"/>.</summary>
    public class RadPanel : Panel, ISupportInitializeCompat
    {
        /// <summary>Gets the root element of the panel (stub).</summary>
        public RadElement RootElement { get; } = new RadElement ();
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
    public class RadWaitingBar : ProgressBar, ISupportInitializeCompat
    {
        /// <summary>Gets the root element of the bar (stub).</summary>
        public RadElement RootElement { get; } = new RadElement ();
        /// <summary>Initializes a new instance of the RadWaitingBar class.</summary>
        public RadWaitingBar ()
        {
            Style = ProgressBarStyle.Marquee;
        }

        /// <summary>Gets whether the bar is animating: true between <see cref="StartWaiting"/> and <see cref="StopWaiting"/>.</summary>
        /// <remarks>Real as of W6 mechanisms (#176): the indicator moves only while this is true, as
        /// Telerik's does. It used to travel from the moment the bar was created, so a bar a form
        /// showed idle looked busy, and StopWaiting did not stop it.</remarks>
        public bool IsWaiting { get; private set; }

        /// <summary>Gets or sets the waiting animation style.</summary>
        /// <remarks>Stored: every style draws as the dash -- a block travelling the track. Telerik's
        /// data cloud, rotating and spinner styles are not drawn.</remarks>
        public WaitingBarStyles WaitingStyle { get; set; } = WaitingBarStyles.Dash;

        /// <summary>Gets or sets the time between animation steps, in ms.</summary>
        public int WaitingSpeed {
            get => waiting_speed;
            set {
                waiting_speed = Math.Max (1, value);

                if (IsWaiting)
                    MarqueeAnimationSpeed = waiting_speed;
            }
        }

        private int waiting_speed = 100;

        /// <summary>Gets or sets how far the indicator moves on each step.</summary>
        /// <remarks>Real as of W6 mechanisms (#176): each step moves it this many of the track's twenty
        /// positions.</remarks>
        public int WaitingStep { get; set; } = 1;

        /// <summary>Gets the waiting indicator elements (designer-populated; count is informational only). Stub.</summary>
        public List<VisualElement> WaitingIndicators { get; } = new ();

        /// <summary>Gets or sets the size of the travelling indicator.</summary>
        /// <remarks>Real as of W6 mechanisms (#176): its width, in logical pixels, is the indicator's
        /// width; empty keeps the default of three tenths of the track.</remarks>
        public Size WaitingIndicatorSize { get; set; }

        /// <summary>Starts the waiting animation.</summary>
        public void StartWaiting ()
        {
            IsWaiting = true;
            MarqueeAnimationSpeed = waiting_speed;
            RefreshMarquee ();
        }

        /// <summary>Stops the waiting animation, leaving the indicator where it is.</summary>
        public void StopWaiting ()
        {
            IsWaiting = false;
            RefreshMarquee ();
        }

        internal override bool MarqueeRuns => IsWaiting;

        internal override int MarqueeStep => Math.Max (1, WaitingStep);

        internal override int MarqueeBlockWidth (int trackWidth)
            => WaitingIndicatorSize.Width > 0 ? LogicalToDeviceUnits (WaitingIndicatorSize.Width) : base.MarqueeBlockWidth (trackWidth);

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

        /// <summary>
        /// Designer code walks the real Telerik element tree with chained <c>GetChildAt</c> calls and
        /// CTypes each level (<c>CType(bar.GetChildAt(0).GetChildAt(0), WaitingBarContentElement)</c>),
        /// so index 0 must be the typed <see cref="ContentElement"/>, not the base's untyped stub.
        /// </summary>
        public override RadElement GetChildAt (int index) => index == 0 ? ContentElement : base.GetChildAt (index);
    }

    /// <summary>Telerik-compat waiting-bar content element (hosts the indicator/separator elements). Designer-only stub.</summary>
    public class WaitingBarContentElement : VisualElement
    {
        /// <summary>Gets or sets the waiting animation style. Stub — mirrors the value on the owning RadWaitingBar.</summary>
        public WaitingBarStyles WaitingStyle { get; set; } = WaitingBarStyles.Dash;

        /// <summary>Gets the separator element (also returned by <c>GetChildAt(0)</c>, matching real
        /// Telerik's tree shape that designer code CTypes against).</summary>
        public WaitingBarSeparatorElement SeparatorElement { get; } = new WaitingBarSeparatorElement ();

        /// <inheritdoc cref="RadWaitingBarElement.GetChildAt"/>
        public override RadElement GetChildAt (int index) => index == 0 ? SeparatorElement : base.GetChildAt (index);
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
    public class RadListControl : ListBox, ISupportInitializeCompat 
    {
        /// <summary>Gets the root element of the list (stub).</summary>
        public RadElement RootElement { get; } = new RadElement ();
    }

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
    public class RadDateTimePicker : DateTimePicker, ISupportInitializeCompat
    {
        /// <summary>Gets the root element of the picker (stub).</summary>
        public RadElement RootElement { get; } = new RadElement ();
        /// <summary>Initializes a new instance of the RadDateTimePicker class.</summary>
        public RadDateTimePicker ()
        {
            ValueChanged += (_, _) => ValueChanging?.Invoke (this, new ValueChangingEventArgs { NewValue = Value });
        }

        /// <summary>Gets the picker element (stub).</summary>
        public RadElement DateTimePickerElement { get; } = new RadElement ();
        /// <summary>Raised before the value changes. Stub (fires alongside ValueChanged).</summary>
        public event EventHandler<ValueChangingEventArgs>? ValueChanging;
        /// <summary>Raised when the drop-down calendar opens.</summary>
        /// <remarks>
        /// An alias for the engine's <see cref="DateTimePicker.DropDown"/>, which became real in
        /// W5.20c. The accessors were previously <c>add { } remove { }</c>, which DISCARDS the
        /// delegate at the add site -- worse than a never-raised event, because even a later <c>-=</c>
        /// is meaningless and nothing can detect the loss at runtime.
        /// </remarks>
        public event EventHandler? Opened {
            add => DropDown += value;
            remove => DropDown -= value;
        }
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
    public class RadTreeView : Majorsilence.Forms.TreeView, ISupportInitializeCompat
    {
        /// <summary>Gets or sets the theme name. No-op stub.</summary>
        public string ThemeName { get; set; } = string.Empty;
        /// <summary>Gets the root element of the control (stub).</summary>
        public RadElement RootElement { get; } = new RadElement ();

        /// <summary>Gets or sets extra vertical space, in logical pixels, added to every node's row.</summary>
        /// <remarks>Real as of W6 mechanisms (#176): the rows are laid out, drawn and scrolled with it.</remarks>
        public int SpacingBetweenNodes {
            get => spacing_between_nodes;
            set {
                value = Math.Max (0, value);

                if (spacing_between_nodes == value)
                    return;

                spacing_between_nodes = value;
                Invalidate ();
            }
        }

        private int spacing_between_nodes;

        internal override int ItemSpacing => spacing_between_nodes;

        // ── data binding (W6 mechanisms, #176) ──────────────────────────────────────────────────────

        /// <summary>Gets or sets the list the tree builds its nodes from.</summary>
        /// <remarks>
        /// Real as of W6 mechanisms (#176). Each item of the list -- a <c>DataTable</c>, a
        /// <c>DataView</c>, a <c>BindingSource</c>, any <c>IListSource</c>, <c>IList</c> or enumerable --
        /// becomes a <see cref="RadTreeNode"/>: its <see cref="DisplayMember"/> is the text, its
        /// <see cref="ValueMember"/> the <see cref="RadTreeNode.Value"/>, and the item itself the
        /// <see cref="RadTreeNode.DataBoundItem"/>. With both <see cref="ChildMember"/> and
        /// <see cref="ParentMember"/> set the list is self-referencing, as Telerik's is: an item whose
        /// <see cref="ParentMember"/> equals another item's <see cref="ChildMember"/> goes under it, and
        /// one whose parent is empty or not in the list is a root. A list that announces its changes
        /// rebuilds the tree when it does. Setting it replaces the nodes already in the tree.
        /// </remarks>
        public object? DataSource {
            get => data_source;
            set {
                if (ReferenceEquals (data_source, value))
                    return;

                if (bound_list is not null)
                    bound_list.ListChanged -= BoundList_ListChanged;

                data_source = value;
                bound_list = null;
                Rebind ();
            }
        }

        private object? data_source;
        private System.ComponentModel.IBindingList? bound_list;

        /// <summary>Gets or sets the member supplying each node's text; the item's own text when empty.</summary>
        public string DisplayMember {
            get => display_member;
            set => SetMember (ref display_member, value);
        }

        /// <summary>Gets or sets the member supplying each item's own key, for a self-referencing list.</summary>
        public string ChildMember {
            get => child_member;
            set => SetMember (ref child_member, value);
        }

        /// <summary>Gets or sets the member supplying each node's value; the item itself when empty.</summary>
        public string ValueMember {
            get => value_member;
            set => SetMember (ref value_member, value);
        }

        /// <summary>Gets or sets the member supplying each item's parent key, for a self-referencing list.</summary>
        public string ParentMember {
            get => parent_member;
            set => SetMember (ref parent_member, value);
        }

        private string display_member = string.Empty;
        private string child_member = string.Empty;
        private string value_member = string.Empty;
        private string parent_member = string.Empty;

        private void SetMember (ref string field, string? value)
        {
            value ??= string.Empty;

            if (field == value)
                return;

            field = value;
            Rebind ();
        }

        private void BoundList_ListChanged (object? sender, System.ComponentModel.ListChangedEventArgs e) => Rebind ();

        private void Rebind ()
        {
            if (data_source is null)
                return;

            var list = ResolveList (data_source);

            if (list is System.ComponentModel.IBindingList notifying && !ReferenceEquals (notifying, bound_list)) {
                if (bound_list is not null)
                    bound_list.ListChanged -= BoundList_ListChanged;

                bound_list = notifying;
                notifying.ListChanged += BoundList_ListChanged;
            }

            var nodes = new List<(object Item, RadTreeNode Node)> ();

            if (list is not null) {
                foreach (var item in list) {
                    if (item is null)
                        continue;

                    var text = display_member.Length > 0 ? Read (item, display_member) : item;
                    var node = new RadTreeNode (Convert.ToString (text, System.Globalization.CultureInfo.CurrentCulture) ?? string.Empty) {
                        Value = value_member.Length > 0 ? Read (item, value_member) : item,
                        DataBoundItem = item,
                    };
                    nodes.Add ((item, node));
                }
            }

            base.Nodes.Clear ();

            var self_referencing = child_member.Length > 0 && parent_member.Length > 0;
            var by_key = new Dictionary<object, RadTreeNode> ();

            if (self_referencing)
                foreach (var (item, node) in nodes)
                    if (Key (Read (item, child_member)) is { } key && !by_key.ContainsKey (key))
                        by_key[key] = node;

            // A child listed before its parent goes under it all the same: the parent node exists
            // already, detached, and is placed in its own turn. The one insertion refused is one whose
            // parent already hangs below the node -- A under B under A -- which would make a loop; that
            // node goes to the root instead, so every node stays reachable.
            foreach (var (item, node) in nodes) {
                var parent = self_referencing && Key (Read (item, parent_member)) is { } parent_key
                    && by_key.TryGetValue (parent_key, out var found) && !CreatesLoop (node, found)
                    ? found
                    : null;

                if (parent is null)
                    base.Nodes.Add (node);
                else
                    parent.Nodes.Add (node);
            }

            Invalidate ();
        }

        // Whether putting node under parent would make node its own ancestor.
        private static bool CreatesLoop (RadTreeNode node, RadTreeNode parent)
        {
            for (TreeNode? ancestor = parent; ancestor is not null; ancestor = ancestor.Parent)
                if (ReferenceEquals (ancestor, node))
                    return true;

            return false;
        }

        // An empty key -- null or a database null -- means "no parent".
        private static object? Key (object? value) => value is null or DBNull ? null : value;

        [System.Diagnostics.CodeAnalysis.UnconditionalSuppressMessage ("Trimming", "IL2026", Justification = "Data binding reads members by name through TypeDescriptor, as Telerik's does.")]
        private static object? Read (object item, string member)
            => System.ComponentModel.TypeDescriptor.GetProperties (item).Find (member, ignoreCase: true)?.GetValue (item);

        private static System.Collections.IEnumerable? ResolveList (object source)
            => source switch {
                System.Data.DataTable table => table.DefaultView,
                System.Data.DataSet set => set.Tables.Count > 0 ? set.Tables[0].DefaultView : null,
                System.ComponentModel.IListSource list_source => list_source.GetList (),
                System.Collections.IEnumerable enumerable when source is not string => enumerable,
                _ => null,
            };

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
            void Visit (System.Collections.Generic.IEnumerable<TreeNode> items)
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

        // W6.1, the Telerik dead-event sweep. The three raisers above were correct and complete, and
        // NOTHING CALLED THEM -- which is why no `add { } remove { }` grep and no single-hop baseline
        // scan ever flagged them: the events look alive from every angle except the one that matters.
        // The engine underneath has had the real hooks since W5.9, so all three are forwards.

        /// <inheritdoc/>
        /// <remarks>
        /// Forwards the engine's cancellable check to <see cref="NodeCheckedChanging"/>. Without this a
        /// consumer's veto handler on a checkbox tree simply did not run, and the check went through --
        /// silently, because a cancelled event and an unwired one look identical from the handler's side.
        /// </remarks>
        protected override bool OnBeforeCheck (TreeViewCancelEventArgs e)
        {
            Guard.ThrowIfNull (e);

            if (!base.OnBeforeCheck (e))
                return false;

            // A tree built through Nodes.Add (string) yields the WinForms-named subclass; one built
            // from plain TreeNodes does not, and a Telerik-typed event cannot describe it.
            if (e.Node is not RadTreeNode node)
                return true;

            return OnNodeCheckedChanging (node);
        }

        /// <inheritdoc/>
        /// <remarks>Forwards the engine's post-check notification to <see cref="NodeCheckedChanged"/>.</remarks>
        protected override void OnAfterCheck (TreeViewEventArgs e)
        {
            base.OnAfterCheck (e);

            Guard.ThrowIfNull (e);

            if (e.Node is RadTreeNode node)
                OnNodeCheckedChanged (node);
        }

        /// <inheritdoc/>
        /// <remarks>
        /// Raises <see cref="NodeFormatting"/> once per node as it is drawn, then writes the element's
        /// appearance back onto the node before the default painting reads it -- the same shape as
        /// <c>RadGridView.RaiseCellFormatting</c>. The element is a carrier: a handler sets properties
        /// on it, and this is what makes those properties mean anything.
        /// </remarks>
        protected internal override void RaiseNodeFormatting (TreeNode item)
        {
            base.RaiseNodeFormatting (item);

            if (NodeFormatting is null || item is not RadTreeNode node)
                return;

            var args = new TreeNodeFormattingEventArgs (node);

            OnNodeFormatting (args);

            // Empty means "the handler did not set this", so the node keeps what it had -- writing
            // Color.Empty back would erase an appearance the application set another way.
            if (args.VisualElement.ForeColor != System.Drawing.Color.Empty)
                node.ForeColor = args.VisualElement.ForeColor;

            if (args.VisualElement.BackColor != System.Drawing.Color.Empty)
                node.BackColor = args.VisualElement.BackColor;

            if (args.VisualElement.Font is { } font)
                node.NodeFont = font;

            if (args.VisualElement.Text.HasValue ())
                node.Text = args.VisualElement.Text;
        }
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

        /// <summary>Gets the list item this node was built from, when the tree is data-bound; null otherwise.</summary>
        public object? DataBoundItem { get; internal set; }

        /// <summary>Gets the parent node, typed as <see cref="RadTreeNode"/> (or null for a root-level or detached node).</summary>
        public new RadTreeNode? Parent => base.Parent as RadTreeNode;

        /// <summary>Gets the child nodes, typed as <see cref="RadTreeNode"/> (Telerik alias for <see cref="Majorsilence.Forms.TreeNode.Nodes"/>).</summary>
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
    public class RadCalendar : MonthCalendar, ISupportInitializeCompat
    {
        /// <summary>Gets or sets the theme name. No-op stub.</summary>
        public string ThemeName { get; set; } = string.Empty;
        /// <summary>Gets the root element of the control (stub).</summary>
        public RadElement RootElement { get; } = new RadElement ();
    }

    /// <summary>Telerik-compat time picker. Backed by <see cref="Majorsilence.Forms.TimePicker"/>.</summary>
    public class RadTimePicker : TimePicker, ISupportInitializeCompat
    {
        /// <summary>Gets or sets the theme name. No-op stub.</summary>
        public string ThemeName { get; set; } = string.Empty;
        /// <summary>Gets the root element of the control (stub).</summary>
        public RadElement RootElement { get; } = new RadElement ();
    }
}
