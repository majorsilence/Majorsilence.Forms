using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Diagnostics.CodeAnalysis;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Reflection.Metadata;

namespace Majorsilence.Forms
{
    // The last of the scattered member gaps (docs/winforms-gap-plan.md).
    //
    // The owner-draw event args carry the most weight. DrawListViewItemEventArgs.DrawBackground and
    // friends are how an owner-drawn control gets the default painting for the parts it does not want
    // to draw itself -- "draw the background yourself, I'll do the text" is the normal shape of an
    // owner-draw handler -- so they paint here rather than being no-ops.
    //
    // Screen's static Get* helpers are likewise real: they pick the screen containing the point or
    // control and return its bounds, which is what positioning a popup near the cursor needs.

    public partial class DrawListViewItemEventArgs
    {
        /// <summary>Fills the item's bounds with its background colour.</summary>
        public void DrawBackground ()
        {
            using var brush = new Majorsilence.Forms.Drawing.SolidBrush (
                Item?.BackColor is { IsEmpty: false } back ? back : SystemColors.Window);

            Graphics?.FillRectangle (brush, Bounds);
        }

        /// <summary>Draws the focus rectangle when the item has focus.</summary>
        public void DrawFocusRectangle ()
        {
            if (State.HasFlag (ListViewItemStates.Focused))
                Graphics?.DrawFocusRectangle (Bounds);
        }

        /// <summary>Draws the item's text with the default formatting.</summary>
        public void DrawText () => DrawText (TextFormatFlags.Left);

        /// <inheritdoc cref="DrawText()"/>
        public void DrawText (TextFormatFlags flags)
        {
            if (Graphics is null || Item is null)
                return;

            var color = Item.ForeColor.IsEmpty ? SystemColors.WindowText : Item.ForeColor;
            TextRenderer.DrawText (Graphics, Item.Text, Item.Font ?? Control.DefaultFont, Bounds, color, flags);
        }
    }

    public partial class DrawListViewSubItemEventArgs
    {
        /// <summary>Fills the sub-item's bounds with its background colour.</summary>
        public void DrawBackground ()
        {
            using var brush = new Majorsilence.Forms.Drawing.SolidBrush (
                SubItem?.BackColor is { IsEmpty: false } back ? back : SystemColors.Window);

            Graphics?.FillRectangle (brush, Bounds);
        }

        /// <summary>Draws the focus rectangle inside the given bounds.</summary>
        public void DrawFocusRectangle (Rectangle bounds)
        {
            if (ItemState.HasFlag (ListViewItemStates.Focused))
                Graphics?.DrawFocusRectangle (bounds);
        }

        /// <summary>Draws the sub-item's text with the default formatting.</summary>
        public void DrawText () => DrawText (TextFormatFlags.Left);

        /// <inheritdoc cref="DrawText()"/>
        public void DrawText (TextFormatFlags flags)
        {
            if (Graphics is null || SubItem is null)
                return;

            var color = SubItem.ForeColor.IsEmpty ? SystemColors.WindowText : SubItem.ForeColor;
            TextRenderer.DrawText (Graphics, SubItem.Text, SubItem.Font ?? Control.DefaultFont, Bounds, color, flags);
        }
    }

    public partial class DrawToolTipEventArgs
    {
        /// <summary>Fills the tooltip's bounds with the system tooltip background.</summary>
        public void DrawBackground ()
        {
            using var brush = new Majorsilence.Forms.Drawing.SolidBrush (SystemColors.Info);
            Graphics?.FillRectangle (brush, Bounds);
        }

        /// <summary>Draws the tooltip's border.</summary>
        public void DrawBorder ()
        {
            if (Graphics is null)
                return;

            using var pen = new Majorsilence.Forms.Drawing.Pen (SystemColors.WindowFrame);
            Graphics.DrawRectangle (pen, Bounds.X, Bounds.Y, Bounds.Width - 1, Bounds.Height - 1);
        }

        /// <summary>Draws the tooltip's text with the default formatting.</summary>
        public void DrawText () => DrawText (TextFormatFlags.Left);

        /// <inheritdoc cref="DrawText()"/>
        public void DrawText (TextFormatFlags flags)
        {
            if (Graphics is null)
                return;

            TextRenderer.DrawText (Graphics, ToolTipText ?? string.Empty, Font ?? Control.DefaultFont,
                Bounds, SystemColors.InfoText, flags);
        }
    }

