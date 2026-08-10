using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Linq;

using Font = Majorsilence.Forms.Drawing.Font;

namespace Majorsilence.Forms
{
    // DataGridViewCell and DataGridViewRowCollection parity (docs/winforms-gap-plan.md).
    //
    // The row collection's half is the more interesting one: WinForms' Get*Row family is how you walk
    // a grid by state -- "the first visible row", "the next selected row after this one" -- and all
    // eight were absent, so that walk had to be written by hand at every call site against a
    // DataGridViewElementStates value the caller had to decode itself. They are implemented here
    // against the row's own state, not stubbed.
    //
    // The cell's half is mostly measurement and geometry, which this control already computes for
    // painting; those members expose it. The editing-control hooks (PositionEditingControl,
    // DetachEditingControl) are virtual seams for derived cells: this grid hosts its editor itself,
    // so the base implementations do the arithmetic and leave the hosting alone.

    public partial class DataGridViewCell : IDisposable
    {
        private AccessibleObject? accessibility_object;

        /// <summary>Gets the accessible object describing this cell.</summary>
        public AccessibleObject AccessibilityObject
            => accessibility_object ??= new DataGridViewCellAccessibleObject (this);

        /// <summary>Gets the area of the cell its content is drawn in, inside the borders.</summary>
        public Rectangle ContentBounds => GetContentBounds (RowIndex);

        /// <summary>Gets the area the error glyph is drawn in.</summary>
        public Rectangle ErrorIconBounds {
            get {
                if (string.IsNullOrEmpty (ErrorText))
                    return Rectangle.Empty;

                const int glyph = 12;
                var bounds = Bounds;
                return new Rectangle (bounds.Right - glyph - 2, bounds.Y + (bounds.Height - glyph) / 2, glyph, glyph);
            }
        }

        /// <summary>Gets or sets the context menu shown when this cell is right-clicked.</summary>
        public virtual ContextMenuStrip? ContextMenuStrip { get; set; }

        /// <summary>Gets whether the cell is currently on screen.</summary>
        public virtual bool Displayed => Visible && DataGridView is not null && !Bounds.IsEmpty;

        /// <summary>Gets whether the cell's row or column is frozen.</summary>
        public virtual bool Frozen => OwningColumn?.Frozen == true || OwningRow?.Frozen == true;

        /// <summary>Gets whether the cell's row is resizable.</summary>
        public virtual bool Resizable => OwningRow is null || OwningRow.Resizable != DataGridViewTriState.False;

        /// <summary>Gets the type <see cref="FormattedValue"/> has.</summary>
        public virtual Type FormattedValueType => typeof (string);

        /// <summary>Gets whether a style has been set on this cell rather than inherited.</summary>
        public bool HasStyle => has_explicit_style;

        /// <summary>Gets the size of the cell.</summary>
        public Size Size => Bounds.Size;

        /// <summary>Gets the size the cell would like to be, given its content.</summary>
        public Size PreferredSize {
            get {
                if (DataGridView is null)
                    return Size.Empty;

                var text = FormattedValue?.ToString () ?? string.Empty;
                var measured = TextMeasurer.MeasureText (text, DataGridView);
                return new Size ((int)Math.Ceiling (measured.Width) + 8, (int)Math.Ceiling (measured.Height) + 4);
            }
        }

        /// <summary>Returns the area the cell's content is drawn in for the given row.</summary>
        public Rectangle GetContentBounds (int rowIndex)
        {
            if (DataGridView is null || rowIndex < 0)
                return Rectangle.Empty;

            var bounds = Bounds;
            return Rectangle.Inflate (bounds, -2, -1);
        }

        /// <summary>Returns the cell's value as it is being edited, or its formatted value when it is not.</summary>
        public object? GetEditedFormattedValue (int rowIndex, DataGridViewDataErrorContexts context)
            => IsInEditMode ? EditedFormattedValue : FormattedValue;

        /// <summary>Returns the context menu this cell uses, falling back to its row, column and grid.</summary>
        public virtual ContextMenuStrip? GetInheritedContextMenuStrip (int rowIndex)
            => ContextMenuStrip ?? DataGridView?.ContextMenuStrip;

