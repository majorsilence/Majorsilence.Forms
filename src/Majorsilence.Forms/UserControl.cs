using System;
using System.ComponentModel;
using Majorsilence.Forms.Layout;

namespace Majorsilence.Forms
{
    /// <summary>
    /// Provides an empty control that can be used to create other controls.
    /// In Majorsilence.Forms, UserControl is an alias for Panel with full tab-stop support.
    /// </summary>
    public partial class UserControl : Panel, IContainerControl
    {
        /// <summary>
        /// Initializes a new instance of the UserControl class.
        /// </summary>
        public UserControl ()
        {
            TabStop = true;
            SetControlBehavior (ControlBehaviors.Selectable, true);

            // What makes GetContainerControl () find this rather than walking past it, and therefore
            // what makes ActiveControl, Validate and ValidateChildren reachable from a child. Upstream
            // sets the same style in ContainerControl's constructor.
            SetStyle (ControlStyles.ContainerControl, true);
        }

        // AutoSizeMode is inherited from Panel (same Get/SetAutoSizeMode mechanism).

        private AutoScaleMode _autoScaleMode = AutoScaleMode.Font;
        private System.Drawing.SizeF _autoScaleDimensions;

        /// <summary>Gets or sets the auto-scale mode.</summary>
        /// <inheritdoc cref="ContainerControl.AutoScaleMode" path="/remarks"/>
        public AutoScaleMode AutoScaleMode {
            get => _autoScaleMode;
            set {
                _autoScaleMode = value;
                ArmAutoScale ();
            }
        }

        /// <summary>Gets or sets the dimensions the designer laid this control out at.</summary>
        /// <inheritdoc cref="ContainerControl.AutoScaleDimensions" path="/remarks"/>
        public System.Drawing.SizeF AutoScaleDimensions {
            get => _autoScaleDimensions;
            set {
                _autoScaleDimensions = value;
                ArmAutoScale ();
            }
        }

        /// <summary>Gets the scale the control is currently laid out at.</summary>
        /// <inheritdoc cref="ContainerControl.CurrentAutoScaleDimensions" path="/remarks"/>
        public System.Drawing.SizeF CurrentAutoScaleDimensions
            => AutoScaleEngine.CurrentDimensions (AutoScaleMode, Font, DeviceDpi);

        /// <summary>Scales the control to the difference between its recorded and current dimensions.</summary>
        public void PerformAutoScale ()
        {
            var recorded = _autoScaleDimensions;
            AutoScaleEngine.Perform (this, AutoScaleMode, ref recorded);

            // The field, not the property: the property setter re-arms, which would leave this needing
            // to run again after every successful run.
            _autoScaleDimensions = recorded;
            _autoScaleNeeded = false;
        }

        // Armed by an assignment to AutoScaleMode, AutoScaleDimensions or Font -- not cleared by the
        // first layout that happens to come along. Upstream does the same (ContainerControl's
        // stateScalingNeededOnLayout), and the reason is the order InitializeComponent uses: controls
        // are added first and the recorded dimensions assigned after, so a flag consumed by the first
        // layout is consumed before there is anything to scale by. That produced exactly one silent
        // no-op here before this was written the upstream way.
        private bool _autoScaleNeeded;

        private void ArmAutoScale () => _autoScaleNeeded = true;

        /// <inheritdoc/>
        protected override void OnLayout (LayoutEventArgs e)
        {
            base.OnLayout (e);

            if (!_autoScaleNeeded)
                return;

            // Cleared before the scale, not after: Scale writes bounds, which raises layout again, and
            // re-entering here would apply the same factor repeatedly.
            _autoScaleNeeded = false;
            PerformAutoScale ();
        }

        /// <inheritdoc/>
        protected override void OnFontChanged (System.EventArgs e)
        {
            base.OnFontChanged (e);

            // A font assigned after construction -- Application.SetDefaultFont included, which is the
            // second half of FRM-17 -- makes the recorded dimensions stale. PerformAutoScale has
            // already stored the previous font's dimensions, so the next pass scales by the difference.
            ArmAutoScale ();
        }

