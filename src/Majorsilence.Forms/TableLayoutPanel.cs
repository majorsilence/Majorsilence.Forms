// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.ComponentModel;
using System.Diagnostics;
using System.Drawing;
using Majorsilence.Forms.Layout;

namespace Majorsilence.Forms;

/// <summary>
/// Represents a TableLayoutPanel control.
/// </summary>
[ProvideProperty ("ColumnSpan", typeof (Control))]
[ProvideProperty ("RowSpan", typeof (Control))]
[ProvideProperty ("Row", typeof (Control))]
[ProvideProperty ("Column", typeof (Control))]
[ProvideProperty ("CellPosition", typeof (Control))]
[DefaultProperty (nameof (ColumnCount))]
public partial class TableLayoutPanel : Panel, IExtenderProvider
{
    private readonly TableLayoutSettings _tableLayoutSettings;

    /// <summary>
    /// Initializes a new instance of the TableLayoutPanel class.
    /// </summary>
    public TableLayoutPanel ()
    {
        _tableLayoutSettings = TableLayout.CreateSettings (this);
    }

    /// <inheritdoc/>
    public override LayoutEngine LayoutEngine => TableLayout.Instance;

    /// <summary>
    /// Gets the layout settings associated with this TableLayoutPanel.
    /// </summary>
    [Browsable (false)]
    [EditorBrowsable (EditorBrowsableState.Never)]
    [DesignerSerializationVisibility (DesignerSerializationVisibility.Hidden)]
    public TableLayoutSettings LayoutSettings {
        get => _tableLayoutSettings;
#if DESIGN_TIME
        set {
            if (value is not null && value.IsStub) {
                // WINRES only scenario.
                // we only support table layout settings that have been created from a type converter.
                // this is here for localization (WinRes) support.
                using (new LayoutTransaction (this, this, PropertyNames.LayoutSettings)) {
                    // apply RowStyles, ColumnStyles, Row & Column assignments.
                    _tableLayoutSettings.ApplySettings (value);
                }
            } else {
                throw new NotSupportedException (SR.TableLayoutSettingSettingsIsNotSupported);
            }
        }
#endif
    }

    //[Browsable(false)]
    //[EditorBrowsable(EditorBrowsableState.Never)]
    //[Localizable(true)]
    //public new BorderStyle BorderStyle
    //{
    //    get => base.BorderStyle;
    //    set
    //    {
    //        base.BorderStyle = value;
    //        Debug.Assert(BorderStyle == value, "BorderStyle should be the same as we set it");
    //    }
    //}

    /// <summary>Gets or sets the cell border style for the TableLayoutPanel.</summary>
    [DefaultValue (TableLayoutPanelCellBorderStyle.None)]
    public TableLayoutPanelCellBorderStyle CellBorderStyle {
        get => _tableLayoutSettings.CellBorderStyle;
        set {
            // Valid values are None (0) through OutsetPartial (6).
            if (value < TableLayoutPanelCellBorderStyle.None || value > TableLayoutPanelCellBorderStyle.OutsetPartial)
                throw new InvalidEnumArgumentException (nameof (value), (int)value, typeof (TableLayoutPanelCellBorderStyle));

            _tableLayoutSettings.CellBorderStyle = value;
            Debug.Assert (CellBorderStyle == value, "CellBorderStyle should be the same as we set it");
        }
    }

    /// <summary>
    /// Gets the collection of controls contained by the control.
    /// </summary>
    [Browsable (false)]
    [DesignerSerializationVisibility (DesignerSerializationVisibility.Content)]
    public new TableLayoutControlCollection Controls => (TableLayoutControlCollection)base.Controls;

    /// <summary>
    ///  This sets the maximum number of columns allowed on this table instead of allocating
    ///  actual spaces for these columns. So it is OK to set ColumnCount to Int32.MaxValue without
    ///  causing out of memory exception
    /// </summary>
    [DefaultValue (0)]
    [Localizable (true)]
    public int ColumnCount {
        get => _tableLayoutSettings.ColumnCount;
        set {
            _tableLayoutSettings.ColumnCount = value;
            Debug.Assert (ColumnCount == value, "ColumnCount should be the same as we set it");
        }
    }

    /// <summary>
    ///  Specifies if a TableLayoutPanel will gain additional rows or columns once its existing cells
    ///  become full.  If the value is 'FixedSize' then the TableLayoutPanel will throw an exception
    ///  when the TableLayoutPanel is over-filled.
    /// </summary>
    [DefaultValue (TableLayoutPanelGrowStyle.AddRows)]
    public TableLayoutPanelGrowStyle GrowStyle {
        get => _tableLayoutSettings.GrowStyle;
        set => _tableLayoutSettings.GrowStyle = value;
    }

    /// <summary>
    ///  This sets the maximum number of rows allowed on this table instead of allocating
    ///  actual spaces for these rows. So it is OK to set RowCount to Int32.MaxValue without
    ///  causing out of memory exception
    /// </summary>
    [DefaultValue (0)]
    [Localizable (true)]
    public int RowCount {
        get => _tableLayoutSettings.RowCount;
        set => _tableLayoutSettings.RowCount = value;
    }

