using System.Drawing;
using Majorsilence.Forms.Backends;
using WF = System.Windows.Forms;
using MF = Majorsilence.Forms;

namespace Majorsilence.Forms.WinForms
{
    /// <summary>
    /// Shows the backend-neutral file/folder picker requests through the native WinForms common
    /// dialogs. Shared by <see cref="WinFormsWindowHost"/> and <see cref="MajorsilenceFormsPresenter"/>.
    /// </summary>
    internal static class WinFormsDialogs
    {
        internal static string[] ShowOpenFileDialog (WF.IWin32Window? owner, OpenFileRequest request)
        {
            using var dialog = new WF.OpenFileDialog {
                Multiselect = request.AllowMultiple,
                Filter = BuildFilter (request.Filters),
            };
            if (!string.IsNullOrEmpty (request.Title))
                dialog.Title = request.Title;
            if (!string.IsNullOrEmpty (request.InitialDirectory))
                dialog.InitialDirectory = request.InitialDirectory;

            return dialog.ShowDialog (owner) == WF.DialogResult.OK
                ? dialog.FileNames
                : Array.Empty<string> ();
        }

        internal static string? ShowSaveFileDialog (WF.IWin32Window? owner, SaveFileRequest request)
        {
            using var dialog = new WF.SaveFileDialog {
                Filter = BuildFilter (request.Filters),
            };
            if (!string.IsNullOrEmpty (request.Title))
                dialog.Title = request.Title;
            if (!string.IsNullOrEmpty (request.InitialDirectory))
                dialog.InitialDirectory = request.InitialDirectory;
            if (!string.IsNullOrEmpty (request.SuggestedFileName))
                dialog.FileName = request.SuggestedFileName;
            if (!string.IsNullOrEmpty (request.DefaultExtension))
                dialog.DefaultExt = request.DefaultExtension;

            return dialog.ShowDialog (owner) == WF.DialogResult.OK ? dialog.FileName : null;
        }

        internal static string? ShowOpenFolderDialog (WF.IWin32Window? owner, FolderDialogRequest request)
        {
            using var dialog = new WF.FolderBrowserDialog ();
            if (!string.IsNullOrEmpty (request.Title))
                dialog.Description = request.Title;
            if (!string.IsNullOrEmpty (request.InitialDirectory))
#if NET48
                // FolderBrowserDialog.InitialDirectory is .NET Core 3.0+; SelectedPath is the
                // .NET Framework way to preselect a starting folder.
                dialog.SelectedPath = request.InitialDirectory;
#else
                dialog.InitialDirectory = request.InitialDirectory;
#endif

            return dialog.ShowDialog (owner) == WF.DialogResult.OK ? dialog.SelectedPath : null;
        }

        // "Name|*.a;*.b|Name2|*.c" — the WinForms filter string format.
        private static string BuildFilter (System.Collections.Generic.IReadOnlyList<FileDialogFilter> filters)
        {
            if (filters.Count == 0)
                return "All files (*.*)|*.*";

            var parts = new System.Collections.Generic.List<string> (filters.Count * 2);
            foreach (var filter in filters) {
                var patterns = filter.Patterns.Count > 0 ? string.Join (";", filter.Patterns) : "*.*";
                parts.Add ($"{filter.Name}|{patterns}");
            }
            return string.Join ("|", parts);
        }
    }

    /// <summary>
    /// Positions a hosted native WinForms control over the Skia surface — bounds and clip arrive in
    /// logical pixels relative to the Majorsilence.Forms client origin; WinForms wants physical.
    /// Shared by <see cref="WinFormsWindowHost"/> and <see cref="MajorsilenceFormsPresenter"/>.
    /// </summary>
    internal static class NativeOverlay
    {
        internal static void Update (
            System.Collections.Generic.Dictionary<MF.NativeControlHost, WF.Control> overlays,
            MF.NativeControlHost host, Rectangle logicalBounds, Rectangle clipBounds, bool visible, double scaling)
        {
            if (!overlays.TryGetValue (host, out var control))
                return;

            static Rectangle Scale (Rectangle r, double s) => new (
                (int) Math.Round (r.X * s), (int) Math.Round (r.Y * s),
                (int) Math.Round (r.Width * s), (int) Math.Round (r.Height * s));

            var bounds = Scale (logicalBounds, scaling);
            var clip = Scale (clipBounds, scaling);

            control.SetBounds (bounds.X, bounds.Y, bounds.Width, bounds.Height);
            control.Visible = visible;

            // Clip to the visible viewport (local to the control). Null when fully visible.
            if (clip == bounds) {
                control.Region?.Dispose ();
                control.Region = null;
            } else {
                var local = new Rectangle (clip.X - bounds.X, clip.Y - bounds.Y, clip.Width, clip.Height);
                control.Region?.Dispose ();
                control.Region = new System.Drawing.Region (local);
            }
        }
    }
}
