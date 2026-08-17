using System.Drawing;
using Majorsilence.Forms.Headless;
using Majorsilence.Forms.Renderers;
using Xunit;

namespace Majorsilence.Forms.Tests;

// MenuStrip, ContextMenuStrip and StatusStrip were re-parented onto ToolStrip so the hierarchy matches
// real WinForms (MenuStrip : ToolStrip, ContextMenuStrip : ToolStripDropDownMenu : ToolStripDropDown :
// ToolStrip, StatusStrip : ToolStrip). ToolStrip was spliced into the existing chain rather than bolted
// on: Menu : ToolStrip and MenuDropDown : ToolStrip, which leaves every renderer registration and all
// the real layout/popup behavior exactly where it was. These tests pin both halves of that: the
// ToolStrip surface is genuinely reachable, AND nothing that already worked moved or broke.
public class StripHierarchyTests
{
    // --- The hierarchy itself ---------------------------------------------------------------------

    [Fact]
    public void MenuStrip_IsToolStripDerived ()
    {
        Assert.IsAssignableFrom<ToolStrip> (new MenuStrip ());
    }

    [Fact]
    public void ContextMenuStrip_IsToolStripDerived ()
    {
        Assert.IsAssignableFrom<ToolStrip> (new ContextMenuStrip ());
    }

    [Fact]
    public void StatusStrip_IsToolStripDerived ()
    {
        Assert.IsAssignableFrom<ToolStrip> (new StatusStrip ());
    }

    // --- ToolStrip members are reachable from all three, and behave -------------------------------
    // Several are still stub-shaped (they store a value but nothing consumes it yet -- see the
    // COMPATIBILITY_MATRIX stub policy). The contract these pin is: reachable, round-trip, no throw.

    public static TheoryData<ToolStrip> AllThreeStrips => new () {
        new MenuStrip (),
        new ContextMenuStrip (),
        new StatusStrip (),
    };

    [Theory]
    [MemberData (nameof (AllThreeStrips))]
    public void ToolStripMembers_AreReachableAndRoundTrip (ToolStrip strip)
    {
        var renderer = new ToolStripProfessionalRenderer ();

        strip.Renderer = renderer;
        strip.RenderMode = ToolStripRenderMode.Professional;
        strip.LayoutStyle = ToolStripLayoutStyle.VerticalStackWithOverflow;
        strip.GripStyle = ToolStripGripStyle.Hidden;
        strip.Stretch = true;
        strip.CanOverflow = false;

        Assert.Same (renderer, strip.Renderer);
        Assert.Equal (ToolStripRenderMode.Professional, strip.RenderMode);
        Assert.Equal (ToolStripLayoutStyle.VerticalStackWithOverflow, strip.LayoutStyle);
        Assert.Equal (ToolStripGripStyle.Hidden, strip.GripStyle);
        Assert.True (strip.Stretch);
        Assert.False (strip.CanOverflow);
    }

    [Theory]
    [MemberData (nameof (AllThreeStrips))]
    public void ToolStripMembers_HaveWinFormsDefaults (ToolStrip strip)
    {
        Assert.Null (strip.Renderer);
        Assert.Equal (ToolStripRenderMode.ManagerRenderMode, strip.RenderMode);
        Assert.Equal (ToolStripLayoutStyle.HorizontalStackWithOverflow, strip.LayoutStyle);
        Assert.Equal (ToolStripGripStyle.Visible, strip.GripStyle);
        Assert.False (strip.Stretch);
        Assert.True (strip.CanOverflow);
        Assert.Equal (new Size (16, 16), strip.ImageScalingSize);
    }

    // --- MenuStrip still lays out and renders as a top-docked bar ---------------------------------

    [Fact]
    public void MenuStrip_DocksTop_ByDefault ()
    {
        Assert.Equal (DockStyle.Top, new MenuStrip ().Dock);
    }