    /// <summary>
    /// Gets the collection of RowStyles for this TableLayoutPanel.
    /// </summary>
    [DesignerSerializationVisibility (DesignerSerializationVisibility.Content)]
    [DisplayName ("Rows")]
    [MergableProperty (false)]
    [Browsable (false)]
    public TableLayoutRowStyleCollection RowStyles => _tableLayoutSettings.RowStyles;

    /// <summary>
    /// Gets the collection of ColumnStyles for this TableLayoutPanel.
    /// </summary>
    [DesignerSerializationVisibility (DesignerSerializationVisibility.Content)]
    [DisplayName ("Columns")]
    [Browsable (false)]
    [MergableProperty (false)]
    public TableLayoutColumnStyleCollection ColumnStyles => _tableLayoutSettings.ColumnStyles;

    /// <inheritdoc/>
    [EditorBrowsable (EditorBrowsableState.Advanced)]
    protected override ControlCollection CreateControlsInstance ()
    {
        return new TableLayoutControlCollection (this);
    }

    private bool ShouldSerializeControls ()
    {
        var collection = Controls;

        return collection is not null && collection.Count > 0;
    }

    #region Extended Properties
    bool IExtenderProvider.CanExtend (object obj)
    {
        return obj is Control control && control.Parent == this;
    }

    /// <summary>
    /// Returns the column span value for the specified control.
    /// </summary>
    [DefaultValue (1)]
    [DisplayName ("ColumnSpan")]
    public int GetColumnSpan (Control control) => _tableLayoutSettings.GetColumnSpan (control);

    /// <summary>
    /// Sets the column span value for the specified control.
    /// </summary>
    public void SetColumnSpan (Control control, int value)
    {
        // layout.SetColumnSpan() throws ArgumentException if out of range.
        _tableLayoutSettings.SetColumnSpan (control, value);
        Debug.Assert (GetColumnSpan (control) == value, "GetColumnSpan should be the same as we set it");
    }

    /// <summary>
    /// Returns the row span value for the specified control.
    /// </summary>
    [DefaultValue (1)]
    [DisplayName ("RowSpan")]
    public int GetRowSpan (Control control) => _tableLayoutSettings.GetRowSpan (control);

    /// <summary>
    /// Sets the row span value for the specified control.
    /// </summary>
    public void SetRowSpan (Control control, int value)
    {
        // layout.SetRowSpan() throws ArgumentException if out of range.
        _tableLayoutSettings.SetRowSpan (control, value);
        Debug.Assert (GetRowSpan (control) == value, "GetRowSpan should be the same as we set it");
    }

    /// <summary>
    ///  Gets the row position of the specified control.
    /// </summary>
    [DefaultValue (-1)]  //if change this value, also change the SerializeViaAdd in TableLayoutControlCollectionCodeDomSerializer
    [DesignerSerializationVisibility (DesignerSerializationVisibility.Hidden)]
    [DisplayName ("Row")]
    public int GetRow (Control control) => _tableLayoutSettings.GetRow (control);

    /// <summary>
    ///  Sets the row position of the specified control.
    /// </summary>
    public void SetRow (Control control, int row)
    {
        _tableLayoutSettings.SetRow (control, row);
        Debug.Assert (GetRow (control) == row, "GetRow should be the same as we set it");
    }

    /// <summary>
    ///  Gets the row and column position of the specified control.
    /// </summary>
#if DESIGN_TIME
    [DefaultValue (typeof (TableLayoutPanelCellPosition), "-1,-1")]  //if change this value, also change the SerializeViaAdd in TableLayoutControlCollectionCodeDomSerializer
#endif
    [DesignerSerializationVisibility (DesignerSerializationVisibility.Hidden)]
    [DisplayName ("Cell")] public TableLayoutPanelCellPosition GetCellPosition (Control control) => _tableLayoutSettings.GetCellPosition (control);

    /// <summary>
    ///  Sets the row and column position of the specified control.
    /// </summary>
    public void SetCellPosition (Control control, TableLayoutPanelCellPosition position) => _tableLayoutSettings.SetCellPosition (control, position);

    /// <summary>
    ///  Gets the column position of the specified control.
    /// </summary>
    [DefaultValue (-1)]  //if change this value, also change the SerializeViaAdd in TableLayoutControlCollectionCodeDomSerializer
    [DesignerSerializationVisibility (DesignerSerializationVisibility.Hidden)]
    [DisplayName ("Column")]
    public int GetColumn (Control control) => _tableLayoutSettings.GetColumn (control);

    /// <summary>
    ///  Sets the column position of the specified control.
    /// </summary>
    public void SetColumn (Control control, int column)
    {
        _tableLayoutSettings.SetColumn (control, column);
        Debug.Assert (GetColumn (control) == column, "GetColumn should be the same as we set it");
    }

    /// <summary>
    ///  Gets the control which covers the specified row and column. return null if we can't find one
    /// </summary>
    public Control? GetControlFromPosition (int column, int row) => (Control?)_tableLayoutSettings.GetControlFromPosition (column, row);

