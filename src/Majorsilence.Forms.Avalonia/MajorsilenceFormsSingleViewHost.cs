#if SINGLEVIEW
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Controls.Platform;
using Avalonia.Input;
using Avalonia.Input.TextInput;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using Avalonia.Rendering.SceneGraph;
using Avalonia.Skia;
using Avalonia.Threading;
using Avalonia.VisualTree;
using SkiaSharp;
using Majorsilence.Forms.Backends;

using System.Collections.Generic;

using AvInputMethod = Avalonia.Input.InputMethod;
using AvPoint = Avalonia.Point;
using AvVector = Avalonia.Vector;
using AvControl = Avalonia.Controls.Control;
using AvCursor = Avalonia.Input.Cursor;
using AvPointerPressedEventArgs = Avalonia.Input.PointerPressedEventArgs;
using AvPointerReleasedEventArgs = Avalonia.Input.PointerReleasedEventArgs;
using AvPointerEventArgs = Avalonia.Input.PointerEventArgs;
using AvPointerWheelChangedEventArgs = Avalonia.Input.PointerWheelEventArgs;
using AvKeyEventArgs = Avalonia.Input.KeyEventArgs;
using AvTextInputEventArgs = Avalonia.Input.TextInputEventArgs;

namespace Majorsilence.Forms
{
    /// <summary>
    /// The single-view counterpart to <see cref="MajorsilenceFormsWindowHost"/>, shared by every Avalonia
    /// platform with no real OS window manager -- currently browser/WebAssembly and Android. Both only
    /// offer a single embeddable view per app (<c>ISingleViewApplicationLifetime.MainView</c>) — Avalonia's
    /// browser platform's <c>BrowserWindowingPlatform.CreateWindow</c> always throws (see Avalonia's own
    /// WindowingPlatform.cs), and Android has no concept of a second freestanding OS window either — so
    /// instead of an Avalonia <c>Window</c>, every Majorsilence.Forms window (the main form, any further
    /// top-level forms, and popups like ComboBox dropdowns/menus) is a <see cref="Canvas"/>:
    ///   - The first non-popup window becomes <see cref="MainHost"/> and registers itself as MainView,
    ///     filling the viewport via ordinary Avalonia layout (no explicit Width/Height, so the default
    ///     Stretch alignment fills whatever the single-view TopLevel gives it).
    ///   - Everything else (popups, and any additional top-level forms) is added as an absolutely
    ///     positioned child of <see cref="MainHost"/>'s own Canvas, using the same coordinate space as
    ///     "screen" coordinates -- there is only one "screen" (the page/activity), so PointToScreen/
    ///     PointToClient and Location need no special-casing between the root and its overlay children.
    ///
    /// Known gap: outside-click popup dismissal that relies on real OS window deactivation
    /// (Application.ScheduleClosePopupsOnDeactivate) does not fire here, since a Canvas has no such
    /// concept. Dismissal via clicking elsewhere *inside* the app still works (Control.RaiseMouseDown
    /// closes popups whenever a click lands outside the active popup's own control tree, independent of
    /// window activation) -- only losing focus to something outside the app entirely is unhandled.
    /// </summary>
    [System.Diagnostics.CodeAnalysis.SuppressMessage ("Design", "CA1001", Justification = "_scene SKPictures are disposed in IWindowBackend.Close/DetachedFromVisualTree; there is no owning Window to dispose through here.")]
    internal sealed class MajorsilenceFormsSingleViewHost : Canvas, IWindowBackend, INativeControlHostBackend
    {
        internal static MajorsilenceFormsSingleViewHost? MainHost { get; private set; }

        private readonly WindowBase _owner;
        private readonly bool _isRoot;
        private System.Drawing.Point _location;
        private System.Drawing.Size _size;

        // The Majorsilence scene is recorded to an immutable SKPicture on the UI thread (RecordFrame)
        // and played back on Avalonia's render thread (SceneView -> SceneDrawOp, GPU-accelerated) --
        // rather than software-rasterised into a WriteableBitmap on the UI thread, which starved
        // Android's input channel and made it cancel touch gestures mid-swipe. Retired pictures are left
        // to the GC / SKPicture finalizer rather than Dispose()d, since a render-thread playback of a
        // just-replaced picture could still be in flight; each is a small op list, not a full framebuffer.
        private readonly SceneView _sceneView;
        private bool _dirty = true;
        private bool _renderPending;
        private bool _painting;
        private bool _invalidatePending;
        private readonly Dictionary<NativeControlHost, AvControl> _overlays = new ();

        // ── Soft keyboard + safe area (root host only) ──
        private readonly MajorsilenceFormsTextInputClient _imClient;
        private bool _textInputActive;
        private IInsetsManager? _insets;
        private IInputPane? _inputPane;

        // ── Touch scroll ──
        // Avalonia's ScrollGestureRecognizer is registered (AvaloniaGestureWiring) but on Android it
        // fires unreliably -- often not at all, and sometimes just a single stray event mid-gesture --
        // so scrolling is also synthesised from the raw touch pointer stream in the OnPointer* overrides
        // below. The synthesis backs off only while the real recognizer is *actively* delivering events
        // (_recognizerLive): a short time window rather than a permanent latch, because latching on the
        // first stray event left scrolling dead for the rest of the session on devices where the
        // recognizer never fires again.
        private long _recognizerScrollTs;   // Stopwatch ticks of the last real recognizer scroll event
        private bool _recognizerLive =>
            _recognizerScrollTs != 0 &&
            System.Diagnostics.Stopwatch.GetTimestamp () - _recognizerScrollTs < System.Diagnostics.Stopwatch.Frequency * 3 / 10;
        private AvPoint? _touchAnchor;     // press position; null once released / capture lost
        private AvPoint _touchLast;
        private bool _touchScrolling;      // past the start-distance slop -> we own this gesture
        private const double TouchScrollStartDistance = 8;   // logical px