    [Fact]
    public void MenuStrip_Items_IsStillTheCollectionTheRendererConsumes ()
    {
        // Menu re-exposes MenuBase's MenuItemCollection past ToolStrip's ToolStripItemCollection facade.
        // If the facade won instead, MenuRenderer/LayoutItems/hit-testing would all see an empty menu
        // while menuStrip.Items looked populated -- exactly the empty-bar bug the ToolStrip facade
        // forwarding was added to fix, re-introduced one level up.
        var strip = new MenuStrip ();
        var item = new ToolStripMenuItem ("File");

        strip.Items.Add (item);

        Assert.IsType<MenuItemCollection> (strip.Items);
        Assert.Same (((MenuBase)strip).Items, strip.Items);
        Assert.Contains (item, ((MenuBase)strip).Items);
    }

    [Fact]
    public void MenuStrip_LaysItemsOutLeftToRightAcrossTheBar ()
    {
        HeadlessRenderer.Use ();

        var form = new Form ();
        var strip = new MenuStrip ();
        var file = new ToolStripMenuItem ("File");
        var edit = new ToolStripMenuItem ("Edit");
        var view = new ToolStripMenuItem ("View");
        strip.Items.Add (file);
        strip.Items.Add (edit);
        strip.Items.Add (view);
        form.Controls.Add (strip);

        // A paint pass is what drives MenuBase.OnPaint -> LayoutItems.
        HeadlessRenderer.CapturePng (form, 500, 200);

        // Every item got real geometry...
        Assert.All (new[] { file, edit, view }, i => {
            Assert.True (i.Bounds.Width > 0, $"'{i.Text}' laid out with zero width.");
            Assert.True (i.Bounds.Height > 0, $"'{i.Text}' laid out with zero height.");
        });

        // ...stacked horizontally, in order, on a single row (this is the menu-BAR shape; a drop down
        // would stack these vertically instead).
        Assert.True (file.Bounds.Right <= edit.Bounds.Left, "Items are not stacked left-to-right.");
        Assert.True (edit.Bounds.Right <= view.Bounds.Left, "Items are not stacked left-to-right.");
        Assert.Equal (file.Bounds.Top, edit.Bounds.Top);
        Assert.Equal (file.Bounds.Top, view.Bounds.Top);

        // ...and the bar is still pinned to the top of the form's client area, above its own height.
        Assert.Equal (DockStyle.Top, strip.Dock);
        Assert.True (file.Bounds.Bottom <= strip.Height, "Items spilled outside the bar.");

        form.Close ();
    }

    [Fact]
    public void MenuStrip_RendersVisibleContent ()
    {
        // Rendering the same bar with and without items must produce different pixels. Before the
        // ToolStrip facade forwarding was in place, the equivalent ToolStrip case painted an identical
        // empty bar either way; this pins that MenuStrip never regresses into that.
        HeadlessRenderer.Use ();

        var withItems = CaptureMenuStrip ("File", "Edit");
        var withoutItems = CaptureMenuStrip ();

        Assert.False (withItems.SequenceEqual (withoutItems), "MenuStrip rendered as an empty bar.");
    }

    private static byte[] CaptureMenuStrip (params string[] itemTexts)
    {
        var form = new Form ();
        var strip = new MenuStrip ();

        foreach (var text in itemTexts)
            strip.Items.Add (new ToolStripMenuItem (text));

        form.Controls.Add (strip);

        var png = HeadlessRenderer.CapturePng (form, 400, 200);
        form.Close ();

        return png;
    }

    [Fact]
    public void MenuStrip_UsesMenuRenderer_NotToolBarRenderer ()
    {
        // MenuStrip is now a ToolBar by inheritance (via ToolStrip), so renderer resolution must still
        // stop at Menu on the way up. RenderManager keys renderers by concrete type and walks BaseType.
        Assert.IsType<MenuRenderer> (RenderManager.GetRenderer<Renderer> (new MenuStrip ()));
    }

    // --- ContextMenuStrip.Show (Point): the fixed bug ---------------------------------------------

