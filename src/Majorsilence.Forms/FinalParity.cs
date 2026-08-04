using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Linq;

namespace Majorsilence.Forms
{
    // The last of the member gaps (docs/winforms-gap-plan.md).
    //
    // Mostly the nested collection types the older controls expose -- StatusBar's panels,
    // CheckedListBox's three views, DomainUpDown's items. They are named in designer files and in
    // `foreach (StatusBarPanel p in bar.Panels)`, which is why they have to exist under those names
    // rather than being a plain List.
    //
    // CheckedListBox's checked views are live over the control, like ListView's from item 8, so a
    // caller that holds one and then ticks a box sees the change.

    public partial class DataGridViewBand
    {
        /// <summary>Gets or sets the type of header cell this band creates.</summary>
        public Type? DefaultHeaderCellType { get; set; }

        /// <summary>Gets or sets the context menu shown for this band.</summary>
        public virtual ContextMenuStrip? ContextMenuStrip { get; set; }

        /// <summary>Gets or sets whether the band is currently on screen.</summary>
        public virtual bool Displayed { get; set; }

        /// <summary>Gets whether a style has been set on this band rather than inherited.</summary>
        public bool HasDefaultCellStyle => DefaultCellStyle is not null;

        /// <summary>Gets the style this band paints with, given what it inherits.</summary>
        public virtual DataGridViewCellStyle InheritedStyle => DefaultCellStyle ?? new DataGridViewCellStyle ();

        /// <summary>Returns a copy of this band.</summary>
        public virtual object Clone ()
            => new DataGridViewBand {
                DefaultCellStyle = DefaultCellStyle,
                DefaultHeaderCellType = DefaultHeaderCellType,
                Frozen = Frozen,
                ReadOnly = ReadOnly,
                Resizable = Resizable,
                Visible = Visible,
                Tag = Tag,
            };
    }

    public partial class StatusBar
    {
        /// <summary>Gets or sets whether the sizing grip is drawn in the corner.</summary>
        public bool SizingGrip { get; set; } = true;

        // Owner-drawn panels are painted by this layer's renderer, which does not call back into
        // application code, so Draw is declared and raisable but not raised. PanelClick is the same:
        // panel hit-testing runs through the strip's own item routing.
#pragma warning disable CS0067
        /// <summary>Raised when an owner-drawn panel must be painted. Not raised by this layer yet.</summary>
        public event StatusBarDrawItemEventHandler? DrawItem;

        /// <summary>Raised when a panel is clicked. Not raised by this layer yet.</summary>
        public event StatusBarPanelClickEventHandler? PanelClick;
#pragma warning restore CS0067

        /// <summary>The panels of a <see cref="StatusBar"/>.</summary>
        public class StatusBarPanelCollection : System.Collections.ObjectModel.Collection<StatusBarPanel>
        {
            /// <summary>Adds a panel showing the given text.</summary>
            public StatusBarPanel Add (string text)
            {
                var panel = new StatusBarPanel { Text = text };
                Add (panel);
                return panel;
            }

            /// <summary>Adds several panels at once.</summary>
            public void AddRange (params StatusBarPanel[] panels)
            {
                ArgumentNullException.ThrowIfNull (panels);

                foreach (var panel in panels)
                    Add (panel);
            }

            /// <summary>Returns whether a panel with the given name is present.</summary>
            public bool ContainsKey (string key) => IndexOfKey (key) >= 0;

            /// <summary>Returns the index of the panel with the given name, or -1.</summary>
            public int IndexOfKey (string key)
            {
                if (string.IsNullOrEmpty (key))
                    return -1;

                for (var i = 0; i < Count; i++)
                    if (string.Equals (this[i].Name, key, StringComparison.OrdinalIgnoreCase))
                        return i;

                return -1;
            }

            /// <summary>Removes the panel with the given name, if there is one.</summary>
            public void RemoveByKey (string key)
            {
                var index = IndexOfKey (key);

                if (index >= 0)
                    RemoveAt (index);
            }
        }
    }

