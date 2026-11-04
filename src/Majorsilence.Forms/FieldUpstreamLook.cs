using SkiaSharp;

namespace Majorsilence.Forms
{
    // A ported WinForms app (one that chose its font with Application.SetDefaultFont) gets the text and
    // list boxes WinForms draws under the Windows 11 theme, colours measured from it: a white field
    // (SystemColors.Window) inside a light outline, a text box with a darker line along its bottom edge,
    // and a single-line text box as tall as its font makes it (upstream's AutoSize: Font.Height + 7, 23px
    // for Segoe UI 9pt). The theme's off-white field and grey frame, 20px tall where the designer left
    // it, read as a different control. Fixed3D (the default) borders only; apps that have not chosen a
    // font keep the theme's fields.
    internal static class FieldUpstreamLook
    {
        internal static readonly SKColor Window = SKColors.White;
        internal static readonly SKColor Control = new SKColor (0xF0, 0xF0, 0xF0);
        internal static readonly SKColor TextBoxOutline = new SKColor (0xEC, 0xEC, 0xEC);
        internal static readonly SKColor TextBoxBottom = new SKColor (0x83, 0x83, 0x83);

        // A disabled text box: the light outline all round (its bottom a shade lighter) with a near-white line inside
        // it, round the Control face. It kept the enabled box's dark bottom line, and on a Control background showed
        // nothing else.
        internal static readonly SKColor TextBoxDisabledBottom = new SKColor (0xED, 0xED, 0xED);
        internal static readonly SKColor TextBoxDisabledInner = new SKColor (0xFB, 0xFB, 0xFB);

        // A read-only box on the Control face keeps the enabled box's frame with a near-white line just inside it.
        internal static readonly SKColor TextBoxReadOnlyInner = new SKColor (0xFE, 0xFE, 0xFE);

        // A line one device unit wide just inside a field's 1px outline.
        internal static void InnerLine (PaintEventArgs e, int width, int height, SKColor colour)
        {
            var px = (float) e.Scaling;
            using var inner = new SKPaint { Color = colour, IsStroke = true, StrokeWidth = px };
            e.Canvas.DrawRect (new SKRect (px * 1.5f, px * 1.5f, width - px * 1.5f, height - px * 1.5f), inner);
        }

        // A focused text box's bottom edge: two pixels of the accent, in place of the grey line.
        internal static readonly SKColor TextBoxFocusBottom = new SKColor (0x00, 0x67, 0xC0);
        internal static readonly SKColor ListBoxOutline = new SKColor (0x82, 0x87, 0x90);

        // A FixedSingle tree's outline: the native control's WS_BORDER line.
        internal static readonly SKColor SingleOutline = new SKColor (0x64, 0x64, 0x64);

        // A FixedSingle list box's outline, lighter when the list box is disabled.
        internal static readonly SKColor ListBoxSingleOutline = new SKColor (0x7A, 0x7A, 0x7A);
        internal static readonly SKColor ListBoxSingleDisabledOutline = new SKColor (0xCC, 0xCC, 0xCC);

        // A drop-down list is drawn like a push button: near-white face, light outline, darker bottom.
        internal static readonly SKColor ComboListFace = new SKColor (0xFD, 0xFD, 0xFD);
        internal static readonly SKColor ComboListOutline = new SKColor (0xD2, 0xD2, 0xD2);
        internal static readonly SKColor ComboListBottom = new SKColor (0xBC, 0xBC, 0xBC);
        internal static readonly SKColor Chevron = new SKColor (0x6E, 0x6E, 0x6E);
        // A grid's combo box cell draws the same field, a separator before its button, and a slightly lighter chevron.
        internal static readonly SKColor ComboCellChevron = new SKColor (0x6F, 0x6F, 0x6F);

        // Disabled, a drop-down list keeps a near-white face inside one light outline all round, and an editable
        // drop-down a white one inside a mid-grey outline; the chevron of either is pale grey.
        internal static readonly SKColor ComboListDisabledFace = new SKColor (0xFA, 0xFA, 0xFA);
        internal static readonly SKColor ComboListDisabledOutline = new SKColor (0xEA, 0xEA, 0xEA);
        internal static readonly SKColor ComboEditDisabledOutline = new SKColor (0xC8, 0xC8, 0xC8);
        internal static readonly SKColor ChevronDisabled = new SKColor (0xBD, 0xBD, 0xBD);

