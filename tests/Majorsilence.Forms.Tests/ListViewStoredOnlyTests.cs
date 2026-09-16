using System.Drawing;
using Majorsilence.Forms.Headless;
using Xunit;

namespace Majorsilence.Forms.Tests
{
    // W6.2, the stored-only sweep (RC-7) -- the ListView slice. Three of the control's 20 baseline
    // entries had a real consumer waiting for them:
    //
    //   TileSize                        the tile layout used a hard-coded 70 and never read it
    //   HideSelection                   the renderer highlighted a selection whatever the focus was
    //   UseCompatibleStateImageBehavior stored with the WRONG DEFAULT (false; upstream is true)
    //
    // The remaining 17 are recorded in docs/behaviour-gap/lists.md with the reason each is inert,
    // which is the other half of what W6.2 asks for.
    //
    // One thing checked and NOT changed: HideSelection's own default. It reads as though it should
    // match ListBox and TextBox, where this layer defaults it to true -- but upstream's ListView
    // carries [DefaultValue(false)] and does not set the flag in its constructor, so false is right
    // here and "fixing" it would have been the regression.
    [Collection ("Headless")]
    public class ListViewStoredOnlyTests
    {
        private static ListView Tiles (out Form form)
        {
            HeadlessRenderer.Use ();

            var view = new ListView { Width = 400, Height = 300, View = View.LargeIcon };

            foreach (var text in new[] { "one", "two", "three" })
                view.Items.Add (new ListViewItem (text));

            form = new Form { Width = 500, Height = 400 };
            form.Controls.Add (view);
            form.Show ();
            PaintSurface.Render (view).Dispose ();

            return view;
        }

        // ---------------- TileSize

        [Fact]
        public void The_tile_layout_uses_the_size_that_was_asked_for ()
        {
            using var view = Tiles (out var form);

            try {
                var before = view.Items[0].Bounds.Size;

                view.TileSize = new Size (120, 40);
                PaintSurface.Render (view).Dispose ();

                Assert.NotEqual (before, view.Items[0].Bounds.Size);
                Assert.Equal (new Size (120, 40), view.Items[0].Bounds.Size);
            } finally {
                form.Close ();
            }
        }

        [Fact]
        public void A_tile_is_not_forced_square ()
        {
            // The layout used ONE value for both dimensions, so even reading TileSize would not have
            // given an application the wide tile it asked for. Asserted separately because a square
            // fallback satisfies the test above whenever width and height happen to match.
            using var view = Tiles (out var form);

            try {
                view.TileSize = new Size (140, 48);
                PaintSurface.Render (view).Dispose ();

                var bounds = view.Items[0].Bounds;

                Assert.Equal (140, bounds.Width);
                Assert.Equal (48, bounds.Height);
            } finally {
                form.Close ();
            }
        }

        [Fact]
        public void Tiles_step_by_the_size_that_was_asked_for ()
        {
            // The size has to reach the STRIDE too, not just each item's own rectangle -- tiles sized
            // correctly but stepped at the old pitch would overlap or leave gaps.
            using var view = Tiles (out var form);

            try {
                view.TileSize = new Size (60, 30);
                PaintSurface.Render (view).Dispose ();

                var first = view.Items[0].Bounds;
                var second = view.Items[1].Bounds;

                Assert.Equal (first.Top, second.Top);
                Assert.Equal (60 + 6, second.Left - first.Left);
            } finally {
                form.Close ();
            }
        }

        [Fact]
        public void An_unset_TileSize_keeps_the_size_the_layout_always_used ()
        {
            // GUARD: the fallback matters more than the feature. Reading TileSize without one would
            // collapse every tile to nothing on every list that never set it -- which is all of them.
            using var view = Tiles (out var form);

            try {
                Assert.Equal (Size.Empty, view.TileSize);
                Assert.Equal (70, view.Items[0].Bounds.Width);
                Assert.Equal (70, view.Items[0].Bounds.Height);
            } finally {
                form.Close ();
            }
        }

