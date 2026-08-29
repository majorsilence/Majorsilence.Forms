using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using Win32 = Microsoft.Win32;
using Majorsilence.Forms.Backends;
using MF = Majorsilence.Forms;

namespace Majorsilence.Forms.Wpf
{
    /// <summary>
    /// Shows the backend-neutral file/folder picker requests through the WPF common dialogs
    /// (<c>Microsoft.Win32</c>). Shared by <see cref="WpfWindowHost"/> and
    /// <see cref="MajorsilenceFormsPresenter"/>.
    /// </summary>
    internal static class WpfDialogs
    {
        internal static string[] ShowOpenFileDialog (Window? owner, OpenFileRequest request)
        {
            var dialog = new Win32.OpenFileDialog
            {
                Multiselect = request.AllowMultiple,
                Filter = BuildFilter (request.Filters),
            };
            if (!string.IsNullOrEmpty (request.Title))
                dialog.Title = request.Title;
            if (!string.IsNullOrEmpty (request.InitialDirectory))
                dialog.InitialDirectory = request.InitialDirectory;

            var ok = owner is not null ? dialog.ShowDialog (owner) : dialog.ShowDialog ();
            return ok == true ? dialog.FileNames : Array.Empty<string> ();
        }

        internal static string? ShowSaveFileDialog (Window? owner, SaveFileRequest request)
        {
            var dialog = new Win32.SaveFileDialog
            {
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

            var ok = owner is not null ? dialog.ShowDialog (owner) : dialog.ShowDialog ();
            return ok == true ? dialog.FileName : null;
        }

        internal static string? ShowOpenFolderDialog (Window? owner, FolderDialogRequest request)
        {
            var dialog = new Win32.OpenFolderDialog ();   // WPF (.NET 8+)
            if (!string.IsNullOrEmpty (request.Title))
                dialog.Title = request.Title;
            if (!string.IsNullOrEmpty (request.InitialDirectory))
                dialog.InitialDirectory = request.InitialDirectory;

            var ok = owner is not null ? dialog.ShowDialog (owner) : dialog.ShowDialog ();
            return ok == true ? dialog.FolderName : null;
        }

        // "Name|*.a;*.b|Name2|*.c" — the Win32 common-dialog filter format (same as WinForms).
        private static string BuildFilter (IReadOnlyList<FileDialogFilter> filters)
        {
            if (filters.Count == 0)
                return "All files (*.*)|*.*";

            var parts = new List<string> (filters.Count);
            foreach (var filter in filters)
            {
                var patterns = filter.Patterns.Count > 0 ? string.Join (";", filter.Patterns) : "*.*";
                parts.Add ($"{filter.Name}|{patterns}");
            }
            return string.Join ("|", parts);
        }
    }

    /// <summary>
    /// Positions a hosted native WPF element over the Skia surface — bounds and clip arrive in logical
    /// pixels relative to the Majorsilence.Forms client origin, which for WPF are DIPs, so no scaling
    /// conversion is needed (unlike the WinForms backend). Shared by <see cref="WpfWindowHost"/> and
    /// <see cref="MajorsilenceFormsPresenter"/>.
    /// </summary>
    internal static class NativeOverlay
    {
        internal static void Attach (Canvas layer, Dictionary<MF.NativeControlHost, FrameworkElement> overlays,
            MF.NativeControlHost host, object nativeControl)
        {
            if (nativeControl is not FrameworkElement element)
                return;

            if (overlays.TryGetValue (host, out var existing) && !ReferenceEquals (existing, element))
                layer.Children.Remove (existing);

            overlays[host] = element;
            if (!layer.Children.Contains (element))
                layer.Children.Add (element);
        }

        internal static void Update (Dictionary<MF.NativeControlHost, FrameworkElement> overlays,
            MF.NativeControlHost host, System.Drawing.Rectangle logicalBounds, System.Drawing.Rectangle clipBounds, bool visible)
        {
            if (!overlays.TryGetValue (host, out var element))
                return;

            Canvas.SetLeft (element, logicalBounds.X);
            Canvas.SetTop (element, logicalBounds.Y);
            element.Width = logicalBounds.Width;
            element.Height = logicalBounds.Height;
            element.Visibility = visible ? Visibility.Visible : Visibility.Collapsed;

            if (clipBounds == logicalBounds)
            {
                element.Clip = null;
            }
            else
            {
                element.Clip = new System.Windows.Media.RectangleGeometry (new Rect (
                    clipBounds.X - logicalBounds.X, clipBounds.Y - logicalBounds.Y,
                    Math.Max (0, clipBounds.Width), Math.Max (0, clipBounds.Height)));
            }
        }

        internal static void Detach (Canvas layer, Dictionary<MF.NativeControlHost, FrameworkElement> overlays, MF.NativeControlHost host)
        {
            if (overlays.TryGetValue (host, out var element))
            {
                overlays.Remove (host);
                layer.Children.Remove (element);
            }
        }
    }
}
