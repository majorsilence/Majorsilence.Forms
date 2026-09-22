using System.Drawing;
using Majorsilence.Forms.Renderers;

namespace Majorsilence.Forms;

/// <summary>
/// Represents a NavigationPane control.
/// </summary>
public class NavigationPane : Control
{
    /// <summary>
    /// Initializes a new instance of the NavigationPane class.
    /// </summary>
    public NavigationPane ()
    {
        Items = new NavigationPaneItemCollection (this);
        Dock = DockStyle.Left;
    }

    /// <inheritdoc/>
    protected override Size DefaultSize => new Size (49, 600);

    /// <inheritdoc/>
    public new static ControlStyle DefaultStyle = new ControlStyle (Control.DefaultStyle,
        (style) => {
            style.BackgroundColor = Theme.ControlMidColor;
            style.Border.Right.Width = 1;
            style.Border.Right.Color = Theme.BorderLowColor;
        });

    // Returns the item at the specified location.
    private NavigationPaneItem? GetItemAtLocation (Point location) => Items.FirstOrDefault (tp => tp.Bounds.Contains (location));

    /// <summary>
    /// Gets the collection of items contained by this NavigationPane.
    /// </summary>
    public NavigationPaneItemCollection Items { get; }

    // Layout the items.
    private void LayoutItems ()
    {
        // DisplayRectangle, not ClientRectangle: the layout engine writes LOGICAL item bounds, and
        // ClientRectangle is DEVICE. Laying out into the device box gave every item a logical width of
        // the pane's DEVICE width -- 151 logical units inside an 80-wide pane at MF_HEADLESS_SCALE=2,
        // so items overran the control and their painted fill was clipped at its edge. Exact at
        // scaling 1, which is why it survived. Menu.LayoutItems uses LogicalClientRectangle for the
        // same reason; this was the only layout in the assembly measuring against the scaled box.
        StackLayoutEngine.VerticalExpand.Layout (DisplayRectangle, Items.Cast<ILayoutable> ());
    }

    /// <inheritdoc/>
    protected override void OnMouseClick (MouseEventArgs e)
    {
        base.OnMouseClick (e);

        var clicked_item = GetItemAtLocation (e.Location);

        // This does a null check
        if (clicked_item?.Enabled == true)
            SelectedItem = clicked_item;
    }

    /// <inheritdoc/>
    protected override void OnMouseLeave (EventArgs e)
    {
        base.OnMouseLeave (e);

        Items.HoveredIndex = -1;
    }

    /// <inheritdoc/>
    protected override void OnMouseMove (MouseEventArgs e)
    {
        base.OnMouseMove (e);

        var hover_item = GetItemAtLocation (e.Location);
        Items.HoveredIndex = hover_item is null ? -1 : Items.IndexOf (hover_item);
    }

    /// <inheritdoc/>
    protected override void OnPaint (PaintEventArgs e)
    {
        base.OnPaint (e);

        // TODO: This should only be done when items are added or removed, or the NavigationPane is resized.
        LayoutItems ();

        RenderManager.Render (this, e);
    }

    /// <summary>
    /// Raises the SelectedItemChanged event.
    /// </summary>
    protected virtual void OnSelectedItemChanged (EventArgs e) => SelectedItemChanged?.Invoke (this, e);

    /// <summary>
    /// Raised when the selected item changes.
    /// </summary>
    public event EventHandler? SelectedItemChanged;

    /// <inheritdoc/>
    public override ControlStyle Style { get; } = new ControlStyle (DefaultStyle);

    /// <summary>
    /// Gets or sets the index of the currently selected item.
    /// </summary>
    public int SelectedIndex {
        get => Items.SelectedIndex;
        set {
            if (Items.SelectedIndex != value) {
                Items.SelectedIndex = value;
                OnSelectedItemChanged (EventArgs.Empty);

                Invalidate ();
            }
        }
    }

    /// <summary>
    /// Gets or sets the currently selected item.
    /// </summary>
    public NavigationPaneItem? SelectedItem {
        get => SelectedIndex >= 0 ? Items[SelectedIndex] : null;
        set {
            if (value is null) {
                SelectedIndex = -1;
                return;
            }

            var index = Items.IndexOf (value);

            if (index == -1)
                throw new ArgumentException ("Item is not part of this list");

            SelectedIndex = index;
        }
    }
}
