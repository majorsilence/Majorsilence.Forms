using System.Drawing;
using SkiaSharp;

namespace Majorsilence.Forms
{
    internal sealed class ControlAdapter : ScrollableControl
    {
        private Control? selected_control;

        public ControlAdapter (WindowBase parent)
        {
            ParentForm = parent;
            SetControlBehavior (ControlBehaviors.Selectable, false);
        }

        // We need to override this because the ControlAdapter doesn't need to be scaled
        public override Rectangle ClientRectangle {
            get {
                var x = CurrentStyle.Border.Left.GetWidth ();
                var y = CurrentStyle.Border.Top.GetWidth ();
                var w = Width - CurrentStyle.Border.Right.GetWidth () - x;
                var h = Height - CurrentStyle.Border.Bottom.GetWidth () - y;
                return new Rectangle (x, y, w, h);
            }
        }

        public new WindowBase ParentForm { get; }

        // The Adapter is given the Form's native surface including any managed Form borders, and it
        // needs to not draw on top of those borders -- that is, it often has to start drawing at
        // (1, 1) instead of (0, 0). This could probably be eliminated in the future with
        // Canvas.Translate.
        internal override Point ChildPaintOffset {
            get {
                var form_border = ParentForm.CurrentStyle.Border;
                var scaling = Scaling;

                return new Point (
                    (int)(form_border.Left.GetWidth () * scaling),
                    (int)(form_border.Top.GetWidth () * scaling));
            }
        }

        public override bool Visible {
            get => ParentForm != null;
            set { }
        }

        // Raised when the focused (selected) control changes. The single choke-point for keyboard focus,
        // consumed by AutomationObserver to feed UI Automation focus-changed events.
        internal event EventHandler<Control?>? SelectedControlChanged;

        internal Control? SelectedControl {
            get => selected_control;
            set {
                if (selected_control == value)
                    return;

                selected_control?.Deselect ();

                if (value is ControlAdapter)
                    return;

                // Note they could be setting this to null
                selected_control = value;
                SelectedControlChanged?.Invoke (this, selected_control);
                selected_control?.Select ();
            }
        }

        internal void RaiseParentVisibleChanged (EventArgs e)
        {
            OnParentVisibleChanged (e);
        }
    }
}
