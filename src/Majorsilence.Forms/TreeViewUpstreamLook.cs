using System.Drawing;

namespace Majorsilence.Forms
{
    // A ported WinForms app (one that chose its font with Application.SetDefaultFont) gets the tree WinForms
    // draws under the Windows 11 theme, laid out as upstream's native tree lays it out. All of it measured
    // from WinForms (.NET 10), in logical pixels from the row's left:
    //   - a node's columns: one per level, Indent wide, plus one for the root when root lines or buttons
    //     are shown (ShowRootLines and ShowLines or ShowPlusMinus);
    //   - its button and line centre: in its last column, half the image width plus 3 from the column's
    //     left, or half the indent plus 3 without an ImageList; vertically half the row height, rounded up
    //     to even (12 in a 22px row, 10 in an 18px one);
    //   - its content: the check box (13 wide), then the image, then the label, starting 3px after the
    //     last column, whose width is the larger of Indent and the image width; the label 3px after the
    //     image, its text 2px in and the label 4px wider than the text;
    //   - an unset Indent is 19, or the image width plus 3 when that is wider.
    // Apps that have not chosen a font keep the theme's layout.
    public partial class TreeView
    {
        /// <summary>Whether this tree is laid out and drawn as upstream's.</summary>
        internal static bool UsesUpstreamLook => ControlPaint.UsesUpstreamGlyphs;

        /// <summary>The check box column's width under the upstream look, in logical pixels.</summary>
        internal const int UpstreamCheckWidth = 13;

        private const int DefaultIndent = 19;

        // The ImageList's image width; zero without one.
        private int UpstreamImageWidth => ImageList?.ImageSize.Width ?? 0;

        // The indent the native tree uses: Indent, which when left at its default grows to the image width
        // plus 3.
        internal int UpstreamIndent => Indent == DefaultIndent ? System.Math.Max (DefaultIndent, UpstreamImageWidth + 3) : Indent;

        // How many indent columns come before a node's content.
        internal int UpstreamColumns (TreeNode node)
            => node.IndentLevel + (ShowRootLines && (ShowLines || ShowPlusMinus) ? 1 : 0);

        /// <summary>The x, in device pixels, of the centre of the button and lines in column
        /// <paramref name="columns"/> (1 for the first) of <paramref name="node"/>'s row.</summary>
        internal int UpstreamColumnCentre (TreeNode node, int columns)
        {
            var image = UpstreamImageWidth;
            var centre = (columns - 1) * UpstreamIndent + (image > 0 ? image : UpstreamIndent) / 2 + 3;

            return node.Bounds.Left + LogicalToDeviceUnits (centre);
        }

        /// <summary>The y, in device pixels, of the centre of <paramref name="node"/>'s button and stub.</summary>
        internal int UpstreamCentreY (TreeNode node)
        {
            var half = DeviceToLogicalUnits (node.Bounds.Height) / 2;

            return node.Bounds.Top + LogicalToDeviceUnits (half % 2 == 0 ? half : half + 1);
        }

        /// <summary>The x, in device pixels, where <paramref name="node"/>'s content (check box, image,
        /// label) starts.</summary>
        internal int UpstreamContentLeft (TreeNode node)
        {
            var columns = UpstreamColumns (node);
            var left = columns <= 0 ? 3 : (columns - 1) * UpstreamIndent + System.Math.Max (UpstreamIndent, UpstreamImageWidth) + 3;

            return node.Bounds.Left + LogicalToDeviceUnits (left);
        }

        /// <summary>The x, in device pixels, where <paramref name="node"/>'s image starts.</summary>
        internal int UpstreamImageLeft (TreeNode node)
            => UpstreamContentLeft (node) + (CheckBoxes ? LogicalToDeviceUnits (UpstreamCheckWidth) : 0);

        /// <summary>The x, in device pixels, where <paramref name="node"/>'s label starts.</summary>
        internal int UpstreamLabelLeft (TreeNode node)
        {
            var image = UpstreamImageWidth;

            return UpstreamImageLeft (node) + (image > 0 ? LogicalToDeviceUnits (image + 3) : 0);
        }

        /// <summary>The image rectangle, in device pixels: the ImageList's size, centred in the row.</summary>
        internal Rectangle UpstreamImageBounds (TreeNode node)
        {
            if (ImageList is not { } images)
                return Rectangle.Empty;

            var size = images.ImageSize;
            var width = LogicalToDeviceUnits (size.Width);
            var height = LogicalToDeviceUnits (size.Height);

            return new Rectangle (UpstreamImageLeft (node), node.Bounds.Top + (node.Bounds.Height - height) / 2, width, height);
        }

        /// <summary>The 9px button rectangle, in device pixels, or empty when the node's row has no button
        /// column.</summary>
        internal Rectangle UpstreamButtonBounds (TreeNode node)
        {
            var columns = UpstreamColumns (node);

            if (!ShowPlusMinus || columns <= 0)
                return Rectangle.Empty;

            var size = LogicalToDeviceUnits (9);
            var half = LogicalToDeviceUnits (4);

            return new Rectangle (UpstreamColumnCentre (node, columns) - half, UpstreamCentreY (node) - half, size, size);
        }
    }
}
