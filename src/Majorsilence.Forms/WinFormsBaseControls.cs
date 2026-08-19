using System;
using System.ComponentModel;
using System.Drawing;

namespace Majorsilence.Forms
{
    // The intermediate base classes WinForms puts between Control and its concrete controls
    // (docs/winforms-gap-plan.md, item 3).
    //
    // These matter in a way member-level work cannot reach: migrated code routinely writes
    //     class MyButton : ButtonBase
    //     if (control is ListControl list) ...
    //     foreach (ButtonBase b in panel.Controls.OfType<ButtonBase> ())
    // and none of that compiles — or, worse for the type tests, silently matches nothing — unless the
    // base type genuinely sits in the hierarchy. Adding them is therefore a reparenting job, not just
    // a new file: Button/CheckBox/RadioButton now derive from ButtonBase, ListBox/ComboBox from
    // ListControl, and so on.
    //
    // Each declares the surface upstream declares on it, so a member reached through the base resolves
    // the same way it would in WinForms.

    /// <summary>
    /// Base class for controls that behave like buttons — <see cref="Button"/>, <see cref="CheckBox"/>
    /// and <see cref="RadioButton"/>.
    /// </summary>
    public abstract partial class ButtonBase : Control
    {
        /// <summary>Gets or sets whether an ellipsis is shown when the text overflows.</summary>
        public virtual bool AutoEllipsis { get; set; }

        /// <summary>Gets or sets the flat-style appearance of this control.</summary>
        public virtual FlatStyle FlatStyle { get; set; } = FlatStyle.Standard;

        /// <summary>Gets the appearance settings used when <see cref="FlatStyle"/> is Flat.</summary>
        public virtual FlatButtonAppearance FlatAppearance { get; } = new ();

        /// <summary>Gets or sets the alignment of the text on this control.</summary>
        public virtual ContentAlignment TextAlign { get; set; } = ContentAlignment.MiddleCenter;

        /// <summary>Gets or sets the relative placement of the image and text.</summary>
        public virtual TextImageRelation TextImageRelation { get; set; } = TextImageRelation.Overlay;

        /// <summary>Gets or sets whether the first character preceded by an ampersand is an access key.</summary>
        public bool UseMnemonic { get; set; } = true;

        /// <summary>
        /// Gets or sets whether text is rendered with the compatibility renderer. Stored and
        /// round-tripped: all text here goes through the same SkiaSharp path either way.
        /// </summary>
        public virtual bool UseCompatibleTextRendering { get; set; }
    }

    /// <summary>
    /// Base class for controls that present a list of items bound to a data source —
    /// <see cref="ListBox"/> and <see cref="ComboBox"/>.
    /// </summary>
    public abstract partial class ListControl : Control
    {
        private object? dataSource;
        private string displayMember = string.Empty;
        private string valueMember = string.Empty;

        /// <summary>Occurs when the <see cref="DataSource"/> changes.</summary>
        public event EventHandler? DataSourceChanged;

        /// <summary>Occurs when the <see cref="DisplayMember"/> changes.</summary>
        public event EventHandler? DisplayMemberChanged;

        /// <summary>Occurs when the <see cref="ValueMember"/> changes.</summary>
        public event EventHandler? ValueMemberChanged;

#pragma warning disable CS0067 // Declared so handlers compile and can subscribe; nothing raises these
                               // yet -- the documented stub shape, see COMPATIBILITY_MATRIX.md.
        /// <summary>Occurs when <see cref="FormattingEnabled"/> changes.</summary>
        public event EventHandler? FormattingEnabledChanged;

        /// <summary>Occurs when <see cref="FormatInfo"/> changes.</summary>
        public event EventHandler? FormatInfoChanged;

        /// <summary>Occurs when <see cref="FormatString"/> changes.</summary>
        public event EventHandler? FormatStringChanged;
#pragma warning restore CS0067

        /// <summary>Occurs when an item's display text is being formatted.</summary>
        public event ListControlConvertEventHandler? Format;

        /// <summary>Gets or sets the data source this control presents.</summary>
        public virtual object? DataSource {
            get => dataSource;
            set {
                if (ReferenceEquals (dataSource, value))
                    return;
                dataSource = value;
                OnDataSourceChanged (EventArgs.Empty);
            }
        }

        /// <summary>Gets or sets the property of the data source used as each item's display text.</summary>
        public virtual string DisplayMember {
            get => displayMember;
            set {
                if (string.Equals (displayMember, value, StringComparison.Ordinal))
                    return;
                displayMember = value ?? string.Empty;
                DisplayMemberChanged?.Invoke (this, EventArgs.Empty);
            }
        }