        /// <summary>Returns the cell's state combined with the state it inherits from its row and column.</summary>
        public virtual DataGridViewElementStates GetInheritedState (int rowIndex)
        {
            var state = DataGridViewElementStates.ResizableSet;

            if (Visible)
                state |= DataGridViewElementStates.Visible;
            if (Selected)
                state |= DataGridViewElementStates.Selected;
            if (ReadOnly || DataGridView?.ReadOnly == true)
                state |= DataGridViewElementStates.ReadOnly;
            if (Frozen)
                state |= DataGridViewElementStates.Frozen;
            if (Displayed)
                state |= DataGridViewElementStates.Displayed;
            if (Resizable)
                state |= DataGridViewElementStates.Resizable;

            return state;
        }

        /// <summary>Returns the style this cell paints with, given the styles it inherits.</summary>
        public virtual DataGridViewCellStyle GetInheritedStyle (DataGridViewCellStyle? inheritedCellStyle, int rowIndex, bool includeColors)
            => inheritedCellStyle ?? InheritedStyle;

        /// <summary>Returns whether the given key should put this cell into edit mode.</summary>
        public virtual bool KeyEntersEditMode (KeyEventArgs e)
        {
            ArgumentNullException.ThrowIfNull (e);

            if (ReadOnly || e.Alt || e.Control)
                return false;

            var key = e.KeyCode;

            return key is >= Keys.A and <= Keys.Z
                or >= Keys.D0 and <= Keys.D9
                or >= Keys.NumPad0 and <= Keys.NumPad9
                or Keys.Space or Keys.F2 or Keys.OemMinus or Keys.Oemplus or Keys.OemPeriod;
        }

        /// <summary>Returns the height the given text needs at the given width.</summary>
        public static int MeasureTextHeight (Graphics graphics, string text, Font font, int maxWidth, TextFormatFlags flags)
        {
            var truncated = false;
            return MeasureTextHeight (graphics, text, font, maxWidth, flags, ref truncated);
        }

        /// <inheritdoc cref="MeasureTextHeight(Graphics,string,Font,int,TextFormatFlags)"/>
        public static int MeasureTextHeight (Graphics graphics, string text, Font font, int maxWidth, TextFormatFlags flags, ref bool widthTruncated)
        {
            ArgumentOutOfRangeException.ThrowIfNegativeOrZero (maxWidth);

            var unconstrained = MeasureTextSize (graphics, text, font, flags);
            widthTruncated = unconstrained.Width > maxWidth;

            var wrapped = TextMeasurer.MeasureText (text ?? string.Empty, ToTypeface (font), FontSize (font), new Size (maxWidth, int.MaxValue));
            return (int)Math.Ceiling (wrapped.Height);
        }

        /// <summary>Returns the width the given text needs at the given height.</summary>
        public static int MeasureTextWidth (Graphics graphics, string text, Font font, int maxHeight, TextFormatFlags flags)
        {
            ArgumentOutOfRangeException.ThrowIfNegativeOrZero (maxHeight);

            var size = MeasureTextSize (graphics, text, font, flags);
            return size.Width;
        }

        /// <summary>Returns the size the given text needs on one line.</summary>
        public static Size MeasureTextSize (Graphics graphics, string text, Font font, TextFormatFlags flags)
        {
            var size = TextMeasurer.MeasureText (text ?? string.Empty, ToTypeface (font), FontSize (font));
            return new Size ((int)Math.Ceiling (size.Width), (int)Math.Ceiling (size.Height));
        }

        /// <summary>Returns the size the given text prefers, wrapped no wider than the given ratio.</summary>
        public static Size MeasureTextPreferredSize (Graphics graphics, string text, Font font, float maxRatio, TextFormatFlags flags)
        {
            ArgumentOutOfRangeException.ThrowIfNegativeOrZero (maxRatio);

            var single = MeasureTextSize (graphics, text, font, flags);

            // Already within the requested width-to-height ratio: one line is the preferred shape.
            if (single.Height == 0 || single.Width <= single.Height * maxRatio)
                return single;

            var width = (int)Math.Ceiling (Math.Sqrt (single.Width * (double)single.Height * maxRatio));
            var height = MeasureTextHeight (graphics, text, font, Math.Max (1, width), flags);
            return new Size (width, height);
        }