        // A date picker is the native control's: one darker line all round, and (ShowUpDown) spin buttons with a
        // near-white face in a pale outline.
        internal static readonly SKColor PickerFrame = new SKColor (0x8D, 0x8D, 0x8D);

        // The native picker's chevron is darker than a combo box's; disabled, the picker is a near-white field in a pale
        // frame with a pale chevron, where it kept the enabled frame on the Control face.
        internal static readonly SKColor PickerChevron = new SKColor (0x4D, 0x4D, 0x4D);
        internal static readonly SKColor PickerDisabledFrame = new SKColor (0xC8, 0xC8, 0xC8);
        internal static readonly SKColor PickerDisabledFace = new SKColor (0xFE, 0xFE, 0xFE);
        internal static readonly SKColor PickerDisabledChevron = new SKColor (0xC9, 0xC9, 0xC9);
        internal static readonly SKColor SpinFace = new SKColor (0xFA, 0xFA, 0xFA);
        internal static readonly SKColor SpinOutline = new SKColor (0xD2, 0xD2, 0xD2);

        internal static bool Applies => ControlPaint.UsesUpstreamGlyphs;

        // Fills the field and draws its 1px outline; bottom, when given, is the colour of the bottom edge.
        internal static void Paint (PaintEventArgs e, int width, int height, SKColor face, SKColor outline, SKColor? bottom)
        {
            var scale = (float) e.Scaling;

            e.Canvas.Clear (face);

            using var paint = new SKPaint { Color = outline };
            e.Canvas.DrawRect (new SKRect (0, 0, width, scale), paint);
            e.Canvas.DrawRect (new SKRect (0, 0, scale, height), paint);
            e.Canvas.DrawRect (new SKRect (width - scale, 0, width, height), paint);

            paint.Color = bottom ?? outline;
            e.Canvas.DrawRect (new SKRect (0, height - scale, width, height), paint);
        }
    }

    public partial class TextBox
    {
        /// <inheritdoc/>
        internal override SKColor? UpstreamDefaultBackColor
            => FieldUpstreamLook.Applies && (Enabled || IsEmbeddedEdit) && !ReadOnly ? FieldUpstreamLook.Window : null;

        // A combo box's edit region: the combo paints the field round it, and upstream's edit is a borderless child
        // with a white face, enabled or not. It painted a field of its own, forced to a text box's height, so its
        // outline showed inside the combo's and its face covered part of the combo's bottom line.
        internal virtual bool IsEmbeddedEdit => false;

        private bool PaintsUpstreamField => FieldUpstreamLook.Applies && BorderStyle == BorderStyle.Fixed3D && BackgroundImage is null && !IsEmbeddedEdit;

        // Upstream's AutoSize height for a single-line box: the font's height plus the border and its
        // spacing (SystemInformation border size x 4 + 3), or the font's height alone without a border.
        private int UpstreamHeight => Font.Height + (BorderStyle == BorderStyle.None ? 0 : 7);

        private bool SizesToUpstreamHeight => FieldUpstreamLook.Applies && !Multiline && !IsEmbeddedEdit;

        private void PaintUpstreamField (PaintEventArgs e)
        {
            using var device = e.DeviceSpace ();

            // Read-only and disabled boxes show SystemColors.Control, as upstream's do.
            var face = !Enabled || ReadOnly && Style.BackgroundColor is null ? FieldUpstreamLook.Control : GetEffectiveBackgroundColor ();

            if (!Enabled) {
                FieldUpstreamLook.Paint (e, ScaledWidth, ScaledHeight, face, FieldUpstreamLook.TextBoxOutline, FieldUpstreamLook.TextBoxDisabledBottom);

                // The near-white line just inside the outline.
                FieldUpstreamLook.InnerLine (e, ScaledWidth, ScaledHeight, FieldUpstreamLook.TextBoxDisabledInner);
                return;
            }

            FieldUpstreamLook.Paint (e, ScaledWidth, ScaledHeight, face, FieldUpstreamLook.TextBoxOutline, FieldUpstreamLook.TextBoxBottom);

            // A read-only box on the Control face has a near-white line inside its frame too, as upstream's does.
            if (ReadOnly && Style.BackgroundColor is null)
                FieldUpstreamLook.InnerLine (e, ScaledWidth, ScaledHeight, FieldUpstreamLook.TextBoxReadOnlyInner);

            // With the keyboard focus, upstream's box underlines itself in the accent, two pixels deep.
            if (Selected && Enabled) {
                using var paint = new SKPaint { Color = FieldUpstreamLook.TextBoxFocusBottom };
                var line = 2 * (float) e.Scaling;
                e.Canvas.DrawRect (new SKRect (0, ScaledHeight - line, ScaledWidth, ScaledHeight), paint);
            }
        }

