using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Linq;
using Majorsilence.Forms.Layout;

namespace Majorsilence.Forms
{
    // The tail of the WinForms member audit (docs/winforms-gap-plan.md).
    //
    // What is left after the enum values and the nested types: one or two members each on about
    // sixty types. They are grouped by owner rather than by kind, so each type's additions read
    // together with the reason they behave the way they do.

    public partial class ApplicationContext
    {
        /// <summary>Gets or sets arbitrary data associated with this context.</summary>
        public object? Tag { get; set; }
    }

    public partial class AutoCompleteStringCollection
    {
        /// <summary>Raised when the collection changes.</summary>
        public event CollectionChangeEventHandler? CollectionChanged;

        /// <summary>Raises the <see cref="CollectionChanged"/> event.</summary>
        protected virtual void OnCollectionChanged (CollectionChangeEventArgs e)
            => CollectionChanged?.Invoke (this, e);
    }

    public abstract partial class BindableComponent
    {
        /// <summary>Raised when <see cref="BindingContext"/> changes.</summary>
        public event EventHandler? BindingContextChanged;

        /// <summary>Raises the <see cref="BindingContextChanged"/> event.</summary>
        protected virtual void OnBindingContextChanged (EventArgs e)
            => BindingContextChanged?.Invoke (this, e);
    }

    public partial class BindingCompleteEventArgs
    {
        /// <summary>Gets the outcome of the binding operation.</summary>
        public BindingCompleteState BindingCompleteState { get; internal set; }
    }

    public partial class BindingsCollection
    {
        /// <summary>Raised after the collection changes.</summary>
        public event CollectionChangeEventHandler? CollectionChanged;

        /// <summary>Raised before the collection changes.</summary>
        public event CollectionChangeEventHandler? CollectionChanging;

        /// <summary>Raises the <see cref="CollectionChanged"/> event.</summary>
        protected virtual void OnCollectionChanged (CollectionChangeEventArgs ccevent)
            => CollectionChanged?.Invoke (this, ccevent);

        /// <summary>Raises the <see cref="CollectionChanging"/> event.</summary>
        protected virtual void OnCollectionChanging (CollectionChangeEventArgs ccevent)
            => CollectionChanging?.Invoke (this, ccevent);
    }

    public partial class ControlBindingsCollection
    {
        /// <summary>Gets the component whose properties are bound.</summary>
        public IBindableComponent? BindableComponent { get; internal set; }
    }

    public partial class Button
    {
        /// <summary>Gets whether the form has told this button it is the default button.</summary>
        public bool IsDefault { get; private set; }

        /// <summary>Tells the button whether it is the form's default button.</summary>
        /// <remarks>WinForms uses this to give the default button its heavier border; here it sets
        /// <see cref="IsDefault"/> and repaints, so a renderer that wants the heavier border has
        /// the flag to read.</remarks>
        public virtual void NotifyDefault (bool value)
        {
            if (IsDefault == value)
                return;

            IsDefault = value;
            Invalidate ();
        }
    }

    public partial class CheckBox
    {
        /// <summary>Raised when the control's Appearance changes.</summary>
        public event EventHandler? AppearanceChanged;

        /// <summary>Raises the <see cref="AppearanceChanged"/> event.</summary>
        protected virtual void OnAppearanceChanged (EventArgs e) => AppearanceChanged?.Invoke (this, e);
    }

    public partial class RadioButton
    {
        /// <summary>Raised when the control's Appearance changes.</summary>
        public event EventHandler? AppearanceChanged;

        /// <summary>Raises the <see cref="AppearanceChanged"/> event.</summary>
        protected virtual void OnAppearanceChanged (EventArgs e) => AppearanceChanged?.Invoke (this, e);
    }

    public partial class ColumnHeader
    {
        /// <summary>Gets the image list the header's image comes from.</summary>
        public ImageList? ImageList => ListView?.SmallImageList;