    public partial class Screen
    {
        // The From* lookups are nullable here because a headless run may have no screen at all;
        // Rectangle.Empty is what a caller positioning a window against "no screen" can act on,
        // whereas a null dereference would take the application down.

        /// <summary>Returns the bounds of the screen containing the given point.</summary>
        public static Rectangle GetBounds (Point pt) => FromPoint (pt)?.Bounds ?? Rectangle.Empty;

        /// <summary>Returns the bounds of the screen containing most of the given rectangle.</summary>
        public static Rectangle GetBounds (Rectangle rect) => FromRectangle (rect)?.Bounds ?? Rectangle.Empty;

        /// <summary>Returns the bounds of the screen containing the given control.</summary>
        public static Rectangle GetBounds (Control ctl) => FromControl (ctl)?.Bounds ?? Rectangle.Empty;

        /// <summary>Returns the working area of the screen containing the given point.</summary>
        public static Rectangle GetWorkingArea (Point pt) => FromPoint (pt)?.WorkingArea ?? Rectangle.Empty;

        /// <summary>Returns the working area of the screen containing most of the given rectangle.</summary>
        public static Rectangle GetWorkingArea (Rectangle rect) => FromRectangle (rect)?.WorkingArea ?? Rectangle.Empty;

        /// <summary>Returns the working area of the screen containing the given control.</summary>
        public static Rectangle GetWorkingArea (Control ctl) => FromControl (ctl)?.WorkingArea ?? Rectangle.Empty;
    }

    public partial class DragEventArgs
    {
        /// <summary>Gets or sets the glyph shown beside the pointer during the drag.</summary>
        public DropImageType DropImageType { get; set; } = DropImageType.Invalid;

        /// <summary>Gets or sets the message shown beside the pointer during the drag.</summary>
        public string? Message { get; set; }

        /// <summary>Gets or sets the text substituted into <see cref="Message"/>'s placeholder.</summary>
        public string? MessageReplacementToken { get; set; }
    }

    public partial class GiveFeedbackEventArgs
    {
        /// <summary>Gets or sets the image dragged alongside the pointer.</summary>
        public Majorsilence.Forms.Drawing.Bitmap? DragImage { get; set; }

        /// <summary>Gets or sets where the pointer sits within <see cref="DragImage"/>.</summary>
        public Point CursorOffset { get; set; }

        /// <summary>Gets or sets whether the system supplies the drag image.</summary>
        public bool UseDefaultDragImage { get; set; }
    }

    public partial class TreeNodeCollection
    {
        /// <summary>Gets whether this collection can be modified.</summary>
        public bool IsReadOnly => false;

        /// <summary>Adds several nodes at once.</summary>
        public virtual void AddRange (params TreeNode[] nodes)
        {
            Guard.ThrowIfNull (nodes);

            foreach (var node in nodes)
                Add (node);
        }

        /// <summary>Returns the index of the node with the given key, or -1.</summary>
        public virtual int IndexOfKey (string key)
        {
            if (string.IsNullOrEmpty (key))
                return -1;

            for (var i = 0; i < Count; i++)
                if (string.Equals (this[i].Name, key, StringComparison.OrdinalIgnoreCase))
                    return i;

            return -1;
        }
    }

    public partial class HelpProvider
    {
        /// <summary>Returns whether this provider can supply help for the given object.</summary>
        public virtual bool CanExtend (object? target) => target is Control;

        /// <summary>Returns the navigator used when help is shown for the given control.</summary>
        public virtual HelpNavigator GetHelpNavigator (Control ctl) => HelpNavigator.AssociateIndex;

        /// <summary>Stops showing help for the given control.</summary>
        public virtual void ResetShowHelp (Control ctl) => SetShowHelp (ctl, false);
    }

    public partial class ErrorProvider
    {
        /// <summary>Returns whether this provider can show an error for the given object.</summary>
        public bool CanExtend (object? extendee) => extendee is Control;

        /// <summary>Binds the provider to a data source and its error information.</summary>
        public void BindToDataAndErrors (object? newDataSource, string? newDataMember)
        {
            DataSource = newDataSource;
            DataMember = newDataMember ?? string.Empty;
        }

