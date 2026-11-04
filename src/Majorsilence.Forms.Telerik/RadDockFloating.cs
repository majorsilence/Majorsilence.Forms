using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Linq;

namespace Majorsilence.Forms.Telerik
{
    // Floating dock windows, and the dock's window-state machine they share with auto-hide (RadDockAutoHide.cs).
    // Measured on Telerik's RadDock (UI for WinForms 2026.2.520), probe wfdockfloat:
    //   - FloatWindow(w) moves w into a FloatingWindow: a sizable form owned by the dock's form, not in the taskbar,
    //     with a maximize box and no minimize box, captioned with the window's text. Its bounds are the window's
    //     DefaultFloatingSize (300 x 300 unless set), centred on the dock's screen rectangle; FloatWindow(w, rect)
    //     puts it at rect instead. Inside, a split container holds a tool tab strip with no caption, the window
    //     inset 1 at the sides and 2 at the top and bottom.
    //   - The strip w left stays in the dock, hidden, so the dock's other panels share its room.
    //   - Events: DockStateChanging (cancellable), FloatingWindowCreated, DockStateChanged, then DockWindowAdded for
    //     a window the dock did not have, then ActiveWindowChanging and ActiveWindowChanged making w active.
    //     DockState = Floating does the same without the activation; a window not allowed to float stays as it is,
    //     and floating a floating window changes nothing.
    //   - Closing a floating window (its Close, or the floating form's) raises DockStateChanging (Hidden),
    //     DockWindowClosing, DockStateChanged and DockWindowClosed; the window leaves the floating form, which hides
    //     (it is kept, not disposed), and the dock's document becomes active. Show brings it back into that form.
    //   - DockState = Docked puts a floating window back in the strip it came from and makes it active; DockWindow
    //     docks it where it is told. The floating form hides and is kept: floating the window again reuses it where
    //     it was, without a FloatingWindowCreated.
    //   - PreviousDockState only changes when a window is hidden (it is then the state it was hidden from).
    public partial class RadDock
    {
        /// <summary>Raised before a dock window changes its <see cref="DockWindowBase.DockState"/>; cancel it to keep the state.</summary>
        public event EventHandler<DockStateChangingEventArgs>? DockStateChanging;

        /// <summary>Raised after a dock window has changed its <see cref="DockWindowBase.DockState"/>.</summary>
        public event EventHandler<DockWindowEventArgs>? DockStateChanged;

        /// <summary>Raised when a window the dock did not have joins it, by floating, auto-hiding or docking.</summary>
        public event EventHandler<DockWindowEventArgs>? DockWindowAdded;

        /// <summary>Raised when a window leaves the dock: closed through <see cref="CloseWindow"/>, it is no longer one of its <see cref="DockWindows"/>.</summary>
        public event EventHandler<DockWindowEventArgs>? DockWindowRemoved;

        /// <summary>Raised before a dock window closes; cancel it to keep the window open.</summary>
        public event EventHandler<DockWindowCancelEventArgs>? DockWindowClosing;

        /// <summary>Raised after a dock window has closed.</summary>
        public event EventHandler<DockWindowEventArgs>? DockWindowClosed;

        /// <summary>Raised before the dock's <see cref="ActiveWindow"/> changes.</summary>
        public event EventHandler<DockWindowCancelEventArgs>? ActiveWindowChanging;

        /// <summary>Raised after the dock's <see cref="ActiveWindow"/> has changed.</summary>
        public event EventHandler<DockWindowEventArgs>? ActiveWindowChanged;

        /// <summary>Raised when the dock creates a <see cref="FloatingWindow"/> to float a window in.</summary>
        public event EventHandler<FloatingWindowEventArgs>? FloatingWindowCreated;

        private readonly List<FloatingWindow> floating_windows = new ();

        // Windows the dock holds outside its own control tree: floating, auto-hidden, or hidden from either.
        private readonly List<DockWindowBase> detached_windows = new ();

        /// <summary>Gets the floating windows the dock has created, shown or hidden.</summary>
        public FloatingWindowCollection FloatingWindows => new (floating_windows);

        /// <summary>Gets the floating windows that are on screen.</summary>
        public System.Collections.ObjectModel.ReadOnlyCollection<FloatingWindow> ActiveFloatingWindows
            => floating_windows.Where (f => f.Visible && !f.IsDisposed).ToList ().AsReadOnly ();