        /// <summary>Returns a copy of this header, detached from any list view.</summary>
        public object Clone () => new ColumnHeader {
            Text = Text,
            Width = Width,
            TextAlign = TextAlign,
            ImageIndex = ImageIndex,
            ImageKey = ImageKey,
            Tag = Tag,
            Name = Name,
        };
    }

    public partial class ContainerControl
    {
        /// <summary>Raised when <see cref="AutoValidate"/> changes.</summary>
        public event EventHandler? AutoValidateChanged;

        /// <summary>Raises the <see cref="AutoValidateChanged"/> event.</summary>
        protected virtual void OnAutoValidateChanged (EventArgs e) => AutoValidateChanged?.Invoke (this, e);

        /// <summary>Gets the scale the container is currently laid out at.</summary>
        /// <remarks>Derived from the current font, matching AutoScaleMode.Font; the Dpi mode reports
        /// the device's own dots per inch. Both are read from the live control rather than from the
        /// designer-recorded <see cref="AutoScaleDimensions"/>, which is what makes the ratio
        /// between the two meaningful.</remarks>
        public SizeF CurrentAutoScaleDimensions => AutoScaleMode switch {
            AutoScaleMode.Font => new SizeF (Font.Size * 2f, Font.Height),
            AutoScaleMode.Dpi => new SizeF (DeviceDpi, DeviceDpi),
            _ => SizeF.Empty,
        };

        /// <summary>Scales the container to the difference between its recorded and current dimensions.</summary>
        public void PerformAutoScale ()
        {
            if (AutoScaleMode is AutoScaleMode.None or AutoScaleMode.Inherit)
                return;

            var current = CurrentAutoScaleDimensions;
            var recorded = AutoScaleDimensions;

            if (recorded.Width == 0 || recorded.Height == 0 || current.Width == 0 || current.Height == 0)
                return;

            Scale (new SizeF (current.Width / recorded.Width, current.Height / recorded.Height));
            AutoScaleDimensions = current;
        }
    }

    public partial class UserControl
    {
        /// <summary>Raised when <see cref="AutoValidate"/> changes.</summary>
        public event EventHandler? AutoValidateChanged;

        /// <summary>Raises the <see cref="AutoValidateChanged"/> event.</summary>
        protected virtual void OnAutoValidateChanged (EventArgs e) => AutoValidateChanged?.Invoke (this, e);
    }

    public partial class ContextMenu
    {
        /// <summary>Raised before the menu is displayed.</summary>
        public event EventHandler? Popup;

        /// <summary>Raised after the menu is dismissed.</summary>
        public event EventHandler? Collapse;

        /// <summary>Raises the <see cref="Popup"/> event.</summary>
        protected internal virtual void OnPopup (EventArgs e) => Popup?.Invoke (this, e);

        /// <summary>Raises the <see cref="Collapse"/> event.</summary>
        protected internal virtual void OnCollapse (EventArgs e) => Collapse?.Invoke (this, e);
    }

    public partial class MainMenu
    {
        /// <summary>Raised after the menu is dismissed.</summary>
        public event EventHandler? Collapse;

        /// <summary>Raises the <see cref="Collapse"/> event.</summary>
        protected internal virtual void OnCollapse (EventArgs e) => Collapse?.Invoke (this, e);

        /// <summary>Returns the form this menu is attached to, or null.</summary>
        public Form? GetForm () => FindForm ();
    }

    public partial class DataGridTextBox
    {
        /// <summary>Gets or sets whether the box is navigating rather than editing.</summary>
        public bool IsInEditOrNavigateMode { get; set; } = true;

        /// <summary>Associates the box with the grid it edits.</summary>
        public void SetDataGrid (DataGrid parentGrid) => ParentGrid = parentGrid;

        /// <summary>Gets the grid this box edits.</summary>
        internal DataGrid? ParentGrid { get; private set; }
    }

