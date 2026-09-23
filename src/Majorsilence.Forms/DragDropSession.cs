using System;
using System.Drawing;
using System.Threading.Tasks;

namespace Majorsilence.Forms
{
    /// <summary>
    /// The in-process drag-and-drop pipeline behind <see cref="Control.DoDragDrop(object, DragDropEffects)"/> (W6 mechanisms).
    /// </summary>
    /// <remarks>
    /// One session is active at a time. While it is, the window's pointer entry points hand every move
    /// and release here instead of routing them as mouse events: a move finds the drop target under the
    /// pointer -- the deepest visible control whose <see cref="Control.AllowDrop"/> is set, or the form
    /// when none is -- and raises <c>DragEnter</c> / <c>DragOver</c> / <c>DragLeave</c> on it as the
    /// target changes, then <c>GiveFeedback</c> and <c>QueryContinueDrag</c> on the source; the release
    /// asks <c>QueryContinueDrag</c> with <see cref="DragAction.Drop"/> and raises <c>DragDrop</c>; Escape
    /// asks with <see cref="DragAction.Cancel"/>. The effect the target left in the drop's args is the
    /// session's result, which <see cref="Control.DoDragDrop(object, DragDropEffects)"/> returns once the nested loop it runs sees
    /// the session complete. Drags stay inside the process and the window: there is no OLE source.
    /// </remarks>
    internal sealed class DragDropSession
    {
        /// <summary>The session in progress, or null.</summary>
        internal static DragDropSession? Active { get; private set; }

        private readonly Control? source_control;
        private readonly ToolStripItem? source_item;
        private readonly TaskCompletionSource<DragDropEffects> completion = new ();
        private object? target;             // Control or Form
        private DragDropEffects effect;
        private WindowBase? window;

        /// <summary>The pointer's position in the current target control's client coordinates.</summary>
        /// <remarks>What a target reads back with <c>PointToClient (new Point (e.X, e.Y))</c>; kept here
        /// as well so a target that owns items (a strip) can hit-test without the screen round trip.</remarks>
        internal Point TargetClientPoint { get; private set; }

        private DragDropSession (Control? sourceControl, ToolStripItem? sourceItem, IDataObject data, DragDropEffects allowed)
        {
            source_control = sourceControl;
            source_item = sourceItem;
            Data = data;
            AllowedEffects = allowed;
        }

        /// <summary>The data being dragged.</summary>
        internal IDataObject Data { get; }

        /// <summary>The effects the source allows.</summary>
        internal DragDropEffects AllowedEffects { get; }

        /// <summary>Completes with the effect of the drop, or <see cref="DragDropEffects.None"/>.</summary>
        internal Task<DragDropEffects> Completion => completion.Task;

        /// <summary>The finished session's effect, or null while it is still running.</summary>
        internal DragDropEffects? Result
            // Spelled out rather than IsCompletedSuccessfully, which netstandard2.0 does not have.
            => completion.Task.IsCompleted && !completion.Task.IsFaulted && !completion.Task.IsCanceled ? completion.Task.Result : null;

        /// <summary>Starts a session for a control (or an item on a strip) as the source.</summary>
        internal static DragDropSession Begin (Control source, object data, DragDropEffects allowedEffects, ToolStripItem? item = null)
        {
            Guard.ThrowIfNull (source);
            Guard.ThrowIfNull (data);

            // A session already running ends as cancelled: two drags cannot share the pointer.
            Active?.Finish (DragDropEffects.None);

            var session = new DragDropSession (source, item, Wrap (data), allowedEffects);

            Active = session;
            session.window = source.FindWindow ();

            // Upstream releases the mouse capture when a drag begins, so the source's own MouseMove
            // stops seeing the pointer and the targets do.
            if (Control.CaptureHolder is { } holder)
                holder.Capture = false;

            return session;
        }

        // Anything that is not already a data object is wrapped as upstream wraps it: under its type
        // name, and for a string under the Text format too, so both `GetDataPresent (typeof (string))`
        // and `GetDataPresent (DataFormats.Text)` answer.
        private static IDataObject Wrap (object data)
        {
            if (data is IDataObject already)
                return already;

            var wrapped = new DataObject ();

            if (data is string text) {
                wrapped.SetData (DataFormats.Text.Name, text);
                wrapped.SetData (typeof (string), text);
            } else
                wrapped.SetData (data);

            return wrapped;
        }

        // The window's pointer move, in window-logical coordinates.
        internal void Track (WindowBase host, Point location, MouseButtons buttons, Keys keys)
        {
            window = host;

            var key_state = KeyState (buttons, keys);
            var (next, local) = FindTarget (host, location);

            TargetClientPoint = local;

            // Screen coordinates through the TARGET's own conversion, so the target's
            // PointToClient (e.X, e.Y) gives back exactly the point under the pointer.
            var screen = next is Control target_control ? target_control.PointToScreen (local) : host.PointToScreen (location);

            if (!ReferenceEquals (next, target)) {
                if (target is not null)
                    RaiseDragLeave (target);

                target = next;
                effect = DragDropEffects.None;

                if (target is not null) {
                    var enter = new DragEventArgs (Data, key_state, screen.X, screen.Y, AllowedEffects, DragDropEffects.None);
                    RaiseDragEnter (target, enter);
                    effect = enter.Effect & AllowedEffects;
                }
            } else if (target is not null) {
                var over = new DragEventArgs (Data, key_state, screen.X, screen.Y, AllowedEffects, effect);
                RaiseDragOver (target, over);
                effect = over.Effect & AllowedEffects;
            }

            GiveFeedback ();

            var query = new QueryContinueDragEventArgs (key_state, false, DragAction.Continue);
            RaiseQueryContinueDrag (query);

            if (query.Action == DragAction.Drop)
                Drop (key_state, screen);
            else if (query.Action == DragAction.Cancel)
                Cancel ();
        }

