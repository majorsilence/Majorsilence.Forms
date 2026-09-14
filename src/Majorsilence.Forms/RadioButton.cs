using System.Collections.Specialized;
using System.Drawing;
using Majorsilence.Forms.Layout;
using Majorsilence.Forms.Renderers;
using SkiaSharp;

namespace Majorsilence.Forms
{
    /// <summary>
    /// Represents a RadioButton control.
    /// </summary>
    public partial class RadioButton : ButtonBase, IHaveGlyph, IHaveTextAndImageAlign
    {
        private static readonly BitVector32.Section s_stateAutoEllipsis = BitVector32.CreateSection (1);
        private static readonly BitVector32.Section s_stateChecked = BitVector32.CreateSection (1, s_stateAutoEllipsis);

        private static readonly int s_propGlyphAlign = PropertyStore.CreateKey ();
        private static readonly int s_propImage = PropertyStore.CreateKey ();
        private static readonly int s_propImageSK = PropertyStore.CreateKey ();
        private static readonly int s_propImageAlign = PropertyStore.CreateKey ();
        private static readonly int s_propImageList = PropertyStore.CreateKey ();
        private static readonly int s_propImageIndex = PropertyStore.CreateKey ();
        private static readonly int s_propImageKey = PropertyStore.CreateKey ();
        private static readonly int s_propTextAlign = PropertyStore.CreateKey ();
        private static readonly int s_propTextImageRelation = PropertyStore.CreateKey ();

        private BitVector32 _radiobuttonState;

        /// <summary>
        /// Initializes a new instance of the RadioButton class.
        /// </summary>
        public RadioButton ()
        {
            SetControlBehavior (ControlBehaviors.InvalidateOnTextChanged);

            // SMP-02: upstream's ctor does this (Controls/Buttons/RadioButton.cs:48). A radio button is
            // not a tab stop in its own right; PerformAutoUpdates grants one to the checked member of
            // the group.
            TabStop = false;
        }

        // Backing store for AutoCheck, whose setter lives in RadioButton.Group.cs because it drives
        // the same group bookkeeping the Checked setter does.
        private bool auto_check = true;

        /// <summary>
        /// Gets or sets a value indicating if text will be truncated with an ellipsis if it cannot fully fit in the <see cref='RadioButton'/>.
        /// </summary>
        public override bool AutoEllipsis {
            get => _radiobuttonState[s_stateAutoEllipsis] != 0;
            set {
                if (AutoEllipsis != value) {

                    _radiobuttonState[s_stateAutoEllipsis] = value ? 1 : 0;

                    if (Parent is not null)
                        LayoutTransaction.DoLayoutIf (AutoSize, Parent, this, PropertyNames.AutoEllipsis);

                    Invalidate ();
                }
            }
        }

        /// <summary>
        ///  Allows the control to optionally shrink when <see cref="Control.AutoSize"/> is <see langword="true"/>.
        /// </summary>
        public AutoSizeMode AutoSizeMode {
            get => GetAutoSizeMode ();
            set {
                SourceGenerated.EnumValidator.Validate (value);

                if (GetAutoSizeMode () != value) {
                    SetAutoSizeMode (value);
                    if (Parent is not null) {
                        // DefaultLayout does not keep anchor information until it needs to. When
                        // AutoSize became a common property, we could no longer blindly call into
                        // DefaultLayout, so now we do a special InitLayout just for DefaultLayout.
                        if (Parent.LayoutEngine == DefaultLayout.Instance)
                            Parent.LayoutEngine.InitLayout (this, BoundsSpecified.Size);

                        LayoutTransaction.DoLayout (Parent, this, PropertyNames.AutoSize);
                    }
                }
            }
        }

        /// <summary>
        /// Gets or sets the alignment of the radio button glyph on the <see cref='RadioButton'/>.
        /// </summary>
        public ContentAlignment GlyphAlign {
            get => Properties.GetEnum (s_propGlyphAlign, ContentAlignment.MiddleLeft);
            set {
                SourceGenerated.EnumValidator.Validate (value);

                if (value != GlyphAlign) {
                    Properties.SetEnum (s_propGlyphAlign, value);
                    LayoutTransaction.DoLayoutIf (AutoSize, Parent, this, PropertyNames.GlyphAlign);
                    Invalidate ();
                }
            }
        }

        /// <summary>Gets or sets the alignment of the radio button glyph (WinForms compat alias for GlyphAlign).</summary>
        public ContentAlignment CheckAlign {
            get => GlyphAlign;
            set => GlyphAlign = value;
        }

        /// <summary>
        /// Gets or sets a value indicating if the RadioButton is in the checked state.
        /// </summary>
        public bool Checked {
            get => _radiobuttonState[s_stateChecked] != 0;
            set {
                if (Checked != value) {
                    _radiobuttonState[s_stateChecked] = value ? 1 : 0;
                    Invalidate ();

                    PerformAutoUpdates (false);

                    OnCheckedChanged (EventArgs.Empty);
                }
            }
        }

        /// <summary>
        /// Raised when the value of the Checked property changes.
        /// </summary>
        public event EventHandler? CheckedChanged;

        /// <inheritdoc/>
        protected override Cursor DefaultCursor => Cursors.Hand;

        /// <inheritdoc/>
        protected override Size DefaultSize => new Size (104, 24);

        /// <inheritdoc/>
        public new static readonly ControlStyle DefaultStyle = new ControlStyle (Control.DefaultStyle);

