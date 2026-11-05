using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using SkiaSharp;

namespace Majorsilence.Forms.Telerik
{
    // Auto-hidden dock windows. Measured on Telerik's RadDock (UI for WinForms 2026.2.520), probe wfdockfloat:
    //   - AutoHideWindow(w) auto-hides every window of w's strip; the strip stays in the dock, hidden. Each window
    //     gets a tab on the side its strip was docked to (Left for one never docked), and an empty AutoHideSize
    //     becomes the strip's AbsoluteSize. Events: DockStateChanging (AutoHide) and DockStateChanged for each.
    //   - The tabs sit in a band along that side, inside the dock's padding of 5: 26 wide on the left and right,
    //     25 high at the top and bottom. The left and right bands run between the top and bottom ones; the top and
    //     bottom bands between the left and right ones, so the corners stay empty. The rest of the dock lays its
    //     panels out as before. A tab is inset 1 into its band, 24 across, and as long as its text in the theme's
    //     Segoe UI 8.25 plus 16; the tabs follow one another with no gap, the one auto-hidden last first.
    //   - ShowAutoHidePopup(w), or a click on its tab, slides w out in a borderless popup owned by the dock's form:
    //     on the left from 2 inside the band's right edge, on the right up to the band's left edge, at the top
    //     below the band, at the bottom above it; AutoHideSize long and across the rest of the dock. Inside, a
    //     tool strip with a caption (24 high, from 2,2) ending in close, pin and menu buttons; the window inset 3,
    //     26, 2 and 2, with a 3 wide splitter more on the side facing the dock. Events: AutoHideWindowDisplaying
    //     (cancellable), ActiveWindowChanging and ActiveWindowChanged making w active, AutoHideWindowDisplayed.
    //     CloseAutoHidePopup raises AutoHideWindowHiding (cancellable), hands activation back to the document and raises
    //     AutoHideWindowHidden.
    //   - DockState = Docked (the pin) puts the window back in its strip; Close hides it as any close does.
    public partial class RadDock
    {
        internal const int AutoHideBandWidth = 26;
        internal const int AutoHideBandHeight = 25;
        internal const int AutoHideTabThickness = 24;
        internal const int AutoHideTabPadding = 16;
        internal const int AutoHideTabFontSize = 11;

        // GDI+, which Telerik measures the tab text with, runs about 3.5% wider than Skia on Segoe UI 8.25 (11 px): fitted on
        // the three captions measured (t1, Search Window and Filter for : Customers: 26, 94 and 120 long tabs).
        internal const double AutoHideTabTextScale = 1.035;

        /// <summary>Raised before an auto-hidden window slides out; cancel it to keep it in.</summary>
        public event EventHandler<AutoHideWindowDisplayingEventArgs>? AutoHideWindowDisplaying;

        /// <summary>Raised after an auto-hidden window has slid out.</summary>
        public event EventHandler<DockWindowEventArgs>? AutoHideWindowDisplayed;

        /// <summary>Raised before an auto-hidden window slides back in; cancel it to keep it out.</summary>
        public event EventHandler<DockWindowCancelEventArgs>? AutoHideWindowHiding;

        /// <summary>Raised after an auto-hidden window has slid back in.</summary>
        public event EventHandler<DockWindowEventArgs>? AutoHideWindowHidden;

        /// <summary>Gets or sets how an auto-hidden window slides out. Stored for Telerik compatibility; the popup shows at once.</summary>
        public AutoHideAnimateMode AutoHideAnimation { get; set; } = AutoHideAnimateMode.AnimateShow;

        /// <summary>Gets or sets how long the slide takes, in milliseconds. Stored for Telerik compatibility.</summary>
        public int AutoHideAnimationDuration { get; set; } = 200;

        private readonly Dictionary<AutoHidePosition, List<DockWindowBase>> auto_hide_tabs = new () {
            [AutoHidePosition.Left] = new (), [AutoHidePosition.Top] = new (), [AutoHidePosition.Right] = new (), [AutoHidePosition.Bottom] = new (),
        };