        /// <summary>Floats the specified dock window in a <see cref="FloatingWindow"/> of its
        /// <see cref="DockWindowBase.DefaultFloatingSize"/>, centred on the dock, and makes it active.</summary>
        public void FloatWindow (DockWindowBase window) => Float (window, null, activate: true);

        /// <summary>Floats the specified dock window in a <see cref="FloatingWindow"/> at the given screen bounds, and makes it active.</summary>
        public void FloatWindow (DockWindowBase window, Rectangle bounds) => Float (window, bounds, activate: true);

        /// <summary>Floats the specified windows together, as tabs of one <see cref="FloatingWindow"/> at the given screen bounds.</summary>
        public void FloatWindows (IEnumerable<DockWindowBase> windows, Rectangle bounds)
        {
            Guard.ThrowIfNull (windows);

            FloatingWindow? host = null;
            DockWindowBase? first = null;

            foreach (var window in windows.ToList ()) {
                if (!Float (window, bounds, activate: false, host))
                    continue;

                host ??= window.floating_parent;
                first ??= window;
            }

            if (first is not null) {
                first.floating_parent?.Strip.SelectWindowInternal (first, raiseSelectedTabChanged: false);
                SetActiveWindow (first);
            }
        }

        /// <summary>Puts the specified window in the given state, as setting its <see cref="DockWindowBase.DockState"/> does.</summary>
        public void SetWindowState (DockWindowBase window, DockState state)
        {
            Guard.ThrowIfNull (window);

            switch (state) {
                case DockState.Floating:
                    Float (window, null, activate: false);
                    break;
                case DockState.AutoHide:
                    // A window shown again goes back to the side it was hidden from.
                    AutoHide (window, window.DockState == DockState.Hidden && window.PreviousDockState == DockState.AutoHide ? window.auto_hide_side : AutoHidePosition.Auto);
                    break;
                case DockState.Hidden:
                    window.Hide ();
                    break;
                default:
                    DockBack (window, state);
                    break;
            }
        }

        // Whether the dock already has the window: in its tree, tracked as a tool window, or held outside the tree.
        private bool Has (DockWindowBase window)
            => detached_windows.Contains (window) || window is ToolWindow tool && _toolWindows.Contains (tool)
               || AllWindowsInTree ().Contains (window);

        private bool Float (DockWindowBase window, Rectangle? bounds, bool activate, FloatingWindow? into = null)
        {
            Guard.ThrowIfNull (window);

            if ((window.AllowedDockState & AllowedDockState.Floating) == 0)
                return false;

            if (window.DockState == DockState.Floating && window.Parent is not null && window.floating_parent is { IsDisposed: false })
                return false;

            if (!RaiseDockStateChanging (window, DockState.Floating))
                return false;

            var added = !Has (window);

            Detach (window);

            var host = into ?? (window.floating_parent is { IsDisposed: false } kept ? kept : null);

            if (host is null) {
                host = new FloatingWindow (this);
                var size = bounds?.Size ?? window.DefaultFloatingSize;
                host.Size = size;
                host.Location = bounds?.Location ?? CentredOnDock (host, size);
                floating_windows.Add (host);
                FloatingWindowCreated?.Invoke (this, new FloatingWindowEventArgs (host));
            } else if (bounds is { } given && into is null) {
                host.Location = given.Location;
                host.Size = given.Size;
            }

            if (host.Strip.Controls.Count == 0)
                host.Text = DockStrip.Caption (window);

            window.manager = this;
            window.floating_parent = host;
            window.SetStateField (DockState.Floating);
            window.Visible = true;
            host.Strip.Controls.Add (window);

            if (!detached_windows.Contains (window))
                detached_windows.Add (window);

            host.Strip.PerformLayout ();

            DockStateChanged?.Invoke (this, new DockWindowEventArgs { DockWindow = window });

            if (added)
                DockWindowAdded?.Invoke (this, new DockWindowEventArgs { DockWindow = window });

            if (!host.Visible)
                host.Show ();

            if (activate)
                SetActiveWindow (window);

            return true;
        }

        // The floating form's location centred on the dock's screen rectangle, as Telerik's FloatWindow places it.
        private Point CentredOnDock (FloatingWindow host, Size size)
        {
            var screen = RectangleToScreen (new Rectangle (0, 0, Width, Height));
            var scale = host.DesktopScaling;
            var width = (int) Math.Round (size.Width * scale);
            var height = (int) Math.Round (size.Height * scale);

            return new Point (screen.X + (screen.Width - width) / 2, screen.Y + (screen.Height - height) / 2);
        }

