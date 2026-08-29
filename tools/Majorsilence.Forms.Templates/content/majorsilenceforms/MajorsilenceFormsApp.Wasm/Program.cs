using System.Threading.Tasks;
using Majorsilence.Forms;

namespace MajorsilenceFormsApp.Wasm
{
    public class Program
    {
        // No [STAThread]/blocking Application.Run: the browser backend starts asynchronously (attaching
        // to the "out" div in wwwroot/index.html) and, once started, is driven by the browser's own
        // JS event loop.
        private static Task Main (string[] args) => Application.RunBrowserAsync (() => new MainForm ());
    }
}
