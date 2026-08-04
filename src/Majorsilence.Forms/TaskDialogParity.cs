using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Drawing;
using System.Linq;
using System.Threading.Tasks;

namespace Majorsilence.Forms
{
    // The TaskDialog family (docs/winforms-gap-plan.md).
    //
    // Upstream this is a thin wrapper over the Win32 TaskDialogIndirect API. There is no such API
    // here, so ShowDialog builds the dialog out of this layer's own controls: a Form with the
    // heading, text, an optional check box and radio group, and one button per TaskDialogButton. That
    // means a migrated app that shows a task dialog gets a working dialog and the button the user
    // chose, rather than a stub returning null.
    //
    // What the Win32 dialog can do and a composed one cannot is stated on the members concerned:
    // Handle is zero, and the shield icons are the OS's rather than something this layer can draw.

    /// <summary>A dialog with a heading, body text and a set of buttons.</summary>
    /// <remarks>Instances are not constructed directly: one exists while a page is showing, and a
    /// page reaches it through <see cref="TaskDialogPage.BoundDialog"/>. That is upstream's shape --
    /// Handle and Close are instance members, ShowDialog is static.</remarks>
    public sealed class TaskDialog
    {
        private readonly Form form;

        private TaskDialog (Form form) => this.form = form;

        /// <summary>Gets the window handle of this dialog.</summary>
        /// <remarks>Zero: the dialog is composed from this layer's own controls rather than created
        /// by the Win32 task-dialog API, so there is no HWND to report.</remarks>
        public IntPtr Handle => IntPtr.Zero;

        /// <summary>Closes this dialog.</summary>
        public void Close () => form.Close ();

        /// <summary>Shows the dialog and returns the button the user chose.</summary>
        public static TaskDialogButton ShowDialog (TaskDialogPage page,
            TaskDialogStartupLocation startupLocation = TaskDialogStartupLocation.CenterOwner)
            => ShowDialog (owner: null, page, startupLocation);

        /// <inheritdoc cref="ShowDialog(TaskDialogPage,TaskDialogStartupLocation)"/>
        public static TaskDialogButton ShowDialog (IWin32Window? owner, TaskDialogPage page,
            TaskDialogStartupLocation startupLocation = TaskDialogStartupLocation.CenterOwner)
        {
            ArgumentNullException.ThrowIfNull (page);

            var chosen = page.Buttons.Count > 0 ? page.Buttons[0] : TaskDialogButton.OK;

            using var form = Build (page, button => chosen = button);
            page.Bind (new TaskDialog (form));

            try {
                page.RaiseCreated ();

                if (owner is Form ownerForm)
                    form.ShowDialog (ownerForm);
                else
                    form.ShowDialog ();
            } finally {
                page.Bind (null);
                page.RaiseDestroyed ();
            }

            return chosen;
        }

        /// <inheritdoc cref="ShowDialog(TaskDialogPage,TaskDialogStartupLocation)"/>
        /// <remarks>The handle overload cannot find a window: there are no HWNDs here, so the dialog
        /// is shown without an owner rather than against the wrong one.</remarks>
        public static TaskDialogButton ShowDialog (IntPtr hwndOwner, TaskDialogPage page,
            TaskDialogStartupLocation startupLocation = TaskDialogStartupLocation.CenterOwner)
            => ShowDialog (owner: null, page, startupLocation);

        /// <inheritdoc cref="ShowDialog(TaskDialogPage,TaskDialogStartupLocation)"/>
        public static Task<TaskDialogButton> ShowDialogAsync (TaskDialogPage page,
            TaskDialogStartupLocation startupLocation = TaskDialogStartupLocation.CenterOwner)
            => ShowDialogAsync (owner: null, page, startupLocation);

