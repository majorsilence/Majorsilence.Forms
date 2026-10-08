using System.ComponentModel;
using System.Drawing;
using Majorsilence.Forms.Renderers;
using SkiaSharp;

#pragma warning disable CA1416

namespace Majorsilence.Forms
{
    /// <summary>
    /// Represents a PictureBox control.
    /// </summary>
    public partial class PictureBox : Control, ISupportInitialize
    {
        private static HttpClient? client;

        private Majorsilence.Forms.Drawing.Image? _systemImage;
        private SKBitmap? _skImage;
        private string? image_location;
        private PictureBoxSizeMode size_mode;

        /// <summary>
        /// Initializes a new instance of the PictureBox class.
        /// </summary>
        public PictureBox ()
        {
            SetControlBehavior (ControlBehaviors.Selectable, false);
        }

        // Lazily initialize and cache an HttpClient if needed.
        private static HttpClient Client => client ??= new HttpClient ();

        /// <inheritdoc/>
        protected override Size DefaultSize => new Size (100, 50);

        /// <summary>
        /// The default <see cref="ControlStyle"/> for all <see cref="PictureBox"/> instances. Gives the type
        /// its own layer in the style chain so a CSS theme rule (<c>PictureBox { ... }</c>) can target it.
        /// </summary>
        public new static readonly ControlStyle DefaultStyle = new ControlStyle (Control.DefaultStyle);

        /// <inheritdoc/>
        public override ControlStyle Style { get; } = new ControlStyle (DefaultStyle);

        // WinForms designer-generated InitializeComponent code always brackets a PictureBox's
        // property assignments with ((ISupportInitialize)(this.pictureBox1)).BeginInit()/EndInit()
        // -- explicit no-op implementations (matching NumericUpDown/DataGridView's own) so that
        // cast succeeds instead of throwing InvalidCastException.
        void ISupportInitialize.BeginInit () { }
        void ISupportInitialize.EndInit () { }

        /// <summary>
        /// Gets or sets the image the PictureBox should display.
        /// Accepts <see cref="Majorsilence.Forms.Drawing.Image"/> for WinForms compatibility.
        /// </summary>
        public Majorsilence.Forms.Drawing.Image? Image {
            get => _systemImage;
            set {
                // Upstream InstallNewImage: stop the old image's animation, start the new one's.
                StopAnimate ();
                _systemImage = value;
                _skImage?.Dispose ();
                _skImage = value?.ToSKBitmap ();
                IsErrored = false;
                UpdateSize ();
                Animate ();
                Invalidate ();
            }
        }

        /// <summary>
        /// Gets the SKBitmap representation of the current image (for renderer use).
        /// </summary>
        internal SKBitmap? SKImage => _skImage;

        /// <summary>
        /// Gets or sets the path or URL of the image the PictureBox should display.
        /// </summary>
        public string? ImageLocation {
            get => image_location;
            // Synchronously, as Load does (SMP-20): the async void this used to call returned before
            // any bytes were read, so the Image was still null when the next line of the caller ran.
            // A setter has no way to report a failure, so this records IsErrored where Load throws.
            set {
                if (image_location == value && (value is null || _skImage is not null))
                    return;

                // WaitOnLoad cleared means the assignment starts a background load and returns at
                // once, which is what upstream does (W6 mechanisms).
                if (!WaitOnLoad && !string.IsNullOrWhiteSpace (value)) {
                    LoadAsync (value!);
                    return;
                }

                LoadCore (value, rethrow: false);
            }
        }

        /// <summary>Sets the image from a <see cref="Majorsilence.Forms.Drawing.Bitmap"/>.</summary>
        public void SetImage (Majorsilence.Forms.Drawing.Bitmap bitmap) => Image = bitmap;

        /// <summary>Sets the image from a SKBitmap for Majorsilence.Forms usage.</summary>
        public void SetSKImage (SKBitmap? bitmap)
        {
            StopAnimate ();
            _systemImage = null;
            _skImage?.Dispose ();
            _skImage = bitmap;
            IsErrored = false;
            UpdateSize ();
            Invalidate ();
        }

        /// <summary>
        /// Gets a value indicating the requested image could not be loaded.
        /// </summary>
        public bool IsErrored { get; private set; }

