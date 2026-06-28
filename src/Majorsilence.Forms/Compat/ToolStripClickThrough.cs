using Majorsilence.Forms;

namespace Majorsilence.Forms
{
    public class ToolStripClickThrough : ToolStrip
    {
        protected override void WndProc(ref Message m)
        {
            ClickThroughHelper.HandleMouseActivate(this, ref m);
            base.WndProc(ref m);
        }
    }
}