        /// <inheritdoc cref="ShowDialog(TaskDialogPage,TaskDialogStartupLocation)"/>
        public static Task<TaskDialogButton> ShowDialogAsync (IWin32Window? owner, TaskDialogPage page,
            TaskDialogStartupLocation startupLocation = TaskDialogStartupLocation.CenterOwner)
            => Task.FromResult (ShowDialog (owner, page, startupLocation));

        /// <inheritdoc cref="ShowDialog(IntPtr,TaskDialogPage,TaskDialogStartupLocation)"/>
        public static Task<TaskDialogButton> ShowDialogAsync (IntPtr hwndOwner, TaskDialogPage page,
            TaskDialogStartupLocation startupLocation = TaskDialogStartupLocation.CenterOwner)
            => ShowDialogAsync (owner: null, page, startupLocation);

        // Lays the page out as an ordinary Form. Kept deliberately plain: the point is that the
        // buttons work and the chosen one comes back, not that it looks like the Windows dialog.
        private static Form Build (TaskDialogPage page, Action<TaskDialogButton> choose)
        {
            var form = new Form {
                Text = page.Caption ?? string.Empty,
                Width = 420,
                Height = 220,
            };

            var y = 12;

            if (!string.IsNullOrEmpty (page.Heading)) {
                form.Controls.Add (new Label { Text = page.Heading, Left = 12, Top = y, Width = 396, Height = 24 });
                y += 28;
            }

            if (!string.IsNullOrEmpty (page.Text)) {
                form.Controls.Add (new Label { Text = page.Text, Left = 12, Top = y, Width = 396, Height = 40 });
                y += 46;
            }

            foreach (var radio in page.RadioButtons) {
                var control = new RadioButton { Text = radio.Text ?? string.Empty, Left = 12, Top = y, Width = 396, Checked = radio.Checked };
                control.CheckedChanged += (_, _) => radio.Checked = control.Checked;
                form.Controls.Add (control);
                y += 24;
            }

            if (page.Verification is { } verification) {
                var check = new CheckBox { Text = verification.Text ?? string.Empty, Left = 12, Top = y, Width = 396, Checked = verification.Checked };
                check.CheckedChanged += (_, _) => verification.Checked = check.Checked;
                form.Controls.Add (check);
                y += 28;
            }

            var buttons = page.Buttons.Count > 0 ? page.Buttons.ToArray () : [TaskDialogButton.OK];
            var x = 408 - (buttons.Length * 84);

            foreach (var button in buttons) {
                var control = new Button {
                    Text = button.Text ?? string.Empty,
                    Left = x,
                    Top = y + 8,
                    Width = 80,
                    Enabled = button.Enabled,
                    Visible = button.Visible,
                };

                var captured = button;
                control.Click += (_, _) => {
                    choose (captured);
                    captured.PerformClick ();

                    if (captured.AllowCloseDialog)
                        form.Close ();
                };

                form.Controls.Add (control);
                x += 84;
            }

            form.Height = y + 80;
            return form;
        }
    }

    /// <summary>The base of the controls a <see cref="TaskDialogPage"/> holds.</summary>
    public abstract class TaskDialogControl
    {
        /// <summary>Gets the page this control belongs to.</summary>
        public TaskDialogPage? BoundPage { get; internal set; }

        /// <summary>Gets or sets arbitrary data associated with this control.</summary>
        public object? Tag { get; set; }
    }

    /// <summary>A button on a <see cref="TaskDialogPage"/>.</summary>
    public class TaskDialogButton : TaskDialogControl
    {
        /// <summary>Initializes a new instance of the <see cref="TaskDialogButton"/> class.</summary>
        public TaskDialogButton () { }

        /// <inheritdoc cref="TaskDialogButton()"/>
        public TaskDialogButton (string? text, bool enabled = true, bool allowCloseDialog = true)
        {
            Text = text;
            Enabled = enabled;
            AllowCloseDialog = allowCloseDialog;
        }

        /// <summary>Gets or sets the button's caption.</summary>
        public string? Text { get; set; }

        /// <summary>Gets or sets whether the button can be clicked.</summary>
        public bool Enabled { get; set; } = true;