    public partial class StatusBarPanel
    {
        /// <summary>Gets the status bar this panel belongs to.</summary>
        public StatusBar? Parent { get; internal set; }

        /// <summary>Signals that initialization is starting.</summary>
        public void BeginInit () { }

        /// <summary>Signals that initialization has finished.</summary>
        public void EndInit () { }
    }

    /// <summary>Provides the data needed to paint an owner-drawn <see cref="StatusBarPanel"/>.</summary>
    /// <remarks>
    /// This and <see cref="StatusBarPanelClickEventArgs"/> were on item 2's deliberately-absent list,
    /// because generating them mechanically needed base constructors this layer does not have in the
    /// same shape. Written by hand they are straightforward.
    /// </remarks>
    public class StatusBarDrawItemEventArgs : DrawItemEventArgs
    {
        /// <summary>Initializes a new instance of the <see cref="StatusBarDrawItemEventArgs"/> class.</summary>
        public StatusBarDrawItemEventArgs (Graphics g, Majorsilence.Forms.Drawing.Font? font, Rectangle r,
            int itemId, DrawItemState itemState, StatusBarPanel panel)
            : base (g, font, r, itemId, itemState) => Panel = panel;

        /// <inheritdoc cref="StatusBarDrawItemEventArgs(Graphics,Majorsilence.Forms.Drawing.Font,Rectangle,int,DrawItemState,StatusBarPanel)"/>
        public StatusBarDrawItemEventArgs (Graphics g, Majorsilence.Forms.Drawing.Font? font, Rectangle r,
            int itemId, DrawItemState itemState, StatusBarPanel panel, Color foreColor, Color backColor)
            : base (g, font, r, itemId, itemState, foreColor, backColor) => Panel = panel;

        /// <summary>Gets the panel being painted.</summary>
        public StatusBarPanel Panel { get; }
    }

    /// <summary>Provides the data for a click on a <see cref="StatusBarPanel"/>.</summary>
    public class StatusBarPanelClickEventArgs : MouseEventArgs
    {
        /// <summary>Initializes a new instance of the <see cref="StatusBarPanelClickEventArgs"/> class.</summary>
        public StatusBarPanelClickEventArgs (StatusBarPanel statusBarPanel, MouseButtons button, int clicks, int x, int y)
            : base (button, clicks, x, y, Point.Empty) => StatusBarPanel = statusBarPanel;

        /// <summary>Gets the panel that was clicked.</summary>
        public StatusBarPanel StatusBarPanel { get; }
    }

    /// <summary>Handles painting of an owner-drawn <see cref="StatusBarPanel"/>.</summary>
    public delegate void StatusBarDrawItemEventHandler (object? sender, StatusBarDrawItemEventArgs sbdevent);

    /// <summary>Handles a click on a <see cref="StatusBarPanel"/>.</summary>
    public delegate void StatusBarPanelClickEventHandler (object? sender, StatusBarPanelClickEventArgs e);

    public partial class CheckedListBox
    {
        /// <summary>Gets or sets whether text is drawn through the compatible text renderer.</summary>
        public bool UseCompatibleTextRendering { get; set; }

        /// <summary>A live view of the indices of a <see cref="CheckedListBox"/>'s ticked items.</summary>
        public class CheckedIndexCollection : IReadOnlyList<int>
        {
            private readonly CheckedListBox owner;

            internal CheckedIndexCollection (CheckedListBox owner) => this.owner = owner;

            private IEnumerable<int> Matching => Enumerable.Range (0, owner.Items.Count).Where (owner.GetItemChecked);

            /// <summary>Gets the number of ticked items.</summary>
            public int Count => Matching.Count ();

            /// <summary>Gets the item index at the given position within this view.</summary>
            public int this[int index] => Matching.ElementAt (index);

            /// <summary>Returns whether the item at the given index is ticked.</summary>
            public bool Contains (int index) => Matching.Contains (index);