    public partial class DataGridViewButtonColumn
    {
        /// <summary>Gets or sets the flat-style appearance of the column's buttons.</summary>
        public FlatStyle FlatStyle { get; set; } = FlatStyle.Standard;
    }

    public partial class DataGridViewColumnHeaderCell
    {
        /// <summary>Gets or sets the direction of the sort glyph shown on the header.</summary>
        public SortOrder SortGlyphDirection { get; set; } = SortOrder.None;
    }

    public partial class DataGridViewImageCell
    {
        /// <summary>Gets or sets the cell's accessible description.</summary>
        public string? Description { get; set; }

        /// <summary>Gets or sets whether the cell's value is an icon rather than an image.</summary>
        public bool ValueIsIcon { get; set; }
    }

    public partial class DataGridViewLinkCell
    {
        /// <summary>Gets or sets whether the cell paints visited links differently.</summary>
        public bool TrackVisitedState { get; set; } = true;
    }

    public partial class DataGridViewLinkColumn
    {
        /// <summary>Gets or sets how the column's links are underlined.</summary>
        public LinkBehavior LinkBehavior { get; set; } = LinkBehavior.SystemDefault;
    }

    public partial class DataGridViewTextBoxCell
    {
        /// <summary>Gets or sets the longest value the cell's editing control accepts.</summary>
        public virtual int MaxInputLength { get; set; } = 32767;
    }

    public partial class DataGridViewCellPaintingEventArgs
    {
        /// <summary>Gets the border style the cell is painted with.</summary>
        public DataGridViewAdvancedBorderStyle AdvancedBorderStyle { get; internal set; } = new ();

        /// <summary>Gets the state of the cell being painted.</summary>
        public DataGridViewElementStates State { get; internal set; }
    }

    public partial class FontDialog
    {
        /// <summary>Gets or sets whether the dialog offers only fonts using the selected script.</summary>
        public bool ScriptsOnly { get; set; }
    }

    public partial class GroupBox
    {
        /// <summary>Gets or sets how the group box sizes itself to its contents.</summary>
        public AutoSizeMode AutoSizeMode {
            get => GetAutoSizeMode ();
            set => SetAutoSizeMode (value);
        }
    }

    public partial class LabelEditEventArgs
    {
        /// <summary>Gets or sets whether the edit is cancelled.</summary>
        /// <remarks>Tied to <see cref="CancelEventArgs.Cancel"/> rather than being a second flag:
        /// list view label editing has one outcome, and two independent flags could disagree.</remarks>
        public bool CancelEdit {
            get => Cancel;
            set => Cancel = value;
        }
    }

    public partial class NodeLabelEditEventArgs
    {
        /// <summary>Gets or sets whether the edit is cancelled.</summary>
        /// <inheritdoc cref="LabelEditEventArgs.CancelEdit" path="/remarks"/>
        public bool CancelEdit {
            get => Cancel;
            set => Cancel = value;
        }
    }

    public partial class LinkClickedEventArgs
    {
        /// <summary>Initializes a new instance of the <see cref="LinkClickedEventArgs"/> class.</summary>
        public LinkClickedEventArgs (string linkText, int linkStart, int linkLength)
            : this (linkText)
        {
            LinkStart = linkStart;
            LinkLength = linkLength;
        }

        /// <summary>Gets the offset of the link within the control's text.</summary>
        public int LinkStart { get; }

        /// <summary>Gets the length of the link within the control's text.</summary>
        public int LinkLength { get; }
    }

    public partial class NavigateEventArgs
    {
        /// <summary>Initializes a new instance of the <see cref="NavigateEventArgs"/> class.</summary>
        public NavigateEventArgs (bool isForward) => Forward = isForward;

        /// <summary>Gets whether navigation is forward through the list.</summary>
        public bool Forward { get; }
    }

    public abstract partial class ListControl
    {
        /// <summary>Raised when <c>SelectedValue</c> changes.</summary>
        public event EventHandler? SelectedValueChanged;