        /// <summary>Converts a formatted value back into the cell's value type.</summary>
        [System.Diagnostics.CodeAnalysis.UnconditionalSuppressMessage ("Trimming", "IL2026:RequiresUnreferencedCode",
            Justification = "Cell value types are supplied by the application and are not trim-safe by design -- same as WinForms.")]
        [System.Diagnostics.CodeAnalysis.UnconditionalSuppressMessage ("Trimming", "IL2072:DynamicallyAccessedMembers",
            Justification = "See above: ValueType comes from the bound data, so it cannot be annotated here.")]
        public virtual object? ParseFormattedValue (object? formattedValue, DataGridViewCellStyle? cellStyle,
            TypeConverter? formattedValueTypeConverter, TypeConverter? valueTypeConverter)
        {
            var target = ValueType;

            if (formattedValue is null || target is null)
                return formattedValue;

            if (target.IsInstanceOfType (formattedValue))
                return formattedValue;

            if (valueTypeConverter is not null && valueTypeConverter.CanConvertFrom (formattedValue.GetType ()))
                return valueTypeConverter.ConvertFrom (formattedValue);

            var text = formattedValue as string ?? formattedValue.ToString ();
            var converter = TypeDescriptor.GetConverter (target);

            return converter.CanConvertFrom (typeof (string)) && text is not null
                ? converter.ConvertFromString (text)
                : formattedValue;
        }

        /// <summary>Returns the rectangle an editing control should occupy for this cell.</summary>
        public virtual Rectangle PositionEditingPanel (Rectangle cellBounds, Rectangle cellClip,
            DataGridViewCellStyle? cellStyle, bool singleVerticalBorderAdded, bool singleHorizontalBorderAdded,
            bool isFirstDisplayedColumn, bool isFirstDisplayedRow)
        {
            var editing = Rectangle.Intersect (cellBounds, cellClip);

            // The borders are drawn inside the cell, so the editor sits one pixel in on the edges that
            // actually have one.
            if (!isFirstDisplayedColumn || singleVerticalBorderAdded) {
                editing.X += 1;
                editing.Width = Math.Max (0, editing.Width - 1);
            }

            if (!isFirstDisplayedRow || singleHorizontalBorderAdded) {
                editing.Y += 1;
                editing.Height = Math.Max (0, editing.Height - 1);
            }

            return editing;
        }

        /// <summary>Places the editing control over this cell.</summary>
        public virtual void PositionEditingControl (bool setLocation, bool setSize, Rectangle cellBounds,
            Rectangle cellClip, DataGridViewCellStyle? cellStyle, bool singleVerticalBorderAdded,
            bool singleHorizontalBorderAdded, bool isFirstDisplayedColumn, bool isFirstDisplayedRow)
        {
            if (DataGridView?.EditingControl is not Control editor)
                return;

            var bounds = PositionEditingPanel (cellBounds, cellClip, cellStyle, singleVerticalBorderAdded,
                singleHorizontalBorderAdded, isFirstDisplayedColumn, isFirstDisplayedRow);

            if (setLocation)
                editor.Location = bounds.Location;

            if (setSize)
                editor.Size = bounds.Size;
        }

        /// <summary>Releases the editing control from this cell.</summary>
        public virtual void DetachEditingControl () { }

        /// <summary>Returns the border style to use for this cell, given the grid's settings.</summary>
        public virtual DataGridViewAdvancedBorderStyle AdjustCellBorderStyle (
            DataGridViewAdvancedBorderStyle dataGridViewAdvancedBorderStyleInput,
            DataGridViewAdvancedBorderStyle dataGridViewAdvancedBorderStylePlaceholder,
            bool singleVerticalBorderAdded, bool singleHorizontalBorderAdded,
            bool isFirstDisplayedColumn, bool isFirstDisplayedRow)
        {
            ArgumentNullException.ThrowIfNull (dataGridViewAdvancedBorderStyleInput);
            ArgumentNullException.ThrowIfNull (dataGridViewAdvancedBorderStylePlaceholder);

            var result = dataGridViewAdvancedBorderStylePlaceholder;

            result.Top = dataGridViewAdvancedBorderStyleInput.Top;
            result.Bottom = dataGridViewAdvancedBorderStyleInput.Bottom;
            result.Left = dataGridViewAdvancedBorderStyleInput.Left;
            result.Right = dataGridViewAdvancedBorderStyleInput.Right;

            // Interior cells do not draw their leading edges: the neighbour above and to the left
            // already drew that line, and drawing it twice is what makes a grid look bold in places.
            if (!isFirstDisplayedColumn && !singleVerticalBorderAdded)
                result.Left = DataGridViewAdvancedCellBorderStyle.None;

            if (!isFirstDisplayedRow && !singleHorizontalBorderAdded)
                result.Top = DataGridViewAdvancedCellBorderStyle.None;

            return result;
        }