        /// <summary>Gets or sets how the UserControl validates its children. Stub in Majorsilence.Forms.</summary>
        public AutoValidate AutoValidate { get; set; } = AutoValidate.EnablePreventFocusChange;

        private Control? active_control;

        /// <summary>Gets or sets the active control inside this UserControl.</summary>
        /// <inheritdoc cref="ContainerControl.ActiveControl" path="/remarks"/>
        public Control? ActiveControl {
            get => ContainerFocus.ActiveControlOf (this, active_control);
            set => ContainerFocus.SetActiveControl (this, value, ref active_control);
        }

        /// <summary>Activates the given child control.</summary>
        public bool ActivateControl (Control active)
            => ContainerFocus.SetActiveControl (this, active, ref active_control);

        /// <summary>Validates all child controls by triggering their Validating/Validated events.</summary>
        /// <inheritdoc cref="ContainerControl.ValidateChildren()" path="/remarks"/>
        public bool ValidateChildren () => ValidateChildren (ValidationConstraints.Selectable);

        /// <summary>Raised when the control is first displayed.</summary>
        public event EventHandler? Load;

        /// <summary>Raises the Load event.</summary>
        protected virtual void OnLoad (EventArgs e) => Load?.Invoke (this, e);

        /// <summary>
        /// Raises <see cref="Load"/> the first time the control goes live, matching WinForms, where
        /// UserControl.OnCreateControl is what fires Load.
        /// </summary>
        /// <remarks>
        /// Without this the event existed but nothing ever raised it, so a ported UserControl compiled
        /// cleanly and then silently never ran its Load handler -- typically the one populating it.
        /// CreateControl is guarded by the Created state, so this happens exactly once.
        /// </remarks>
        protected override void OnCreateControl ()
        {
            base.OnCreateControl ();
            OnLoad (EventArgs.Empty);
        }
    }

    /// <summary>
    /// Provides focus-management functionality for controls that contain other controls.
    /// In Majorsilence.Forms this is an alias for Panel.
    /// </summary>
    public partial class ContainerControl : Panel, IContainerControl
    {
        /// <summary>Initializes a new instance of the ContainerControl class.</summary>
        public ContainerControl ()
        {
            // See UserControl's constructor: this is what GetContainerControl () looks for.
            SetStyle (ControlStyles.ContainerControl, true);
        }

        /// <summary>Gets or sets the active control inside this container.</summary>
        /// <remarks>
        /// Both halves are real. The getter reports the focused descendant; the setter moves focus,
        /// which is what makes <c>ActiveControl = txtName</c> — the standard way to set initial focus —
        /// do anything. It used to be a plain auto-property: the setter focused nothing and the getter
        /// never reflected reality, so <c>if (ActiveControl is TextBox t) t.SelectAll ()</c> never
        /// matched.
        /// </remarks>
        public Control? ActiveControl {
            get => ContainerFocus.ActiveControlOf (this, active_control);
            set => ContainerFocus.SetActiveControl (this, value, ref active_control);
        }

        private Control? active_control;

        /// <summary>Activates the given child control.</summary>
        public bool ActivateControl (Control active)
            => ContainerFocus.SetActiveControl (this, active, ref active_control);

        /// <summary>Gets or sets how the container validates its children.</summary>
        /// <remarks>Consulted by the focus choke point in <c>ControlAdapter</c>: <c>Disable</c> skips
        /// the validation cycle entirely, and <c>EnableAllowFocusChange</c> runs it but lets focus move
        /// even when a handler cancels.</remarks>
        public AutoValidate AutoValidate { get; set; } = AutoValidate.EnablePreventFocusChange;

        private AutoScaleMode _autoScaleMode = AutoScaleMode.Font;
        private System.Drawing.SizeF _autoScaleDimensions;

        /// <summary>Gets or sets the auto-scale mode.</summary>
        /// <remarks>Real as of 2026-08-31 (<c>W3.6</c>): with <see cref="AutoScaleMode.Font"/> and a
        /// recorded <see cref="AutoScaleDimensions"/>, the container scales its children on first
        /// layout by the ratio between the recorded dimensions and the current font's.
        /// <see cref="AutoScaleMode.Dpi"/> is deliberately inert -- see
        /// <see cref="AutoScaleEngine.TryGetFactor"/> for why scaling by DPI here would double-count
        /// the backend's own display scale.</remarks>
        public AutoScaleMode AutoScaleMode {
            get => _autoScaleMode;
            set {
                _autoScaleMode = value;
                ArmAutoScale ();
            }
        }

