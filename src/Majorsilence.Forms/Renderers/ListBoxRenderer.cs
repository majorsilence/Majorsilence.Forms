using System.Drawing;

namespace Majorsilence.Forms.Renderers
{
    /// <summary>
    /// Represents a class that can render a ListBox.
    /// </summary>
    public class ListBoxRenderer : Renderer<ListBox>
    {
        /// <inheritdoc/>
        protected override void Render (ListBox control, PaintEventArgs e)
        {
            e.Canvas.Save ();
            e.Canvas.Clip (control.ItemsArea);

            // +2: with a sub-row touch-scroll offset item[FirstVisibleIndex] is partly above the top
            // and one extra row peeks in at the bottom. The Clip above trims both.
            for (var i = control.FirstVisibleIndex; i < Math.Min (control.Items.Count, control.FirstVisibleIndex + control.VisibleItemCount + 2); i++) {
                var item = control.Items[i];
                var bounds = control.GetItemRectangleDevice (i);

                RenderItem (control, item, i, bounds, e);
            }

            // If there are no items we still may need to draw a focus rectangle
            if (control.Items.Count == 0 && control.Selected && control.ShowFocusCues) {
                var client = control.ClientRectangle;
                client.Height = control.ScaledItemHeight;

                e.Canvas.DrawFocusRectangle (client, 1);
            }

            e.Canvas.Restore ();
        }

        /// <summary>
        /// Renders a ListBox item.
        /// </summary>
        protected virtual void RenderItem (ListBox control, object item, int index, Rectangle bounds, PaintEventArgs e)
        {
            // Draw selected background. ShowsSelection is where HideSelection is read: a list that has
            // lost focus gives up its highlight only when the application asked for that.
            var selected = control.Items.SelectedIndexes.Contains (index) && control.ShowsSelection;

            // Owner draw (W6 mechanisms): the application paints the item; nothing here does.
            if (control.DrawMode != DrawMode.Normal) {
                var state = DrawItemState.None;

                if (selected)
                    state |= DrawItemState.Selected;
                if (control.Selected && control.ShowFocusCues && control.Items.FocusedIndex == index)
                    state |= DrawItemState.Focus;
                if (!control.Enabled)
                    state |= DrawItemState.Disabled;
                if (control.Items.HoveredIndex == index)
                    state |= DrawItemState.HotLight;

                bounds.Height = control.ItemHeightDeviceAt (index);
                control.RaiseDrawItem (index, bounds, state, e);
                return;
            }

            if (selected)
                e.Canvas.FillRectangle (bounds, ListBox.DefaultSelectionStyle.GetBackgroundColor ());

            // Draw hover background
            else if (control.ShowHover && control.Items.HoveredIndex == index)
                e.Canvas.FillRectangle (bounds, Theme.ControlMidColor);

            // Draw focus rectangle
            if (control.Selected && control.ShowFocusCues && control.Items.FocusedIndex == index)
                e.Canvas.DrawFocusRectangle (bounds, 1);

            // This fixes text positioning for partially shown items
            bounds.Height = control.ItemHeightDeviceAt (index);
            bounds.Inflate (-4, 0);

            // Draw text
            // GetItemText, not ToString: items are the bound objects, so DisplayMember decides the text.
            var text = control.GetItemText (item);

            // Tab stops (W6 mechanisms): a tabbed item is drawn segment by segment at the stops.
            if (control.UseTabStops && text.Contains ('\t')) {
                var colour = selected && ListBox.DefaultSelectionStyle.ForegroundColor is { } sel_fg && control.Enabled
                    ? sel_fg
                    : control.Enabled ? control.GetEffectiveForegroundColor () : Theme.ForegroundDisabledColor;
                RenderTabbedText (control, text, bounds, colour, e);
                return;
            }

            if (selected && ListBox.DefaultSelectionStyle.ForegroundColor is { } selection_fg && control.Enabled)
                e.Canvas.DrawText (text, control.GetEffectiveFont (), control.LogicalToDeviceUnits (control.GetEffectiveFontSize ()), bounds, selection_fg, ContentAlignment.MiddleLeft, maxLines: 1);
            else
                e.Canvas.DrawText (text, bounds, control, ContentAlignment.MiddleLeft, maxLines: 1);
        }

        /// <summary>
        /// Draws text containing tabs across the list's tab stops: every eight average characters by
        /// default, or the control's <see cref="ListBox.CustomTabOffsets"/> (logical pixels from the
        /// item's left) when <see cref="ListBox.UseCustomTabOffsets"/> is on, then the default spacing
        /// beyond the last custom stop.
        /// </summary>
        protected virtual void RenderTabbedText (ListBox control, string text, Rectangle bounds, SkiaSharp.SKColor colour, PaintEventArgs e)
        {
            var font = control.GetEffectiveFont ();
            var font_size = control.LogicalToDeviceUnits (control.GetEffectiveFontSize ());
            var average = Math.Max (1f, TextMeasurer.MeasureText ("0123456789", font, font_size).Width / 10f);
            var default_stop = Math.Max (1, (int) Math.Round (average * 8));
            var custom = control.UseCustomTabOffsets ? control.CustomTabOffsets : null;

            var segments = text.Split ('\t');
            var x = bounds.Left;

            for (var i = 0; i < segments.Length; i++) {
                if (i > 0) {
                    // The next stop strictly to the right of where the previous segment ended.
                    var relative = x - bounds.Left;
                    var next = -1;

                    if (custom is { Count: > 0 })
                        foreach (var offset in custom) {
                            var device = control.LogicalToDeviceUnits (offset);

                            if (device > relative) {
                                next = device;
                                break;
                            }
                        }

                    if (next < 0)
                        next = (relative / default_stop + 1) * default_stop;

                    x = bounds.Left + next;
                }

                if (segments[i].Length == 0 || x >= bounds.Right)
                    continue;

                var segment_bounds = new Rectangle (x, bounds.Top, bounds.Right - x, bounds.Height);
                e.Canvas.DrawText (segments[i], font, font_size, segment_bounds, colour, ContentAlignment.MiddleLeft, maxLines: 1);
                x += (int) Math.Ceiling (TextMeasurer.MeasureText (segments[i], font, font_size).Width);
            }
        }
    }
}
