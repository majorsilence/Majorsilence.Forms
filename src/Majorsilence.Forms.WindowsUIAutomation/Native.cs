using System;
using System.Runtime.InteropServices;

namespace Majorsilence.Forms.WindowsUIAutomation
{
    // Minimal Win32 interop for window subclassing. We subclass the host HWND (which the Avalonia/Uno
    // backend owns) so we can answer WM_GETOBJECT with our UI Automation provider while leaving the
    // backend's own window procedure intact (DefSubclassProc chains to it).
    internal static class Native
    {
        public const int WM_GETOBJECT = 0x003D;

        // lParam value WM_GETOBJECT carries when a UI Automation client is requesting the root provider.
        public const int UiaRootObjectId = -25;

        public delegate IntPtr SubclassProc (
            IntPtr hWnd, uint uMsg, IntPtr wParam, IntPtr lParam, UIntPtr uIdSubclass, IntPtr dwRefData);

        [DllImport ("comctl32.dll", SetLastError = true)]
        public static extern bool SetWindowSubclass (
            IntPtr hWnd, SubclassProc pfnSubclass, UIntPtr uIdSubclass, IntPtr dwRefData);

        [DllImport ("comctl32.dll", SetLastError = true)]
        public static extern bool RemoveWindowSubclass (
            IntPtr hWnd, SubclassProc pfnSubclass, UIntPtr uIdSubclass);

        [DllImport ("comctl32.dll")]
        public static extern IntPtr DefSubclassProc (IntPtr hWnd, uint uMsg, IntPtr wParam, IntPtr lParam);
    }
}
