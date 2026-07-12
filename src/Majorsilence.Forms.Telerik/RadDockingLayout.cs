using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using SkiaSharp;

namespace Majorsilence.Forms.Telerik
{
    // Minimal docking-layout rendering for the Telerik-compat dock. Telerik lays a RadDock out through a
    // SizeInfo/SplitPanel/tab-strip engine this compat layer doesn't have, so migrated docked forms
    // (frmMaintainCustomer/Property, frmMainBK, ...) parented their content correctly but it sized to
    // nothing and never tabbed. This fills the content chain (RadDock -> DocumentContainer ->
    // Document/ToolTabStrip) and renders the tab strips as real tabs (header row + the selected window
    // filling the rest), so the forms actually show. It is deliberately simple: single-child fill, no
    // draggable splitters/floating/auto-hide.
    internal static class DockStrip
    {
        internal const int HeaderHeight = 26;

        internal static List<DockWindowBase> Windows (Control strip)
            => strip.Controls.OfType<DockWindowBase> ().ToList ();

        internal static DockWindowBase? Normalize (Control strip, DockWindowBase? selected)
        {
            var ws = Windows (strip);
            if (ws.Count == 0)
                return null;
            return selected is not null && ws.Contains (selected) ? selected : ws[0];
        }

        // Flows the tab headers into rows within the strip's width, in LOGICAL units: a header that
        // would cross the right edge wraps to the next row -- WinForms multiline tab behavior --
        // so every tab stays reachable no matter how many the strip holds. Headers are measured
        // with the strip's effective font (the same resolution painting uses).
        internal static List<(DockWindowBase win, Rectangle rect)> FlowHeaders (Control strip, List<DockWindowBase> ws, out int rowCount)
        {
            var result = new List<(DockWindowBase, Rectangle)> ();
            var font = strip.GetEffectiveFont ();
            var fontSize = strip.GetEffectiveFontSize ();
            var avail = Math.Max (60, strip.ClientRectangle.Width);

            var x = 0;
            var row = 0;
            foreach (var w in ws) {
                var textWidth = (int) Math.Ceiling (TextMeasurer.MeasureText (Caption (w), font, fontSize).Width);
                var width = Math.Min (Math.Max (60, textWidth + 20), avail);

                if (x > 0 && x + width > avail) {
                    x = 0;
                    row++;
                }

                result.Add ((w, new Rectangle (x, row * HeaderHeight, width, HeaderHeight)));
                x += width;
            }

            rowCount = row + 1;
            return result;
        }

        // Header rows on top when >1 window; the selected window fills the remaining client area,
        // the rest hide. The header band grows by whole rows when the tabs wrap.
        internal static void LayoutTabs (Control strip, DockWindowBase? selected)
        {
            var ws = Windows (strip);
            if (ws.Count == 0)
                return;

            selected ??= ws[0];
            var client = strip.ClientRectangle;
            var header = 0;
            if (ws.Count > 1) {
                FlowHeaders (strip, ws, out var rows);
                header = rows * HeaderHeight;
            }
            var content = new Rectangle (client.Left, client.Top + header, client.Width, Math.Max (0, client.Height - header));

            foreach (var w in ws) {
                if (w == selected) {
                    w.Visible = true;
                    w.Bounds = content;
                } else {
                    w.Visible = false;
                }
            }
        }

        internal static string Caption (DockWindowBase w)
            => !string.IsNullOrEmpty (w.Text) ? w.Text
             : w is ToolWindow tw && !string.IsNullOrEmpty (tw.Caption) ? tw.Caption
             : w.Name ?? string.Empty;

        // Per-strip header hit state. Rects are stored in DEVICE units so hit-testing agrees with
        // mouse coordinates on scaled displays.
        internal sealed class HeaderState
        {
            public readonly List<(DockWindowBase win, Rectangle rect)> Rects = new ();
            public int RowCount = 1;
        }

