using System.Drawing;
using SkiaSharp;

namespace Majorsilence.Forms.Renderers
{
    /// <summary>
    /// Renders a <see cref="PropertyGrid"/>: the view of property rows, the category headers and their
    /// expanders, the commands pane and the help pane (W6 mechanisms — the grid used to paint a flat
    /// list of rows and nothing else, so its help, commands and border colours had nothing to colour).
    /// </summary>
    public class PropertyGridRenderer : Renderer<PropertyGrid>
    {
        /// <inheritdoc/>
        protected override void Render (PropertyGrid control, PaintEventArgs e)
        {
            RenderView (control, e);
            RenderCommands (control, e);
            RenderHelp (control, e);
        }

        /// <summary>Renders the property rows and the border around them.</summary>
        protected virtual void RenderView (PropertyGrid control, PaintEventArgs e)
        {
            var view = control.ViewBounds;

            if (view.Width <= 0 || view.Height <= 0)
                return;

            e.Canvas.Save ();
            e.Canvas.Clip (view);
            e.Canvas.FillRectangle (view, SK (control.ViewBackColor));

            if (control.SelectedObject is null || control.VisibleRows.Count == 0) {
                e.Canvas.DrawText ("(no object selected)", Theme.UIFont, e.LogicalToDeviceUnits (11), view,
                    Theme.ForegroundColor, ContentAlignment.MiddleCenter);
                e.Canvas.Restore ();
                ViewBorder (control, view, e);
                return;
            }

            var name_width = control.ScaledNameColumnWidth;
            var line = SK (control.LineColor);
            var font_size = e.LogicalToDeviceUnits (10);

            for (var i = 0; i < control.VisibleRows.Count; i++) {
                var row = control.RowBounds (i);

                if (row.Bottom < view.Top || row.Top > view.Bottom)
                    continue;

                var item = control.VisibleRows[i];
                var is_category = item.GridItemType == GridItemType.Category;
                var is_selected = ReferenceEquals (item, control.SelectedGridItem);

                if (is_category) {
                    e.Canvas.FillRectangle (row, Theme.ControlMidColor);
                    RenderExpander (control, i, item.Expanded, e);

                    var label = new Rectangle (row.Left + e.LogicalToDeviceUnits (16), row.Top,
                        row.Width - e.LogicalToDeviceUnits (16), row.Height);

                    e.Canvas.DrawText (item.Label, Theme.UIFont, font_size, label, SK (control.CategoryForeColor),
                        ContentAlignment.MiddleLeft, maxLines: 1);

                    // CategorySplitterColor: the rule under a category header (W6 mechanisms).
                    e.Canvas.DrawLine (row.Left, row.Bottom - 1, row.Right, row.Bottom - 1, SK (control.CategorySplitterColor));
                    continue;
                }

                var background = is_selected ? SelectionBack (control) : SK (control.ViewBackColor);

                // DisabledItemForeColor: a read-only property is drawn in it (W6 mechanisms).
                var foreground = is_selected
                    ? SelectionFore (control)
                    : item.PropertyDescriptor is { IsReadOnly: true }
                        ? SK (control.DisabledItemForeColor)
                        : SK (control.ViewForeColor);

                e.Canvas.FillRectangle (row, background);

                var indent = item.Parent is null ? 2 : 16;
                var name = new Rectangle (row.Left + e.LogicalToDeviceUnits (indent), row.Top,
                    name_width - e.LogicalToDeviceUnits (indent), row.Height);
                var value = new Rectangle (row.Left + name_width + e.LogicalToDeviceUnits (2), row.Top,
                    row.Width - name_width - e.LogicalToDeviceUnits (4), row.Height);

                e.Canvas.DrawText (item.Label, Theme.UIFont, font_size, name, foreground, ContentAlignment.MiddleLeft, maxLines: 1);

                // The open editor draws the value itself.
                if (!ReferenceEquals (item, control.EditingItem))
                    e.Canvas.DrawText (PropertyGrid.ValueTextOf (item), Theme.UIFont, font_size, value, foreground,
                        ContentAlignment.MiddleLeft, maxLines: 1);

                e.Canvas.DrawLine (row.Left, row.Bottom - 1, row.Right, row.Bottom - 1, line);
            }

            // The column separator, down the rows that exist.
            var last = control.RowBounds (control.VisibleRows.Count - 1);
            e.Canvas.DrawLine (view.Left + name_width, view.Top, view.Left + name_width, System.Math.Min (view.Bottom, last.Bottom), line);

            e.Canvas.Restore ();
            ViewBorder (control, view, e);
        }