        /// <inheritdoc/>
        protected override void SetBoundsCore (int x, int y, int width, int height, BoundsSpecified specified)
        {
            if (SizesToUpstreamHeight)
                height = UpstreamHeight;

            base.SetBoundsCore (x, y, width, height, specified);
        }
    }

    public partial class ComboBox
    {
        /// <inheritdoc/>
        internal override SKColor? UpstreamDefaultBackColor
            => FieldUpstreamLook.Applies && Enabled
                ? DropDownStyle == ComboBoxStyle.DropDownList ? FieldUpstreamLook.ComboListFace : FieldUpstreamLook.Window
                : null;

        // A Simple combo's list is a child box with its own field; only the drop-down styles get the face.
        internal bool PaintsUpstreamField => FieldUpstreamLook.Applies && !IsSimple && BackgroundImage is null;

        /// <inheritdoc/>
        protected override void OnPaintBackground (PaintEventArgs e)
        {
            if (!PaintsUpstreamField) {
                base.OnPaintBackground (e);
                return;
            }

            using var device = e.DeviceSpace ();

            if (!Enabled) {
                if (DropDownStyle == ComboBoxStyle.DropDownList)
                    FieldUpstreamLook.Paint (e, ScaledWidth, ScaledHeight, FieldUpstreamLook.ComboListDisabledFace, FieldUpstreamLook.ComboListDisabledOutline, null);
                else
                    FieldUpstreamLook.Paint (e, ScaledWidth, ScaledHeight, FieldUpstreamLook.Window, FieldUpstreamLook.ComboEditDisabledOutline, null);

                return;
            }

            var face = GetEffectiveBackgroundColor ();

            if (DropDownStyle == ComboBoxStyle.DropDownList)
                FieldUpstreamLook.Paint (e, ScaledWidth, ScaledHeight, face, FieldUpstreamLook.ComboListOutline, FieldUpstreamLook.ComboListBottom);
            else
                FieldUpstreamLook.Paint (e, ScaledWidth, ScaledHeight, face, FieldUpstreamLook.TextBoxOutline, FieldUpstreamLook.TextBoxBottom);
        }

        // Upstream's Windows 11 drop-down arrow: an 8 x 4 chevron of single pixels, a staircase down four
        // and up four, starting 12px in from the right-hand outline and centred vertically. Drawn as whole
        // pixels: a 1px antialiased line at this size smears into pale grey.
        internal static void DrawUpstreamChevron (PaintEventArgs e, int width, int height, bool enabled)
            => DrawUpstreamChevron (e, width, height, enabled ? FieldUpstreamLook.Chevron : FieldUpstreamLook.ChevronDisabled);

        internal static void DrawUpstreamChevron (PaintEventArgs e, int width, int height, SKColor colour)
        {
            var px = (int) System.Math.Max (1, System.Math.Round (e.Scaling));
            var left = width - 13 * px;
            var top = (height - 4 * px + 1) / 2;

            using var paint = new SKPaint { Color = colour };

            for (var step = 0; step < 4; step++) {
                e.Canvas.DrawRect (SKRect.Create (left + step * px, top + step * px, px, px), paint);
                e.Canvas.DrawRect (SKRect.Create (left + (7 - step) * px, top + step * px, px, px), paint);
            }
        }
    }

