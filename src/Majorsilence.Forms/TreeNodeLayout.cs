using System.Drawing;

namespace Majorsilence.Forms
{
    /// <summary>
    /// How a derived tree lays out and draws its nodes when it does so its own way, as Telerik's RadTreeView
    /// does. A tree without one (<see cref="TreeView.NodeLayout"/> null) is laid out as the theme, or in a
    /// ported app as upstream's tree, lays it out. All rectangles are in device pixels.
    /// </summary>
    internal abstract class TreeNodeLayout
    {
        /// <summary>Where the node's content -- its check box, image and label -- starts.</summary>
        public abstract int ContentLeft (TreeNode node);

        /// <summary>The expand/collapse button, or empty when the node shows none.</summary>
        public abstract Rectangle ButtonBounds (TreeNode node);

        /// <summary>The area the node's image is drawn in, or empty when it has none.</summary>
        public abstract Rectangle ImageBounds (TreeNode node, SkiaSharp.SKBitmap? image);

        /// <summary>The node's label: where its text is drawn and its highlight goes when the row is not
        /// selected in full.</summary>
        public abstract Rectangle LabelBounds (TreeNode node, SkiaSharp.SKBitmap? image);

        /// <summary>The font a node without its own is drawn in, or null for the tree's.</summary>
        public abstract Drawing.Font? DefaultFont { get; }

        /// <summary>The colour of a node's text when neither the node nor the selection sets one.</summary>
        public abstract SkiaSharp.SKColor ForeColor { get; }

        /// <summary>Draws the node's image into <see cref="ImageBounds"/>.</summary>
        public abstract void DrawImage (PaintEventArgs e, TreeNode node, SkiaSharp.SKBitmap image, bool disabled);

        /// <summary>Draws the node's text into its label.</summary>
        public abstract void DrawText (PaintEventArgs e, TreeNode node, Rectangle label, SkiaSharp.SKTypeface font, int fontSize, SkiaSharp.SKColor colour);

        /// <summary>Draws the expand/collapse button.</summary>
        public abstract void DrawButton (PaintEventArgs e, Rectangle bounds, bool expanded);

        /// <summary>Draws the lines joining the node to its parent and siblings, when the tree shows them.</summary>
        public abstract void DrawLines (PaintEventArgs e, TreeNode node);
    }
}
