using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Platform.Storage;

using AvDragDropEffects = Avalonia.Input.DragDropEffects;
using AvDragEventArgs = Avalonia.Input.DragEventArgs;
using MfDragDropEffects = Majorsilence.Forms.DragDropEffects;

namespace Majorsilence.Forms
{
    /// <summary>
    /// Drags from outside the application -- files from Finder, Explorer or a file manager, text from
    /// another app -- into a Majorsilence.Forms window. Avalonia's drag-and-drop events are translated into
    /// the window's neutral external-drag entry points, which raise DragEnter / DragOver / DragDrop /
    /// DragLeave on the control under the pointer whose <see cref="Control.AllowDrop"/> is set, exactly as
    /// an in-process <c>DoDragDrop</c> does. Files arrive as <see cref="DataFormats.FileDrop"/> (a
    /// <c>string[]</c> of local paths), text as <see cref="DataFormats.Text"/> and <see cref="DataFormats.UnicodeText"/>.
    /// </summary>
    internal static class AvaloniaDropWiring
    {
        internal static void Attach (Window host, WindowBase owner, Func<double> scaling)
        {
            DragDrop.SetAllowDrop (host, true);

            // One data object per drag: built on entry, reused for every drag-over and the drop.
            IDataObject? data = null;

            IDataObject Data (AvDragEventArgs e) => data ??= ToDataObject (e);

            (int X, int Y) Device (AvDragEventArgs e)
            {
                var pos = e.GetPosition (host);
                var scale = scaling ();
                return ((int) (pos.X * scale), (int) (pos.Y * scale));
            }

            void Over (object? sender, AvDragEventArgs e)
            {
                var (x, y) = Device (e);
                var effect = owner.HandleExternalDragOver (Data (e), ToMf (e.DragEffects), x, y, AvaloniaKeyInterop.ModifiersOnly (e.KeyModifiers));
                e.DragEffects = ToAvalonia (effect) & e.DragEffects;
                e.Handled = true;
            }

            host.AddHandler (DragDrop.DragEnterEvent, Over);
            host.AddHandler (DragDrop.DragOverEvent, Over);
            host.AddHandler (DragDrop.DragLeaveEvent, (_, e) => {
                owner.HandleExternalDragLeave ();
                data = null;
            });
            host.AddHandler (DragDrop.DropEvent, (_, e) => {
                var (x, y) = Device (e);
                var effect = owner.HandleExternalDrop (Data (e), ToMf (e.DragEffects), x, y, AvaloniaKeyInterop.ModifiersOnly (e.KeyModifiers));
                e.DragEffects = ToAvalonia (effect) & e.DragEffects;
                e.Handled = true;
                data = null;
            });
        }

        private static DataObject ToDataObject (AvDragEventArgs e)
        {
            var result = new DataObject ();

            if (e.DataTransfer.TryGetFiles () is { } items) {
                var paths = items.Select (i => i.TryGetLocalPath ()).OfType<string> ().ToArray ();
                if (paths.Length > 0)
                    result.SetData (DataFormats.FileDrop.Name, paths);
            }

            if (e.DataTransfer.TryGetText () is { } text) {
                result.SetData (DataFormats.Text.Name, text);
                result.SetData (DataFormats.UnicodeText.Name, text);
                result.SetData (typeof (string), text);
            }

            return result;
        }

        // Copy, Move and Link have the same bits in both (WinForms' values).
        private static MfDragDropEffects ToMf (AvDragDropEffects effects)
        {
            var result = MfDragDropEffects.None;
            if (effects.HasFlag (AvDragDropEffects.Copy)) result |= MfDragDropEffects.Copy;
            if (effects.HasFlag (AvDragDropEffects.Move)) result |= MfDragDropEffects.Move;
            if (effects.HasFlag (AvDragDropEffects.Link)) result |= MfDragDropEffects.Link;
            return result;
        }

        private static AvDragDropEffects ToAvalonia (MfDragDropEffects effects)
        {
            var result = AvDragDropEffects.None;
            if (effects.HasFlag (MfDragDropEffects.Copy)) result |= AvDragDropEffects.Copy;
            if (effects.HasFlag (MfDragDropEffects.Move)) result |= AvDragDropEffects.Move;
            if (effects.HasFlag (MfDragDropEffects.Link)) result |= AvDragDropEffects.Link;
            return result;
        }
    }
}
