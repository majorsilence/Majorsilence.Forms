using System.Drawing;
using Majorsilence.Forms.Headless;
using Majorsilence.Forms.Telerik;
using Xunit;

namespace Majorsilence.Forms.Tests;

// W6 mechanisms, twenty-first chunk (#176, dock layout): the tab selection model, the tool strip's
// caption and tab placement, the document close box, hidden windows, the allowed-state gate, the three
// docking events that never fired, and layout save and load.
[Collection ("Headless")]
public class W6TelerikDockingTests
{
    private static RadDock Dock (out Form form, out DocumentTabStrip strip, params string[] documents)
    {
        HeadlessRenderer.Use ();
        form = new Form { Size = new Size (500, 400) };
        var dock = new RadDock { Bounds = new Rectangle (0, 0, 480, 360) };
        var container = new DocumentContainer ();
        dock.Controls.Add (container);
        dock.MainDocumentContainer = container;
        form.Controls.Add (dock);
        form.Show ();

        foreach (var name in documents)
            dock.DockWindow (new DocumentWindow (name) { Name = name });

        strip = dock.GetDefaultDocumentTabStrip (createIfMissing: false);
        Settle (dock);
        return dock;
    }

    // Lays the chain out and paints the strips, which is what records their header rectangles.
    private static void Settle (RadDock dock)
    {
        dock.PerformLayout ();

        foreach (var control in Descendants (dock)) {
            control.PerformLayout ();

            if (control is DocumentTabStrip or ToolTabStrip)
                PaintSurface.Render (control).Dispose ();
        }
    }

    private static IEnumerable<Control> Descendants (Control root)
    {
        foreach (Control child in root.Controls) {
            yield return child;

            foreach (var nested in Descendants (child))
                yield return nested;
        }
    }

    private static Rectangle HeaderOf (DocumentTabStrip strip, string name)
        => strip.Headers.Rects.Single (r => r.win.Name == name).rect;

    private static void Click (Control strip, Rectangle logical, MouseButtons button = MouseButtons.Left)
        => strip.RaiseMouseDown (new MouseEventArgs (button, 1, logical.Left + 5, logical.Top + (logical.Height / 2), 0));

    // ── the tab selection model ─────────────────────────────────────────────────────────────────────

    [Fact]
    public void SelectedIndex_SelectedTab_and_ActiveWindow_select_a_tab_and_announce_it ()
    {
        var dock = Dock (out var form, out var strip, "Alpha", "Bravo", "Charlie");

        using (form) {
            var index_changes = 0;
            var tab_changes = new List<string?> ();
            strip.SelectedIndexChanged += (_, _) => index_changes++;
            dock.SelectedTabChanged += (_, e) => tab_changes.Add (e.NewWindow?.Name);

            Assert.Equal (0, strip.SelectedIndex);

            strip.SelectedIndex = 2;
            Assert.Equal ("Charlie", ((DockWindowBase) strip.SelectedTab!).Name);
            Assert.True (((DockWindowBase) strip.SelectedTab!).Visible);

            strip.SelectedTab = strip.Controls.OfType<DocumentWindow> ().First (w => w.Name == "Bravo");
            Assert.Equal (1, strip.SelectedIndex);

            dock.ActiveWindow = strip.Controls.OfType<DocumentWindow> ().First (w => w.Name == "Alpha");
            Assert.Equal (0, strip.SelectedIndex);

            Assert.Equal (3, index_changes);
            Assert.Equal (new[] { "Charlie", "Bravo", "Alpha" }, tab_changes);
        }
    }

    [Fact]
    public void An_index_set_before_the_windows_are_added_applies_once_they_are ()
    {
        HeadlessRenderer.Use ();
        using var form = new Form { Size = new Size (400, 300) };
        var strip = new DocumentTabStrip { Bounds = new Rectangle (0, 0, 380, 260) };

        // The designer's order: the property, then the children, then layout.
        strip.SelectedIndex = 1;
        strip.Controls.Add (new DocumentWindow ("First") { Name = "First" });
        strip.Controls.Add (new DocumentWindow ("Second") { Name = "Second" });
        form.Controls.Add (strip);
        form.Show ();
        strip.PerformLayout ();

        Assert.Equal (1, strip.SelectedIndex);
        Assert.Equal ("Second", strip.ActiveWindow!.Name);
    }

