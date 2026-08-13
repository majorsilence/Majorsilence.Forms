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

        /// <summary>
        /// Lays the window's children out, then lets the window itself lay out.
        /// </summary>
        /// <remarks>
        /// The adapter is the Control standing in for the Form, so its layout pass IS the Form's.
        /// Base first: a Form's OnLayout override generally positions children relative to the ones
        /// the normal dock/anchor pass has just placed, so it has to see their settled bounds.
        /// </remarks>
        protected override void OnLayout (LayoutEventArgs e)
        {
            base.OnLayout (e);

            // Only once the window has actually been shown. Adding controls during a Form subclass's
            // construction lays the adapter out, and forwarding that would invoke the subclass's
            // OnLayout override before its own constructor has run -- a real consumer (DockPanelSuite's
            // FloatWindow) reads a collection there that its constructor has not created yet and throws.
            // WinForms does not raise a Form's layout before it has a handle either.
            if (ParentForm is { shown: true } window)
                window.RaiseLayout (e);
        }

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