        // Digitizer-chop bridging. On Android the touch stream for one continuous swipe arrives as a
        // burst of ~90Hz moves, then PointerCaptureLost, then NOTHING for ~300-1300ms, then a fresh
        // press further along the swipe path -- over and over (the software renderer keeps the UI thread
        // busy enough that Android periodically cancels the gesture; a dedicated render thread would
        // avoid it -- see the paint-throttle and DispatcherPriority notes below). Left alone that is a
        // stutter of tiny disconnected drags with no momentum.
        //
        // Bridging stitches it back together: on a lost capture the scroll is NOT ended -- the content
        // coasts through the blind gap (DeferTouchGestureEnd), and a press that lands within
        // _bridgeTimer's window and roughly on the swipe path is treated as the SAME gesture (coast
        // stopped, drag resumed, its position corrected, its tap swallowed). Only when the window
        // elapses with no such press is the gesture really over (FinaliseTouchGesture).
        private readonly DispatcherTimer _bridgeTimer;
        private bool _touchReleasePending; // contact lost mid-scroll; waiting to see if a press bridges it
        private AvPoint _touchReleasePos;
        private long _touchReleaseTs;      // Stopwatch ticks
        private AvPoint _gestureAnchorPt;  // where THIS swipe first touched down -- stays over the scrollable
                                           // content, so the coast/fling hit-tests there even when the finger
                                           // has since flown to a screen edge
        private const double BridgeWindowSeconds = 1.4;      // after a capture loss -- covers the re-acquisition gap
        private const double RealLiftFinaliseSeconds = 0.12; // after a genuine pointer-up -- momentum starts almost at once
        private const double BridgeAcrossAxis = 110;     // logical px; re-press drift across the swipe axis
        private const double BridgeAlongAxis = 900;      // logical px; the finger travels far along it while blind
        private const double CoastMinSpeed = 40;         // device px/s; below this a lost contact just stops

        // Momentum: the drag tracks the finger 1:1, then keeps gliding after lift-off with a decaying
        // velocity -- the native Android/iOS feel. Avalonia's recognizer would do this itself (its
        // IsScrollInertiaEnabled), but it isn't firing, so the fling is synthesised here too.
        private readonly DispatcherTimer _flingTimer;
        private AvVector _touchVelocity;   // device px/s, smoothed over the last few moves
        private bool _touchVelocityValid;
        private long _touchLastMoveTs;     // Stopwatch ticks
        private AvVector _flingVelocity;   // device px/s, decays each tick
        private AvPoint _flingOrigin;      // the gesture's start anchor -- where the coast/fling hit-tests
        private long _flingLastTs, _flingStartTs;
        private double _flingAccumX, _flingAccumY;   // sub-pixel carry so the glide tail stays smooth
        private int _flingSentX, _flingSentY;        // device px the glide has scrolled since the contact was lost
        private double _flingRetention;              // active decay: gentle while coasting a blind gap, sharp after
        private bool _coasting;                      // the glide is filling a digitizer blind gap, not a post-lift fling
        private bool _flingCaughtByPress;  // a press landed on a live fling -> swallow its tap
        private const double FlingStopSpeed = 18;        // device px/s; the glide ends here
        private const double FlingRetentionPerSecond = 0.04;   // post-lift decay -- keeps 4% after 1s (tau ~ 0.31s)
        private const double CoastRetentionFast = 0.6;   // blind-gap decay for a real swipe -- near-constant, the finger keeps going
        private const double CoastRetentionSlow = 0.08;  // blind-gap decay for a slow drag -- dies in ~250ms, re-press correction carries it
        private const double CoastFastSpeed = 320;       // device px/s; at/above this the finger is really swiping (fast decay profile)
        private const double CoastToFlingSpeed = 120;    // device px/s; a gap that ends this slow just stops, no post-lift fling
        private const double FlingMaxSeconds = 2.0;

        internal MajorsilenceFormsSingleViewHost (WindowBase owner, bool isPopup)
        {
            _owner = owner;
            _isRoot = !isPopup && MainHost is null;

            Focusable = true;
            Background = Brushes.Transparent;

            _sceneView = new SceneView ();
            Canvas.SetLeft (_sceneView, 0);
            Canvas.SetTop (_sceneView, 0);
            Children.Add (_sceneView);   // bottom child; native-control overlays are added above it

            // LayoutUpdated fires after every completed layout pass, so it catches the (asynchronous,
            // relative to construction) moment the single-view TopLevel actually assigns this control a
            // real size -- unlike a one-shot render scheduled from OnAttachedToVisualTree, which can fire
            // before that first real layout pass has run and then never gets asked again.
            LayoutUpdated += (_, _) => { ScheduleRender (); RefreshSafeArea (); };

            if (_isRoot) {
                MainHost = this;

                // A bare Canvas has no intrinsic size of its own to request; Stretch alignment is what
                // makes the single-view TopLevel's arrange pass give it the full viewport instead of 0x0.
                HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Stretch;
                VerticalAlignment = Avalonia.Layout.VerticalAlignment.Stretch;

                if (Avalonia.Application.Current?.ApplicationLifetime is ISingleViewApplicationLifetime lifetime)
                    lifetime.MainView = this;
            }

            // The onRecognizerScroll callback timestamps the last real recognizer event; while those keep
            // arriving the raw-pointer synthesis in OnPointerMoved stands down (see _recognizerLive).
            AvaloniaGestureWiring.Attach (this, _owner, () => Scale,
                onRecognizerScroll: () => _recognizerScrollTs = System.Diagnostics.Stopwatch.GetTimestamp ());

            // Background, not Render: Avalonia's Render priority sits ABOVE Input, so a fling/paint tick
            // posted there preempts the touch-event queue -- Android then sees input going un-ACKed and
            // cancels the in-progress gesture. Below Input, the pointer burst drains first.
            _flingTimer = new DispatcherTimer (DispatcherPriority.Background) { Interval = TimeSpan.FromMilliseconds (16) };
            _flingTimer.Tick += (_, _) => FlingTick ();

            _bridgeTimer = new DispatcherTimer (DispatcherPriority.Input) { Interval = TimeSpan.FromSeconds (BridgeWindowSeconds) };
            _bridgeTimer.Tick += (_, _) => FinaliseTouchGesture ();

            // The on-screen keyboard: Avalonia asks the focused InputElement for a text-input client when
            // it wants an IME. This host routes keys manually and always "has focus", so it answers that
            // request itself -- returning the client only while a Majorsilence.Forms TextBox is focused
            // (SetTextInputActive), which is what makes Android/iOS/browser show and hide the keyboard.
            _imClient = new MajorsilenceFormsTextInputClient (this, _owner);
            TextInputMethodClientRequested += (_, args) => {
                if (_textInputActive)
                    args.Client = _imClient;
            };
        }

