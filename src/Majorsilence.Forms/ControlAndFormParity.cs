using System;
using Majorsilence.Forms.Backends;
using System.Drawing;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;

namespace Majorsilence.Forms
{
    // Control and Form parity (docs/winforms-gap-plan.md).
    //
    // These two types are touched by every migrated app, so the bar for "implemented" is higher here
    // than for a control an app may never instantiate. Most of what was missing is genuinely
    // computable and is computed: CompanyName/ProductName/ProductVersion read the entry assembly's
    // attributes, IsKeyLocked answers from the backend's modifier state, InvokeAsync is a real
    // awaitable marshal, and Form's caption and border colours round-trip and notify.
    //
    // The exceptions are named as exceptions. FromHandle and FromChildHandle cannot work: there are
    // no HWNDs here, which is the same reason the Win32 message plumbing is a documented non-goal, so
    // they return null rather than pretending. CheckForIllegalCrossThreadCalls is a diagnostic for a
    // check this layer does not perform.

    public partial class Control
    {
        private static readonly Lazy<Majorsilence.Forms.Drawing.Font> s_defaultFont =
            new (() => new Majorsilence.Forms.Drawing.Font (Theme.UIFont.FamilyName, Theme.FontSize));

        /// <summary>Gets or sets the offset scrolled to when this control is scrolled into view.</summary>
        public virtual Point AutoScrollOffset { get; set; }

        /// <summary>Gets or sets an arbitrary object shared with this control's children for binding.</summary>
        /// <remarks>Inherited down the tree, as upstream: a child with no DataContext of its own
        /// reports its parent's. That inheritance is the whole point of the property.</remarks>
        public virtual object? DataContext {
            get => data_context ?? Parent?.DataContext;
            set {
                if (Equals (data_context, value))
                    return;

                data_context = value;
                OnDataContextChanged (EventArgs.Empty);
            }
        }

        private object? data_context;

        /// <summary>Gets or sets whether this control is visible to accessibility clients.</summary>
        public bool IsAccessible { get; set; } = true;

        /// <summary>Gets whether an ancestor of this control is being edited in a designer.</summary>
        public bool IsAncestorSiteInDesignMode {
            get {
                for (var parent = Parent; parent is not null; parent = parent.Parent)
                    if (parent.Site?.DesignMode == true)
                        return true;

                return false;
            }
        }

        /// <summary>Gets whether the control's handle is being recreated.</summary>
        /// <remarks>Always false: handles are not recreated here, because there is no HWND whose style
        /// bits would require it.</remarks>
        public bool RecreatingHandle => false;

        /// <summary>Gets the company name from the entry assembly's metadata.</summary>
        public string CompanyName => AssemblyMetadata<AssemblyCompanyAttribute> (a => a.Company);

        /// <summary>Gets the product name from the entry assembly's metadata.</summary>
        public string ProductName => AssemblyMetadata<AssemblyProductAttribute> (a => a.Product);

        /// <summary>Gets the product version from the entry assembly's metadata.</summary>
        public string ProductVersion
            => AssemblyMetadata<AssemblyInformationalVersionAttribute> (a => a.InformationalVersion) is { Length: > 0 } informational
                ? informational
                : Assembly.GetEntryAssembly ()?.GetName ().Version?.ToString () ?? string.Empty;

        /// <summary>Gets the default font controls are drawn with.</summary>
        public static Majorsilence.Forms.Drawing.Font DefaultFont => s_defaultFont.Value;

        /// <summary>Gets the default foreground colour of a control.</summary>
        public static Color DefaultForeColor => Theme.ForegroundColor.ToDrawingColor ();

        /// <summary>Gets which mouse buttons are currently held down.</summary>
        public static MouseButtons MouseButtons { get; internal set; }

        /// <summary>Gets or sets whether cross-thread access to a control is reported as an error.</summary>
        /// <remarks>Stored and never consulted: this layer does not check the calling thread, so
        /// setting it true would promise a diagnostic that never arrives.</remarks>
        public static bool CheckForIllegalCrossThreadCalls { get; set; }

