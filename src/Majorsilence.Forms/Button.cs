using System.Collections.Specialized;
using System.Drawing;
using Majorsilence.Forms.Layout;
using Majorsilence.Forms.Renderers;
using SkiaSharp;

namespace Majorsilence.Forms
{
    /// <summary>
    /// Represents a Button control.
    /// </summary>
    // IButtonControl is what Form.AcceptButton/CancelButton are typed as, so without it the ordinary
    // `this.AcceptButton = btnOk;` does not compile. Every member it requires -- DialogResult,
    // PerformClick, NotifyDefault -- was already here; only the declaration was missing.
    public partial class Button : ButtonBase, IHaveTextAndImageAlign, IButtonControl
    {
        private static readonly BitVector32.Section s_stateAutoEllipsis = BitVector32.CreateSection (1);

        private static readonly int s_propImage = PropertyStore.CreateKey ();
        private static readonly int s_propImageSK = PropertyStore.CreateKey ();
        private static readonly int s_propImageAlign = PropertyStore.CreateKey ();
        private static readonly int s_propImageList = PropertyStore.CreateKey ();
        private static readonly int s_propImageIndex = PropertyStore.CreateKey ();
        private static readonly int s_propImageKey = PropertyStore.CreateKey ();
        private static readonly int s_propTextAlign = PropertyStore.CreateKey ();
        private static readonly int s_propTextImageRelation = PropertyStore.CreateKey ();

        private BitVector32 _buttonState;

        /// <summary>
        /// Initializes a new instance of the Button class.
        /// </summary>
        public Button ()
        {
            SetControlBehavior (ControlBehaviors.Hoverable);
            SetControlBehavior (ControlBehaviors.InvalidateOnTextChanged);
        }

