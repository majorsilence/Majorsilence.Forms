using System;
using System.Drawing;
using Majorsilence.Forms.Drawing;

namespace Majorsilence.Forms
{
    // Third wave from the Krypton port: members and overloads that existed under the right name with the
    // wrong shape, plus a handful that were simply absent. Grouped by the type they belong to rather than
    // by cause, because each one is small.

    public partial struct Padding
    {
        /// <summary>Adds two paddings side by side.</summary>
        /// <remarks>
        /// The additive form of the <c>+</c> operator, which WinForms exposes as a named static so that a
        /// caller composing padding from several sources -- a control's own padding plus the border its
        /// renderer draws -- can do so without operator syntax.
        /// </remarks>
        public static Padding Add (Padding p1, Padding p2)
            => new Padding (p1.Left + p2.Left, p1.Top + p2.Top, p1.Right + p2.Right, p1.Bottom + p2.Bottom);

        /// <summary>Subtracts one padding from another.</summary>
        /// <inheritdoc cref="Add"/>
        public static Padding Subtract (Padding p1, Padding p2)
            => new Padding (p1.Left - p2.Left, p1.Top - p2.Top, p1.Right - p2.Right, p1.Bottom - p2.Bottom);

        /// <summary>Adds two paddings side by side.</summary>
        public static Padding operator + (Padding p1, Padding p2) => Add (p1, p2);

        /// <summary>Subtracts one padding from another.</summary>
        public static Padding operator - (Padding p1, Padding p2) => Subtract (p1, p2);
    }

    public partial class NativeWindow : IWin32Window
    {
        /// <summary>
        /// Invokes the default window procedure for the message.
        /// </summary>
        /// <remarks>
        /// Never called: there is no Win32 message pump here, the documented non-goal shared with
        /// <see cref="WndProc"/>. It has to exist all the same, and its absence was actively confusing --
        /// a subclassing window calling <c>DefWndProc</c> from inside its <c>WndProc</c> override would
        /// bind to the <i>enclosing</i> control's inherited <c>Control.DefWndProc</c> instead, and fail
        /// with a complaint about needing an object reference rather than about a missing member.
        /// </remarks>
        protected virtual void DefWndProc (ref Message m) { }
    }

    public partial class NumericUpDown
    {
        /// <summary>Gets or sets whether the control is in the middle of updating its own text.</summary>
        /// <remarks>
        /// WinForms declares this on <c>UpDownBase</c>, which every up-down derives from; here
        /// <see cref="NumericUpDown"/> derives from <see cref="Control"/> directly, so it is declared on
        /// both (see <see cref="UpDownBase.ChangingText"/>). It is a re-entrancy guard: while the control
        /// pushes a new value into its edit box, the resulting TextChanged must not be mistaken for the
        /// user typing.
        /// </remarks>
        protected bool ChangingText { get; set; }
    }

    public partial class ContextMenuStrip
    {
        /// <summary>Gets or sets whether space is reserved down the left edge for item images.</summary>
        /// <remarks>Turning it off is how a menu of plain text items loses the empty image gutter. Stored:
        /// the gutter is sized by this layer's own menu renderer from the items' images.</remarks>
        public bool ShowImageMargin { get; set; } = true;

        /// <summary>Gets or sets whether space is reserved for check marks separately from images.</summary>
        /// <inheritdoc cref="ShowImageMargin"/>
        public bool ShowCheckMargin { get; set; }
    }

    public partial class DataGridViewColumn : System.ComponentModel.IComponent
    {
        /// <summary>Gets whether a style has been set on this column rather than inherited.</summary>
        /// <remarks>
        /// WinForms reaches this through <c>DataGridViewBand</c>, which a column derives from; the column
        /// here is an independent type, so it declares its own (as it already does for Frozen, Visible and
        /// the rest). A column's <c>ShouldSerializeDefaultCellStyle</c> asks this before inspecting the
        /// style, which is what a designer serialiser and a themed column both call.
        /// </remarks>
        public bool HasDefaultCellStyle => true;

        /// <summary>Gets whether the column is currently on screen.</summary>
        /// <remarks>Answers from <see cref="Visible"/> and whether the column is attached to a grid,
        /// matching how <c>DataGridViewRow.Displayed</c> answers it.</remarks>
        public virtual bool Displayed => Visible && DataGridView is not null;

        // Site and Disposed, the other two halves of IComponent, were already declared (see
        // DataGridViewFamilyParity.cs). The interface itself was the missing part: without it a column
        // could not be handed to a component-change service, which is how a themed grid reports that it
        // has restyled one.
    }

    public partial class DataGridViewCell
    {
        /// <summary>Returns the cell's underlying value for the given row.</summary>
        /// <remarks>
        /// The value before formatting, which is what a derived cell reads when it needs to constrain or
        /// reinterpret what it holds -- a numeric cell clamping to a new maximum, a date cell re-parsing
        /// after the format changed. Reads through the owning grid so it sees the same value the grid
        /// paints; falls back to the cell's own <see cref="Value"/> when the cell is not attached to one.
        /// </remarks>
        protected virtual object? GetValue (int rowIndex)
        {
            if (DataGridView is null || rowIndex < 0 || rowIndex >= DataGridView.Rows.Count)
                return Value;

            var row = DataGridView.Rows[rowIndex];

            return ColumnIndex >= 0 && ColumnIndex < row.Cells.Count ? row.Cells[ColumnIndex].Value : Value;
        }