        /// <summary>Returns whether the given toggle key is currently on.</summary>
        public static bool IsKeyLocked (Keys keyVal)
        {
            // Only the three toggles have a locked state; upstream throws for anything else rather
            // than answering false, because asking is a programming error.
            if (keyVal is not (Keys.CapsLock or Keys.NumLock or Keys.Scroll))
                throw new NotSupportedException ($"{keyVal} is not a toggle key.");

            return (ModifierKeys & keyVal) == keyVal;
        }

        /// <summary>Returns the control that owns the given window handle.</summary>
        /// <remarks>Always null. There are no window handles in this layer — the same reason the
        /// Win32 message plumbing is a documented non-goal — so any answer other than null would be
        /// invented.</remarks>
        public static Control? FromHandle (IntPtr handle) => null;

        /// <inheritdoc cref="FromHandle(IntPtr)"/>
        public static Control? FromChildHandle (IntPtr handle) => null;

        // Dispatch for the InvokeAsync family. Running inline when the caller is already on the UI
        // thread is not just an optimisation: Post only queues, and the queue is drained by the
        // message loop, so posting from the UI thread and then awaiting the result deadlocks -- and
        // calling InvokeAsync from the UI thread is the ordinary case, not the exotic one.
        private static void Dispatch (Action work)
        {
            if (Platform.Backend.CheckAccess ())
                work ();
            else
                Platform.Backend.Post (work);
        }

        /// <summary>Runs the callback on the UI thread and returns a task that completes with it.</summary>
        public Task InvokeAsync (Action callback, CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull (callback);

            var completion = new TaskCompletionSource ();

            Dispatch (() => {
                if (cancellationToken.IsCancellationRequested) {
                    completion.TrySetCanceled (cancellationToken);
                    return;
                }

                try {
                    callback ();
                    completion.TrySetResult ();
                } catch (Exception ex) {
                    // The exception belongs to the awaiter, not to the UI thread's dispatch loop --
                    // letting it escape here would take the application down instead.
                    completion.TrySetException (ex);
                }
            });

            return completion.Task;
        }

        /// <inheritdoc cref="InvokeAsync(Action,CancellationToken)"/>
        public Task<T> InvokeAsync<T> (Func<T> callback, CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull (callback);

            var completion = new TaskCompletionSource<T> ();

            Dispatch (() => {
                if (cancellationToken.IsCancellationRequested) {
                    completion.TrySetCanceled (cancellationToken);
                    return;
                }

                try {
                    completion.TrySetResult (callback ());
                } catch (Exception ex) {
                    completion.TrySetException (ex);
                }
            });

            return completion.Task;
        }

        /// <inheritdoc cref="InvokeAsync(Action,CancellationToken)"/>
        public Task InvokeAsync (Func<CancellationToken, ValueTask> callback, CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull (callback);

            var completion = new TaskCompletionSource ();

            Dispatch (async () => {
                try {
                    await callback (cancellationToken).ConfigureAwait (true);
                    completion.TrySetResult ();
                } catch (OperationCanceledException) {
                    completion.TrySetCanceled (cancellationToken);
                } catch (Exception ex) {
                    completion.TrySetException (ex);
                }
            });

            return completion.Task;
        }

        /// <inheritdoc cref="InvokeAsync(Action,CancellationToken)"/>
        public Task<T> InvokeAsync<T> (Func<CancellationToken, ValueTask<T>> callback, CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull (callback);

            var completion = new TaskCompletionSource<T> ();

            Dispatch (async () => {
                try {
                    completion.TrySetResult (await callback (cancellationToken).ConfigureAwait (true));
                } catch (OperationCanceledException) {
                    completion.TrySetCanceled (cancellationToken);
                } catch (Exception ex) {
                    completion.TrySetException (ex);
                }
            });

            return completion.Task;
        }

        /// <summary>Starts a drag operation carrying the data serialised as JSON.</summary>
        /// <remarks>Serialisation is what upstream added this for — it avoids the binary formatter.
        /// The drag itself still reports that none occurred, as <see cref="DoDragDrop(object,DragDropEffects)"/> does.</remarks>
        [System.Diagnostics.CodeAnalysis.RequiresUnreferencedCode ("The dragged type is serialised with reflection, as it is upstream.")]
        public DragDropEffects DoDragDropAsJson<T> (T data, DragDropEffects allowedEffects)
            => DoDragDrop (System.Text.Json.JsonSerializer.Serialize (data), allowedEffects);