        /// <summary>Gets or sets whether the button is shown.</summary>
        public bool Visible { get; set; } = true;

        /// <summary>Gets or sets whether clicking the button closes the dialog.</summary>
        public bool AllowCloseDialog { get; set; } = true;

        /// <summary>Gets or sets whether the shield glyph is drawn on the button.</summary>
        /// <remarks>Stored: the shield is an OS elevation glyph the composed dialog has no source
        /// for. It round-trips so a caller's configuration survives.</remarks>
        public bool ShowShieldIcon { get; set; }

        /// <summary>Raised when the button is clicked.</summary>
        public event EventHandler? Click;

        /// <summary>Raises <see cref="Click"/> as though the user had pressed the button.</summary>
        public void PerformClick () => Click?.Invoke (this, EventArgs.Empty);

        /// <inheritdoc/>
        public override string ToString () => Text ?? base.ToString () ?? nameof (TaskDialogButton);

        /// <summary>Gets a standard OK button.</summary>
        public static TaskDialogButton OK => new ("OK");

        /// <summary>Gets a standard Abort button.</summary>
        public static TaskDialogButton Abort => new ("Abort");

        /// <summary>Gets a standard Cancel button.</summary>
        public static TaskDialogButton Cancel => new ("Cancel");

        /// <summary>Gets a standard Close button.</summary>
        public static TaskDialogButton Close => new ("Close");

        /// <summary>Gets a standard Yes button.</summary>
        public static TaskDialogButton Yes => new ("Yes");

        /// <summary>Gets a standard No button.</summary>
        public static TaskDialogButton No => new ("No");

        /// <summary>Gets a standard Retry button.</summary>
        public static TaskDialogButton Retry => new ("Retry");

        /// <summary>Gets a standard Try Again button.</summary>
        public static TaskDialogButton TryAgain => new ("Try Again");

        /// <summary>Gets a standard Continue button.</summary>
        public static TaskDialogButton Continue => new ("Continue");

        /// <summary>Gets a standard Ignore button.</summary>
        public static TaskDialogButton Ignore => new ("Ignore");

        /// <summary>Gets a standard Help button.</summary>
        public static TaskDialogButton Help => new ("Help");
    }

    /// <summary>A button that shows a description below its caption.</summary>
    public class TaskDialogCommandLinkButton : TaskDialogButton
    {
        /// <summary>Initializes a new instance of the <see cref="TaskDialogCommandLinkButton"/> class.</summary>
        public TaskDialogCommandLinkButton () { }

        /// <inheritdoc cref="TaskDialogCommandLinkButton()"/>
        public TaskDialogCommandLinkButton (string? text, string? descriptionText = null,
            bool enabled = true, bool allowCloseDialog = true)
            : base (text, enabled, allowCloseDialog) => DescriptionText = descriptionText;

        /// <summary>Gets or sets the smaller text shown below the caption.</summary>
        public string? DescriptionText { get; set; }
    }

    /// <summary>A radio button on a <see cref="TaskDialogPage"/>.</summary>
    public class TaskDialogRadioButton : TaskDialogControl
    {
        private bool is_checked;

        /// <summary>Initializes a new instance of the <see cref="TaskDialogRadioButton"/> class.</summary>
        public TaskDialogRadioButton () { }

        /// <inheritdoc cref="TaskDialogRadioButton()"/>
        public TaskDialogRadioButton (string? text) => Text = text;

        /// <summary>Gets or sets the button's caption.</summary>
        public string? Text { get; set; }

        /// <summary>Gets or sets whether the button can be chosen.</summary>
        public bool Enabled { get; set; } = true;