        /// <summary>Re-reads the error information from the data source.</summary>
        public void UpdateBinding () { }
    }

    public partial class BindingNavigator
    {
        /// <summary>Gets or sets the format string used for the item count.</summary>
        public string CountItemFormat { get; set; } = "of {0}";

        /// <summary>Adds the standard navigation items to the strip.</summary>
        /// <remarks>
        /// WinForms builds move-first, move-previous, a position box, move-next, move-last, add and
        /// delete, named as upstream names them so designer code assigning to MoveFirstItem and
        /// friends finds them. It does NOT remove existing items (BND-12) -- upstream's own remark --
        /// because InitializeComponent adds the designer's items (custom Save buttons included) and
        /// wires their handlers BEFORE EndInit runs: clearing here replaced all of them with unwired
        /// copies, so the Save button vanished and its Click handler held an orphan. A navigator that
        /// already has items keeps them, and the standard set is only built into an empty strip.
        /// </remarks>
        public virtual void AddStandardItems ()
        {
            if (Items.Count > 0)
                return;

            MoveFirstItem = Add ("bindingNavigatorMoveFirstItem", "Move first");
            MovePreviousItem = Add ("bindingNavigatorMovePreviousItem", "Move previous");
            PositionItem = new ToolStripTextBox { Name = "bindingNavigatorPositionItem", Text = "0" };
            Items.Add (PositionItem);
            CountItem = new ToolStripLabel { Name = "bindingNavigatorCountItem" };
            Items.Add (CountItem);
            MoveNextItem = Add ("bindingNavigatorMoveNextItem", "Move next");
            MoveLastItem = Add ("bindingNavigatorMoveLastItem", "Move last");
            AddNewItem = Add ("bindingNavigatorAddNewItem", "Add new");
            DeleteItem = Add ("bindingNavigatorDeleteItem", "Delete");

            RefreshItemsCore ();

            ToolStripButton Add (string name, string text)
            {
                var button = new ToolStripButton { Name = name, Text = text };
                Items.Add (button);
                return button;
            }
        }

        /// <summary>Signals that initialization is starting.</summary>
        public void BeginInit () { }

        /// <summary>Signals that initialization has finished.</summary>
        /// <remarks>A refresh, nothing more (BND-12): this used to call
        /// <see cref="AddStandardItems"/>, which cleared and rebuilt the strip, destroying the very
        /// items InitializeComponent had just assembled.</remarks>
        public void EndInit () => RefreshItemsCore ();

        /// <summary>Raised when the navigator refreshes its items from the source.</summary>
        public event EventHandler? RefreshItems;

        // What the navigator SHOWS: position as 1-based text, the count through CountItemFormat, and
        // each button enabled only when its move can do anything (BND-11).
        internal void RefreshItemsCore ()
        {
            var source = BindingSource;
            var count = source?.Count ?? 0;
            var position = source?.Position ?? -1;

            if (PositionItem is not null)
                PositionItem.Text = (position + 1).ToString (System.Globalization.CultureInfo.CurrentCulture);

            if (CountItem is not null)
                CountItem.Text = string.Format (System.Globalization.CultureInfo.CurrentCulture, CountItemFormat, count);

            SetEnabled (MoveFirstItem, position > 0);
            SetEnabled (MovePreviousItem, position > 0);
            SetEnabled (MoveNextItem, position < count - 1);
            SetEnabled (MoveLastItem, position < count - 1);
            SetEnabled (AddNewItem, source?.AllowNew ?? false);
            SetEnabled (DeleteItem, (source?.AllowRemove ?? false) && count > 0);

            RefreshItems?.Invoke (this, EventArgs.Empty);

            static void SetEnabled (ToolStripButton? item, bool enabled)
            {
                if (item is not null)
                    item.Enabled = enabled;
            }
        }
    }

    public partial class ToolStripPanel
    {
        /// <summary>Signals that initialization is starting.</summary>
        public void BeginInit () { }

        /// <summary>Signals that initialization has finished.</summary>
        public void EndInit () => PerformLayout ();
    }

    public partial class NumericUpDown
    {
        /// <summary>Gets the accelerations applied when the up or down button is held.</summary>
        public NumericUpDownAccelerationCollection Accelerations => accelerations ??= [];