        [Fact]
        public void A_non_positive_TileSize_is_rejected ()
        {
            // Upstream throws ArgumentOutOfRangeException rather than accepting a size that would make
            // every tile invisible. Stored silently here before.
            using var view = new ListView ();

            Assert.Throws<System.ArgumentOutOfRangeException> (() => view.TileSize = new Size (0, 40));
            Assert.Throws<System.ArgumentOutOfRangeException> (() => view.TileSize = new Size (40, 0));
            Assert.Throws<System.ArgumentOutOfRangeException> (() => view.TileSize = new Size (-1, -1));
        }

        [Fact]
        public void Writing_the_unset_default_back_does_not_throw ()
        {
            // The ordering upstream is careful about, and the reason the equality check comes first:
            // a designer round-tripping the unset value must not blow up on its own default.
            using var view = new ListView ();

            view.TileSize = Size.Empty;

            Assert.Equal (Size.Empty, view.TileSize);
        }

        // ---------------- HideSelection

        private static ListView Details (out Form form)
        {
            HeadlessRenderer.Use ();

            var view = new ListView { Width = 300, Height = 200, View = View.Details, FullRowSelect = true };
            view.Columns.Add (new ColumnHeader { Text = "Name", Width = 200 });

            foreach (var text in new[] { "one", "two" })
                view.Items.Add (new ListViewItem (text));

            form = new Form { Width = 400, Height = 300 };
            form.Controls.Add (view);
            form.Show ();

            return view;
        }

        // The colour at the centre-left of the first item, where the selection band is painted.
        private static SkiaSharp.SKColor BandPixel (ListView view)
        {
            using var bitmap = PaintSurface.Render (view, 1f);

            var bounds = view.Items[0].DeviceBounds;

            return bitmap.GetPixel (bounds.Left + 2, bounds.Top + bounds.Height / 2);
        }

        [Fact]
        public void HideSelection_hides_the_band_when_focus_is_elsewhere ()
        {
            using var view = Details (out var form);

            try {
                view.Items[0].Selected = true;
                view.HideSelection = false;

                var shown = BandPixel (view);

                view.HideSelection = true;

                var hidden = BandPixel (view);

                // PREMISE: the control really is unfocused, so HideSelection is the only thing that
                // changed between the two renders. Without this the test would pass on a build where
                // the band was never painted at all.
                Assert.False (view.Focused);
                Assert.NotEqual (shown, hidden);
            } finally {
                form.Close ();
            }
        }

        [Fact]
        public void A_focused_list_keeps_its_band_with_HideSelection_set ()
        {
            // The other half, and the half that makes it HideSelection rather than "never highlight":
            // the highlight is only given up when focus has genuinely gone somewhere else.
            using var view = Details (out var form);

            try {
                view.Items[0].Selected = true;
                view.HideSelection = true;

                var unfocused = BandPixel (view);

                view.Focus ();

                // PREMISE: focus really was taken. If it was not, this test says nothing.
                Assert.True (view.Focused);

                Assert.NotEqual (unfocused, BandPixel (view));
            } finally {
                form.Close ();
            }
        }

        [Fact]
        public void The_default_keeps_the_band_on_an_unfocused_list ()
        {
            // GUARD, and the reason the condition reads as a double negative: upstream defaults
            // HideSelection to FALSE, so an untouched list keeps its highlight when focus moves away.
            // An implementation that hid the band whenever the control lacked focus would satisfy the
            // first test and break every list that never set the property.
            using var view = Details (out var form);

            try {
                Assert.False (view.HideSelection);

                var unselected = BandPixel (view);

                view.Items[0].Selected = true;

                Assert.False (view.Focused);
                Assert.NotEqual (unselected, BandPixel (view));
            } finally {
                form.Close ();
            }
        }

        // ---------------- UseCompatibleStateImageBehavior

        [Fact]
        public void UseCompatibleStateImageBehavior_defaults_to_true ()
        {
            // Not wired to anything -- it selects a .NET 1.1-era state-image behaviour this layer does
            // not implement either way -- but the DEFAULT was observably wrong, which an application
            // reading it back can see without any state images existing. Upstream sets the flag in its
            // constructor and carries [DefaultValue(true)].
            using var view = new ListView ();

            Assert.True (view.UseCompatibleStateImageBehavior);
        }
    }
}