    /// <summary>
    ///  Gets the row and column position of the specified control.
    /// </summary>
    public TableLayoutPanelCellPosition GetPositionFromControl (Control control) => _tableLayoutSettings.GetPositionFromControl (control);

    /// <summary>
    ///  Gets an array representing the widths (in pixels) of the columns in the TableLayoutPanel.
    /// </summary>
    [Browsable (false)]
    [EditorBrowsable (EditorBrowsableState.Never)]
    public int[] GetColumnWidths ()
    {
        var containerInfo = TableLayout.GetContainerInfo (this);

        if (containerInfo.Columns is null)
            return [];

        var cw = new int[containerInfo.Columns.Length];

        for (var i = 0; i < containerInfo.Columns.Length; i++)
            cw[i] = containerInfo.Columns[i].MinSize;

        return cw;
    }

    /// <summary>
    ///  Gets an array representing the heights (in pixels) of the rows in the TableLayoutPanel.
    /// </summary>
    [Browsable (false)]
    [EditorBrowsable (EditorBrowsableState.Never)]
    public int[] GetRowHeights ()
    {
        var containerInfo = TableLayout.GetContainerInfo (this);

        if (containerInfo.Rows is null)
            return [];

        var rh = new int[containerInfo.Rows.Length];

        for (var i = 0; i < containerInfo.Rows.Length; i++)
            rh[i] = containerInfo.Rows[i].MinSize;

        return rh;
    }
    #endregion

    #region PaintCode
    // LAY-22: the OnLayout override that invalidates, and the whole of OnPaintBackground, were
    // commented out behind a `// TODO: Custom Cell Paint`. CellBorderStyle was honoured by the layout
    // engine, which reserves the gap between cells, and nothing ever drew in that gap -- so a
    // grid-looking form migrated as a grid of floating controls separated by mysterious whitespace.
    // CellPaint itself is declared in RemainingMemberParity.cs (with upstream's delegate type) and was
    // simply never raised, which is why it sat on the inert-event baseline.

    /// <summary>
    /// When a layout fires, make sure we are painting all of our cell borders.
    /// </summary>
    [EditorBrowsable (EditorBrowsableState.Advanced)]
    protected override void OnLayout (LayoutEventArgs e)
    {
        base.OnLayout (e);
        Invalidate ();
    }

    /// <inheritdoc/>
    /// <remarks>
    /// Walks the strips the layout engine computed -- the same ones it reserved the border gaps from --
    /// raising <see cref="CellPaint"/> for each cell and then drawing that cell's border on top.
    /// </remarks>
    protected override void OnPaintBackground (PaintEventArgs e)
    {
        base.OnPaintBackground (e);

        Guard.ThrowIfNull (e);

        var cell_border_width = _tableLayoutSettings.CellBorderWidth;
        // Unqualified `Layout.` would bind to Control's Layout EVENT, not the namespace.
        var container = TableLayout.GetContainerInfo (this);
        var columns = container.Columns;
        var rows = container.Rows;
        var style = CellBorderStyle;

        if (columns is null || rows is null)
            return;

        var display = DisplayRectangle;
        var clip = e.ClipRectangle;

        // Half the border width is left outside the first cell, so the grid lines land between cells
        // rather than half off the panel.
        var right_to_left = RightToLeft == RightToLeft.Yes;
        var startx = right_to_left
            ? display.Right - (cell_border_width / 2)
            : display.X + (cell_border_width / 2);

        for (var i = 0; i < columns.Length; i++) {
            var starty = display.Y + (cell_border_width / 2);

            if (right_to_left)
                startx -= columns[i].MinSize;

            for (var j = 0; j < rows.Length; j++) {
                var outside = new Rectangle (startx, starty, columns[i].MinSize, rows[j].MinSize);
                var inside = new Rectangle (
                    outside.X + (cell_border_width + 1) / 2,
                    outside.Y + (cell_border_width + 1) / 2,
                    outside.Width - (cell_border_width + 1) / 2,
                    outside.Height - (cell_border_width + 1) / 2);

                if (clip.IntersectsWith (inside)) {
                    // The application paints first; the grid line goes on top of whatever it drew.
                    OnCellPaint (new TableLayoutCellPaintEventArgs (e.Info, e.Canvas, e.Scaling, clip, inside, i, j));

                    ControlPaint.PaintTableCellBorder (style, e, outside);
                }

                starty += rows[j].MinSize;
            }

            if (!right_to_left)
                startx += columns[i].MinSize;
        }

        // The table's own outer border, which the per-cell pass cannot draw because each cell only
        // owns its top and left edges.
        if (style != TableLayoutPanelCellBorderStyle.None) {
            var table = new Rectangle (
                cell_border_width / 2 + display.X,
                cell_border_width / 2 + display.Y,
                display.Width - cell_border_width,
                display.Height - cell_border_width);

            ControlPaint.PaintTableCellBorder (style, e, table);
        }
    }
    #endregion
}
