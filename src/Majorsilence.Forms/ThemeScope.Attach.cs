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
                Theme.BumpScopedRules ();   // class and id rules read the scope the control resolves

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

        // ThemeScope.Current when this window was created: the scope of the control whose click, menu or
        // key handler opened it. Used when nothing in the owner chain has a scope -- a scoped control
        // inside an unscoped window opens a dialog owned by that window, or by no window at all when it
        // is hosted in another toolkit, and the dialog should still look like the control.
        private readonly ThemeScope? opened_under_scope;

        /// <summary>
        /// Gets or sets a theme for this window in place of the application-wide <see cref="Theme"/>. When
        /// null (the default) a dialog uses its owner's, an MDI child its parent's, and a drop-down or tool
        /// tip the scope of the control it opened for. Failing those, a window created while a scope was
        /// current -- a dialog opened from a scoped control's event handler -- uses that scope.
        /// </summary>
        public ThemeScope? ThemeScope {
            get => theme_scope;
            set {
                if (ReferenceEquals (theme_scope, value))
                    return;

                theme_scope?.Detach (this);
                theme_scope = value;
                value?.Attach (this);
                Theme.BumpScopedRules ();   // class and id rules read the scope the control resolves

                Majorsilence.Forms.ThemeScope.NotifyThemeChanged (this);
            }
        }

        internal ThemeScope? ResolveThemeScope ()
        {
            if (theme_scope is not null)
                return theme_scope;

            var inherited = this switch {
                Form { Owner: { } owner } => owner.ResolveThemeScope (),
                Form { MdiParent: { } mdiParent } => mdiParent.ResolveThemeScope (),
                PopupWindow popup => popup.ResolveOwnerThemeScope (),
                _ => scope_dialog_parent?.ResolveThemeScope (),
            };

            return inherited ?? opened_under_scope;
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