        private NumericUpDownAccelerationCollection? accelerations;

        /// <summary>Signals that initialization is starting.</summary>
        public void BeginInit () { }

        /// <summary>Signals that initialization has finished.</summary>
        public void EndInit () { }
    }

    /// <summary>How fast a <see cref="NumericUpDown"/> changes once its button has been held.</summary>
    public class NumericUpDownAcceleration
    {
        /// <summary>Initializes a new instance of the <see cref="NumericUpDownAcceleration"/> class.</summary>
        public NumericUpDownAcceleration (int seconds, decimal increment)
        {
            Guard.ThrowIfNegative (seconds);
            Guard.ThrowIfNegative (increment);

            Seconds = seconds;
            Increment = increment;
        }

        /// <summary>Gets or sets how long the button must be held before this acceleration applies.</summary>
        public int Seconds { get; set; }

        /// <summary>Gets or sets the amount added per step once it applies.</summary>
        public decimal Increment { get; set; }
    }

    /// <summary>The accelerations of a <see cref="NumericUpDown"/>, kept sorted by duration.</summary>
    public partial class NumericUpDownAccelerationCollection : System.Collections.ObjectModel.Collection<NumericUpDownAcceleration>
    {
        /// <summary>Adds several accelerations at once.</summary>
        public void AddRange (params NumericUpDownAcceleration[] accelerations)
        {
            Guard.ThrowIfNull (accelerations);

            foreach (var acceleration in accelerations)
                Add (acceleration);

            Sort ();
        }

        /// <inheritdoc/>
        protected override void InsertItem (int index, NumericUpDownAcceleration item)
        {
            Guard.ThrowIfNull (item);

            base.InsertItem (index, item);
            Sort ();
        }

        // WinForms keeps the collection ordered by Seconds so the control can walk it forwards and
        // stop at the first entry whose threshold the hold has not yet reached.
        private void Sort ()
        {
            var ordered = Items.OrderBy (a => a.Seconds).ToList ();

            for (var i = 0; i < ordered.Count; i++)
                Items[i] = ordered[i];
        }
    }

    public partial class OpenFileDialog
    {
        /// <summary>Gets or sets whether the read-only check box is ticked.</summary>
        public bool SelectReadOnly { get; set; }

        /// <summary>Gets or sets whether the dialog shows a preview pane.</summary>
        /// <remarks>Stored: the backends show their platform's file picker, which has its own preview
        /// and does not take direction on it.</remarks>
        public bool ShowPreview { get; set; }

        /// <summary>Opens the chosen file for reading.</summary>
        public Stream OpenFile ()
        {
            if (string.IsNullOrEmpty (FileName))
                throw new InvalidOperationException ("No file has been chosen.");

            return File.OpenRead (FileName);
        }
    }

    public partial class SaveFileDialog
    {
        /// <summary>Gets or sets whether the dialog checks that the file can be written before closing.</summary>
        public bool CheckWriteAccess { get; set; } = true;

        /// <summary>Gets or sets whether the dialog opens in its expanded form.</summary>
        public bool ExpandedMode { get; set; } = true;

        /// <summary>Creates or truncates the chosen file and opens it for writing.</summary>
        public Stream OpenFile ()
        {
            if (string.IsNullOrEmpty (FileName))
                throw new InvalidOperationException ("No file has been chosen.");

            return File.Create (FileName);
        }
    }

    public partial class DataObject
    {
        /// <inheritdoc cref="TryGetData{T}(string,out T)"/>
        public bool TryGetData<T> (string format, bool autoConvert, out T? data) => TryGetData (format, out data);

        /// <inheritdoc cref="TryGetData{T}(string,out T)"/>
        /// <remarks>The resolver maps a stored type name to the type to deserialise as. Nothing here
        /// serialises by type name -- values are stored as objects -- so it is never consulted.</remarks>
        public bool TryGetData<T> (string format, Func<TypeName, Type> resolver, bool autoConvert, out T? data)
            => TryGetData (format, out data);
    }

    public partial class Clipboard
    {
        /// <inheritdoc cref="TryGetData{T}(string,out T)"/>
        public static bool TryGetData<T> (string format, Func<TypeName, Type> resolver, out T? data)
            => TryGetData (format, out data);
    }
}