        // Draws the wrapped tab header rows and records their hit rectangles. No headers for a
        // single-tab strip. Layout flows in logical units; painting and the recorded hit rects are
        // scaled to device units (the paint canvas is the control's device-pixel back buffer, so
        // logical-unit drawing renders undersized text and boxes on scaled displays).
        internal static void PaintHeaders (Control strip, PaintEventArgs e, DockWindowBase? selected, HeaderState state)
        {
            state.Rects.Clear ();
            state.RowCount = 1;
            var ws = Windows (strip);
            if (ws.Count <= 1)
                return;

            using var unselFill = new SKPaint { Color = new SKColor (0xE1, 0xE1, 0xE1) };
            using var selFill = new SKPaint { Color = new SKColor (0xFF, 0xFF, 0xFF) };
            using var border = new SKPaint { Color = new SKColor (0xB0, 0xB0, 0xB0), IsStroke = true };

            var flowed = FlowHeaders (strip, ws, out var rows);
            state.RowCount = rows;

            var font = strip.GetEffectiveFont ();
            var deviceFontSize = strip.LogicalToDeviceUnits (strip.GetEffectiveFontSize ());
            var pad = strip.LogicalToDeviceUnits (8);

            foreach (var (w, logical) in flowed) {
                var r = new Rectangle (
                    strip.LogicalToDeviceUnits (logical.Left),
                    strip.LogicalToDeviceUnits (logical.Top),
                    strip.LogicalToDeviceUnits (logical.Width),
                    strip.LogicalToDeviceUnits (logical.Height));

                e.Canvas.DrawRect (r.Left, r.Top, r.Width, r.Height, w == selected ? selFill : unselFill);
                e.Canvas.DrawRect (r.Left + 0.5f, r.Top + 0.5f, r.Width - 1, r.Height - 1, border);
                e.Canvas.DrawText (Caption (w), font, deviceFontSize,
                    new Rectangle (r.Left + pad, r.Top, r.Width - pad - 4, r.Height),
                    strip.Enabled ? strip.CurrentStyle.GetForegroundColor () : Theme.ForegroundDisabledColor,
                    ContentAlignment.MiddleLeft, maxLines: 1);

                state.Rects.Add ((w, r));
            }
        }

        internal static DockWindowBase? HitTest (HeaderState state, Point p)
        {
            foreach (var (win, rect) in state.Rects)
                if (rect.Contains (p))
                    return win;
            return null;
        }

        // Fires the activation notifications for a tab switch: Leave on the window being deselected,
        // Enter on the newly-selected one, and SelectedTabChanged on the owning RadDock. This is how
        // WinForms/Telerik tab selection surfaces to app code -- e.g. a form that loads a tab's grid
        // data in the document window's Enter handler, or in the dock's SelectedTabChanged handler.
        internal static void RaiseTabActivation (Control strip, DockWindowBase? previous, DockWindowBase current)
        {
            previous?.RaiseLeave ();
            current.RaiseEnter ();

            var ancestor = strip.Parent;
            while (ancestor is not null and not RadDock)
                ancestor = ancestor.Parent;

            (ancestor as RadDock)?.NotifySelectedTabChanged (previous, current);
        }

        // Fills the container's primary content child (a tab strip / nested container) to the client area.
        internal static void FillPrimary (Control container)
        {
            var primary = container.Controls.OfType<Control> ()
                .FirstOrDefault (c => c is DocumentTabStrip or ToolTabStrip or DocumentContainer or SplitPanel);
            if (primary is not null) {
                primary.Visible = true;
                primary.Bounds = container.ClientRectangle;
            }
        }
    }

    public partial class RadDock
    {
        /// <summary>
        /// Sets <see cref="ActiveWindow"/> to the newly-selected dock window and raises
        /// <see cref="SelectedTabChanged"/>. Called by the tab strips when the user switches tabs, so
        /// Telerik code that reacts to a document/tool tab becoming active runs (the compat dock
        /// otherwise never raised this event).
        /// </summary>
        internal void NotifySelectedTabChanged (DockWindowBase? previous, DockWindowBase current)
        {
            ActiveWindow = current;
            SelectedTabChanged?.Invoke (this, new SelectedTabChangedEventArgs { OldWindow = previous, NewWindow = current });
        }