        /// <inheritdoc cref="DoDragDropAsJson{T}(T,DragDropEffects)"/>
        [System.Diagnostics.CodeAnalysis.RequiresUnreferencedCode ("The dragged type is serialised with reflection, as it is upstream.")]
        public DragDropEffects DoDragDropAsJson<T> (T data, DragDropEffects allowedEffects,
            Majorsilence.Forms.Drawing.Bitmap? dragImage, Point cursorOffset, bool useDefaultDragImage)
            => DoDragDropAsJson (data, allowedEffects);

        /// <summary>Re-reads every bound property from its data source.</summary>
        public void ResetBindings ()
        {
            foreach (var binding in DataBindings)
                binding.WriteValue ();
        }

        /// <summary>Runs this control's pre-processing for a keyboard message.</summary>
        /// <remarks>Always false, and it has to be: there is no message pump here, so no message ever
        /// reaches this. Present because <c>PreProcessMessage</c> is the documented override point for
        /// a control that wants first refusal on a key, and such a control must still compile.</remarks>
        public virtual bool PreProcessMessage (ref Message msg) => false;

        /// <summary>Runs this control's pre-processing and reports what it did with the message.</summary>
        /// <inheritdoc cref="PreProcessMessage(ref Message)" path="/remarks"/>
        public PreProcessControlState PreProcessControlMessage (ref Message msg)
            => PreProcessMessage (ref msg) ? PreProcessControlState.MessageProcessed : PreProcessControlState.MessageNotNeeded;

        /// <summary>Raised when <see cref="DataContext"/> changes.</summary>
        public event EventHandler? DataContextChanged;

        /// <summary>Raised when <see cref="Control.ClientSize"/> changes.</summary>
        public event EventHandler? ClientSizeChanged;

        /// <summary>Raised when the control's region changes.</summary>
        public event EventHandler? RegionChanged;

        /// <summary>Raised when the control's window style changes.</summary>
        /// <remarks>Never raised: there is no window style to change. Present because designer code
        /// binds it.</remarks>
#pragma warning disable CS0067
        public event EventHandler? StyleChanged;

        /// <summary>Raised when the background image changes. Never raised; this layer does not draw one.</summary>
        public event EventHandler? BackgroundImageChanged;

        /// <summary>Raised when the background image layout changes. Never raised; see
        /// <see cref="BackgroundImageChanged"/>.</summary>
        public event EventHandler? BackgroundImageLayoutChanged;
#pragma warning restore CS0067

        /// <summary>Raises the <see cref="DataContextChanged"/> event.</summary>
        protected virtual void OnDataContextChanged (EventArgs e)
        {
            DataContextChanged?.Invoke (this, e);

            // Children that have no context of their own now report a different one, so they need to
            // hear about it too -- that is what makes the property inheritable rather than per-control.
            foreach (var child in Controls)
                if (child.data_context is null)
                    child.OnDataContextChanged (e);
        }

        /// <summary>Raises the <see cref="ClientSizeChanged"/> event.</summary>
        protected virtual void OnClientSizeChanged (EventArgs e) => ClientSizeChanged?.Invoke (this, e);

        /// <summary>Raises the <see cref="RegionChanged"/> event.</summary>
        protected virtual void OnRegionChanged (EventArgs e) => RegionChanged?.Invoke (this, e);

        /// <summary>Exposes a <see cref="Control"/> to accessibility clients.</summary>
        public class ControlAccessibleObject : AccessibleObject
        {
            /// <summary>Initializes a new instance of the <see cref="ControlAccessibleObject"/> class.</summary>
            public ControlAccessibleObject (Control ownerControl) => Owner = ownerControl;

            /// <summary>Gets the control this object describes.</summary>
            public Control Owner { get; }

            /// <summary>Gets the name reported to assistive technology.</summary>
            public override string? Name => Owner.AccessibleName ?? Owner.Text;

