using System;
using System.Drawing;
using SkiaSharp;

namespace Majorsilence.Forms.Telerik
{
    // RadTreeView's nodes, laid out and drawn as Telerik's are under its default theme. Measured on UI for
    // WinForms 2026.2.520 (.NET 10), in logical pixels from the row's left:
    //   - every level is TreeIndent wide (20 by default), and a node's own column holds its expander whether
    //     or not it has children, so its content starts (level + 1) * TreeIndent in;
    //   - the expander is a 9px box at (TreeIndent - 9) / 2 + 1 across its column and (ItemHeight - 9) / 2 + 1
    //     down the row: navy edges shading lighter towards the bottom right when the node is open and the
    //     other way round when it is closed, white inside, a minus, and an upright through it when closed;
    //   - the image is drawn at its own size 1px into an element 2px wider than it, centred down the content
    //     (1px below the row's top, ItemHeight - 2 high) and clipped to it; with no image the element is
    //     still there, 2px wide, and the text follows it;
    //   - the text is black Segoe UI 9pt whatever the tree's Font, centred down the content;
    //   - a row is ItemHeight high (20 by default) and the next starts ItemHeight + SpacingBetweenNodes
    //     below it (-1 by default, so neighbouring rows share a pixel);
    //   - with ShowLines, #808080 dots on every other pixel down the centre of a node's column (from the
    //     top of its row unless it is the first root, to the row's centre or to 3px above its expander, and
    //     on to the bottom when a sibling follows), down the column of each ancestor with a sibling still to
    //     come, and along the row's centre out to the content: from 1px past the expander to 1px short of
    //     the column's right edge, or with no expander from 2px past the centre to 3px short of that edge.
    //     LineWidth thickens them.
    internal sealed class RadTreeNodeLayout : TreeNodeLayout
    {
        private readonly RadTreeView tree;

        internal RadTreeNodeLayout (RadTreeView tree) => this.tree = tree;

        private static readonly Drawing.Font ThemeFont = new Drawing.Font ("Segoe UI", 9f);
        private static readonly SKColor LineColour = new SKColor (0x80, 0x80, 0x80);

        // The expander, pixel by pixel: "." is the white inside.
        private static readonly string[] OpenButton = {
            "6f7b90 0a1f42 0a1f42 0a1f42 0a1f42 0a1f42 0c2145 10274b 76839a",
            "0a1f42 . . . . . . . 1c355c",
            "0a1f42 . . . . . . . 223d66",
            "0a1f42 . . . . . . . 2a4671",
            "2b3d5b f1f2f4 0a1f42 0a1f42 365d9c 0a1f42 182c4d . 314f7c",
            "0a1f42 . . . . . . . 385887",
            "0c2145 . . . . . . . 406192",
            "10274b . . . . . . . 476a9c",
            "76839a 1c355c 223d66 2a4671 314f7c 385887 406192 476a9c 96acca",
        };

        private static readonly string[] ClosedButton = {
            "9fb6d6 5b83ba 577fb5 5379ae 5175a7 476a9c 406192 385887 8697b2",
            "5b83ba . . . . . . . 2a4671",
            "577fb5 . . . 17315d . . . 223d66",
            "5379ae . . . 294b81 . . . 1c355c",
            "6585b1 f1f2f4 17315d 294b81 365d9c 294b81 253d67 . 162d53",
            "476a9c . . . 294b81 . . . 10274b",
            "406192 . . . 17315d . . . 0c2145",
            "385887 . . . . . . . 0a1f42",
            "8697b2 2a4671 223d66 1c355c 1b3257 10274b 0c2145 0a1f42 6f7b90",
        };

        private static readonly SKColor?[,] OpenPixels = Parse (OpenButton);
        private static readonly SKColor?[,] ClosedPixels = Parse (ClosedButton);

        private static SKColor?[,] Parse (string[] rows)
        {
            var pixels = new SKColor?[9, 9];

            for (var y = 0; y < 9; y++) {
                var cells = rows[y].Split (' ');

                for (var x = 0; x < 9; x++)
                    pixels[y, x] = cells[x] == "." ? null : new SKColor (0xFF000000 | Convert.ToUInt32 (cells[x], 16));
            }

            return pixels;
        }

        private int Device (int logical) => tree.LogicalToDeviceUnits (logical);

        private int Indent => Math.Max (1, tree.TreeIndent);

        private int RowHeight => tree.ItemHeight > 0 ? tree.ItemHeight : 20;

        // The left of column `column` (0 for the roots') of the node's row.
        private int ColumnLeft (TreeNode node, int column) => node.DeviceBounds.Left + Device (column * Indent);

        public override int ContentLeft (TreeNode node) => ColumnLeft (node, node.IndentLevel + 1);