        /// <summary>Releases the resources used by this cell.</summary>
        public void Dispose ()
        {
            Dispose (disposing: true);
            GC.SuppressFinalize (this);
        }

        /// <summary>Releases the resources used by this cell.</summary>
        protected virtual void Dispose (bool disposing) { }

        private static SkiaSharp.SKTypeface ToTypeface (Font? font)
            => font is null ? SkiaSharp.SKTypeface.Default : (TypefaceCache.Get (font.Name) ?? SkiaSharp.SKTypeface.Default);

        private static int FontSize (Font? font) => (int)Math.Round (font?.Size ?? 12f);
    }

    /// <summary>Exposes a <see cref="DataGridViewCell"/> to accessibility clients.</summary>
    public class DataGridViewCellAccessibleObject : AccessibleObject
    {
        private readonly DataGridViewCell cell;

        /// <summary>Initializes a new instance of the <see cref="DataGridViewCellAccessibleObject"/> class.</summary>
        public DataGridViewCellAccessibleObject (DataGridViewCell owner) => cell = owner;

        /// <summary>Gets the cell's formatted value, which is what a screen reader announces.</summary>
        public override string? Name => cell.FormattedValue?.ToString ();

        /// <summary>Gets the role reported to assistive technology.</summary>
        public override AccessibleRole Role => AccessibleRole.Cell;

        /// <summary>Gets the bounds of the cell.</summary>
        public override Rectangle Bounds => cell.Bounds;
    }

    public partial class DataGridViewRowCollection
    {
        /// <summary>Raised when rows are added to or removed from the collection.</summary>
        public event CollectionChangeEventHandler? CollectionChanged;

        /// <summary>Adds several rows at once.</summary>
        public virtual void AddRange (params DataGridViewRow[] dataGridViewRows)
        {
            ArgumentNullException.ThrowIfNull (dataGridViewRows);

            foreach (var row in dataGridViewRows)
                Add (row);
        }

        /// <summary>Adds a copy of an existing row, and returns the new row's index.</summary>
        public virtual int AddCopy (int indexSource) => AddCopies (indexSource, 1);

        /// <summary>Adds several copies of an existing row, and returns the index of the last one.</summary>
        public virtual int AddCopies (int indexSource, int count)
        {
            RequireIndex (indexSource, nameof (indexSource));
            ArgumentOutOfRangeException.ThrowIfNegativeOrZero (count);

            var last = -1;

            for (var i = 0; i < count; i++)
                last = Add (CopyOf (indexSource));

            return last;
        }

        /// <summary>Inserts several rows at once.</summary>
        public virtual void InsertRange (int rowIndex, params DataGridViewRow[] dataGridViewRows)
        {
            ArgumentNullException.ThrowIfNull (dataGridViewRows);
            ArgumentOutOfRangeException.ThrowIfNegative (rowIndex);
            ArgumentOutOfRangeException.ThrowIfGreaterThan (rowIndex, Count);

            for (var i = 0; i < dataGridViewRows.Length; i++)
                Insert (rowIndex + i, dataGridViewRows[i]);
        }

        /// <summary>Inserts a copy of an existing row.</summary>
        public virtual void InsertCopy (int indexSource, int indexDestination)
            => InsertCopies (indexSource, indexDestination, 1);

        /// <summary>Inserts several copies of an existing row.</summary>
        public virtual void InsertCopies (int indexSource, int indexDestination, int count)
        {
            RequireIndex (indexSource, nameof (indexSource));
            ArgumentOutOfRangeException.ThrowIfNegative (indexDestination);
            ArgumentOutOfRangeException.ThrowIfGreaterThan (indexDestination, Count);
            ArgumentOutOfRangeException.ThrowIfNegativeOrZero (count);

            for (var i = 0; i < count; i++)
                Insert (indexDestination + i, CopyOf (indexSource));
        }

        /// <summary>Returns the row at the given index without unsharing it.</summary>
        /// <remarks>Row sharing is a memory optimisation this layer does not implement, so this returns
        /// the same row the indexer does. It exists because WinForms code uses it to read a row without
        /// paying the unshare cost, and that code should keep working.</remarks>
        public DataGridViewRow SharedRow (int rowIndex)
        {
            RequireIndex (rowIndex, nameof (rowIndex));
            return this[rowIndex];
        }

