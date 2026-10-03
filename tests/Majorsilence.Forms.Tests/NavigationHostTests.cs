using System.Collections.Generic;
using System.Drawing;
using System.Threading.Tasks;
using Majorsilence.Forms.Headless;
using Xunit;

namespace Majorsilence.Forms.Tests;

[Collection ("Headless")]
public class NavigationHostTests
{
    private static (Form form, NavigationHost host) Show ()
    {
        HeadlessRenderer.Use ();
        var form = new Form { Size = new Size (400, 700) };
        var host = new NavigationHost { Dock = DockStyle.Fill };
        form.Controls.Add (host);
        form.Show ();
        return (form, host);
    }

    private static Panel Page (string title) => new () { Text = title };

    [Fact]
    public async Task Push_shows_only_the_top_page_and_pop_returns_to_the_one_below ()
    {
        var (form, host) = Show ();
        using var _ = form;
        var list = Page ("Posts");
        var detail = Page ("Post");

        await host.PushAsync (list);
        await host.PushAsync (detail);

        Assert.Same (detail, host.CurrentPage);
        Assert.True (host.CanGoBack);
        Assert.False (list.Visible);
        Assert.True (detail.Visible);

        var popped = await host.PopAsync ();

        Assert.Same (detail, popped);
        Assert.Same (list, host.CurrentPage);
        Assert.True (list.Visible);
        Assert.False (host.CanGoBack);
    }

    [Fact]
    public async Task Pop_with_one_page_does_nothing ()
    {
        var (form, host) = Show ();
        using var _ = form;
        await host.PushAsync (Page ("Only"));

        Assert.Null (await host.PopAsync ());
        Assert.Single (host.Pages);
    }

    [Fact]
    public async Task The_current_page_fills_the_area_below_the_navigation_bar ()
    {
        var (form, host) = Show ();
        using var _ = form;
        var page = Page ("Posts");
        await host.PushAsync (page);
        HeadlessRenderer.CapturePng (form);

        var inHost = host.RectangleToClient (page.RectangleToScreen (page.ClientRectangle));
        Assert.True (inHost.Top >= 40, $"page top {inHost.Top} should be below the bar");
        Assert.Equal (host.ClientSize.Width, page.Width);
    }

    [Fact]
    public async Task Hiding_the_bar_lets_the_page_use_the_full_height ()
    {
        var (form, host) = Show ();
        using var _ = form;
        host.ShowNavigationBar = false;
        var page = Page ("Posts");
        await host.PushAsync (page);
        HeadlessRenderer.CapturePng (form);

        var inHost = host.RectangleToClient (page.RectangleToScreen (page.ClientRectangle));
        Assert.Equal (0, inHost.Top);
    }

    [Fact]
    public async Task Popped_pages_are_disposed_unless_that_is_turned_off ()
    {
        var (form, host) = Show ();
        using var _ = form;
        var root = Page ("root");
        var a = Page ("a");
        var b = Page ("b");
        await host.PushAsync (root);
        await host.PushAsync (a);
        await host.PushAsync (b);

        await host.PopAsync ();
        Assert.True (b.IsDisposed);

        host.DisposeRemovedPages = false;
        await host.PopAsync ();
        Assert.False (a.IsDisposed);
    }

    [Fact]
    public async Task PopToRoot_removes_everything_above_the_first_page ()
    {
        var (form, host) = Show ();
        using var _ = form;
        var root = Page ("root");
        await host.PushAsync (root);
        await host.PushAsync (Page ("a"));
        await host.PushAsync (Page ("b"));

        Assert.True (await host.PopToRootAsync ());

        Assert.Same (root, host.CurrentPage);
        Assert.Single (host.Pages);
        Assert.True (root.Visible);
        Assert.False (await host.PopToRootAsync ());
    }

    [Fact]
    public async Task Replace_swaps_the_top_page ()
    {
        var (form, host) = Show ();
        using var _ = form;
        var root = Page ("root");
        var old = Page ("old");
        var next = Page ("next");
        await host.PushAsync (root);
        await host.PushAsync (old);

        await host.ReplaceAsync (next);

        Assert.Equal (new Control[] { root, next }, host.Pages);
        Assert.True (old.IsDisposed);
        Assert.True (next.Visible);
    }

    [Fact]
    public async Task Navigating_can_cancel_and_Navigated_reports_what_happened ()
    {
        var (form, host) = Show ();
        using var _ = form;
        var log = new List<string> ();
        host.Navigated += (_, e) => log.Add ($"{e.Kind}:{e.From?.Text}->{e.To?.Text}");
        await host.PushAsync (Page ("list"));
        await host.PushAsync (Page ("detail"));

        host.Navigating += (_, e) => e.Cancel = true;
        Assert.Null (await host.PopAsync ());
        Assert.Equal ("detail", host.CurrentPage?.Text);

        Assert.Equal (new[] { "Push:->list", "Push:list->detail" }, log);
    }

    [Fact]
    public async Task The_title_follows_the_current_pages_text ()
    {
        var (form, host) = Show ();
        using var _ = form;
        var page = Page ("First");
        await host.PushAsync (page);

        page.Text = "Renamed";
        await host.PushAsync (Page ("Second"));

        await host.PopAsync ();
        Assert.Equal ("Renamed", host.CurrentPage?.Text);
    }

    [Fact]
    public async Task Pushing_a_page_twice_throws ()
    {
        var (form, host) = Show ();
        using var _ = form;
        var page = Page ("a");
        await host.PushAsync (page);

        await Assert.ThrowsAsync<System.ArgumentException> (() => host.PushAsync (page));
    }

    [Fact]
    public async Task The_platform_back_button_pops_and_asks_to_stay ()
    {
        var (form, host) = Show ();
        using var _ = form;
        await host.PushAsync (Page ("list"));
        await host.PushAsync (Page ("detail"));

        Assert.True (form.RaiseBackRequested ());   // handled: the app stays open
        Assert.Equal ("list", host.CurrentPage?.Text);
    }

    [Fact]
    public async Task Back_at_the_root_is_left_to_the_platform ()
    {
        var (form, host) = Show ();
        using var _ = form;
        await host.PushAsync (Page ("list"));

        Assert.False (form.RaiseBackRequested ());   // not handled: the platform leaves the app
    }

    [Fact]
    public async Task Back_handling_can_be_turned_off ()
    {
        var (form, host) = Show ();
        using var _ = form;
        host.HandleBackRequested = false;
        await host.PushAsync (Page ("list"));
        await host.PushAsync (Page ("detail"));

        Assert.False (form.RaiseBackRequested ());
        Assert.Equal ("detail", host.CurrentPage?.Text);
    }

    [Fact]
    public async Task Disposing_the_host_stops_it_handling_back ()
    {
        var (form, host) = Show ();
        using var _ = form;
        await host.PushAsync (Page ("list"));
        await host.PushAsync (Page ("detail"));

        host.Dispose ();

        Assert.False (form.RaiseBackRequested ());
    }
}
