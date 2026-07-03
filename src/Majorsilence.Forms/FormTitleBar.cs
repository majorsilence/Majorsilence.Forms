using System.Drawing;
using Majorsilence.Forms.Renderers;
using SkiaSharp;

namespace Majorsilence.Forms
{
    /// <summary>
    /// Represents a FormTitleBar control.
    /// </summary>
    public class FormTitleBar : Control
    {
        // Logical width reserved on the left for the native macOS traffic-light buttons when the
        // title bar is merged into the OS title bar (native-overlay mode).
        private const int MAC_TRAFFIC_LIGHT_INSET = 78;

        private readonly TitleBarButton minimize_button;
        private readonly TitleBarButton maximize_button;
        private readonly TitleBarButton close_button;
        private readonly PictureBox form_image;
        private Control? overlay_spacer;

        private bool show_image = true;
        private bool show_text = true;
        private bool native_overlay;

        /// <summary>
        /// Initializes a new instance of the FormTitleBar class.
        /// </summary>
        public FormTitleBar ()
        {
            Dock = DockStyle.Top;

            SetControlBehavior (ControlBehaviors.InvalidateOnTextChanged);
            SetControlBehavior (ControlBehaviors.Selectable, false);

            minimize_button = Controls.AddImplicitControl (new TitleBarButton (TitleBarButton.TitleBarButtonGlyph.Minimize));
            minimize_button.Click += (o, e) => {
                var form_min = FindForm ();

                if (form_min is not null)
                    form_min.WindowState = FormWindowState.Minimized;
            };

            maximize_button = Controls.AddImplicitControl (new TitleBarButton (TitleBarButton.TitleBarButtonGlyph.Maximize));
            maximize_button.Click += (o, e) => {
                var form = FindForm ();

                if (form is not null) {
                    form.WindowState = form.WindowState == FormWindowState.Maximized
                        ? FormWindowState.Normal
                        : FormWindowState.Maximized;

                    UpdateMaximizeButtonGlyph ();
                }
            };

            close_button = Controls.AddImplicitControl (new TitleBarButton (TitleBarButton.TitleBarButtonGlyph.Close));
            close_button.Click += (o, e) => { FindForm ()?.Close (); };

            form_image = Controls.AddImplicitControl (new PictureBox {
                Width = DefaultSize.Height,
                Dock = DockStyle.Left,
                Visible = false,
                SizeMode = PictureBoxSizeMode.CenterImage
            });

            // It's ok to hardcode SKColors.Transparent here.
            form_image.Style.BackgroundColor = SKColors.Transparent;
            form_image.SetControlBehavior (ControlBehaviors.ReceivesMouseEvents, false);
        }

        /// <summary>
        /// Whether the title bar is merged into a native OS title bar (macOS): the OS draws the caption
        /// buttons (traffic lights) and frame, while this control paints the title/background into the
        /// extended title-bar area. In this mode our own caption buttons are hidden and a left margin is
        /// reserved for the traffic lights.
        /// </summary>
        internal bool NativeOverlay {
            get => native_overlay;
            set {
                if (native_overlay == value)
                    return;

                native_overlay = value;
                ApplyNativeOverlay ();
            }
        }

        // Which side the (logical) caption buttons occupy. The reserved traffic-light inset is on the
        // left in native-overlay mode; our own buttons are on the right otherwise.
        internal bool CaptionButtonsOnLeft => native_overlay;

        private void ApplyNativeOverlay ()
        {
            // The OS draws the caption buttons in native-overlay mode, so hide ours and reserve room on
            // the left for the traffic lights. The strip blends with the window background rather than
            // using the accent color.
            minimize_button.Visible = !native_overlay;
            maximize_button.Visible = !native_overlay;
            close_button.Visible = !native_overlay;

            if (native_overlay && overlay_spacer is null) {
                overlay_spacer = Controls.AddImplicitControl (new Control { Dock = DockStyle.Left, Width = MAC_TRAFFIC_LIGHT_INSET });
                overlay_spacer.Style.BackgroundColor = SKColors.Transparent;
                overlay_spacer.SetControlBehavior (ControlBehaviors.ReceivesMouseEvents, false);
            }

            if (overlay_spacer is not null)
                overlay_spacer.Visible = native_overlay;

            Style.BackgroundColor = native_overlay ? Theme.BackgroundColor : DefaultStyle.GetBackgroundColor ();
            Invalidate ();
        }

        /// <inheritdoc/>
        protected internal override void OnThemeChanged (EventArgs e)
        {
            // The merged (native-overlay) title bar blends with the window background, which tracks the theme.
            if (native_overlay)
                Style.BackgroundColor = Theme.BackgroundColor;

            base.OnThemeChanged (e);
        }

        /// <summary>
        /// Gets or sets whenther the Maximize button is shown.
        /// </summary>
        public bool AllowMaximize {
            get => maximize_button.Visible;
            set {
                maximize_button.Visible = value && !native_overlay;
                UpdateMaximizeButtonGlyph ();
                Invalidate (); // TODO: Shouldn't be necessary, should automatically be triggered
            }
        }

        /// <summary>
        /// Gets or sets whether the Minimize button is shown.
        /// </summary>
        public bool AllowMinimize {
            get => minimize_button.Visible;
            set {
                minimize_button.Visible = value && !native_overlay;
                Invalidate (); // TODO: Shouldn't be necessary, should automatically be triggered
            }
        }