    [Fact]
    public void ContextMenuStrip_ShowPoint_ActuallyOpensTheMenu ()
    {
        // REGRESSION: ContextMenuStrip used to override Show (Point) with an empty body, shadowing the
        // real inherited overloads. The single-Point overload -- the most idiomatic WinForms call shape
        // -- silently did nothing at all.
        HeadlessRenderer.Use ();

        var form = new Form ();
        form.Show ();

        var menu = new ContextMenuStrip ();
        menu.Items.Add (new ToolStripMenuItem ("Cut"));
        menu.Items.Add (new ToolStripMenuItem ("Copy"));

        menu.Show (new Point (40, 60));

        Assert.True (menu.Visible, "ContextMenuStrip.Show (Point) did not open the menu.");

        form.Close ();
    }

    [Fact]
    public void ContextMenuStrip_ShowPoint_RaisesOpeningThenOpened ()
    {
        HeadlessRenderer.Use ();

        var form = new Form ();
        form.Show ();

        var order = new List<string> ();
        var menu = new ContextMenuStrip ();
        menu.Items.Add (new ToolStripMenuItem ("Cut"));
        menu.Opening += (_, _) => order.Add ("Opening");
        menu.Opened += (_, _) => order.Add ("Opened");

        menu.Show (new Point (40, 60));

        Assert.Equal (new[] { "Opening", "Opened" }, order);

        form.Close ();
    }

    [Fact]
    public void ContextMenuStrip_ShowPoint_FullLifecycleFiresInWinFormsOrder ()
    {
        HeadlessRenderer.Use ();

        var form = new Form ();
        form.Show ();

        var order = new List<string> ();
        var menu = new ContextMenuStrip ();
        menu.Items.Add (new ToolStripMenuItem ("Cut"));
        menu.Opening += (_, _) => order.Add ("Opening");
        menu.Opened += (_, _) => order.Add ("Opened");
        menu.Closing += (_, _) => order.Add ("Closing");
        menu.Closed += (_, _) => order.Add ("Closed");

        menu.Show (new Point (40, 60));

        // Deactivate is the single guaranteed teardown path -- what Application.ClosePopups, an outside
        // click and focus loss all funnel into. Called directly rather than via ClosePopups because
        // Application.ActiveMenu is process-global state other test classes also touch.
        menu.Deactivate ();

        Assert.Equal (new[] { "Opening", "Opened", "Closing", "Closed" }, order);
        Assert.False (menu.Visible);

        form.Close ();
    }

    [Fact]
    public void ContextMenuStrip_ShowPoint_CancellingOpening_KeepsMenuClosed ()
    {
        HeadlessRenderer.Use ();

        var form = new Form ();
        form.Show ();

        var openedRaised = false;
        var menu = new ContextMenuStrip ();
        menu.Items.Add (new ToolStripMenuItem ("Cut"));
        menu.Opening += (_, e) => e.Cancel = true;
        menu.Opened += (_, _) => openedRaised = true;

        menu.Show (new Point (40, 60));

        Assert.False (menu.Visible);
        Assert.False (openedRaised);

        form.Close ();
    }

    [Fact]
    public void ContextMenuStrip_ShowControlPoint_StillWorksAndSetsSourceControl ()
    {
        HeadlessRenderer.Use ();

        var form = new Form ();
        var host = new Panel { Left = 10, Top = 10, Width = 100, Height = 50 };
        form.Controls.Add (host);
        form.Show ();

        var menu = new ContextMenuStrip ();
        menu.Items.Add (new ToolStripMenuItem ("Cut"));

        menu.Show (host, new Point (5, 5));

        Assert.True (menu.Visible);
        Assert.Same (host, menu.SourceControl);

        form.Close ();
    }

    [Fact]
    public void ContextMenuStrip_Items_IsStillTheCollectionTheRendererConsumes ()
    {
        var menu = new ContextMenuStrip ();
        var item = new ToolStripMenuItem ("Cut");

        menu.Items.Add (item);

        Assert.IsType<MenuItemCollection> (menu.Items);
        Assert.Same (((MenuBase)menu).Items, menu.Items);
        Assert.Same (menu.Items, menu.MenuItems);
        Assert.Contains (item, ((MenuBase)menu).Items);
    }

