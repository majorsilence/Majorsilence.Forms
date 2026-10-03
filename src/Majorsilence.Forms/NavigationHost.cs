using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Threading.Tasks;

namespace Majorsilence.Forms
{
    /// <summary>What a navigation did to the page stack.</summary>
    public enum NavigationKind
    {
        /// <summary>A page was pushed on top.</summary>
        Push,

        /// <summary>The top page was removed, revealing the one below.</summary>
        Pop,

        /// <summary>Every page above the first was removed.</summary>
        PopToRoot,

        /// <summary>The top page was swapped for another.</summary>
        Replace,
    }

    /// <summary>Data for <see cref="NavigationHost.Navigated"/>.</summary>
    public class NavigationEventArgs : EventArgs
    {
        /// <summary>Initializes a new <see cref="NavigationEventArgs"/>.</summary>
        public NavigationEventArgs (NavigationKind kind, Control? from, Control? to)
        {
            Kind = kind;
            From = from;
            To = to;
        }

        /// <summary>What the navigation did.</summary>
        public NavigationKind Kind { get; }

        /// <summary>The page that was showing before, or null for the first push.</summary>
        public Control? From { get; }

        /// <summary>The page that is showing now, or null if the stack is empty.</summary>
        public Control? To { get; }
    }

    /// <summary>Data for <see cref="NavigationHost.Navigating"/>. Set <see cref="CancelEventArgs.Cancel"/> to stop the navigation.</summary>
    public class NavigatingEventArgs : CancelEventArgs
    {
        /// <summary>Initializes a new <see cref="NavigatingEventArgs"/>.</summary>
        public NavigatingEventArgs (NavigationKind kind, Control? from, Control? to)
        {
            Kind = kind;
            From = from;
            To = to;
        }

        /// <summary>What the navigation will do.</summary>
        public NavigationKind Kind { get; }

        /// <summary>The page showing now, or null if the stack is empty.</summary>
        public Control? From { get; }

        /// <summary>The page that will be showing afterwards, or null if the stack will be empty.</summary>
        public Control? To { get; }
    }

    /// <summary>
    /// A stack of full-screen pages with a back button, the navigation model phone apps use: <see cref="PushAsync"/> a detail
    /// page over a list, <see cref="PopAsync"/> to return. Only the top page is visible and it fills the host. The page's own
    /// <see cref="Control.Text"/> is its title in the optional navigation bar.
    /// </summary>
    /// <remarks>
    /// <para>
    /// When the host is on a <see cref="Form"/> the platform back button or gesture (<see cref="WindowBase.BackRequested"/>) pops
    /// the stack, and is left alone when only one page remains so the platform's own behaviour (leaving the app) proceeds. On
    /// Android and iOS the app's own activity must forward the press to <c>AvaloniaPlatformBackend.RaiseBackRequested</c>; the
    /// framework cannot discover the activity itself. The host starts handling back as soon as it is visible on a form.
    /// </para>
    /// <para>
    /// Navigation methods return a <see cref="Task"/> so a transition can become asynchronous without an API change; today they
    /// complete synchronously. They must be called on the UI thread.
    /// </para>
    /// </remarks>
    public class NavigationHost : Panel
    {
        private const int BarHeight = 44;

        private readonly List<Control> pages = new ();
        private readonly Panel bar = new () { Dock = DockStyle.Top, Height = BarHeight };
        private readonly Button back_button = new () { Text = "Back", Dock = DockStyle.Left, Width = 80 };
        private readonly Label title_label = new () { Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleCenter, AutoSize = false };
        private readonly Panel content = new () { Dock = DockStyle.Fill };

        private Form? attached_form;
        private bool show_navigation_bar = true;

        /// <summary>Initializes a new instance of the <see cref="NavigationHost"/> class.</summary>
        public NavigationHost ()
        {
            back_button.Click += (_, _) => _ = PopAsync ();
            bar.Controls.Add (title_label);
            bar.Controls.Add (back_button);

            // The bar is added after the content so it is laid out first and claims the top strip.
            Controls.Add (content);
            Controls.Add (bar);
            UpdateChrome ();
        }

        /// <inheritdoc/>
        protected override Size DefaultSize => new Size (320, 480);

        /// <summary>The pages in the stack, bottom first. The last one is showing.</summary>
        public IReadOnlyList<Control> Pages => pages;

        /// <summary>The page that is showing, or null if the stack is empty.</summary>
        public Control? CurrentPage => pages.Count > 0 ? pages[^1] : null;

        /// <summary>Whether there is a page below the current one to return to.</summary>
        public bool CanGoBack => pages.Count > 1;

        /// <summary>Whether to show the bar with the page title and a back button. Defaults to true.</summary>
        public bool ShowNavigationBar {
            get => show_navigation_bar;
            set {
                if (show_navigation_bar == value)
                    return;
                show_navigation_bar = value;
                UpdateChrome ();
            }
        }

        /// <summary>Whether a page that is popped, replaced or cleared is disposed. Defaults to true.</summary>
        public bool DisposeRemovedPages { get; set; } = true;

        /// <summary>
        /// Whether the platform back button pops this host's stack. Defaults to true. Turn it off to handle
        /// <see cref="WindowBase.BackRequested"/> yourself, or on all but one host when hosts are nested.
        /// </summary>
        public bool HandleBackRequested { get; set; } = true;

        /// <summary>Raised before a navigation. Set <see cref="CancelEventArgs.Cancel"/> to stop it, for example to confirm leaving unsaved work.</summary>
        public event EventHandler<NavigatingEventArgs>? Navigating;