        /// <summary>Gets or sets whether the button is chosen.</summary>
        public bool Checked {
            get => is_checked;
            set {
                if (is_checked == value)
                    return;

                is_checked = value;

                // Checking one clears the rest, because a radio group with two chosen buttons is not
                // a state the dialog can be in.
                if (value && BoundPage is { } page)
                    foreach (var other in page.RadioButtons.Where (r => !ReferenceEquals (r, this)))
                        other.Checked = false;

                CheckedChanged?.Invoke (this, EventArgs.Empty);
            }
        }

        /// <summary>Raised when <see cref="Checked"/> changes.</summary>
        public event EventHandler? CheckedChanged;

        /// <inheritdoc/>
        public override string ToString () => Text ?? base.ToString () ?? nameof (TaskDialogRadioButton);
    }

    /// <summary>The check box shown at the bottom of a <see cref="TaskDialogPage"/>.</summary>
    public class TaskDialogVerificationCheckBox : TaskDialogControl
    {
        private bool is_checked;

        /// <summary>Initializes a new instance of the <see cref="TaskDialogVerificationCheckBox"/> class.</summary>
        public TaskDialogVerificationCheckBox () { }

        /// <inheritdoc cref="TaskDialogVerificationCheckBox()"/>
        public TaskDialogVerificationCheckBox (string? text, bool isChecked = false)
        {
            Text = text;
            is_checked = isChecked;
        }

        /// <summary>Gets or sets the check box's caption.</summary>
        public string? Text { get; set; }

        /// <summary>Gets or sets whether the box is ticked.</summary>
        public bool Checked {
            get => is_checked;
            set {
                if (is_checked == value)
                    return;

                is_checked = value;
                CheckedChanged?.Invoke (this, EventArgs.Empty);
            }
        }

        /// <summary>Raised when <see cref="Checked"/> changes.</summary>
        public event EventHandler? CheckedChanged;

        /// <inheritdoc/>
        public override string ToString () => Text ?? base.ToString () ?? nameof (TaskDialogVerificationCheckBox);
    }

    /// <summary>The collapsible area of a <see cref="TaskDialogPage"/>.</summary>
    public class TaskDialogExpander : TaskDialogControl
    {
        private bool expanded;

        /// <summary>Initializes a new instance of the <see cref="TaskDialogExpander"/> class.</summary>
        public TaskDialogExpander () { }

        /// <inheritdoc cref="TaskDialogExpander()"/>
        public TaskDialogExpander (string? text) => Text = text;

        /// <summary>Gets or sets the text revealed when the area is expanded.</summary>
        public string? Text { get; set; }

        /// <summary>Gets or sets the label shown while collapsed.</summary>
        public string? CollapsedButtonText { get; set; }

        /// <summary>Gets or sets the label shown while expanded.</summary>
        public string? ExpandedButtonText { get; set; }

        /// <summary>Gets or sets where the expanded text appears.</summary>
        public TaskDialogExpanderPosition Position { get; set; } = TaskDialogExpanderPosition.AfterText;

        /// <summary>Gets or sets whether the area is expanded.</summary>
        public bool Expanded {
            get => expanded;
            set {
                if (expanded == value)
                    return;

                expanded = value;
                ExpandedChanged?.Invoke (this, EventArgs.Empty);
            }
        }

        /// <summary>Raised when <see cref="Expanded"/> changes.</summary>
        public event EventHandler? ExpandedChanged;

        /// <inheritdoc/>
        public override string ToString () => Text ?? base.ToString () ?? nameof (TaskDialogExpander);
    }

    /// <summary>The footer of a <see cref="TaskDialogPage"/>.</summary>
    public class TaskDialogFootnote : TaskDialogControl
    {
        /// <summary>Initializes a new instance of the <see cref="TaskDialogFootnote"/> class.</summary>
        public TaskDialogFootnote () { }

        /// <inheritdoc cref="TaskDialogFootnote()"/>
        public TaskDialogFootnote (string? text) => Text = text;

        /// <summary>Gets or sets the footer text.</summary>
        public string? Text { get; set; }

        /// <summary>Gets or sets the icon shown beside the footer.</summary>
        public TaskDialogIcon? Icon { get; set; }