        /// <summary>Raises the <see cref="SelectedValueChanged"/> event.</summary>
        protected virtual void OnSelectedValueChanged (EventArgs e) => SelectedValueChanged?.Invoke (this, e);
    }

    public partial class MdiClient
    {
        /// <summary>Gets the child forms hosted by this client.</summary>
        public Form[] MdiChildren => ChildForms.ToArray ();

    }

    public partial class MenuStrip
    {
        /// <summary>Raised when the menu receives focus.</summary>
        public event EventHandler? MenuActivate;

        /// <summary>Raised when the menu loses focus.</summary>
        public event EventHandler? MenuDeactivate;

        /// <summary>Raises the <see cref="MenuActivate"/> event.</summary>
        protected virtual void OnMenuActivate (EventArgs e) => MenuActivate?.Invoke (this, e);

        /// <summary>Raises the <see cref="MenuDeactivate"/> event.</summary>
        protected virtual void OnMenuDeactivate (EventArgs e) => MenuDeactivate?.Invoke (this, e);
    }

    public partial class ProgressBar
    {
        private bool right_to_left_layout;

        /// <summary>Gets or sets whether the bar fills from the right under a right-to-left layout.</summary>
        public virtual bool RightToLeftLayout {
            get => right_to_left_layout;
            set {
                if (right_to_left_layout == value)
                    return;

                right_to_left_layout = value;
                OnRightToLeftLayoutChanged (EventArgs.Empty);
                Invalidate ();
            }
        }

        /// <summary>Raised when <see cref="RightToLeftLayout"/> changes.</summary>
        public event EventHandler? RightToLeftLayoutChanged;

        /// <summary>Raises the <see cref="RightToLeftLayoutChanged"/> event.</summary>
        protected virtual void OnRightToLeftLayoutChanged (EventArgs e) => RightToLeftLayoutChanged?.Invoke (this, e);
    }

    public partial class PropertyTabChangedEventArgs
    {
        /// <summary>Gets the tab that was selected.</summary>
        public PropertyTab? NewTab { get; internal set; }

        /// <summary>Gets the tab that was previously selected.</summary>
        public PropertyTab? OldTab { get; internal set; }
    }

    public partial class ScrollBar
    {
        /// <summary>Gets or sets whether the bar's own metrics scale with the DPI.</summary>
        public bool ScaleScrollBarForDpiChange { get; set; } = true;
    }

    public partial class StatusStrip
    {
        /// <summary>Gets or sets whether the strip draws a sizing grip in its trailing corner.</summary>
        public bool SizingGrip { get; set; } = true;

        /// <summary>Gets the bounds of the sizing grip, or an empty rectangle when there is none.</summary>
        public Rectangle SizeGripBounds
        {
            get {
                if (!SizingGrip)
                    return Rectangle.Empty;

                // WinForms reserves a square the height of the strip at the trailing edge, mirrored
                // for a right-to-left layout.
                var size = Height;
                var x = RightToLeft == RightToLeft.Yes ? 0 : Width - size;

                return new Rectangle (x, 0, size, size);
            }
        }
    }

    public partial class TableLayoutPanel
    {
        /// <summary>Raised for each cell as the panel paints.</summary>
        public event TableLayoutCellPaintEventHandler? CellPaint;

        /// <summary>Raises the <see cref="CellPaint"/> event.</summary>
        protected virtual void OnCellPaint (TableLayoutCellPaintEventArgs e) => CellPaint?.Invoke (this, e);
    }

    public partial class ToolBarButton
    {
        /// <summary>Gets the toolbar this button belongs to.</summary>
        public ToolBar? Parent { get; internal set; }

        /// <summary>Gets the button's bounds within its toolbar.</summary>
        public Rectangle Rectangle { get; internal set; }
    }

    public partial class ToolStripContentPanelRenderEventArgs
    {
        /// <summary>Gets the panel being painted.</summary>
        public ToolStripContentPanel? ToolStripContentPanel { get; internal set; }
    }

