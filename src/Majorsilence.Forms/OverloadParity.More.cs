using System;
using System.Collections;
using System.ComponentModel;
using System.Drawing;
using System.Linq;
using System.Threading.Tasks;

namespace Majorsilence.Forms
{
    // The rest of the overload parity pass — see OverloadParity.cs for what the pass is for.

    public partial class ContainerControl
    {
        /// <summary>Validates the child controls, limited to those the constraints select.</summary>
        /// <remarks>The constraints are honoured rather than accepted and dropped: a caller asking for
        /// <see cref="ValidationConstraints.Enabled"/> gets disabled children skipped, which is the
        /// difference between this and calling the no-argument form.</remarks>
        public bool ValidateChildren (ValidationConstraints validationConstraints)
            => ValidateChildrenCore (Controls, validationConstraints);

        // Upstream declares this on ContainerControl and lets Form and UserControl inherit it. Here
        // Form derives from WindowBase and UserControl from Panel, so neither is a ContainerControl;
        // they each declare the overload and call this rather than carrying a second copy of the rule.
        internal static bool ValidateChildrenCore (Control.ControlCollection children, ValidationConstraints validationConstraints)
        {
            foreach (var child in children) {
                if (validationConstraints.HasFlag (ValidationConstraints.Enabled) && !child.Enabled)
                    continue;
                if (validationConstraints.HasFlag (ValidationConstraints.Visible) && !child.Visible)
                    continue;
                if (validationConstraints.HasFlag (ValidationConstraints.TabStop) && !child.TabStop)
                    continue;
                if (validationConstraints.HasFlag (ValidationConstraints.Selectable) && !child.CanSelect)
                    continue;

                if (!child.Validate ())
                    return false;
            }

            return true;
        }
    }

    public partial class TextBoxBase
    {
        /// <summary>Selects a range of text.</summary>
        public void Select (int start, int length)
        {
            SelectionStart = start;
            SelectionLength = length;
        }
    }

    public partial class UpDownBase
    {
        /// <summary>Selects a range of text in the editable portion.</summary>
        /// <remarks>Routed through the hosted editor when the derived control has one. The base has
        /// no text box of its own -- NumericUpDown and DomainUpDown each own theirs -- so this is the
        /// seam rather than a second copy of the selection state.</remarks>
        public void Select (int start, int length)
        {
            if (Controls.OfType<TextBoxBase> ().FirstOrDefault () is { } editor)
                editor.Select (start, length);
        }
    }

    public partial class ToolStripTextBox
    {
        /// <summary>Selects a range of text in the hosted text box.</summary>
        public new void Select (int start, int length) => TextBox.Select (start, length);
    }

    public partial class ToolStripComboBox
    {
        /// <summary>Selects a range of text in the hosted combo box's editable portion.</summary>
        public new void Select (int start, int length) => ComboBox.Select (start, length);
    }

    public partial class ToolStripItem
    {
        /// <summary>Starts a drag operation with an explicit drag image.</summary>
        /// <remarks>The image and offset are accepted and ignored, as on <see cref="Control"/>: there
        /// is no OS drag source in this layer, and the two-argument form reports that honestly.</remarks>
        public DragDropEffects DoDragDrop (object data, DragDropEffects allowedEffects,
            Majorsilence.Forms.Drawing.Bitmap? dragImage, Point cursorOffset, bool useDefaultDragImage)
            => DoDragDrop (data, allowedEffects);
    }

    public partial class ProgressBar
    {
        /// <summary>Advances the value by the given amount, clamped to the bar's range.</summary>
        public void Increment (int value) => Increment ((int?)value);
    }

    public partial class TreeNode
    {
        /// <summary>Collapses this node, and optionally only this node rather than its children too.</summary>
        public void Collapse (bool ignoreChildren)
        {
            Collapse ();

            if (ignoreChildren)
                return;

            foreach (var child in Nodes)
                child.Collapse (ignoreChildren: false);
        }
    }
}