    [Fact]
    public void ContextMenuStrip_UsesMenuDropDownRenderer_NotToolBarRenderer ()
    {
        Assert.IsType<MenuDropDownRenderer> (RenderManager.GetRenderer<Renderer> (new ContextMenuStrip ()));
    }

    [Fact]
    public void MenuDropDown_ItemPreferredSize_ComesFromMenuDropDownRenderer ()
    {
        // MenuDropDown is now a ToolBar by inheritance, so MenuItem.GetPreferredSize's owner-type
        // dispatch has to test MenuDropDown before ToolBar. If ToolBar won, drop-down items would fall
        // through to the un-matched default and report the bare proposed size.
        var menu = new ContextMenuStrip ();
        var item = new ToolStripMenuItem ("Copy");
        menu.Items.Add (item);

        var size = item.GetPreferredSize (Size.Empty);

        Assert.True (size.Width > 0, "Drop-down item measured to zero width.");
        Assert.True (size.Height > 0, "Drop-down item measured to zero height (ToolBarRenderer's shape).");
    }

    // --- StatusStrip still renders its items through its own renderer -----------------------------

    [Fact]
    public void StatusStrip_DocksBottom_ByDefault ()
    {
        // ToolBar's constructor docks bars to the Top; StatusStrip must still override that.
        Assert.Equal (DockStyle.Bottom, new StatusStrip ().Dock);
    }

    [Fact]
    public void StatusStrip_StillResolvesItsOwnRenderer ()
    {
        // RenderManager keys on the concrete type and walks BaseType, so re-parenting off Control must
        // not shift StatusStrip onto ToolBarRenderer.
        Assert.IsType<StatusStripRenderer> (RenderManager.GetRenderer<Renderer> (new StatusStrip ()));
    }

    [Fact]
    public void StatusStrip_Items_ReachTheBaseCollection ()
    {
        // StatusStrip dropped its own private ToolStripItemCollection for ToolStrip's forwarding facade,
        // so its items now also reach the collection LayoutItems and hit-testing consume.
        var strip = new StatusStrip ();
        var label = new ToolStripStatusLabel { Text = "Ready", Size = new Size (120, 17) };

        strip.Items.Add (label);

        Assert.Contains (label, strip.Items);
        Assert.Contains (label, ((MenuBase)strip).Items);
    }

    [Fact]
    public void StatusStrip_RendersItsItems ()
    {
        // StatusStrip lost its own OnPaint override (MenuBase's already dispatches to RenderManager), so
        // prove the items still actually reach pixels through StatusStripRenderer.
        HeadlessRenderer.Use ();

        var withLabel = CaptureStatusStrip (new ToolStripStatusLabel { Text = "Ready", Size = new Size (150, 17) });
        var empty = CaptureStatusStrip ();

        Assert.False (withLabel.SequenceEqual (empty), "StatusStrip rendered no item content.");
    }

    [Fact]
    public void StatusStrip_LaysItemsOutWhereItPaintsThem ()
    {
        // Status items keep their own width and sit left-to-right -- they must NOT be expanded to fill the
        // bar the way ToolBar's buttons are (which is what StatusStrip would inherit unguarded now that
        // it's ToolStrip-derived), because then the clickable region wouldn't match the painted one.
        HeadlessRenderer.Use ();

        var form = new Form ();
        var strip = new StatusStrip ();
        var ready = new ToolStripStatusLabel { Text = "Ready", Size = new Size (150, 17) };
        var hidden = new ToolStripStatusLabel { Text = "Hidden", Size = new Size (90, 17), Visible = false };
        var working = new ToolStripStatusLabel { Text = "Working", Size = new Size (100, 17) };
        strip.Items.Add (ready);
        strip.Items.Add (hidden);
        strip.Items.Add (working);
        form.Controls.Add (strip);

        HeadlessRenderer.CapturePng (form, 500, 200);

        var rect = strip.PaddedClientRectangle;

        Assert.Equal (new Rectangle (rect.X, rect.Y, 150, rect.Height), ready.Bounds);
        Assert.Equal (Rectangle.Empty, hidden.Bounds);
        // Follows 'ready' directly -- the hidden item consumed no space.
        Assert.Equal (new Rectangle (rect.X + 150 + StatusStrip.ItemSpacing, rect.Y, 100, rect.Height), working.Bounds);

        form.Close ();
    }

