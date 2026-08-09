using System.Drawing;
using SkiaSharp;

namespace Majorsilence.Forms
{
    /// <summary>
    /// The frame that hosts one <see cref="Form"/> inside an ordinary control tree, so the WinForms
    /// idiom
    /// <code>
    /// form.TopLevel = false;
    /// form.Dock = DockStyle.Fill;
    /// panel.Controls.Add (form);
    /// </code>
    /// works here too. It is created for you by <see cref="Control.ControlCollection.Add(Form)"/>;
    /// you never construct one directly.
    /// </summary>
    /// <remarks>
    /// This is the chromeless sibling of <see cref="MdiChildWindow"/>, and works the same way: the
    /// hosted form never owns an on-screen OS window, its content is composited into this control's
    /// own buffer via <see cref="WindowBase.RenderFrame"/>, and input is forwarded inward. The two
    /// differ only in chrome -- an MDI child draws a caption bar and supports move/resize/minimize,
    /// whereas a panel-hosted form fills its frame exactly and has none of that.
    ///
    /// Because RenderFrame re-lays-out the hosted form's adapter to whatever size it is handed, the
    /// host does not have to push its size into the child: docking and anchoring inside the hosted
    /// form follow the frame automatically.
    /// </remarks>
    internal sealed class FormHost : Control
    {
        private SKBitmap? content_buffer;

        public FormHost (Form child)
        {
            ChildForm = child;

            // Selectable so the container's ControlAdapter can make this the SelectedControl, which is
            // what gets keystrokes routed here (and from here into the hosted form) -- see
            // Control.RaiseKeyDown.
            SetControlBehavior (ControlBehaviors.Selectable, true);
        }

        public Form ChildForm { get; }

        // ── Painting ─────────────────────────────────────────────────────────────

        // A size change alone doesn't mark this control dirty, so without this, resizing the frame
        // reallocates the back buffer in the paint walk but never repaints into it -- leaving the
        // hosted form's Dock/Anchor children laid out for the old size. Same reason MdiChildWindow
        // overrides this.
        protected override void OnResize (EventArgs e)
        {
            base.OnResize (e);
            Invalidate ();
        }

        protected override void OnPaint (PaintEventArgs e)
        {
            var w = ScaledWidth;
            var h = ScaledHeight;

            if (w <= 0 || h <= 0)
                return;

            // The hosted form's OnPaintBackground clears the whole canvas, so it has to render into
            // an isolated buffer rather than straight onto the shared one.
            EnsureContentBuffer (w, h);

            using (var canvas = new SKCanvas (content_buffer)) {
                ChildForm.RenderFrame (canvas, w, h, e.Scaling);
                canvas.Flush ();
            }

            e.Canvas.DrawBitmap (content_buffer, 0, 0);
        }

        private void EnsureContentBuffer (int w, int h)
        {
            if (content_buffer is null || content_buffer.Width != w || content_buffer.Height != h) {
                content_buffer?.Dispose ();
                content_buffer = new SKBitmap (new SKImageInfo (w, h, SKImageInfo.PlatformColorType, SKAlphaType.Premul));
            }
        }

        // ── Input ────────────────────────────────────────────────────────────────

        // The whole frame is the hosted form's client area -- there is no chrome to subtract -- so
        // pointer coordinates pass straight through.

        protected override void OnMouseDown (MouseEventArgs e)
        {
            base.OnMouseDown (e);
            ChildForm.HandlePointerPressed (e.Button, e.X, e.Y, e.Modifiers);
        }

        protected override void OnMouseMove (MouseEventArgs e)
        {
            base.OnMouseMove (e);
            ChildForm.HandlePointerMoved (e.Button, e.X, e.Y, e.Modifiers);
        }

        protected override void OnMouseUp (MouseEventArgs e)
        {
            base.OnMouseUp (e);
            ChildForm.HandlePointerReleased (e.Button, e.X, e.Y, e.Modifiers);
        }

        protected override void OnMouseWheel (MouseEventArgs e)
        {
            base.OnMouseWheel (e);
            ChildForm.HandlePointerWheel (e.Button, e.X, e.Y, e.Delta, e.Modifiers);
        }

        protected override void OnMouseLeave (EventArgs e)
        {
            base.OnMouseLeave (e);
            ChildForm.HandlePointerExited (MouseButtons.None, 0, 0, Keys.None);
        }

        // Keyboard arrives via the container adapter's SelectedControl (this host) -- see the
        // FormHost branch in Control.RaiseKeyDown, which forwards ahead of the adapter's own Tab
        // handling so Tab cycles inside the hosted form rather than moving focus in the container.
        internal bool ForwardKeyDown (Keys keys) => ChildForm.HandleKeyDown (keys);

        internal bool ForwardKeyUp (Keys keys) => ChildForm.HandleKeyUp (keys);

        internal bool ForwardTextInput (string text) => ChildForm.HandleTextInput (text);

        // ── Lifetime ─────────────────────────────────────────────────────────────

        // Detaches the hosted form from this frame. Called when the frame leaves its parent's
        // collection (Controls.Remove/Clear) and by Form.Close on the hosted path.
        internal void DetachChild ()
        {
            if (ChildForm.PanelHost == this)
                ChildForm.PanelHost = null;
        }

        protected override void Dispose (bool disposing)
        {
            if (disposing) {
                content_buffer?.Dispose ();
                content_buffer = null;
            }

            base.Dispose (disposing);
        }
    }
}