    // A list box is the native field: a Fixed3D one's client area two pixels in -- a #828790 line, then a white one
    // whatever the BackColor -- and a FixedSingle one's one, inside a #7A7A7A line (#CCCCCC disabled); its face the
    // BackColor, white unless set, enabled or not. Measured on WinForms (.NET 10): a 120 x 120 list box's client is
    // 116 square from (2, 2), or 118 from (1, 1), and with IntegralHeight it is 104, or 102, high. Here the client
    // was one pixel in whatever the BorderStyle, inside a #ABABAB line for FixedSingle, and a disabled list box's
    // face was grey. A combo box's drop-down and suggestion lists keep the field they had.
    public partial class ListBox
    {
        /// <inheritdoc/>
        internal override SKColor? UpstreamDefaultBackColor
            => FieldUpstreamLook.Applies && (Enabled || UsesUpstreamFieldEdge) ? FieldUpstreamLook.Window : null;

        // A combo box's drop-down or suggestion list, kept off the list box's field: the native drop-down is a window
        // of its own (see PaintsUpstreamDropDown).
        internal bool IsDropDownList { get; set; }

        // A combo box's drop-down drawn as upstream's native list window (ComboLBox), measured on WinForms (.NET 10)
        // under the Windows 11 theme: a one-pixel #7A7A7A line (a FixedSingle list box's) round a face of the combo's
        // BackColor, white unless set; the highlighted row filled with SystemColors.Highlight under HighlightText,
        // other rows in the combo's ForeColor; the highlight follows the mouse, there being no hover colour of its
        // own; and a DropDownList's list a dotted focus cue round the highlighted row. Only the combo's own list
        // says yes.
        internal virtual bool PaintsUpstreamDropDown => false;

        // Whether the drop-down draws the dotted focus cue: a DropDownList's does, a DropDown's (whose focus is in the
        // edit region) does not.
        internal virtual bool UpstreamDropDownFocusCue => false;

        // The colour of an unhighlighted row's text in the drop-down.
        internal virtual SKColor UpstreamDropDownForeColor => GetEffectiveForegroundColor ();

        // The row the drop-down highlights: the one under the mouse, else the selected one.
        internal int UpstreamDropDownHighlight => Items.HoveredIndex >= 0 ? Items.HoveredIndex : SelectedIndex;

        // Whether the list box is the native field, edge and all.
        private bool UsesUpstreamFieldEdge => FieldUpstreamLook.Applies && !IsDropDownList;

        // The field's edge, in logical pixels, by BorderStyle.
        private int UpstreamFieldEdge => border_style switch {
            BorderStyle.Fixed3D => 2,
            BorderStyle.FixedSingle => 1,
            _ => 0,
        };

        /// <inheritdoc/>
        internal override System.Drawing.Rectangle DeviceClientRectangle {
            get {
                // The drop-down's client is inside its one-pixel line, at any scale.
                if (!UsesUpstreamFieldEdge && !PaintsUpstreamDropDown)
                    return base.DeviceClientRectangle;

                var edge = LogicalToDeviceUnits (PaintsUpstreamDropDown ? 1 : UpstreamFieldEdge);
                return new System.Drawing.Rectangle (edge, edge, System.Math.Max (0, ScaledWidth - edge * 2), System.Math.Max (0, ScaledHeight - edge * 2));
            }
        }

        /// <inheritdoc/>
        /// <remarks>Inside the field's edge, where the docked scroll bar sits: upstream's stops short of the outline,
        /// clear of the white line inside it.</remarks>
        public override System.Drawing.Rectangle DisplayRectangle {
            get {
                if (!UsesUpstreamFieldEdge)
                    return base.DisplayRectangle;

                var edge = UpstreamFieldEdge;
                return new System.Drawing.Rectangle (edge, edge, System.Math.Max (0, Width - edge * 2), System.Math.Max (0, Height - edge * 2));
            }
        }

        private bool PaintsUpstreamField
            => FieldUpstreamLook.Applies && BackgroundImage is null
            && (PaintsUpstreamDropDown || (UsesUpstreamFieldEdge ? BorderStyle != BorderStyle.None : BorderStyle == BorderStyle.Fixed3D));