        /// <inheritdoc/>
        protected override void OnAttachedToVisualTree (VisualTreeAttachmentEventArgs e)
        {
            base.OnAttachedToVisualTree (e);
            ScheduleRender ();

            if (_isRoot)
                WireSingleViewServices ();
        }

        /// <inheritdoc/>
        protected override void OnDetachedFromVisualTree (VisualTreeAttachmentEventArgs e)
        {
            _renderPending = false;
            StopFling ();
            _bridgeTimer.Stop ();
            _touchReleasePending = false;

            if (_insets is not null)
                _insets.SafeAreaChanged -= OnSafeAreaChanged;
            if (_inputPane is not null)
                _inputPane.StateChanged -= OnInputPaneStateChanged;
            _insets = null;
            _inputPane = null;

            base.OnDetachedFromVisualTree (e);
        }

        // Wire the device safe-area insets and the on-screen keyboard occlusion into the core, once the
        // host is in the visual tree and a TopLevel is reachable. Root host only -- there is one page.
        private void WireSingleViewServices ()
        {
            var top = TopLevel.GetTopLevel (this);
            if (top is null)
                return;

            _insets = top.InsetsManager;
            if (_insets is not null) {
                // We draw the whole page and inset the layout ourselves; ask the OS not to letterbox and
                // don't let Avalonia also pad this control (it would double-count).
                try { _insets.DisplayEdgeToEdgePreference = true; } catch { /* not settable on every platform */ }
                TopLevel.SetAutoSafeAreaPadding (this, false);
                _insets.SafeAreaChanged += OnSafeAreaChanged;
                RefreshSafeArea ();   // corrected again from LayoutUpdated once RenderScaling is known
            }

            _inputPane = top.InputPane;
            if (_inputPane is not null)
                _inputPane.StateChanged += OnInputPaneStateChanged;
        }

        private void OnSafeAreaChanged (object? sender, SafeAreaChangedArgs e) => PushSafeArea (e.SafeAreaPadding);

        private Majorsilence.Forms.Padding _lastSafeArea = new (-1, -1, -1, -1);

        // Re-reads and re-pushes the current safe area. Called on every layout pass as well as on the OS
        // event: Avalonia derives SafeAreaPadding by dividing the raw window insets by RenderScaling, and
        // the first event/read can land before RenderScaling is known (it reads 1), yielding a value in
        // physical pixels that is ~scale times too large. PushSafeArea drops those until layout settles.
        private void RefreshSafeArea ()
        {
            if (_isRoot && _insets is not null)
                PushSafeArea (_insets.SafeAreaPadding);
        }

        private void PushSafeArea (Thickness t)
        {
            // SafeAreaPadding is already in logical (DIP) units -- Avalonia's Android InsetsManager
            // divides the native WindowInsets by RenderScaling for us. But that division is a no-op
            // (scale == 1) until the single-view TopLevel has completed its first real layout, so an
            // early non-zero reading is really physical pixels and must be ignored; the LayoutUpdated
            // re-read will deliver the correct value once RenderScaling settles.
            if (t != default && Scale <= 1)
                return;

            var p = new Majorsilence.Forms.Padding (
                (int) System.Math.Round (t.Left), (int) System.Math.Round (t.Top),
                (int) System.Math.Round (t.Right), (int) System.Math.Round (t.Bottom));

            if (p == _lastSafeArea)
                return;
            _lastSafeArea = p;
            _owner.HandleSafeAreaChanged (p);
        }

        private void OnInputPaneStateChanged (object? sender, InputPaneStateEventArgs e)
        {
            var r = e.NewState == InputPaneState.Open ? e.EndRect : default;
            // OccludedRect is in screen DIPs; translate its top edge into this control's local space so
            // the core can measure how much of the form the keyboard now covers.
            var localTop = r.Height > 0
                ? (this.PointToClient (new PixelPoint ((int) r.X, (int) r.Y)).Y)
                : 0;
            var occludedHeight = r.Height > 0
                ? System.Math.Max (0, (int) System.Math.Round (Bounds.Height - localTop))
                : 0;
            _owner.HandleInputPaneChanged (occludedHeight > 0
                ? new System.Drawing.Rectangle (0, (int) System.Math.Round (Bounds.Height) - occludedHeight, (int) System.Math.Round (Bounds.Width), occludedHeight)
                : System.Drawing.Rectangle.Empty);
        }

        private double Scale => TopLevel.GetTopLevel (this)?.RenderScaling ?? 1;

        // ── Rendering ────────────────────────────────────────────────────────────────────────────────
        // RecordFrame() walks the Majorsilence scene on the UI thread and captures it as an immutable
        // SKPicture (cheap -- it emits draw ops, it does not rasterise; the per-control SKBitmap
        // back-buffers in Control.PaintChildren are still filled here, but the full-scene composite and
        // present are only *recorded*). SceneDrawOp then plays that picture back on Avalonia's render
        // thread, GPU-accelerated, via Render(DrawingContext). This replaces a per-frame UI-thread
        // software raster into a WriteableBitmap, which kept the thread busy enough that Android
        // cancelled touch gestures mid-swipe.

        private void ScheduleRender ()
        {
            if (_renderPending)
                return;
            _renderPending = true;
            // Background, below Input: the record walk still costs UI-thread time (per-control raster in
            // Control.PaintChildren), and Avalonia's Render priority is above Input, so scheduling it
            // there preempted the touch queue and Android cancelled the in-progress gesture.
            Dispatcher.UIThread.Post (() => {
                _renderPending = false;
                RecordFrame ();
            }, DispatcherPriority.Background);
        }

        private long _lastPaintTs;                            // Stopwatch ticks of the last completed record
        private PixelSize _lastPhys;                          // physical scene size at the last record
        private const double TouchPaintMinIntervalMs = 32;    // cap the record walk to ~30fps while a finger
                                                              // is down / the fling coasts, so the UI thread
                                                              // keeps slack to service touch

