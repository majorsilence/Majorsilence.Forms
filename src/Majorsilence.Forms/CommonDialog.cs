using System;
using System.ComponentModel;
using System.Threading.Tasks;

namespace Majorsilence.Forms
{
    /// <summary>
    /// WinForms compatibility: specifies the base class used for displaying dialog boxes on screen.
    /// In Majorsilence.Forms, dialogs are implemented as Form subclasses; this class provides compatibility.
    /// </summary>
    public abstract partial class CommonDialog : Component
    {
        /// <summary>Gets or sets an arbitrary object that provides additional data about the dialog. Stub in Majorsilence.Forms.</summary>
        public object? Tag { get; set; }

        /// <summary>When overridden in a derived class, resets the properties of a common dialog box to their default values.</summary>
        public abstract void Reset ();

        /// <summary>Runs a common dialog box with a default owner.</summary>
        public DialogResult ShowDialog () => ShowDialog (null);

        /// <summary>Runs a common dialog box with the specified owner. Override in derived classes.</summary>
        public virtual DialogResult ShowDialog (IWin32Window? owner) => RunDialog (owner) ? DialogResult.OK : DialogResult.Cancel;

        /// <summary>Runs the dialog with a default owner, without blocking the caller.</summary>
        /// <returns>A task that completes with <see cref="DialogResult.OK"/> or <see cref="DialogResult.Cancel"/>.</returns>
        public Task<DialogResult> ShowDialogAsync () => ShowDialogAsync (null);

        /// <summary>Runs the dialog with the specified owner, without blocking the caller.</summary>
        /// <inheritdoc cref="ShowDialogAsync()" path="/returns"/>
        /// <remarks>Not an upstream member: the browser target cannot block (issue #406), so a dialog
        /// derived from this class needs a form that can be awaited. It runs <see cref="RunDialogAsync"/>,
        /// which a derived dialog overrides to show its UI with <see cref="Form.ShowDialogAsync()"/>.</remarks>
        public async Task<DialogResult> ShowDialogAsync (IWin32Window? owner)
            => await RunDialogAsync (owner).ConfigureAwait (true) ? DialogResult.OK : DialogResult.Cancel;

        /// <summary>
        /// When overridden in a derived class, runs the dialog without blocking. Returns true if the user
        /// clicked OK, false if cancelled.
        /// </summary>
        /// <remarks>The default runs the synchronous <see cref="RunDialog(IWin32Window)"/>, which is right
        /// for a dialog that has nothing to wait for and wrong for one that shows a modal form: override
        /// this for that one, or it blocks (and fails on the browser) just as <see cref="ShowDialog()"/> does.</remarks>
        protected virtual Task<bool> RunDialogAsync (IWin32Window? hwndOwner) => Task.FromResult (RunDialog (hwndOwner));

        /// <summary>
        /// When overridden in a derived class, specifies the common dialog box.
        /// Returns true if the user clicked OK, false if cancelled.
        /// </summary>
        protected virtual bool RunDialog (IWin32Window? hwndOwner) => RunDialog (hwndOwner?.Handle ?? IntPtr.Zero);

        /// <summary>Raised after the dialog is closed.</summary>
        public event EventHandler? HelpRequest;

        /// <summary>Raises the HelpRequest event.</summary>
        protected virtual void OnHelpRequest (EventArgs e) => HelpRequest?.Invoke (this, e);
    }
}