        private AutoHidePopup? auto_hide_popup;

        /// <summary>Gets the popup auto-hidden windows slide out in.</summary>
        public AutoHidePopup AutoHidePopup => auto_hide_popup ??= new AutoHidePopup (this);

        /// <summary>Auto-hides the specified window, together with the other windows of its strip.</summary>
        public void AutoHideWindow (DockWindowBase window)
        {
            Guard.ThrowIfNull (window);

            var windows = window.Parent is ToolTabStrip strip and not AutoHideTabStrip && IsInDock (strip)
                ? strip.Controls.OfType<DockWindowBase> ().Where (w => w.DockState != DockState.Hidden).ToList ()
                : new List<DockWindowBase> { window };

            foreach (var w in windows)
                AutoHide (w, AutoHidePosition.Auto);
        }

        /// <summary>Auto-hides the specified windows on the given side.</summary>
        public void AutoHideWindows (IEnumerable<DockWindowBase> windows, AutoHidePosition position)
        {
            Guard.ThrowIfNull (windows);

            foreach (var window in windows.Reverse ().ToList ())
                AutoHide (window, position);
        }

        // The side a window auto-hides to: where its strip sits beside the dock's main document container.
        private AutoHidePosition SideOf (DockWindowBase window)
        {
            var strip = window.Parent ?? window.home;

            if (strip is null || !IsInDock (strip))
                return AutoHidePosition.Left;

            var main = (Control?) MainDocumentContainer ?? Controls.OfType<DocumentContainer> ().FirstOrDefault ();
            var r = BoundsInDock (strip);

            if (main is null || !main.Visible) {
                var client = DockStrip.LogicalClient (this);
                return r.Right <= client.Width / 2 ? AutoHidePosition.Left : r.Left >= client.Width / 2 ? AutoHidePosition.Right
                    : r.Bottom <= client.Height / 2 ? AutoHidePosition.Top : AutoHidePosition.Bottom;
            }

            var m = BoundsInDock (main);

            if (r.Right <= m.Left)
                return AutoHidePosition.Left;
            if (r.Left >= m.Right)
                return AutoHidePosition.Right;
            if (r.Bottom <= m.Top)
                return AutoHidePosition.Top;

            return r.Top >= m.Bottom ? AutoHidePosition.Bottom : AutoHidePosition.Left;
        }

        private Rectangle BoundsInDock (Control control)
        {
            var r = control.Bounds;

            for (var p = control.Parent; p is not null && !ReferenceEquals (p, this); p = p.Parent)
                r.Offset (p.Bounds.Location);

            return r;
        }

        internal bool AutoHide (DockWindowBase window, AutoHidePosition position)
        {
            if ((window.AllowedDockState & AllowedDockState.AutoHide) == 0 || window is DocumentWindow)
                return false;

            if (window.DockState == DockState.AutoHide && window.Parent is AutoHideTabStrip)
                return false;

            if (!RaiseDockStateChanging (window, DockState.AutoHide))
                return false;

            var added = !Has (window);
            var side = position == AutoHidePosition.Auto ? SideOf (window) : position;
            var strip = window.Parent as ToolTabStrip;

            if (window is ToolWindow tool && tool.AutoHideSize.IsEmpty) {
                var size = strip?.SizeInfo.AbsoluteSize ?? new SizeF (200, 200);
                tool.AutoHideSize = new Size ((int) Math.Round (size.Width), (int) Math.Round (size.Height));
            }

            if (window.floating_parent is not null && ReferenceEquals (window.Parent, window.floating_parent.Strip))
                Detach (window);
            else if (window.Parent is not null)
                Detach (window);

            window.manager = this;
            window.auto_hide_side = side;
            window.SetStateField (DockState.AutoHide);
            window.Visible = false;

            AutoHidePopup.ToolStrip.Controls.Add (window);
            auto_hide_tabs[side].Insert (0, window);

            if (!detached_windows.Contains (window))
                detached_windows.Add (window);

            PerformLayout ();
            Invalidate ();

            DockStateChanged?.Invoke (this, new DockWindowEventArgs { DockWindow = window });

            if (added)
                DockWindowAdded?.Invoke (this, new DockWindowEventArgs { DockWindow = window });

            return true;
        }