        private void RecordFrame ()
        {
            if (Bounds.Width <= 0 || Bounds.Height <= 0)
                return;

            var scaling = Scale <= 0 ? 1 : Scale;
            var physW = Math.Max (1, (int)Math.Round (Bounds.Width * scaling));
            var physH = Math.Max (1, (int)Math.Round (Bounds.Height * scaling));
            var resized = physW != _lastPhys.Width || physH != _lastPhys.Height;

            if (!resized && !_dirty && !_owner.adapter.NeedsPaint)
                return;

            // Throttle during a touch drag / blind-gap coast: the record walk still rasterises dirty
            // control back-buffers on the UI thread. A dropped frame here is invisible; a cancelled
            // gesture is the stutter.
            if (_touchAnchor is not null || _touchReleasePending) {
                var sinceMs = (System.Diagnostics.Stopwatch.GetTimestamp () - _lastPaintTs) * 1000.0 / System.Diagnostics.Stopwatch.Frequency;
                if (sinceMs < TouchPaintMinIntervalMs) {
                    if (!_renderPending) {
                        _renderPending = true;
                        DispatcherTimer.RunOnce (() => { _renderPending = false; ScheduleRender (); },
                            TimeSpan.FromMilliseconds (TouchPaintMinIntervalMs - sinceMs), DispatcherPriority.Background);
                    }
                    return;
                }
            }
            _lastPaintTs = System.Diagnostics.Stopwatch.GetTimestamp ();

            _dirty = false;
            _invalidatePending = false;

            SKPicture? picture = null;
            _painting = true;
            try {
                using var recorder = new SKPictureRecorder ();
                var canvas = recorder.BeginRecording (new SKRect (0, 0, physW, physH));
                canvas.Clear (SKColors.Transparent);
                _owner.RenderFrame (canvas, physW, physH, scaling);
                picture = recorder.EndRecording ();
            } catch (Exception ex) {
                Console.Error.WriteLine ($"[CF] SingleViewHost RecordFrame error: {ex.Message}");
            } finally {
                _painting = false;
            }

            if (picture is not null) {
                _lastPhys = new PixelSize (physW, physH);
                _sceneView.Width = Bounds.Width;
                _sceneView.Height = Bounds.Height;
                _sceneView.Present (picture, scaling);   // the previous picture is now garbage (see field comment)
            }

            if (_invalidatePending)
                ScheduleRender ();
        }

        // Panel.Render is sealed, so the scene can't be drawn by the host Canvas itself. This bottom
        // child -- a plain Control whose Render is not sealed -- plays the recorded picture on the
        // render thread; the native-control overlays are Canvas children above it, unchanged.
        private sealed class SceneView : AvControl
        {
            private SKPicture? _picture;
            private double _pictureScale = 1;

            public SceneView () => IsHitTestVisible = false;

            public void Present (SKPicture picture, double pictureScale)
            {
                _picture = picture;
                _pictureScale = pictureScale;
                InvalidateVisual ();
            }

            public override void Render (DrawingContext context)
            {
                base.Render (context);
                var picture = _picture;
                if (picture is not null && Bounds is { Width: > 0, Height: > 0 })
                    context.Custom (new SceneDrawOp (new Rect (Bounds.Size), picture, _pictureScale));
            }
        }

        // Plays a recorded scene picture into Avalonia's render-thread Skia canvas. The picture is in
        // physical pixels; the leased canvas already carries the layout + DPI transform (logical units),
        // so it is scaled down by 1/pictureScale before playback.
        private sealed class SceneDrawOp : ICustomDrawOperation
        {
            private readonly SKPicture _picture;
            private readonly float _inverseScale;

            public SceneDrawOp (Rect bounds, SKPicture picture, double pictureScale)
            {
                Bounds = bounds;
                _picture = picture;
                _inverseScale = pictureScale > 0 ? (float)(1.0 / pictureScale) : 1f;
            }

            public Rect Bounds { get; }

            // Input is handled by the host Canvas, not the draw op.
            public bool HitTest (Point p) => false;

            // A fresh op every frame; never equal, so Avalonia always re-renders.
            public bool Equals (ICustomDrawOperation? other) => false;

            public void Dispose () { /* the host owns the picture's lifetime */ }

            public void Render (ImmediateDrawingContext context)
            {
                var lease = context.TryGetFeature<ISkiaSharpApiLeaseFeature> ()?.Lease ();
                if (lease is null)
                    return;   // non-Skia backend: nothing we can do here

                using (lease) {
                    var canvas = lease.SkCanvas;
                    canvas.Save ();
                    canvas.Scale (_inverseScale, _inverseScale);
                    canvas.DrawPicture (_picture);
                    canvas.Restore ();
                }
            }
        }

        // ── Input forwarding (Avalonia → Majorsilence.Forms; positions scaled to physical pixels) ───────

        protected override void OnPointerPressed (AvPointerPressedEventArgs e)
        {
            Focus ();

            var pos = e.GetPosition (this);
            var isTouch = e.Pointer.Type is PointerType.Touch or PointerType.Pen;
            var now = System.Diagnostics.Stopwatch.GetTimestamp ();

            // Is this press really the same swipe the digitizer just chopped? Only bridge a contact that
            // was already scrolling -- a plain tap that never crossed the slop finalises as a tap, so
            // tap-to-select is untouched. A press that lands while the blind-gap coast is still running
            // is a continuation almost by definition; otherwise it must be soon after, and roughly on
            // the swipe path (far along the axis is fine -- the finger moved while the digitizer slept).
            var bridging = false;
            if (_touchReleasePending) {
                var gap = (now - _touchReleaseTs) / (double) System.Diagnostics.Stopwatch.Frequency;
                bridging = isTouch && !_recognizerLive
                           && (_coasting || (gap < BridgeWindowSeconds && BridgeReachable (pos - _touchReleasePos)));
                _touchReleasePending = false;
                _bridgeTimer.Stop ();
            }

            var props = e.GetCurrentPoint (this).Properties;
            _owner.HandlePointerPressed (
                AvaloniaKeyInterop.PressedButton (props.PointerUpdateKind),
                (int)(pos.X * Scale), (int)(pos.Y * Scale),
                AvaloniaKeyInterop.ModifiersOnly (e.KeyModifiers));

            // A press lands: end any glide still running. If one was, this press only stops it -- it
            // must not also tap/select (native behaviour: the first touch catches the fling).
            _flingCaughtByPress = _flingTimer.IsEnabled;
            var coastSent = new System.Drawing.Point (_flingSentX, _flingSentY);
            StopFling ();

            if (!_recognizerLive && isTouch) {
                _touchAnchor = pos;
                _touchLast = pos;
                _touchLastMoveTs = 0;   // next move keeps the carried velocity rather than one across the seam

                if (bridging) {
                    _touchScrolling = true;   // stay in the scroll we were already doing
                    // _gestureAnchorPt unchanged -- still the first touch-down of this swipe

                    // The coast glide only *approximated* the finger's travel through the blind gap.
                    // Now the real end position is known: scroll by whatever it under- or over-shot, so
                    // the content lands exactly where a continuous drag would have put it.
                    var corrX = (int)((pos.X - _touchReleasePos.X) * Scale) - coastSent.X;
                    var corrY = (int)((pos.Y - _touchReleasePos.Y) * Scale) - coastSent.Y;
                    if (corrX != 0 || corrY != 0)
                        _owner.HandleScrollGesture ((int)(pos.X * Scale), (int)(pos.Y * Scale), corrX, corrY);
                } else {
                    _touchScrolling = _flingCaughtByPress;
                    _touchVelocity = default;
                    _touchVelocityValid = false;
                    _gestureAnchorPt = pos;   // a brand-new swipe starts here
                    _flingSentX = _flingSentY = 0;
                }

                // A bridging press, or one that just caught a fling, must not also select.
                if (bridging || _flingCaughtByPress)
                    _owner.HandlePointerReleased (MouseButtons.Left, -1_000_000, -1_000_000, Keys.None);
            }

            base.OnPointerPressed (e);
        }

