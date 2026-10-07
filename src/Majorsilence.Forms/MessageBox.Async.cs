using System.Threading.Tasks;

namespace Majorsilence.Forms
{
    // The awaitable twin of every MessageBox.Show overload (issue #406). Show blocks its caller in a
    // nested modal loop until the box is closed; ShowAsync shows the same box, owned and laid out the
    // same way, and returns at once with a task that completes with the button the user chose. That is
    // the only form that works on the browser target, where nothing can block the page's one thread, and
    // it is equally valid on every desktop backend -- the box is modal either way, because the modality
    // (owner disabled, Application.ModalStack) lives in Form.ShowDialogAsync, not in the waiting.
    //
    // The arguments mean exactly what they mean to the matching Show overload, including the ones that
    // are accepted for source compatibility and have no effect here (help file, keyword, navigator).

    public static partial class MessageBox
    {
        // Every ShowAsync overload lands here, as every Show overload lands in ShowCore.
        private static Task<DialogResult> ShowCoreAsync (Form? owner, string text, string caption, MessageBoxButtons buttons,
            MessageBoxIcon icon, MessageBoxDefaultButton defaultButton, MessageBoxOptions options)
        {
            var (parent, form) = Prepare (owner, text, caption, buttons, icon, defaultButton, options);

            return form.ShowDialogAsync (parent);
        }

        /// <summary>Shows a message box with the specified text, without blocking the caller.</summary>
        /// <returns>A task that completes with the button the user chose when the box closes.</returns>
        public static Task<DialogResult> ShowAsync (string text)
            => ShowAsync (text, string.Empty, MessageBoxButtons.OK, MessageBoxIcon.None);

        /// <summary>Shows a message box with the specified text and caption, without blocking the caller.</summary>
        /// <inheritdoc cref="ShowAsync(string)" path="/returns"/>
        public static Task<DialogResult> ShowAsync (string text, string caption)
            => ShowAsync (text, caption, MessageBoxButtons.OK, MessageBoxIcon.None);

        /// <summary>Shows a message box with the specified text, caption, and buttons, without blocking the caller.</summary>
        /// <inheritdoc cref="ShowAsync(string)" path="/returns"/>
        public static Task<DialogResult> ShowAsync (string text, string caption, MessageBoxButtons buttons)
            => ShowAsync (text, caption, buttons, MessageBoxIcon.None);

        /// <summary>Shows a message box with the specified text, caption, buttons, and icon, without blocking the caller.</summary>
        /// <inheritdoc cref="ShowAsync(string)" path="/returns"/>
        public static Task<DialogResult> ShowAsync (string text, string caption, MessageBoxButtons buttons, MessageBoxIcon icon)
            => ShowCoreAsync (null, text, caption, buttons, icon, DefaultButton, MessageBoxOptions.None);

        /// <summary>Shows a message box with text, caption, buttons, icon, and default button, without blocking the caller.</summary>
        /// <inheritdoc cref="ShowAsync(string)" path="/returns"/>
        public static Task<DialogResult> ShowAsync (string text, string caption, MessageBoxButtons buttons, MessageBoxIcon icon,
            MessageBoxDefaultButton defaultButton)
            => ShowCoreAsync (null, text, caption, buttons, icon, defaultButton, MessageBoxOptions.None);

        /// <summary>Shows a message box with text, caption, buttons, icon, default button, and options, without blocking the caller.</summary>
        /// <inheritdoc cref="ShowAsync(string)" path="/returns"/>
        public static Task<DialogResult> ShowAsync (string text, string caption, MessageBoxButtons buttons, MessageBoxIcon icon,
            MessageBoxDefaultButton defaultButton, MessageBoxOptions options)
            => ShowCoreAsync (null, text, caption, buttons, icon, defaultButton, options);