        /// <summary>
        /// Gets or sets a value indicating if text will be truncated with an ellipsis if it cannot fully fit in the <see cref='Button'/>.
        /// </summary>
        public override bool AutoEllipsis {
            get => _buttonState[s_stateAutoEllipsis] != 0;
            set {
                if (AutoEllipsis != value) {

                    _buttonState[s_stateAutoEllipsis] = value ? 1 : 0;

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

        /// <inheritdoc/>
        protected override Cursor DefaultCursor => Cursors.Hand;

        /// <inheritdoc/>
        protected override Size DefaultSize => new Size (100, 30);

        /// <summary>
        /// The default ControlStyle for all instances of Button.
        /// </summary>
        public new static readonly ControlStyle DefaultStyle = new ControlStyle (Control.DefaultStyle,
            (style) => style.Border.Width = 1);

        /// <summary>
        /// The default hover ControlStyle for all instances of Button.
        /// </summary>
        public new static readonly ControlStyle DefaultStyleHover = new ControlStyle (DefaultStyle,
            (style) => {
                style.BackgroundColor = Theme.AccentColor;
                style.Border.Color = Theme.AccentColor2;
                style.ForegroundColor = Theme.ForegroundColorOnAccent;
            });

        /// <summary>
        /// Gets or sets a value that is returned to the parent form when the button is clicked.
        /// </summary>
        public DialogResult DialogResult { get; set; }

        /// <summary>Gets or sets the flat style appearance of the button. Stub in Majorsilence.Forms.</summary>
        public override FlatStyle FlatStyle { get; set; } = FlatStyle.Standard;

        /// <summary>Gets the appearance settings used when the button has a flat appearance. Stub in Majorsilence.Forms.</summary>
        public override FlatButtonAppearance FlatAppearance { get; } = new FlatButtonAppearance ();

        /// <summary>Gets or sets whether the button uses the system visual style. Stub in Majorsilence.Forms.</summary>
        public override bool UseCompatibleTextRendering { get; set; }

        /// <summary>
        /// Gets or sets the image displayed on the <see cref='Button'/>.
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
        /// Gets or sets the alignment of the image on the <see cref='Button'/>. Defaults to
        /// <see cref="ContentAlignment.MiddleCenter"/>, as System.Windows.Forms.ButtonBase does --
        /// same reasoning as <see cref="TextAlign"/>: a designer only emits the property when it
        /// differs from the default, so an icon button relies on it to centre its glyph.
        /// </summary>
        public override ContentAlignment ImageAlign {
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
        /// Gets or sets the index of the image in the <see cref='ImageList'/> to display on the <see cref='Button'/>.
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
        /// Gets or sets the key of the image in the <see cref='ImageList'/> to display on the <see cref='Button'/>.
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
        /// Gets or sets the <see cref='ImageList'/> that contains the image to display on the <see cref='Button'/>.
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

        /// <inheritdoc/>
        protected override void OnClick (EventArgs e)
        {
            if (FindForm () is Form form)
                form.DialogResult = DialogResult;

            base.OnClick (e);
        }

        /// <inheritdoc/>
        protected override void OnKeyUp (KeyEventArgs e)
        {
            if (e.KeyCode.In (Keys.Space, Keys.Enter)) {
                PerformClick ();
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

        /// <summary>
        /// Generates a Click event for the Button.
        /// </summary>
        public void PerformClick ()
        {
            OnClick (new MouseEventArgs (MouseButtons.Left, 1, 0, 0, Point.Empty));
        }

        /// <inheritdoc/>
        public override ControlStyle Style { get; } = new ControlStyle (DefaultStyle);

        /// <inheritdoc/>
        public override ControlStyle StyleHover { get; } = new ControlStyle (DefaultStyleHover);

        /// <inheritdoc/>
        /// <remarks>
        /// Folds <see cref="FlatStyle"/> and <see cref="FlatAppearance"/> into the style chain before it
        /// is handed to the renderer. They can't be applied when they're set: <see cref="FlatAppearance"/>
        /// is a mutable object, so <c>button.FlatAppearance.BorderSize = 0</c> — the form the designer
        /// emits — raises no notification at all. Resolving here instead means a borderless flat button
        /// really is borderless, and switching back to <see cref="Majorsilence.Forms.FlatStyle.Standard"/>
        /// restores the themed border by clearing the override rather than by guessing its old value.
        /// </remarks>
        public override ControlStyle CurrentStyle {
            get {
                ApplyFlatAppearance ();
                return base.CurrentStyle;
            }
        }

        private void ApplyFlatAppearance ()
        {
            // Popup is flat until the pointer is over it, when it raises a Standard border.
            var isFlat = FlatStyle == FlatStyle.Flat
                      || (FlatStyle == FlatStyle.Popup && !(IsHovering && Enabled));

            if (!isFlat) {
                // Null lets the width fall back through the style chain to the themed default.
                Style.Border.Width = null;
                StyleHover.Border.Width = null;
                return;
            }

            Style.Border.Width = FlatAppearance.BorderSize;
            StyleHover.Border.Width = FlatAppearance.BorderSize;

            if (FlatAppearance.BorderColor != System.Drawing.Color.Empty) {
                Style.Border.Color = FlatAppearance.BorderColor.ToSKColor ();
                StyleHover.Border.Color = FlatAppearance.BorderColor.ToSKColor ();
            }

            if (FlatAppearance.MouseOverBackColor != System.Drawing.Color.Empty)
                StyleHover.BackgroundColor = FlatAppearance.MouseOverBackColor.ToSKColor ();
        }

        /// <summary>
        /// Gets or sets the alignment of the text on the <see cref='Button'/>. Defaults to
        /// <see cref="ContentAlignment.MiddleCenter"/>, as System.Windows.Forms.ButtonBase does --
        /// a designer only emits TextAlign when it differs from that, so every button laid out in the
        /// designer relies on the default to centre its caption. (CheckBox and RadioButton keep their
        /// own MiddleLeft default, which is what WinForms uses for those.)
        /// </summary>
        public override ContentAlignment TextAlign {
            get => Properties.GetEnum (s_propTextAlign, ContentAlignment.MiddleCenter);
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
        /// Gets or sets the alignment of the text relative to the image on the <see cref='Button'/>.
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

        bool IHaveTextAndImageAlign.Multiline => false;

        /// <inheritdoc/>
        public override string ToString () => $"{base.ToString ()}, Text: {Text}";
    }
}
