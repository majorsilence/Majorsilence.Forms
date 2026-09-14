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
                _systemImage = value;
                _skImage?.Dispose ();
                _skImage = value?.ToSKBitmap ();
                IsErrored = false;
                UpdateSize ();
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

                LoadCore (value, rethrow: false);
            }
        }

        /// <summary>Sets the image from a <see cref="Majorsilence.Forms.Drawing.Bitmap"/>.</summary>
        public void SetImage (Majorsilence.Forms.Drawing.Bitmap bitmap) => Image = bitmap;

        /// <summary>Sets the image from a SKBitmap for Majorsilence.Forms usage.</summary>
        public void SetSKImage (SKBitmap? bitmap)
        {
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

        /// <summary>Gets or sets the border style of the PictureBox. Stub in Majorsilence.Forms.</summary>
        public PictureBoxBorderStyle PictureBoxBorderStyle { get; set; } = PictureBoxBorderStyle.None;

        /// <summary>Gets or sets the border style of the picture box (WinForms compatibility).</summary>
        public BorderStyle BorderStyle {
            get => (BorderStyle)(int)PictureBoxBorderStyle;
            set => PictureBoxBorderStyle = (PictureBoxBorderStyle)(int)value;
        }

        /// <summary>Gets or sets whether the PictureBox should wait for an image to load before displaying. Stub.</summary>
        public bool WaitOnLoad { get; set; } = true;

        /// <summary>Gets or sets the image shown when the primary image load fails. Stub in Majorsilence.Forms.</summary>
        public Majorsilence.Forms.Drawing.Image? ErrorImage { get; set; }

        /// <summary>Gets or sets the image shown while the primary image is loading. Stub in Majorsilence.Forms.</summary>
        public Majorsilence.Forms.Drawing.Image? InitialImage { get; set; }

        /// <inheritdoc/>
        protected override void OnPaint (PaintEventArgs e)
        {
            base.OnPaint (e);

            RenderManager.Render (this, e);
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
                Size = new Size (_skImage.Width, _skImage.Height);

            Parent?.PerformLayout (this, nameof (AutoSize));
        }

        /// <inheritdoc/>
        public override Size GetPreferredSize (Size proposedSize)
            => size_mode == PictureBoxSizeMode.AutoSize && _skImage is not null
                ? new Size (_skImage.Width, _skImage.Height)
                : base.GetPreferredSize (proposedSize);
    }
}
#pragma warning restore CA1416
