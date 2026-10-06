using System.ComponentModel;
using System.Drawing;
using Majorsilence.Forms.Layout;

namespace Majorsilence.Forms
{
    /// <summary>
    /// A panel that stacks its visible children one after another along a single axis, in the order they
    /// were added. A Majorsilence extension: WinForms has no stack panel.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Use it for the form-shaped screen: a column of captions, fields and buttons that fills a phone, stops
    /// at a readable width on a tablet (<see cref="MaximumContentWidth"/>), and scrolls when it is taller than
    /// the screen (<see cref="ScrollableControl.AutoScroll"/>).
    /// </para>
    /// <para>
    /// Every layout pass computes each child's size across the stack from the panel's current content width.
    /// Nothing is remembered between passes, so a resize can never leave a child at a width it had before,
    /// which is what an <see cref="Control.Anchor"/> snapshot in a <see cref="FlowLayoutPanel"/> can do.
    /// </para>
    /// <para>
    /// Along the stack a child keeps its own size, except that an <see cref="Control.AutoSize"/> child (a
    /// wrapping <see cref="Label"/>, for one) is asked for its preferred size at the width it will have, so it
    /// reports its wrapped height. <see cref="Control.Margin"/> is honoured and invisible children take no
    /// room. Wrapping onto new lines is <see cref="FlowLayoutPanel"/>'s job and proportional sizing is
    /// <see cref="TableLayoutPanel"/>'s.
    /// </para>
    /// </remarks>
    [ProvideProperty ("StackAlignment", typeof (Control))]
    [DefaultProperty (nameof (Orientation))]
    public class StackPanel : Panel, IExtenderProvider
    {
        private Orientation orientation = Orientation.Vertical;
        private int spacing;
        private int maximumContentWidth;
        private StackAlignment contentAlignment = StackAlignment.Center;
        private StackAlignment childAlignment = StackAlignment.Stretch;

        /// <inheritdoc/>
        public override LayoutEngine LayoutEngine => StackLayout.Instance;

        // Without this the implicit scroll bars of an AutoScroll panel would be stacked as children.
        internal override IEnumerable<Control> LayoutChildren => Controls.GetAllControls (false);

        /// <summary>The axis the children are stacked along. Vertical by default.</summary>
        [DefaultValue (Orientation.Vertical)]
        public Orientation Orientation {
            get => orientation;
            set {
                if (!EnumCompat.IsDefined (value))
                    throw new InvalidEnumArgumentException (nameof (value), (int)value, typeof (Orientation));

                if (orientation == value)
                    return;

                orientation = value;
                PerformLayout (this, nameof (Orientation));
            }
        }

        /// <summary>The gap between one child and the next, in addition to their margins. Zero by default.</summary>
        [DefaultValue (0)]
        public int Spacing {
            get => spacing;
            set {
                Guard.ThrowIfLessThan (value, 0);

                if (spacing == value)
                    return;

                spacing = value;
                PerformLayout (this, nameof (Spacing));
            }
        }

        /// <summary>
        /// For a vertical stack, the widest the column of children may be; zero (the default) means the full
        /// width. When the panel is wider, the column sits where <see cref="ContentAlignment"/> says and the
        /// panel itself, and so its scroll bar, still fills its parent. A horizontal stack ignores it.
        /// </summary>
        [DefaultValue (0)]
        public int MaximumContentWidth {
            get => maximumContentWidth;
            set {
                Guard.ThrowIfLessThan (value, 0);

                if (maximumContentWidth == value)
                    return;

                maximumContentWidth = value;
                PerformLayout (this, nameof (MaximumContentWidth));
            }
        }

        /// <summary>Where a column capped by <see cref="MaximumContentWidth"/> sits in a wider panel. <see cref="StackAlignment.Center"/> by default.</summary>
        [DefaultValue (StackAlignment.Center)]
        public StackAlignment ContentAlignment {
            get => contentAlignment;
            set {
                ValidateAlignment (value);

                if (contentAlignment == value)
                    return;

                contentAlignment = value;
                PerformLayout (this, nameof (ContentAlignment));
            }
        }

        /// <summary>How a child is placed across the stack when it has no <see cref="SetAlignment"/> of its own. <see cref="StackAlignment.Stretch"/> by default.</summary>
        [DefaultValue (StackAlignment.Stretch)]
        public StackAlignment ChildAlignment {
            get => childAlignment;
            set {
                ValidateAlignment (value);

                if (childAlignment == value)
                    return;

                childAlignment = value;
                PerformLayout (this, nameof (ChildAlignment));
            }
        }

        bool IExtenderProvider.CanExtend (object obj) => obj is Control control && control.Parent == this;

        /// <summary>Gets how <paramref name="child"/> is placed across the stack: its own setting, or <see cref="ChildAlignment"/> when it has none.</summary>
        [DefaultValue (StackAlignment.Stretch)]
        [DisplayName ("StackAlignment")]
        public StackAlignment GetAlignment (Control child)
        {
            Guard.ThrowIfNull (child);

            return StackLayout.GetAlignment (child) ?? childAlignment;
        }

        /// <summary>Sets how <paramref name="child"/> is placed across the stack, overriding <see cref="ChildAlignment"/> for it.</summary>
        [DisplayName ("StackAlignment")]
        public void SetAlignment (Control child, StackAlignment value)
        {
            Guard.ThrowIfNull (child);
            ValidateAlignment (value);

            StackLayout.SetAlignment (child, value);
            PerformLayout (child, "StackAlignment");
        }

        private static void ValidateAlignment (StackAlignment value)
        {
            if (!EnumCompat.IsDefined (value))
                throw new InvalidEnumArgumentException (nameof (value), (int)value, typeof (StackAlignment));
        }
    }
}
