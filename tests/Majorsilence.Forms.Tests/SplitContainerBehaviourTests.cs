using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using Xunit;

namespace Majorsilence.Forms.Tests
{
    // W5.22 -- the behaviour behind SplitContainer's and Splitter's WinForms-named members, which were
    // very largely decoration: Panel1MinSize/Panel2MinSize/FixedPanel were stored and never read
    // (LAY-01, LAY-02), SplitterMoving/SplitterMoved were declared and never raised (LAY-03),
    // Splitter.SplitPosition aliased the bar's own thickness (LAY-04) and the legacy Splitter never
    // touched the sibling it exists to resize (LAY-05). LAY-07/LAY-08 are the structural pair: the
    // panels' type and the Dock the constructor forced.
    //
    // The assertions here are deliberately relational -- "the panel grew by the same amount the bar
    // moved", "the space left over equals MinExtra" -- because the absolute numbers depend on the
    // container's border inset and on the DPI scale, and a test that pins them measures the wrong
    // thing.
    public class SplitContainerBehaviourTests
    {
        // The drag entry points are protected, as they are in WinForms. A unit test wants them
        // directly rather than through Control.Raise*, which routes by hit-testing the control tree.
        private sealed class TestSplitter : Splitter
        {
            public void Press (int x, int y) => OnMouseDown (new MouseEventArgs (MouseButtons.Left, 1, x, y, 0));

            public void Drift (int x, int y) => OnMouseMove (new MouseEventArgs (MouseButtons.Left, 0, x, y, 0));

            public void Release (int x, int y) => OnMouseUp (new MouseEventArgs (MouseButtons.Left, 1, x, y, 0));
        }

        // The classic migrated-form idiom: a docked panel, a Splitter against it, and a Fill panel.
        // Children are added Fill-first because the dock walk runs in reverse z-order, so the control
        // added LAST claims the outer edge -- the same order SplitContainer itself uses.
        private static (Panel Parent, Panel Target, TestSplitter Bar, Panel Fill) LegacyIdiom (
            DockStyle dock = DockStyle.Left, int targetExtent = 100)
        {
            var parent = new Panel { Size = new Size (300, 200) };
            var fill = parent.Controls.Add (new Panel { Dock = DockStyle.Fill });
            var bar = parent.Controls.Add (new TestSplitter { Dock = dock, SplitterWidth = 5 });
            var target = parent.Controls.Add (new Panel { Dock = dock });

            if (dock is DockStyle.Left or DockStyle.Right)
                target.Width = targetExtent;
            else
                target.Height = targetExtent;

            parent.PerformLayout ();

            return (parent, target, bar, fill);
        }

        private static Splitter Bar (SplitContainer split) => split.Controls.OfType<Splitter> ().Single ();

        private static void DragBar (Splitter bar, int fromX, int fromY, int toX, int toY)
        {
            bar.RaiseMouseDown (new MouseEventArgs (MouseButtons.Left, 1, fromX, fromY, 0));
            bar.RaiseMouseMove (new MouseEventArgs (MouseButtons.Left, 0, toX, toY, 0));
            bar.RaiseMouseUp (new MouseEventArgs (MouseButtons.Left, 1, toX, toY, 0));
        }

        // ---- LAY-01: Panel1MinSize / Panel2MinSize are the clamp -------------------------------

        [Fact]
        public void Panel1MinSize_clamps_a_smaller_SplitterDistance ()
        {
            // The finding's own test. Panel1MinSize was a plain auto-property nothing read, so the
            // enforced minimum was always the hard-coded 25 and a designer's Panel1MinSize = 150 was
            // accepted and ignored.
            using var split = new SplitContainer { Size = new Size (400, 200) };

            split.Panel1MinSize = 150;
            split.SplitterDistance = 10;

            Assert.Equal (split.Panel1MinSize, split.SplitterDistance);
        }

        [Fact]
        public void Panel1MinSize_pushes_the_splitter_out_when_raised_past_the_current_split ()
        {
            using var split = new SplitContainer { Size = new Size (400, 200) };
            split.SplitterDistance = 60;

            split.Panel1MinSize = 150;

            // Raising the minimum past where the split already is has to move the split, not just
            // record a number for next time.
            Assert.Equal (150, split.SplitterDistance);
        }

