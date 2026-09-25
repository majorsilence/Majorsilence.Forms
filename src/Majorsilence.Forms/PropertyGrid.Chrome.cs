using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.ComponentModel.Design;
using System.Drawing;
using System.Linq;

namespace Majorsilence.Forms
{
    // The chrome around the property view: the toolbar, the property tabs and the commands pane
    // (W6 mechanisms). All three were declared and inert before -- the grid painted rows and nothing
    // else, which is what put two dozen of its properties in the stored-only baseline.
    public partial class PropertyGrid
    {
        private readonly ToolStrip toolbar;
        private ToolStripButton? categorised_button;
        private ToolStripButton? alphabetical_button;
        private readonly List<ToolStripButton> tab_buttons = [];

        /// <summary>The toolbar the grid shows above its view.</summary>
        /// <remarks>Its renderer is <see cref="ToolStripRenderer"/> and its button size follows
        /// <see cref="LargeButtons"/> (W6 mechanisms).</remarks>
        internal ToolStrip Toolbar => toolbar;

        private void BuildToolbar ()
        {
            toolbar.Items.Clear ();
            tab_buttons.Clear ();

            // The two sort buttons upstream shows, as checkable toggles that drive PropertySort.
            categorised_button = new ToolStripButton ("Categorized") { CheckOnClick = false };
            categorised_button.Click += (_, _) => PropertySort = PropertySort.CategorizedAlphabetical;

            alphabetical_button = new ToolStripButton ("Alphabetical") { CheckOnClick = false };
            alphabetical_button.Click += (_, _) => PropertySort = PropertySort.Alphabetical;

            toolbar.Items.Add (categorised_button);
            toolbar.Items.Add (alphabetical_button);

            // One button per property tab, plus one for the built-in properties view, which is how a
            // tab is chosen (W6 mechanisms).
            if (property_tabs is { Count: > 0 } tabs) {
                toolbar.Items.Add (new ToolStripSeparator ());

                var properties = new ToolStripButton ("Properties");
                properties.Click += (_, _) => SelectedTab = null;
                toolbar.Items.Add (properties);
                tab_buttons.Add (properties);

                foreach (var tab in tabs) {
                    var button = new ToolStripButton (tab.TabName);
                    var captured = tab;
                    button.Click += (_, _) => SelectedTab = captured;
                    toolbar.Items.Add (button);
                    tab_buttons.Add (button);
                }
            }

            ApplyToolbarState ();
        }

        // LargeButtons sizes the strip's icons, and the checked state follows the current sort and tab.
        private void ApplyToolbarState ()
        {
            toolbar.ImageScalingSize = LargeButtons ? new Size (24, 24) : new Size (16, 16);
            toolbar.Height = LargeButtons ? 34 : 26;

            if (categorised_button is { } categorised)
                categorised.Checked = PropertySort is PropertySort.Categorized or PropertySort.CategorizedAlphabetical;

            if (alphabetical_button is { } alphabetical)
                alphabetical.Checked = PropertySort == PropertySort.Alphabetical;

            // tab_buttons[0] is the built-in properties view (the null tab); the rest follow the
            // collection in order.
            for (var i = 0; i < tab_buttons.Count; i++)
                tab_buttons[i].Checked = i == 0
                    ? _selected_tab is null
                    : property_tabs is { } tabs && i - 1 < tabs.Count && ReferenceEquals (tabs[i - 1], _selected_tab);
        }

