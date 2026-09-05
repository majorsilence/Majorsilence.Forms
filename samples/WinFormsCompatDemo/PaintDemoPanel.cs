using System.Windows.Forms;

namespace WinFormsCompatDemo
{
    // A hand-written custom control (not designer-generated) exercising the event-shadowing pass:
    // overriding the compat-typed OnPaint hook and calling base.OnPaint -- which is what makes the
    // shadowed Paint event still fire for anyone who also subscribes with `+=` -- through unmodified
    // `using System.Windows.Forms;` source. See RESULTS.md's "event shadowing" section.
    public class PaintDemoPanel : Panel
    {
        public int PaintCount { get; private set; }

        protected override void OnPaint(PaintEventArgs e)
        {
            PaintCount++;
            base.OnPaint(e);
        }
    }
}
