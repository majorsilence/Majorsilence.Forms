namespace Majorsilence.Forms
{
    // A maximized MDI child has no caption of its own (MdiChildWindow.ChromeMerged). As upstream does,
    // its icon goes to the leading edge of the parent's menu bar, its minimize/restore/close buttons to
    // the trailing edge, and its title into the parent's as "Parent - [Child]". Without this a maximized
    // child kept a full caption bar inside the parent, under the parent's own.

    /// <summary>One of the maximized MDI child's controls, merged into its parent's menu bar.</summary>
    internal sealed class MdiControlItem : ToolStripMenuItem
    {
        internal enum Kind { System, Minimize, Restore, Close }

        internal MdiControlItem (Form child, Kind kind)
        {
            Child = child;
            ControlKind = kind;
            Alignment = kind == Kind.System ? ToolStripItemAlignment.Left : ToolStripItemAlignment.Right;
            ToolTipText = kind switch {
                Kind.Minimize => "Minimize",
                Kind.Restore => "Restore Down",
                Kind.Close => "Close",
                _ => string.Empty,
            };

            // The icon opens the child's window menu, as upstream's system menu does.
            if (kind == Kind.System) {
                AddCommand ("&Restore", () => child.WindowState = FormWindowState.Normal);
                if (child.MinimizeBox)
                    AddCommand ("Mi&nimize", () => child.WindowState = FormWindowState.Minimized);
                DropDownItems.Add (new ToolStripSeparator ());
                AddCommand ("&Close", child.Close);
            }
        }

        private void AddCommand (string text, Action action)
        {
            var item = new ToolStripMenuItem (text);
            item.Click += (_, _) => action ();
            DropDownItems.Add (item);
        }

        internal Form Child { get; }

        internal Kind ControlKind { get; }

        /// <summary>Logical width on the strip: upstream's 20px icon cell and 24px button cells.</summary>
        internal int LogicalWidth => ControlKind == Kind.System ? 20 : 24;

        /// <inheritdoc/>
        protected override void OnClick (EventArgs e)
        {
            base.OnClick (e);

            switch (ControlKind) {
                case Kind.Minimize: Child.WindowState = FormWindowState.Minimized; break;
                case Kind.Restore: Child.WindowState = FormWindowState.Normal; break;
                case Kind.Close: Child.Close (); break;
            }
        }
    }

    public partial class Form
    {
        // The items currently merged into the menu bar, and the strip they were merged into.
        private List<MdiControlItem>? merged_mdi_items;
        private MenuStrip? merged_mdi_strip;

        /// <summary>The menu bar a maximized child's controls merge into: <see cref="MainMenuStrip"/>, else
        /// the form's first menu strip. Null when the form has none.</summary>
        internal MenuStrip? MdiMenuStrip
            => IsMdiContainer ? MainMenuStrip ?? Controls.OfType<MenuStrip> ().FirstOrDefault () : null;

        /// <summary>The active child when it is maximized with its chrome merged into this form, else null.</summary>
        internal Form? MergedMdiChild
            => ActiveMdiChild is { MdiHost: { ChromeMerged: true } } child ? child : null;

        /// <summary>Puts the active maximized child's controls into the menu bar and its title into this
        /// form's, or takes them out again.</summary>
        internal void SyncMdiMergedChrome ()
        {
            var child = MergedMdiChild;
            var strip = child is null ? null : MdiMenuStrip;

            var current = merged_mdi_items?.FirstOrDefault ()?.Child;

            if (child != current || strip != merged_mdi_strip) {
                RemoveMergedMdiItems ();

                if (child is not null && strip is not null) {
                    merged_mdi_items = new List<MdiControlItem> ();
                    merged_mdi_strip = strip;

                    var system = new MdiControlItem (child, MdiControlItem.Kind.System);
                    strip.Items.Insert (0, system);
                    merged_mdi_items.Add (system);

                    // Upstream shows each button the child allows, minimize then restore then close.
                    var kinds = new List<MdiControlItem.Kind> ();
                    if (child.MinimizeBox)
                        kinds.Add (MdiControlItem.Kind.Minimize);
                    if (child.MaximizeBox)
                        kinds.Add (MdiControlItem.Kind.Restore);
                    kinds.Add (MdiControlItem.Kind.Close);

                    foreach (var kind in kinds) {
                        var item = new MdiControlItem (child, kind);
                        strip.Items.Add (item);
                        merged_mdi_items.Add (item);
                    }
                }

                strip?.PerformLayout ();
                strip?.Invalidate ();
            }

            ApplyTitle ();
        }

        private void RemoveMergedMdiItems ()
        {
            if (merged_mdi_items is null)
                return;

            foreach (var item in merged_mdi_items)
                merged_mdi_strip?.Items.Remove (item);

            merged_mdi_strip?.PerformLayout ();
            merged_mdi_strip?.Invalidate ();
            merged_mdi_items = null;
            merged_mdi_strip = null;
        }

        /// <summary>The caption shown for this form: its text, with a merged maximized child's appended
        /// in brackets, as upstream composes it.</summary>
        internal string DisplayTitle
            => MergedMdiChild is { } child && !string.IsNullOrEmpty (child.Text)
                ? $"{Text} - [{child.Text}]"
                : Text;

        private void ApplyTitle ()
        {
            var title = DisplayTitle;
            Backend.Title = title;
            TitleBar.Text = title;
        }
    }
}