        [Fact]
        public void Panel2MinSize_caps_how_far_the_splitter_can_travel ()
        {
            using var split = new SplitContainer { Size = new Size (400, 200) };
            split.Panel2MinSize = 120;

            split.SplitterDistance = 10_000;

            // Whatever the container's own inset is, Panel2 is left with exactly its minimum.
            Assert.Equal (split.Panel2MinSize, split.Panel2.Width);
        }

        [Fact]
        public void Panel1MinimumSize_is_the_same_store_as_Panel1MinSize ()
        {
            // The Majorsilence-only spelling used to be the one that worked, with the WinForms name
            // beside it collecting values nothing consumed. They are now one value from both ends.
            using var split = new SplitContainer { Size = new Size (400, 200) };

            split.Panel1MinSize = 90;
            Assert.Equal (90, split.Panel1MinimumSize);

            split.Panel2MinimumSize = 70;
            Assert.Equal (70, split.Panel2MinSize);
        }

        [Fact]
        public void The_minimums_coerce_a_negative_to_zero ()
        {
            using var split = new SplitContainer { Size = new Size (400, 200) };

            split.Panel1MinSize = -5;
            split.Panel2MinSize = -5;

            Assert.Equal (0, split.Panel1MinSize);
            Assert.Equal (0, split.Panel2MinSize);
        }

        // ---- LAY-02: FixedPanel drives the resize ----------------------------------------------

        [Fact]
        public void FixedPanel_defaults_to_None ()
        {
            // GUARD, not proof: the property was always initialised to None, so no previous version
            // of this file could fail it. It is here because FixedPanel.None is now the mode that
            // does the most work (it keeps the split proportional), and a future change that made
            // Panel1 the default would silently restore the old, wrong behaviour.
            using var split = new SplitContainer ();

            Assert.Equal (FixedPanel.None, split.FixedPanel);
        }

        [Fact]
        public void FixedPanel_None_keeps_the_split_proportional ()
        {
            using var split = new SplitContainer { Size = new Size (200, 100) };
            split.SplitterDistance = 80;

            var before = split.SplitterDistance;

            split.Size = new Size (400, 100);

            // Panel1 was docked with a fixed extent and nothing read FixedPanel, so the container
            // behaved as though Panel1 were permanently pinned: every extra pixel went to Panel2.
            // The default keeps the proportion instead, so doubling the width doubles the split.
            Assert.Equal (before * 2, split.SplitterDistance);
        }

        [Fact]
        public void FixedPanel_None_keeps_the_split_proportional_when_shrinking_too ()
        {
            using var split = new SplitContainer { Size = new Size (400, 100) };
            split.SplitterDistance = 200;

            var before = split.SplitterDistance;

            split.Size = new Size (200, 100);

            Assert.Equal (before / 2, split.SplitterDistance);
        }

        [Fact]
        public void FixedPanel_Panel1_pins_Panel1_and_gives_the_delta_to_Panel2 ()
        {
            // GUARD, not proof: pinning Panel1 is exactly what the container did before FixedPanel
            // was read at all (Panel1 is docked with a fixed extent), so no previous version could
            // fail it. It is the one mode that used to work by accident, and this pins that the new
            // ratio arithmetic did not break it on the way past.
            using var split = new SplitContainer { Size = new Size (200, 100), FixedPanel = FixedPanel.Panel1 };
            split.SplitterDistance = 80;

            var distance = split.SplitterDistance;
            var panel2 = split.Panel2.Width;

            split.Size = new Size (400, 100);

            Assert.Equal (distance, split.SplitterDistance);
            Assert.Equal (panel2 + 200, split.Panel2.Width);
        }

        [Fact]
        public void FixedPanel_Panel2_pins_Panel2_and_gives_the_delta_to_Panel1 ()
        {
            using var split = new SplitContainer { Size = new Size (200, 100), FixedPanel = FixedPanel.Panel2 };
            split.SplitterDistance = 80;

            var distance = split.SplitterDistance;
            var panel2 = split.Panel2.Width;

            split.Size = new Size (400, 100);

            // Apps that asked for Panel2 used to get the exact opposite of what they wanted.
            Assert.Equal (panel2, split.Panel2.Width);
            Assert.Equal (distance + 200, split.SplitterDistance);
        }