    public partial class ToolStripDropDownButton
    {
        /// <summary>Gets or sets whether the button paints a drop-down arrow.</summary>
        public bool ShowDropDownArrow { get; set; } = true;
    }

    public partial class ToolStripLabel
    {
        /// <summary>Gets or sets the colour of the link while it is being pressed.</summary>
        public Color ActiveLinkColor { get; set; } = Color.Red;
    }

    public partial class ToolStripMenuItem
    {
        /// <summary>Raised when <c>CheckState</c> changes.</summary>
        public event EventHandler? CheckStateChanged;

        /// <summary>Raises the <see cref="CheckStateChanged"/> event.</summary>
        protected virtual void OnCheckStateChanged (EventArgs e) => CheckStateChanged?.Invoke (this, e);

        /// <summary>Gets whether this item is one of the MDI window list entries.</summary>
        /// <remarks>False: the window list is built by <c>MdiWindowListItem</c>, and nothing in this
        /// layer marks the generated entries, so no ordinary item can be mistaken for one.</remarks>
        public bool IsMdiWindowListEntry => false;
    }

    public partial class ToolStripOverflowButton
    {
        /// <summary>Gets whether the overflow currently holds any items.</summary>
        /// <remarks>An override now that ToolStripDropDownButton derives from ToolStripDropDownItem, which
        /// declares this virtual -- before, the button had no base to override and this declared its own.</remarks>
        public override bool HasDropDownItems => DropDownItems.Count > 0;
    }

    public partial class ToolStripPanelRow
    {
        /// <summary>Gets the layout engine that arranges this row.</summary>
        public LayoutEngine LayoutEngine => Layout.DefaultLayout.Instance;
    }

    public partial class ToolStripProfessionalRenderer
    {
        /// <summary>Gets the colour table this renderer paints from.</summary>
        public ProfessionalColorTable ColorTable { get; protected set; } = new ProfessionalColorTable ();
    }

    public partial class ToolStripStatusLabel
    {
        /// <summary>Gets or sets how assistive technology is notified when the label's text changes.</summary>
        public AutomationLiveSetting LiveSetting { get; set; } = AutomationLiveSetting.Off;
    }

    public partial class ImageListStreamer
    {
        /// <summary>Releases the resources held by this streamer.</summary>
        /// <remarks>Nothing to release: the images live in the <see cref="ImageList"/> that produced
        /// the streamer, not in the streamer itself. Present because upstream's is, so a using
        /// statement around one still compiles.</remarks>
        public void Dispose () { }

        /// <summary>Populates a serialization info with the data needed to recreate this streamer.</summary>
        /// <remarks>Adds nothing. The upstream payload is a Win32 image-list blob, which this layer
        /// neither produces nor can read; writing a partial one would give a stream that
        /// deserialises into a silently empty image list.</remarks>
        public void GetObjectData (System.Runtime.Serialization.SerializationInfo si,
            System.Runtime.Serialization.StreamingContext context)
            => ArgumentNullException.ThrowIfNull (si);
    }

    public partial class NumericUpDownAccelerationCollection
    {
        /// <summary>Gets whether the collection is read-only.</summary>
        public bool IsReadOnly => false;
    }

    public partial class DrawListViewColumnHeaderEventArgs
    {
        /// <summary>Paints the header's default background.</summary>
        public void DrawBackground ()
        {
            using var brush = new SolidBrush (BackColor);
            Graphics.FillRectangle (brush, Bounds);
        }

        /// <summary>Paints the header's default text.</summary>
        public void DrawText () => DrawText (TextFormatFlags.Default);

        /// <summary>Paints the header's default text with the given formatting.</summary>
        public void DrawText (TextFormatFlags flags)
            => TextRenderer.DrawText (Graphics, Header?.Text ?? string.Empty, Font, Bounds, ForeColor, flags);
    }

