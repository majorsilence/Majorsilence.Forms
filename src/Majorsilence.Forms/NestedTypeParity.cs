using System;
using System.Collections.Generic;
using System.Drawing;
using SkiaSharp;

namespace Majorsilence.Forms
{
    // Nested types (docs/winforms-gap-plan.md).
    //
    // WinForms nests its collection and accessibility types inside the control that owns them --
    // ListView.ListViewItemCollection, ComboBox.ObjectCollection, ButtonBase.ButtonBaseAccessibleObject
    // and so on. This layer had grown the same types at namespace scope, so a migrated file that
    // spells the nested name out (designer files always do) would not compile.
    //
    // Each nested collection derives from the namespace-scope type it shadows, so both spellings
    // name a usable type and a value of the nested type is still assignable to the old one. Nothing
    // that compiled before stops compiling.

    public partial class ListView
    {
        /// <summary>The items of a <see cref="ListView"/>.</summary>
        public class ListViewItemCollection : Majorsilence.Forms.ListViewItemCollection
        {
            /// <summary>Initializes a new instance of the <see cref="ListViewItemCollection"/> class.</summary>
            public ListViewItemCollection (ListView owner) : base (owner) { }
        }

        /// <summary>The column headers of a <see cref="ListView"/>.</summary>
        public class ColumnHeaderCollection : Majorsilence.Forms.ColumnHeaderCollection
        {
            /// <summary>Initializes a new instance of the <see cref="ColumnHeaderCollection"/> class.</summary>
            public ColumnHeaderCollection (ListView? owner = null) => Owner = owner;

            /// <summary>Gets the list view the headers belong to.</summary>
            public ListView? Owner { get; }

            /// <inheritdoc/>
            protected override void InsertItem (int index, ColumnHeader item)
            {
                base.InsertItem (index, item);

                if (item is not null)
                    item.ListView = Owner;
            }

            /// <inheritdoc/>
            protected override void RemoveItem (int index)
            {
                this[index].ListView = null;
                base.RemoveItem (index);
            }
        }
    }

    public partial class TabControl
    {
        /// <summary>The pages of a <see cref="TabControl"/>.</summary>
        public class TabPageCollection : Majorsilence.Forms.TabPageCollection
        {
            internal TabPageCollection (TabControl owner, TabStrip tabStrip) : base (owner, tabStrip) { }

            /// <summary>Initializes a collection for the given tab control.</summary>
            /// <remarks>
            /// The WinForms-shaped constructor, so a library can derive its own collection type. It
            /// binds to the owner's existing tab strip rather than creating a second one.
            /// </remarks>
            public TabPageCollection (TabControl owner) : this (owner, owner.TabStrip) { }
        }
    }

    public partial class ToolBar
    {
        /// <summary>The buttons of a <see cref="ToolBar"/>.</summary>
        public class ToolBarButtonCollection : Majorsilence.Forms.ToolBarButtonCollection
        {
            /// <summary>Initializes a new instance of the <see cref="ToolBarButtonCollection"/> class.</summary>
            public ToolBarButtonCollection (ToolBar? owner = null) => Owner = owner;

            /// <summary>Gets the toolbar the buttons belong to.</summary>
            public ToolBar? Owner { get; }
        }
    }

    public partial class Menu
    {
        /// <summary>The items of a <see cref="Menu"/>.</summary>
        public class MenuItemCollection : Majorsilence.Forms.MenuItemCollection
        {
            /// <summary>Initializes a new instance of the <see cref="MenuItemCollection"/> class.</summary>
            public MenuItemCollection (MenuItem owner) : base (owner) { }
        }
    }

    public partial class ImageList
    {
        /// <summary>The images of an <see cref="ImageList"/>.</summary>
        public sealed class ImageCollection : Majorsilence.Forms.ImageCollection
        {
            internal ImageCollection (SKSize imageSize) : base (imageSize) { }
        }
    }

    public partial class ComboBox
    {
        /// <summary>The items of a <see cref="ComboBox"/>.</summary>
        /// <remarks>
        /// A combo box's items live in the list box it drops down, so this is that list's own collection
        /// under the name WinForms gives it. It is not a second collection over the same items -- one would
        /// diverge from the other on the first change -- which is why the constructor is the list-box one
        /// inherited from the base and <see cref="ComboBox.Items"/> is the instance the popup was built with.
        /// </remarks>
        public class ObjectCollection : ListBox.ObjectCollection
        {
            internal ObjectCollection (ListBox owner) : base (owner) { }
        }

