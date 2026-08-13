using System.Collections.Specialized;
using System.Drawing;
using Majorsilence.Forms.Layout;
using Majorsilence.Forms.Renderers;
using SkiaSharp;

namespace Majorsilence.Forms
{
    // TODO:
    // AutoEllipsis

    /// <summary>
    /// Represents a Label control.
    /// </summary>
    public partial class Label : Control, IHaveTextAndImageAlign
    {
        private static readonly object s_eventTextAlignChanged = new ();

        private static readonly BitVector32.Section s_stateUseMnemonic = BitVector32.CreateSection (1);
        private static readonly BitVector32.Section s_stateAutoSize = BitVector32.CreateSection (1, s_stateUseMnemonic);
        private static readonly BitVector32.Section s_stateAutoEllipsis = BitVector32.CreateSection (1, s_stateAutoSize);
        private static readonly BitVector32.Section s_stateMultiline = BitVector32.CreateSection (1, s_stateAutoEllipsis);

        private static readonly int s_propImage = PropertyStore.CreateKey ();
        private static readonly int s_propImageSK = PropertyStore.CreateKey ();
        private static readonly int s_propImageAlign = PropertyStore.CreateKey ();
        private static readonly int s_propImageList = PropertyStore.CreateKey ();
        private static readonly int s_propImageIndex = PropertyStore.CreateKey ();
        private static readonly int s_propImageKey = PropertyStore.CreateKey ();
        private static readonly int s_propTextAlign = PropertyStore.CreateKey ();
        private static readonly int s_propTextImageRelation = PropertyStore.CreateKey ();

        private BitVector32 _labelState;

        /// <summary>
        /// Initializes a new instance of the Label class.
        /// </summary>
        public Label ()
        {
            SetControlBehavior (ControlBehaviors.InvalidateOnTextChanged);
            SetControlBehavior (ControlBehaviors.Selectable, false);

            _labelState[s_stateUseMnemonic] = 1;

            TabStop = false;
        }

        /// <summary>
        /// Gets or sets a value indicating if text will be truncated with an ellipsis if it cannot fully fit in the Label.
        /// </summary>
        public bool AutoEllipsis {
            get => _labelState[s_stateAutoEllipsis] != 0;
            set {
                if (AutoEllipsis != value) {

                    _labelState[s_stateAutoEllipsis] = value ? 1 : 0;

                    if (Parent is not null)
                        LayoutTransaction.DoLayoutIf (AutoSize, Parent, this, PropertyNames.AutoEllipsis);

                    Invalidate ();
                }
            }
        }

        // GDI label text carries a ~2px bearing inset on each side, which LabelRenderer reproduces.
        // The preferred width has to leave room for it or an auto-sized label clips its own last glyph.
        private const int TextBearingInset = 2;

        /// <summary>
        /// Measures the size this label wants to be: its text under the font it will actually be drawn
        /// with, plus padding and the bearing inset, unioned with any image.
        /// </summary>
        internal override Size GetPreferredSizeCore (Size proposedSize)
        {
            var text = Text;
            var image = ImageSK;

            // Nothing to measure -- keep whatever size was set rather than collapsing to nothing.
            if (!text.HasValue () && image is null)
                return base.GetPreferredSizeCore (proposedSize);

            var width = 0;
            var height = 0;

            if (text.HasValue ()) {
                // Measured with the ambient font (GetEffectiveFont), which is the font the renderer
                // draws with -- measuring with a different one is how a label ends up sized for text
                // it isn't showing. Logical units throughout, to match Bounds.
                var constraint = proposedSize.Width > 0 && proposedSize.Width < int.MaxValue
                    ? new Size (proposedSize.Width, int.MaxValue)
                    : TextMeasurer.MaxSize;

                var measured = TextMeasurer.MeasureText (text, GetEffectiveFont (), GetEffectiveFontSize (), constraint);

                width = (int) Math.Ceiling (measured.Width) + TextBearingInset * 2;
                height = (int) Math.Ceiling (measured.Height);
            }

            if (image is not null) {
                width = Math.Max (width, image.Width);
                height = Math.Max (height, image.Height);
            }

            return new Size (width + Padding.Horizontal, height + Padding.Vertical);
        }