        [Fact]
        public void FixedPanel_survives_an_orientation_change_without_redistributing ()
        {
            // The extent FixedPanel remembers is measured along the split axis, so flipping the axis
            // must forget it rather than treat a 400-wide-to-200-tall change as a shrink.
            using var split = new SplitContainer { Size = new Size (400, 200) };
            split.SplitterDistance = 100;

            split.Orientation = Orientation.Horizontal;

            // The Orientation setter transposes Panel1, which is what carries the split across.
            Assert.Equal (100, split.SplitterDistance);
        }

        // ---- LAY-03: SplitterMoving / SplitterMoved --------------------------------------------

        [Fact]
        public void A_drag_raises_SplitterMoving_while_moving_and_SplitterMoved_once_at_the_end ()
        {
            using var split = new SplitContainer { Size = new Size (300, 200) };
            split.SplitterDistance = 100;

            var moving = new List<SplitterCancelEventArgs> ();
            var moved = new List<SplitterEventArgs> ();

            split.SplitterMoving += (_, e) => moving.Add (e);
            split.SplitterMoved += (_, e) => moved.Add (e);

            var before = split.SplitterDistance;

            DragBar (Bar (split), 0, 0, 30, 0);

            Assert.NotEmpty (moving);
            Assert.Single (moved);

            // The split followed the pointer by the distance the pointer travelled.
            Assert.Equal (before + 30, split.SplitterDistance);

            // And the args describe where the bar ended up, not where it started.
            Assert.Equal (split.SplitterRectangle.X, moved[0].SplitX);
        }

        [Fact]
        public void SplitterMoving_Cancel_leaves_the_split_where_it_was ()
        {
            using var split = new SplitContainer { Size = new Size (300, 200) };
            split.SplitterDistance = 100;

            var moved = 0;

            split.SplitterMoving += (_, e) => e.Cancel = true;
            split.SplitterMoved += (_, _) => moved++;

            DragBar (Bar (split), 0, 0, 30, 0);

            Assert.Equal (100, split.SplitterDistance);
            Assert.Equal (0, moved);
        }

        [Fact]
        public void SplitterMoving_can_steer_the_split_somewhere_else ()
        {
            // WinForms lets a handler rewrite SplitX/SplitY -- how an application snaps the bar to a
            // grid or to its content.
            using var split = new SplitContainer { Size = new Size (300, 200) };
            split.SplitterDistance = 100;

            split.SplitterMoving += (_, e) => e.SplitX = 175;

            DragBar (Bar (split), 0, 0, 30, 0);

            Assert.Equal (175, split.SplitterDistance);
        }

        [Fact]
        public void SplitterMoved_is_not_raised_by_a_press_and_release_that_moved_nothing ()
        {
            using var split = new SplitContainer { Size = new Size (300, 200) };
            split.SplitterDistance = 100;

            var moved = 0;
            split.SplitterMoved += (_, _) => moved++;

            var bar = Bar (split);
            bar.RaiseMouseDown (new MouseEventArgs (MouseButtons.Left, 1, 0, 0, 0));
            bar.RaiseMouseUp (new MouseEventArgs (MouseButtons.Left, 1, 0, 0, 0));

            Assert.Equal (0, moved);

            // The other half, so this cannot pass by the event simply never being raised: the same
            // gesture with a move in the middle does raise it.
            DragBar (bar, 0, 0, 20, 0);

            Assert.Equal (1, moved);
        }

        [Fact]
        public void Splitter_stores_its_SplitterMoved_handler ()
        {
            // Both of Splitter's splitter events were declared `add { } remove { }`, which discards
            // the handler outright: the shape looks wired at compile time and leaks nothing at
            // runtime, so not even reflection could observe the omission.
            var (parent, _, bar, _) = LegacyIdiom ();

            using (parent) {
                var moved = 0;
                bar.SplitterMoved += (_, _) => moved++;

                bar.SplitPosition = 140;

                Assert.Equal (1, moved);
            }
        }

