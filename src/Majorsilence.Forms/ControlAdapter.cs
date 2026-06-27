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

        protected override void OnPaint (PaintEventArgs e)
        {
            // We have this special version for the Adapter because it is
            // given the Form's native surface including any managed Form
            // borders, and it needs to not draw on top of those borders.
            // That is, this often needs to start drawing at (1, 1) instead of (0, 0)
            // This could probably eliminated in the future with Canvas.Translate.
            var form_border = ParentForm.CurrentStyle.Border;

            var scaling = Scaling;
            var form_x = (int)(form_border.Left.GetWidth () * scaling);
            var form_y = (int)(form_border.Top.GetWidth ()  * scaling);

            foreach (var control in Controls.GetAllControls ().Where (c => c.Visible).ToArray ()) {
                if (control.Width <= 0 || control.Height <= 0)
                    continue;

                var info = new SKImageInfo (control.ScaledSize.Width, control.ScaledSize.Height, SKImageInfo.PlatformColorType, SKAlphaType.Premul);
                var buffer = control.GetBackBuffer ();

                if (control.NeedsPaint) {
                    using (var canvas = new SKCanvas (buffer)) {
                        // start drawing
                        var args = new PaintEventArgs (info, canvas, Scaling);

                        control.RaisePaintBackground (args);
                        control.RaisePaint (args);

                        canvas.Flush ();
                    }
                }

                e.Canvas.DrawBitmap (buffer, form_x + control.ScaledLeft, form_y + control.ScaledTop);
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