        // Takes a window out of wherever it is -- a strip of the dock, a floating form or an auto-hide strip --
        // remembering the dock strip it leaves so it can go back there.
        internal void Detach (DockWindowBase window)
        {
            var parent = window.Parent;

            if (parent is null)
                return;

            if (window.floating_parent is { } host && ReferenceEquals (parent, host.Strip)) {
                parent.Controls.Remove (window);
                HideIfEmpty (host);
                return;
            }

            if (parent is AutoHideTabStrip) {
                RemoveFromAutoHide (window);
                return;
            }

            window.home = parent;
            window.home_index = parent.Controls.GetChildIndex (window);
            parent.Controls.Remove (window);

            if (parent is ToolTabStrip strip)
                UpdateStripVisibility (strip);

            PerformLayout ();
        }

        private static void HideIfEmpty (FloatingWindow host)
        {
            if (host.Strip.Controls.OfType<DockWindowBase> ().Any (w => w.DockState != DockState.Hidden)) {
                host.Strip.PerformLayout ();
                return;
            }

            host.HideKept ();
        }

        // A tool strip with no window to show takes no room in the dock, as Telerik's hides itself.
        internal void UpdateStripVisibility (ToolTabStrip strip)
        {
            if (strip.Parent is null || strip is AutoHideTabStrip || strip.ContentInset != Padding.Empty)
                return;

            strip.Visible = strip.Controls.OfType<DockWindowBase> ().Any (w => w.DockState != DockState.Hidden);
        }

        // Back from floating or auto-hide: into the strip the window came from, or, for one that never was in the
        // dock, a new strip on the dock's left, as Telerik's DockWindow places a tool window.
        private void DockBack (DockWindowBase window, DockState state)
        {
            var leaving = window.DockState is DockState.Floating or DockState.AutoHide || window.Parent is null && window.manager is not null;

            if (!leaving) {
                window.SetStateDirect (state);
                return;
            }

            if (!RaiseDockStateChanging (window, state))
                return;

            Detach (window);
            detached_windows.Remove (window);

            var home = window.home is { IsDisposed: false } h && IsInDock (h) ? h : null;

            window.SetStateField (state);
            window.Visible = true;

            if (home is not null) {
                home.Controls.Add (window);
                home.Controls.SetChildIndex (window, Math.Min (window.home_index, home.Controls.Count - 1));

                if (home is ToolTabStrip strip)
                    UpdateStripVisibility (strip);

                home.PerformLayout ();
            } else if (window is DocumentWindow || state == DockState.TabbedDocument) {
                GetDefaultDocumentTabStrip (createIfMissing: true).Controls.Add (window);
            } else {
                DockToolStrip (window, DockPosition.Left);
            }

            PerformLayout ();

            DockStateChanged?.Invoke (this, new DockWindowEventArgs { DockWindow = window });
            SetActiveWindow (window);
            ActivateWindow (window);
        }

        private bool IsInDock (Control control)
        {
            for (var c = control; c is not null; c = c.Parent)
                if (ReferenceEquals (c, this))
                    return true;

            return false;
        }

        internal bool RaiseDockStateChanging (DockWindowBase window, DockState state)
        {
            var args = new DockStateChangingEventArgs (window, state);
            DockStateChanging?.Invoke (this, args);
            return !args.Cancel;
        }

        internal void RaiseDockStateChanged (DockWindowBase window)
            => DockStateChanged?.Invoke (this, new DockWindowEventArgs { DockWindow = window });

        // Closing a window the dock holds: the events Telerik raises around it, with the window leaving a floating
        // form or an auto-hide strip. Returns false when a handler cancelled.
        internal bool CloseCore (DockWindowBase window, Action close)
        {
            if (!RaiseDockStateChanging (window, DockState.Hidden))
                return false;

            var closing = new DockWindowCancelEventArgs (null, window);
            DockWindowClosing?.Invoke (this, closing);

            if (closing.Cancel)
                return false;

            var was_active = ReferenceEquals (active_window, window);
            var detached = window.DockState is DockState.Floating or DockState.AutoHide;

            if (detached)
                Detach (window);

            close ();

            if (window.Parent is ToolTabStrip strip)
                UpdateStripVisibility (strip);

            PerformLayout ();

            DockStateChanged?.Invoke (this, new DockWindowEventArgs { DockWindow = window });
            DockWindowClosed?.Invoke (this, new DockWindowEventArgs { DockWindow = window });

            if (was_active || detached)
                ActivateDocument ();

            return true;
        }

