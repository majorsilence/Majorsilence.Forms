using System.Drawing;

namespace Majorsilence.Forms.Layout;

/// <summary>
/// The engine behind <see cref="StackPanel"/>. Stateless between passes on purpose: everything about a
/// child's size across the stack is derived from the container's content width as it is now.
/// </summary>
internal sealed class StackLayout : LayoutEngine
{
    internal static readonly StackLayout Instance = new ();

    private static readonly int s_alignmentProperty = PropertyStore.CreateKey ();

    internal static StackAlignment? GetAlignment (Control child)
    {
        var element = (IArrangedElement)child;
        var value = element.Properties.GetInteger (s_alignmentProperty, -1);
        return value < 0 ? null : (StackAlignment)value;
    }

    internal static void SetAlignment (Control child, StackAlignment value)
        => ((IArrangedElement)child).Properties.SetInteger (s_alignmentProperty, (int)value);

    private protected override bool LayoutCore (IArrangedElement container, LayoutEventArgs args)
    {
        if (container is not StackPanel panel)
            return false;

        // The display rectangle already carries the scroll offset as its origin and has the padding and any
        // visible scroll bar taken off, so children are placed where scrolling has put them. Starting from
        // the padding again here is what snaps a scrolled form back to the top.
        var area = container.DisplayRectangle;

        // The width to fill is the viewport's, not the display rectangle's: that one becomes the content extent once a horizontal
        // scroll bar shows, and filling it would keep the scroll bar alive.
        area.Width = panel.ViewportWidth;
        var used = Arrange (panel, area, measureOnly: false);
        CommonProperties.SetLayoutBounds (container, used);
        return CommonProperties.GetAutoSize (container);
    }

    internal override Size GetPreferredSize (IArrangedElement container, Size proposedConstraints)
    {
        if (container is not StackPanel panel)
            return Size.Empty;

        return Arrange (panel, new Rectangle (Point.Empty, proposedConstraints), measureOnly: true);
    }

    // Lays the visible children out in `area`, or only measures them. Returns the extent used.
    // "Main" is the axis the children are stacked along, "cross" the other one.
    private static Size Arrange (StackPanel panel, Rectangle area, bool measureOnly)
    {
        var vertical = panel.Orientation == Orientation.Vertical;
        var children = ((IArrangedElement)panel).Children.Where (c => ((IArrangedElement)c).ParticipatesInLayout).ToList ();

        var crossAvailable = vertical ? area.Width : area.Height;
        var cap = vertical && panel.MaximumContentWidth > 0 ? panel.MaximumContentWidth : int.MaxValue;

        // A measure with no width to work to is asking for the natural size, the widest child. Callers say so with zero or with
        // the maximum size (Control.GetPreferredSize (Size.Empty) arrives as the latter).
        var constrained = crossAvailable > 0 && crossAvailable < int.MaxValue / 2;
        var crossExtent = constrained ? Math.Min (crossAvailable, cap) : NaturalCross (children, vertical, cap);

        var crossOrigin = vertical ? area.X : area.Y;
        crossOrigin += constrained ? Offset (panel.ContentAlignment, crossAvailable - crossExtent) : 0;

        var mainOrigin = vertical ? area.Y : area.X;
        var main = mainOrigin;
        var first = true;

        foreach (var child in children) {
            var margin = child.Margin;
            var marginCross = vertical ? margin.Horizontal : margin.Vertical;
            var innerCross = Math.Max (0, crossExtent - marginCross);

            // A child that sizes itself is asked at the width it will get, so a wrapping label reports its wrapped height.
            var preferred = child.AutoSize
                ? child.GetPreferredSize (vertical ? new Size (innerCross, 0) : new Size (0, innerCross))
                : OwnSize (child);

            var alignment = GetAlignment (child) ?? panel.ChildAlignment;
            var childCross = alignment == StackAlignment.Stretch ? innerCross : Math.Min (vertical ? preferred.Width : preferred.Height, innerCross);
            var childMain = vertical ? preferred.Height : preferred.Width;

            if (!first)
                main += panel.Spacing;
            first = false;

            main += vertical ? margin.Top : margin.Left;

            if (!measureOnly) {
                var cross = crossOrigin + (vertical ? margin.Left : margin.Top) + Offset (alignment, innerCross - childCross);
                var bounds = vertical
                    ? new Rectangle (cross, main, childCross, childMain)
                    : new Rectangle (main, cross, childMain, childCross);
                ((IArrangedElement)child).SetBounds (bounds, BoundsSpecified.None);
            }

            main += childMain + (vertical ? margin.Bottom : margin.Right);
        }

        var length = main - mainOrigin;
        return vertical ? new Size (crossExtent, length) : new Size (length, crossExtent);
    }

    // The size the child was given, not the size a previous pass stretched it to: otherwise a child set to Stretch and then to
    // Start would keep the width the stretch gave it.
    private static Size OwnSize (Control child) => CommonProperties.GetSpecifiedBounds (child).Size;

    private static int NaturalCross (List<Control> children, bool vertical, int cap)
    {
        var widest = 0;
        foreach (var child in children) {
            var size = child.AutoSize ? child.GetPreferredSize (Size.Empty) : OwnSize (child);
            var margin = child.Margin;
            widest = Math.Max (widest, vertical ? size.Width + margin.Horizontal : size.Height + margin.Vertical);
        }

        return Math.Min (widest, cap);
    }

    // Where `free` spare pixels leave something placed by `alignment`; Stretch has none to spare, so it sits at the start.
    private static int Offset (StackAlignment alignment, int free)
        => alignment switch {
            StackAlignment.Center => free / 2,
            StackAlignment.End => free,
            _ => 0,
        };
}
