namespace Majorsilence.Forms
{
    /// <summary>
    /// Specifies the preferred rounding of a <see cref="Form"/>'s corners. Mirrors the WinForms enum
    /// (and the underlying Windows 11 DWM window-corner preference); Majorsilence.Forms stores the
    /// value for source parity — corner rounding is decided by the platform backend or the OS.
    /// </summary>
    public enum FormCornerPreference
    {
        /// <summary>Let the system decide (the default).</summary>
        Default = 0,

        /// <summary>Never round the window's corners.</summary>
        DoNotRound = 1,

        /// <summary>Round the window's corners.</summary>
        Round = 2,

        /// <summary>Round the window's corners with a smaller radius.</summary>
        RoundSmall = 3
    }
}
