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

        // The same bearing allowance Label makes: GDI text carries roughly two pixels of side bearing
        // that the renderer reproduces, and a preferred width that does not leave room for it clips the
        // caption's last glyph.
        private const int TextBearingInset = 2;

        /// <summary>
        /// Measures what this button wants to be: its caption under the font it will actually be drawn
        /// with, plus the image, the check/radio glyph, padding and the border.
        /// </summary>
        /// <remarks>
        /// <para>
        /// There was no override here at all until 2026-08-31 (finding <c>LAY-34</c>), so
        /// <see cref="Control.GetPreferredSizeCore"/> answered with the size the designer last set --
        /// which made <c>AutoSize = true</c> on a button do nothing at all, silently, while reading
        /// back <c>true</c>. That is the standard way to make a localised caption fit, and the same
        /// wrong size then propagated up through any <c>AutoSize</c> <see cref="FlowLayoutPanel"/> or
        /// <see cref="TableLayoutPanel"/> the button sat in.
        /// </para>
        /// <para>
        /// Every quantity here is taken from what the renderer draws rather than from a table of
        /// numbers: the font from <c>GetEffectiveFont</c>, the image gap from the renderer's
        /// <c>ImageTextMargin</c>, the glyph box and its gap from the renderer's <c>GlyphSize</c> and
        /// <c>GlyphTextPadding</c>, and the border from the style. Measuring with anything else is how
        /// a control ends up sized for text it is not showing -- the failure W5.17 catalogues.
        /// </para>
        /// <para>
        /// One simplification: the glyph is measured as a column beside the text, which is what
        /// <c>GlyphAlign</c>'s default (and near-universal) left/right alignment produces. A top- or
        /// bottom-centred glyph would want that allowance on the vertical axis instead.
        /// </para>
        /// </remarks>
        internal override Size GetPreferredSizeCore (Size proposedSize)
        {
            var text = Text;
            var image = (this as IHaveTextAndImageAlign).GetImage ();
            var glyph = GlyphAllowance ();

            if (!text.HasValue () && image is null && glyph.IsEmpty)
                return base.GetPreferredSizeCore (proposedSize);

            var width = 0;
            var height = 0;

            if (text.HasValue ()) {
                var constraint = proposedSize.Width > 0 && proposedSize.Width < int.MaxValue
                    ? new Size (proposedSize.Width, int.MaxValue)
                    : TextMeasurer.MaxSize;

                var measured = TextMeasurer.MeasureText (
                    text, GetEffectiveFont (), GetEffectiveFontSize (), constraint);

                width = (int)Math.Ceiling (measured.Width) + TextBearingInset * 2;
                height = (int)Math.Ceiling (measured.Height);
            }

            if (image is not null) {
                var gap = Renderers.RenderManager.GetRenderer<Renderers.Renderer> (this)
                    is Renderers.IRenderTextAndImage textAndImage ? textAndImage.ImageTextMargin : 0;

                switch (TextImageRelation) {
                    case TextImageRelation.ImageBeforeText:
                    case TextImageRelation.TextBeforeImage:
                        width += image.Width + gap;
                        height = Math.Max (height, image.Height);
                        break;
                    case TextImageRelation.ImageAboveText:
                    case TextImageRelation.TextAboveImage:
                        width = Math.Max (width, image.Width);
                        height += image.Height + gap;
                        break;
                    default:
                        // Overlay: the two share the same space, so neither adds to the other.
                        width = Math.Max (width, image.Width);
                        height = Math.Max (height, image.Height);
                        break;
                }
            }

            width += glyph.Width;
            height = Math.Max (height, glyph.Height);

            var border = BorderInset ();

            return new Size (
                width + Padding.Horizontal + border.Horizontal,
                height + Padding.Vertical + border.Vertical);
        }

        // The column TextImageLayoutEngine reserves for a check or radio glyph, in logical units:
        // the box, the gap to the text, and the single pixel that engine adds.
        private Size GlyphAllowance ()
        {
            if (this is not IHaveGlyph)
                return Size.Empty;

            // SMP-03: a toggle button draws no glyph, so it must not be measured for one either --
            // otherwise an AutoSize segmented control keeps a 13px column of empty space per item.
            if (AppearanceCore == Appearance.Button)
                return Size.Empty;

            if (Renderers.RenderManager.GetRenderer<Renderers.Renderer> (this) is not Renderers.IRenderGlyph renderer
                || renderer.GlyphSize <= 0)
                return Size.Empty;

            return new Size (renderer.GlyphSize + renderer.GlyphTextPadding + 1, renderer.GlyphSize);
        }

        private Padding BorderInset ()
            => new Padding (
                Style.Border.Left.GetWidth (),
                Style.Border.Top.GetWidth (),
                Style.Border.Right.GetWidth (),
                Style.Border.Bottom.GetWidth ());

        /// <summary>Gets or sets the flat-style appearance of this control.</summary>
        public virtual FlatStyle FlatStyle { get; set; } = FlatStyle.Standard;

        /// <summary>Gets the appearance settings used when <see cref="FlatStyle"/> is Flat.</summary>
        public virtual FlatButtonAppearance FlatAppearance { get; } = new ();

        /// <summary>Gets or sets the alignment of the text on this control.</summary>
        public virtual ContentAlignment TextAlign { get; set; } = ContentAlignment.MiddleCenter;

        /// <summary>Gets or sets the relative placement of the image and text.</summary>
        public virtual TextImageRelation TextImageRelation { get; set; } = TextImageRelation.Overlay;

        /// <summary>Gets or sets whether the first character preceded by an ampersand is an access key.</summary>
        /// <remarks>
        /// Consulted by <see cref="ProcessMnemonic"/>. It was stored-only for as long as nothing
        /// dispatched access keys, so <c>&amp;Save</c> underlined the S and Alt+S did nothing.
        /// </remarks>
        public bool UseMnemonic { get; set; } = true;

        /// <summary>
        /// Upstream's <c>CanSelect</c>, as <c>PerformClick</c> guards with it (SMP-08): selectable,
        /// and neither this control nor any ancestor disabled or hidden.
        /// </summary>
        /// <remarks>
        /// Not <see cref="Control.CanSelect"/>, because that asks <see cref="Control.Visible"/>, which
        /// here reports a control with no parent as hidden. Upstream reports it visible, so a button
        /// built in code and clicked before it is parented -- a command binding, a test -- clicks
        /// there and must click here. "Hidden" therefore means an explicit <c>Visible = false</c> on
        /// the control or an ancestor, which is the control's own flag
        /// (<c>IArrangedElement.ParticipatesInLayout</c> reads exactly that).
        /// </remarks>
        private protected bool CanPerformClick {
            get {
                if (!GetControlBehavior (ControlBehaviors.Selectable))
                    return false;

                for (Control? c = this; c is not null; c = c.Parent) {
                    if (!c.Enabled || !((Layout.IArrangedElement) c).ParticipatesInLayout)
                        return false;
                }

                return true;
            }
        }

        /// <summary>
        /// Clicks the button when <paramref name="charCode"/> is its access key.
        /// </summary>
        /// <remarks>
        /// Mirrors <c>ButtonBase.ProcessMnemonic</c>: a button responds to its mnemonic by clicking,
        /// which is why Alt+S on a <c>&amp;Save</c> button saves rather than merely focusing it.
        /// <para>
        /// Raises <see cref="Control.OnClick"/> directly rather than calling a <c>PerformClick</c> on
        /// the base. <see cref="Button"/> and <see cref="RadioButton"/> each declare their own public
        /// <c>PerformClick</c>, and both are exactly this call — introducing a base method of that name
        /// would shadow two shipped members to no benefit.
        /// </para>
        /// </remarks>
        protected override bool ProcessMnemonic (char charCode)
        {
            if (!UseMnemonic || !CanSelect || !IsMnemonic (charCode, Text ?? string.Empty))
                return false;

            OnClick (new MouseEventArgs (MouseButtons.Left, 1, 0, 0, System.Drawing.Point.Empty));
            return true;
        }

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

        /// <summary>Occurs when <see cref="FormattingEnabled"/> changes.</summary>
        public event EventHandler? FormattingEnabledChanged;

        /// <summary>Occurs when <see cref="FormatInfo"/> changes.</summary>
        public event EventHandler? FormatInfoChanged;

        /// <summary>Occurs when <see cref="FormatString"/> changes.</summary>
        public event EventHandler? FormatStringChanged;

        /// <summary>Raises the <see cref="FormattingEnabledChanged"/> event.</summary>
        protected virtual void OnFormattingEnabledChanged (EventArgs e) => FormattingEnabledChanged?.Invoke (this, e);

        /// <summary>Raises the <see cref="FormatInfoChanged"/> event.</summary>
        protected virtual void OnFormatInfoChanged (EventArgs e) => FormatInfoChanged?.Invoke (this, e);

        /// <summary>Raises the <see cref="FormatStringChanged"/> event.</summary>
        protected virtual void OnFormatStringChanged (EventArgs e) => FormatStringChanged?.Invoke (this, e);

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
                OnDisplayMemberChanged (EventArgs.Empty);
            }
        }

        /// <summary>Gets or sets the property of the data source used as each item's value.</summary>
        public virtual string ValueMember {
            get => valueMember;
            set {
                if (string.Equals (valueMember, value, StringComparison.Ordinal))
                    return;
                valueMember = value ?? string.Empty;
                OnValueMemberChanged (EventArgs.Empty);
            }
        }

        /// <summary>Gets or sets the index of the selected item.</summary>
        public abstract int SelectedIndex { get; set; }

        /// <summary>Gets or sets the value of the selected item, taken from <see cref="ValueMember"/>.</summary>
        public virtual object? SelectedValue { get; set; }

        private bool formatting_enabled;
        private string format_string = string.Empty;
        private IFormatProvider? format_info;

        /// <summary>Gets or sets whether item text is formatted for display.</summary>
        /// <remarks>
        /// The three formatting properties notify on change, as upstream's <c>ListControl</c> does --
        /// each of its setters compares, stores, refreshes and raises. All three events were declared
        /// here behind a <c>CS0067</c> pragma and raised by nothing, so a data-bound control watching
        /// for a formatting change never heard about one (<c>W6.1</c>).
        ///
        /// Upstream also calls <c>RefreshItems ()</c> between the store and the raise; there is no such
        /// method on this <c>ListControl</c>, and the three properties are additionally unread by the
        /// display path here -- a separate gap, recorded in the stored-only baseline. Raising the event
        /// is the half this finding is about, and it is worth having on its own: it is what a binding
        /// layer subscribes to.
        /// </remarks>
        public virtual bool FormattingEnabled {
            get => formatting_enabled;
            set {
                if (formatting_enabled == value)
                    return;

                formatting_enabled = value;
                OnFormattingEnabledChanged (EventArgs.Empty);
            }
        }

        /// <summary>Gets or sets the format string applied to item text.</summary>
        /// <remarks>
        /// Null collapses to empty before the comparison, as upstream does (<c>value ??= string.Empty</c>),
        /// so assigning null to an already-empty format string is not a change.
        /// </remarks>
        public string FormatString {
            get => format_string;
            set {
                value ??= string.Empty;

                if (format_string == value)
                    return;

                format_string = value;
                OnFormatStringChanged (EventArgs.Empty);
            }
        }

        /// <summary>Gets or sets the format provider applied to item text.</summary>
        public IFormatProvider? FormatInfo {
            get => format_info;
            set {
                if (ReferenceEquals (format_info, value))
                    return;

                format_info = value;
                OnFormatInfoChanged (EventArgs.Empty);
            }
        }

        /// <summary>
        /// Returns the text to display for an item: its <see cref="DisplayMember"/> value, offered to
        /// <see cref="Format"/> and then formatted with <see cref="FormatString"/> and
        /// <see cref="FormatInfo"/> when <see cref="FormattingEnabled"/> is set.
        /// </summary>
        /// <remarks>
        /// One implementation for every list control, in the shape of upstream's
        /// <c>ListControl.GetItemText</c> (<c>ListControl/ListControl.cs</c>). <c>ListBox</c> and
        /// <c>ComboBox</c> each had their own copy that raised <see cref="Format"/> with the display
        /// text rather than the member's value and never applied <see cref="FormatString"/>, so a list
        /// of prices with <c>FormatString = "C2"</c> showed raw numbers (<c>LST-27</c>).
        /// </remarks>
        public virtual string GetItemText (object? item)
        {
            // A CheckedListBox stores each item in a wrapper that carries its check state; the text,
            // the display member and the Format event's ListItem are all about the caller's own object.
            if (item is CheckedListBoxItem wrapper)
                item = wrapper.Value;

            if (!FormattingEnabled)
                return DataSourceBinding.DisplayText (item, DisplayMember);

            if (item is null)
                return string.Empty;

            var filtered = string.IsNullOrEmpty (DisplayMember)
                ? item
                : DataSourceBinding.MemberValue (item, DisplayMember) ?? item;

            // Upstream: the handler's answer wins only when it replaced the value with a string.
            var e = new ListControlConvertEventArgs (filtered, typeof (string), item);
            OnFormat (e);

            if (!ReferenceEquals (e.Value, item) && e.Value is string handled)
                return handled;

            return FormatValue (filtered);
        }

        // Formatter.FormatObject's string branch: an IFormattable value takes FormatString with
        // FormatInfo; anything else converts under FormatInfo's culture. Null and DBNull have no
        // formatted-null value on a list control, so they show as empty.
        private string FormatValue (object? value)
        {
            if (value is null || value is DBNull)
                return string.Empty;

            if (value is IFormattable formattable && FormatString.Length > 0)
                return formattable.ToString (FormatString, FormatInfo) ?? string.Empty;

            return Convert.ToString (value, FormatInfo as System.Globalization.CultureInfo ?? System.Globalization.CultureInfo.CurrentCulture) ?? string.Empty;
        }

        /// <summary>Raises the <see cref="DisplayMemberChanged"/> event.</summary>
        protected virtual void OnDisplayMemberChanged (EventArgs e) => DisplayMemberChanged?.Invoke (this, e);

        /// <summary>Raises the <see cref="ValueMemberChanged"/> event.</summary>
        protected virtual void OnValueMemberChanged (EventArgs e) => ValueMemberChanged?.Invoke (this, e);

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
        /// <remarks>Virtual because a derived control can measure rather than estimate; the estimate
        /// here is the fallback for one that does not.</remarks>
        public virtual int PreferredHeight => Font is null ? 20 : (int)Math.Ceiling (Font.Size * 2.2f);

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

        // DropDownItems is this item's own Items here (the ToolStripDropDown object is a separate
        // shell), so a child's Click is relayed from that collection as DropDownItemClicked, which is
        // what upstream raises for a click on any of DropDownItems. Until the W6.1 sweep the event was
        // declared and raised by nothing.
        internal override void OnChildItemAddedCore (MenuItem child)
        {
            base.OnChildItemAddedCore (child);
            if (child is ToolStripItem item)
                item.Click += RelayDropDownItemClicked;
        }

        internal override void OnChildItemRemovedCore (MenuItem child)
        {
            base.OnChildItemRemovedCore (child);
            if (child is ToolStripItem item)
                item.Click -= RelayDropDownItemClicked;
        }

        private void RelayDropDownItemClicked (object? sender, EventArgs e)
        {
            if (sender is ToolStripItem item)
                OnDropDownItemClicked (new ToolStripItemClickedEventArgs (item));
        }

        /// <summary>Gets the items in this item's drop-down.</summary>
        /// <remarks>
        /// The item's own <see cref="MenuItem.Items"/> -- the same collection <see cref="DropDown"/>'s
        /// <c>Items</c> resolves to, as in WinForms, and returned directly so reading it does not force
        /// the drop-down control into existence.
        /// </remarks>
        public virtual MenuItemCollection DropDownItems => Items;

        /// <summary>Gets or sets the direction the drop-down opens in.</summary>
        /// <remarks>Read by <see cref="MenuItem.ShowDropDown"/> as of W6 mechanisms: the drop-down opens
        /// to the named side of the item. <c>Default</c> defers to the strip's
        /// <see cref="ToolStrip.DefaultDropDownDirection"/>, then to below the item (beside it for a
        /// submenu).</remarks>
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

            // Opened only when something opened. MenuItem.ShowDropDown does nothing for an item with no
            // sub-items or no strip, and DropDownOpened fired anyway -- for every leaf, and for a lazy
            // menu whose DropDownOpening added nothing. Upstream raises it from the drop-down's own
            // Opened, which an empty auto-generated drop-down never reaches
            // (ToolStripDropDownItem.cs, ShowDropDownInternal) (TSM-35).
            if (IsDropDownOpened)
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
