using System.Collections.Generic;
﻿using System.Drawing;

namespace Majorsilence.Forms
{
    /// <summary>
    /// Represents a row in a DataGridView control.
    /// </summary>
    public partial class DataGridViewRow
    {
        private int height = 22;
        private DataGridView? owner;

        /// <summary>
        /// Initializes a new instance of the DataGridViewRow class.
        /// </summary>
        public DataGridViewRow ()
        {
            Cells = new DataGridViewCellCollection (this);
        }

        /// <summary>
        /// Gets the bounding rectangle of the row.
        /// </summary>
        internal Rectangle Bounds { get; set; }

        /// <summary>
        /// Gets the collection of cells in this row.
        /// </summary>
        public DataGridViewCellCollection Cells { get; }

        /// <summary>
        /// Gets or sets the header cell for this row.
        /// </summary>
        /// <remarks>Settable, as upstream's is; a replacement notifies the grid (W6.1, DGV-45).</remarks>
        public DataGridViewRowHeaderCell HeaderCell {
            get => header_cell;
            set {
                if (ReferenceEquals (header_cell, value))
                    return;

                header_cell = value ?? new DataGridViewRowHeaderCell ();
                DataGridView?.NotifyRowHeaderCellChanged (this);
            }
        }

        private DataGridViewRowHeaderCell header_cell = new DataGridViewRowHeaderCell ();

        /// <summary>
        /// Gets the DataGridView that contains this row.
        /// </summary>
        public DataGridView? DataGridView => owner;

        /// <summary>
        /// Gets or sets the height, in pixels, of the row.
        /// </summary>
        public int Height {
            get {
                // An unbound virtual-mode grid asks the application for row heights (W6 mechanisms).
                if (owner is { IsVirtualUnbound: true } virtual_grid && Index is >= 0 and var index)
                    return virtual_grid.RaiseRowHeightInfoNeeded (index, height, MinimumRowHeight);

                return height;
            }
            set {
                // ...and is told about a change first; a handler that stores it itself marks the push
                // Handled and the row keeps its own figure for the next Needed to overwrite.
                if (owner is { IsVirtualUnbound: true } virtual_grid && Index is >= 0 and var pushed_index
                    && virtual_grid.RaiseRowHeightInfoPushed (pushed_index, value, MinimumRowHeight))
                    return;

                if (height != value) {
                    height = Math.Max (value, MinimumRowHeight);
                    owner?.OnRowsChanged ();
                    owner?.RaiseRowHeightChanged (this);
                }
            }
        }

        // The floor the setter has always applied; named so the RowHeightInfo pair can report it.
        private const int MinimumRowHeight = 3;

        /// <summary>
        /// Gets the index of this row in the DataGridView.
        /// </summary>
        public int Index => owner?.Rows.IndexOf (this) ?? -1;

        /// <summary>
        /// Gets or sets whether this row is selected. Setting it selects or deselects the row in the
        /// owning grid, which repaints and raises <see cref="DataGridView.SelectionChanged"/>; with
        /// <see cref="DataGridView.MultiSelect"/> off, selecting this row deselects everything else.
        /// A row that belongs to no grid simply stores the value.
        /// </summary>
        public bool Selected {
            get => selected;
            set {
                if (owner is { } grid)
                    grid.SetRowSelected (this, value);
                else
                    SetSelectedCore (value, 0);
            }
        }

        private bool selected;

        // The grid's SetRowSelected writes through here, so the flag and the recency stamp are always
        // set together. Assigning `selected` anywhere else would leave SelectedRows mis-ordered.
        internal void SetSelectedCore (bool value, long order)
        {
            selected = value;
            selection_order = order;
        }

        // When this row was most recently selected, as handed out by the grid. Zero when unselected.
        // DataGridView.SelectedRows sorts on it so the most recently selected row comes first, the way
        // upstream's prepend-to-a-linked-list does.
        internal long SelectionOrder => selection_order;

        private long selection_order;