        /// <summary>
        /// Gets or sets a value indicated the sizing mode used.
        /// </summary>
        public PictureBoxSizeMode SizeMode {
            get => size_mode;
            set {
                if (!EnumCompat.IsDefined (value))
                    throw new InvalidEnumArgumentException (nameof (value), (int)value, typeof (PictureBoxSizeMode));

                if (size_mode != value) {
                    size_mode = value;

                    // SMP-23: every arm of PictureBoxRenderer draws the image somewhere different, so
                    // changing the mode changes the picture even when it changes no bound -- and
                    // UpdateSize only ever resized under AutoSize. Without this the old painting stayed
                    // on screen until something else invalidated: the "click Fit and nothing happens" bug.
                    // AutoSize follows the mode as upstream does (PictureBox.cs:821-847), so a layout
                    // container that consults AutoSize measures the box the way the mode implies.
                    AutoSize = value == PictureBoxSizeMode.AutoSize;
                    UpdateSize ();
                    Invalidate ();
                    OnSizeModeChanged (EventArgs.Empty);
                }
            }
        }

        /// <summary>
        /// Raised when the value of the SizeMode property changes.
        /// </summary>
        public event EventHandler? SizeModeChanged;

        private PictureBoxBorderStyle border_style = PictureBoxBorderStyle.None;

        /// <summary>Gets or sets the border style of the PictureBox.</summary>
        /// <remarks>
        /// SMP-24: this was stored and read by nothing, so the near-universal designer setting for an
        /// image placeholder drew no frame. Mapped onto the instance style's border, as
        /// <see cref="Panel.BorderStyle"/> does, it both draws the frame and deflates the client area
        /// the image is laid out in -- upstream's <c>WS_BORDER</c>/<c>WS_EX_CLIENTEDGE</c>, 1px for
        /// FixedSingle and 2px for Fixed3D. None clears the override so a CSS rule still decides.
        /// </remarks>
        public PictureBoxBorderStyle PictureBoxBorderStyle {
            get => border_style;
            set {
                // Upstream PictureBox.BorderStyle validates the enum (Controls/PictureBox/PictureBox.cs).
                if (!EnumCompat.IsDefined (value))
                    throw new InvalidEnumArgumentException (nameof (value), (int)value, typeof (BorderStyle));

                if (border_style == value)
                    return;

                border_style = value;
                Style.Border.Width = value switch {
                    PictureBoxBorderStyle.FixedSingle => 1,
                    PictureBoxBorderStyle.Fixed3D => 2,
                    _ => null,
                };

                // The border is part of an auto-sized box's size (upstream AdjustSize).
                UpdateSize ();
                Invalidate ();
            }
        }

        /// <summary>Gets or sets the border style of the picture box (WinForms compatibility).</summary>
        public BorderStyle BorderStyle {
            get => (BorderStyle)(int)PictureBoxBorderStyle;
            set => PictureBoxBorderStyle = (PictureBoxBorderStyle)(int)value;
        }

        /// <summary>Gets or sets whether a load blocks until the image is decoded.</summary>
        /// <remarks>
        /// Read as of W6 mechanisms: cleared, assigning <see cref="ImageLocation"/> or calling
        /// <c>Load</c> starts a background load and returns at once, completing through
        /// <c>LoadCompleted</c>. Deviation: upstream defaults this to <c>false</c>. It defaults to
        /// <c>true</c> here because the async path used to return before any bytes were read, leaving
        /// <c>Image</c> null on the next line of the caller (SMP-20); an application that wants the
        /// upstream default sets it.
        /// </remarks>
        public bool WaitOnLoad { get; set; } = true;

        /// <summary>Gets or sets the image shown when a load fails.</summary>
        /// <remarks>Read as of W6 mechanisms, on both the synchronous and the background paths; with
        /// none set a failed load leaves the box empty, as it always did.</remarks>
        public Majorsilence.Forms.Drawing.Image? ErrorImage { get; set; }

        /// <summary>Gets or sets the image shown while the primary image is loading. Stub in Majorsilence.Forms.</summary>
        public Majorsilence.Forms.Drawing.Image? InitialImage { get; set; }

        /// <inheritdoc/>
        protected override void OnPaint (PaintEventArgs e)
        {
            base.OnPaint (e);

            // Upstream OnPaint: Animate, then UpdateFrames to select the frame the animator moved to.
            // The renderer draws a copy of the image's pixels, so a new frame means a fresh copy.
            if (_systemImage is not null) {
                Animate ();
                if (Majorsilence.Forms.Drawing.ImageAnimator.ApplyPendingFrame (_systemImage)) {
                    _skImage?.Dispose ();
                    _skImage = _systemImage.ToSKBitmap ();
                }
            }

            RenderManager.Render (this, e);
        }

