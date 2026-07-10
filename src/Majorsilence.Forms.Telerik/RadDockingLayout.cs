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

        // Draws the tab headers and records their hit rectangles. No headers for a single-tab strip.
        internal static void PaintHeaders (Control strip, PaintEventArgs e, DockWindowBase? selected,
            List<(DockWindowBase win, Rectangle rect)> headerRects)
        {
            headerRects.Clear ();
            var ws = Windows (strip);
            if (ws.Count <= 1)
                return;

            var x = 0;
            using var unselFill = new SKPaint { Color = new SKColor (0xE1, 0xE1, 0xE1) };
            using var selFill = new SKPaint { Color = new SKColor (0xFF, 0xFF, 0xFF) };
            using var border = new SKPaint { Color = new SKColor (0xB0, 0xB0, 0xB0), IsStroke = true };

            foreach (var w in ws) {
                var caption = Caption (w);
                var width = Math.Max (60, caption.Length * 7 + 22);
                var r = new Rectangle (x, 0, width, HeaderHeight);

                e.Canvas.DrawRect (r.Left, r.Top, r.Width, r.Height, w == selected ? selFill : unselFill);
                e.Canvas.DrawRect (r.Left + 0.5f, r.Top + 0.5f, r.Width - 1, r.Height - 1, border);
                e.Canvas.DrawText (caption, new Rectangle (r.Left + 8, r.Top, r.Width - 12, r.Height),
                    strip, ContentAlignment.MiddleLeft, maxLines: 1);

                headerRects.Add ((w, r));
                x += width;
            }
        }

        internal static DockWindowBase? HitTest (List<(DockWindowBase win, Rectangle rect)> headerRects, Point p)
        {
            foreach (var (win, rect) in headerRects)
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
                if (MainDocumentContainerVisible)
                    main.Bounds = ClientRectangle;
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
        private readonly List<(DockWindowBase win, Rectangle rect)> _headerRects = new ();

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
            DockStrip.PaintHeaders (this, e, _selectedWindow, _headerRects);
        }

        /// <inheritdoc/>
        protected override void OnMouseDown (MouseEventArgs e)
        {
            base.OnMouseDown (e);
            var hit = DockStrip.HitTest (_headerRects, e.Location);
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
        private readonly List<(DockWindowBase win, Rectangle rect)> _headerRects = new ();

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
            DockStrip.PaintHeaders (this, e, _selectedWindow, _headerRects);
        }

        /// <inheritdoc/>
        protected override void OnMouseDown (MouseEventArgs e)
        {
            base.OnMouseDown (e);
            var hit = DockStrip.HitTest (_headerRects, e.Location);
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