        public override Rectangle ButtonBounds (TreeNode node)
        {
            if (!node.HasChildren)
                return Rectangle.Empty;

            var size = Device (9);

            return new Rectangle (ColumnLeft (node, node.IndentLevel) + Device ((Indent - 9) / 2 + 1),
                node.DeviceBounds.Top + Device ((RowHeight - 9) / 2 + 1), size, size);
        }

        // The image element: the image's width plus 2, down the content.
        private Rectangle ImageElement (TreeNode node, SKBitmap? image)
            => new Rectangle (ContentLeft (node) + tree.ScaledCheckWidth, node.DeviceBounds.Top + Device (1),
                Device ((image?.Width ?? 0) + 2), Device (RowHeight - 2));

        public override Rectangle ImageBounds (TreeNode node, SKBitmap? image)
            => image is null ? Rectangle.Empty : ImageElement (node, image);

        public override Rectangle LabelBounds (TreeNode node, SKBitmap? image)
        {
            var element = ImageElement (node, image);

            return new Rectangle (element.Right, element.Top, Math.Max (0, node.DeviceBounds.Right - element.Right), element.Height);
        }

        public override Drawing.Font? DefaultFont => ThemeFont;

        public override SKColor ForeColor => SKColors.Black;

        public override void DrawImage (PaintEventArgs e, TreeNode node, SKBitmap image, bool disabled)
        {
            var element = ImageElement (node, image);
            var width = Device (image.Width);
            var height = Device (image.Height);

            e.Canvas.Save ();
            e.Canvas.Clip (element);
            e.Canvas.DrawBitmap (image, new Rectangle (element.Left + Device (1), element.Top + (element.Height - height) / 2, width, height), disabled);
            e.Canvas.Restore ();
        }

        // Telerik's text sits 2px further in and 1px higher than text centred down the content.
        public override void DrawText (PaintEventArgs e, TreeNode node, Rectangle label, SKTypeface font, int fontSize, SKColor colour)
            => e.Canvas.DrawText (node.Text.Trim (), font, fontSize, new Rectangle (label.Left + Device (2), label.Top - Device (1), Math.Max (0, label.Width - Device (2)), label.Height),
                colour, ContentAlignment.MiddleLeft, maxLines: 1);

        public override void DrawButton (PaintEventArgs e, Rectangle bounds, bool expanded)
        {
            var pixels = expanded ? OpenPixels : ClosedPixels;
            var unit = Math.Max (1, bounds.Width / 9);

            for (var y = 0; y < 9; y++)
                for (var x = 0; x < 9; x++)
                    e.Canvas.FillRectangle (bounds.Left + x * unit, bounds.Top + y * unit, unit, unit, pixels[y, x] ?? SKColors.White);
        }

        public override void DrawLines (PaintEventArgs e, TreeNode node)
        {
            var dot = Device (1);
            var step = 2 * dot;
            var thickness = Math.Max (1, (int) Math.Round (dot * tree.LineWidth));
            var top = node.DeviceBounds.Top;
            var bottom = node.DeviceBounds.Bottom;
            var centre_y = top + Device (RowHeight / 2);
            var level = node.IndentLevel;

            int CentreX (int column) => ColumnLeft (node, column) + Device (Indent / 2);

            // Dots counted from the row's top, in [from, to].
            void Down (int x, int from, int to)
            {
                for (var y = top + (from - top + step - 1) / step * step; y <= to; y += step)
                    e.Canvas.FillRectangle (x, y, thickness, dot, LineColour);
            }

            void Across (int from, int to)
            {
                for (var x = from; x <= to; x += step)
                    e.Canvas.FillRectangle (x, centre_y, dot, thickness, LineColour);
            }

            if (level > 0 || tree.ShowRootLines) {
                var x = CentreX (level);

                var column_right = ColumnLeft (node, level + 1);
                var button = tree.ShowPlusMinus ? ButtonBounds (node) : Rectangle.Empty;

                if (node.PrevNode is not null || level > 0)
                    Down (x, top, button.IsEmpty ? centre_y : button.Top - 3 * dot);

                if (node.NextNode is not null)
                    Down (x, centre_y, bottom - 1);

                if (button.IsEmpty)
                    Across (x + 2 * dot, column_right - 3 * dot);
                else
                    Across (button.Right + dot, column_right - dot);
            }

            for (var ancestor = node.Parent; ancestor is not null; ancestor = ancestor.Parent) {
                var ancestor_level = ancestor.IndentLevel;

                // A root's Parent is the tree's hidden root node, at level -1.
                if (ancestor_level < 0 || ancestor_level == 0 && !tree.ShowRootLines || ancestor.NextNode is null)
                    continue;

                Down (CentreX (ancestor_level), top, bottom - 1);
            }
        }
    }
}