        [Fact]
        public void Splitter_raises_SplitterMoving_then_SplitterMoved_across_a_drag ()
        {
            var (parent, _, bar, _) = LegacyIdiom ();

            using (parent) {
                var moving = 0;
                var moved = 0;
                bar.SplitterMoving += (_, _) => moving++;
                bar.SplitterMoved += (_, _) => moved++;

                bar.Press (0, 0);
                bar.Drift (10, 0);
                bar.Drift (20, 0);
                bar.Release (20, 0);

                Assert.Equal (2, moving);
                Assert.Equal (1, moved);
            }
        }

        [Fact]
        public void Cancelling_Splitter_SplitterMoving_ends_the_drag ()
        {
            var (parent, target, bar, _) = LegacyIdiom ();

            using (parent) {
                var moving = 0;
                var width = target.Width;

                bar.SplitterMoving += (_, e) => {
                    moving++;
                    e.Cancel = true;
                };

                bar.Press (0, 0);
                bar.Drift (20, 0);
                // The drag is over, so a second move must not be seen at all -- upstream's
                // SplitEnd (false).
                bar.Drift (40, 0);
                bar.Release (40, 0);

                Assert.Equal (1, moving);
                Assert.Equal (width, target.Width);
            }
        }

        // ---- LAY-04: SplitPosition is the sibling's size, not the bar's thickness ---------------

        [Fact]
        public void SplitPosition_sizes_the_docked_sibling_and_leaves_the_bar_alone ()
        {
            // It used to be `get => SplitterWidth; set => SplitterWidth = value;`, so
            // splitter1.SplitPosition = 140 produced a 140-pixel-thick bar.
            var (parent, target, bar, _) = LegacyIdiom ();

            using (parent) {
                var thickness = bar.Width;

                bar.SplitPosition = 140;

                Assert.Equal (140, target.Width);
                Assert.Equal (thickness, bar.Width);
                Assert.Equal (140, bar.SplitPosition);
            }
        }

        [Fact]
        public void SplitPosition_reads_back_the_sibling_it_is_docked_against ()
        {
            var (parent, target, bar, _) = LegacyIdiom (targetExtent: 111);

            using (parent)
                Assert.Equal (target.Width, bar.SplitPosition);
        }

        [Fact]
        public void SplitPosition_is_minus_one_with_no_sibling_to_measure ()
        {
            // WinForms' CalcSplitSize reports -1 when there is no target. A parentless Splitter used
            // to report its own width here, which is what made restoring a saved layout land
            // somewhere arbitrary.
            using var bar = new Splitter { SplitterWidth = 5 };

            Assert.Equal (-1, bar.SplitPosition);
        }

        [Fact]
        public void SplitPosition_is_clamped_by_MinSize ()
        {
            var (parent, target, bar, _) = LegacyIdiom ();

            using (parent) {
                bar.MinSize = 70;

                bar.SplitPosition = 5;

                Assert.Equal (bar.MinSize, target.Width);
            }
        }

        [Fact]
        public void SplitPosition_is_clamped_by_MinExtra ()
        {
            var (parent, _, bar, fill) = LegacyIdiom ();

            using (parent) {
                bar.MinExtra = 60;

                bar.SplitPosition = 10_000;

                // MinExtra is the room that must be left for whatever fills the remainder, which is
                // exactly what the Fill panel ends up with.
                Assert.Equal (bar.MinExtra, fill.Width);
            }
        }

        // ---- LAY-05: the drag resizes the docked sibling ---------------------------------------

        [Fact]
        public void Dragging_a_left_docked_splitter_grows_the_sibling_by_the_same_amount ()
        {
            // Nothing in Splitter used to touch any sibling: only SplitContainer subscribed to its
            // bespoke Drag event, so a migrated form showed a bar with the right cursor that moved
            // nothing at all. Silent: no exception, correct-looking cursor.
            var (parent, target, bar, _) = LegacyIdiom ();

            using (parent) {
                var width = target.Width;
                var left = bar.Left;

                bar.Press (0, 0);
                bar.Drift (40, 0);
                bar.Release (40, 0);

                Assert.Equal (width + 40, target.Width);

                // And the bar came along with the edge it is docked against.
                Assert.Equal (left + 40, bar.Left);
            }
        }