    public partial class DataGridViewRowPostPaintEventArgs
    {
        /// <summary>Paints the focus rectangle for the row.</summary>
        /// <remarks>Does nothing when the event is raised outside a paint pass, which is when
        /// <see cref="DataGridViewRowPaintBaseEventArgs.Graphics"/> is null -- the same shape as the
        /// other Paint* helpers on these args.</remarks>
        public void DrawFocus (Rectangle bounds, bool cellsPaintSelectionBackground)
        {
            // Two null checks, not one: the args carry no Graphics outside a paint pass, and a Graphics
            // made for measurement rather than painting carries no canvas.
            if (Graphics?.Canvas is { } canvas)
                canvas.DrawFocusRectangle (bounds.X, bounds.Y, bounds.Width, bounds.Height);
        }
    }

    public partial class DataGridViewRowPrePaintEventArgs
    {
        /// <inheritdoc cref="DataGridViewRowPostPaintEventArgs.DrawFocus"/>
        public void DrawFocus (Rectangle bounds, bool cellsPaintSelectionBackground)
        {
            if (Graphics?.Canvas is { } canvas)
                canvas.DrawFocusRectangle (bounds.X, bounds.Y, bounds.Width, bounds.Height);
        }
    }

    public partial class GridColumnStylesCollection
    {
        /// <summary>Drops the property descriptors cached for the collection's styles.</summary>
        public void ResetPropertyDescriptors ()
        {
            foreach (var style in this.OfType<DataGridColumnStyle> ())
                style.PropertyDescriptor = null;
        }
    }

    public abstract partial class GridItem
    {
        /// <summary>Gets whether this item can be expanded.</summary>
        public virtual bool Expandable => GridItems.Count > 0;

        /// <summary>Gets what kind of item this is.</summary>
        public virtual GridItemType GridItemType => GridItemType.Property;
    }

    public sealed partial class GridItemCollection
    {
        /// <summary>An empty collection.</summary>
        public static readonly GridItemCollection Empty = [];
    }

    public sealed partial class DataGridViewColumnDesignTimeVisibleAttribute
    {
        /// <summary>The column is visible in the designer.</summary>
        public static readonly DataGridViewColumnDesignTimeVisibleAttribute Yes = new (true);

        /// <summary>The column is hidden in the designer.</summary>
        public static readonly DataGridViewColumnDesignTimeVisibleAttribute No = new (false);
    }

    public sealed partial class DockingAttribute
    {
        /// <summary>The default docking behaviour.</summary>
        public static readonly DockingAttribute Default = new (DockingBehavior.Never);
    }

    public partial class DataGridTableStyle
    {
        /// <summary>The style a grid uses when no table style of its own applies.</summary>
        public static readonly DataGridTableStyle DefaultTableStyle = new ();
    }

    public partial class DataGrid
    {
        /// <summary>What part of a <see cref="DataGrid"/> a point falls on.</summary>
        public enum HitTestType
        {
            /// <summary>Nothing.</summary>
            None = 0,
            /// <summary>A data cell.</summary>
            Cell = 1,
            /// <summary>A column header.</summary>
            ColumnHeader = 2,
            /// <summary>A row header.</summary>
            RowHeader = 4,
            /// <summary>The resize handle between two columns.</summary>
            ColumnResize = 8,
            /// <summary>The resize handle between two rows.</summary>
            RowResize = 16,
            /// <summary>The grid's caption.</summary>
            Caption = 32,
            /// <summary>The parent-row area shown when a child table is displayed.</summary>
            ParentRows = 64,
        }

        /// <summary>What a point in a <see cref="DataGrid"/> hit.</summary>
        public sealed class HitTestInfo
        {
            internal HitTestInfo (HitTestType type, int row, int column)
            {
                Type = type;
                Row = row;
                Column = column;
            }

            /// <summary>A hit on nothing.</summary>
            public static readonly HitTestInfo Nowhere = new (HitTestType.None, -1, -1);