        private void PaintUpstreamField (PaintEventArgs e)
        {
            using var device = e.DeviceSpace ();

            if (PaintsUpstreamDropDown) {
                FieldUpstreamLook.Paint (e, ScaledWidth, ScaledHeight, GetEffectiveBackgroundColor (), FieldUpstreamLook.ListBoxSingleOutline, null);
                return;
            }

            if (!UsesUpstreamFieldEdge) {
                var face = Enabled ? GetEffectiveBackgroundColor () : FieldUpstreamLook.Control;
                FieldUpstreamLook.Paint (e, ScaledWidth, ScaledHeight, face, FieldUpstreamLook.ListBoxOutline, null);
                return;
            }

            if (BorderStyle == BorderStyle.Fixed3D) {
                FieldUpstreamLook.Paint (e, ScaledWidth, ScaledHeight, GetEffectiveBackgroundColor (), FieldUpstreamLook.ListBoxOutline, null);
                FieldUpstreamLook.InnerLine (e, ScaledWidth, ScaledHeight, FieldUpstreamLook.Window);
            } else {
                var outline = Enabled ? FieldUpstreamLook.ListBoxSingleOutline : FieldUpstreamLook.ListBoxSingleDisabledOutline;
                FieldUpstreamLook.Paint (e, ScaledWidth, ScaledHeight, GetEffectiveBackgroundColor (), outline, null);
            }
        }
    }

    // A tree is a native field as a list box is: a white face, and a Fixed3D tree's client area two pixels in -- a
    // #828790 line, then a pixel of the face -- or a FixedSingle tree's one, inside a #646464 line, enabled or not.
    // Measured on WinForms (.NET 10): a 120 x 120 tree's client is 116 square from (2, 2), or 118 from (1, 1), and
    // the line just inside the outline is the tree's BackColor. Here the tree kept the theme's off-white face and a
    // one-pixel grey frame, whatever its BorderStyle, so it showed two more pixels of rows each way.
    public partial class TreeView
    {
        /// <inheritdoc/>
        internal override SKColor? UpstreamDefaultBackColor => PaintsUpstreamFieldLook ? FieldUpstreamLook.Window : null;

        // Whether this tree is a native field: a derived tree with a look of its own (Telerik's) says no.
        internal virtual bool PaintsUpstreamFieldLook => FieldUpstreamLook.Applies;

        // The field's edge, in logical pixels, by BorderStyle.
        private int UpstreamFieldEdge => border_style switch {
            BorderStyle.Fixed3D => 2,
            BorderStyle.FixedSingle => 1,
            _ => 0,
        };

        /// <inheritdoc/>
        internal override System.Drawing.Rectangle DeviceClientRectangle {
            get {
                if (!PaintsUpstreamFieldLook)
                    return base.DeviceClientRectangle;

                var edge = LogicalToDeviceUnits (UpstreamFieldEdge);
                return new System.Drawing.Rectangle (edge, edge, System.Math.Max (0, ScaledWidth - edge * 2), System.Math.Max (0, ScaledHeight - edge * 2));
            }
        }

        /// <inheritdoc/>
        /// <remarks>Inside the field's edge, where the docked scroll bar sits: upstream's stops short of the outline,
        /// clear of the pixel of face inside it.</remarks>
        public override System.Drawing.Rectangle DisplayRectangle {
            get {
                if (!PaintsUpstreamFieldLook)
                    return base.DisplayRectangle;

                var edge = UpstreamFieldEdge;
                return new System.Drawing.Rectangle (edge, edge, System.Math.Max (0, Width - edge * 2), System.Math.Max (0, Height - edge * 2));
            }
        }

        private bool PaintsUpstreamField => PaintsUpstreamFieldLook && border_style != BorderStyle.None && BackgroundImage is null;

        private void PaintUpstreamField (PaintEventArgs e)
        {
            using var device = e.DeviceSpace ();

            var outline = border_style == BorderStyle.Fixed3D ? FieldUpstreamLook.ListBoxOutline : FieldUpstreamLook.SingleOutline;
            FieldUpstreamLook.Paint (e, ScaledWidth, ScaledHeight, GetEffectiveBackgroundColor (), outline, null);
        }
    }
}