        /// <inheritdoc cref="ShowAsync(string,string,MessageBoxButtons,MessageBoxIcon,MessageBoxDefaultButton,MessageBoxOptions)"/>
        public static Task<DialogResult> ShowAsync (string text, string caption, MessageBoxButtons buttons, MessageBoxIcon icon,
            MessageBoxDefaultButton defaultButton, MessageBoxOptions options, bool displayHelpButton)
            => ShowCoreAsync (null, text, caption, buttons, icon, defaultButton, options);

        /// <inheritdoc cref="ShowAsync(string,string,MessageBoxButtons,MessageBoxIcon,MessageBoxDefaultButton,MessageBoxOptions)"/>
        public static Task<DialogResult> ShowAsync (string text, string caption, MessageBoxButtons buttons, MessageBoxIcon icon,
            MessageBoxDefaultButton defaultButton, MessageBoxOptions options, string helpFilePath)
            => ShowCoreAsync (null, text, caption, buttons, icon, defaultButton, options);

        /// <inheritdoc cref="ShowAsync(string,string,MessageBoxButtons,MessageBoxIcon,MessageBoxDefaultButton,MessageBoxOptions)"/>
        public static Task<DialogResult> ShowAsync (string text, string caption, MessageBoxButtons buttons, MessageBoxIcon icon,
            MessageBoxDefaultButton defaultButton, MessageBoxOptions options, string helpFilePath, string keyword)
            => ShowCoreAsync (null, text, caption, buttons, icon, defaultButton, options);

        /// <inheritdoc cref="ShowAsync(string,string,MessageBoxButtons,MessageBoxIcon,MessageBoxDefaultButton,MessageBoxOptions)"/>
        public static Task<DialogResult> ShowAsync (string text, string caption, MessageBoxButtons buttons, MessageBoxIcon icon,
            MessageBoxDefaultButton defaultButton, MessageBoxOptions options, string helpFilePath, HelpNavigator navigator)
            => ShowCoreAsync (null, text, caption, buttons, icon, defaultButton, options);

        /// <inheritdoc cref="ShowAsync(string,string,MessageBoxButtons,MessageBoxIcon,MessageBoxDefaultButton,MessageBoxOptions)"/>
        public static Task<DialogResult> ShowAsync (string text, string caption, MessageBoxButtons buttons, MessageBoxIcon icon,
            MessageBoxDefaultButton defaultButton, MessageBoxOptions options, string helpFilePath, HelpNavigator navigator, object param)
            => ShowCoreAsync (null, text, caption, buttons, icon, defaultButton, options);

        // ── Owned by a form ──

        /// <summary>Shows a message box owned by the given form, without blocking the caller.</summary>
        /// <inheritdoc cref="ShowAsync(string)" path="/returns"/>
        public static Task<DialogResult> ShowAsync (Form owner, string text)
            => ShowAsync (owner, text, string.Empty, MessageBoxButtons.OK, MessageBoxIcon.None);

        /// <inheritdoc cref="ShowAsync(Form,string)"/>
        public static Task<DialogResult> ShowAsync (Form owner, string text, string caption)
            => ShowAsync (owner, text, caption, MessageBoxButtons.OK, MessageBoxIcon.None);

        /// <inheritdoc cref="ShowAsync(Form,string)"/>
        public static Task<DialogResult> ShowAsync (Form owner, string text, string caption, MessageBoxButtons buttons)
            => ShowAsync (owner, text, caption, buttons, MessageBoxIcon.None);

        /// <inheritdoc cref="ShowAsync(Form,string)"/>
        public static Task<DialogResult> ShowAsync (Form owner, string text, string caption, MessageBoxButtons buttons, MessageBoxIcon icon)
            => ShowCoreAsync (owner, text, caption, buttons, icon, DefaultButton, MessageBoxOptions.None);

        // ── Owned by any window (a form, or a control's form) ──