        /// <summary>Exposes a child of a <see cref="ComboBox"/> -- its edit field or its list -- to
        /// accessibility clients.</summary>
        public class ChildAccessibleObject : AccessibleObject
        {
            private readonly ComboBox owner;

            /// <summary>Initializes a new instance of the <see cref="ChildAccessibleObject"/> class.</summary>
            /// <remarks>The handle is accepted for source compatibility and ignored: there are no
            /// HWNDs here, so the child is identified by its owner alone.</remarks>
            public ChildAccessibleObject (ComboBox owner, IntPtr handle)
            {
                ArgumentNullException.ThrowIfNull (owner);
                this.owner = owner;
            }

            /// <inheritdoc/>
            public override string? Name => owner.AccessibleName ?? owner.Text;
        }
    }

    public partial class ListBox
    {
        /// <summary>The items of a <see cref="ListBox"/>.</summary>
        public class ObjectCollection : ListBoxItemCollection
        {
            /// <summary>Initializes a new instance of the <see cref="ObjectCollection"/> class.</summary>
            public ObjectCollection (ListBox owner) : base (owner) { }
        }

        /// <summary>The selected items of a <see cref="ListBox"/>.</summary>
        public class SelectedObjectCollection : System.Collections.ObjectModel.Collection<object>
        {
            /// <summary>Initializes a new instance of the <see cref="SelectedObjectCollection"/> class.</summary>
            public SelectedObjectCollection (ListBox owner)
            {
                ArgumentNullException.ThrowIfNull (owner);

                foreach (var item in owner.Items.SelectedItems)
                    Add (item);
            }
        }
    }

    public partial class TabPage
    {
        /// <summary>The controls of a <see cref="TabPage"/>.</summary>
        public class TabPageControlCollection : Control.ControlCollection
        {
            /// <summary>Initializes a new instance of the <see cref="TabPageControlCollection"/> class.</summary>
            public TabPageControlCollection (TabPage owner) : base (owner) { }
        }
    }

    public partial class ToolStripPanel
    {
        /// <summary>The rows of a <see cref="ToolStripPanel"/>.</summary>
        public class ToolStripPanelRowCollection : Layout.ArrangedElementCollection
        {
            /// <summary>Initializes a new instance of the <see cref="ToolStripPanelRowCollection"/> class.</summary>
            public ToolStripPanelRowCollection (ToolStripPanel owner) => Owner = owner;

            /// <summary>Initializes a new instance of the <see cref="ToolStripPanelRowCollection"/> class.</summary>
            public ToolStripPanelRowCollection (ToolStripPanel owner, ToolStripPanelRow[] value)
                : this (owner)
            {
                ArgumentNullException.ThrowIfNull (value);

                foreach (var row in value)
                    Add (row);
            }

            /// <summary>Gets the panel the rows belong to.</summary>
            public ToolStripPanel? Owner { get; }

            /// <summary>Gets the row at the given index.</summary>
            public new ToolStripPanelRow? this[int index] => base[index] as ToolStripPanelRow;

            /// <summary>Adds a row.</summary>
            public int Add (ToolStripPanelRow value) => base.Add (value);

            /// <summary>Removes a row.</summary>
            public void Remove (ToolStripPanelRow value) => base.Remove (value);
        }
    }

    public partial class ScrollableControl
    {
        /// <summary>Converts a <see cref="DockPaddingEdges"/> to and from other representations.</summary>
        public class DockPaddingEdgesConverter : System.ComponentModel.TypeConverter
        {
            /// <summary>Initializes a new instance of the <see cref="DockPaddingEdgesConverter"/> class.</summary>
            public DockPaddingEdgesConverter () { }
        }
    }

    public partial struct LinkArea
    {
        /// <summary>Converts a <see cref="LinkArea"/> to and from other representations.</summary>
        public class LinkAreaConverter : System.ComponentModel.TypeConverter
        {
            /// <summary>Initializes a new instance of the <see cref="LinkAreaConverter"/> class.</summary>
            public LinkAreaConverter () { }

            /// <inheritdoc/>
            public override bool CanConvertFrom (System.ComponentModel.ITypeDescriptorContext? context, Type sourceType)
                => sourceType == typeof (string) || base.CanConvertFrom (context, sourceType);

