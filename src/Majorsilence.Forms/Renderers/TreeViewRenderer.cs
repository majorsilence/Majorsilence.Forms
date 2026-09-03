using System.Drawing;

namespace Majorsilence.Forms.Renderers
{
    /// <summary>
    /// Represents a class that can render a TreeView.
    /// </summary>
    public class TreeViewRenderer : Renderer<TreeView>
    {
        /// <summary>
        /// Size of each indent level.
        /// </summary>
        protected const int INDENT_SIZE = 18;
        /// <summary>
        /// Size of item image.
        /// </summary>
        protected const int IMAGE_SIZE = 16;
        /// <summary>
        /// Size of dropdown glyph.
        /// </summary>
        protected const int GLYPH_SIZE = 10;

        /// <inheritdoc/>
        protected override void Render (TreeView control, PaintEventArgs e)
        {
            e.Canvas.Save ();
            e.Canvas.Clip (control.ClientRectangle);

            // LayoutedItems is populated by TreeView.LayoutItems() before Render is called.
            // Using it avoids a second tree traversal on every paint.
            var visible_item_count = control.ScaledHeight / control.ScaledItemHeight;
            var items = control.LayoutedItems;

            // +1: with a sub-row touch-scroll offset, item[0] is partly above the top and one more
            // row than usual peeks in at the bottom. The Clip above trims both.
            for (var i = 0; i < items.Count && i <= visible_item_count + 1; i++)
                RenderItem (control, items[i], e);

            e.Canvas.Restore ();
        }

        /// <summary>
        /// Renders a TreeNode.
        /// </summary>
        protected virtual void RenderItem (TreeView control, TreeNode item, PaintEventArgs e)
        {
            // OwnerDrawAll hands over the whole node, background and focus cue included, so the
            // event has to come before anything is painted. OwnerDrawText keeps those and hands
            // over only the content -- hence the two checks rather than one.
            if (control.DrawMode == TreeViewDrawMode.OwnerDrawAll) {
                var all = new TreeViewDrawEventArgs (control, item, e);
                control.RaiseDrawNode (all);

                if (!all.DrawDefault)
                    return;
            }

            var is_selected = item == control.SelectedItem;

            // A node's own ForeColor/BackColor win over the theme; Color.Empty means "use the theme".
            // All three were stored and never read at paint, so bold "unread" and red "error" nodes
            // were silently ignored (LST-26).
            var foreground_color = !control.Enabled
                ? Theme.ForegroundDisabledColor
                : item.ForeColor != System.Drawing.Color.Empty ? item.ForeColor.ToSKColor () : Theme.ForegroundColor;

            if (item.BackColor != System.Drawing.Color.Empty && !is_selected)
                e.Canvas.FillRectangle (item.Bounds, item.BackColor.ToSKColor ());

            if (is_selected)
#if NETSTANDARD2_0
                // Style's static return type is the base ControlStyle here (no covariant returns on
                // netstandard2.0); the instance is a TreeViewControlStyle.
                e.Canvas.FillRectangle (item.Bounds, ((TreeView.TreeViewControlStyle) control.Style).GetSelectedItemBackgroundColor ());
#else
                e.Canvas.FillRectangle (item.Bounds, control.Style.GetSelectedItemBackgroundColor ());
#endif

            if (is_selected && control.Focused && control.ShowFocusCues)
                e.Canvas.DrawFocusRectangle (item.Bounds, e.LogicalToDeviceUnits (1));

            if (control.DrawMode == TreeViewDrawMode.OwnerDrawText) {
                var dea = new TreeViewDrawEventArgs (control, item, e);
                control.RaiseDrawNode (dea);

                if (!dea.DrawDefault)
                    return;
            }

            if (control.ShowDropdownGlyph == true) {
                var glyph_bounds = GetGlyphBounds (control, item);

                if (GetShouldDrawDropdownGlyph (control, item))
                    ControlPaint.DrawArrowGlyph (e, glyph_bounds, foreground_color, item.Expanded ? ArrowDirection.Down : ArrowDirection.Right);
            }

            // The check box, when the tree shows them: nothing drew one, so a permissions tree with
            // CheckBoxes = true showed no boxes at all (LST-24). Same ControlPaint entry point as
            // CheckBox and CheckedListBox, so the three cannot drift apart.
            if (control.CheckBoxes)
                ControlPaint.DrawCheckBox (e, control.CheckBounds (item),
                    item.Checked ? CheckState.Checked : CheckState.Unchecked, !control.Enabled);

            if (control.ShowItemImages == true && ResolveImage (control, item, is_selected) is { } image) {
                var image_bounds = GetImageBounds (control, item, e);

                e.Canvas.DrawBitmap (image, image_bounds, !control.Enabled);
            }

            if (string.IsNullOrWhiteSpace (item.Text))
                return;

            var text_bounds = GetTextBounds (control, item, e);

            // The node's own font when it has one, else the control's -- NodeFont was stored and the
            // renderer always used the theme font (LST-26).
            var font = item.NodeFont is { } node_font ? node_font.GetSKTypeface () : control.GetEffectiveFont ();
            var font_size = item.NodeFont is { } sized
                ? (int)System.Math.Round (sized.PixelSize)
                : e.LogicalToDeviceUnits (Theme.FontSize);

            e.Canvas.DrawText (item.Text.Trim (), font, font_size, text_bounds, foreground_color, ContentAlignment.MiddleLeft, maxLines: 1);
        }