        protected override void OnPointerReleased (AvPointerReleasedEventArgs e)
        {
            var pos = e.GetPosition (this);

            // A synthesised scroll already cancelled the press (SynthesiseTouchScroll), so the real
            // release must not fire a second MouseUp/Click -- that is what would select an item at the
            // end of a swipe.
            if (!_touchScrolling) {
                var props = e.GetCurrentPoint (this).Properties;
                _owner.HandlePointerReleased (
                    AvaloniaKeyInterop.ReleasedButton (props.PointerUpdateKind),
                    (int)(pos.X * Scale), (int)(pos.Y * Scale),
                    AvaloniaKeyInterop.ModifiersOnly (e.KeyModifiers));
            } else {
                // A real pointer-up: the finger genuinely lifted, so only a brief window to absorb a
                // stray re-press -- momentum should start almost immediately.
                DeferTouchGestureEnd (pos, ambiguous: false);
            }

            _touchAnchor = null;
            _touchScrolling = false;   // restored by a bridging press, if one comes
            base.OnPointerReleased (e);
        }

        protected override void OnPointerCaptureLost (PointerCaptureLostEventArgs e)
        {
            // Capture loss mid-scroll is ambiguous: on Android it is mostly the digitizer dropping and
            // re-acquiring contact within one swipe, not a real lift -- so keep the gesture alive for
            // the full bridge window to catch the re-press.
            if (_touchScrolling)
                DeferTouchGestureEnd (_touchLast, ambiguous: true);

            _touchAnchor = null;
            _touchScrolling = false;
            base.OnPointerCaptureLost (e);
        }

        // True if a re-press at this offset from the lost contact is still the same swipe: generous
        // along the swipe axis (the finger travelled while the digitizer was blind), tight across it.
        private bool BridgeReachable (AvVector jump)
        {
            var alongY = System.Math.Abs (_touchVelocity.Y) >= System.Math.Abs (_touchVelocity.X);
            var along = alongY ? System.Math.Abs (jump.Y) : System.Math.Abs (jump.X);
            var across = alongY ? System.Math.Abs (jump.X) : System.Math.Abs (jump.Y);
            return across < BridgeAcrossAxis && along < BridgeAlongAxis;
        }

        // A touch scroll has (maybe) ended. Don't commit yet: on Android a lost capture is usually the
        // digitizer dropping contact mid-swipe, and a fresh press lands within ~1s further along the
        // path. The content coasts through that blind gap at the last measured finger speed -- for a
        // fast swipe near-constant, for a slow drag decaying quickly (its speed sample is too noisy to
        // trust, and the re-press position correction carries it instead). A bridging press resumes the
        // drag; if _bridgeTimer fires first, FinaliseTouchGesture turns the coast into a normal
        // post-lift fling (or stops it). <paramref name="ambiguous"/> is false for a real pointer-up
        // (short window, momentum starts almost at once) and true for a capture loss.
        private void DeferTouchGestureEnd (AvPoint pos, bool ambiguous)
        {
            _touchReleasePending = true;
            _touchReleasePos = pos;
            _touchReleaseTs = System.Diagnostics.Stopwatch.GetTimestamp ();
            _bridgeTimer.Stop ();
            _bridgeTimer.Interval = TimeSpan.FromSeconds (ambiguous ? BridgeWindowSeconds : RealLiftFinaliseSeconds);
            _bridgeTimer.Start ();

            if (_recognizerLive || !_touchVelocityValid || _touchVelocity.Length < CoastMinSpeed) {
                StopFling ();
                return;
            }

            var fast = _touchVelocity.Length >= CoastFastSpeed;
            _coasting = true;
            _flingVelocity = _touchVelocity;
            _flingRetention = fast ? CoastRetentionFast : CoastRetentionSlow;
            _flingOrigin = _gestureAnchorPt;
            _flingAccumX = _flingAccumY = 0;
            _flingSentX = _flingSentY = 0;
            _flingLastTs = _flingStartTs = _touchReleaseTs;
            _flingTimer.Start ();
        }

        // The bridge window elapsed with no re-press: the finger really did lift. Hand the coast over
        // to the sharp post-lift decay (or stop it if it has already slowed to a crawl).
        private void FinaliseTouchGesture ()
        {
            _bridgeTimer.Stop ();
            if (!_touchReleasePending)
                return;
            _touchReleasePending = false;
            _touchVelocityValid = false;
            _coasting = false;

            if (_flingTimer.IsEnabled && _flingVelocity.Length >= CoastToFlingSpeed) {
                _flingRetention = FlingRetentionPerSecond;
                _flingStartTs = System.Diagnostics.Stopwatch.GetTimestamp ();
            } else {
                StopFling ();
            }
        }

        protected override void OnPointerMoved (AvPointerEventArgs e)
        {
            var pos = e.GetPosition (this);

            // Forward the move as a mouse move only when it is NOT a touch scroll -- otherwise a swipe
            // also drags a selection / updates hover under the finger.
            if (!SynthesiseTouchScroll (e, pos)) {
                var props = e.GetCurrentPoint (this).Properties;
                _owner.HandlePointerMoved (
                    AvaloniaKeyInterop.ToMouseButtons (props),
                    (int)(pos.X * Scale), (int)(pos.Y * Scale),
                    AvaloniaKeyInterop.ModifiersOnly (e.KeyModifiers));
            }

            base.OnPointerMoved (e);
        }

