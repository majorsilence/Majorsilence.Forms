using System.Collections.Generic;
using System.Text;

namespace Majorsilence.Forms
{
    /// <summary>
    /// How serious a <see cref="ThemeCssDiagnostic"/> is. Errors mean a declaration or rule was
    /// dropped; warnings mean the stylesheet applied as written but probably not as intended.
    /// </summary>
    public enum ThemeCssSeverity
    {
        /// <summary>The stylesheet applied, but something in it looks like a mistake (e.g. an unused variable).</summary>
        Warning,

        /// <summary>The offending declaration or rule was ignored.</summary>
        Error,

        /// <summary>
        /// Nothing is wrong with the stylesheet; the message tells the author something about how it
        /// applies. Used by host appliers (e.g. the System.Windows.Forms one) to report selectors and
        /// properties their toolkit has no counterpart for -- the sheet stays valid for every host.
        /// </summary>
        Info
    }

    /// <summary>
    /// One problem found while parsing a CSS theme (<see cref="ThemeStyleSheet.Parse"/>). Messages are
    /// written to be actionable by a person or a coding assistant: they name the offending text, say
    /// why it is not supported, and where possible suggest the nearest supported spelling.
    /// </summary>
    public sealed class ThemeCssDiagnostic
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="ThemeCssDiagnostic"/> class.
        /// </summary>
        public ThemeCssDiagnostic (ThemeCssSeverity severity, int line, int column, string message)
        {
            Severity = severity;
            Line = line;
            Column = column;
            Message = message;
        }

        /// <summary>Whether this is an error (dropped) or a warning (applied, but suspicious).</summary>
        public ThemeCssSeverity Severity { get; }

        /// <summary>The 1-based line of the offending text.</summary>
        public int Line { get; }

        /// <summary>The 1-based column of the offending text.</summary>
        public int Column { get; }

        /// <summary>What is wrong and, where possible, what to write instead.</summary>
        public string Message { get; }

        /// <inheritdoc/>
        public override string ToString ()
            => $"{Severity switch { ThemeCssSeverity.Error => "error", ThemeCssSeverity.Info => "info", _ => "warning" }} ({Line}:{Column}): {Message}";
    }

    /// <summary>
    /// The exception thrown when a CSS theme has errors and was registered or loaded through one of the
    /// throwing <see cref="Theme"/> entry points (<see cref="Theme.RegisterThemeCss"/>,
    /// <see cref="Theme.LoadFromCss"/>). <see cref="Diagnostics"/> carries every problem found, not just
    /// the first, so a single round-trip is enough to fix a stylesheet.
    /// </summary>
    public sealed class ThemeCssException : Exception
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="ThemeCssException"/> class with a single message.
        /// </summary>
        public ThemeCssException (string message) : base (message)
        {
            Diagnostics = new[] { new ThemeCssDiagnostic (ThemeCssSeverity.Error, 0, 0, message) };
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="ThemeCssException"/> class from the diagnostics of
        /// a parse.
        /// </summary>
        public ThemeCssException (IReadOnlyList<ThemeCssDiagnostic> diagnostics) : base (BuildMessage (diagnostics))
        {
            Diagnostics = diagnostics;
        }

        /// <summary>Every problem found in the stylesheet.</summary>
        public IReadOnlyList<ThemeCssDiagnostic> Diagnostics { get; }

        private static string BuildMessage (IReadOnlyList<ThemeCssDiagnostic> diagnostics)
        {
            var sb = new StringBuilder ("The CSS theme has errors:");

            foreach (var d in diagnostics)
                if (d.Severity == ThemeCssSeverity.Error)
                    sb.Append ("\n  ").Append (d);

            return sb.ToString ();
        }
    }
}
