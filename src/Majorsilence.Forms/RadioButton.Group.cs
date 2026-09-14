using System;
using System.Linq;

namespace Majorsilence.Forms
{
    // SMP-01 and SMP-02: what makes a set of radio buttons a GROUP.
    //
    // UpdateSiblings unchecked every sibling RadioButton on the parent with no regard for anyone's
    // AutoCheck, so the standard manually-managed group (AutoCheck = false, code decides who is
    // checked) could never show what it was told to -- checking one member silently wiped the rest.
    //
    // Nothing anywhere wrote TabStop, so every button in a group was its own tab stop: a six-option
    // group cost six tabs instead of one, and tabbing in did not land on the checked option. That is
    // the most visible half of "this form does not feel like WinForms" on any data-entry screen.
    //
    // Both are one routine upstream -- PerformAutoUpdates, with WipeTabStops
    // (Controls/Buttons/RadioButton.cs:411-460) -- called from the Checked setter, the AutoCheck setter
    // and OnEnter. Ported here with the same shape, including first_focus: without it the sibling
    // updates below re-enter WipeTabStops and clear the tab stop this call has just set.
    public partial class RadioButton
    {
        private bool first_focus = true;

        /// <summary>
        /// Gets or sets whether the button checks itself when clicked, and takes part in the automatic
        /// unchecking of its group.
        /// </summary>
        /// <remarks>
        /// A button with <c>AutoCheck</c> off neither unchecks its siblings nor is unchecked by them:
        /// its state belongs to the application, which is the point of turning it off.
        /// </remarks>
        public bool AutoCheck {
            get => auto_check;
            set {
                if (auto_check == value)
                    return;

                auto_check = value;
                PerformAutoUpdates (false);
            }
        }

        // Upstream's PerformAutoUpdates. `tabbedInto` distinguishes arriving by keyboard (which must
        // leave the group's first-focus flag alone) from a programmatic change.
        private void PerformAutoUpdates (bool tabbedInto)
        {
            if (!AutoCheck)
                return;

            if (first_focus)
                WipeTabStops (tabbedInto);

            // The checked member is the group's single tab stop: tab moves between groups, the arrow
            // keys move within one.
            TabStop = Checked;

            if (!Checked)
                return;

            var siblings = Parent?.Controls.OfType<RadioButton> ().Where (rb => rb != this).ToList ();

            if (siblings is null)
                return;

            foreach (var sibling in siblings)
                // Only an automatic sibling is unchecked automatically. A manual one is left exactly as
                // the application set it, which is what makes two checked buttons possible.
                if (sibling.AutoCheck && sibling.Checked)
                    sibling.Checked = false;
        }

        // Clears the tab stop on every automatic radio button under the same parent, this one included;
        // the caller re-arms its own immediately afterwards.
        private void WipeTabStops (bool tabbedInto)
        {
            var group = Parent?.Controls.OfType<RadioButton> ().ToList ();

            if (group is null)
                return;

            foreach (var button in group) {
                if (!tabbedInto)
                    button.first_focus = false;

                if (button.AutoCheck)
                    button.TabStop = false;
            }
        }

        /// <inheritdoc/>
        /// <remarks>
        /// Arriving by keyboard re-arms the group's tab stop on whichever member is checked, so tabbing
        /// out and back returns to the selected option rather than the first one.
        /// </remarks>
        protected override void OnEnter (EventArgs e)
        {
            PerformAutoUpdates (true);

            base.OnEnter (e);
        }
    }
}
