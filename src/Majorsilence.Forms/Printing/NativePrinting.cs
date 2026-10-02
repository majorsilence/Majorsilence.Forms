using System.Diagnostics;

namespace Majorsilence.Forms.Printing
{
    /// <summary>
    /// Hands a rendered PDF to the operating system's printing: CUPS' <c>lp</c> on macOS and Linux, and the
    /// shell's <c>print</c> / <c>printto</c> verb on Windows (SVC-29). <see cref="PrintDocument.Print"/> used
    /// to write the PDF to the temp folder and stop there, so a migrated Print button did nothing at all.
    /// </summary>
    internal static class NativePrinting
    {
        // Test seams: a launcher that sees the command instead of running it, returning whether the launch
        // succeeded. LauncherOverride is scoped to the calling flow (one test), DefaultLauncher applies
        // everywhere else (a test assembly's "never reach a real printer").
        internal static Func<ProcessStartInfo, bool>? DefaultLauncher;

        private static readonly System.Threading.AsyncLocal<Func<ProcessStartInfo, bool>?> scoped_launcher = new ();

        internal static Func<ProcessStartInfo, bool>? LauncherOverride {
            get => scoped_launcher.Value ?? DefaultLauncher;
            set => scoped_launcher.Value = value;
        }

        internal static ProcessStartInfo Command (string pdf, string? printer)
        {
            if (OperatingSystemCompat.IsWindows ()) {
                var verb = string.IsNullOrEmpty (printer) ? "print" : "printto";
                return new ProcessStartInfo (pdf) {
                    UseShellExecute = true,
                    Verb = verb,
                    Arguments = string.IsNullOrEmpty (printer) ? string.Empty : $"\"{printer}\"",
                    CreateNoWindow = true,
                };
            }

            var lp = new ProcessStartInfo ("lp") { UseShellExecute = false, CreateNoWindow = true };

            if (!string.IsNullOrEmpty (printer)) {
                AddArgument (lp, "-d");
                AddArgument (lp, printer!);
            }

            AddArgument (lp, pdf);
            return lp;
        }

        // ArgumentList quotes for us; netstandard2.0 has only the Arguments string (as NativeAudio does it).
        private static void AddArgument (ProcessStartInfo info, string argument)
        {
#if NETSTANDARD2_0
            var quoted = argument.Length > 0 && argument.IndexOf (' ') < 0 && argument.IndexOf ('"') < 0
                ? argument
                : "\"" + argument.Replace ("\"", "\\\"") + "\"";
            info.Arguments = string.IsNullOrEmpty (info.Arguments) ? quoted : info.Arguments + " " + quoted;
#else
            info.ArgumentList.Add (argument);
#endif
        }

        /// <exception cref="InvalidPrinterException">The job could not be submitted -- no print system, or
        /// no such printer -- as upstream throws when it has no printer to print to.</exception>
        internal static void Submit (string pdf, PrinterSettings settings)
        {
            var command = Command (pdf, settings.PrinterName);
            bool started;

            try {
                if (LauncherOverride is { } launcher) {
                    started = launcher (command);
                } else {
                    using var process = Process.Start (command);
                    started = process is not null;
                }
            } catch (Exception ex) when (ex is System.ComponentModel.Win32Exception or InvalidOperationException) {
                throw new InvalidPrinterException ($"The document could not be sent to the printer: {ex.Message}", ex);
            }

            if (!started)
                throw new InvalidPrinterException (settings);
        }
    }
}