        /// <inheritdoc/>
        public override string ToString () => Text ?? base.ToString () ?? nameof (TaskDialogFootnote);
    }

    /// <summary>The progress bar on a <see cref="TaskDialogPage"/>.</summary>
    public class TaskDialogProgressBar : TaskDialogControl
    {
        /// <summary>Initializes a new instance of the <see cref="TaskDialogProgressBar"/> class.</summary>
        public TaskDialogProgressBar () { }

        /// <inheritdoc cref="TaskDialogProgressBar()"/>
        public TaskDialogProgressBar (TaskDialogProgressBarState state) => State = state;

        /// <summary>Gets or sets how the bar is drawn.</summary>
        public TaskDialogProgressBarState State { get; set; } = TaskDialogProgressBarState.Normal;

        /// <summary>Gets or sets the lowest value.</summary>
        public int Minimum { get; set; }

        /// <summary>Gets or sets the highest value.</summary>
        public int Maximum { get; set; } = 100;

        /// <summary>Gets or sets the current value.</summary>
        public int Value { get; set; }

        /// <summary>Gets or sets how fast the marquee block moves, in milliseconds per step.</summary>
        public int MarqueeSpeed { get; set; }
    }

    /// <summary>The icon shown on a <see cref="TaskDialogPage"/>.</summary>
    public class TaskDialogIcon : IDisposable
    {
        private TaskDialogIcon (string name) => Name = name;

        /// <summary>Initializes a new instance from a bitmap.</summary>
        public TaskDialogIcon (Majorsilence.Forms.Drawing.Bitmap image)
        {
            Image = image;
            Name = nameof (Image);
        }

        /// <summary>Initializes a new instance from an icon.</summary>
        public TaskDialogIcon (Majorsilence.Forms.Drawing.Icon icon)
        {
            Icon = icon;
            Name = nameof (Icon);
        }

        /// <summary>Initializes a new instance from a Win32 icon handle.</summary>
        /// <remarks>The handle is stored and never dereferenced; there is no GDI here to resolve it.</remarks>
        public TaskDialogIcon (IntPtr iconHandle)
        {
            IconHandle = iconHandle;
            Name = nameof (IconHandle);
        }

        /// <summary>Gets the bitmap this icon was built from, if any.</summary>
        public Majorsilence.Forms.Drawing.Bitmap? Image { get; }

        /// <summary>Gets the icon this icon was built from, if any.</summary>
        public Majorsilence.Forms.Drawing.Icon? Icon { get; }

        /// <summary>Gets the Win32 handle this icon was built from, or zero.</summary>
        public IntPtr IconHandle { get; }

        /// <summary>Gets the name of the standard icon, for the shared instances below.</summary>
        internal string Name { get; }

        /// <summary>Releases the resources used by this icon.</summary>
        public void Dispose ()
        {
            Image?.Dispose ();
            GC.SuppressFinalize (this);
        }

        // The standard icons. The shield variants are Windows elevation glyphs drawn by the task
        // dialog itself; there is nothing to draw them from here, so they are distinguishable
        // instances a caller can compare and switch on rather than images.

        /// <summary>No icon.</summary>
        public static readonly TaskDialogIcon None = new (nameof (None));

        /// <summary>The information icon.</summary>
        public static readonly TaskDialogIcon Information = new (nameof (Information));

        /// <summary>The warning icon.</summary>
        public static readonly TaskDialogIcon Warning = new (nameof (Warning));

        /// <summary>The error icon.</summary>
        public static readonly TaskDialogIcon Error = new (nameof (Error));

        /// <summary>The elevation shield.</summary>
        public static readonly TaskDialogIcon Shield = new (nameof (Shield));

        /// <summary>The shield on a blue bar.</summary>
        public static readonly TaskDialogIcon ShieldBlueBar = new (nameof (ShieldBlueBar));