        // Takes a window off its auto-hide tab, closing the popup if it was showing it.
        internal void RemoveFromAutoHide (DockWindowBase window)
        {
            if (auto_hide_popup is { } popup && ReferenceEquals (popup.ActiveWindow, window))
                ClosePopup (raise: false);

            foreach (var tabs in auto_hide_tabs.Values)
                tabs.Remove (window);

            window.Parent?.Controls.Remove (window);
            PerformLayout ();
            Invalidate ();
        }

        /// <summary>Slides the specified auto-hidden window out.</summary>
        /// <remarks>Telerik reports it as <see cref="AutoHideDisplayReason.TabItemClicked"/> (measured), as a click on the tab is.</remarks>
        public void ShowAutoHidePopup (ToolWindow window) => ShowPopup (window, AutoHideDisplayReason.TabItemClicked);

        internal void ShowPopup (DockWindowBase window, AutoHideDisplayReason reason)
        {
            if (window.DockState != DockState.AutoHide || window.Parent is not AutoHideTabStrip)
                return;

            var popup = AutoHidePopup;

            if (popup.Visible && ReferenceEquals (popup.ActiveWindow, window))
                return;

            var displaying = new AutoHideWindowDisplayingEventArgs (window, reason);
            AutoHideWindowDisplaying?.Invoke (this, displaying);

            if (displaying.Cancel)
                return;

            if (popup.Visible)
                ClosePopup (raise: true);

            popup.Present (window, PopupBounds (window));
            SetActiveWindow (window);
            AutoHideWindowDisplayed?.Invoke (this, new DockWindowEventArgs { DockWindow = window });
        }

        /// <summary>Slides the auto-hidden window that is out back in.</summary>
        public void CloseAutoHidePopup () => ClosePopup (raise: true);

        private void ClosePopup (bool raise)
        {
            if (auto_hide_popup is not { Visible: true } popup || popup.ActiveWindow is not { } window)
                return;

            if (raise) {
                var hiding = new DockWindowCancelEventArgs (null, window);
                AutoHideWindowHiding?.Invoke (this, hiding);

                if (hiding.Cancel)
                    return;
            }

            popup.Withdraw ();

            if (!raise)
                return;

            ActivateDocument ();
            AutoHideWindowHidden?.Invoke (this, new DockWindowEventArgs { DockWindow = window });
        }

        private void DisposeAutoHidePopup ()
        {
            auto_hide_popup?.DisposeKept ();
            auto_hide_popup = null;
        }

        // The auto-hide bands and the room they leave for the dock's panels, in the dock's logical client space.
        internal (Dictionary<AutoHidePosition, Rectangle> bands, Rectangle content) AutoHideLayout ()
        {
            var inner = DockStrip.LogicalClient (this);
            inner.Inflate (-SplitLayout.DockPadding, -SplitLayout.DockPadding);

            bool Has (AutoHidePosition side) => auto_hide_tabs[side].Count > 0;

            var left = Has (AutoHidePosition.Left) ? AutoHideBandWidth : 0;
            var right = Has (AutoHidePosition.Right) ? AutoHideBandWidth : 0;
            var top = Has (AutoHidePosition.Top) ? AutoHideBandHeight : 0;
            var bottom = Has (AutoHidePosition.Bottom) ? AutoHideBandHeight : 0;

            var content = Rectangle.FromLTRB (inner.Left + left, inner.Top + top, Math.Max (inner.Left + left, inner.Right - right), Math.Max (inner.Top + top, inner.Bottom - bottom));
            var bands = new Dictionary<AutoHidePosition, Rectangle> ();

            if (left > 0)
                bands[AutoHidePosition.Left] = new Rectangle (inner.Left, content.Top, left, content.Height);
            if (right > 0)
                bands[AutoHidePosition.Right] = new Rectangle (content.Right, content.Top, right, content.Height);
            if (top > 0)
                bands[AutoHidePosition.Top] = new Rectangle (content.Left, inner.Top, content.Width, top);
            if (bottom > 0)
                bands[AutoHidePosition.Bottom] = new Rectangle (content.Left, content.Bottom, content.Width, bottom);

            // The vertical bands run between the horizontal ones, which run between the vertical ones.
            return (bands, content);
        }