        // Turns a raw touch/pen drag into the neutral scroll-gesture pipeline (HandleScrollGesture ->
        // RaiseScrollGesture -> the leaf's OnScrollGesture -> its scrollbar). Returns true once the
        // drag has been claimed as a scroll, so the caller stops forwarding the move as a mouse move.
        // Disabled the moment Avalonia's own ScrollGestureRecognizer proves it works (_recognizerLive).
        private bool SynthesiseTouchScroll (AvPointerEventArgs e, AvPoint pos)
        {
            if (_recognizerLive || _touchAnchor is null || e.Pointer.Type is not (PointerType.Touch or PointerType.Pen))
                return false;

            if (!_touchScrolling) {
                var moved = pos - _touchAnchor.Value;
                if (System.Math.Abs (moved.X) < TouchScrollStartDistance && System.Math.Abs (moved.Y) < TouchScrollStartDistance)
                    return false;

                _touchScrolling = true;
                // Measure the first delta from the anchor, not from here, so the distance already
                // travelled to cross the slop still scrolls.
                _touchLast = _touchAnchor.Value;

                // Cancel the tap this drag started as: a MouseUp/Click far outside every control (no
                // real screen is a million pixels wide) drops the leaf's mouse capture without
                // hit-testing onto -- and selecting -- anything.
                _owner.HandlePointerReleased (MouseButtons.Left, -1_000_000, -1_000_000, Keys.None);
            }

            // delta measured in device pixels; leave _touchLast where it is until a whole pixel has
            // accumulated, so a slow drag isn't lost to int truncation frame by frame.
            var moveLogX = pos.X - _touchLast.X;
            var moveLogY = pos.Y - _touchLast.Y;
            var dx = (int)(moveLogX * Scale);
            var dy = (int)(moveLogY * Scale);
            if (dx == 0 && dy == 0)
                return true;

            // Smooth the finger speed (device px/s) over the last few moves so a fling launches with
            // the release-moment velocity rather than a single noisy sample.
            var now = System.Diagnostics.Stopwatch.GetTimestamp ();
            if (_touchLastMoveTs != 0) {
                var dt = (now - _touchLastMoveTs) / (double) System.Diagnostics.Stopwatch.Frequency;
                if (dt is > 0.0008 and < 0.2) {
                    var inst = new AvVector (moveLogX * Scale / dt, moveLogY * Scale / dt);
                    _touchVelocity = _touchVelocityValid ? _touchVelocity * 0.55 + inst * 0.45 : inst;
                    _touchVelocityValid = true;
                }
            }
            _touchLastMoveTs = now;
            _touchLast = pos;

            // An upward drag is negative, matching Avalonia's own recognizer -- content follows the finger.
            _owner.HandleScrollGesture ((int)(pos.X * Scale), (int)(pos.Y * Scale), dx, dy);
            return true;
        }

        private void StopFling ()
        {
            _flingTimer.Stop ();
            _flingVelocity = default;
            _coasting = false;
            // _flingSentX/Y is NOT cleared here: a coast that self-stops mid-gap has still scrolled the
            // content, and the next bridging press must subtract that from its position correction.
            // It is zeroed when a fresh coast starts (DeferTouchGestureEnd) or a new swipe begins.
        }

        // One frame of inertial scroll: move by velocity*dt (device px, sub-pixel carried), then decay
        // the velocity by _flingRetention and stop once it is slow or the glide has run its limit.
        // _flingRetention is gentle (CoastRetention*) while coasting a digitizer blind gap and sharp
        // (FlingRetentionPerSecond) once the finger has genuinely lifted.
        private void FlingTick ()
        {
            if (_recognizerLive || _touchAnchor is not null) {   // a new touch is down -> the press handler owns it
                StopFling ();
                return;
            }

            var now = System.Diagnostics.Stopwatch.GetTimestamp ();
            var freq = (double) System.Diagnostics.Stopwatch.Frequency;
            var dt = (now - _flingLastTs) / freq;
            _flingLastTs = now;
            if (dt <= 0)
                return;
            if (dt > 0.05)   // a hitch shouldn't fling the content a long way in one step
                dt = 0.05;

            _flingAccumX += _flingVelocity.X * dt;
            _flingAccumY += _flingVelocity.Y * dt;
            _flingVelocity *= System.Math.Pow (_flingRetention, dt);

            var stepX = (int) _flingAccumX;
            var stepY = (int) _flingAccumY;
            _flingAccumX -= stepX;
            _flingAccumY -= stepY;

            if (stepX != 0 || stepY != 0) {
                _flingSentX += stepX;
                _flingSentY += stepY;
                _owner.HandleScrollGesture (
                    (int)(_flingOrigin.X * Scale), (int)(_flingOrigin.Y * Scale), stepX, stepY);
            }

            // While coasting a blind gap the glide must not self-terminate on the MaxSeconds limit --
            // the bridge timer owns that decision. Only a truly-lifted (sharp-decay) glide times out.
            var timedOut = _flingRetention <= FlingRetentionPerSecond && (now - _flingStartTs) / freq > FlingMaxSeconds;
            if (_flingVelocity.Length < FlingStopSpeed || timedOut)
                StopFling ();
        }

        protected override void OnPointerWheelChanged (AvPointerWheelChangedEventArgs e)
        {
            var pos = e.GetPosition (this);
            var props = e.GetCurrentPoint (this).Properties;
            _owner.HandlePointerWheel (
                AvaloniaKeyInterop.ToMouseButtons (props),
                (int)(pos.X * Scale), (int)(pos.Y * Scale),
                new System.Drawing.Point ((int)e.Delta.X, (int)e.Delta.Y),
                AvaloniaKeyInterop.ModifiersOnly (e.KeyModifiers));
            base.OnPointerWheelChanged (e);
        }

        protected override void OnPointerExited (AvPointerEventArgs e)
        {
            var pos = e.GetPosition (this);
            var props = e.GetCurrentPoint (this).Properties;
            _owner.HandlePointerExited (
                AvaloniaKeyInterop.ToMouseButtons (props),
                (int)(pos.X * Scale), (int)(pos.Y * Scale),
                AvaloniaKeyInterop.ModifiersOnly (e.KeyModifiers));
            base.OnPointerExited (e);
        }

