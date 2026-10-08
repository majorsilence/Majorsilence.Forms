using System.Drawing;

namespace Majorsilence.Forms.Backends
{
    /// <summary>
    /// Optional window-backend capability: where and how large the window is when the platform maximizes it.
    /// </summary>
    /// <remarks>
    /// Upstream applies <c>Form.MaximizedBounds</c> by answering <c>WM_GETMINMAXINFO</c>
    /// (<c>ptMaxPosition</c>/<c>ptMaxSize</c>, Form.cs WmGetMinMaxInfoHelper), so the window manager does the
    /// maximizing and the form stays in the <c>Maximized</c> state. Only a backend whose platform has an
    /// equivalent hint can honour it; the WinForms host does, through the native form's own
    /// <c>MaximizedBounds</c>. Avalonia, GTK 4, WPF, Uno and the single-view hosts have no maximize-bounds
    /// hint, so they do not implement this and a maximized form fills the work area there. Kept off
    /// <see cref="IWindowBackend"/> so backends without the hint need nothing.
    /// </remarks>
    internal interface IMaximizedBoundsBackend
    {
        /// <summary>Sets the screen bounds the window takes when maximized; empty restores the default.</summary>
        void SetMaximizedBounds (Rectangle bounds);
    }
}
