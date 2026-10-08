using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using Majorsilence.Forms.Headless;
using Xunit;

namespace Majorsilence.Forms.Tests
{
    // LAY-06: the splitter moves from the keyboard. Upstream's SplitContainer (Layout/Containers/
    // SplitContainer.cs) treats "the container has focus" as "the splitter is focused": a press on the
    // bar focuses it, the unmodified arrow keys stop being dialog keys (ProcessDialogKey), each key-down
    // moves the bar by SplitterIncrement without crossing a minimum, the repeats of a held key raise
    // SplitterMoving, Escape abandons the move, the key-up raises SplitterMoved, and the bar carries a
    // focus cue. Ours had no focus state for the bar and no key handling, and the bar itself -- a
    // focusable Splitter here, never focusable upstream -- was a dead tab stop.
    //
    // Keys go through Form.HandleKeyDown/HandleKeyUp, the backend entry, so the pre-processing chain
    // (ProcessDialogKey) is part of what is tested.
    [Collection ("Headless")]
    public class SplitContainerKeyboardTests
    {
        private static (Form Form, SplitContainer Split, TextBox Left, TextBox Right) Build ()
        {
            HeadlessRenderer.Use ();
            var form = new Form { Size = new Size (420, 300) };
            var split = new SplitContainer { Dock = DockStyle.Fill, SplitterIncrement = 10 };
            var left = new TextBox ();
            var right = new TextBox ();
            split.Panel1.Controls.Add (left);
            split.Panel2.Controls.Add (right);
            form.Controls.Add (split);
            form.Show ();

            // After Show: FixedPanel.None keeps the split proportional while the container grows from
            // its default size to the form's.
            split.SplitterDistance = 120;

            return (form, split, left, right);
        }

        private static Splitter Bar (SplitContainer split) => split.Controls.OfType<Splitter> ().Single ();

        private static void Press (Form form, Keys key, int repeats = 1)
        {
            for (var i = 0; i < repeats; i++)
                form.HandleKeyDown (key);

            form.HandleKeyUp (key);
        }

        private static void ClickBar (SplitContainer split)
        {
            var bar = Bar (split);
            bar.RaiseMouseDown (new MouseEventArgs (MouseButtons.Left, 1, 2, 10, 0));
            bar.RaiseMouseUp (new MouseEventArgs (MouseButtons.Left, 1, 2, 10, 0));
        }

        [Fact]
        public void A_press_on_the_bar_focuses_the_container ()
        {
            var (form, split, left, _) = Build ();
            using var _f = form;
            left.Focus ();

            ClickBar (split);

            Assert.True (split.Focused);
            Assert.False (left.Focused);
        }

        [Fact]
        public void The_bar_is_not_a_focus_target_of_its_own ()
        {
            // Upstream's Splitter turns Selectable off and TabStop off (Controls/Splitter/Splitter.cs).
            var (form, split, _, _) = Build ();
            using var _f = form;

            Assert.False (Bar (split).CanSelect);
            Assert.False (Bar (split).TabStop);
        }

        [Fact]
        public void An_arrow_key_moves_the_focused_splitter_by_the_increment_and_raises_SplitterMoved_once ()
        {
            var (form, split, _, _) = Build ();
            using var _f = form;
            ClickBar (split);
            var before = split.SplitterDistance;
            var moved = 0;
            split.SplitterMoved += (_, _) => moved++;

            Press (form, Keys.Right);

            Assert.Equal (before + split.SplitterIncrement, split.SplitterDistance);
            Assert.Equal (1, moved);
            // The arrow did not move focus to the next control. A guard: the arrows are let through by the
            // ProcessDialogKey override, as upstream's are, but nothing on the dialog-key path here moves
            // focus with an arrow today, so removing the override leaves this green.
            Assert.True (split.Focused);

            Press (form, Keys.Left);
            Press (form, Keys.Left);

            Assert.Equal (before - split.SplitterIncrement, split.SplitterDistance);
            Assert.Equal (3, moved);
        }

