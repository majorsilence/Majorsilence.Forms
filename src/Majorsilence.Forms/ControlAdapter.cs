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

        /// <summary>
        /// The focused control, and the single choke point for changing it.
        /// </summary>
        /// <remarks>
        /// Assigning here runs the whole WinForms focus sequence — see <see cref="ChangeFocus"/>. It
        /// used to deselect the old control and then call <c>Select ()</c> on the new one, which called
        /// back into this setter; the mutual recursion is why the event order differed between mouse
        /// and keyboard focus changes.
        /// </remarks>
        internal Control? SelectedControl {
            get => selected_control;
            set {
                if (selected_control == value)
                    return;

                // The adapter is the root standing in for the window; it is never itself "focused".
                if (value is ControlAdapter)
                    return;

                ChangeFocus (value);
            }
        }

        // Guards against a Validating or Enter handler moving focus again while we are still moving it.
        // Upstream has the same problem and solves it the same way (ContainerControl's s_stateValidating).
        private bool changing_focus;

        /// <summary>
        /// Moves focus from the current control to <paramref name="value"/>, running WinForms' sequence:
        /// Leave up the leaving chain, the validation cycle between the two, then Enter down the
        /// entering chain, and finally the LostFocus/GotFocus notifications.
        /// </summary>
        /// <remarks>
        /// <para>
        /// Mirrors <c>ContainerControl.UpdateFocusedControl</c> and <c>EnterValidation</c>. The pieces
        /// that matter, and that the previous implementation had wrong:
        /// </para>
        /// <list type="bullet">
        /// <item>Leave and Enter walk the <em>ancestor path</em>, not just the two leaf controls, so a
        /// Panel or UserControl hears focus entering and leaving it.</item>
        /// <item>Validation runs while focus is still on the leaving control, so cancelling it can
        /// actually keep focus there.</item>
        /// <item>Validation is gated on the <em>entering</em> control's <c>CausesValidation</c> and on
        /// the container's <c>AutoValidate</c>, both of which were previously ignored.</item>
        /// </list>
        /// </remarks>
        private void ChangeFocus (Control? value)
        {
            // A handler that moves focus again re-enters here. Let the innermost move win rather than
            // interleaving two half-finished sequences.
            if (changing_focus) {
                selected_control = value;
                return;
            }

            var leaving = selected_control;
            var entering = value;
            var ancestor = CommonAncestor (leaving, entering);

            changing_focus = true;

            try {
                // 1. Leave, bottom-up from the leaving control to (excluding) the common ancestor.
                for (var c = leaving; c is not null && c != ancestor; c = c.Parent)
                    c.RaiseLeaveOnly ();

                // 2. The validation cycle, still focused on the leaving control so a cancel can hold it.
                if (!RunValidation (leaving, entering, ancestor)) {
                    // Cancelled: focus stays where it was. Re-enter the control we just left so its own
                    // Enter/Leave pairing stays balanced for anything counting them.
                    for (var c = leaving; c is not null && c != ancestor; c = c.Parent)
                        c.RaiseEnterOnly ();

                    return;
                }

                selected_control = entering;

                leaving?.MarkDeselected ();
                entering?.MarkSelected ();

                // 3. The focus notifications and the Enter walk, top-down to the entering control.
                leaving?.RaiseLostFocusOnly ();

                foreach (var c in PathDownTo (entering, ancestor))
                    c.RaiseEnterOnly ();

                entering?.RaiseGotFocus ();

                leaving?.Invalidate ();
                entering?.Invalidate ();
            } finally {
                changing_focus = false;
            }

            SelectedControlChanged?.Invoke (this, selected_control);
        }

        /// <summary>
        /// Runs Validating/Validated on the leaving control and its ancestors up to (excluding) the
        /// common ancestor. Returns false when a handler cancelled and focus must stay put.
        /// </summary>
        private bool RunValidation (Control? leaving, Control? entering, Control? ancestor)
        {
            if (leaving is null)
                return true;

            // Upstream's EnterValidation gates: nothing to validate, the entered control opting out, or
            // auto-validation switched off on the container.
            if (entering is not null && !entering.CausesValidation)
                return true;

            var mode = AutoValidateFor (leaving);

            if (mode == AutoValidate.Disable)
                return true;

            for (var c = leaving; c is not null && c != ancestor; c = c.Parent) {
                if (!c.CausesValidation)
                    continue;

                if (c.RaiseValidation ())
                    continue;

                // A cancel only holds focus in EnablePreventFocusChange, which is the default. In
                // EnableAllowFocusChange the validation still ran and still failed, but focus moves on.
                return mode != AutoValidate.EnablePreventFocusChange;
            }

            return true;
        }

        // The effective AutoValidate for a control: the nearest container that declares one, else the
        // window's.
        private AutoValidate AutoValidateFor (Control control)
        {
            for (var c = control.Parent; c is not null; c = c.Parent) {
                if (c is ContainerControl container)
                    return container.AutoValidate;
            }

            return ParentForm is Form form ? form.AutoValidate : AutoValidate.EnablePreventFocusChange;
        }

        // The deepest control that contains both, or null when they share only this adapter.
        private static Control? CommonAncestor (Control? first, Control? second)
        {
            if (first is null || second is null)
                return null;

            for (var a = first; a is not null; a = a.Parent) {
                for (var b = second; b is not null; b = b.Parent) {
                    if (ReferenceEquals (a, b))
                        return a;
                }
            }

            return null;
        }

        // The chain from just below `ancestor` down to `target`, outermost first, so Enter arrives on a
        // container before the child inside it.
        private static List<Control> PathDownTo (Control? target, Control? ancestor)
        {
            var path = new List<Control> ();

            for (var c = target; c is not null && c != ancestor; c = c.Parent)
                path.Add (c);

            path.Reverse ();
            return path;
        }

        internal void RaiseParentVisibleChanged (EventArgs e)
        {
            OnParentVisibleChanged (e);
        }

        // ── The keyboard pre-processing chain crosses from Control to WindowBase here ─────────────
        //
        // Control's chain bubbles to Parent; the adapter has none, and it is the root control standing
        // in for the window. Upstream the window IS a Control and the walk simply continues, so these
        // four hand the key to WindowBase/Form -- which is where AcceptButton, CancelButton, menu
        // shortcuts and KeyPreview live. Without them the chain would stop one level short of every
        // form-level behaviour it exists to reach.
        //
        // Tab is handled here rather than on the window, mirroring ContainerControl: focus traversal
        // is a property of the container that owns the focus chain, and the adapter is that container.

        protected override bool ProcessCmdKey (ref Message msg, Keys keyData)
            => ParentForm.RaiseProcessCmdKey (ref msg, keyData);

        protected override bool ProcessDialogKey (Keys keyData)
        {
            if ((keyData & Keys.KeyCode) == Keys.Tab && (keyData & (Keys.Alt | Keys.Control)) == Keys.None) {
                if (FindForm () is Form form)
                    form.ShowFocusCues = true;

                SelectNextControl (SelectedControl, (keyData & Keys.Shift) == Keys.None, true, true, true);
                return true;
            }

            return ParentForm.RaiseProcessDialogKey (keyData);
        }

        protected override bool ProcessDialogChar (char charCode)
            => ParentForm.RaiseProcessDialogChar (charCode);

        protected override bool ProcessKeyPreview (ref Message m)
            => ParentForm.RaiseProcessKeyPreview (ref m);
    }
}