        /// <summary>Sets the cell's underlying value for the given row.</summary>
        /// <inheritdoc cref="GetValue"/>
        /// <returns>True when the value was stored.</returns>
        protected virtual bool SetValue (int rowIndex, object? value)
        {
            if (DataGridView is null || rowIndex < 0 || rowIndex >= DataGridView.Rows.Count) {
                Value = value;
                return true;
            }

            var row = DataGridView.Rows[rowIndex];

            if (ColumnIndex < 0 || ColumnIndex >= row.Cells.Count)
                return false;

            row.Cells[ColumnIndex].Value = value;
            return true;
        }
    }

    public partial class Control
    {
        /// <summary>Paints the parent's background behind this control, for simulated transparency.</summary>
        /// <remarks>
        /// <para>
        /// WinForms declares this private, and themed control libraries call it by REFLECTION
        /// (<c>BindingFlags.NonPublic</c>) to paint their transparent-background controls -- so its
        /// absence surfaced as a null dereference on the reflected handle, not as a compile error.
        /// Krypton's <c>VisualControlBase</c> invokes it on every paint of every label, check box, radio
        /// button and group caption.
        /// </para>
        /// <para>
        /// It samples the parent's already-painted PIXELS. Filling the nearest ancestor's
        /// <c>BackColor</c> instead -- which is what this did first -- looks equivalent and is not: a
        /// Krypton container paints itself from its palette and leaves <c>BackColor</c> at the
        /// <c>SystemColors.Control</c> default, so every one of those controls came out as an F0F0F0
        /// rectangle the exact size of itself, with the palette's light text on top of it. It read as
        /// "the group captions and check-box labels are washed out" rather than as a background bug.
        /// </para>
        /// <para>
        /// Sampling is possible because of the order this library paints in: a child is drawn into its own
        /// buffer from inside its parent's <see cref="Control.PaintChildren"/>, which runs after the parent
        /// has painted its own background and content into that buffer. So the pixels wanted are simply
        /// the parent's, at this control's position, and nothing has to re-enter the parent's paint routine
        /// or walk further up the chain.
        /// </para>
        /// <para>
        /// That rests on two properties of the pipeline, so changing either breaks this. The parent's
        /// background pass overwrites the previous frame's blitted children, so what is sampled is never
        /// last frame's picture of this same control. And a child is never repainted without its parent
        /// being repainted first -- <see cref="Control.PaintChildren"/> re-renders a child only while
        /// rendering the parent, and reuses the whole subtree's buffers otherwise.
        /// </para>
        /// </remarks>
        internal void PaintTransparentBackground (PaintEventArgs e, Rectangle rectangle, Region? transparentRegion)
        {
            if (Parent is not { } parent)
                return;

            var scale = ScaleFactor;

            e.Canvas.Save ();

            try {
                e.Canvas.ClipRect (new SkiaSharp.SKRect (
                    rectangle.Left * scale.Width, rectangle.Top * scale.Height,
                    rectangle.Right * scale.Width, rectangle.Bottom * scale.Height));

                // Krypton passes null, but the parameter is part of the signature it reflects on.
                if (transparentRegion?.GetSKRegion () is { IsEmpty: false } region) {
                    using var path = region.GetBoundaryPath ();
                    e.Canvas.ClipPath (path, SkiaSharp.SKClipOperation.Intersect, antialias: true);
                }

                // The field, not GetBackBuffer (): that would REBUILD the parent's buffer if its size had
                // changed, discarding the very pixels being sampled.
                var behind = parent.BackBufferPixels;

                if (behind is not null
                    && behind.Width == parent.ScaledSize.Width
                    && behind.Height == parent.ScaledSize.Height) {
                    var offset = parent.ChildPaintOffset;
                    e.Canvas.DrawBitmap (behind, -(offset.X + ScaledLeft), -(offset.Y + ScaledTop));
                    return;
                }

                // Nothing to sample: the root ControlAdapter paints straight into the window surface
                // rather than into a buffer. The parent's resolved background colour is then the best
                // answer, and the correct one whenever that parent is a plain fill.
                e.Canvas.Clear (parent.GetEffectiveBackgroundColor ());
            } finally {
                e.Canvas.Restore ();
            }
        }
    }

    public partial class WindowBase
    {
        /// <summary>
        /// Returns the child control at the given client coordinates, skipping the kinds of child
        /// <paramref name="skipValue"/> selects.
        /// </summary>
        /// <remarks>
        /// The overload that matters for hit-testing: a form routing a help request or a context menu to
        /// whatever is under the pointer must skip invisible and disabled children, or it targets something
        /// the user cannot see. Mirrors the <see cref="Control"/> overload of the same shape.
        /// </remarks>
        public Control? GetChildAtPoint (System.Drawing.Point pt, GetChildAtPointSkip skipValue)
        {
            foreach (var child in Controls.GetAllControls ()) {
                if (skipValue.HasFlag (GetChildAtPointSkip.Invisible) && !child.Visible)
                    continue;

                if (skipValue.HasFlag (GetChildAtPointSkip.Disabled) && !child.Enabled)
                    continue;

                if (skipValue.HasFlag (GetChildAtPointSkip.Transparent) && child.BackColor.A == 0)
                    continue;

                if (child.Bounds.Contains (pt))
                    return child;
            }

            return null;
        }
    }
}
