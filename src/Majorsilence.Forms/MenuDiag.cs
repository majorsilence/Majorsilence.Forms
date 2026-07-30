namespace Majorsilence.Forms
{
    // TEMPORARY diagnostic logging for tracking down a real, reported bug: Menu/MenuStrip drop downs
    // flash open then immediately close on Avalonia. Opt-in via MAJORSILENCE_MENU_DEBUG=1 so it's
    // silent for everyone else. Remove once the root cause is confirmed and fixed.
    internal static class MenuDiag
    {
        private static readonly bool Enabled = Environment.GetEnvironmentVariable ("MAJORSILENCE_MENU_DEBUG") == "1";
        private static readonly DateTime Start = DateTime.Now;

        public static void Log (string message)
        {
            if (!Enabled)
                return;

            var elapsed = (DateTime.Now - Start).TotalMilliseconds;
            Console.Error.WriteLine ($"[MENUDIAG +{elapsed,8:F1}ms] {message}");
        }
    }
}