        /// <summary>Gets or sets the property of the data source used as each item's value.</summary>
        public virtual string ValueMember {
            get => valueMember;
            set {
                if (string.Equals (valueMember, value, StringComparison.Ordinal))
                    return;
                valueMember = value ?? string.Empty;
                ValueMemberChanged?.Invoke (this, EventArgs.Empty);
            }
        }

        /// <summary>Gets or sets the index of the selected item.</summary>
        public abstract int SelectedIndex { get; set; }

        /// <summary>Gets or sets the value of the selected item, taken from <see cref="ValueMember"/>.</summary>
        public virtual object? SelectedValue { get; set; }

        /// <summary>Gets or sets whether item text is formatted for display.</summary>
        public virtual bool FormattingEnabled { get; set; }

        /// <summary>Gets or sets the format string applied to item text.</summary>
        public string FormatString { get; set; } = string.Empty;

        /// <summary>Gets or sets the format provider applied to item text.</summary>
        public IFormatProvider? FormatInfo { get; set; }

        /// <summary>
        /// Returns the text to display for an item, honoring <see cref="DisplayMember"/> when the item
        /// exposes that property.
        /// </summary>
        [System.Diagnostics.CodeAnalysis.UnconditionalSuppressMessage ("Trimming", "IL2075",
            Justification = "DisplayMember names a property on a caller-supplied item type; the caller " +
                            "is responsible for keeping it, exactly as WinForms data binding requires.")]
        public virtual string GetItemText (object? item)
        {
            if (item is null)
                return string.Empty;
            if (string.IsNullOrEmpty (DisplayMember))
                return item.ToString () ?? string.Empty;

            var property = item.GetType ().GetProperty (DisplayMember);
            return property?.GetValue (item)?.ToString () ?? item.ToString () ?? string.Empty;
        }

        /// <summary>Raises the <see cref="DataSourceChanged"/> event.</summary>
        protected virtual void OnDataSourceChanged (EventArgs e) => DataSourceChanged?.Invoke (this, e);

        /// <summary>Raises the <see cref="Format"/> event.</summary>
        protected virtual void OnFormat (ListControlConvertEventArgs e) => Format?.Invoke (this, e);
    }

    /// <summary>
    /// Base class for the spinner controls — <see cref="NumericUpDown"/> and
    /// <see cref="DomainUpDown"/>.
    /// </summary>
    public abstract partial class UpDownBase : ContainerControl
    {
        /// <summary>Gets or sets whether the up/down arrow keys change the value.</summary>
        public bool InterceptArrowKeys { get; set; } = true;

        /// <summary>Gets or sets whether the text can be edited directly.</summary>
        public bool ReadOnly { get; set; }

        /// <summary>Gets or sets the alignment of the text within the control.</summary>
        public HorizontalAlignment TextAlign { get; set; } = HorizontalAlignment.Left;

        /// <summary>Gets or sets which side the spin buttons appear on.</summary>
        public LeftRightAlignment UpDownAlign { get; set; } = LeftRightAlignment.Right;

        /// <summary>Gets the height this control prefers, based on its font.</summary>
        public int PreferredHeight => Font is null ? 20 : (int)Math.Ceiling (Font.Size * 2.2f);

        /// <summary>Increments the value.</summary>
        public abstract void UpButton ();

        /// <summary>Decrements the value.</summary>
        public abstract void DownButton ();
    }

    /// <summary>
    /// Base class for toolbar and menu items that host a drop-down — <see cref="ToolStripMenuItem"/>,
    /// <see cref="ToolStripDropDownButton"/> and <see cref="ToolStripSplitButton"/>.
    /// </summary>
    public abstract partial class ToolStripDropDownItem : ToolStripItem
    {
        private ToolStripDropDown? dropDown;

        /// <summary>Occurs when the drop-down has opened.</summary>
        public event EventHandler? DropDownOpened;

        /// <summary>Occurs before the drop-down opens.</summary>
        public event EventHandler? DropDownOpening;

        /// <summary>Occurs when the drop-down has closed.</summary>
        public event EventHandler? DropDownClosed;

        /// <summary>Occurs when an item in the drop-down is clicked.</summary>
        public event ToolStripItemClickedEventHandler? DropDownItemClicked;