        /// <summary>
        /// Gets or sets the image displayed on the <see cref='RadioButton'/>.
        /// </summary>
#pragma warning disable CA1416
        public override Majorsilence.Forms.Drawing.Image? Image {
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
        /// Gets or sets the alignment of the image on the <see cref='RadioButton'/>.
        /// </summary>
        public override ContentAlignment ImageAlign {
            get => Properties.GetEnum (s_propImageAlign, ContentAlignment.MiddleLeft);
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
        /// Gets or sets the index of the image in the <see cref='ImageList'/> associated with the <see cref='RadioButton'/>.
        /// </summary>
        public override int ImageIndex {
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
        /// Gets or sets the key of the image in the <see cref='ImageList'/> associated with the <see cref='RadioButton'/>.
        /// </summary>
        public override string ImageKey {
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
        /// Gets or sets the <see cref='ImageList'/> associated with the <see cref='RadioButton'/>.
        /// </summary>
        public override ImageList? ImageList {
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
        /// Raises the CheckedChanged event.
        /// </summary>
        protected virtual void OnCheckedChanged (EventArgs e) => CheckedChanged?.Invoke (this, e);

        /// <inheritdoc/>
        protected override void OnClick (EventArgs e)
        {
            if (AutoCheck && !Checked)
                Checked = true;

            base.OnClick (e);
        }

        /// <inheritdoc/>
        protected override void OnKeyUp (KeyEventArgs e)
        {
            if (e.KeyCode.In (Keys.Space, Keys.Enter)) {
                OnClick (new MouseEventArgs (MouseButtons.Left, 1, 0, 0, Point.Empty));
                e.Handled = true;
                return;
            }

            base.OnKeyUp (e);
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
        /// Gets or sets the alignment of the text on the <see cref='RadioButton'/>.
        /// </summary>
        public override ContentAlignment TextAlign {
            get => Properties.GetEnum (s_propTextAlign, ContentAlignment.MiddleLeft);
            set {
                SourceGenerated.EnumValidator.Validate (value);

                if (value != TextAlign) {
                    Properties.SetEnum (s_propTextAlign, value);
                    LayoutTransaction.DoLayoutIf (AutoSize, Parent, this, PropertyNames.TextAlign);
                    Invalidate ();
                }
            }
        }

        /// <summary>
        /// Gets or sets the alignment of the text relative to the image on the <see cref='RadioButton'/>.
        /// </summary>
        public override TextImageRelation TextImageRelation {
            get => Properties.GetEnum (s_propTextImageRelation, TextImageRelation.ImageBeforeText);
            set {
                SourceGenerated.EnumValidator.Validate (value);

                if (value != TextImageRelation) {
                    Properties.SetEnum (s_propTextImageRelation, value);
                    LayoutTransaction.DoLayoutIf (AutoSize, Parent, this, PropertyNames.TextImageRelation);
                    Invalidate ();
                }
            }
        }

        /// <summary>Gets or sets whether the control draws itself as a radio button, or as a toggle button.</summary>
        /// <remarks>
        /// SMP-03: this was a stub nothing read, so the segmented-control idiom -- a row of
        /// <see cref="Majorsilence.Forms.Appearance.Button"/> controls acting as a toolbar -- drew as
        /// ordinary radio buttons with no pressed state. SMP-04: the setter now raises
        /// <see cref="AppearanceChanged"/>, which an auto-property had nowhere to do from.
        /// </remarks>
        public Appearance Appearance {
            get => appearance;
            set {
                if (appearance == value)
                    return;

                appearance = value;

                // The glyph's column is part of the preferred size, so gaining or losing it re-measures.
                if (Parent is not null)
                    LayoutTransaction.DoLayoutIf (AutoSize, Parent, this, PropertyNames.Appearance);

                Invalidate ();
                OnAppearanceChanged (EventArgs.Empty);
            }
        }

        private Appearance appearance = Appearance.Normal;

        /// <inheritdoc/>
        internal override Appearance AppearanceCore => Appearance;

        /// <inheritdoc/>
        internal override bool IsLatched => Checked;

        /// <inheritdoc/>
        /// <remarks>Folds FlatStyle/FlatAppearance/Appearance in before the renderer sees the style.</remarks>
        public override ControlStyle CurrentStyle {
            get {
                ApplyFlatAppearance ();
                return base.CurrentStyle;
            }
        }

        /// <summary>Gets or sets the flat style appearance of the radio button.</summary>
        /// <remarks>
        /// SMP-05: stored only until now -- only <see cref="Button"/> folded FlatStyle and
        /// <see cref="FlatAppearance"/> into its style chain, so a flat radio button kept the themed
        /// 3-D frame and the designer's FlatAppearance lines were inert.
        /// </remarks>
        public override FlatStyle FlatStyle { get; set; } = FlatStyle.Standard;

        /// <summary>Gets the appearance settings for a flat-style button.</summary>
        public override FlatButtonAppearance FlatAppearance { get; } = new FlatButtonAppearance ();

        /// <summary>Simulates a click on the radio button. Checks the button if AutoCheck is true.</summary>
        public void PerformClick () => OnClick (new MouseEventArgs (MouseButtons.Left, 1, 0, 0, System.Drawing.Point.Empty));

        /// <inheritdoc/>
        public override string ToString () => $"{base.ToString ()}, Checked: {Checked}";

        // SMP-13: hands the renderer the full text region rather than a rectangle measured for one
        // line, so a wrapped caption has somewhere to put its second line. Alignment is unaffected --
        // the renderer still aligns the text within the region using TextAlign.
        bool IHaveTextAndImageAlign.Multiline => true;
    }
}
