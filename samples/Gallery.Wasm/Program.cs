using System.Threading.Tasks;
using ControlGallery;
using Majorsilence.Forms;

namespace Gallery.Wasm
{
    public class Program
    {
        // No [STAThread]/blocking Application.Run here: the browser backend starts asynchronously
        // (attaching to the "out" div in wwwroot/index.html) and, once started, is driven entirely by
        // the browser's own JS event loop rather than a blocking main loop on this thread.
        private static Task Main (string[] args) => Application.RunBrowserAsync (() => new MainForm ());
    }
}