            /// <summary>Returns the position of the given item index within this view, or -1.</summary>
            public int IndexOf (int index) => Matching.ToList ().IndexOf (index);

            /// <inheritdoc/>
            public IEnumerator<int> GetEnumerator () => Matching.GetEnumerator ();

            IEnumerator IEnumerable.GetEnumerator () => GetEnumerator ();
        }

        /// <summary>A live view of a <see cref="CheckedListBox"/>'s ticked items.</summary>
        public class CheckedItemCollection : IReadOnlyList<object>
        {
            private readonly CheckedListBox owner;

            internal CheckedItemCollection (CheckedListBox owner) => this.owner = owner;

            private IEnumerable<object> Matching => Enumerable.Range (0, owner.Items.Count)
                .Where (owner.GetItemChecked)
                .Select (i => owner.Items[i]!);

            /// <summary>Gets the number of ticked items.</summary>
            public int Count => Matching.Count ();

            /// <summary>Gets the ticked item at the given position within this view.</summary>
            public object this[int index] => Matching.ElementAt (index);

            /// <summary>Returns whether the given item is ticked.</summary>
            public bool Contains (object? item) => item is not null && Matching.Contains (item);

            /// <summary>Returns the position of the given item within this view, or -1.</summary>
            public int IndexOf (object item) => Matching.ToList ().IndexOf (item);

            /// <inheritdoc/>
            public IEnumerator<object> GetEnumerator () => Matching.GetEnumerator ();

            IEnumerator IEnumerable.GetEnumerator () => GetEnumerator ();
        }

        /// <summary>The items of a <see cref="CheckedListBox"/>.</summary>
        /// <remarks>Derives from this assembly's list-box item collection, which is what
        /// <c>Items</c> already returns, so nothing that uses that type has to change.</remarks>
        public class ObjectCollection : ListBoxItemCollection
        {
            /// <summary>Initializes a new instance of the <see cref="ObjectCollection"/> class.</summary>
            internal ObjectCollection (CheckedListBox owner) : base (owner) { }
        }
    }

    public partial class DomainUpDown
    {
        /// <summary>The items of a <see cref="DomainUpDown"/>.</summary>
        public class DomainUpDownItemCollection : ArrayList
        {
        }

        /// <summary>Exposes a <see cref="DomainUpDown"/> to accessibility clients.</summary>
        public class DomainUpDownAccessibleObject : AccessibleObject
        {
            /// <summary>Initializes a new instance of the <see cref="DomainUpDownAccessibleObject"/> class.</summary>
            public DomainUpDownAccessibleObject (DomainUpDown owner) => Owner = owner;

            /// <summary>Gets the control this object describes.</summary>
            public DomainUpDown Owner { get; }

            /// <summary>Gets the role reported to assistive technology.</summary>
            public override AccessibleRole Role => AccessibleRole.SpinButton;
        }

        /// <summary>Exposes one of a <see cref="DomainUpDown"/>'s items to accessibility clients.</summary>
        public class DomainItemAccessibleObject : AccessibleObject
        {
            /// <summary>Initializes a new instance of the <see cref="DomainItemAccessibleObject"/> class.</summary>
            public DomainItemAccessibleObject (string? name, AccessibleObject parent)
            {
                Name = name;
                Parent = parent;
            }

            /// <summary>Gets the object this item belongs to.</summary>
            public new AccessibleObject Parent { get; }

            /// <summary>Gets the role reported to assistive technology.</summary>
            public override AccessibleRole Role => AccessibleRole.ListItem;
        }
    }

    public partial class DataGridViewImageColumn
    {
        /// <summary>Gets or sets the icon shown in the column's cells.</summary>
        public Majorsilence.Forms.Drawing.Icon? Icon { get; set; }

        /// <summary>Gets or sets whether the cells hold icons rather than images.</summary>
        public bool ValuesAreIcons { get; set; }

