using System.Runtime.InteropServices;

namespace Majorsilence.Forms.Theming.WinForms
{
    /// <summary>
    /// The one native-drawn control with a colour seam: a <c>ProgressBar</c> honours its
    /// <c>ForeColor</c> / <c>BackColor</c> (WinForms forwards them as <c>PBM_SETBARCOLOR</c> /
    /// <c>PBM_SETBKCOLOR</c> on every handle creation) only once visual styles are switched off for
    /// its window. Best effort, like <see cref="Dwm"/>.
    /// </summary>
    internal static class NativeMethods
    {
        [DllImport ("uxtheme.dll", ExactSpelling = true, CharSet = CharSet.Unicode)]
        private static extern int SetWindowTheme (IntPtr hwnd, string subAppName, string subIdList);

        /// <summary>Turns visual styles off for one window (<c>SetWindowTheme (hwnd, "", "")</c>).</summary>
        public static void DisableVisualStyles (IntPtr hwnd)
        {
            if (hwnd != IntPtr.Zero)
                _ = SetWindowTheme (hwnd, "", "");
        }
    }
}