        /// <summary>
        /// Activates the given dock window: selects its tab in the owning strip -- raising the
        /// standard Leave/Enter/SelectedTabChanged activation events, exactly as a user click on
        /// the tab header does -- and makes it the dock's <see cref="ActiveWindow"/>. Matches the
        /// real docking API surface, where programmatic activation is the documented way to bring
        /// a document or tool window to the front.
        /// </summary>
        public void ActivateWindow (DockWindowBase window)
        {
            switch (window?.Parent) {
                case DocumentTabStrip dts:
                    dts.SelectWindowInternal (window);
                    break;
                case ToolTabStrip tts:
                    tts.SelectWindowInternal (window);
                    break;
            }
        }

        /// <summary>
        /// Enumerates every <see cref="DocumentWindow"/> hosted anywhere in this dock's control tree.
        /// Document windows are parented into the document tab strips declaratively (not tracked in the
        /// tool-window list), so anything reporting the open documents -- e.g. DocumentManager
        /// .DocumentArray, which Telerik code queries to tell whether the dock has content -- must walk
        /// the tree rather than the tool-window list.
        /// </summary>
        internal IEnumerable<DocumentWindow> AllDocumentWindows ()
        {
            var stack = new Stack<Control> ();
            foreach (Control c in Controls)
                stack.Push (c);

            while (stack.Count > 0) {
                var c = stack.Pop ();
                if (c is DocumentWindow dw)
                    yield return dw;
                foreach (Control child in c.Controls)
                    stack.Push (child);
            }
        }

        /// <inheritdoc/>
        protected override void OnLayout (LayoutEventArgs e)
        {
            base.OnLayout (e);

            // Fill the main document container to the dock's client area (the content chain hangs off it).
            var main = (Control?) MainDocumentContainer
                       ?? Controls.OfType<DocumentContainer> ().FirstOrDefault ();

            if (main is null)
                return;

            // Some designer serializations put the actual content in TOOL strips parented directly
            // to the dock and leave the main document container EMPTY (the split engine this compat
            // lacks would have divided the space between them). An empty container has nothing to
            // show -- filling it frontmost would cover the real content with a blank panel -- so
            // hide it and promote the first visible content-bearing strip to fill the dock instead.
            if (main.Controls.Count == 0) {
                main.Visible = false;

                var strip = Controls.OfType<Control> ()
                    .FirstOrDefault (c => !ReferenceEquals (c, main) && c.Visible
                        && c is DocumentTabStrip or ToolTabStrip or DocumentContainer or SplitPanel);
                if (strip is not null) {
                    strip.Bounds = ClientRectangle;
                    if (Controls.GetChildIndex (strip, throwException: false) > 0)
                        Controls.SetChildIndex (strip, 0);
                }

                return;
            }

            main.Visible = MainDocumentContainerVisible;
            if (MainDocumentContainerVisible) {
                main.Bounds = ClientRectangle;

                // This compat dock has no SplitPanel engine: sibling tool strips keep their
                // designer bounds while the main container fills the WHOLE dock, so they always
                // overlap it. The container must therefore be frontmost (z-index 0) or a sibling
                // strip serialized before it -- e.g. a top-docked tool strip row -- paints over
                // the document tab band.
                if (Controls.GetChildIndex (main, throwException: false) > 0)
                    Controls.SetChildIndex (main, 0);
            }
        }

        private bool _initialFillDone;