    // ── hidden windows, the close box and the allowed-state gate ────────────────────────────────────

    [Fact]
    public void A_hidden_window_loses_its_tab_and_gets_it_back ()
    {
        var dock = Dock (out var form, out var strip, "Alpha", "Bravo");

        using (form) {
            var bravo = strip.Controls.OfType<DocumentWindow> ().First (w => w.Name == "Bravo");
            strip.SelectedTab = bravo;

            bravo.Close ();   // CloseAction.Hide

            Assert.Equal (DockState.Hidden, bravo.DockState);
            Assert.Equal (DockState.Docked, bravo.PreviousDockState);
            Assert.False (bravo.Visible);
            Assert.Equal ("Alpha", strip.ActiveWindow!.Name);
            Assert.Equal (new[] { bravo }, dock.GetWindows (DockState.Hidden));

            Settle (dock);
            Assert.DoesNotContain (strip.Headers.Rects, r => r.win == bravo);

            bravo.DockState = DockState.TabbedDocument;
            strip.SelectedTab = bravo;
            Assert.True (bravo.Visible);
            Assert.Equal (DockState.Hidden, bravo.PreviousDockState);
        }
    }

    [Fact]
    public void The_close_box_closes_its_tab_and_DocumentButtons_takes_it_away ()
    {
        var dock = Dock (out var form, out var strip, "Alpha", "Bravo");

        using (form) {
            var close = strip.Headers.CloseRects.Single (r => r.win.Name == "Bravo").rect;
            strip.RaiseMouseDown (new MouseEventArgs (MouseButtons.Left, 1, close.Left + (close.Width / 2), close.Top + (close.Height / 2), 0));

            var bravo = strip.Controls.OfType<DocumentWindow> ().First (w => w.Name == "Bravo");
            Assert.Equal (DockState.Hidden, bravo.DockState);

            // Without the Close button the tabs carry no box to hit.
            bravo.DockState = DockState.TabbedDocument;
            strip.DocumentButtons = DocumentStripButtons.None;
            Settle (dock);
            Assert.Empty (strip.Headers.CloseRects);
        }
    }

    [Fact]
    public void FloatWindow_honours_AllowedDockState ()
    {
        var dock = Dock (out var form, out var strip, "Alpha");

        using (form) {
            var alpha = strip.Controls.OfType<DocumentWindow> ().Single ();

            alpha.AllowedDockState = AllowedDockState.Docked | AllowedDockState.TabbedDocument;
            dock.FloatWindow (alpha);
            Assert.NotEqual (DockState.Floating, alpha.DockState);

            alpha.AllowedDockState = AllowedDockState.All;
            dock.FloatWindow (alpha);
            Assert.Equal (DockState.Floating, alpha.DockState);
        }
    }

    // ── the tool strip's shape ──────────────────────────────────────────────────────────────────────

    [Fact]
    public void A_tool_strip_places_its_caption_and_tabs_as_asked ()
    {
        HeadlessRenderer.Use ();
        using var form = new Form { Size = new Size (400, 300) };
        var strip = new ToolTabStrip { Bounds = new Rectangle (0, 0, 300, 200) };
        var one = new ToolWindow ("One") { Name = "One" };
        var two = new ToolWindow ("Two") { Name = "Two" };
        strip.Controls.Add (one);
        strip.Controls.Add (two);
        form.Controls.Add (strip);
        form.Show ();
        strip.PerformLayout ();

        // Caption band on top, then the tab headers, then the content.
        Assert.Equal (DockStrip.CaptionHeight + DockStrip.HeaderHeight, one.Top);

        strip.CaptionVisible = false;
        Assert.Equal (DockStrip.HeaderHeight, one.Top);

        // Tabs along the bottom: the content starts at the top and stops above them.
        strip.TabStripAlignment = TabStripAlignment.Bottom;
        Assert.Equal (0, one.Top);
        Assert.Equal (200 - DockStrip.HeaderHeight, one.Bottom);

        // No tabs at all: the active window takes the whole strip.
        strip.TabStripVisible = false;
        Assert.Equal (new Rectangle (0, 0, 300, 200), one.Bounds);

        // The window reports the strip it is in.
        Assert.Same (strip, one.TabStrip);
        Assert.Equal (0, strip.SelectedIndex);
        strip.SelectedIndex = 1;
        Assert.Same (two, strip.ActiveWindow);
    }