        /// <summary>Returns the state of the row at the given index.</summary>
        public virtual DataGridViewElementStates GetRowState (int rowIndex)
        {
            RequireIndex (rowIndex, nameof (rowIndex));

            var row = this[rowIndex];
            var state = DataGridViewElementStates.None;

            if (row.Visible)
                state |= DataGridViewElementStates.Visible;
            if (row.Selected)
                state |= DataGridViewElementStates.Selected;
            if (row.ReadOnly)
                state |= DataGridViewElementStates.ReadOnly;
            if (row.Frozen)
                state |= DataGridViewElementStates.Frozen;
            if (row.Resizable != DataGridViewTriState.False)
                state |= DataGridViewElementStates.Resizable;

            return state;
        }

        /// <summary>Returns the index of the first row matching the filter, or -1.</summary>
        public int GetFirstRow (DataGridViewElementStates includeFilter)
            => GetNextRow (-1, includeFilter);

        /// <inheritdoc cref="GetFirstRow(DataGridViewElementStates)"/>
        public int GetFirstRow (DataGridViewElementStates includeFilter, DataGridViewElementStates excludeFilter)
            => GetNextRow (-1, includeFilter, excludeFilter);

        /// <summary>Returns the index of the last row matching the filter, or -1.</summary>
        public int GetLastRow (DataGridViewElementStates includeFilter)
            => GetPreviousRow (Count, includeFilter);

        /// <summary>Returns the index of the first row after <paramref name="indexStart"/> matching the filter, or -1.</summary>
        public int GetNextRow (int indexStart, DataGridViewElementStates includeFilter)
            => GetNextRow (indexStart, includeFilter, DataGridViewElementStates.None);

        /// <inheritdoc cref="GetNextRow(int,DataGridViewElementStates)"/>
        public int GetNextRow (int indexStart, DataGridViewElementStates includeFilter, DataGridViewElementStates excludeFilter)
        {
            ArgumentOutOfRangeException.ThrowIfLessThan (indexStart, -1);

            for (var i = indexStart + 1; i < Count; i++)
                if (Matches (i, includeFilter, excludeFilter))
                    return i;

            return -1;
        }

        /// <summary>Returns the index of the last row before <paramref name="indexStart"/> matching the filter, or -1.</summary>
        public int GetPreviousRow (int indexStart, DataGridViewElementStates includeFilter)
            => GetPreviousRow (indexStart, includeFilter, DataGridViewElementStates.None);

        /// <inheritdoc cref="GetPreviousRow(int,DataGridViewElementStates)"/>
        public int GetPreviousRow (int indexStart, DataGridViewElementStates includeFilter, DataGridViewElementStates excludeFilter)
        {
            ArgumentOutOfRangeException.ThrowIfGreaterThan (indexStart, Count);

            for (var i = Math.Min (indexStart, Count) - 1; i >= 0; i--)
                if (Matches (i, includeFilter, excludeFilter))
                    return i;

            return -1;
        }

        /// <summary>Returns how many rows match the filter.</summary>
        public int GetRowCount (DataGridViewElementStates includeFilter)
        {
            var count = 0;

            for (var i = 0; i < Count; i++)
                if (Matches (i, includeFilter, DataGridViewElementStates.None))
                    count++;

            return count;
        }

        /// <summary>Returns the combined height of the rows matching the filter.</summary>
        public int GetRowsHeight (DataGridViewElementStates includeFilter)
        {
            var height = 0;

            for (var i = 0; i < Count; i++)
                if (Matches (i, includeFilter, DataGridViewElementStates.None))
                    height += this[i].Height;

            return height;
        }

        /// <summary>Raises the <see cref="CollectionChanged"/> event.</summary>
        protected virtual void OnCollectionChanged (CollectionChangeEventArgs e) => CollectionChanged?.Invoke (this, e);

        private bool Matches (int index, DataGridViewElementStates includeFilter, DataGridViewElementStates excludeFilter)
        {
            var state = GetRowState (index);

            // WinForms treats the include filter as "has all of these" and the exclude filter as "has
            // none of these"; None as an include filter matches every row.
            return (includeFilter == DataGridViewElementStates.None || (state & includeFilter) == includeFilter)
                && (excludeFilter == DataGridViewElementStates.None || (state & excludeFilter) == 0);
        }

        private DataGridViewRow CopyOf (int indexSource) => (DataGridViewRow)this[indexSource].Clone ();

        private void RequireIndex (int index, string name)
        {
            ArgumentOutOfRangeException.ThrowIfNegative (index, name);
            ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual (index, Count, name);
        }
    }
}
