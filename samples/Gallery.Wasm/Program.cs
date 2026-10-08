using System;
using System.Linq;
using System.Threading.Tasks;
using ControlGallery;
using Gallery.Checks;
using Majorsilence.Forms;

namespace Gallery.Wasm
{
    public class Program
    {
        // No [STAThread]/blocking Application.Run here: the browser backend starts asynchronously
        // (attaching to the "out" div in wwwroot/index.html) and, once started, is driven entirely by
        // the browser's own JS event loop rather than a blocking main loop on this thread.
        //
        // wwwroot/main.js passes the page URL as the only argument. "?check=<name>" swaps the gallery
        // for ModalCheckForm, the browser-head check of the blocking modal patterns (issue #406).
        private static Task Main (string[] args)
        {
            var check = CheckName (args.FirstOrDefault ());

            return check is null
                ? Application.RunBrowserAsync (() => new MainForm ())
                : Application.RunBrowserAsync (() => new ModalCheckForm (check));
        }

        private static string? CheckName (string? url)
        {
            if (url is null || !Uri.TryCreate (url, UriKind.Absolute, out var uri))
                return null;

            foreach (var pair in uri.Query.TrimStart ('?').Split ('&', StringSplitOptions.RemoveEmptyEntries)) {
                var parts = pair.Split ('=', 2);

                if (parts.Length == 2 && parts[0] == "check")
                    return Uri.UnescapeDataString (parts[1]);
            }

            return null;
        }
    }
}
