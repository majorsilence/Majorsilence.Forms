namespace Majorsilence.Forms
{
    /// <summary>
    /// A minimal cross-platform stand-in for <c>Microsoft.VisualBasic.Devices.ComputerInfo</c> — the
    /// backing type for classic VB's <c>My.Computer</c>. <c>My.Computer.Name</c> is by far the only
    /// member of <c>My.Computer</c> observed in real migrated code, so this deliberately implements only
    /// <see cref="Name"/>; the rest of <c>My.Computer</c> (Registry, Clipboard, FileSystem, Info's other
    /// OS-version members, …) is genuinely Windows-specific or otherwise out of scope and is not shimmed
    /// here — see MIGRATION.md.
    /// <code>
    /// Namespace My
    ///     Friend Module MyComputer
    ///         Friend ReadOnly Property Computer As Majorsilence.Forms.ComputerInfo = New Majorsilence.Forms.ComputerInfo()
    ///     End Module
    /// End Namespace
    /// </code>
    /// </summary>
    public sealed class ComputerInfo
    {
        /// <summary>Gets the machine's network name. Forwards to <see cref="Environment.MachineName"/>.</summary>
        public string Name => Environment.MachineName;
    }
}