            /// <inheritdoc/>
            public override object? ConvertFrom (System.ComponentModel.ITypeDescriptorContext? context,
                System.Globalization.CultureInfo? culture, object value)
            {
                if (value is not string text)
                    return base.ConvertFrom (context, culture, value);

                culture ??= System.Globalization.CultureInfo.CurrentCulture;
                var separator = culture.TextInfo.ListSeparator[0];
                var parts = text.Split (separator);

                if (parts.Length != 2)
                    throw new ArgumentException ($"Cannot convert \"{text}\" to a LinkArea.", nameof (value));

                return new LinkArea (int.Parse (parts[0].Trim (), culture), int.Parse (parts[1].Trim (), culture));
            }

            /// <inheritdoc/>
            public override object? ConvertTo (System.ComponentModel.ITypeDescriptorContext? context,
                System.Globalization.CultureInfo? culture, object? value, Type destinationType)
            {
                if (destinationType == typeof (string) && value is LinkArea area) {
                    culture ??= System.Globalization.CultureInfo.CurrentCulture;
                    return string.Join (culture.TextInfo.ListSeparator + " ", area.Start, area.Length);
                }

                return base.ConvertTo (context, culture, value, destinationType);
            }
        }
    }

    public abstract partial class ButtonBase
    {
        /// <summary>Exposes a <see cref="ButtonBase"/> to accessibility clients.</summary>
        public class ButtonBaseAccessibleObject : Control.ControlAccessibleObject
        {
            /// <summary>Initializes a new instance of the <see cref="ButtonBaseAccessibleObject"/> class.</summary>
            public ButtonBaseAccessibleObject (Control owner) : base (owner) { }

            /// <inheritdoc/>
            public override string? Name => Owner.AccessibleName ?? Owner.Text;

            /// <inheritdoc/>
            public override string? KeyboardShortcut => Mnemonic (Owner.Text);

            /// <inheritdoc/>
            public override AccessibleStates State
            {
                get {
                    var state = AccessibleStates.None;

                    if (!Owner.Enabled)
                        state |= AccessibleStates.Unavailable;
                    if (Owner.Focused)
                        state |= AccessibleStates.Focused;
                    if (!Owner.Visible)
                        state |= AccessibleStates.Invisible;

                    return state;
                }
            }

            /// <inheritdoc/>
            /// <remarks>Only a <see cref="Button"/> can be clicked from here: WinForms puts
            /// PerformClick on Button rather than ButtonBase, and the check box and radio button
            /// override this to toggle themselves instead.</remarks>
            public override void DoDefaultAction ()
            {
                if (Owner is Button button)
                    button.PerformClick ();
            }

            // WinForms reports the shortcut as "Alt+<mnemonic>", taken from the ampersand in Text.
            internal static string? Mnemonic (string? text)
            {
                if (string.IsNullOrEmpty (text))
                    return null;

                for (var i = 0; i < text.Length - 1; i++) {
                    if (text[i] != '&')
                        continue;

                    if (text[i + 1] == '&') {
                        i++;
                        continue;
                    }

                    return "Alt+" + text[i + 1];
                }

                return null;
            }
        }
    }

    public partial class CheckBox
    {
        /// <summary>Exposes a <see cref="CheckBox"/> to accessibility clients.</summary>
        public class CheckBoxAccessibleObject : ButtonBase.ButtonBaseAccessibleObject
        {
            /// <summary>Initializes a new instance of the <see cref="CheckBoxAccessibleObject"/> class.</summary>
            public CheckBoxAccessibleObject (Control owner) : base (owner) { }

            /// <inheritdoc/>
            public override AccessibleRole Role
                => Owner.AccessibleRole == AccessibleRole.Default ? AccessibleRole.CheckButton : Owner.AccessibleRole;

            /// <inheritdoc/>
            public override string? DefaultAction
                => Owner is CheckBox { Checked: true } ? "Uncheck" : "Check";

            /// <inheritdoc/>
            public override AccessibleStates State
            {
                get {
                    var state = base.State;

                    if (Owner is CheckBox box) {
                        if (box.CheckState == CheckState.Checked)
                            state |= AccessibleStates.Checked;
                        else if (box.CheckState == CheckState.Indeterminate)
                            state |= AccessibleStates.Indeterminate;
                    }

                    return state;
                }
            }

            /// <inheritdoc/>
            public override void DoDefaultAction ()
            {
                if (Owner is CheckBox box)
                    box.Checked = !box.Checked;
            }
        }
    }