        protected override void OnKeyDown (AvKeyEventArgs e)
        {
            if (_owner.HandleKeyDown (AvaloniaKeyInterop.AddModifiers (AvaloniaKeyInterop.ToFormsKey (e.Key), e.KeyModifiers)))
                e.Handled = true;
            base.OnKeyDown (e);
        }

        protected override void OnKeyUp (AvKeyEventArgs e)
        {
            if (_owner.HandleKeyUp (AvaloniaKeyInterop.AddModifiers (AvaloniaKeyInterop.ToFormsKey (e.Key), e.KeyModifiers)))
                e.Handled = true;
            base.OnKeyUp (e);
        }

        protected override void OnTextInput (AvTextInputEventArgs e)
        {
            if (_owner.HandleTextInput (e.Text ?? string.Empty))
                e.Handled = true;
            base.OnTextInput (e);
        }

        // ── IWindowBackend ───────────────────────────────────────────────────────────────────────────

        System.Drawing.Point IWindowBackend.Location {
            get => _location;
            set {
                _location = value;
                if (!_isRoot) {
                    Canvas.SetLeft (this, value.X);
                    Canvas.SetTop (this, value.Y);
                }
            }
        }

        System.Drawing.Size IWindowBackend.Size {
            get => _isRoot ? new System.Drawing.Size ((int)Bounds.Width, (int)Bounds.Height) : _size;
            set {
                _size = value;
                // The root fills the browser viewport via ordinary layout (Stretch); it doesn't own an
                // explicit size to set. Popups/secondary windows size themselves explicitly.
                if (!_isRoot) {
                    Width = value.Width;
                    Height = value.Height;
                }
            }
        }

        System.Drawing.Size IWindowBackend.ClientSize
            => new System.Drawing.Size ((int)Bounds.Width, (int)Bounds.Height);

        double IWindowBackend.Scaling => Scale <= 0 ? 1 : Scale;

        bool IWindowBackend.IsSingleView => true;

        void IWindowBackend.Show ()
        {
            if (!_isRoot && MainHost is not null && !MainHost.Children.Contains (this))
                MainHost.Children.Add (this);
            IsVisible = true;
        }

        void IWindowBackend.ShowDialog (IWindowBackend? owner)
            // No true modal window concept in the browser; the parent-disable + blocking-wait behaviour
            // that makes this act modal is handled above this seam (WindowBase.ShowDialog + RunModalLoop).
            => ((IWindowBackend)this).Show ();

        void IWindowBackend.Hide () => IsVisible = false;

        void IWindowBackend.Close ()
        {
            if (!_isRoot)
                MainHost?.Children.Remove (this);
        }

        void IWindowBackend.Activate () => Focus ();

        // Single-view (browser/mobile): one surface, nothing to activate. Stored only.
        bool IWindowBackend.ShowActivated { get; set; } = true;

        bool IWindowBackend.Enabled {
            get => IsEnabled;
            set => IsEnabled = value;
        }

        string IWindowBackend.Title { set { /* no browser tab/window title support yet */ } }

        bool IWindowBackend.Topmost { get => false; set { } }

        void IWindowBackend.SetSystemDecorations (bool useSystemDecorations) { /* no chrome in the browser */ }

        void IWindowBackend.SetCursor (CursorType cursor) => Cursor = MapCursor (cursor);

        void IWindowBackend.SetTextInputActive (bool active, TextInputKind kind)
        {
            _textInputActive = active;

            // Describe the wanted keyboard to the platform.
            TextInputOptions.SetMultiline (this, kind == TextInputKind.Multiline);
            TextInputOptions.SetIsSensitive (this, kind == TextInputKind.Password);
            TextInputOptions.SetContentType (this, kind switch {
                TextInputKind.Password => TextInputContentType.Password,
                TextInputKind.Email    => TextInputContentType.Email,
                TextInputKind.Number   => TextInputContentType.Number,
                TextInputKind.Url      => TextInputContentType.Url,
                TextInputKind.Phone    => TextInputContentType.Number,
                _                      => TextInputContentType.Normal,
            });

            AvInputMethod.SetIsInputMethodEnabled (this, active);

            if (active) {
                if (!IsFocused)
                    Focus ();
                _imClient.NotifyCursorMoved ();
            }

            // Ask Avalonia's text-input manager to re-query TextInputMethodClientRequested so it picks up
            // (active) or drops (inactive) our client -- which is what raises/dismisses the keyboard.
            RaiseEvent (new global::Avalonia.Interactivity.RoutedEventArgs (
                AvInputMethod.TextInputMethodClientRequeryRequestedEvent));
        }

        void IWindowBackend.SetIcon (byte[]? iconPng) { /* no window icon in the browser */ }

        System.Drawing.Size IWindowBackend.MinimumSize { set { } }

        System.Drawing.Size IWindowBackend.MaximumSize { set { } }

        bool IWindowBackend.CanResize { get => false; set { } }

        bool IWindowBackend.ShowInTaskbar { get => false; set { } }

        double IWindowBackend.Opacity { get => Opacity; set => Opacity = value; }

        FormWindowState IWindowBackend.WindowState { get => FormWindowState.Normal; set { } }

        System.Drawing.Point IWindowBackend.PointToClient (System.Drawing.Point screen)
        {
            var top = TopLevel.GetTopLevel (this);
            if (top is null) return screen;
            var inTop = top.PointToClient (new PixelPoint (screen.X, screen.Y));
            var local = top.TranslatePoint (inTop, this) ?? inTop;
            return new System.Drawing.Point ((int)local.X, (int)local.Y);
        }

        System.Drawing.Point IWindowBackend.PointToScreen (System.Drawing.Point client)
        {
            var top = TopLevel.GetTopLevel (this);
            if (top is null) return client;
            var inTop = this.TranslatePoint (new AvPoint (client.X, client.Y), top) ?? new AvPoint (client.X, client.Y);
            var screen = top.PointToScreen (inTop);
            return new System.Drawing.Point (screen.X, screen.Y);
        }

        void IWindowBackend.BeginMoveDrag () { /* no window manager to drag against */ }

        void IWindowBackend.BeginResizeDrag (Backends.WindowEdge edge) { /* not resizable */ }

        void IWindowBackend.Invalidate ()
        {
            if (_painting) {
                _invalidatePending = true;
                return;
            }
            _dirty = true;
            ScheduleRender ();
        }