        /// <summary>The shield on a grey bar.</summary>
        public static readonly TaskDialogIcon ShieldGrayBar = new (nameof (ShieldGrayBar));

        /// <summary>The shield on a red bar.</summary>
        public static readonly TaskDialogIcon ShieldErrorRedBar = new (nameof (ShieldErrorRedBar));

        /// <summary>The shield on a green bar.</summary>
        public static readonly TaskDialogIcon ShieldSuccessGreenBar = new (nameof (ShieldSuccessGreenBar));

        /// <summary>The shield on a yellow bar.</summary>
        public static readonly TaskDialogIcon ShieldWarningYellowBar = new (nameof (ShieldWarningYellowBar));
    }

    /// <summary>The buttons on a <see cref="TaskDialogPage"/>.</summary>
    public class TaskDialogButtonCollection : Collection<TaskDialogButton>
    {
        // Set when the collection is handed to a page, so a button knows its page from the moment
        // it is added rather than only once a dialog is showing.
        internal TaskDialogPage? Page { get; set; }

        /// <summary>Adds a button with the given caption.</summary>
        public TaskDialogButton Add (string? text, bool enabled = true, bool allowCloseDialog = true)
        {
            var button = new TaskDialogButton (text, enabled, allowCloseDialog);
            Add (button);
            return button;
        }

        /// <inheritdoc/>
        protected override void InsertItem (int index, TaskDialogButton item)
        {
            base.InsertItem (index, item);
            Page?.Adopt (item);
        }

        /// <inheritdoc/>
        protected override void SetItem (int index, TaskDialogButton item)
        {
            base.SetItem (index, item);
            Page?.Adopt (item);
        }
    }

    /// <summary>The radio buttons on a <see cref="TaskDialogPage"/>.</summary>
    public class TaskDialogRadioButtonCollection : Collection<TaskDialogRadioButton>
    {
        /// <inheritdoc cref="TaskDialogButtonCollection.Page"/>
        internal TaskDialogPage? Page { get; set; }

        /// <summary>Adds a radio button with the given caption.</summary>
        public TaskDialogRadioButton Add (string? text)
        {
            var button = new TaskDialogRadioButton (text);
            Add (button);
            return button;
        }

        /// <inheritdoc/>
        protected override void InsertItem (int index, TaskDialogRadioButton item)
        {
            base.InsertItem (index, item);
            Page?.Adopt (item);
        }

        /// <inheritdoc/>
        protected override void SetItem (int index, TaskDialogRadioButton item)
        {
            base.SetItem (index, item);
            Page?.Adopt (item);
        }
    }

    /// <summary>What a <see cref="TaskDialog"/> shows.</summary>
    public class TaskDialogPage
    {
        private TaskDialogButtonCollection buttons = [];
        private TaskDialogRadioButtonCollection radio_buttons = [];

        /// <summary>Initializes a new instance of the <see cref="TaskDialogPage"/> class.</summary>
        public TaskDialogPage ()
        {
            buttons.Page = this;
            radio_buttons.Page = this;
        }

        /// <summary>Gets or sets the dialog's title-bar text.</summary>
        public string? Caption { get; set; }

        /// <summary>Gets or sets the large text at the top of the dialog.</summary>
        public string? Heading { get; set; }

        /// <summary>Gets or sets the body text.</summary>
        public string? Text { get; set; }

        /// <summary>Gets or sets the icon shown beside the heading.</summary>
        public TaskDialogIcon? Icon { get; set; }

        /// <summary>Gets or sets the buttons.</summary>
        public TaskDialogButtonCollection Buttons {
            get => buttons;
            set {
                buttons.Page = null;
                buttons = value ?? [];
                buttons.Page = this;
                Bind ();
            }
        }

        /// <summary>Gets or sets the radio buttons.</summary>
        public TaskDialogRadioButtonCollection RadioButtons {
            get => radio_buttons;
            set {
                radio_buttons.Page = null;
                radio_buttons = value ?? [];
                radio_buttons.Page = this;
                Bind ();
            }
        }

