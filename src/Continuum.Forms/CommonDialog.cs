using System.ComponentModel;

namespace Continuum.Forms
{
    /// <summary>
    /// WinForms compatibility: specifies the base class used for displaying dialog boxes on screen.
    /// In Continuum.Forms, dialogs are implemented as Form subclasses; this class provides compatibility.
    /// </summary>
    public abstract class CommonDialog : Component
    {
        /// <summary>Gets or sets an arbitrary object that provides additional data about the dialog. Stub in Continuum.Forms.</summary>
        public object? Tag { get; set; }

        /// <summary>When overridden in a derived class, resets the properties of a common dialog box to their default values.</summary>
        public abstract void Reset ();

        /// <summary>Runs a common dialog box with a default owner.</summary>
        public DialogResult ShowDialog () => ShowDialog (null);

        /// <summary>Runs a common dialog box with the specified owner. Override in derived classes.</summary>
        public virtual DialogResult ShowDialog (IWin32Window? owner) => RunDialog (owner) ? DialogResult.OK : DialogResult.Cancel;

        /// <summary>
        /// When overridden in a derived class, specifies the common dialog box.
        /// Returns true if the user clicked OK, false if cancelled.
        /// </summary>
        protected abstract bool RunDialog (IWin32Window? hwndOwner);

        /// <summary>Raised after the dialog is closed.</summary>
        public event EventHandler? HelpRequest;

        /// <summary>Raises the HelpRequest event.</summary>
        protected virtual void OnHelpRequest (EventArgs e) => HelpRequest?.Invoke (this, e);
    }
}