        /// <summary>
        /// Gets or sets an object that contains data to associate with the row.
        /// </summary>
        public object? Tag { get; set; }

        /// <summary>
        /// Gets or sets the data-source object bound to this row.
        /// Set automatically when AutoGenerateColumns=false and DataSource is assigned.
        /// </summary>
        public object? DataBoundItem { get; set; }

        /// <summary>Gets whether this row is the new-row placeholder. Always false in Majorsilence.Forms.</summary>
        public bool IsNewRow => false;

        /// <summary>Gets or sets whether all cells in this row are read-only.</summary>
        public bool ReadOnly {
            get => read_only;
            set {
                read_only = value;

                // WinForms semantics: setting the row's ReadOnly cascades to every cell. Clearing it
                // also clears the cell-level flags.
                foreach (var cell in Cells)
                    cell.ReadOnly = value;
            }
        }

        private bool read_only;

        private int minimum_height = 10;

        /// <summary>Gets or sets the minimum height for this row.</summary>
        /// <remarks>Notifies the owning grid on change, as upstream's setter does (W6.1, DGV-45).</remarks>
        public int MinimumHeight {
            get => minimum_height;
            set {
                if (EqualityComparer<int>.Default.Equals (minimum_height, value))
                    return;

                minimum_height = value;
                DataGridView?.NotifyRowMinimumHeightChanged (this);
            }
        }

        private DataGridViewCellStyle default_cell_style = new DataGridViewCellStyle ();

        /// <summary>Gets or sets the default cell style applied to cells in this row.</summary>
        /// <remarks>Notifies the owning grid on change, as upstream's setter does (W6.1, DGV-45).</remarks>
        public DataGridViewCellStyle DefaultCellStyle {
            get {
                // See DataGridViewColumn.DefaultCellStyle: attached on every read (W6 mechanisms).
                default_cell_style.Attach (owner, DataGridViewCellStyleScopes.Row);
                return default_cell_style;
            }
            set {
                // ReferenceEquals, not value equality: DataGridViewCellStyle compares by value, so a
                // freshly constructed style "equals" the default one and a value comparison swallowed
                // every replacement -- the event never fired at all. Upstream fires on assignment of a
                // different instance, whatever its contents.
                if (ReferenceEquals (default_cell_style, value))
                    return;

                default_cell_style = value ?? new DataGridViewCellStyle ();
                default_cell_style.Attach (owner, DataGridViewCellStyleScopes.Row);
                DataGridView?.NotifyRowDefaultCellStyleChanged (this);
            }
        }

        /// <summary>Gets or sets whether this row is visible.</summary>
        /// <remarks>
        /// A hidden row is excluded from layout, painting, hit-testing and scrolling, because it has no
        /// height (see <c>DataGridView.RowDeviceHeight</c>). It used to be a plain auto-property that
        /// only <c>InheritedState</c> read, so the commonest client-side filter there is --
        /// <c>foreach (var r in grid.Rows) r.Visible = !Matches (r);</c> -- left every row on screen
        /// (finding <c>DGV-20</c>, P0).
        /// <para>
        /// Note this does NOT throw when hiding the current row, where WinForms does. Ours neither hid
        /// the row nor threw before, and adding the throw is a separate behavioural decision from
        /// making the property work.
        /// </para>
        /// </remarks>
        public bool Visible {
            get => visible;
            set {
                if (visible == value)
                    return;

                visible = value;

                // The grid's own scroll extent and displayed-row count are derived from row heights, so
                // both have to be recomputed before the repaint.
                owner?.NotifyRowVisibleChanged ();
            }
        }

        private bool visible = true;

        /// <summary>Gets or sets whether the row can be resized by the user. Stub in Majorsilence.Forms.</summary>
        public DataGridViewTriState Resizable { get; set; } = DataGridViewTriState.NotSet;

        /// <summary>Gets a value indicating whether this row is frozen (cannot scroll). Stub in Majorsilence.Forms.</summary>
        public bool Frozen { get; set; }

        private string error_text = string.Empty;