        /// <summary>Gets or sets how the image is fitted into the cell.</summary>
        public DataGridViewImageCellLayout ImageLayout { get; set; } = DataGridViewImageCellLayout.Normal;
    }

    public partial class DataGridTextBoxColumn
    {
        /// <summary>Gets or sets the format string applied to the column's values.</summary>
        public string Format { get; set; } = string.Empty;

        /// <summary>Gets or sets the culture used when formatting.</summary>
        public IFormatProvider? FormatInfo { get; set; }

        /// <summary>Gets or sets the property this column is bound to.</summary>
        public PropertyDescriptor? PropertyDescriptor { get; set; }
    }

    public partial class CurrencyManager
    {
        // The list notifications. This manager reads a snapshot of the bound list rather than
        // subscribing to it, so nothing here observes a change to raise them from; a derived manager
        // that does subscribe can raise them.
#pragma warning disable CS0067
        /// <summary>Raised when the current item changes. Not raised by this layer yet.</summary>
        public event EventHandler? ItemChanged;

        /// <summary>Raised when the bound list changes. Not raised by this layer yet.</summary>
        public event ListChangedEventHandler? ListChanged;

        /// <summary>Raised when the list's schema changes. Not raised by this layer yet.</summary>
        public event EventHandler? MetaDataChanged;
#pragma warning restore CS0067
    }

    public partial class DataGridViewTextBoxEditingControl
    {
        /// <summary>Gets the cursor shown over the editing panel behind this control.</summary>
        public Cursor? EditingPanelCursor => Cursors.Default;

        /// <summary>Gets whether the cell should be repainted when the value changes.</summary>
        public bool RepositionEditingControlOnValueChange => false;

        /// <summary>Applies the cell's style to this control.</summary>
        public void ApplyCellStyleToEditingControl (DataGridViewCellStyle dataGridViewCellStyle)
        {
            if (dataGridViewCellStyle is null)
                return;

            Font = dataGridViewCellStyle.Font ?? Font;
            ForeColor = dataGridViewCellStyle.ForeColor;
            BackColor = dataGridViewCellStyle.BackColor;
        }

        /// <summary>Returns whether this control wants the given key rather than the grid.</summary>
        public bool EditingControlWantsInputKey (Keys keyData, bool dataGridViewWantsInputKey)
        {
            // A text box wants the keys that move the caret within the text; the grid wants the ones
            // that move between cells. Getting this backwards makes arrow keys jump cells mid-word.
            var key = keyData & Keys.KeyCode;

            return key switch {
                Keys.Right or Keys.Left or Keys.Home or Keys.End => true,
                Keys.Up or Keys.Down when Multiline => true,
                _ => !dataGridViewWantsInputKey,
            };
        }
    }

    public partial class DataGridViewComboBoxEditingControl
    {
        /// <summary>Gets the cursor shown over the editing panel behind this control.</summary>
        public Cursor? EditingPanelCursor => Cursors.Default;

        /// <summary>Gets whether the cell should be repainted when the value changes.</summary>
        public bool RepositionEditingControlOnValueChange => false;

        /// <summary>Applies the cell's style to this control.</summary>
        public void ApplyCellStyleToEditingControl (DataGridViewCellStyle dataGridViewCellStyle)
        {
            if (dataGridViewCellStyle is null)
                return;

            Font = dataGridViewCellStyle.Font ?? Font;
            ForeColor = dataGridViewCellStyle.ForeColor;
            BackColor = dataGridViewCellStyle.BackColor;
        }

        /// <summary>Returns whether this control wants the given key rather than the grid.</summary>
        public bool EditingControlWantsInputKey (Keys keyData, bool dataGridViewWantsInputKey)
        {
            // A drop-down wants the vertical keys, which move through its items; the grid keeps the
            // horizontal ones, which move between cells.
            var key = keyData & Keys.KeyCode;

            return key switch {
                Keys.Up or Keys.Down or Keys.PageUp or Keys.PageDown => true,
                _ => !dataGridViewWantsInputKey,
            };
        }
    }
}