    public partial class RadioButton
    {
        /// <summary>Exposes a <see cref="RadioButton"/> to accessibility clients.</summary>
        public class RadioButtonAccessibleObject : Control.ControlAccessibleObject
        {
            /// <summary>Initializes a new instance of the <see cref="RadioButtonAccessibleObject"/> class.</summary>
            public RadioButtonAccessibleObject (RadioButton owner) : base (owner) { }

            /// <inheritdoc/>
            public override string? Name => Owner.AccessibleName ?? Owner.Text;

            /// <inheritdoc/>
            public override string? KeyboardShortcut => ButtonBase.ButtonBaseAccessibleObject.Mnemonic (Owner.Text);

            /// <inheritdoc/>
            public override AccessibleRole Role
                => Owner.AccessibleRole == AccessibleRole.Default ? AccessibleRole.RadioButton : Owner.AccessibleRole;

            /// <inheritdoc/>
            public override string? DefaultAction => "Select";

            /// <inheritdoc/>
            public override AccessibleStates State
            {
                get {
                    var state = AccessibleStates.None;

                    if (Owner is RadioButton { Checked: true })
                        state |= AccessibleStates.Checked;
                    if (!Owner.Enabled)
                        state |= AccessibleStates.Unavailable;
                    if (Owner.Focused)
                        state |= AccessibleStates.Focused;

                    return state;
                }
            }

            /// <inheritdoc/>
            public override void DoDefaultAction ()
            {
                if (Owner is RadioButton radio)
                    radio.Checked = true;
            }
        }
    }

    public partial class DateTimePicker
    {
        /// <summary>Exposes a <see cref="DateTimePicker"/> to accessibility clients.</summary>
        public class DateTimePickerAccessibleObject : Control.ControlAccessibleObject
        {
            /// <summary>Initializes a new instance of the <see cref="DateTimePickerAccessibleObject"/> class.</summary>
            public DateTimePickerAccessibleObject (DateTimePicker owner) : base (owner) { }

            /// <inheritdoc/>
            public override string? Name => Owner.AccessibleName ?? Owner.Text;

            /// <inheritdoc/>
            public override string? KeyboardShortcut => ButtonBase.ButtonBaseAccessibleObject.Mnemonic (Owner.Text);

            /// <inheritdoc/>
            public override AccessibleRole Role
                => Owner.AccessibleRole == AccessibleRole.Default ? AccessibleRole.DropList : Owner.AccessibleRole;

            /// <inheritdoc/>
            public override string? DefaultAction => "DropDown";

            /// <inheritdoc/>
            public override string? Value
            {
                get => Owner is DateTimePicker picker ? picker.Text : null;
                set { }
            }

            /// <inheritdoc/>
            public override AccessibleStates State
            {
                get {
                    var state = AccessibleStates.None;

                    if (!Owner.Enabled)
                        state |= AccessibleStates.Unavailable;
                    if (Owner.Focused)
                        state |= AccessibleStates.Focused;

                    return state;
                }
            }
        }
    }

    public partial class ToolStripItem
    {
        /// <summary>Exposes a <see cref="ToolStripItem"/> to accessibility clients.</summary>
        public class ToolStripItemAccessibleObject : Majorsilence.Forms.ToolStripItemAccessibleObject
        {
            private AccessibleStates added;

            /// <summary>Initializes a new instance of the <see cref="ToolStripItemAccessibleObject"/> class.</summary>
            public ToolStripItemAccessibleObject (ToolStripItem ownerItem) : base (ownerItem)
                => ArgumentNullException.ThrowIfNull (ownerItem);

            /// <summary>Gets the item this object describes.</summary>
            protected ToolStripItem OwnerItem => Owner;

            /// <inheritdoc/>
            public override string? KeyboardShortcut
                => ButtonBase.ButtonBaseAccessibleObject.Mnemonic (OwnerItem.Text);

            /// <inheritdoc/>
            public override AccessibleRole Role
                => OwnerItem.AccessibleRole == AccessibleRole.Default ? AccessibleRole.PushButton : OwnerItem.AccessibleRole;

            /// <inheritdoc/>
            public override Rectangle Bounds => OwnerItem.Bounds;

            /// <inheritdoc/>
            public override AccessibleObject? Parent => null;

            /// <inheritdoc/>
            public override AccessibleStates State
            {
                get {
                    var state = added;

                    if (!OwnerItem.Enabled)
                        state |= AccessibleStates.Unavailable;
                    if (!OwnerItem.Visible)
                        state |= AccessibleStates.Invisible;

                    return state;
                }
            }

            /// <summary>Adds a state flag reported by <see cref="State"/>.</summary>
            public void AddState (AccessibleStates state) => added = state == AccessibleStates.None ? AccessibleStates.None : added | state;