        /// <summary>Gets or sets the tab whose properties are shown; null is the built-in properties view.</summary>
        /// <remarks>
        /// <para>Real as of W6 mechanisms: the tab supplies the property descriptors the grid builds its
        /// tree from, and changing it raises <see cref="PropertyTabChanged"/> carrying the old and new
        /// tabs. It used to answer the first tab in the collection and never change, so selecting that
        /// tab could not be a change at all.</para>
        /// <para>Deviation: upstream's default is a built-in <c>PropertiesTab</c> instance, so its
        /// <c>SelectedTab</c> is never null. There is no such instance here, and <c>null</c> stands for
        /// the same thing -- the type's own properties, which is what the grid shows until a tab is
        /// chosen. The toolbar's first button selects it back.</para>
        /// </remarks>
        public PropertyTab? SelectedTab {
            get => _selected_tab;
            set {
                var old = _selected_tab;

                if (ReferenceEquals (old, value))
                    return;

                _selected_tab = value;
                RebuildEntries ();
                ApplyToolbarState ();
                Invalidate ();
                OnPropertyTabChanged (new PropertyTabChangedEventArgs { OldTab = old, NewTab = value });
            }
        }

        /// <summary>Raises <see cref="PropertyTabChanged"/>.</summary>
        protected virtual void OnPropertyTabChanged (PropertyTabChangedEventArgs e)
            => PropertyTabChanged?.Invoke (this, e);

        /// <summary>Rebuilds the toolbar's tab buttons.</summary>
        public void RefreshTabs (PropertyTabScope tabScope)
        {
            BuildToolbar ();
            Invalidate ();
        }

        // ── the commands pane ───────────────────────────────────────────────────────────────────────

        /// <summary>
        /// The designer verbs of the selected object, which is what the commands pane shows.
        /// </summary>
        /// <remarks>
        /// Taken the way upstream takes them: from the <see cref="IMenuCommandService"/> the selected
        /// component's site offers. With no such service there are no verbs and the pane stays hidden,
        /// which is what upstream does too.
        /// </remarks>
        internal IReadOnlyList<DesignerVerb> Verbs {
            get {
                if (_selected_object is not IComponent { Site: { } site })
                    return [];

                try {
                    if (site.GetService (typeof (IMenuCommandService)) is not IMenuCommandService commands)
                        return [];

                    return commands.Verbs?.Cast<DesignerVerb> ().ToList () ?? (IReadOnlyList<DesignerVerb>) [];
                } catch {
                    // A site that refuses the query simply offers no commands.
                    return [];
                }
            }
        }

        /// <summary>Gets whether the selected object offers any commands.</summary>
        /// <remarks>Real as of W6 mechanisms: true when its designer has verbs.</remarks>
        public virtual bool CanShowCommands => Verbs.Count > 0;

        /// <summary>Gets whether the commands pane is showing.</summary>
        /// <remarks>Real as of W6 mechanisms: <see cref="CanShowCommands"/> and
        /// <see cref="CommandsVisibleIfAvailable"/> together.</remarks>
        public virtual bool CommandsVisible => CommandsVisibleIfAvailable && CanShowCommands;

        /// <summary>The device rectangle of the command link at <paramref name="index"/>.</summary>
        internal Rectangle CommandBounds (int index)
        {
            var pane = CommandsBounds;

            if (pane.IsEmpty || index < 0 || index >= Verbs.Count)
                return Rectangle.Empty;

            var height = LogicalToDeviceUnits (COMMANDS_ROW_HEIGHT);
            var inset = LogicalToDeviceUnits (4);

            return new Rectangle (pane.Left + inset, pane.Top + inset + (index * height), Math.Max (0, pane.Width - (inset * 2)), height);
        }

        // A click in the commands pane invokes the verb whose row it landed on.
        private void RunCommandAt (Point device)
        {
            if (!CommandsVisible)
                return;

            var verbs = Verbs;

            for (var i = 0; i < verbs.Count; i++) {
                if (!CommandBounds (i).Contains (device))
                    continue;

                if (verbs[i].Enabled)
                    verbs[i].Invoke ();

                return;
            }
        }

        /// <summary>The help text for the selected item: its display name and description.</summary>
        /// <remarks>Shown in the help pane as of W6 mechanisms.</remarks>
        internal (string Title, string Description) HelpText {
            get {
                if (SelectedGridItem is not { } item)
                    return (string.Empty, string.Empty);

                return (item.Label, item.PropertyDescriptor?.Description ?? string.Empty);
            }
        }
    }
}
