namespace Majorsilence.Forms
{
    public partial class Control
    {
        private ThemeScope? theme_scope;

        /// <summary>
        /// Gets or sets a theme for this control and everything inside it, in place of the application-wide
        /// <see cref="Theme"/>; null (the default) inherits the parent's, and at the top the window's.
        /// </summary>
        public ThemeScope? ThemeScope {
            get => theme_scope;
            set {
                if (ReferenceEquals (theme_scope, value))
                    return;

                theme_scope?.Detach (this);
                theme_scope = value;
                value?.Attach (this);

                Majorsilence.Forms.ThemeScope.NotifyThemeChanged (this);
            }
        }

        /// <summary>The scope this control paints under: its own, else the nearest ancestor's or its window's.</summary>
        internal ThemeScope? ResolveThemeScope ()
        {
            for (Control? c = this; c is not null; c = c.Parent) {
                if (c.theme_scope is { } scope)
                    return scope;
                if (c is ControlAdapter adapter)
                    return adapter.ParentForm?.ResolveThemeScope ();
            }

            return null;
        }
    }

    public partial class WindowBase
    {
        private ThemeScope? theme_scope;

        // The window a dialog was shown over (ShowDialog); its scope is the dialog's when it has none.
        // Kept after the dialog closes, unlike Form's own dialog_parent, so a reshown dialog keeps it.
        internal WindowBase? scope_dialog_parent;

        /// <summary>
        /// Gets or sets a theme for this window in place of the application-wide <see cref="Theme"/>. When
        /// null (the default) a dialog uses its owner's, an MDI child its parent's, and a drop-down or tool
        /// tip the scope of the control it opened for.
        /// </summary>
        public ThemeScope? ThemeScope {
            get => theme_scope;
            set {
                if (ReferenceEquals (theme_scope, value))
                    return;

                theme_scope?.Detach (this);
                theme_scope = value;
                value?.Attach (this);

                Majorsilence.Forms.ThemeScope.NotifyThemeChanged (this);
            }
        }

        internal ThemeScope? ResolveThemeScope ()
        {
            if (theme_scope is not null)
                return theme_scope;

            switch (this) {
                case Form { Owner: { } owner }:
                    return owner.ResolveThemeScope ();
                case Form { MdiParent: { } mdiParent }:
                    return mdiParent.ResolveThemeScope ();
                case PopupWindow popup:
                    return popup.ResolveOwnerThemeScope ();
            }

            return scope_dialog_parent?.ResolveThemeScope ();
        }
    }

    public partial class PopupWindow
    {
        // The control the popup opened for decides, so a combo box inside a scoped panel of an unscoped
        // window gets a scoped drop-down; failing that, the window it belongs to.
        internal ThemeScope? ResolveOwnerThemeScope ()
            => ScopeOwner?.ResolveThemeScope ()
                ?? (AccessibleOwner as Control)?.ResolveThemeScope ()
                ?? ParentWindow?.ResolveThemeScope ();
    }
}
