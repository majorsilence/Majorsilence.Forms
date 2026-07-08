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
    public class PictureBox : Control, ISupportInitialize
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
            set => LoadInternal (value);
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
        /// Loads the image at the specified path or URL and sets ImageLocation to it.
        /// </summary>
        public void Load (string url)
        {
            if (string.IsNullOrWhiteSpace (url))
                throw new InvalidOperationException ("ImageLocation not specified.");

            ImageLocation = url;
        }

        // Load image from path or URL and display it.
        private async void LoadInternal (string? url)
        {
            if (image_location == url)
                return;

            if (url is null) {
                image_location = null;
                _systemImage = null;
                _skImage?.Dispose ();
                _skImage = null;
                Invalidate ();
                return;
            }

            IsErrored = false;
            image_location = url;

            try {
                SKBitmap? bmp;
                if (url.Contains ("://")) {
                    var bytes = await Client.GetByteArrayAsync (url);
                    bmp = SKBitmap.Decode (bytes);
                } else
                    bmp = SKBitmap.Decode (url);

                _systemImage = null;
                _skImage?.Dispose ();
                _skImage = bmp;
                UpdateSize ();
                Invalidate ();
            } catch (Exception) {
                IsErrored = true;
                Invalidate ();
            }
        }

        /// <summary>
        /// Gets or sets a value indicated the sizing mode used.
        /// </summary>
        public PictureBoxSizeMode SizeMode {
            get => size_mode;
            set {
                if (!Enum.IsDefined (value))
                    throw new InvalidEnumArgumentException (nameof (value), (int)value, typeof (PictureBoxSizeMode));

                if (size_mode != value) {
                    size_mode = value;
                    UpdateSize ();
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

        /// <summary>Loads the image from the specified URL asynchronously. Stub delegates to Load() in Majorsilence.Forms.</summary>
        public void LoadAsync (string url) => Load (url);

        /// <summary>Cancels a pending asynchronous image load. Stub in Majorsilence.Forms.</summary>
        public void CancelAsync () { }

        /// <summary>Raised when an asynchronous load completes. Stub in Majorsilence.Forms.</summary>
        public event EventHandler? LoadCompleted { add { } remove { } }

        /// <summary>Raised to report progress of an asynchronous load. Stub in Majorsilence.Forms.</summary>
        public event EventHandler? LoadProgressChanged { add { } remove { } }

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

            Parent?.PerformLayout (this, nameof (AutoSize));
        }
    }
}
#pragma warning restore CA1416