        // Hands activation to the main document strip's selected window, as Telerik does when the active window leaves.
        private void ActivateDocument ()
        {
            var strip = GetDefaultDocumentTabStrip (createIfMissing: false);

            if (strip.Parent is not null && strip.SelectedTab is DockWindowBase document)
                SetActiveWindow (document);
        }

        // Makes a window the dock's active one, raising ActiveWindowChanging and ActiveWindowChanged as Telerik does
        // (also when it already is, as Telerik raises them for a newly floated window).
        internal void SetActiveWindow (DockWindowBase window)
        {
            var args = new DockWindowCancelEventArgs (active_window, window);
            ActiveWindowChanging?.Invoke (this, args);

            if (args.Cancel)
                return;

            active_window = window;
            ActiveWindowChanged?.Invoke (this, new DockWindowEventArgs { DockWindow = window });
        }

        internal void RaiseActiveWindowChanging (DockWindowBase? previous, DockWindowBase current)
            => ActiveWindowChanging?.Invoke (this, new DockWindowCancelEventArgs (previous, current));

        internal void RaiseActiveWindowChanged (DockWindowBase current)
            => ActiveWindowChanged?.Invoke (this, new DockWindowEventArgs { DockWindow = current });

        internal void RaiseDockWindowAdded (DockWindowBase window)
            => DockWindowAdded?.Invoke (this, new DockWindowEventArgs { DockWindow = window });

        internal void RaiseDockWindowRemoved (DockWindowBase window)
            => DockWindowRemoved?.Invoke (this, new DockWindowEventArgs { DockWindow = window });

        // Docks a tool window in a strip of its own at a side of the dock, as Telerik's DockWindow(window, position)
        // does (measured): the strip is 200 long (its AbsoluteSize, 200 x 200); Left goes first and Right last
        // along a dock laid out side by side, Top first and Bottom last along one laid out top to bottom; docking
        // across the dock's orientation first wraps what the dock holds in a split container of the old
        // orientation, which then fills, and turns the dock.
        internal ToolTabStrip DockToolStrip (DockWindowBase window, DockPosition position)
        {
            var strip = new ToolTabStrip ();
            strip.TabStripAlignment = TabStripAlignment.Bottom;
            strip.Controls.Add (window);

            var along = position is DockPosition.Left or DockPosition.Right ? Orientation.Vertical : Orientation.Horizontal;
            var panels = Controls.OfType<Control> ().Where (c => SplitLayout.SizeInfoOf (c) is not null).ToList ();

            if (Orientation != along && panels.Count > 1) {
                var wrapper = new RadSplitContainer { Orientation = Orientation };
                wrapper.SizeInfo.SizeMode = SplitPanelSizeMode.Fill;

                foreach (var panel in panels) {
                    Controls.Remove (panel);
                    wrapper.Controls.Add (panel);
                }

                Controls.Add (wrapper);
                orientation = along;
            } else if (panels.Count <= 1) {
                // A lone panel needs no wrapping: the dock only turns (measured with just the document container).
                orientation = along;
            }

            Controls.Add (strip);

            if (position is DockPosition.Left or DockPosition.Top)
                Controls.SetChildIndex (strip, 0);

            window.home = strip;
            window.home_index = 0;
            PerformLayout ();
            return strip;
        }

        /// <inheritdoc/>
        protected override void Dispose (bool disposing)
        {
            if (disposing) {
                foreach (var host in floating_windows.ToList ())
                    host.DisposeKept ();

                floating_windows.Clear ();
                DisposeAutoHidePopup ();
            }

            base.Dispose (disposing);
        }
    }

    /// <summary>
    /// The form a <see cref="RadDock"/> floats its windows in: sizable, owned by the dock's form, not in the taskbar,
    /// with a maximize box and no minimize box, as Telerik's is.
    /// </summary>
    public class FloatingWindow : Form
    {
        /// <summary>The size a floating window takes when its dock window sets none: 300 x 300.</summary>
        public static readonly Size DefaultFloatingSize = new (300, 300);