            /// <summary>Gets the description reported to assistive technology.</summary>
            public override string? Description => Owner.AccessibleDescription;

            /// <summary>Gets the role reported to assistive technology.</summary>
            public override AccessibleRole Role
                => Owner.AccessibleRole is { } role and not AccessibleRole.Default ? role : AccessibleRole.Client;

            /// <summary>Gets the control's bounds.</summary>
            public override Rectangle Bounds => Owner.Bounds;
        }

        private static string AssemblyMetadata<TAttribute> (Func<TAttribute, string?> read) where TAttribute : Attribute
            => Assembly.GetEntryAssembly ()?.GetCustomAttribute<TAttribute> () is { } attribute
                ? read (attribute) ?? string.Empty
                : string.Empty;
    }

    public partial class Form
    {
        private Color form_border_color = Color.Empty;
        private Color form_caption_back_color = Color.Empty;
        private Color form_caption_text_color = Color.Empty;

        /// <summary>Gets or sets whether the form supports per-pixel transparency.</summary>
        public bool AllowTransparency { get; set; }

        /// <summary>Gets or sets whether the form scales itself to the system font.</summary>
        /// <remarks>Superseded by AutoScaleMode upstream and kept for designer files that still set
        /// it; scaling here is driven by the backend's DPI, not by this flag.</remarks>
        public bool AutoScale { get; set; }

        /// <summary>Gets or sets how the form sizes itself to its contents.</summary>
        public AutoSizeMode AutoSizeMode { get; set; } = AutoSizeMode.GrowOnly;

        /// <summary>Gets or sets whether the form is a tab stop.</summary>
        public bool TabStop {
            get => tab_stop;
            set {
                if (tab_stop == value)
                    return;

                tab_stop = value;
                TabStopChanged?.Invoke (this, EventArgs.Empty);
            }
        }

        private bool tab_stop = true;

        /// <summary>Gets or sets the colour of the form's border.</summary>
        public Color FormBorderColor {
            get => form_border_color;
            set => SetChromeColor (ref form_border_color, value, () => FormBorderColorChanged);
        }

        /// <summary>Gets or sets the background colour of the form's caption bar.</summary>
        public Color FormCaptionBackColor {
            get => form_caption_back_color;
            set => SetChromeColor (ref form_caption_back_color, value, () => FormCaptionBackColorChanged);
        }

        /// <summary>Gets or sets the text colour of the form's caption bar.</summary>
        public Color FormCaptionTextColor {
            get => form_caption_text_color;
            set => SetChromeColor (ref form_caption_text_color, value, () => FormCaptionTextColorChanged);
        }

        /// <summary>Gets or sets whether the form may be captured by screen-recording software.</summary>
        /// <remarks>Stored only: excluding a window from capture is an OS privilege this layer does
        /// not reach for, so setting it must not be read as protection.</remarks>
        public ScreenCaptureMode FormScreenCaptureMode { get; set; } = ScreenCaptureMode.Allow;

        /// <summary>Gets or sets whether minimized MDI children anchor to the bottom of the client area.</summary>
        public bool MdiChildrenMinimizedAnchorBottom { get; set; } = true;

        /// <summary>Gets or sets whether the form lays out right to left when RightToLeft is set.</summary>
        public virtual bool RightToLeftLayout {
            get => right_to_left_layout;
            set {
                if (right_to_left_layout == value)
                    return;

                right_to_left_layout = value;
                RightToLeftLayoutChanged?.Invoke (this, EventArgs.Empty);
                PerformLayout ();
            }
        }

        private bool right_to_left_layout;

        /// <summary>Gets whether the form runs with restricted permissions.</summary>
        /// <remarks>Always false: code access security was removed from .NET, so there is no
        /// restricted state for a window to be in.</remarks>
        public bool IsRestrictedWindow => false;

        /// <summary>Gets or sets the main menu displayed on the form.</summary>
        public MainMenu? Menu { get; set; }

        /// <summary>Gets the menu formed by merging this form's menu with its MDI children's.</summary>
        /// <remarks>MDI menu merging is not implemented, so this reports the form's own menu rather
        /// than a merged one that would be indistinguishable from it anyway.</remarks>
        public MainMenu? MergedMenu => Menu;

