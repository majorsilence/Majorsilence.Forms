using System;
using System.Threading;
using Majorsilence.Forms.Headless;

namespace Majorsilence.Forms.Tests;

internal static class AudioTestSupport
{
    /// <summary>
    /// Empties the headless backend's audio request queue and returns once a quiet moment has passed with nothing new in it. A looping
    /// <c>SoundPlayer</c> respawns on a background task that <c>Stop ()</c> only asks to end, so an earlier test's loop can still be
    /// writing when the next one starts; clearing once and asserting at once raced it (#379). Bounded, so a loop that never ends fails the
    /// test that follows instead of hanging.
    /// </summary>
    public static void WaitForQuiet ()
    {
        var deadline = DateTime.UtcNow.AddSeconds (3);

        do {
            HeadlessRenderer.ClearAudioRequests ();
            Thread.Sleep (30);
        } while (HeadlessRenderer.AudioRequests.Count > 0 && DateTime.UtcNow < deadline);
    }
}
