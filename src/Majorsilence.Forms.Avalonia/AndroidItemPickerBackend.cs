#if ANDROID
using System;
using System.Collections.Generic;
using System.Linq;
using Android.App;
using Android.Content;

namespace Majorsilence.Forms.Backends
{
    /// <summary>
    /// Android's item picker: an <see cref="AlertDialog"/> listing the items with the current one marked, on the
    /// current <see cref="Activity"/> (the same <see cref="AvaloniaPlatformBackend.CurrentAndroidActivity"/> the other
    /// Android capabilities use). Choosing an item answers and closes; tapping outside or Back answers -1. It is full
    /// width and scrolls, which is why it replaces the small drop-down a finger keeps missing (#438).
    /// </summary>
    internal sealed class AndroidItemPickerBackend
    {
        public bool IsAvailable => AvaloniaPlatformBackend.CurrentAndroidActivity is { IsFinishing: false, IsDestroyed: false };

        public bool Show (string? title, IReadOnlyList<string> items, int selectedIndex, Action<int> completed)
        {
            if (AvaloniaPlatformBackend.CurrentAndroidActivity is not { IsFinishing: false, IsDestroyed: false } activity)
                return false;

            var answered = false;
            void Answer (int index)
            {
                if (answered)
                    return;

                answered = true;
                completed (index);
            }

            var builder = new AlertDialog.Builder (activity);
            if (!string.IsNullOrEmpty (title))
                builder.SetTitle (title);

            builder.SetSingleChoiceItems (items.ToArray (), selectedIndex, (sender, e) => {
                // Answered before dismissing, so the dismissal that follows is not taken for a cancel.
                Answer (e.Which);
                (sender as IDialogInterface)?.Dismiss ();
            });

            // Back, a tap outside the dialog, and anything else that closes it without a choice.
            builder.SetOnDismissListener (new DismissListener (() => Answer (-1)));

            builder.Show ();
            return true;
        }

        private sealed class DismissListener (Action dismissed) : Java.Lang.Object, IDialogInterfaceOnDismissListener
        {
            public void OnDismiss (IDialogInterface? dialog) => dismissed ();
        }
    }
}
#endif