            /// <inheritdoc/>
            public override void DoDefaultAction () => OwnerItem.PerformClick ();

            /// <inheritdoc/>
            public override int GetHelpTopic (out string? fileName)
            {
                fileName = null;
                return -1;
            }

            /// <inheritdoc/>
            public override AccessibleObject? Navigate (AccessibleNavigation navdir) => null;

            /// <inheritdoc/>
            public override string ToString () => $"ToolStripItemAccessibleObject: Owner = {OwnerItem}";
        }
    }

    public partial class ToolStrip
    {
        /// <summary>Exposes a <see cref="ToolStrip"/> to accessibility clients.</summary>
        public class ToolStripAccessibleObject : Control.ControlAccessibleObject
        {
            /// <summary>Initializes a new instance of the <see cref="ToolStripAccessibleObject"/> class.</summary>
            public ToolStripAccessibleObject (ToolStrip owner) : base (owner) { }

            /// <inheritdoc/>
            public override AccessibleRole Role
                => Owner.AccessibleRole == AccessibleRole.Default ? AccessibleRole.ToolBar : Owner.AccessibleRole;

            /// <inheritdoc/>
            public override int GetChildCount () => Owner is ToolStrip strip ? strip.Items.Count : 0;

            /// <inheritdoc/>
            public override AccessibleObject? GetChild (int index)
                => Owner is ToolStrip strip && index >= 0 && index < strip.Items.Count
                    // AccessibilityObject is a ToolStripItem member; a plain MenuItem in the strip has
                    // none to expose.
                    ? (strip.Items[index] as ToolStripItem)?.AccessibilityObject
                    : null;

            /// <inheritdoc/>
            public override AccessibleObject? HitTest (int x, int y)
            {
                if (Owner is not ToolStrip strip)
                    return null;

                foreach (ToolStripItem item in strip.Items) {
                    if (item.Bounds.Contains (x, y))
                        return item.AccessibilityObject;
                }

                return base.HitTest (x, y);
            }
        }
    }

    public partial class ToolStripDropDown
    {
        /// <summary>Exposes a <see cref="ToolStripDropDown"/> to accessibility clients.</summary>
        public class ToolStripDropDownAccessibleObject : ToolStrip.ToolStripAccessibleObject
        {
            /// <summary>Initializes a new instance of the <see cref="ToolStripDropDownAccessibleObject"/> class.</summary>
            public ToolStripDropDownAccessibleObject (ToolStripDropDown owner) : base (owner) { }

            /// <inheritdoc/>
            public override string? Name
            {
                get => Owner.AccessibleName ?? Owner.Text;
                set => Owner.AccessibleName = value;
            }

            /// <inheritdoc/>
            public override AccessibleRole Role
                => Owner.AccessibleRole == AccessibleRole.Default ? AccessibleRole.MenuPopup : Owner.AccessibleRole;
        }
    }

    public partial class ToolStripSplitButton
    {
        /// <summary>Exposes a <see cref="ToolStripSplitButton"/> to accessibility clients.</summary>
        public class ToolStripSplitButtonAccessibleObject : ToolStripItem.ToolStripItemAccessibleObject
        {
            /// <summary>Initializes a new instance of the <see cref="ToolStripSplitButtonAccessibleObject"/> class.</summary>
            public ToolStripSplitButtonAccessibleObject (ToolStripSplitButton item) : base (item) { }

            /// <inheritdoc/>
            public override void DoDefaultAction ()
            {
                if (OwnerItem is ToolStripSplitButton split)
                    split.PerformButtonClick ();
            }
        }
    }

    public partial class ToolStripControlHost
    {
        /// <summary>Exposes the control hosted by a <see cref="ToolStripControlHost"/> to
        /// accessibility clients.</summary>
        public class ToolStripHostedControlAccessibleObject : Majorsilence.Forms.ToolStripHostedControlAccessibleObject
        {
            /// <summary>Initializes a new instance of the <see cref="ToolStripHostedControlAccessibleObject"/> class.</summary>
            public ToolStripHostedControlAccessibleObject (Control toolStripHostedControl,
                ToolStripControlHost? toolStripControlHost)
                : base (toolStripHostedControl, toolStripControlHost)
                => ControlHostItem = toolStripControlHost;

            /// <summary>Gets the item hosting the control.</summary>
            protected ToolStripControlHost? ControlHostItem { get; }
        }
    }
}