        // ── INativeControlHostBackend ────────────────────────────────────────────────────────────────

        void INativeControlHostBackend.AttachNativeControl (NativeControlHost host, object nativeControl)
        {
            if (nativeControl is not AvControl control)
                return;

            if (_overlays.TryGetValue (host, out var existing) && !ReferenceEquals (existing, control))
                Children.Remove (existing);

            _overlays[host] = control;
            if (!Children.Contains (control))
                Children.Add (control);
        }

        void INativeControlHostBackend.UpdateNativeControl (NativeControlHost host, System.Drawing.Rectangle logicalBounds, System.Drawing.Rectangle clipBounds, bool visible)
        {
            if (!_overlays.TryGetValue (host, out var control))
                return;

            Canvas.SetLeft (control, logicalBounds.X);
            Canvas.SetTop (control, logicalBounds.Y);
            control.Width = Math.Max (0, logicalBounds.Width);
            control.Height = Math.Max (0, logicalBounds.Height);
            control.IsVisible = visible;

            control.Clip = clipBounds == logicalBounds
                ? null
                : new RectangleGeometry (new Rect (
                    clipBounds.X - logicalBounds.X, clipBounds.Y - logicalBounds.Y,
                    Math.Max (0, clipBounds.Width), Math.Max (0, clipBounds.Height)));
        }

        void INativeControlHostBackend.DetachNativeControl (NativeControlHost host)
        {
            if (_overlays.Remove (host, out var control))
                Children.Remove (control);
        }

        // ── File/folder pickers (delegated to the host TopLevel's storage provider) ──────────────────

        private static Avalonia.Platform.Storage.FilePickerFileType[] MapFilters (System.Collections.Generic.IReadOnlyList<Backends.FileDialogFilter> filters)
            => filters.Select (f => new Avalonia.Platform.Storage.FilePickerFileType (f.Name) {
                Patterns = f.Patterns.ToList ()
            }).ToArray ();

        private async System.Threading.Tasks.Task<Avalonia.Platform.Storage.IStorageFolder?> ResolveStartFolder (string? initialDirectory)
        {
            var top = TopLevel.GetTopLevel (this);
            return top is null || initialDirectory is null
                ? null
                : await top.StorageProvider.TryGetFolderFromPathAsync (new System.Uri (initialDirectory));
        }

        async System.Threading.Tasks.Task<string[]> IWindowBackend.ShowOpenFileDialog (Backends.OpenFileRequest request)
        {
            var top = TopLevel.GetTopLevel (this);
            if (top is null) return Array.Empty<string> ();

            var result = await top.StorageProvider.OpenFilePickerAsync (new Avalonia.Platform.Storage.FilePickerOpenOptions {
                AllowMultiple = request.AllowMultiple,
                SuggestedStartLocation = await ResolveStartFolder (request.InitialDirectory),
                Title = request.Title,
                FileTypeFilter = MapFilters (request.Filters)
            });
            return result.Select (f => f.GetFullPath ()).WhereNotNull ().ToArray ();
        }

        async System.Threading.Tasks.Task<string?> IWindowBackend.ShowSaveFileDialog (Backends.SaveFileRequest request)
        {
            var top = TopLevel.GetTopLevel (this);
            if (top is null) return null;

            var result = await top.StorageProvider.SaveFilePickerAsync (new Avalonia.Platform.Storage.FilePickerSaveOptions {
                DefaultExtension = request.DefaultExtension,
                SuggestedStartLocation = await ResolveStartFolder (request.InitialDirectory),
                SuggestedFileName = request.SuggestedFileName,
                Title = request.Title,
                FileTypeChoices = MapFilters (request.Filters)
            });
            return result?.GetFullPath ();
        }

        async System.Threading.Tasks.Task<string?> IWindowBackend.ShowOpenFolderDialog (Backends.FolderDialogRequest request)
        {
            var top = TopLevel.GetTopLevel (this);
            if (top is null) return null;

            var result = await top.StorageProvider.OpenFolderPickerAsync (new Avalonia.Platform.Storage.FolderPickerOpenOptions {
                AllowMultiple = false,
                SuggestedStartLocation = await ResolveStartFolder (request.InitialDirectory),
                Title = request.Title
            });
            return result.Select (f => f.GetFullPath ()).WhereNotNull ().FirstOrDefault ();
        }

        private static readonly System.Collections.Generic.Dictionary<CursorType, AvCursor> _cursorCache = new ();

        private static AvCursor MapCursor (CursorType cursor)
        {
            if (_cursorCache.TryGetValue (cursor, out var cached))
                return cached;

            var type = cursor switch {
                CursorType.Arrow => StandardCursorType.Arrow,
                CursorType.AppStarting => StandardCursorType.AppStarting,
                CursorType.Cross => StandardCursorType.Cross,
                CursorType.Hand => StandardCursorType.Hand,
                CursorType.Help => StandardCursorType.Help,
                CursorType.Ibeam => StandardCursorType.Ibeam,
                CursorType.No => StandardCursorType.No,
                CursorType.UpArrow => StandardCursorType.UpArrow,
                CursorType.Wait => StandardCursorType.Wait,
                CursorType.SizeAll => StandardCursorType.SizeAll,
                CursorType.SizeNorthSouth => StandardCursorType.SizeNorthSouth,
                CursorType.SizeWestEast => StandardCursorType.SizeWestEast,
                CursorType.TopSide => StandardCursorType.TopSide,
                CursorType.BottomSide => StandardCursorType.BottomSide,
                CursorType.LeftSide => StandardCursorType.LeftSide,
                CursorType.RightSide => StandardCursorType.RightSide,
                CursorType.TopLeftCorner => StandardCursorType.TopLeftCorner,
                CursorType.TopRightCorner => StandardCursorType.TopRightCorner,
                CursorType.BottomLeftCorner => StandardCursorType.BottomLeftCorner,
                CursorType.BottomRightCorner => StandardCursorType.BottomRightCorner,
                CursorType.DragCopy => StandardCursorType.DragCopy,
                CursorType.DragLink => StandardCursorType.DragLink,
                CursorType.DragMove => StandardCursorType.DragMove,
                _ => StandardCursorType.Arrow
            };

            var avCursor = new AvCursor (type);
            _cursorCache[cursor] = avCursor;
            return avCursor;
        }
    }
}
#endif
