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

        // Header row on top when >1 window; the selected window fills the remaining client area, the rest hide.
        internal static void LayoutTabs (Control strip, DockWindowBase? selected)
        {
            var ws = Windows (strip);
            if (ws.Count == 0)
                return;

            selected ??= ws[0];
            var client = strip.ClientRectangle;
            var header = ws.Count > 1 ? HeaderHeight : 0;
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

        // Per-strip header paint/hit state, including the scroll offset used when the tab headers
        // are wider than the strip (real dock tab strips scroll their headers behind arrow buttons
        // rather than letting them run off the edge).
        internal sealed class HeaderState
        {
            public readonly List<(DockWindowBase win, Rectangle rect)> Rects = new ();
            public int ScrollOffset;
            public bool HasOverflow;
            public Rectangle LeftArrow;
            public Rectangle RightArrow;
        }

        private const int ArrowWidth = 18;
        private const int ScrollStep = 90;

        // Draws the tab headers and records their hit rectangles. No headers for a single-tab strip.
        // When the headers overflow the strip width, the row scrolls: headers shift left by the
        // state's ScrollOffset and clip before a pair of right-pinned arrow buttons.
        internal static void PaintHeaders (Control strip, PaintEventArgs e, DockWindowBase? selected, HeaderState state)
        {
            state.Rects.Clear ();
            state.HasOverflow = false;
            var ws = Windows (strip);
            if (ws.Count <= 1)
                return;

            using var unselFill = new SKPaint { Color = new SKColor (0xE1, 0xE1, 0xE1) };
            using var selFill = new SKPaint { Color = new SKColor (0xFF, 0xFF, 0xFF) };
            using var border = new SKPaint { Color = new SKColor (0xB0, 0xB0, 0xB0), IsStroke = true };

            // Measure every header with the strip's effective font (the same resolution the text
            // draw below uses) instead of estimating from character counts.
            var font = strip.GetEffectiveFont ();
            var fontSize = strip.LogicalToDeviceUnits (strip.GetEffectiveFontSize ());
            var widths = new int[ws.Count];
            var total = 0;
            for (var i = 0; i < ws.Count; i++) {
                var textWidth = (int) Math.Ceiling (TextMeasurer.MeasureText (Caption (ws[i]), font, fontSize).Width);
                widths[i] = Math.Max (60, textWidth + 20);
                total += widths[i];
            }

            var avail = strip.ClientRectangle.Width;
            state.HasOverflow = total > avail;
            var visibleWidth = avail;

            if (state.HasOverflow) {
                visibleWidth = Math.Max (0, avail - 2 * ArrowWidth);
                state.ScrollOffset = Math.Max (0, Math.Min (state.ScrollOffset, total - visibleWidth));
            } else {
                state.ScrollOffset = 0;
            }

            e.Canvas.Save ();
            e.Canvas.Clip (new Rectangle (0, 0, visibleWidth, HeaderHeight));

            var x = -state.ScrollOffset;
            for (var i = 0; i < ws.Count; i++) {
                var w = ws[i];
                var r = new Rectangle (x, 0, widths[i], HeaderHeight);
                x += widths[i];

                // Off-screen headers are neither painted nor clickable.
                if (r.Right <= 0 || r.Left >= visibleWidth)
                    continue;

                e.Canvas.DrawRect (r.Left, r.Top, r.Width, r.Height, w == selected ? selFill : unselFill);
                e.Canvas.DrawRect (r.Left + 0.5f, r.Top + 0.5f, r.Width - 1, r.Height - 1, border);
                e.Canvas.DrawText (Caption (w), new Rectangle (r.Left + 8, r.Top, r.Width - 12, r.Height),
                    strip, ContentAlignment.MiddleLeft, maxLines: 1);

                state.Rects.Add ((w, r));
            }

            e.Canvas.Restore ();

            if (state.HasOverflow) {
                state.LeftArrow = new Rectangle (avail - 2 * ArrowWidth, 0, ArrowWidth, HeaderHeight);
                state.RightArrow = new Rectangle (avail - ArrowWidth, 0, ArrowWidth, HeaderHeight);

                var canLeft = state.ScrollOffset > 0;
                var canRight = state.ScrollOffset < total - visibleWidth;

                PaintArrow (e, strip, state.LeftArrow, "◂", canLeft, unselFill, border);
                PaintArrow (e, strip, state.RightArrow, "▸", canRight, unselFill, border);
            } else {
                state.LeftArrow = Rectangle.Empty;
                state.RightArrow = Rectangle.Empty;
            }
        }

        private static void PaintArrow (PaintEventArgs e, Control strip, Rectangle r, string glyph, bool enabled,
            SKPaint fill, SKPaint border)
        {
            e.Canvas.DrawRect (r.Left, r.Top, r.Width, r.Height, fill);
            e.Canvas.DrawRect (r.Left + 0.5f, r.Top + 0.5f, r.Width - 1, r.Height - 1, border);
            e.Canvas.DrawText (glyph, strip.GetEffectiveFont (), strip.LogicalToDeviceUnits (strip.GetEffectiveFontSize ()),
                r, enabled ? Theme.ForegroundColor : Theme.ForegroundDisabledColor, ContentAlignment.MiddleCenter);
        }

        // Adjusts the scroll offset when an arrow is clicked. Returns true when the click was
        // consumed (so it must not fall through to header hit-testing).
        internal static bool HandleArrowClick (Control strip, HeaderState state, Point p)
        {
            if (!state.HasOverflow)
                return false;

            if (state.LeftArrow.Contains (p)) {
                state.ScrollOffset = Math.Max (0, state.ScrollOffset - ScrollStep);
                strip.Invalidate ();
                return true;
            }

            if (state.RightArrow.Contains (p)) {
                state.ScrollOffset += ScrollStep; // clamped against the measured total on next paint
                strip.Invalidate ();
                return true;
            }

            return false;
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
            if (main is not null) {
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

        /// <inheritdoc/>
        protected override void OnMouseDown (MouseEventArgs e)
        {
            base.OnMouseDown (e);

            if (DockStrip.HandleArrowClick (this, _headers, e.Location))
                return;

            var hit = DockStrip.HitTest (_headers, e.Location);
            if (hit is not null && hit != _selectedWindow) {
                var previous = _selectedWindow;
                _selectedWindow = hit;
                DockStrip.LayoutTabs (this, _selectedWindow);
                DockStrip.RaiseTabActivation (this, previous, hit);
                Invalidate ();
            }
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

        /// <inheritdoc/>
        protected override void OnMouseDown (MouseEventArgs e)
        {
            base.OnMouseDown (e);

            if (DockStrip.HandleArrowClick (this, _headers, e.Location))
                return;

            var hit = DockStrip.HitTest (_headers, e.Location);
            if (hit is not null && hit != _selectedWindow) {
                var previous = _selectedWindow;
                _selectedWindow = hit;
                DockStrip.LayoutTabs (this, _selectedWindow);
                DockStrip.RaiseTabActivation (this, previous, hit);
                Invalidate ();
            }
        }
    }
}
