using Majorsilence.Forms.Backends;

namespace Majorsilence.Forms
{
    /// <summary>
    /// A chromeless Majorsilence.Forms root that is hosted inside another UI toolkit's element
    /// (e.g. an Avalonia control or an Uno element) instead of owning a top-level OS window.
    ///
    /// The hosting toolkit supplies an <see cref="IWindowBackend"/> implementation (its presenter
    /// control) that provides the Skia surface, scaling and coordinate mapping. Everything above the
    /// backend seam — layout, painting, input routing, popups, modal dialogs — is reused unchanged
    /// from <see cref="WindowBase"/>.
    ///
    /// Unlike <see cref="Form"/> this type draws no title bar and (by default) no border, so the
    /// embedded scene is just the client content. Drop a single root <see cref="Content"/> control
    /// (typically a Panel or UserControl) into it, or add multiple controls via <see cref="WindowBase.Controls"/>.
    /// </summary>
    public class HostedSurface : WindowBase
    {
        private Control? content;

        /// <summary>
        /// Initializes a new instance of the <see cref="HostedSurface"/> class bound to a backend
        /// supplied by the hosting toolkit's presenter control.
        /// </summary>
        public HostedSurface (IWindowBackend backend)
        {
            ArgumentNullException.ThrowIfNull (backend);

            InitWindow (backend);

            // The host owns chrome and visibility; the surface is "shown" for the lifetime of the
            // presenter so child controls receive the expected visible/shown notifications.
            IsHosted = true;
            StartPosition = FormStartPosition.Manual;

            // A HostedSurface is not a Form and is not in Application.OpenForms, so the global theme
            // change broadcast never reaches it. Register so Application.DoThemeChanged also notifies it —
            // and crucially does so *after* the Theme.ThemeChanged broadcast has refreshed every
            // ControlStyle's cached colors, so a synchronous repaint (Uno) picks up the new colors.
            Application.EmbeddedSurfaces.Add (this);

            Show ();
        }

        /// <summary>Called when the application theme changes; re-themes and repaints the embedded scene.</summary>
        protected internal virtual void OnThemeChanged (EventArgs e)
        {
            foreach (var control in Controls.GetAllControls ())
                control.OnThemeChanged (e);

            Invalidate ();
        }

        /// <inheritdoc/>
        protected override void Dispose (bool disposing)
        {
            if (disposing)
                Application.EmbeddedSurfaces.Remove (this);

            base.Dispose (disposing);
        }

        /// <inheritdoc/>
        protected override System.Drawing.Size DefaultSize => new System.Drawing.Size (100, 100);

        /// <summary>Gets the default style for all hosted surfaces.</summary>
        public new static ControlStyle DefaultStyle = new ControlStyle (Control.DefaultStyle,
            (style) => {
                // Transparent by default so the host's own background shows through; callers can set a
                // solid colour (e.g. Theme.BackgroundColor) on the instance Style for an opaque panel.
                style.BackgroundColor = SkiaSharp.SKColors.Transparent;
            });

        /// <summary>Gets the ControlStyle properties for this instance of the surface.</summary>
        public override ControlStyle Style { get; } = new ControlStyle (DefaultStyle);

        /// <summary>
        /// Gets or sets the single root control hosted by this surface. Setting this replaces any
        /// existing content and docks the new control to fill the surface. For multiple roots, use
        /// <see cref="WindowBase.Controls"/> directly instead.
        /// </summary>
        public Control? Content {
            get => content;
            set {
                if (content == value)
                    return;

                if (content is not null)
                    Controls.Remove (content);

                content = value;

                if (content is not null) {
                    content.Dock = DockStyle.Fill;
                    Controls.Add (content);
                }

                Invalidate ();
            }
        }
    }
}