            /// <summary>Gets what part of the grid was hit.</summary>
            public HitTestType Type { get; }

            /// <summary>Gets the row that was hit, or -1.</summary>
            public int Row { get; }

            /// <summary>Gets the column that was hit, or -1.</summary>
            public int Column { get; }

            /// <inheritdoc/>
            public override string ToString () => $"{{ {Type},{Row},{Column} }}";
        }
    }

    public partial class DataGridViewCellCollection
    {
        /// <summary>Raised when the collection changes.</summary>
        public event CollectionChangeEventHandler? CollectionChanged;

        /// <summary>Adds several cells at once.</summary>
        public virtual void AddRange (params DataGridViewCell[] dataGridViewCells)
        {
            ArgumentNullException.ThrowIfNull (dataGridViewCells);

            foreach (var cell in dataGridViewCells)
                Add (cell);

            CollectionChanged?.Invoke (this, new CollectionChangeEventArgs (CollectionChangeAction.Refresh, null));
        }
    }

    public static partial class Help
    {
        /// <summary>Shows the index of the given help file.</summary>
        /// <remarks>Does nothing, like the other Help members here: there is no help viewer to
        /// launch on the platforms this layer targets.</remarks>
        public static void ShowHelpIndex (Control? parent, string? url) { }
    }

    public partial class NotifyIcon
    {
        /// <summary>Raised when a mouse button is pressed over the icon.</summary>
        public event MouseEventHandler? MouseDown;

        /// <summary>Raised when a mouse button is released over the icon.</summary>
        public event MouseEventHandler? MouseUp;

        /// <summary>Raises the <see cref="MouseDown"/> event.</summary>
        protected virtual void OnMouseDown (MouseEventArgs e) => MouseDown?.Invoke (this, e);

        /// <summary>Raises the <see cref="MouseUp"/> event.</summary>
        protected virtual void OnMouseUp (MouseEventArgs e) => MouseUp?.Invoke (this, e);
    }

    public partial class PaintEventArgs
    {
        /// <summary>Releases the resources held by these args.</summary>
        /// <remarks>Nothing to release: the canvas belongs to the paint pass that created the args
        /// and outlives them. Present so a using statement around one still compiles.</remarks>
        public void Dispose () { }
    }

    public partial class PreviewKeyDownEventArgs
    {
        /// <summary>Gets the key code as an integer.</summary>
        public int KeyValue => (int) (KeyData & Keys.KeyCode);
    }

    public partial class Screen
    {
        /// <summary>Returns the screen showing the window with the given handle.</summary>
        /// <remarks>The primary screen. There are no HWNDs here, so the handle cannot be resolved to
        /// a window -- and upstream also falls back to the primary screen for a handle it does not
        /// recognise, so a caller sees a screen rather than null either way.</remarks>
        public static Screen? FromHandle (IntPtr hwnd) => PrimaryScreen;
    }

    public partial class ScrollableControl
    {
        /// <summary>Gets the padding between the control's docked edges and its contents.</summary>
        public DockPaddingEdges DockPadding { get; } = new ();
    }

    public partial class TabPage
    {
        /// <summary>Returns the page a component sits on, or null.</summary>
        public static TabPage? GetTabPageOfComponent (object? comp)
        {
            for (var control = comp as Control; control is not null; control = control.Parent) {
                if (control is TabPage page)
                    return page;
            }

            return null;
        }
    }

    public partial class ToolStripButton
    {
        /// <summary>Raised when <see cref="Checked"/> changes.</summary>
        public event EventHandler? CheckedChanged;

        /// <summary>Gets or sets the button's check state.</summary>
        public CheckState CheckState {
            get => Checked ? CheckState.Checked : CheckState.Unchecked;
            set => Checked = value == CheckState.Checked;
        }

        /// <summary>Raises the <see cref="CheckedChanged"/> event.</summary>
        protected virtual void OnCheckedChanged (EventArgs e) => CheckedChanged?.Invoke (this, e);
    }

}