        // Each auto-hide tab's rectangle, in the dock's logical client space.
        internal List<(DockWindowBase window, Rectangle rect, AutoHidePosition side)> AutoHideTabs ()
        {
            var result = new List<(DockWindowBase, Rectangle, AutoHidePosition)> ();
            var (bands, _) = AutoHideLayout ();
            var font = TypefaceCache.Get ("Segoe UI") ?? Theme.UIFont;

            foreach (var (side, band) in bands) {
                var vertical = side is AutoHidePosition.Left or AutoHidePosition.Right;
                var offset = 1;

                foreach (var window in auto_hide_tabs[side]) {
                    var text = (int) Math.Round (TextMeasurer.MeasureText (DockStrip.Caption (window), font, AutoHideTabFontSize).Width * AutoHideTabTextScale);
                    var length = text + AutoHideTabPadding;
                    var rect = vertical
                        ? new Rectangle (band.Left + 1, band.Top + offset, AutoHideTabThickness, length)
                        : new Rectangle (band.Left + offset, band.Top + 1, length, AutoHideTabThickness);

                    result.Add ((window, rect, side));
                    offset += length;
                }
            }

            return result;
        }

        // Where an auto-hidden window slides out, on screen.
        private Rectangle PopupBounds (DockWindowBase window)
        {
            var (bands, content) = AutoHideLayout ();
            var side = window.auto_hide_side;
            var size = window is ToolWindow tool && !tool.AutoHideSize.IsEmpty ? tool.AutoHideSize : new Size (200, 200);
            var band = bands.TryGetValue (side, out var b) ? b : content;

            var r = side switch {
                AutoHidePosition.Right => new Rectangle (band.Left - size.Width, content.Top, size.Width, content.Height),
                AutoHidePosition.Top => new Rectangle (content.Left, band.Bottom, content.Width, size.Height),
                AutoHidePosition.Bottom => new Rectangle (content.Left, band.Top - size.Height, content.Width, size.Height),
                _ => new Rectangle (band.Right - 2, content.Top, size.Width, content.Height),
            };

            var screen = PointToScreen (r.Location);
            return new Rectangle (screen, r.Size);
        }