        // GFX-36: an animated GIF in a PictureBox showed its first frame and stopped, because nothing
        // started the animator. Upstream PictureBox animates while it is visible, enabled and parented
        // (Controls/PictureBox/PictureBox.cs, Animate) and repaints on each frame change.
        private bool _currentlyAnimating;

        private void Animate () => Animate (!DesignMode && Visible && Enabled && Parent is not null && !Disposing && !IsDisposed);

        private void StopAnimate () => Animate (false);

        private void Animate (bool animate)
        {
            if (animate == _currentlyAnimating || _systemImage is null)
                return;

            if (animate) {
                // Frame changes are delivered on the UI thread from here on, so OnFrameChanged can
                // invalidate directly; upstream raises them on its animation thread and BeginInvokes.
                Majorsilence.Forms.Drawing.ImageAnimator.UIThreadDispatcher ??= Application.RunOnUIThread;
                Majorsilence.Forms.Drawing.ImageAnimator.Animate (_systemImage, OnFrameChanged);
            } else {
                Majorsilence.Forms.Drawing.ImageAnimator.StopAnimate (_systemImage, OnFrameChanged);
            }
            _currentlyAnimating = animate;
        }

        private void OnFrameChanged (object? sender, EventArgs e)
        {
            if (IsDisposed)
                return;

            // Upstream stops through Dispose when the form disposes its controls. Disposing a window
            // here does not dispose its controls (WindowBase.Dispose), so a box left on a disposed form
            // would animate for ever; the first frame change after the window has gone stops it.
            if (FindForm () is { IsDisposed: true }) {
                StopAnimate ();
                return;
            }

            if (InvokeRequired) {
                BeginInvoke (new EventHandler (OnFrameChanged), sender, e);
                return;
            }

            Invalidate ();
        }

        /// <inheritdoc/>
        protected override void OnVisibleChanged (EventArgs e)
        {
            base.OnVisibleChanged (e);
            Animate ();
        }

        /// <inheritdoc/>
        protected override void OnEnabledChanged (EventArgs e)
        {
            base.OnEnabledChanged (e);
            Animate ();
        }

        /// <inheritdoc/>
        protected override void OnParentChanged (EventArgs e)
        {
            base.OnParentChanged (e);
            Animate ();
        }

        /// <inheritdoc/>
        protected override void Dispose (bool disposing)
        {
            if (disposing)
                StopAnimate ();

            base.Dispose (disposing);
        }

        /// <summary>
        /// Raises the SizeModeChanged event.
        /// </summary>
        protected void OnSizeModeChanged (EventArgs e) => SizeModeChanged?.Invoke (this, e);

        // Trigger a resizing.
        private void UpdateSize ()
        {
            if (_skImage == null)
                return;

            // WinForms' PictureBox sizes ITSELF to the image under SizeMode.AutoSize -- it does not wait
            // for a parent layout pass, and it does not require Control.AutoSize to be set. Asking the
            // parent to lay out was a no-op for a control that is neither docked nor anchored, so the box
            // sat at its default 100x50 however big the image was.
            //
            // A docking library's drop guides are built exactly this way: a PictureBox per guide, sized
            // by its own artwork. Stuck at the default, the guides were hit-tested against a box smaller
            // than the cluster drawn on screen, and the hot-spot lookup indexed the artwork at
            // coordinates that did not correspond to it -- so dropping on a lobe mostly missed.
            if (size_mode == PictureBoxSizeMode.AutoSize)
                Size = AutoSizeFor (_skImage);

            Parent?.PerformLayout (this, nameof (AutoSize));
        }

        // Upstream PictureBox.GetPreferredSizeCore: the image plus SizeFromClientSize (Size.Empty) plus
        // Padding, so an auto-sized box grows to keep its border and padding outside the image instead
        // of clipping the image's right and bottom edges by them (SMP-25).
        private Size AutoSizeFor (SKBitmap image)
        {
            var border = CurrentStyle.Border;

            return new Size (
                image.Width + Padding.Horizontal + border.Left.GetWidth () + border.Right.GetWidth (),
                image.Height + Padding.Vertical + border.Top.GetWidth () + border.Bottom.GetWidth ());
        }

        /// <inheritdoc/>
        protected override void OnPaddingChanged (EventArgs e)
        {
            base.OnPaddingChanged (e);

            UpdateSize ();
            Invalidate ();
        }

        /// <inheritdoc/>
        public override Size GetPreferredSize (Size proposedSize)
            => size_mode == PictureBoxSizeMode.AutoSize && _skImage is not null
                ? AutoSizeFor (_skImage)
                : base.GetPreferredSize (proposedSize);
    }
}
#pragma warning restore CA1416
