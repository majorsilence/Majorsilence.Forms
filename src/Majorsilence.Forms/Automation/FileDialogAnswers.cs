using System;
using System.Collections.Generic;

namespace Majorsilence.Forms.Automation
{
    /// <summary>
    /// Answers for the next file dialogs, queued by a test or an automation client before the action that
    /// opens one.
    /// </summary>
    /// <remarks>
    /// A desktop backend shows the operating system's own picker, which is outside the application's
    /// automation tree, so a script driving the app could reach every control except the one that picks a
    /// file. When an answer is queued, the next <see cref="OpenFileDialog"/> or <see cref="SaveFileDialog"/>
    /// takes it instead of showing the picker, and then runs exactly as after a real pick: FileOk, the
    /// default extension, FilterIndex. With nothing queued, dialogs show the picker as always.
    /// </remarks>
    public static class FileDialogAnswers
    {
        private static readonly object gate = new ();
        private static readonly Queue<string[]> open = new ();
        private static readonly Queue<string?> save = new ();

        /// <summary>Queues the files the next open-file dialog returns. No paths answers it with Cancel.</summary>
        public static void EnqueueOpen (params string[] paths)
        {
            Guard.ThrowIfNull (paths);
            lock (gate)
                open.Enqueue ((string[]) paths.Clone ());
        }

        /// <summary>Queues the file the next save-file dialog returns. Null answers it with Cancel.</summary>
        public static void EnqueueSave (string? path)
        {
            lock (gate)
                save.Enqueue (path);
        }

        /// <summary>The number of open-file answers not yet taken.</summary>
        public static int PendingOpen { get { lock (gate) return open.Count; } }

        /// <summary>The number of save-file answers not yet taken.</summary>
        public static int PendingSave { get { lock (gate) return save.Count; } }

        /// <summary>Discards every queued answer.</summary>
        public static void Clear ()
        {
            lock (gate) {
                open.Clear ();
                save.Clear ();
            }
        }

        internal static bool TryTakeOpen (out string[] paths)
        {
            lock (gate) {
                if (open.Count > 0) {
                    paths = open.Dequeue ();
                    return true;
                }
            }
            paths = Array.Empty<string> ();
            return false;
        }

        internal static bool TryTakeSave (out string? path)
        {
            lock (gate) {
                if (save.Count > 0) {
                    path = save.Dequeue ();
                    return true;
                }
            }
            path = null;
            return false;
        }
    }
}