        internal void PaintAutoHideTabs (PaintEventArgs e)
        {
            var tabs = AutoHideTabs ();

            if (tabs.Count == 0)
                return;

            using var device = e.DeviceSpace ();

            var (bands, _) = AutoHideLayout ();
            var font = TypefaceCache.Get ("Segoe UI") ?? Theme.UIFont;
            var font_size = LogicalToDeviceUnits (AutoHideTabFontSize);
            var fore = Enabled ? GetEffectiveForegroundColor () : Theme.ForegroundDisabledColor;

            Rectangle Device (Rectangle r) => new (LogicalToDeviceUnits (r.Left), LogicalToDeviceUnits (r.Top), LogicalToDeviceUnits (r.Width), LogicalToDeviceUnits (r.Height));

            using var band_fill = new SKPaint { Color = new SKColor (0xDD, 0xE2, 0xEC) };
            using var tab_fill = new SKPaint { Color = new SKColor (0xF5, 0xF8, 0xFC) };
            using var border = new SKPaint { Color = new SKColor (0x6F, 0x95, 0xC9), IsStroke = true };

            foreach (var band in bands.Values) {
                var d = Device (band);
                e.Canvas.DrawRect (d.Left, d.Top, d.Width, d.Height, band_fill);
            }

            foreach (var (window, rect, side) in tabs) {
                var d = Device (rect);
                e.Canvas.DrawRect (d.Left, d.Top, d.Width, d.Height, tab_fill);
                e.Canvas.DrawRect (d.Left + 0.5f, d.Top + 0.5f, d.Width - 1, d.Height - 1, border);

                var text = DockStrip.Caption (window);

                if (side is AutoHidePosition.Left or AutoHidePosition.Right) {
                    // Read top to bottom, as Telerik draws a side tab's text.
                    e.Canvas.Save ();
                    e.Canvas.Translate (d.Right, d.Top);
                    e.Canvas.RotateDegrees (90);
                    e.Canvas.DrawText (text, font, font_size, new Rectangle (0, 0, d.Height, d.Width), fore, ContentAlignment.MiddleCenter, maxLines: 1);
                    e.Canvas.Restore ();
                } else {
                    e.Canvas.DrawText (text, font, font_size, d, fore, ContentAlignment.MiddleCenter, maxLines: 1);
                }
            }
        }

        // A click on an auto-hide tab slides its window out; a click anywhere else in the dock slides it back.
        internal bool HandleAutoHideMouseDown (Point location)
        {
            foreach (var (window, rect, _) in AutoHideTabs ()) {
                if (!rect.Contains (location))
                    continue;

                if (auto_hide_popup is { Visible: true } popup && ReferenceEquals (popup.ActiveWindow, window))
                    CloseAutoHidePopup ();
                else
                    ShowPopup (window, AutoHideDisplayReason.TabItemClicked);

                return true;
            }

            CloseAutoHidePopup ();
            return false;
        }

        // The auto-hidden windows of a side, in tab order. Test seam.
        internal IReadOnlyList<DockWindowBase> AutoHideTabsOn (AutoHidePosition side) => auto_hide_tabs[side];

        /// <inheritdoc/>
        protected override void OnMouseDown (MouseEventArgs e)
        {
            base.OnMouseDown (e);

            if (e.Button == MouseButtons.Left)
                HandleAutoHideMouseDown (e.Location);
        }
    }

    /// <summary>The borderless popup a <see cref="RadDock"/> slides its auto-hidden windows out in, as Telerik's.</summary>
    public class AutoHidePopup : Form
    {
        private bool disposing_kept;

        /// <summary>Gets the root element of the popup (stub).</summary>
        public RadElement RootElement { get; } = new RadElement ();

        internal AutoHidePopup (RadDock dock)
        {
            owner_dock = dock;
            FormBorderStyle = FormBorderStyle.None;
            ShowInTaskbar = false;
            StartPosition = FormStartPosition.Manual;
            ToolStrip = new AutoHideTabStrip (dock) { Dock = DockStyle.Fill };
            Controls.Add (ToolStrip);
            FormClosing += (_, e) => {
                if (!disposing_kept && e.CloseReason == CloseReason.UserClosing) {
                    e.Cancel = true;
                    Hide ();
                }
            };
        }

        private readonly RadDock owner_dock;

        /// <summary>Gets the tool strip holding the auto-hidden windows.</summary>
        public AutoHideTabStrip ToolStrip { get; }

        /// <summary>Gets the window that is out, if any.</summary>
        public DockWindowBase? ActiveWindow { get; private set; }

        /// <summary>Gets the side the popup slides out from.</summary>
        public DockStyle AutoHideDock => ToolStrip.AutoHideDock;

        /// <summary>Gets the width of the splitter on the popup's edge facing the dock.</summary>
        public int SplitterWidth => ToolStrip.SplitterWidth;