        // The window's pointer release: the drop, unless the source says otherwise.
        internal void Release (WindowBase host, Point location, Keys keys)
        {
            Track (host, location, MouseButtons.None, keys);

            if (!ReferenceEquals (Active, this))
                return;

            var key_state = KeyState (MouseButtons.None, keys);
            var query = new QueryContinueDragEventArgs (key_state, false, DragAction.Drop);
            RaiseQueryContinueDrag (query);

            if (query.Action == DragAction.Cancel)
                Cancel ();
            else if (query.Action == DragAction.Drop)
                Drop (key_state, host.PointToScreen (location));
        }

        // Escape while dragging: cancels unless the source's QueryContinueDrag says to go on.
        internal void Escape ()
        {
            var query = new QueryContinueDragEventArgs (0, true, DragAction.Cancel);
            RaiseQueryContinueDrag (query);

            if (query.Action == DragAction.Cancel)
                Cancel ();
            else if (query.Action == DragAction.Drop && window is { } host)
                Drop (0, host.PointToScreen (Point.Empty));
        }

        private void Drop (int keyState, Point screen)
        {
            var result = DragDropEffects.None;

            if (target is not null && effect != DragDropEffects.None) {
                var drop = new DragEventArgs (Data, keyState, screen.X, screen.Y, AllowedEffects, effect);
                RaiseDragDrop (target, drop);
                result = drop.Effect & AllowedEffects;
            } else if (target is not null)
                RaiseDragLeave (target);

            Finish (result);
        }

        private void Cancel ()
        {
            if (target is not null)
                RaiseDragLeave (target);

            Finish (DragDropEffects.None);
        }

        private void Finish (DragDropEffects result)
        {
            if (ReferenceEquals (Active, this))
                Active = null;

            target = null;
            window?.SetDragCursor (Cursors.Arrow);
            completion.TrySetResult (result);
        }

        // Upstream's DragEventArgs.KeyState bits: 1 left, 2 right, 4 shift, 8 ctrl, 16 middle, 32 alt.
        private static int KeyState (MouseButtons buttons, Keys keys)
        {
            var state = 0;

            if (buttons.HasFlag (MouseButtons.Left)) state |= 1;
            if (buttons.HasFlag (MouseButtons.Right)) state |= 2;
            if (keys.HasFlag (Keys.Shift)) state |= 4;
            if (keys.HasFlag (Keys.Control)) state |= 8;
            if (buttons.HasFlag (MouseButtons.Middle)) state |= 16;
            if (keys.HasFlag (Keys.Alt)) state |= 32;

            return state;
        }

        // The drop target under a window-logical point: the deepest visible control there, walked
        // back up to the first that accepts drops; failing that, the form itself when it does.
        private static (object? target, Point local) FindTarget (WindowBase host, Point location)
        {
            // From the window's root control with the window-logical point, exactly as the mouse
            // path routes a move (WindowBase hands the adapter the raw logical point).
            var control = host.ContentControl;
            var local = location;

            while (control.Controls.FindVisibleChildAt (local) is { } child) {
                local = new Point (local.X - child.Left, local.Y - child.Top);
                control = child;
            }

            for (var candidate = control; candidate is not null; candidate = candidate.Parent) {
                if (candidate.AllowDrop && candidate.Enabled)
                    return (candidate, local);

                local = new Point (local.X + candidate.Left, local.Y + candidate.Top);
            }

            return (host is Form { AllowDrop: true } form ? form : null, location);
        }

        private static void RaiseDragEnter (object target, DragEventArgs e)
        {
            if (target is Control control) control.RaiseDragEnter (e);
            else if (target is Form form) form.RaiseDragEnter (e);
        }

        private static void RaiseDragOver (object target, DragEventArgs e)
        {
            if (target is Control control) control.RaiseDragOver (e);
            else if (target is Form form) form.RaiseDragOver (e);
        }

        private static void RaiseDragDrop (object target, DragEventArgs e)
        {
            if (target is Control control) control.RaiseDragDrop (e);
            else if (target is Form form) form.RaiseDragDrop (e);
        }

        private static void RaiseDragLeave (object target)
        {
            if (target is Control control)
                control.RaiseDragLeave (EventArgs.Empty);
        }

        private void GiveFeedback ()
        {
            var feedback = new GiveFeedbackEventArgs (effect, true);

            if (source_item is not null)
                source_item.RaiseGiveFeedback (feedback);
            else
                source_control?.RaiseGiveFeedback (feedback);

            // The default cursors: the no-drop sign where nothing accepts, the arrow otherwise. A
            // source that draws its own says so through UseDefaultCursors and is left alone.
            if (feedback.UseDefaultCursors)
                window?.SetDragCursor (effect == DragDropEffects.None ? Cursors.No : Cursors.Arrow);
        }

        private void RaiseQueryContinueDrag (QueryContinueDragEventArgs e)
        {
            if (source_item is not null)
                source_item.RaiseQueryContinueDrag (e);
            else
                source_control?.RaiseQueryContinueDrag (e);
        }
    }
}