        /// <summary>Shows a message box owned by the given window, without blocking the caller.</summary>
        /// <remarks>A control owner means the form it is on, as for the matching <c>Show</c> overload.</remarks>
        /// <inheritdoc cref="ShowAsync(string)" path="/returns"/>
        public static Task<DialogResult> ShowAsync (IWin32Window owner, string text)
            => ShowAsync (owner, text, string.Empty, MessageBoxButtons.OK, MessageBoxIcon.None);

        /// <inheritdoc cref="ShowAsync(IWin32Window,string)"/>
        public static Task<DialogResult> ShowAsync (IWin32Window owner, string text, string caption)
            => ShowAsync (owner, text, caption, MessageBoxButtons.OK, MessageBoxIcon.None);

        /// <inheritdoc cref="ShowAsync(IWin32Window,string)"/>
        public static Task<DialogResult> ShowAsync (IWin32Window owner, string text, string caption, MessageBoxButtons buttons)
            => ShowAsync (owner, text, caption, buttons, MessageBoxIcon.None);

        /// <inheritdoc cref="ShowAsync(IWin32Window,string)"/>
        public static Task<DialogResult> ShowAsync (IWin32Window owner, string text, string caption, MessageBoxButtons buttons, MessageBoxIcon icon)
            => ShowCoreAsync (OwnerForm (owner), text, caption, buttons, icon, DefaultButton, MessageBoxOptions.None);

        /// <inheritdoc cref="ShowAsync(IWin32Window,string)"/>
        public static Task<DialogResult> ShowAsync (IWin32Window owner, string text, string caption, MessageBoxButtons buttons,
            MessageBoxIcon icon, MessageBoxDefaultButton defaultButton)
            => ShowCoreAsync (OwnerForm (owner), text, caption, buttons, icon, defaultButton, MessageBoxOptions.None);

        /// <inheritdoc cref="ShowAsync(IWin32Window,string)"/>
        public static Task<DialogResult> ShowAsync (IWin32Window owner, string text, string caption, MessageBoxButtons buttons,
            MessageBoxIcon icon, MessageBoxDefaultButton defaultButton, MessageBoxOptions options)
            => ShowCoreAsync (OwnerForm (owner), text, caption, buttons, icon, defaultButton, options);

        /// <inheritdoc cref="ShowAsync(IWin32Window,string)"/>
        public static Task<DialogResult> ShowAsync (IWin32Window owner, string text, string caption, MessageBoxButtons buttons,
            MessageBoxIcon icon, MessageBoxDefaultButton defaultButton, MessageBoxOptions options, string helpFilePath)
            => ShowCoreAsync (OwnerForm (owner), text, caption, buttons, icon, defaultButton, options);

        /// <inheritdoc cref="ShowAsync(IWin32Window,string)"/>
        public static Task<DialogResult> ShowAsync (IWin32Window owner, string text, string caption, MessageBoxButtons buttons,
            MessageBoxIcon icon, MessageBoxDefaultButton defaultButton, MessageBoxOptions options, string helpFilePath, string keyword)
            => ShowCoreAsync (OwnerForm (owner), text, caption, buttons, icon, defaultButton, options);

        /// <inheritdoc cref="ShowAsync(IWin32Window,string)"/>
        public static Task<DialogResult> ShowAsync (IWin32Window owner, string text, string caption, MessageBoxButtons buttons,
            MessageBoxIcon icon, MessageBoxDefaultButton defaultButton, MessageBoxOptions options, string helpFilePath, HelpNavigator navigator)
            => ShowCoreAsync (OwnerForm (owner), text, caption, buttons, icon, defaultButton, options);

        /// <inheritdoc cref="ShowAsync(IWin32Window,string)"/>
        public static Task<DialogResult> ShowAsync (IWin32Window owner, string text, string caption, MessageBoxButtons buttons,
            MessageBoxIcon icon, MessageBoxDefaultButton defaultButton, MessageBoxOptions options, string helpFilePath,
            HelpNavigator navigator, object param)
            => ShowCoreAsync (OwnerForm (owner), text, caption, buttons, icon, defaultButton, options);
    }
}
