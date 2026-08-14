namespace Majorsilence.Forms;

/// <summary>
///  Provides data for the TreeView.DrawNode event.
/// </summary>
public class TreeViewDrawEventArgs : PaintEventArgs
{
    /// <summary>
    /// Gets the TreeView requesting the draw.
    /// </summary>
    public TreeView TreeView { get; }

    /// <summary>
    /// Gets the TreeNode to draw.
    /// </summary>
    public TreeNode Item { get; }

    /// <summary>
    ///  Initializes a new instance of the TreeViewDrawEventArgs class.
    /// </summary>
    public TreeViewDrawEventArgs (TreeView treeView, TreeNode item, PaintEventArgs pea) : base (pea.Info, pea.Canvas, pea.Scaling)
    {
        TreeView = treeView;
        Item = item;
    }

    /// <summary>
    ///  Causes the item do be drawn by the system instead of owner drawn.
    /// </summary>
    public bool DrawDefault { get; set; }
}