        [Fact]
        public void A_held_key_raises_SplitterMoving_for_each_repeat_and_SplitterMoved_on_release ()
        {
            var (form, split, _, _) = Build ();
            using var _f = form;
            ClickBar (split);
            var before = split.SplitterDistance;
            var events = new List<string> ();
            split.SplitterMoving += (_, e) => events.Add ($"moving {e.SplitX}");
            split.SplitterMoved += (_, _) => events.Add ("moved");

            Press (form, Keys.Right, repeats: 3);

            Assert.Equal (before + 3 * split.SplitterIncrement, split.SplitterDistance);
            Assert.Equal (new[] {
                $"moving {before + 2 * split.SplitterIncrement}",
                $"moving {before + 3 * split.SplitterIncrement}",
                "moved",
            }, events);
        }

        [Fact]
        public void Escape_abandons_a_keyboard_move_and_cancelling_SplitterMoving_does_too ()
        {
            var (form, split, _, _) = Build ();
            using var _f = form;
            ClickBar (split);
            var before = split.SplitterDistance;
            var moved = 0;
            split.SplitterMoved += (_, _) => moved++;

            form.HandleKeyDown (Keys.Right);
            form.HandleKeyDown (Keys.Right);
            Assert.NotEqual (before, split.SplitterDistance);
            form.HandleKeyDown (Keys.Escape);
            form.HandleKeyUp (Keys.Right);

            Assert.Equal (before, split.SplitterDistance);
            Assert.Equal (0, moved);

            split.SplitterMoving += (_, e) => e.Cancel = true;
            Press (form, Keys.Down, repeats: 2);

            Assert.Equal (before, split.SplitterDistance);
            Assert.Equal (0, moved);
        }

        [Fact]
        public void A_step_that_would_cross_a_minimum_is_not_taken ()
        {
            var (form, split, _, _) = Build ();
            using var _f = form;
            split.Panel1MinSize = 115;
            ClickBar (split);

            // 120 - 10 = 110 is under the minimum; upstream refuses the step rather than clamping to 115.
            Press (form, Keys.Left);

            Assert.Equal (120, split.SplitterDistance);
        }

        [Fact]
        public void A_fixed_splitter_ignores_the_arrows_and_a_press_does_not_focus_it ()
        {
            var (form, split, left, _) = Build ();
            using var _f = form;
            split.IsSplitterFixed = true;
            left.Focus ();

            ClickBar (split);
            Assert.False (split.Focused);

            split.Focus ();
            var before = split.SplitterDistance;
            Press (form, Keys.Right);

            Assert.Equal (before, split.SplitterDistance);
        }

        [Fact]
        public void Arrows_typed_in_a_panel_child_do_not_move_the_splitter ()
        {
            var (form, split, left, _) = Build ();
            using var _f = form;
            left.Focus ();
            var before = split.SplitterDistance;

            Press (form, Keys.Right);

            Assert.Equal (before, split.SplitterDistance);
        }

        [Fact]
        public void The_bar_shows_a_focus_cue_while_the_container_has_focus ()
        {
            var (form, split, left, _) = Build ();
            using var _f = form;
            var bar = Bar (split);

            left.Focus ();
            var unfocused = DarkPixels (bar);

            split.Focus ();
            var focused = DarkPixels (bar);

            // The bar is a flat fill; the cue is the dashed black focus rectangle drawn over it.
            Assert.Equal (0, unfocused);
            Assert.True (focused > 0, "no focus cue on the bar");
        }

        private static int DarkPixels (Control control)
        {
            using var bitmap = PaintSurface.Render (control);
            var count = 0;

            for (var y = 0; y < bitmap.Height; y++)
                for (var x = 0; x < bitmap.Width; x++) {
                    var c = bitmap.GetPixel (x, y);
                    if (c.Alpha > 0 && c.Red < 60 && c.Green < 60 && c.Blue < 60)
                        count++;
                }

            return count;
        }
    }
}