        /// <summary>Gets or sets the drop-down shown by this item, creating one on first access.</summary>
        /// <remarks>
        /// Created with <c>OwnerItem = this</c>, which is the whole trick: an owned
        /// <see cref="ToolStripDropDown"/> is a view onto its item -- its <c>Items</c> ARE the item's own
        /// <see cref="MenuItem.Items"/>, and its <c>Visible</c>/<c>Close</c> forward to the item's real
        /// open/close. It used to be created bare, so a menu built through
        /// <see cref="DropDownItems"/> went into a collection the strip never rendered, and
        /// <c>DropDown.Close(...)</c> closed an orphan while the shown menu stayed open -- both silently.
        /// (<see cref="ToolStripMenuItem"/> always wired the owner; this moves that wiring to the base so
        /// <see cref="ToolStripDropDownButton"/> and <see cref="ToolStripSplitButton"/> get it too.)
        /// </remarks>
        public virtual ToolStripDropDown DropDown {
            get => dropDown ??= CreateDefaultDropDown ();
            set => dropDown = value;
        }

        /// <summary>Gets the items in this item's drop-down.</summary>
        /// <remarks>
        /// The item's own <see cref="MenuItem.Items"/> -- the same collection <see cref="DropDown"/>'s
        /// <c>Items</c> resolves to, as in WinForms, and returned directly so reading it does not force
        /// the drop-down control into existence.
        /// </remarks>
        public virtual MenuItemCollection DropDownItems => Items;

        /// <summary>Gets or sets the direction the drop-down opens in.</summary>
        public ToolStripDropDownDirection DropDownDirection { get; set; } = ToolStripDropDownDirection.Default;

        /// <summary>Gets whether a drop-down has been created for this item.</summary>
        public bool HasDropDown => dropDown is not null;

        /// <summary>Gets whether this item's drop-down contains any items.</summary>
        /// <remarks>Answers from the item's own items: it used to answer from the lazily-created
        /// drop-down, so an item with sub-items reported false until something touched
        /// <see cref="DropDown"/>.</remarks>
        public virtual bool HasDropDownItems => Items.Count > 0;

        /// <summary>Gets whether the drop-down is currently shown.</summary>
        public override bool Pressed => IsDropDownOpened;

        /// <summary>Shows this item's drop-down.</summary>
        /// <remarks>Routes through <see cref="MenuItem.ShowDropDown"/> -- the same native open the strip
        /// uses for a click -- rather than showing the drop-down control as a free-standing popup. An
        /// OVERRIDE, not a `new`: every caller that opens a menu holds the item as a
        /// <see cref="MenuItem"/>, so hiding the base method meant a click opened the menu without raising
        /// either event or consulting the cancellable Opening.</remarks>
        public override void ShowDropDown ()
        {
            // Opening is the cancellable point: a drop-down that populates itself lazily builds its
            // items there and cancels when there is nothing to show, so the open must really be
            // abandoned rather than merely notified.
            if (HasDropDown && DropDown.RaiseOpeningCancelled ())
                return;

            OnDropDownShow (EventArgs.Empty);
            base.ShowDropDown ();
            OnDropDownOpened (EventArgs.Empty);
        }

        /// <summary>Hides this item's drop-down.</summary>
        /// <remarks>An override rather than a `new`, for the reason given on <see cref="ShowDropDown"/>.</remarks>
        public override void HideDropDown ()
        {
            if (!IsDropDownOpened)
                return;
            base.HideDropDown ();
            OnDropDownHide (EventArgs.Empty);
        }

        /// <summary>Raises the <see cref="DropDownOpening"/> event.</summary>
        protected virtual void OnDropDownShow (EventArgs e) => DropDownOpening?.Invoke (this, e);

        /// <summary>Raises the <see cref="DropDownOpened"/> event.</summary>
        protected virtual void OnDropDownOpened (EventArgs e) => DropDownOpened?.Invoke (this, e);

        /// <summary>Raises the <see cref="DropDownClosed"/> event.</summary>
        /// <remarks>Defers to <see cref="OnDropDownClosed"/>, the name WinForms uses, so overriding
        /// either one catches the close.</remarks>
        protected virtual void OnDropDownHide (EventArgs e) => OnDropDownClosed (e);

        // The actual raise, reached from OnDropDownClosed. Separate because that override lives in
        // another partial and cannot see the event's backing field through a virtual call.
        private protected void RaiseDropDownClosed (EventArgs e) => DropDownClosed?.Invoke (this, e);

        /// <summary>Raises the <see cref="DropDownItemClicked"/> event.</summary>
        protected virtual void OnDropDownItemClicked (ToolStripItemClickedEventArgs e)
            => DropDownItemClicked?.Invoke (this, e);
    }
}
