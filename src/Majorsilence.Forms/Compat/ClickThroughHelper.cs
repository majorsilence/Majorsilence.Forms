using Majorsilence.Forms;

namespace Majorsilence.Forms
{
    internal static class ClickThroughHelper
    {
        private const int WM_MOUSEACTIVATE = 0x21;

        public static void HandleMouseActivate(Control control, ref Message m)
        {
            if (m.Msg == WM_MOUSEACTIVATE && control.CanFocus && !control.Focused)
                control.Focus();
        }
    }
}