        // ViewBorderColor: the frame around the property view (W6 mechanisms).
        private static void ViewBorder (PropertyGrid control, Rectangle view, PaintEventArgs e)
            => e.Canvas.DrawRectangle (view, SK (control.ViewBorderColor));

        /// <summary>Renders a category's expand/collapse glyph.</summary>
        protected virtual void RenderExpander (PropertyGrid control, int index, bool expanded, PaintEventArgs e)
        {
            var box = control.ExpanderBounds (index);

            e.Canvas.DrawRectangle (box, SK (control.CategoryForeColor));

            var mid_y = box.Top + (box.Height / 2);
            var mid_x = box.Left + (box.Width / 2);

            e.Canvas.DrawLine (box.Left + 2, mid_y, box.Right - 2, mid_y, SK (control.CategoryForeColor));

            if (!expanded)
                e.Canvas.DrawLine (mid_x, box.Top + 2, mid_x, box.Bottom - 2, SK (control.CategoryForeColor));
        }

        /// <summary>Renders the commands pane: the selected object's designer verbs as links.</summary>
        protected virtual void RenderCommands (PropertyGrid control, PaintEventArgs e)
        {
            var pane = control.CommandsBounds;

            if (pane.IsEmpty)
                return;

            e.Canvas.FillRectangle (pane, SK (control.CommandsBackColor));
            e.Canvas.DrawRectangle (pane, SK (control.CommandsBorderColor));

            var verbs = control.Verbs;
            var font_size = e.LogicalToDeviceUnits (10);

            for (var i = 0; i < verbs.Count; i++) {
                var bounds = control.CommandBounds (i);

                // A disabled verb takes CommandsDisabledLinkColor; an enabled one the link colour, and
                // the one under the pointer the active colour (W6 mechanisms).
                var colour = !verbs[i].Enabled ? SK (control.CommandsDisabledLinkColor)
                    : bounds.Contains (control.LogicalToDeviceUnits (control.LastMousePosition)) ? SK (control.CommandsActiveLinkColor)
                    : SK (control.CommandsLinkColor);

                e.Canvas.DrawText (verbs[i].Text ?? string.Empty, Theme.UIFont, font_size, bounds, colour,
                    ContentAlignment.MiddleLeft, maxLines: 1);
            }
        }

        /// <summary>Renders the help pane: the selected property's name and description.</summary>
        protected virtual void RenderHelp (PropertyGrid control, PaintEventArgs e)
        {
            var pane = control.HelpBounds;

            if (pane.IsEmpty)
                return;

            e.Canvas.FillRectangle (pane, SK (control.HelpBackColor));
            e.Canvas.DrawRectangle (pane, SK (control.HelpBorderColor));

            var (title, description) = control.HelpText;

            if (string.IsNullOrEmpty (title))
                return;

            var inset = e.LogicalToDeviceUnits (4);
            var line_height = e.LogicalToDeviceUnits (16);
            var fore = SK (control.HelpForeColor);

            e.Canvas.DrawText (title, Theme.UIFontBold, e.LogicalToDeviceUnits (10),
                new Rectangle (pane.Left + inset, pane.Top + inset, pane.Width - (inset * 2), line_height),
                fore, ContentAlignment.MiddleLeft, maxLines: 1);

            if (!string.IsNullOrEmpty (description))
                e.Canvas.DrawText (description, Theme.UIFont, e.LogicalToDeviceUnits (10),
                    new Rectangle (pane.Left + inset, pane.Top + inset + line_height, pane.Width - (inset * 2),
                        pane.Height - inset - line_height - inset),
                    fore, ContentAlignment.TopLeft, maxLines: 2);
        }

        // With focus the selection takes SelectedItemWithFocusBackColor/ForeColor, as upstream's does;
        // without it the theme's accent stays, since upstream's unfocused grey is not a property.
        private static SKColor SelectionBack (PropertyGrid control)
            => control.Focused ? SK (control.SelectedItemWithFocusBackColor) : Theme.AccentColor;

        private static SKColor SelectionFore (PropertyGrid control)
            => control.Focused ? SK (control.SelectedItemWithFocusForeColor) : Theme.ForegroundColorOnAccent;

        private static SKColor SK (Color c) => new SKColor (c.R, c.G, c.B, c.A);
    }
}