        /// <summary>
        /// Resizes the label to its preferred size when <see cref="AutoSize"/> is on.
        /// </summary>
        /// <remarks>
        /// WinForms' Label does this itself rather than leaving it to the layout engine, and the
        /// difference matters: the engine's auto-size pass only ever *grows* an element, so a label
        /// carrying a designer size larger than its text would keep it forever. A designer emits
        /// AutoSize = true followed by the size the label happened to have at design time, so that is
        /// the normal case, not an edge one -- and an over-wide label silently swallows the mouse
        /// events of whatever it is sitting on.
        /// </remarks>
        private void AdjustSize ()
        {
            if (!AutoSize)
                return;

            var preferred = GetPreferredSize (Size.Empty);

            if (preferred != Size && preferred.Width > 0 && preferred.Height > 0)
                Size = preferred;
        }

        /// <inheritdoc/>
        public override bool AutoSize {
            get => base.AutoSize;
            set {
                if (base.AutoSize == value)
                    return;

                base.AutoSize = value;
                AdjustSize ();
            }
        }

        /// <inheritdoc/>
        protected override void OnTextChanged (EventArgs e)
        {
            base.OnTextChanged (e);
            AdjustSize ();
        }

        /// <inheritdoc/>
        protected override void OnFontChanged (EventArgs e)
        {
            base.OnFontChanged (e);
            AdjustSize ();
        }

        /// <inheritdoc/>
        protected override void OnPaddingChanged (EventArgs e)
        {
            base.OnPaddingChanged (e);
            AdjustSize ();
        }

        // Until the label has a parent it has no ambient font to measure against, so a designer that
        // configures the label before adding it would measure with the wrong one. Re-measure on attach.
        /// <inheritdoc/>
        protected override void OnParentChanged (EventArgs e)
        {
            base.OnParentChanged (e);
            AdjustSize ();
        }

        /// <inheritdoc/>
        protected override Padding DefaultMargin => new Padding (3, 0, 3, 0);

        /// <inheritdoc/>
        protected override Size DefaultSize => new Size (100, 23);

        /// <inheritdoc/>
        public new static readonly ControlStyle DefaultStyle = new ControlStyle (Control.DefaultStyle);

        /// <summary>
        /// Gets or sets the image that is displayed on a <see cref='Label'/>.
        /// </summary>
#pragma warning disable CA1416
        public Majorsilence.Forms.Drawing.Image? Image {
            get => Properties.GetObject<Majorsilence.Forms.Drawing.Image> (s_propImage);
            set {
                if (Image != value) {
                    Properties.SetObject (s_propImage, value);
                    Properties.SetObject (s_propImageSK, value?.ToSKBitmap ());
                    Invalidate ();
                }
            }
        }
#pragma warning restore CA1416

        /// <summary>Gets the SKBitmap representation of the image (used by renderers).</summary>
        public SKBitmap? ImageSK => Properties.GetObject<SKBitmap> (s_propImageSK);

        /// <summary>
        /// Gets or sets the alignment of the image on the <see cref='Label'/>.
        /// </summary>
        public ContentAlignment ImageAlign {
            get => Properties.GetEnum (s_propImageAlign, ContentAlignment.MiddleCenter);
            set {
                SourceGenerated.EnumValidator.Validate (value);

                if (value != ImageAlign) {
                    Properties.SetEnum (s_propImageAlign, value);
                    LayoutTransaction.DoLayoutIf (AutoSize, Parent, this, PropertyNames.ImageAlign);
                    Invalidate ();
                }
            }
        }

        /// <summary>
        /// Gets or sets the index of the image in the <see cref='ImageList'/> to display on the <see cref='CheckBox'/>.
        /// </summary>
        public int ImageIndex {
            get => Properties.GetInteger (s_propImageIndex, -1);
            set {
                if (ImageIndex != value) {
                    Properties.SetInteger (s_propImageIndex, value);

                    // Setting this clears any existing ImageKey and Image
                    if (value >= 0) {
                        Properties.RemoveObject (s_propImage);
                        Properties.RemoveObject (s_propImageKey);
                    }

                    Invalidate ();
                }
            }
        }

        /// <summary>
        /// Gets or sets the key of the image in the <see cref='ImageList'/> to display on the <see cref='CheckBox'/>.
        /// </summary>
        public string ImageKey {
            get => Properties.GetObject<string> (s_propImageKey) ?? string.Empty;
            set {
                if (ImageKey != value) {
                    Properties.SetObject (s_propImageKey, value);

                    // Setting this clears any existing ImageIndex and Image
                    if (value is not null) {
                        Properties.RemoveObject (s_propImage);
                        Properties.RemoveInteger (s_propImageIndex);
                    }

                    Invalidate ();
                }
            }
        }