        /// <summary>Gets or sets the dimensions the designer laid this container out at.</summary>
        /// <remarks>Empty means "not recorded", and nothing scales -- the normal case for a container
        /// built in code. A designer file assigns it, which is what gives the ratio a denominator.</remarks>
        public System.Drawing.SizeF AutoScaleDimensions {
            get => _autoScaleDimensions;
            set {
                _autoScaleDimensions = value;
                ArmAutoScale ();
            }
        }

        // Armed by an assignment to AutoScaleMode, AutoScaleDimensions or Font -- not consumed by
        // whichever layout happens to run first. Upstream arms the same way (ContainerControl's
        // stateScalingNeededOnLayout), and InitializeComponent is the reason: it adds the controls
        // first and assigns the recorded dimensions afterwards, so a flag cleared by the first layout
        // is spent before there is any ratio to scale by. That is not hypothetical -- written the other
        // way round, this silently did nothing and the test caught it.
        private bool _autoScaleNeeded;

        private void ArmAutoScale () => _autoScaleNeeded = true;

        /// <summary>Validates all child controls by triggering their Validating/Validated events.</summary>
        /// <remarks>
        /// Defers to the <see cref="ValidationConstraints"/> overload with
        /// <see cref="ValidationConstraints.Selectable"/>, as upstream does. It was <c>=&gt; true</c>,
        /// so the extremely common <c>if (!ValidateChildren ()) return;</c> guard in an OK handler
        /// always proceeded and the Validating handlers that would have flagged empty fields never ran.
        /// </remarks>
        public bool ValidateChildren () => ValidateChildren (ValidationConstraints.Selectable);
    }

    /// <summary>
    /// The container-side focus helpers shared by <see cref="ContainerControl"/> and
    /// <see cref="UserControl"/>.
    /// </summary>
    /// <remarks>
    /// Upstream both inherit this from <c>ContainerControl</c>. Here <see cref="UserControl"/> derives
    /// from <see cref="Panel"/> rather than from <see cref="ContainerControl"/>, so the behaviour lives
    /// in one place both can call instead of being written twice.
    /// </remarks>
    internal static class ContainerFocus
    {
        /// <summary>
        /// The container's active control: the focused descendant if focus is currently inside it,
        /// otherwise the last one assigned.
        /// </summary>
        /// <remarks>
        /// The fallback to the stored value is upstream's actual shape — <c>ActiveControl</c>'s getter
        /// is a field that <c>UpdateFocusedControl</c> keeps in step, not a live search. It matters for
        /// the ordinary designer case of assigning <c>ActiveControl</c> before the container is on a
        /// shown form, where focus cannot move yet and a purely derived getter would answer null.
        /// </remarks>
        internal static Control? ActiveControlOf (Control container, Control? stored)
            => FocusedDescendantOf (container) ?? stored;

        private static Control? FocusedDescendantOf (Control container)
        {
            foreach (var child in container.Controls) {
                if (child.Focused)
                    return child;

                if (FocusedDescendantOf (child) is { } nested)
                    return nested;
            }

            return null;
        }

        /// <summary>
        /// Records <paramref name="value"/> as the container's active control and moves focus to it.
        /// Null clears both, matching upstream's <c>ActiveControl = null</c>.
        /// </summary>
        internal static bool SetActiveControl (Control container, Control? value, ref Control? stored)
        {
            if (value is null) {
                stored = null;

                // Only clear real focus if it is actually inside this container -- assigning null to
                // one container must not steal focus away from another.
                if (FocusedDescendantOf (container) is not null && container.FindAdapter () is { } adapter)
                    adapter.SelectedControl = null;

                return true;
            }

            if (!container.Contains (value))
                return false;

            stored = value;
            value.Select ();
            return value.Focused;
        }
    }
}