        /// <inheritdoc/>
        protected override void OnPaint (PaintEventArgs e)
        {
            // The designer builds the dock inside SuspendLayout()/ResumeLayout(false) -- which never
            // performs the deferred layout -- and the dock keeps its designer size once shown, so its
            // size never changes and OnLayout (which fills the main document container, and from there
            // cascades the whole content chain) would otherwise never run. The form-show path does not
            // drive OnCreateControl/OnVisibleChanged for these hosted controls either, but paint is a
            // guaranteed callback on a realised, correctly-sized dock -- so force the fill once here.
            // Subsequent resizes go through OnLayout as usual.
            if (!_initialFillDone) {
                _initialFillDone = true;
                PerformLayout ();

                // The fill must CASCADE explicitly: assigning a child container the same bounds it
                // already has (the common case when the form shows at its designer size) raises no
                // resize, so the child's own OnLayout -- where tab strips normalize their selected
                // window and hide the rest -- would never run and every document window would stay
                // visible, stacked at its serialized designer position with no header row painted.
                foreach (var child in DockDescendants (this))
                    child.PerformLayout ();
            }

            base.OnPaint (e);
        }

        private static IEnumerable<Control> DockDescendants (Control root)
        {
            foreach (Control c in root.Controls) {
                if (c is DocumentContainer or DocumentTabStrip or ToolTabStrip or SplitPanel)
                    yield return c;
                foreach (var nested in DockDescendants (c))
                    yield return nested;
            }
        }
    }

    public partial class DocumentContainer
    {
        /// <inheritdoc/>
        protected override void OnLayout (LayoutEventArgs e)
        {
            base.OnLayout (e);
            DockStrip.FillPrimary (this);
        }
    }

    public partial class DocumentTabStrip
    {
        private DockWindowBase? _selectedWindow;
        private readonly DockStrip.HeaderState _headers = new ();

        /// <inheritdoc/>
        protected override void OnLayout (LayoutEventArgs e)
        {
            base.OnLayout (e);
            _selectedWindow = DockStrip.Normalize (this, _selectedWindow);
            DockStrip.LayoutTabs (this, _selectedWindow);
        }

        /// <inheritdoc/>
        protected override void OnPaint (PaintEventArgs e)
        {
            base.OnPaint (e);
            DockStrip.PaintHeaders (this, e, _selectedWindow, _headers);
        }

        // Selects the given window's tab, raising the standard activation events -- the shared
        // implementation behind both a header click and programmatic RadDock.ActivateWindow.
        internal void SelectWindowInternal (DockWindowBase win)
        {
            if (win == _selectedWindow || !Controls.Contains (win))
                return;

            var previous = _selectedWindow;
            _selectedWindow = win;
            DockStrip.LayoutTabs (this, _selectedWindow);
            DockStrip.RaiseTabActivation (this, previous, win);
            Invalidate ();
        }

        /// <inheritdoc/>
        protected override void OnMouseDown (MouseEventArgs e)
        {
            base.OnMouseDown (e);

            var hit = DockStrip.HitTest (_headers, e.Location);
            if (hit is not null)
                SelectWindowInternal (hit);
        }
    }

    public partial class ToolTabStrip
    {
        private DockWindowBase? _selectedWindow;
        private readonly DockStrip.HeaderState _headers = new ();

        /// <inheritdoc/>
        protected override void OnLayout (LayoutEventArgs e)
        {
            base.OnLayout (e);
            _selectedWindow = DockStrip.Normalize (this, _selectedWindow);
            DockStrip.LayoutTabs (this, _selectedWindow);
        }

        /// <inheritdoc/>
        protected override void OnPaint (PaintEventArgs e)
        {
            base.OnPaint (e);
            DockStrip.PaintHeaders (this, e, _selectedWindow, _headers);
        }

        // Selects the given window's tab, raising the standard activation events -- the shared
        // implementation behind both a header click and programmatic RadDock.ActivateWindow.
        internal void SelectWindowInternal (DockWindowBase win)
        {
            if (win == _selectedWindow || !Controls.Contains (win))
                return;

            var previous = _selectedWindow;
            _selectedWindow = win;
            DockStrip.LayoutTabs (this, _selectedWindow);
            DockStrip.RaiseTabActivation (this, previous, win);
            Invalidate ();
        }

        /// <inheritdoc/>
        protected override void OnMouseDown (MouseEventArgs e)
        {
            base.OnMouseDown (e);

            var hit = DockStrip.HitTest (_headers, e.Location);
            if (hit is not null)
                SelectWindowInternal (hit);
        }
    }
}
