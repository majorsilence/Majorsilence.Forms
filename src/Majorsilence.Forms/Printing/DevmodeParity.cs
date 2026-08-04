using System;

namespace Majorsilence.Forms.Printing
{
    // The DEVMODE/DEVNAMES interop on the printing settings (docs/gdi-gap-plan.md).
    //
    // A DEVMODE is a Win32 structure the print spooler allocates and a driver fills in; DEVNAMES is
    // the driver/device/port triple beside it. Both are handles to memory owned by the Windows
    // printing subsystem, which is not something this layer can produce or read.
    //
    // They follow the same rule as the rest of the handle interop (see
    // Majorsilence.Forms.Drawing/HandleInterop.cs): the members that would have to *produce* a handle
    // throw rather than return IntPtr.Zero, because a zero handle passed to GlobalLock or
    // DocumentProperties fails obscurely somewhere else, while a throw names the line that caused it.
    // The members that would have to *read* one throw for the same reason -- there is nothing behind
    // the pointer, so any settings applied would be silently wrong rather than visibly absent.

    public sealed partial class PageSettings
    {
        /// <summary>Copies these page settings into a Win32 DEVMODE structure.</summary>
        /// <exception cref="PlatformNotSupportedException">Always. See the note in DevmodeParity.cs.</exception>
        public void CopyToHdevmode (IntPtr hdevmode) => throw NoDevmode (nameof (CopyToHdevmode));

        /// <summary>Applies a Win32 DEVMODE structure to these page settings.</summary>
        /// <exception cref="PlatformNotSupportedException">Always. See the note in DevmodeParity.cs.</exception>
        public void SetHdevmode (IntPtr hdevmode) => throw NoDevmode (nameof (SetHdevmode));

        internal static PlatformNotSupportedException NoDevmode (string member) => new (
            $"{member} works on a Win32 DEVMODE, which the Windows print spooler owns. Set the properties "
            + "on PageSettings and PrinterSettings directly instead.");
    }

    public sealed partial class PrinterSettings
    {
        /// <summary>Creates a Win32 DEVMODE structure from these printer settings.</summary>
        /// <exception cref="PlatformNotSupportedException">Always. See the note in DevmodeParity.cs.</exception>
        public IntPtr GetHdevmode () => throw PageSettings.NoDevmode (nameof (GetHdevmode));

        /// <inheritdoc cref="GetHdevmode()"/>
        public IntPtr GetHdevmode (PageSettings pageSettings) => throw PageSettings.NoDevmode (nameof (GetHdevmode));

        /// <summary>Creates a Win32 DEVNAMES structure from these printer settings.</summary>
        /// <exception cref="PlatformNotSupportedException">Always. See the note in DevmodeParity.cs.</exception>
        public IntPtr GetHdevnames () => throw PageSettings.NoDevmode (nameof (GetHdevnames));

        /// <summary>Applies a Win32 DEVMODE structure to these printer settings.</summary>
        /// <exception cref="PlatformNotSupportedException">Always. See the note in DevmodeParity.cs.</exception>
        public void SetHdevmode (IntPtr hdevmode) => throw PageSettings.NoDevmode (nameof (SetHdevmode));

        /// <summary>Applies a Win32 DEVNAMES structure to these printer settings.</summary>
        /// <exception cref="PlatformNotSupportedException">Always. See the note in DevmodeParity.cs.</exception>
        public void SetHdevnames (IntPtr hdevnames) => throw PageSettings.NoDevmode (nameof (SetHdevnames));
    }
}