        // Total logical width of the caption-button cluster: in native-overlay mode the reserved
        // traffic-light inset on the left; otherwise close + optional maximize/minimize on the right.
        // The Form treats the rest of the title bar as a draggable caption region.
        internal int CaptionButtonsWidth =>
            native_overlay
                ? (overlay_spacer?.Width ?? 0)
                : (close_button.Visible ? close_button.Width : 0)
                    + (maximize_button.Visible ? maximize_button.Width : 0)
                    + (minimize_button.Visible ? minimize_button.Width : 0);

        // The preferred title-bar height, used to size the extended (merged) title-bar region.
        internal int PreferredHeight => DefaultSize.Height;

        /// <inheritdoc/>
        protected override Size DefaultSize => new Size (600, 34);

        /// <inheritdoc/>
        public new static ControlStyle DefaultStyle = new ControlStyle (Control.DefaultStyle,
           (style) => style.BackgroundColor = Theme.AccentColor2);

        /// <summary>
        /// Gets or sets the image used as the upper-left icon of the titlebar.
        /// </summary>
#pragma warning disable CA1416
        public Majorsilence.Forms.Drawing.Image? Image {
            get => form_image.Image;
            set {
                form_image.Image = value;
                form_image.Visible = form_image.Image is not null && show_image;
                Invalidate ();
            }
        }
#pragma warning restore CA1416

        internal SKBitmap? SKImage => form_image.SKImage;

        internal void SetSKImage (SKBitmap? bmp)
        {
            form_image.SetSKImage (bmp);
            form_image.Visible = form_image.SKImage is not null && show_image;
            Invalidate ();
        }

        /// <inheritdoc/>
        protected override void OnMouseDown (MouseEventArgs e)
        {
            base.OnMouseDown (e);

            // We won't get a MouseUp from the system for this, so don't capture the mouse
            Capture = false;
            FindForm ()?.BeginMoveDrag ();
        }

        /// <inheritdoc/>
        protected override void OnPaint (PaintEventArgs e)
        {
            base.OnPaint (e);

            RenderManager.Render (this, e);
        }

        /// <inheritdoc/>
        protected override void OnSizeChanged (EventArgs e)
        {
            base.OnSizeChanged (e);

            // Keep our form image a square
            form_image.Width = Height;

            UpdateMaximizeButtonGlyph ();
        }

        /// <summary>
        /// Specifies whether the Form's title text is painted in the title bar. Set to false when the
        /// title bar hosts its own content (e.g. a tab strip) that would otherwise collide with the text.
        /// </summary>
        public bool ShowText {
            get => show_text;
            set {
                if (show_text != value) {
                    show_text = value;
                    Invalidate ();
                }
            }
        }

        /// <summary>
        /// Specifies whether the Form's Image should be shown in the left corner.
        /// </summary>
        public bool ShowImage {
            get => show_image;
            set {
                if (show_image != value) {
                    show_image = value;
                    form_image.Visible = value && (form_image.Image is not null || form_image.SKImage is not null);
                    Invalidate (); // TODO: Shouldn't be required
                }
            }
        }

        /// <inheritdoc/>
        public override ControlStyle Style { get; } = new ControlStyle (DefaultStyle);

        private void UpdateMaximizeButtonGlyph ()
        {
            var form = FindForm ();

            if (form == null || !maximize_button.Visible) {
                maximize_button.Glyph = TitleBarButton.TitleBarButtonGlyph.Maximize;
                return;
            }

            maximize_button.Glyph = form.WindowState == FormWindowState.Maximized
                ? TitleBarButton.TitleBarButtonGlyph.Restore
                : TitleBarButton.TitleBarButtonGlyph.Maximize;
        }

        internal sealed class TitleBarButton : Button
        {
            private const int BUTTON_PADDING = 10;

            private TitleBarButtonGlyph glyph;

            /// <summary>
            /// Gets or sets the glyph displayed by the button.
            /// </summary>
            public TitleBarButtonGlyph Glyph {
                get => glyph;
                set {
                    if (glyph != value) {
                        glyph = value;
                        Invalidate ();
                    }
                }
            }

            public TitleBarButton (TitleBarButtonGlyph glyph)
            {
                this.glyph = glyph;
                Width = 46;
                Dock = DockStyle.Right;

                Style.BackgroundColor = SKColors.Transparent;
                Style.Border.Width = 0;
                StyleHover.Border.Width = 0;
            }

            protected override void OnPaint (PaintEventArgs e)
            {
                base.OnPaint (e);

                if (IsHovering)
                    e.Canvas.Clear (glyph == TitleBarButtonGlyph.Close ? Theme.WarningHighlightColor : Theme.AccentColor);

                var glyph_bounds = glyph == TitleBarButtonGlyph.Minimize ?
                    DrawingExtensions.CenterRectangle (ClientRectangle, e.LogicalToDeviceUnits (new Size (BUTTON_PADDING, 1))) :
                    DrawingExtensions.CenterSquare (ClientRectangle, e.LogicalToDeviceUnits (BUTTON_PADDING));

                switch (glyph) {
                    case TitleBarButtonGlyph.Close:
                        ControlPaint.DrawCloseGlyph (e, glyph_bounds);
                        break;
                    case TitleBarButtonGlyph.Minimize:
                        ControlPaint.DrawMinimizeGlyph (e, glyph_bounds);
                        break;
                    case TitleBarButtonGlyph.Maximize:
                        ControlPaint.DrawMaximizeGlyph (e, glyph_bounds);
                        break;
                    case TitleBarButtonGlyph.Restore:
                        ControlPaint.DrawRestoreGlyph (e, glyph_bounds);
                        break;
                }
            }

            /// <summary>
            /// Specifies which glyph is displayed by the title bar button.
            /// </summary>
            public enum TitleBarButtonGlyph
            {
                Close,
                Minimize,
                Maximize,
                Restore
            }
        }
    }
}