        /// <summary>Raised after a navigation has changed the stack and the new page is showing.</summary>
        public event EventHandler<NavigationEventArgs>? Navigated;

        /// <summary>Pushes <paramref name="page"/> on top of the stack and shows it. Returns false if <see cref="Navigating"/> cancelled it.</summary>
        public Task<bool> PushAsync (Control page)
        {
            Guard.ThrowIfNull (page);
            if (pages.Contains (page))
                throw new ArgumentException ("The page is already in the stack.", nameof (page));

            var from = CurrentPage;
            if (!RaiseNavigating (NavigationKind.Push, from, page))
                return Task.FromResult (false);

            from?.Hide ();
            pages.Add (page);
            AddToContent (page);
            Complete (NavigationKind.Push, from);
            return Task.FromResult (true);
        }

        /// <summary>
        /// Removes the top page and shows the one below. Returns the page that was removed, or null if there was nothing to go back to
        /// or <see cref="Navigating"/> cancelled it. The removed page is disposed when <see cref="DisposeRemovedPages"/> is set, so a
        /// caller that wants to keep it should turn that off.
        /// </summary>
        public Task<Control?> PopAsync ()
        {
            if (!CanGoBack)
                return Task.FromResult<Control?> (null);

            var from = pages[^1];
            var to = pages[^2];
            if (!RaiseNavigating (NavigationKind.Pop, from, to))
                return Task.FromResult<Control?> (null);

            pages.RemoveAt (pages.Count - 1);
            RemoveFromContent (from);
            to.Show ();
            Complete (NavigationKind.Pop, from);
            return Task.FromResult<Control?> (from);
        }

        /// <summary>Removes every page above the first and shows it. Returns false if there was nothing to remove or <see cref="Navigating"/> cancelled it.</summary>
        public Task<bool> PopToRootAsync ()
        {
            if (!CanGoBack)
                return Task.FromResult (false);

            var from = pages[^1];
            var root = pages[0];
            if (!RaiseNavigating (NavigationKind.PopToRoot, from, root))
                return Task.FromResult (false);

            for (var i = pages.Count - 1; i >= 1; i--) {
                var removed = pages[i];
                pages.RemoveAt (i);
                RemoveFromContent (removed);
            }

            root.Show ();
            Complete (NavigationKind.PopToRoot, from);
            return Task.FromResult (true);
        }

        /// <summary>Swaps the top page for <paramref name="page"/> (or pushes it if the stack is empty). Returns false if <see cref="Navigating"/> cancelled it.</summary>
        public Task<bool> ReplaceAsync (Control page)
        {
            Guard.ThrowIfNull (page);
            if (pages.Contains (page))
                throw new ArgumentException ("The page is already in the stack.", nameof (page));

            var from = CurrentPage;
            if (from is null)
                return PushAsync (page);

            if (!RaiseNavigating (NavigationKind.Replace, from, page))
                return Task.FromResult (false);

            pages[^1] = page;
            RemoveFromContent (from);
            AddToContent (page);
            Complete (NavigationKind.Replace, from);
            return Task.FromResult (true);
        }

        private bool RaiseNavigating (NavigationKind kind, Control? from, Control? to)
        {
            var args = new NavigatingEventArgs (kind, from, to);
            Navigating?.Invoke (this, args);
            return !args.Cancel;
        }

        private void AddToContent (Control page)
        {
            page.Dock = DockStyle.Fill;
            page.Visible = true;
            page.TextChanged += OnPageTextChanged;
            content.Controls.Add (page);
        }

        private void RemoveFromContent (Control page)
        {
            page.TextChanged -= OnPageTextChanged;
            content.Controls.Remove (page);
            if (DisposeRemovedPages)
                page.Dispose ();
        }

        private void Complete (NavigationKind kind, Control? from)
        {
            UpdateChrome ();
            content.PerformLayout ();
            Navigated?.Invoke (this, new NavigationEventArgs (kind, from, CurrentPage));
        }

        private void OnPageTextChanged (object? sender, EventArgs e)
        {
            if (ReferenceEquals (sender, CurrentPage))
                UpdateChrome ();
        }

        private void UpdateChrome ()
        {
            bar.Visible = show_navigation_bar;
            back_button.Visible = CanGoBack;
            title_label.Text = CurrentPage?.Text ?? string.Empty;
        }

        // ── Platform back button ─────────────────────────────────────────────────────────────────────

        /// <inheritdoc/>
        protected override void OnParentChanged (EventArgs e)
        {
            base.OnParentChanged (e);
            AttachToForm ();
        }

        /// <inheritdoc/>
        protected override void OnVisibleChanged (EventArgs e)
        {
            base.OnVisibleChanged (e);
            AttachToForm ();
        }

        private void AttachToForm ()
        {
            var form = FindForm ();
            if (ReferenceEquals (form, attached_form))
                return;

            if (attached_form is not null)
                attached_form.BackRequested -= OnFormBackRequested;
            attached_form = form;
            if (attached_form is not null)
                attached_form.BackRequested += OnFormBackRequested;
        }

        private void OnFormBackRequested (object? sender, CancelEventArgs e)
        {
            // Already handled by something earlier (a sheet, an inner host), or nothing to go back to: leave the
            // platform's own behaviour alone.
            if (e.Cancel || !HandleBackRequested || !Visible || !CanGoBack)
                return;

            e.Cancel = true;
            _ = PopAsync ();
        }

        /// <inheritdoc/>
        protected override void Dispose (bool disposing)
        {
            if (disposing && attached_form is not null) {
                attached_form.BackRequested -= OnFormBackRequested;
                attached_form = null;
            }

            base.Dispose (disposing);
        }
    }
}