        private bool disposing_kept;

        /// <summary>Gets the root element of the floating window (stub).</summary>
        public RadElement RootElement { get; } = new RadElement ();

        /// <summary>Initializes a floating window that belongs to no dock.</summary>
        public FloatingWindow () : this (null) { }

        /// <summary>Initializes a floating window for the specified dock.</summary>
        public FloatingWindow (RadDock? dockManager)
        {
            DockManager = dockManager;
            FormBorderStyle = FormBorderStyle.Sizable;
            ShowInTaskbar = false;
            MaximizeBox = true;
            MinimizeBox = false;
            StartPosition = FormStartPosition.Manual;

            if (dockManager?.FindForm () is { } owner)
                Owner = owner;

            DockContainer = new RadSplitContainer { Dock = DockStyle.Fill };
            Strip = new ToolTabStrip { CaptionVisible = false, TabStripAlignment = TabStripAlignment.Bottom, ContentInset = new Padding (1, 2, 1, 2) };
            DockContainer.Controls.Add (Strip);
            Controls.Add (DockContainer);

            FormClosing += OnClosingKept;
        }

        /// <summary>Gets the dock this floating window belongs to.</summary>
        public RadDock? DockManager { get; }

        /// <summary>Gets the split container holding the floating window's tab strip.</summary>
        public RadSplitContainer DockContainer { get; }

        // The tool strip the floated windows are tabs of.
        internal ToolTabStrip Strip { get; }

        // Closed by the user (its close box, or Close): the windows in it close, and the form hides to be reused, as
        // Telerik's does. Closed with its owner, it closes.
        private void OnClosingKept (object? sender, FormClosingEventArgs e)
        {
            if (disposing_kept || e.CloseReason != CloseReason.UserClosing || DockManager is null)
                return;

            e.Cancel = true;

            foreach (var window in Strip.Controls.OfType<DockWindowBase> ().Where (w => w.DockState != DockState.Hidden).ToList ())
                window.Close ();

            HideKept ();
        }

        internal void HideKept ()
        {
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

    /// <summary>The floating windows of a <see cref="RadDock"/>.</summary>
    public class FloatingWindowCollection : IEnumerable<FloatingWindow>
    {
        private readonly IReadOnlyList<FloatingWindow> windows;

        internal FloatingWindowCollection (IEnumerable<FloatingWindow> windows) => this.windows = windows.ToList ();

        /// <summary>Gets the number of floating windows.</summary>
        public int Count => windows.Count;

        /// <summary>Gets the floating window at the specified index.</summary>
        public FloatingWindow this[int index] => windows[index];

        /// <inheritdoc/>
        public IEnumerator<FloatingWindow> GetEnumerator () => windows.GetEnumerator ();

        System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator () => GetEnumerator ();
    }

    /// <summary>Provides data for a cancellable dock-window event: the window involved and the one it replaces.</summary>
    public class DockWindowCancelEventArgs : CancelEventArgs
    {
        /// <summary>Initializes the event data.</summary>
        public DockWindowCancelEventArgs (DockWindowBase? oldWindow, DockWindowBase? newWindow)
        {
            OldWindow = oldWindow;
            NewWindow = newWindow;
        }

        /// <summary>Gets the window being replaced, if any.</summary>
        public DockWindowBase? OldWindow { get; }

        /// <summary>Gets the window involved.</summary>
        public DockWindowBase? NewWindow { get; }
    }

    /// <summary>Provides data for <see cref="RadDock.DockStateChanging"/>.</summary>
    public class DockStateChangingEventArgs : DockWindowCancelEventArgs
    {
        /// <summary>Initializes the event data.</summary>
        public DockStateChangingEventArgs (DockWindowBase window, DockState newDockState) : base (null, window) => NewDockState = newDockState;

        /// <summary>Gets the state the window is changing to.</summary>
        public DockState NewDockState { get; }
    }

    /// <summary>Provides data for <see cref="RadDock.FloatingWindowCreated"/>.</summary>
    public class FloatingWindowEventArgs : EventArgs
    {
        /// <summary>Initializes the event data.</summary>
        public FloatingWindowEventArgs (FloatingWindow window) => Window = window;

        /// <summary>Gets the floating window that was created.</summary>
        public FloatingWindow Window { get; }
    }
}
