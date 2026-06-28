using Majorsilence.Forms;

namespace Majorsilence.Forms
{
    public class MenuStripClickThrough : MenuStrip
    {
        protected override void WndProc(ref Message m)
        {
            ClickThroughHelper.HandleMouseActivate(this, ref m);
            base.WndProc(ref m);
        }
    }
}