        /// <summary>
        /// The bitmap to draw for a node: its own <c>Image</c>, else the tree's
        /// <c>ImageList</c> entry named by the node's index or key.
        /// </summary>
        /// <remarks>
        /// The <c>ImageList</c> path is new in W5.9 (finding <c>LST-25</c>). Only <c>TreeNode.Image</c>
        /// was ever read, so every explorer-style tree built the WinForms way -- an <c>ImageList</c>
        /// plus per-node <c>ImageIndex</c> -- showed no icons at all, and the compatibility matrix
        /// claimed index-based images worked.
        /// </remarks>
        protected virtual SkiaSharp.SKBitmap? ResolveImage (TreeView control, TreeNode item, bool isSelected)
        {
            if (item.ImageSK is { } own)
                return own;

            var images = control.ImageList?.Images;

            if (images is null || images.Count == 0)
                return null;

            // A selected node prefers its selected-image slot, falling back to the normal one, and each
            // falls back to the tree's own default -- the order upstream resolves in.
            var key = isSelected && item.SelectedImageKey.HasValue () ? item.SelectedImageKey
                : item.ImageKey.HasValue () ? item.ImageKey
                : null;

            if (key is not null)
                return images.ContainsKey (key) ? images[key] : null;

            var index = isSelected && item.SelectedImageIndex >= 0 ? item.SelectedImageIndex
                : item.ImageIndex >= 0 ? item.ImageIndex
                : isSelected && control.SelectedImageIndex >= 0 ? control.SelectedImageIndex
                : control.ImageIndex;

            return index >= 0 && index < images.Count ? images[index] : null;
        }

        /// <summary>
        /// Gets the bounds of the dropdown glyph.
        /// </summary>
        public virtual Rectangle GetGlyphBounds (TreeView control, TreeNode item)
        {
            if (!control.ShowDropdownGlyph)
                return Rectangle.Empty;

            var glyph_area = new Rectangle (GetIndentStart (control, item), item.Bounds.Top, control.LogicalToDeviceUnits (GLYPH_SIZE), item.Bounds.Height);
            var glyph_bounds = DrawingExtensions.CenterSquare (glyph_area, control.LogicalToDeviceUnits (GLYPH_SIZE));

            glyph_bounds.Width = control.LogicalToDeviceUnits (GLYPH_SIZE);

            return glyph_bounds;
        }

        /// <summary>
        /// Gets the bounds of the item image.
        /// </summary>
        protected virtual Rectangle GetImageBounds (TreeView control, TreeNode item, PaintEventArgs e)
        {
            if (!control.ShowItemImages || ResolveImage (control, item, item == control.SelectedItem) is null)
                return Rectangle.Empty;

            var left_index = control.ShowDropdownGlyph ? GetGlyphBounds (control, item).Right : GetIndentStart (control, item);

            // The check box sits between the glyph and the image, so the image starts after it.
            left_index += control.ScaledCheckWidth;
            var image_area = new Rectangle (left_index, item.Bounds.Top, item.Bounds.Height, item.Bounds.Height);

            return DrawingExtensions.CenterSquare (image_area, e.LogicalToDeviceUnits (IMAGE_SIZE));
        }

        /// <summary>
        /// Gets the bounds of the item text.
        /// </summary>
        protected virtual Rectangle GetTextBounds (TreeView control, TreeNode item, PaintEventArgs e)
        {
            var show_glyph = control.ShowDropdownGlyph;
            var show_image = control.ShowItemImages;

            if (!show_glyph && !show_image)
                return new Rectangle (GetIndentStart (control, item), item.Bounds.Top, item.Bounds.Width - GetIndentStart (control, item), item.Bounds.Height);

            // One of these will be valid because we handled the other case above
            var padding = e.LogicalToDeviceUnits (6);
            var has_image = show_image && ResolveImage (control, item, item == control.SelectedItem) is not null;
            var used_bounds = has_image ? GetImageBounds (control, item, e) : GetGlyphBounds (control, item);
            var left = System.Math.Max (used_bounds.Right, has_image ? used_bounds.Right : used_bounds.Right + control.ScaledCheckWidth);

            return new Rectangle (left + padding, item.Bounds.Top, item.Bounds.Right - left - padding, item.Bounds.Height);
        }

        /// <summary>
        /// Gets the left start of the item bounds, accounting for indent level.
        /// </summary>
        /// <remarks>Uses the control's <see cref="TreeView.Indent"/>, which was stored and never read
        /// -- so a wider indent for a deep hierarchy did nothing (<c>LST-26</c>). The constant remains
        /// the fallback for a control whose Indent has been zeroed.</remarks>
        protected virtual int GetIndentStart (TreeView control, TreeNode item)
            => item.Bounds.Left + item.IndentLevel * control.LogicalToDeviceUnits (control.Indent > 0 ? control.Indent : INDENT_SIZE) + 2;

        /// <summary>
        /// Gets if the item should draw a dropdown glyph.
        /// </summary>
        protected virtual bool GetShouldDrawDropdownGlyph (TreeView control, TreeNode item) => control.ShowDropdownGlyph && (item.HasChildren || (control.VirtualMode && item.items == null));
    }
}