        internal void Present (DockWindowBase window, Rectangle screen)
        {
            if (Owner is null && owner_dock.FindForm () is { } owner)
                Owner = owner;

            ActiveWindow = window;
            ToolStrip.AutoHideDock = window.auto_hide_side switch {
                AutoHidePosition.Right => DockStyle.Right,
                AutoHidePosition.Top => DockStyle.Top,
                AutoHidePosition.Bottom => DockStyle.Bottom,
                _ => DockStyle.Left,
            };

            foreach (var w in ToolStrip.Controls.OfType<DockWindowBase> ())
                w.Visible = ReferenceEquals (w, window);

            Location = screen.Location;
            Size = screen.Size;
            ToolStrip.Shown = window;
            ToolStrip.PerformLayout ();
            ToolStrip.Invalidate ();

            if (!Visible)
                Show ();
        }

        internal void Withdraw ()
        {
            if (ActiveWindow is { } window)
                window.Visible = false;

            ActiveWindow = null;
            ToolStrip.Shown = null;

            if (Visible)
                Hide ();
        }

        internal void DisposeKept ()
        {
            disposing_kept = true;

            if (Visible)
                Close ();

            Dispose ();
        }
    }

    /// <summary>The tool strip of an <see cref="AutoHidePopup"/>: a caption with close and pin buttons over the window that is out.</summary>
    public class AutoHideTabStrip : ToolTabStrip
    {
        /// <summary>Initializes a strip that belongs to no dock.</summary>
        public AutoHideTabStrip () { }

        /// <summary>Initializes a strip for the specified dock.</summary>
        public AutoHideTabStrip (RadDock dock) => DockManager = dock;

        internal RadDock? DockManager { get; }

        /// <summary>Gets or sets the side the strip's popup slides out from.</summary>
        public DockStyle AutoHideDock {
            get => auto_hide_dock;
            set {
                auto_hide_dock = value;
                PerformLayout ();
            }
        }

        private DockStyle auto_hide_dock = DockStyle.Left;

        /// <summary>Gets or sets the width of the splitter on the edge facing the dock: 3, as Telerik's.</summary>
        public new int SplitterWidth { get; set; } = 3;

        /// <summary>Gets or sets whether the splitter can be dragged. Stored for Telerik compatibility.</summary>
        public bool FixedSplitter { get; set; }

        // The window the popup shows.
        internal DockWindowBase? Shown { get; set; }
    }

    /// <summary>Provides data for <see cref="RadDock.AutoHideWindowDisplaying"/>.</summary>
    public class AutoHideWindowDisplayingEventArgs : DockWindowCancelEventArgs
    {
        /// <summary>Initializes the event data.</summary>
        public AutoHideWindowDisplayingEventArgs (DockWindowBase window, AutoHideDisplayReason reason) : base (null, window) => DisplayReason = reason;

        /// <summary>Gets why the window is sliding out.</summary>
        public AutoHideDisplayReason DisplayReason { get; }
    }

    /// <summary>Why an auto-hidden window slides out.</summary>
    public enum AutoHideDisplayReason
    {
        /// <summary>The pointer rests on its tab.</summary>
        TabItemHovered,
        /// <summary>Its tab was clicked.</summary>
        TabItemClicked,
        /// <summary>It was activated in code.</summary>
        Activate,
    }

    /// <summary>The side of a <see cref="RadDock"/> an auto-hidden window's tab sits on.</summary>
    public enum AutoHidePosition
    {
        /// <summary>The left side.</summary>
        Left,
        /// <summary>The top.</summary>
        Top,
        /// <summary>The right side.</summary>
        Right,
        /// <summary>The bottom.</summary>
        Bottom,
        /// <summary>The side the window's strip is docked to.</summary>
        Auto,
    }

    /// <summary>How an auto-hidden window slides. Compat for Telerik's AutoHideAnimateMode.</summary>
    public enum AutoHideAnimateMode
    {
        /// <summary>No animation.</summary>
        None,
        /// <summary>Animate showing.</summary>
        AnimateShow,
        /// <summary>Animate hiding.</summary>
        AnimateHide,
        /// <summary>Animate both.</summary>
        Both,
    }
}