        /// <summary>Gets or sets the error text for this row.</summary>
        /// <remarks>Notifies the owning grid on change, as upstream's setter does (W6.1, DGV-45).</remarks>
        public string ErrorText {
            get => error_text;
            set {
                if (EqualityComparer<string>.Default.Equals (error_text, value))
                    return;

                error_text = value;
                DataGridView?.NotifyRowErrorTextChanged (this);
            }
        }

        /// <summary>
        /// Gets the style used for this row: the grid's <see cref="DataGridView.DefaultCellStyle"/>, then
        /// its <see cref="DataGridView.RowsDefaultCellStyle"/>, then (on odd rows) its
        /// <see cref="DataGridView.AlternatingRowsDefaultCellStyle"/>, then this row's
        /// <see cref="DefaultCellStyle"/> -- later layers win. Mirrors WinForms
        /// DataGridViewRow.InheritedStyle.
        /// </summary>
        public DataGridViewCellStyle InheritedStyle {
            get {
                var result = new DataGridViewCellStyle ();

                if (owner is not null) {
                    result.ApplyStyle (owner.DefaultCellStyle.ToDataGridViewCellStyle ());
                    result.ApplyStyle (owner.RowsDefaultCellStyle.ToDataGridViewCellStyle ());

                    if (Index % 2 == 1)
                        result.ApplyStyle (owner.AlternatingRowsDefaultCellStyle.ToDataGridViewCellStyle ());
                }

                result.ApplyStyle (DefaultCellStyle);
                return result;
            }
        }

        /// <summary>Gets the state of this row (WinForms DataGridViewRow.State / InheritedState).</summary>
        public DataGridViewElementStates State {
            get {
                var state = DataGridViewElementStates.None;

                if (Visible)
                    state |= DataGridViewElementStates.Visible;
                if (ReadOnly || (owner?.ReadOnly ?? false))
                    state |= DataGridViewElementStates.ReadOnly;
                if (Selected)
                    state |= DataGridViewElementStates.Selected;
                if (Frozen)
                    state |= DataGridViewElementStates.Frozen;
                if (!Bounds.IsEmpty)
                    state |= DataGridViewElementStates.Displayed;
                if (Resizable != DataGridViewTriState.False && (owner?.AllowUserToResizeRows ?? true))
                    state |= DataGridViewElementStates.Resizable;
                if (Resizable != DataGridViewTriState.NotSet)
                    state |= DataGridViewElementStates.ResizableSet;

                return state;
            }
        }

        /// <summary>Gets the state of this row, including state inherited from the grid. Mirrors WinForms.</summary>
        public DataGridViewElementStates InheritedState => State;

        /// <summary>
        /// Creates an exact copy of this row, including clones of its cells (WinForms
        /// DataGridViewRow.Clone). The clone is unowned -- add it to a grid's row collection to attach it.
        /// </summary>
        [System.Diagnostics.CodeAnalysis.UnconditionalSuppressMessage ("Trimming", "IL2072",
            Justification = "Row types are concrete public types with public parameterless constructors; cloning mirrors WinForms.")]
        public virtual object Clone ()
        {
            var clone = (DataGridViewRow)Activator.CreateInstance (GetType ())!;

            clone.height = height;
            clone.MinimumHeight = MinimumHeight;
            clone.Tag = Tag;
            clone.Visible = Visible;
            clone.Frozen = Frozen;
            clone.SetSelectedCore (Selected, SelectionOrder);   // not the setter: a clone must not notify a grid
            clone.ErrorText = ErrorText;
            clone.Resizable = Resizable;
            clone.DataBoundItem = DataBoundItem;
            clone.DefaultCellStyle = DefaultCellStyle.Clone ();
            clone.read_only = read_only;

            foreach (var cell in Cells)
                clone.Cells.Add ((DataGridViewCell)cell.Clone ());

            return clone;
        }

        /// <summary>
        /// Sets the owning DataGridView.
        /// </summary>
        internal void SetOwner (DataGridView? dataGridView) => owner = dataGridView;
    }
}