    [Fact]
    public void StatusStrip_HitTesting_MatchesThePaintedItems ()
    {
        HeadlessRenderer.Use ();

        var form = new Form ();
        var strip = new StatusStrip ();
        var ready = new ToolStripStatusLabel { Text = "Ready", Size = new Size (150, 17) };
        var working = new ToolStripStatusLabel { Text = "Working", Size = new Size (100, 17) };
        strip.Items.Add (ready);
        strip.Items.Add (working);
        form.Controls.Add (strip);

        HeadlessRenderer.CapturePng (form, 500, 200);

        Assert.Same (ready, strip.GetItemAtLocation (Center (ready.Bounds)));
        Assert.Same (working, strip.GetItemAtLocation (Center (working.Bounds)));
        // Empty space past the last item belongs to no item.
        Assert.Null (strip.GetItemAtLocation (new Point (working.Bounds.Right + 20, Center (working.Bounds).Y)));

        form.Close ();
    }

    private static Point Center (Rectangle r) => new (r.X + r.Width / 2, r.Y + r.Height / 2);

    [Fact]
    public void StatusStrip_ProgressBarItem_StillRendersItsFill ()
    {
        // StatusStripRenderer's ToolStripProgressBar arm paints an accent-coloured fill proportional to
        // Value. A 100% bar and a 0% bar must not produce the same pixels.
        HeadlessRenderer.Use ();

        var empty = CaptureStatusStrip (new ToolStripProgressBar { Size = new Size (150, 16), Minimum = 0, Maximum = 100, Value = 0 });
        var full = CaptureStatusStrip (new ToolStripProgressBar { Size = new Size (150, 16), Minimum = 0, Maximum = 100, Value = 100 });

        Assert.False (empty.SequenceEqual (full), "Progress fill did not affect the rendered StatusStrip.");
    }

    private static byte[] CaptureStatusStrip (params ToolStripItem[] items)
    {
        var form = new Form ();
        var strip = new StatusStrip ();

        foreach (var item in items)
            strip.Items.Add (item);

        form.Controls.Add (strip);

        var png = HeadlessRenderer.CapturePng (form, 400, 200);
        form.Close ();

        return png;
    }

    // --- The rest of the family must not have shifted ---------------------------------------------

    [Fact]
    public void PlainMenu_And_PlainToolBar_KeepTheirOwnRenderers ()
    {
        Assert.IsType<MenuRenderer> (RenderManager.GetRenderer<Renderer> (new Menu ()));
        Assert.IsType<ToolBarRenderer> (RenderManager.GetRenderer<Renderer> (new ToolBar ()));
        Assert.IsType<ToolBarRenderer> (RenderManager.GetRenderer<Renderer> (new ToolStrip ()));
    }

    [Fact]
    public void MenuDropDown_BuiltAroundAnExistingItem_StillSeesThatItemsChildren ()
    {
        // MenuItem.ShowDropDown builds submenus through MenuDropDown (MenuItem root), which now has to
        // reach MenuBase (MenuItem) through ToolStrip's and ToolBar's protected forwarding constructors.
        var root = new ToolStripMenuItem ("File");
        var child = new ToolStripMenuItem ("Open");
        root.DropDownItems.Add (child);

        var dropDown = new MenuDropDown (root);

        Assert.Contains (child, dropDown.Items);
        Assert.Same (dropDown, child.ParentControl);
    }