        /// <summary>
        /// Gets or sets the <see cref='ImageList'/> that contains the image to display on the <see cref='CheckBox'/>.
        /// </summary>
        public ImageList? ImageList {
            get => Properties.GetObject<ImageList> (s_propImageList);
            set {
                if (ImageList != value) {
                    Properties.SetObject (s_propImageList, value);

                    // If an image list is set, clear any existing image
                    if (value is not null)
                        Properties.RemoveObject (s_propImage);

                    Invalidate ();
                }
            }
        }

        /// <summary>
        /// Gets or sets a value indicating if text should wrap.
        /// </summary>
        public bool Multiline {
            get => _labelState[s_stateMultiline] != 0;
            set {
                if (Multiline != value) {
                    _labelState[s_stateMultiline] = value ? 1 : 0;

                    if (Parent is not null)
                        LayoutTransaction.DoLayoutIf (AutoSize, Parent, this, PropertyNames.Multiline);

                    Invalidate ();
                }
            }
        }

        /// <inheritdoc/>
        protected override void OnPaint (PaintEventArgs e)
        {
            base.OnPaint (e);

            RenderManager.Render (this, e);
        }

        /// <inheritdoc/>
        public override ControlStyle Style { get; } = new ControlStyle (DefaultStyle);

        /// <summary>
        /// Gets or sets a value indicating how text will be aligned within the Label.
        /// </summary>
        public ContentAlignment TextAlign {
            get => Properties.GetEnum (s_propTextAlign, ContentAlignment.TopLeft);
            set {
                SourceGenerated.EnumValidator.Validate (value);

                if (TextAlign != value) {
                    Properties.SetEnum (s_propTextAlign, value);
                    Invalidate ();

                    OnTextAlignChanged (EventArgs.Empty);
                }
            }
        }

        /// <summary>
        /// Raised when the TextAlign property is changed.
        /// </summary>
        public event EventHandler? TextAlignChanged {
            add => Events.AddHandler (s_eventTextAlignChanged, value);
            remove => Events.RemoveHandler (s_eventTextAlignChanged, value);
        }

        /// <summary>
        /// Gets or sets a value indicating how the Label's Image and Text are layed out relative to each other.
        /// </summary>
        public TextImageRelation TextImageRelation {
            get => Properties.GetEnum (s_propTextImageRelation, TextImageRelation.Overlay);
            set {
                SourceGenerated.EnumValidator.Validate (value);

                if (TextImageRelation != value) {
                    Properties.SetEnum (s_propTextImageRelation, value);
                    LayoutTransaction.DoLayoutIf (AutoSize, Parent, this, PropertyNames.TextImageRelation);
                    Invalidate ();
                }
            }
        }

        /// <summary>
        ///  Gets or sets a value indicating whether an ampersand (&amp;) included in the text of the control.
        /// </summary>
        public bool UseMnemonic {
            get => _labelState[s_stateUseMnemonic] != 0;
            set {
                if (UseMnemonic == value)
                    return;

                _labelState[s_stateUseMnemonic] = value ? 1 : 0;

                // The size of the label need to be adjusted when the Mnemonic is set irrespective of auto-sizing.
                using (LayoutTransaction.CreateTransactionIf (AutoSize, Parent, this, PropertyNames.Text)) {
                    //AdjustSize ();
                    Invalidate ();
                }
            }
        }

        /// <inheritdoc/>
        protected override void Dispose (bool disposing)
        {
            if (disposing && Image is not null)
                Properties.SetObject (s_propImage, null);

            base.Dispose (disposing);
        }

        /// <summary>
        /// Called when the TextAlign property is changed.
        /// </summary>
        protected virtual void OnTextAlignChanged (EventArgs e) => (Events[s_eventTextAlignChanged] as EventHandler)?.Invoke (this, e);

        /// <summary>Gets or sets the border style for the label. Stub in Majorsilence.Forms (does not render borders).</summary>
        public BorderStyle BorderStyle { get; set; } = BorderStyle.None;

        /// <summary>Gets or sets the flat style for the label. Stub in Majorsilence.Forms.</summary>
        public FlatStyle FlatStyle { get; set; } = FlatStyle.Standard;
    }
}