    // ── the three docking events ────────────────────────────────────────────────────────────────────

    [Fact]
    public void DockTabStripNeeded_lets_the_application_supply_the_document_strip ()
    {
        HeadlessRenderer.Use ();
        using var form = new Form { Size = new Size (500, 400) };
        var dock = new RadDock { Bounds = new Rectangle (0, 0, 480, 360) };
        form.Controls.Add (dock);
        form.Show ();

        var mine = new DocumentTabStrip { Name = "mine" };
        var asked = 0;
        dock.DockTabStripNeeded += (_, e) => { asked++; e.Strip = mine; };

        dock.DockWindow (new DocumentWindow ("Doc") { Name = "Doc" });

        Assert.Equal (1, asked);
        Assert.Same (dock, mine.Parent);
        Assert.Same (mine, dock.GetDefaultDocumentTabStrip (createIfMissing: true));
        Assert.Equal (1, asked);   // found, not needed again
    }

    [Fact]
    public void A_right_click_on_a_tab_offers_its_menu_through_the_ContextMenuService ()
    {
        var dock = Dock (out var form, out var strip, "Alpha", "Bravo", "Charlie");

        using (form) {
            var service = dock.GetService<ContextMenuService> ()!;
            ContextMenuDisplayingEventArgs? seen = null;
            service.ContextMenuDisplaying += (_, e) => seen = e;

            Click (strip, HeaderOf (strip, "Bravo"), MouseButtons.Right);

            Assert.NotNull (seen);
            Assert.Equal ("Bravo", seen!.DockWindow!.Name);
            Assert.Equal (new[] { "Close", "Close All But This", "Close All" }, seen.MenuItems.Cast<MenuItem> ().Select (i => i.Text));

            // The items do what they say.
            ((MenuItem) seen.MenuItems[1]).PerformClick ();

            var states = strip.Controls.OfType<DocumentWindow> ().ToDictionary (w => w.Name, w => w.DockState);
            Assert.Equal (DockState.Hidden, states["Alpha"]);
            Assert.Equal (DockState.Hidden, states["Charlie"]);
            Assert.NotEqual (DockState.Hidden, states["Bravo"]);
        }
    }

    // ── save and load ───────────────────────────────────────────────────────────────────────────────

    [Fact]
    public void A_saved_layout_restores_dock_states_and_the_selected_tab ()
    {
        var dock = Dock (out var form, out var strip, "Alpha", "Bravo", "Charlie");

        using (form) {
            var windows = strip.Controls.OfType<DocumentWindow> ().ToDictionary (w => w.Name);
            windows["Alpha"].Close ();
            strip.SelectedTab = windows["Charlie"];

            using var saved = new MemoryStream ();
            dock.SaveToXml (saved);

            // The user rearranges things; the saved layout is not what is on screen any more.
            windows["Alpha"].DockState = DockState.TabbedDocument;
            strip.SelectedTab = windows["Bravo"];

            saved.Position = 0;
            dock.LoadFromXml (saved);

            Assert.Equal (DockState.Hidden, windows["Alpha"].DockState);
            Assert.Same (windows["Charlie"], strip.SelectedTab);

            // Not a layout document at all.
            Assert.Throws<System.Xml.XmlException> (() => dock.LoadFromXml (new MemoryStream ("not xml"u8.ToArray ())));
        }
    }
}