    [Fact]
    public void MenuStripItem_WithChildren_StillOpensItsSubmenu ()
    {
        HeadlessRenderer.Use ();

        var form = new Form ();
        var strip = new MenuStrip ();
        var file = new ToolStripMenuItem ("File");
        file.DropDownItems.Add (new ToolStripMenuItem ("Open"));
        file.DropDownItems.Add (new ToolStripMenuItem ("Save"));
        strip.Items.Add (file);
        form.Controls.Add (strip);
        form.Show ();
        HeadlessRenderer.CapturePng (form, 500, 200);

        file.ShowDropDown ();

        Assert.True (file.IsDropDownOpened, "A MenuStrip item no longer opens its drop down.");

        form.Close ();
    }

    // --- One collection, both names ---------------------------------------------------------------

    // System.Windows.Forms types every strip's Items as ToolStripItemCollection, so ported code declares
    // helpers that way -- `void InvertIcons(ToolStripItemCollection items)` and the like. The menus here
    // hand out a MenuItemCollection, and until these two types were joined there was no conversion
    // between them, so every such call failed to compile.
    [Fact]
    public void A_menus_Items_satisfies_a_ToolStripItemCollection_parameter ()
    {
        using var menu = new ContextMenuStrip ();

        static int Count (ToolStripItemCollection items) => items.Count;

        menu.Items.Add (new ToolStripMenuItem ("One"));

        Assert.Equal (1, Count (menu.Items));   // would not compile before
    }

    // The point of sharing the type rather than projecting between two: there is one storage, so an item
    // added through either name is visible through the other. A copying facade would have missed this.
    [Fact]
    public void Both_names_see_the_same_storage ()
    {
        using var menu = new ContextMenuStrip ();

        ToolStripItemCollection asStripItems = menu.Items;
        MenuItemCollection asMenuItems = menu.Items;

        asStripItems.Add (new ToolStripMenuItem ("added as a strip item"));
        asMenuItems.Add (new ToolStripMenuItem ("added as a menu item"));

        Assert.Equal (2, asStripItems.Count);
        Assert.Equal (2, asMenuItems.Count);
        Assert.Same (asStripItems, asMenuItems);
    }

    // The reason a live view over the collection could not work: these menus legitimately hold plain
    // MenuItems and separators, which are not ToolStripItems and have no ToolStripItem to project to.
    [Fact]
    public void The_collection_holds_plain_menu_items_too ()
    {
        using var menu = new ContextMenuStrip ();

        menu.Items.Add (new ToolStripMenuItem ("a strip item"));
        menu.Items.Add ("added by text");
        menu.Items.Add (new MenuSeparatorItem ());

        Assert.Equal (3, menu.Items.Count);

        // Add(string) builds a ToolStripMenuItem, as ToolStripItemCollection.Add(string) does in WinForms
        // -- code assigns its result to a ToolStripItem, and returning a plain MenuItem compiled and then
        // threw InvalidCastException at runtime. So two of the three are ToolStripItems here.
        Assert.Equal (2, menu.Items.OfType<ToolStripItem> ().Count ());

        // The point of the test survives: the collection still holds a plain MenuItem alongside them, which
        // is why members like key lookup have to tolerate entries that are not ToolStripItems.
        Assert.Single (menu.Items.Where (i => i is not ToolStripItem));
    }

    // Key lookup is a ToolStripItem concept (Name lives there), so a plain MenuItem must simply never
    // match rather than throwing as the collection is walked.
    [Fact]
    public void Key_lookup_skips_items_that_have_no_name ()
    {
        using var menu = new ContextMenuStrip ();

        menu.Items.Add ("a plain menu item");
        menu.Items.Add (new ToolStripMenuItem ("Named") { Name = "target" });

        Assert.True (menu.Items.ContainsKey ("target"));
        Assert.Equal (1, menu.Items.IndexOfKey ("target"));
        Assert.False (menu.Items.ContainsKey ("absent"));
    }

}