        /// <summary>Returns the size the form would scale to for the given font.</summary>
        public static SizeF GetAutoScaleSize (Majorsilence.Forms.Drawing.Font font)
        {
            ArgumentNullException.ThrowIfNull (font);

            // WinForms measures a fixed reference string, so the ratio between two fonts is what the
            // caller ends up using; the absolute number only has to be consistent.
            var typeface = SkiaSharp.SKTypeface.FromFamilyName (font.Name) ?? Theme.UIFont;
            var size = TextMeasurer.MeasureText ("AaBbYyZz", typeface, (int)Math.Round (font.SizeInPoints));
            return new SizeF (size.Width / 8f, size.Height);
        }

        /// <summary>Shows the form non-modally, owned by the given window.</summary>
        public Task ShowAsync (IWin32Window? owner = null)
        {
            Show ();
            return Task.CompletedTask;
        }

        /// <summary>Raised when <see cref="TabStop"/> changes.</summary>
        public event EventHandler? TabStopChanged;

        /// <summary>Raised when <see cref="FormBorderColor"/> changes.</summary>
        public event EventHandler? FormBorderColorChanged;

        /// <summary>Raised when <see cref="FormCaptionBackColor"/> changes.</summary>
        public event EventHandler? FormCaptionBackColorChanged;

        /// <summary>Raised when <see cref="FormCaptionTextColor"/> changes.</summary>
        public event EventHandler? FormCaptionTextColorChanged;

        /// <summary>Raised when <see cref="RightToLeftLayout"/> changes.</summary>
        public event EventHandler? RightToLeftLayoutChanged;

        /// <summary>Raised when the user clicks the caption bar's help button.</summary>
        public event System.ComponentModel.CancelEventHandler? HelpButtonClicked;

        // Declared and raisable, not raised by the framework yet -- said here rather than left for a
        // caller to discover by waiting for an event that never arrives.
#pragma warning disable CS0067
        /// <summary>Raised when the form's automatic sizing changes. Not raised by this layer yet.</summary>
        public event EventHandler? AutoSizeChanged;

        /// <summary>Raised when AutoValidate changes. Not raised by this layer yet.</summary>
        public event EventHandler? AutoValidateChanged;

        /// <summary>Raised when the form's corner preference changes. Not raised by this layer yet.</summary>
        public event EventHandler? FormCornerPreferenceChanged;

        /// <summary>Raised when the form's margin changes. Not raised by this layer yet.</summary>
        public event EventHandler? MarginChanged;

        /// <summary>Raised when MaximizedBounds changes. Not raised by this layer yet.</summary>
        public event EventHandler? MaximizedBoundsChanged;

        /// <summary>Raised when the tab index changes. Not raised by this layer yet.</summary>
        public event EventHandler? TabIndexChanged;

        /// <summary>Raised when a menu is about to be shown. Not raised by this layer yet.</summary>
        public event EventHandler? MenuStart;

        /// <summary>Raised when a menu has closed. Not raised by this layer yet.</summary>
        public event EventHandler? MenuComplete;
#pragma warning restore CS0067

        /// <summary>Raises the <see cref="HelpButtonClicked"/> event.</summary>
        protected virtual void OnHelpButtonClicked (System.ComponentModel.CancelEventArgs e)
            => HelpButtonClicked?.Invoke (this, e);

        /// <summary>The collection of controls on a <see cref="Form"/>.</summary>
        /// <remarks>WinForms nests a collection type here; this one adds nothing to the base, and
        /// exists so that <c>Form.ControlCollection</c> in migrated code resolves.</remarks>
        public class ControlCollection : Control.ControlCollection
        {
            /// <summary>Initializes a new instance of the <see cref="ControlCollection"/> class.</summary>
            public ControlCollection (Control owner) : base (owner) { }
        }

        private void SetChromeColor (ref Color field, Color value, Func<EventHandler?> handler)
        {
            if (field == value)
                return;

            field = value;
            handler ()?.Invoke (this, EventArgs.Empty);
            Invalidate ();
        }
    }
}
