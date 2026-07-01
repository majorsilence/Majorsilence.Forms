using Majorsilence.Forms;

namespace ControlGallery;

public class BasePanel : Panel
{
    public virtual void UnloadPanel () { }
    public virtual void LoadPanel () { }
}
