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
            // Before anything is read off the node, so a formatting hook can still change it. This
            // runs on every paint, unlike the owner-draw events below.
            control.RaiseNodeFormatting (item);

            // OwnerDrawAll hands over the whole node, background and focus cue included, so the
            // event has to come before anything is painted. OwnerDrawText keeps those and hands
            // over only the content -- hence the two checks rather than one.
            if (control.DrawMode == TreeViewDrawMode.OwnerDrawAll) {
                var all = new TreeViewDrawEventArgs (control, item, e);
                control.RaiseDrawNode (all);

                if (!all.DrawDefault)
                    return;
            }

            // HideSelection: a tree that has lost focus gives up its highlight. Read by nothing before,
            // so the band stayed whatever the focus was -- and unlike ListView, whose upstream default
            // is false, TreeView's upstream default is TRUE, so this is the shape most trees should
            // have had all along. The two siblings genuinely differ; matching them would be the bug.
            var is_selected = item == control.SelectedItem && (control.Focused || !control.HideSelection);

            // A node's own ForeColor/BackColor win over the theme; Color.Empty means "use the theme".
            // All three were stored and never read at paint, so bold "unread" and red "error" nodes
            // were silently ignored (LST-26).
            var foreground_color = !control.Enabled
                ? Theme.ForegroundDisabledColor
                : item.ForeColor != System.Drawing.Color.Empty ? item.ForeColor.ToSKColor ()
                : is_selected && TreeView.DefaultSelectionStyle.ForegroundColor is { } selection_fg ? selection_fg
                : control.HotTracking && ReferenceEquals (control.HotNode, item) ? SystemColors.HotTrack.ToSKColor () // W6
                : Theme.ForegroundColor;

            if (item.BackColor != System.Drawing.Color.Empty && !is_selected)
                e.Canvas.FillRectangle (item.Bounds, item.BackColor.ToSKColor ());

            // FullRowSelect: the whole row, or just the label. It was read by nothing, so every tree
            // highlighted the full row -- which is what FullRowSelect = TRUE means, while the property
            // defaults to false. An explorer-style tree therefore looked like a list.
            if (is_selected) {
                var highlight = control.FullRowSelect ? item.Bounds : GetTextBounds (control, item, e);

#if NETSTANDARD2_0
                // Style's static return type is the base ControlStyle here (no covariant returns on
                // netstandard2.0); the instance is a TreeViewControlStyle.
                e.Canvas.FillRectangle (highlight, ((TreeView.TreeViewControlStyle) control.Style).GetSelectedItemBackgroundColor ());
#else
                e.Canvas.FillRectangle (highlight, control.Style.GetSelectedItemBackgroundColor ());
#endif
            }

            if (is_selected && control.Focused && control.ShowFocusCues)
                e.Canvas.DrawFocusRectangle (item.Bounds, e.LogicalToDeviceUnits (1));

            if (control.DrawMode == TreeViewDrawMode.OwnerDrawText) {
                var dea = new TreeViewDrawEventArgs (control, item, e);
                control.RaiseDrawNode (dea);

                if (!dea.DrawDefault)
                    return;
            }

            RenderLines (control, item, e);

            if (control.ShowDropdownGlyph == true) {
                var glyph_bounds = GetGlyphBounds (control, item);

                if (GetShouldDrawDropdownGlyph (control, item))
                    ControlPaint.DrawArrowGlyph (e, glyph_bounds, foreground_color, item.Expanded ? ArrowDirection.Down : ArrowDirection.Right);
            }

            // The check box, when the tree shows them: nothing drew one, so a permissions tree with
            // CheckBoxes = true showed no boxes at all (LST-24). Same ControlPaint entry point as
            // CheckBox and CheckedListBox, so the three cannot drift apart.
            if (control.CheckBoxes) {
                // A state image replaces the glyph, as it does on ListView and as upstream draws it.
                // TreeView.StateImageList and TreeNode.StateImageIndex were both stored and read by
                // nothing, so a tree using state images showed ordinary check boxes instead.
                if (ListViewRenderer.StateImage (control.StateImageList, item.StateImageIndex) is { } state)
                    e.Canvas.DrawBitmap (state, control.CheckBounds (item), !control.Enabled);
                else
                    ControlPaint.DrawCheckBox (e, control.CheckBounds (item),
                        item.Checked ? CheckState.Checked : CheckState.Unchecked, !control.Enabled);
            }

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
            // The remark above says each falls back to the tree's own default. That was true of the
            // INDEX chain below and had never been implemented for the key chain: TreeView.ImageKey and
            // TreeView.SelectedImageKey were stored and read by nothing, so a tree that named its
            // default icon by key instead of index showed no icon at all. The comment described the
            // behaviour; the code only had half of it.
            var key = isSelected && item.SelectedImageKey.HasValue () ? item.SelectedImageKey
                : item.ImageKey.HasValue () ? item.ImageKey
                : isSelected && control.SelectedImageKey.HasValue () ? control.SelectedImageKey
                : control.ImageKey.HasValue () ? control.ImageKey
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
        /// Draws the lines joining a node to its parent and to its siblings.
        /// </summary>
        /// <remarks>
        /// <para><see cref="TreeView.ShowLines"/>, <see cref="TreeView.ShowRootLines"/> and
        /// <see cref="TreeView.LineColor"/> were all stored and read by nothing: no connector was drawn
        /// at any setting, so a hierarchy read as a flat indented list and the three properties were
        /// indistinguishable from each other (<c>LST-61</c>).</para>
        /// <para>Each node draws in its OWN gutter -- the vertical strip its glyph sits in -- plus a
        /// full-height run in every ANCESTOR gutter whose node still has a sibling below it. That is
        /// what makes a deep branch continuous down the left of its children instead of restarting at
        /// every row, and it is why this cannot be done from the node alone.</para>
        /// <para>Scope, stated rather than implied: <c>ShowRootLines = false</c> suppresses the lines at
        /// root level, but does NOT also hide the root glyphs or re-indent the children, which is what
        /// upstream additionally does. Those are layout changes; this is the paint half. The remainder
        /// is recorded in <c>LST-61</c> rather than half-built here.</para>
        /// </remarks>
        protected virtual void RenderLines (TreeView control, TreeNode item, PaintEventArgs e)
        {
            if (!control.ShowLines)
                return;

            var colour = control.LineColor != System.Drawing.Color.Empty
                ? control.LineColor.ToSKColor ()
                : Theme.BorderMidColor;

            var thickness = e.LogicalToDeviceUnits (1);
            var step = control.LogicalToDeviceUnits (control.Indent > 0 ? control.Indent : INDENT_SIZE);
            var half_glyph = control.LogicalToDeviceUnits (GLYPH_SIZE) / 2;

            var top = item.Bounds.Top;
            var bottom = item.Bounds.Bottom;
            var middle = top + item.Bounds.Height / 2;

            // The centre of the gutter a node at this depth sits in -- the same arithmetic
            // GetIndentStart uses, plus half a glyph, so the run passes through the glyph rather than
            // beside it whether or not one is drawn.
            int Centre (int level) => item.Bounds.Left + level * step + 2 + half_glyph;

            var level = item.IndentLevel;

            if (level > 0 || control.ShowRootLines) {
                var x = Centre (level);

                // Upward: to whatever is above in this gutter. A first root node has nothing above it,
                // so it starts at its own centre; every other node continues a run already in progress.
                if (item.PrevNode is not null || item.Parent is not null)
                    e.Canvas.DrawLine (x, top, x, middle, colour, thickness);

                // Downward: only when a sibling follows, or the run would hang past the last child.
                if (item.NextNode is not null)
                    e.Canvas.DrawLine (x, middle, x, bottom, colour, thickness);

                // The stub out to the node's glyph, stopping at the right edge of its own gutter. It
                // must NOT run on to the content: the check box starts exactly there, and a stub that
                // crossed it would put ink in an unchecked tree's check rectangle -- which is the only
                // evidence TreeViewBehaviourTests has that a box was drawn at all.
                e.Canvas.DrawLine (x, middle, x + half_glyph, middle, colour, thickness);
            }

            // Ancestor gutters: a full-height run wherever that ancestor has a sibling still to come,
            // which is the part a per-node view cannot see.
            for (var ancestor = item.Parent; ancestor is not null; ancestor = ancestor.Parent) {
                var ancestor_level = ancestor.IndentLevel;

                if (ancestor_level == 0 && !control.ShowRootLines)
                    continue;

                if (ancestor.NextNode is null)
                    continue;

                var x = Centre (ancestor_level);

                e.Canvas.DrawLine (x, top, x, bottom, colour, thickness);
            }
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
            => ImageBoundsFor (control, item);

        private Rectangle ImageBoundsFor (TreeView control, TreeNode item)
        {
            if (!control.ShowItemImages || ResolveImage (control, item, item == control.SelectedItem) is null)
                return Rectangle.Empty;

            var left_index = control.ShowDropdownGlyph ? GetGlyphBounds (control, item).Right : GetIndentStart (control, item);

            // The check box sits between the glyph and the image, so the image starts after it.
            left_index += control.ScaledCheckWidth;
            var image_area = new Rectangle (left_index, item.Bounds.Top, item.Bounds.Height, item.Bounds.Height);

            return DrawingExtensions.CenterSquare (image_area, control.LogicalToDeviceUnits (IMAGE_SIZE));
        }

        /// <summary>
        /// Gets the bounds of the item text.
        /// </summary>
        protected virtual Rectangle GetTextBounds (TreeView control, TreeNode item, PaintEventArgs e)
            => TextBoundsFor (control, item);

        /// <summary>The node's text rectangle in device pixels, computed outside a paint pass -- for the
        /// label editor (W6 mechanisms). The same arithmetic <see cref="GetTextBounds"/> uses.</summary>
        internal Rectangle TextBoundsFor (TreeView control, TreeNode item)
        {
            var show_glyph = control.ShowDropdownGlyph;
            var show_image = control.ShowItemImages;

            if (!show_glyph && !show_image)
                return new Rectangle (GetIndentStart (control, item), item.Bounds.Top, item.Bounds.Width - GetIndentStart (control, item), item.Bounds.Height);

            // One of these will be valid because we handled the other case above
            var padding = control.LogicalToDeviceUnits (6);
            var has_image = show_image && ResolveImage (control, item, item == control.SelectedItem) is not null;
            var used_bounds = has_image ? ImageBoundsFor (control, item) : GetGlyphBounds (control, item);
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