        [Fact]
        public void Dragging_a_right_docked_splitter_grows_the_sibling_the_other_way ()
        {
            var (parent, target, bar, _) = LegacyIdiom (DockStyle.Right);

            using (parent) {
                var width = target.Width;
                var right = bar.Left;

                // Pointer moves LEFT, and a right-docked target grows into the space it vacates.
                bar.Press (0, 0);
                bar.Drift (-40, 0);
                bar.Release (-40, 0);

                Assert.Equal (width + 40, target.Width);
                Assert.Equal (right - 40, bar.Left);
            }
        }

        [Fact]
        public void Dragging_a_top_docked_splitter_grows_the_sibling_downwards ()
        {
            var (parent, target, bar, _) = LegacyIdiom (DockStyle.Top);

            using (parent) {
                var height = target.Height;

                bar.Press (0, 0);
                bar.Drift (0, 30);
                bar.Release (0, 30);

                Assert.Equal (height + 30, target.Height);
            }
        }

        [Fact]
        public void A_drag_cannot_push_the_sibling_below_MinSize ()
        {
            var (parent, target, bar, _) = LegacyIdiom ();

            using (parent) {
                bar.MinSize = 60;

                bar.Press (0, 0);
                bar.Drift (-200, 0);
                bar.Release (-200, 0);

                Assert.Equal (bar.MinSize, target.Width);
            }
        }

        [Fact]
        public void A_drag_cannot_eat_into_MinExtra ()
        {
            var (parent, _, bar, fill) = LegacyIdiom ();

            using (parent) {
                bar.MinExtra = 55;

                bar.Press (0, 0);
                bar.Drift (500, 0);
                bar.Release (500, 0);

                Assert.Equal (bar.MinExtra, fill.Width);
            }
        }

        [Fact]
        public void A_splitter_with_nothing_docked_against_it_still_raises_Drag ()
        {
            // GUARD, not proof: the Drag event always fired, because the old OnMouseMove had no early
            // return in it at all, so no previous version could fail this. It guards the regression
            // the new code makes possible: DragTarget returning false for a missing target, or the
            // veto path swallowing the drag, would cut SplitContainer's only channel to its bar.
            using var bar = new TestSplitter ();

            var drags = 0;
            bar.Drag += (_, _) => drags++;

            bar.Press (0, 0);
            bar.Drift (10, 0);
            bar.Release (10, 0);

            Assert.Equal (1, drags);
        }

        // ---- LAY-07 / LAY-08: the structural pair ----------------------------------------------

        [Fact]
        public void The_panels_are_SplitterPanels_that_know_their_container ()
        {
            // Designer-generated and migrated code declares these by type
            // (`SplitterPanel p = sc.Panel1;`, `foreach (SplitterPanel p in ...)`), which a plain
            // Panel made fail to compile or throw InvalidCastException.
            using var split = new SplitContainer ();

            Assert.IsType<SplitterPanel> (split.Panel1);
            Assert.IsType<SplitterPanel> (split.Panel2);
            Assert.Same (split, split.Panel1.Owner);
            Assert.Same (split, split.Panel2.Owner);
        }

        [Fact]
        public void The_constructor_does_not_force_Dock_Fill ()
        {
            // Dock = DockStyle.Fill was the first line of the constructor, so a SplitContainer the
            // designer had given Anchor plus a Location and Size -- its default -- filled the whole
            // form instead.
            using var split = new SplitContainer ();

            Assert.Equal (DockStyle.None, split.Dock);
            Assert.Equal (new Size (150, 100), split.Size);
        }

        [Fact]
        public void SplitterDistance_defaults_to_the_WinForms_50 ()
        {
            // "Restore the saved distance, else use the default" code landed somewhere else than on
            // Windows while this was whatever Panel's own default width happened to be.
            using var split = new SplitContainer ();

            Assert.Equal (50, split.SplitterDistance);
        }
    }
}
