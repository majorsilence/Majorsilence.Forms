#if BROWSER
using System;
using System.IO;
using System.Runtime.InteropServices.JavaScript;
using System.Threading.Tasks;
using Majorsilence.Forms.Automation;

namespace Majorsilence.Forms
{
    /// <summary>
    /// Keeps an accessibility DOM next to the browser canvas: one transparent, click-through element per
    /// control, carrying its ARIA role, name, state and bounds, so screen readers, find-in-page and
    /// DOM-based test tools see the UI. See "Accessibility DOM (browser)" in docs/backends.md.
    /// </summary>
    /// <remarks>
    /// Avalonia.Browser (12.1.1) does not provide this: the DOM it creates is the canvas, a native-control
    /// host <c>div</c> and a hidden IME <c>input</c> (its <c>webapp/modules/avalonia/dom.ts</c>,
    /// <c>createAvaloniaHost</c>), with no automation-peer mirror. It could not use one anyway, since
    /// Majorsilence.Forms draws every control into a single Avalonia visual. So the mirror is built here
    /// from the framework's own automation tree (<see cref="AriaDomMirror"/>, host-neutral and unit-tested),
    /// and this class only carries its changes to <c>BrowserAccessibility.js</c>.
    ///
    /// Off with the <c>Majorsilence.Forms.Browser.DisableAccessibilityDom</c> AppContext switch; the live
    /// region alone is off with <c>Majorsilence.Forms.Browser.DisableLiveAnnouncements</c>.
    /// </remarks>
    internal static partial class BrowserAccessibility
    {
        private const string ModuleName = "majorsilence-forms-a11y";
        internal const string DisableSwitch = "Majorsilence.Forms.Browser.DisableAccessibilityDom";
        internal const string DisableAnnouncementsSwitch = "Majorsilence.Forms.Browser.DisableLiveAnnouncements";

        private static AriaDomMirror? mirror;

        [JSImport ("attach", ModuleName)]
        private static partial bool Attach (string hostId);

        [JSImport ("apply", ModuleName)]
        private static partial void Apply (string opsJson);

        [JSImport ("setActive", ModuleName)]
        private static partial void SetActive (string? elementId);

        [JSImport ("announce", ModuleName)]
        private static partial void AnnounceText (string text, bool assertive, string? key);

        /// <summary>Loads the script and starts mirroring. Never throws: an app whose page refuses the
        /// script (a Content-Security-Policy without <c>data:</c> in script-src) still runs, unmirrored.</summary>
        internal static async Task StartAsync (string hostElementId)
        {
            if (mirror is not null || AppContext.TryGetSwitch (DisableSwitch, out var disabled) && disabled)
                return;

            try {
                await JSHost.ImportAsync (ModuleName, ModuleUrl ()).ConfigureAwait (true);

                if (!Attach (hostElementId)) {
                    Console.Error.WriteLine ($"[Majorsilence.Forms] accessibility DOM not started: no element with id '{hostElementId}'.");
                    return;
                }

                var announce = !(AppContext.TryGetSwitch (DisableAnnouncementsSwitch, out var quiet) && quiet);
                mirror = new AriaDomMirror (new Sink (), announce: announce);
                mirror.RequestSync ();
            } catch (Exception ex) {
                Console.Error.WriteLine ($"[Majorsilence.Forms] accessibility DOM not started: {ex.Message}");
            }
        }

        // The module is embedded rather than shipped as a static web asset, so a head project needs no
        // extra file or <script> tag; a data: URL is the one thing JSHost.ImportAsync can load it from.
        private static string ModuleUrl ()
        {
            using var stream = typeof (BrowserAccessibility).Assembly.GetManifestResourceStream ("Majorsilence.Forms.BrowserAccessibility.js")
                ?? throw new InvalidOperationException ("BrowserAccessibility.js is not embedded.");
            using var memory = new MemoryStream ();
            stream.CopyTo (memory);

            return "data:text/javascript;base64," + Convert.ToBase64String (memory.ToArray ());
        }

        private sealed class Sink : IAriaDomSink
        {
            public void Apply (string opsJson) => BrowserAccessibility.Apply (opsJson);

            public void SetActiveDescendant (string? elementId) => SetActive (elementId);

            public void Announce (string text, bool assertive, string? key) => AnnounceText (text, assertive, key);
        }
    }
}
#endif
