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

        // The windows that have a tab: a hidden window keeps its place in the strip but shows no tab
        // (W6 mechanisms, #176).
        internal static List<DockWindowBase> Windows (Control strip)
            => strip.Controls.OfType<DockWindowBase> ().Where (w => w.DockState != DockState.Hidden).ToList ();

        internal const int CaptionHeight = 22;
        internal const int CloseBoxWidth = 16;

        // How a strip arranges itself: whether tab headers show, whether they sit below the content,
        // the height of a caption band above everything, and whether each tab carries a close box.
        internal readonly struct Shape
        {
            internal Shape (bool headers, bool bottom, int caption, bool close)
            {
                Headers = headers;
                Bottom = bottom;
                Caption = caption;
                Close = close;
            }

            internal bool Headers { get; }
            internal bool Bottom { get; }
            internal int Caption { get; }
            internal bool Close { get; }
        }

        internal static Shape ShapeOf (Control strip) => strip switch {
            // Left and Right place the tabs at the bottom: vertical tab headers are not drawn.
            ToolTabStrip tool => new Shape (tool.TabStripVisible, tool.TabStripAlignment != TabStripAlignment.Top,
                tool.CaptionVisible ? CaptionHeight : 0, close: false),
            DocumentTabStrip document => new Shape (true, false, 0, (document.DocumentButtons & DocumentStripButtons.Close) != 0),
            _ => new Shape (true, false, 0, false),
        };

        // Whether a window is the selected tab of the strip it is in.
        internal static bool IsSelected (DockWindowBase window) => window.Parent switch {
            DocumentTabStrip document => ReferenceEquals (document.SelectedTab, window),
            ToolTabStrip tool => ReferenceEquals (tool.ActiveWindow, window),
            _ => false,
        };

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
        // Control.ClientRectangle is device-scaled while Bounds is logical, so assigning one to the other
        // multiplies a child's size by the display factor -- and it compounds once per nesting level:
        // measured on a 2x display, a 400-logical dock produced a 1600-logical tab strip (dock -> container
        // -> strip), which is why nothing wrapped and hit-testing missed every header.
        internal static Rectangle LogicalClient (Control c)
        {
            var r = c.ClientRectangle;
            return new Rectangle (
                c.DeviceToLogicalUnits (r.X), c.DeviceToLogicalUnits (r.Y),
                c.DeviceToLogicalUnits (r.Width), c.DeviceToLogicalUnits (r.Height));
        }

        internal static List<(DockWindowBase win, Rectangle rect)> FlowHeaders (Control strip, List<DockWindowBase> ws, out int rowCount)
        {
            var result = new List<(DockWindowBase, Rectangle)> ();
            var font = strip.GetEffectiveFont ();
            var fontSize = strip.GetEffectiveFontSize ();
            var avail = Math.Max (60, LogicalClient (strip).Width);

            var x = 0;
            var row = 0;
            foreach (var w in ws) {
                var textWidth = (int) Math.Ceiling (TextMeasurer.MeasureText (Caption (w), font, fontSize).Width);
                var close = ShapeOf (strip).Close ? CloseBoxWidth : 0;
                var width = Math.Min (Math.Max (60, textWidth + 20 + close), avail);

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
            // A hidden window is out of view whatever else happens here.
            foreach (var hidden in strip.Controls.OfType<DockWindowBase> ().Where (w => w.DockState == DockState.Hidden))
                hidden.Visible = false;

            var ws = Windows (strip);

            if (ws.Count == 0)
                return;

            selected ??= ws[0];

            var shape = ShapeOf (strip);
            var client = LogicalClient (strip);
            var header = HeaderBand (strip, ws, shape);
            var top = client.Top + shape.Caption + (shape.Bottom ? 0 : header);
            var content = new Rectangle (client.Left, top, client.Width, Math.Max (0, client.Height - shape.Caption - header));

            foreach (var w in ws) {
                if (w == selected) {
                    w.Visible = true;
                    w.Bounds = content;
                } else {
                    w.Visible = false;
                }
            }
        }

        // The height of the tab header rows: none for a single tab or when the strip hides its tabs.
        private static int HeaderBand (Control strip, List<DockWindowBase> ws, Shape shape)
        {
            if (ws.Count <= 1 || !shape.Headers)
                return 0;

            FlowHeaders (strip, ws, out var rows);
            return rows * HeaderHeight;
        }

        internal static string Caption (DockWindowBase w)
            => !string.IsNullOrEmpty (w.Text) ? w.Text
             : w is ToolWindow tw && !string.IsNullOrEmpty (tw.Caption) ? tw.Caption
             : w.Name ?? string.Empty;

        // Per-strip header hit state. Rects are stored in LOGICAL units, matching MouseEventArgs.
        internal sealed class HeaderState
        {
            public readonly List<(DockWindowBase win, Rectangle rect)> Rects = new ();
            public readonly List<(DockWindowBase win, Rectangle rect)> CloseRects = new ();
            public int RowCount = 1;
        }

        // Draws the wrapped tab header rows and records their hit rectangles. No headers for a
        // single-tab strip. Layout flows in logical units; painting and the recorded hit rects are
        // scaled to device units (the paint canvas is the control's device-pixel back buffer, so
        // logical-unit drawing renders undersized text and boxes on scaled displays).
        internal static void PaintHeaders (Control strip, PaintEventArgs e, DockWindowBase? selected, HeaderState state)
        {
            state.Rects.Clear ();
            state.CloseRects.Clear ();
            state.RowCount = 1;

            var ws = Windows (strip);
            var shape = ShapeOf (strip);
            var client = LogicalClient (strip);
            var font = strip.GetEffectiveFont ();
            var deviceFontSize = strip.LogicalToDeviceUnits (strip.GetEffectiveFontSize ());
            var pad = strip.LogicalToDeviceUnits (8);
            var fore = strip.Enabled ? strip.GetEffectiveForegroundColor () : Theme.ForegroundDisabledColor;

            Rectangle Device (Rectangle r) => new (
                strip.LogicalToDeviceUnits (r.Left), strip.LogicalToDeviceUnits (r.Top),
                strip.LogicalToDeviceUnits (r.Width), strip.LogicalToDeviceUnits (r.Height));

            // The caption band: the selected window's caption across the top (a tool strip's title).
            if (shape.Caption > 0 && ws.Count > 0) {
                var band = Device (new Rectangle (client.Left, client.Top, client.Width, shape.Caption));
                e.Canvas.FillRectangle (band, Theme.ControlMidColor);
                e.Canvas.DrawText (Caption (selected ?? ws[0]), font, deviceFontSize,
                    new Rectangle (band.Left + pad, band.Top, Math.Max (0, band.Width - pad), band.Height),
                    fore, ContentAlignment.MiddleLeft, maxLines: 1);
            }

            if (ws.Count <= 1 || !shape.Headers)
                return;

            using var unselFill = new SKPaint { Color = new SKColor (0xE1, 0xE1, 0xE1) };
            using var selFill = new SKPaint { Color = new SKColor (0xFF, 0xFF, 0xFF) };
            using var border = new SKPaint { Color = new SKColor (0xB0, 0xB0, 0xB0), IsStroke = true };

            var flowed = FlowHeaders (strip, ws, out var rows);
            state.RowCount = rows;

            // Where the header rows start: under the caption, or along the bottom edge.
            var origin = shape.Bottom
                ? client.Bottom - (rows * HeaderHeight)
                : client.Top + shape.Caption;

            foreach (var (w, flow) in flowed) {
                // Logical: MouseEventArgs coordinates are logical, so the hit rects have to match. The
                // device rect is only for painting onto the device-pixel canvas.
                var logical = new Rectangle (client.Left + flow.Left, origin + flow.Top, flow.Width, flow.Height);
                var r = Device (logical);

                e.Canvas.DrawRect (r.Left, r.Top, r.Width, r.Height, w == selected ? selFill : unselFill);
                e.Canvas.DrawRect (r.Left + 0.5f, r.Top + 0.5f, r.Width - 1, r.Height - 1, border);

                var close_width = shape.Close ? strip.LogicalToDeviceUnits (CloseBoxWidth) : 0;

                e.Canvas.DrawText (Caption (w), font, deviceFontSize,
                    new Rectangle (r.Left + pad, r.Top, Math.Max (0, r.Width - pad - 4 - close_width), r.Height),
                    fore, ContentAlignment.MiddleLeft, maxLines: 1);

                state.Rects.Add ((w, logical));

                if (shape.Close) {
                    // The close box: a cross in the tab's right end (DocumentButtons.Close).
                    var box = new Rectangle (logical.Right - CloseBoxWidth - 2, logical.Top + ((logical.Height - CloseBoxWidth) / 2), CloseBoxWidth, CloseBoxWidth);
                    var d = Device (box);
                    var inset = strip.LogicalToDeviceUnits (4);
                    using var cross = new SKPaint { Color = fore, IsStroke = true, StrokeWidth = Math.Max (1, strip.LogicalToDeviceUnits (1)), IsAntialias = true };
                    e.Canvas.DrawLine (d.Left + inset, d.Top + inset, d.Right - inset, d.Bottom - inset, cross);
                    e.Canvas.DrawLine (d.Right - inset, d.Top + inset, d.Left + inset, d.Bottom - inset, cross);
                    state.CloseRects.Add ((w, box));
                }
            }
        }

        // The window whose close box is under a point, or null.
        internal static DockWindowBase? HitClose (HeaderState state, Point p)
        {
            foreach (var (win, rect) in state.CloseRects)
                if (rect.Contains (p))
                    return win;

            return null;
        }

        // The dock a strip belongs to, or null.
        internal static RadDock? DockOf (Control strip)
        {
            var ancestor = strip.Parent;

            while (ancestor is not null and not RadDock)
                ancestor = ancestor.Parent;

            return ancestor as RadDock;
        }

        // A right-click on a tab: the dock's ContextMenuService hears it first and may change or empty
        // the menu, which then opens (W6 mechanisms, #176).
        internal static void ShowTabMenu (Control strip, DockWindowBase window, Point location)
        {
            var dock = DockOf (strip);
            var args = new ContextMenuDisplayingEventArgs { DockWindow = window };

            void Add (string text, Action action)
            {
                var item = new RadMenuItem (text, text);
                item.Click += (_, _) => action ();
                args.MenuItems.Add (item);
            }

            void CloseOne (DockWindowBase w)
            {
                if (dock is not null)
                    dock.CloseWindow (w);
                else
                    w.Close ();
            }

            if (strip is DocumentTabStrip) {
                Add ("Close", () => CloseOne (window));
                Add ("Close All But This", () => { foreach (var w in Windows (strip).Where (w => w != window)) CloseOne (w); });
                Add ("Close All", () => { foreach (var w in Windows (strip)) CloseOne (w); });
            } else {
                Add ("Hide", () => CloseOne (window));
            }

            dock?.RaiseContextMenuDisplaying (args);
            LastMenu = args;

            var menu = new ContextMenu ();

            foreach (var item in args.MenuItems)
                if (item is MenuItem menu_item)
                    menu.Items.Add (menu_item);

            if (menu.Items.Count > 0)
                menu.Show (strip, location);
        }

        /// <summary>The args of the last tab menu shown. Test seam.</summary>
        internal static ContextMenuDisplayingEventArgs? LastMenu { get; private set; }

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
                primary.Bounds = LogicalClient (container);
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
        internal void RaiseContextMenuDisplaying (ContextMenuDisplayingEventArgs e)
            => (_contextMenuService ??= new ContextMenuService ()).RaiseContextMenuDisplaying (e);

        internal void NotifySelectedTabChanged (DockWindowBase? previous, DockWindowBase current)
        {
            active_window = current;
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
                    strip.Bounds = DockStrip.LogicalClient (this);
                    if (Controls.GetChildIndex (strip, throwException: false) > 0)
                        Controls.SetChildIndex (strip, 0);
                }

                return;
            }

            main.Visible = MainDocumentContainerVisible;
            if (MainDocumentContainerVisible) {
                main.Bounds = DockStrip.LogicalClient (this);

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
        private int pending_index = -1;
        private readonly DockStrip.HeaderState _headers = new ();

        /// <summary>The painted header and close-box rectangles, in logical units. Test seam.</summary>
        internal DockStrip.HeaderState Headers => _headers;

        /// <summary>Gets or sets the index of the selected tab.</summary>
        /// <remarks>Real as of W6 mechanisms (#176): setting it selects that tab, raising the same
        /// activation events a click does. An index set before the windows are added -- the designer's
        /// order -- is applied once they are.</remarks>
        public int SelectedIndex {
            get => _selectedWindow is { } w ? DockStrip.Windows (this).IndexOf (w) : -1;
            set {
                var ws = DockStrip.Windows (this);

                if (value >= 0 && value < ws.Count)
                    SelectWindowInternal (ws[value]);
                else
                    pending_index = value;
            }
        }

        /// <summary>Raised when the selected tab changes, by a click or through <see cref="SelectedIndex"/>/<see cref="SelectedTab"/>.</summary>
        public event EventHandler? SelectedIndexChanged;

        /// <summary>Gets or sets the selected tab's window.</summary>
        public object? SelectedTab {
            get => _selectedWindow;
            set {
                if (value is DockWindowBase window)
                    SelectWindowInternal (window);
            }
        }

        /// <summary>Gets or sets the dock window whose tab is active. Alias of SelectedTab for Telerik-shape compat.</summary>
        public DockWindowBase? ActiveWindow {
            get => _selectedWindow;
            set => SelectedTab = value;
        }

        /// <summary>Gets or sets which document buttons show. Defaults to all.</summary>
        /// <remarks>Read as of W6 mechanisms (#176): with <see cref="DocumentStripButtons.Close"/> every
        /// tab carries a close box, and a click on it closes that window. The scroll and item-list
        /// buttons are not drawn; the headers wrap onto more rows instead.</remarks>
        public DocumentStripButtons DocumentButtons {
            get => document_buttons;
            set {
                if (document_buttons == value)
                    return;

                document_buttons = value;
                PerformLayout ();
                Invalidate ();
            }
        }

        private DocumentStripButtons document_buttons = DocumentStripButtons.All;

        /// <inheritdoc/>
        protected override void OnLayout (LayoutEventArgs e)
        {
            base.OnLayout (e);
            ApplyPendingIndex ();
            _selectedWindow = DockStrip.Normalize (this, _selectedWindow);
            DockStrip.LayoutTabs (this, _selectedWindow);
        }

        private void ApplyPendingIndex ()
        {
            if (pending_index < 0)
                return;

            var ws = DockStrip.Windows (this);

            if (pending_index < ws.Count) {
                _selectedWindow = ws[pending_index];
                pending_index = -1;
            }
        }

        /// <inheritdoc/>
        protected override void OnPaint (PaintEventArgs e)
        {
            base.OnPaint (e);
            DockStrip.PaintHeaders (this, e, _selectedWindow, _headers);
        }

        // Selects the given window's tab, raising the standard activation events -- the shared
        // implementation behind a header click, SelectedIndex/SelectedTab and RadDock.ActivateWindow.
        internal void SelectWindowInternal (DockWindowBase win)
        {
            if (win == _selectedWindow || !Controls.Contains (win) || win.DockState == DockState.Hidden)
                return;

            var previous = _selectedWindow;
            _selectedWindow = win;
            pending_index = -1;
            DockStrip.LayoutTabs (this, _selectedWindow);
            DockStrip.RaiseTabActivation (this, previous, win);
            SelectedIndexChanged?.Invoke (this, EventArgs.Empty);
            Invalidate ();
        }

        /// <inheritdoc/>
        protected override void OnMouseDown (MouseEventArgs e)
        {
            base.OnMouseDown (e);

            if (DockStrip.HitTest (_headers, e.Location) is not { } hit)
                return;

            if (e.Button == MouseButtons.Right) {
                DockStrip.ShowTabMenu (this, hit, e.Location);
                return;
            }

            if (DockStrip.HitClose (_headers, e.Location) is { } closing) {
                if (DockStrip.DockOf (this) is { } dock)
                    dock.CloseWindow (closing);
                else
                    closing.Close ();

                PerformLayout ();
                Invalidate ();
                return;
            }

            SelectWindowInternal (hit);
        }
    }

    public partial class ToolTabStrip
    {
        private DockWindowBase? _selectedWindow;
        private int pending_index = -1;
        private readonly DockStrip.HeaderState _headers = new ();

        /// <summary>The painted header and close-box rectangles, in logical units. Test seam.</summary>
        internal DockStrip.HeaderState Headers => _headers;

        /// <summary>Gets or sets the index of the selected tab.</summary>
        /// <remarks>Real as of W6 mechanisms (#176); see <see cref="DocumentTabStrip.SelectedIndex"/>.</remarks>
        public int SelectedIndex {
            get => _selectedWindow is { } w ? DockStrip.Windows (this).IndexOf (w) : -1;
            set {
                var ws = DockStrip.Windows (this);

                if (value >= 0 && value < ws.Count)
                    SelectWindowInternal (ws[value]);
                else
                    pending_index = value;
            }
        }

        /// <summary>Gets or sets the dock window whose tab is active.</summary>
        public DockWindowBase? ActiveWindow {
            get => _selectedWindow;
            set {
                if (value is not null)
                    SelectWindowInternal (value);
            }
        }

        /// <summary>Gets or sets whether a caption band naming the active window runs across the top.</summary>
        /// <remarks>Real as of W6 mechanisms (#176): the band is drawn and the content sits below it.</remarks>
        public bool CaptionVisible {
            get => caption_visible;
            set => SetShape (ref caption_visible, value);
        }

        /// <summary>Gets or sets whether the tab headers are shown when the strip holds more than one window.</summary>
        /// <remarks>Real as of W6 mechanisms (#176): without them the active window takes the space.</remarks>
        public bool TabStripVisible {
            get => tab_strip_visible;
            set => SetShape (ref tab_strip_visible, value);
        }

        /// <summary>Gets or sets which edge the tab headers sit on.</summary>
        /// <remarks>Real as of W6 mechanisms (#176) for <see cref="TabStripAlignment.Top"/> and
        /// <see cref="TabStripAlignment.Bottom"/>. Left and Right place the headers at the bottom:
        /// vertical tab headers are not drawn.</remarks>
        public TabStripAlignment TabStripAlignment {
            get => tab_strip_alignment;
            set {
                if (tab_strip_alignment == value)
                    return;

                tab_strip_alignment = value;
                PerformLayout ();
                Invalidate ();
            }
        }

        private bool caption_visible = true;
        private bool tab_strip_visible = true;
        private TabStripAlignment tab_strip_alignment = TabStripAlignment.Top;

        private void SetShape (ref bool field, bool value)
        {
            if (field == value)
                return;

            field = value;
            PerformLayout ();
            Invalidate ();
        }

        /// <inheritdoc/>
        protected override void OnLayout (LayoutEventArgs e)
        {
            base.OnLayout (e);

            if (pending_index >= 0 && pending_index < DockStrip.Windows (this).Count) {
                _selectedWindow = DockStrip.Windows (this)[pending_index];
                pending_index = -1;
            }

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
        // implementation behind a header click, SelectedIndex/ActiveWindow and RadDock.ActivateWindow.
        internal void SelectWindowInternal (DockWindowBase win)
        {
            if (win == _selectedWindow || !Controls.Contains (win) || win.DockState == DockState.Hidden)
                return;

            var previous = _selectedWindow;
            _selectedWindow = win;
            pending_index = -1;
            DockStrip.LayoutTabs (this, _selectedWindow);
            DockStrip.RaiseTabActivation (this, previous, win);
            Invalidate ();
        }

        /// <inheritdoc/>
        protected override void OnMouseDown (MouseEventArgs e)
        {
            base.OnMouseDown (e);

            if (DockStrip.HitTest (_headers, e.Location) is not { } hit)
                return;

            if (e.Button == MouseButtons.Right) {
                DockStrip.ShowTabMenu (this, hit, e.Location);
                return;
            }

            SelectWindowInternal (hit);
        }
    }
}