        /// <summary>Gets or sets the button focused when the dialog opens.</summary>
        public TaskDialogButton? DefaultButton { get; set; }

        /// <summary>Gets or sets the check box shown at the bottom.</summary>
        public TaskDialogVerificationCheckBox? Verification { get; set; }

        /// <summary>Gets or sets the collapsible area.</summary>
        public TaskDialogExpander? Expander { get; set; }

        /// <summary>Gets or sets the footer.</summary>
        public TaskDialogFootnote? Footnote { get; set; }

        /// <summary>Gets or sets the progress bar.</summary>
        public TaskDialogProgressBar? ProgressBar { get; set; }

        /// <summary>Gets or sets whether the dialog can be cancelled.</summary>
        public bool AllowCancel { get; set; }

        /// <summary>Gets or sets whether the dialog can be minimized.</summary>
        public bool AllowMinimize { get; set; }

        /// <summary>Gets or sets whether hyperlinks in the text are active.</summary>
        public bool EnableLinks { get; set; }

        /// <summary>Gets or sets whether the dialog lays out right to left.</summary>
        public bool RightToLeftLayout { get; set; }

        /// <summary>Gets or sets whether the dialog sizes itself to its content.</summary>
        public bool SizeToContent { get; set; }

        /// <summary>Gets the dialog currently showing this page, or null when it is not shown.</summary>
        public TaskDialog? BoundDialog { get; private set; }

        /// <summary>Replaces the dialog's contents with another page.</summary>
        /// <remarks>Navigation replaces a live Win32 dialog's contents in place. The composed dialog
        /// here is built once when it is shown, so this copies the new page's content onto this one --
        /// a caller that navigates before showing gets what it asked for, and one that navigates
        /// while shown sees the change on the next display rather than nothing at all.</remarks>
        public void Navigate (TaskDialogPage page)
        {
            ArgumentNullException.ThrowIfNull (page);

            Caption = page.Caption;
            Heading = page.Heading;
            Text = page.Text;
            Icon = page.Icon;
            Buttons = page.Buttons;
            RadioButtons = page.RadioButtons;
            DefaultButton = page.DefaultButton;
            Verification = page.Verification;
            Expander = page.Expander;
            Footnote = page.Footnote;
            ProgressBar = page.ProgressBar;
        }

        /// <summary>Raised when the dialog has been created.</summary>
        public event EventHandler? Created;

        /// <summary>Raised when the dialog has been closed.</summary>
        public event EventHandler? Destroyed;

        /// <summary>Raised when the user asks for help.</summary>
#pragma warning disable CS0067
        public event EventHandler? HelpRequest;

        /// <summary>Raised when a hyperlink in the text is clicked. Not raised: the composed dialog
        /// draws its text as a label, which has no links to click.</summary>
        public event EventHandler<TaskDialogLinkClickedEventArgs>? LinkClicked;
#pragma warning restore CS0067

        internal void RaiseCreated () => Created?.Invoke (this, EventArgs.Empty);

        internal void RaiseDestroyed () => Destroyed?.Invoke (this, EventArgs.Empty);

        internal void Bind (TaskDialog? dialog)
        {
            BoundDialog = dialog;
            Bind ();
        }

        // Called by the collections as controls arrive, so BoundPage is right from the moment a
        // control is added rather than only once a dialog is showing.
        internal void Adopt (TaskDialogControl? control)
        {
            if (control is not null)
                control.BoundPage = this;
        }

        // Each control needs to know its page, or a radio button cannot clear its siblings.
        private void Bind ()
        {
            foreach (var control in buttons.Cast<TaskDialogControl> ()
                .Concat (radio_buttons)
                .Concat (new TaskDialogControl?[] { Verification, Expander, Footnote, ProgressBar }.OfType<TaskDialogControl> ()))
                control.BoundPage = this;
        }
    }
}
