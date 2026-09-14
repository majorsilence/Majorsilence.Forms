using System;
using System.Collections.Generic;
using System.Drawing;
using Majorsilence.Forms.VisualStyles;
using SkiaSharp;
using Xunit;
using MFGraphics = Majorsilence.Forms.Drawing.Graphics;

namespace Majorsilence.Forms.Tests
{
    // W5.19 (findings GFX-01, GFX-03, GFX-38).
    //
    // Twenty ControlPaint.Draw* overloads had literally empty bodies. This is *the* API an owner-drawn
    // migrated control uses to paint its chrome, so a custom cell painter, an owner-drawn ListBox or a
    // UserControl calling ControlPaint.DrawBorder rendered nothing at all -- the control looked empty
    // rather than mis-styled. DrawFocusRectangle was hardcoded black, so the keyboard-focus indicator
    // vanished on a dark theme. And Application.RenderWithVisualStyles said true while
    // VisualStyleRenderer.DrawBackground was empty, so the standard themed-or-classic fork took the
    // themed branch, drew nothing, and never reached the fallback.
    //
    // Every paint test here draws over a known solid fill and asks whether ink arrived, and where --
    // never for an exact colour at an exact pixel, which would measure the rasteriser.
    public class ControlPaintChromeTests
    {
        // A bitmap filled with a colour nothing under test draws, so any other pixel is ink.
        private static readonly SKColor Background = new SKColor (0, 255, 0);

        private static (SKBitmap Bitmap, MFGraphics Graphics, SKCanvas Canvas) Surface (int width = 40, int height = 40)
        {
            var bitmap = new SKBitmap (new SKImageInfo (width, height));
            var canvas = new SKCanvas (bitmap);
            canvas.Clear (Background);

            // The internal SKCanvas constructor: the tests assembly has InternalsVisibleTo, and there
            // is no public factory that wraps a canvas (FromImage owns its own).
            return (bitmap, new MFGraphics (canvas), canvas);
        }

        private static int InkCount (SKBitmap bitmap)
        {
            var count = 0;

            for (var x = 0; x < bitmap.Width; x++)
                for (var y = 0; y < bitmap.Height; y++)
                    if (bitmap.GetPixel (x, y) != Background)
                        count++;

            return count;
        }

        // Pixels of one exact colour. Used where the control FILLS its bounds, so "differs from the
        // background" is true everywhere and says nothing about the glyph.
        private static int CountOf (SKBitmap bitmap, Color colour)
        {
            var target = new SKColor (colour.R, colour.G, colour.B);
            var count = 0;

            for (var x = 0; x < bitmap.Width; x++)
                for (var y = 0; y < bitmap.Height; y++)
                    if (bitmap.GetPixel (x, y) == target)
                        count++;

            return count;
        }

        private static bool IsInk (SKBitmap bitmap, int x, int y)
            => x >= 0 && y >= 0 && x < bitmap.Width && y < bitmap.Height && bitmap.GetPixel (x, y) != Background;

        // ---------------- GFX-01: the chrome family paints at all

        [Theory]
        [InlineData ("DrawBorder")]
        [InlineData ("DrawBorder3D")]
        [InlineData ("DrawButton")]
        [InlineData ("DrawCheckBox")]
        [InlineData ("DrawMixedCheckBox")]
        [InlineData ("DrawRadioButton")]
        [InlineData ("DrawComboButton")]
        [InlineData ("DrawScrollButton")]
        [InlineData ("DrawCaptionButton")]
        [InlineData ("DrawMenuGlyph")]
        [InlineData ("DrawSizeGrip")]
        [InlineData ("DrawGrabHandle")]
        [InlineData ("DrawContainerGrabHandle")]
        [InlineData ("DrawLockedFrame")]
        [InlineData ("DrawSelectionFrame")]
        public void Every_chrome_method_puts_ink_on_the_surface (string method)
        {
            // The finding's own test: each of these had an empty body, so the surface came back exactly
            // as it went in. "Not uniform any more" is a weak assertion on its own -- the per-method
            // tests below say WHERE -- but it is the one that catches a regression to a no-op.
            var (bitmap, graphics, canvas) = Surface ();
            using var _bitmap = bitmap;
            using var _canvas = canvas;
            var bounds = new Rectangle (4, 4, 24, 24);

            switch (method) {
                case "DrawBorder": ControlPaint.DrawBorder (graphics, bounds, Color.Red, ButtonBorderStyle.Solid); break;
                case "DrawBorder3D": ControlPaint.DrawBorder3D (graphics, bounds); break;
                case "DrawButton": ControlPaint.DrawButton (graphics, bounds, ButtonState.Normal); break;
                case "DrawCheckBox": ControlPaint.DrawCheckBox (graphics, bounds, ButtonState.Checked); break;
                case "DrawMixedCheckBox": ControlPaint.DrawMixedCheckBox (graphics, bounds, ButtonState.Checked); break;
                case "DrawRadioButton": ControlPaint.DrawRadioButton (graphics, bounds, ButtonState.Checked); break;
                case "DrawComboButton": ControlPaint.DrawComboButton (graphics, bounds, ButtonState.Normal); break;
                case "DrawScrollButton": ControlPaint.DrawScrollButton (graphics, bounds, ScrollButton.Down, ButtonState.Normal); break;
                case "DrawCaptionButton": ControlPaint.DrawCaptionButton (graphics, bounds, CaptionButton.Close, ButtonState.Normal); break;
                case "DrawMenuGlyph": ControlPaint.DrawMenuGlyph (graphics, bounds, MenuGlyph.Checkmark); break;
                case "DrawSizeGrip": ControlPaint.DrawSizeGrip (graphics, SystemColors.Control, bounds); break;
                case "DrawGrabHandle": ControlPaint.DrawGrabHandle (graphics, bounds, primary: true, enabled: true); break;
                case "DrawContainerGrabHandle": ControlPaint.DrawContainerGrabHandle (graphics, bounds); break;
                case "DrawLockedFrame": ControlPaint.DrawLockedFrame (graphics, bounds, primary: true); break;
                case "DrawSelectionFrame": ControlPaint.DrawSelectionFrame (graphics, active: true, bounds, Rectangle.Inflate (bounds, -3, -3), SystemColors.Control); break;
            }

            Assert.True (InkCount (bitmap) > 0, $"{method} drew nothing");
        }

        [Fact]
        public void DrawBorder_puts_its_colour_on_the_edges_and_leaves_the_middle_alone ()
        {
            var (bitmap, graphics, canvas) = Surface ();
            using var _bitmap = bitmap;
            using var _canvas = canvas;
            var bounds = new Rectangle (4, 4, 20, 20);

            ControlPaint.DrawBorder (graphics, bounds, Color.Red, ButtonBorderStyle.Solid);

            var red = new SKColor (255, 0, 0);
            Assert.Equal (red, bitmap.GetPixel (bounds.Left, bounds.Top + 10));            // left edge
            Assert.Equal (red, bitmap.GetPixel (bounds.Right - 1, bounds.Top + 10));       // right edge
            Assert.Equal (red, bitmap.GetPixel (bounds.Left + 10, bounds.Top));            // top edge
            Assert.Equal (red, bitmap.GetPixel (bounds.Left + 10, bounds.Bottom - 1));     // bottom edge

            // A border is a border: it does not fill.
            Assert.Equal (Background, bitmap.GetPixel (bounds.Left + 10, bounds.Top + 10));
        }

        [Fact]
        public void DrawBorder_with_style_None_draws_nothing ()
        {
            // GUARD, not proof: the method drew nothing for every style before. It pins that None still
            // means none now that the others paint.
            var (bitmap, graphics, canvas) = Surface ();
            using var _bitmap = bitmap;
            using var _canvas = canvas;

            ControlPaint.DrawBorder (graphics, new Rectangle (4, 4, 20, 20), Color.Red, ButtonBorderStyle.None);

            Assert.Equal (0, InkCount (bitmap));
        }

        [Fact]
        public void A_raised_border3D_and_a_sunken_one_are_mirror_images ()
        {
            // The bevel has a DIRECTION, and that is the whole point of a 3D border: the top-left of a
            // raised border is the colour the bottom-right of a sunken one is.
            var (raised, raised_g, raised_c) = Surface ();
            using var _raised = raised;
            using var _raisedC = raised_c;
            var (sunken, sunken_g, sunken_c) = Surface ();
            using var _sunken = sunken;
            using var _sunkenC = sunken_c;
            var bounds = new Rectangle (4, 4, 20, 20);

            ControlPaint.DrawBorder3D (raised_g, bounds, Border3DStyle.Raised);
            ControlPaint.DrawBorder3D (sunken_g, bounds, Border3DStyle.Sunken);

            var raisedTopLeft = raised.GetPixel (bounds.Left, bounds.Top);
            var raisedBottomRight = raised.GetPixel (bounds.Right - 1, bounds.Bottom - 1);
            var sunkenTopLeft = sunken.GetPixel (bounds.Left, bounds.Top);

            Assert.NotEqual (raisedTopLeft, raisedBottomRight);
            Assert.NotEqual (raisedTopLeft, sunkenTopLeft);
        }

        [Fact]
        public void DrawBorder3D_honours_the_sides_it_is_given ()
        {
            var (bitmap, graphics, canvas) = Surface ();
            using var _bitmap = bitmap;
            using var _canvas = canvas;
            var bounds = new Rectangle (4, 4, 20, 20);

            ControlPaint.DrawBorder3D (graphics, bounds, Border3DStyle.Raised, Border3DSide.Top);

            Assert.True (IsInk (bitmap, bounds.Left + 10, bounds.Top), "the top edge should be drawn");
            Assert.False (IsInk (bitmap, bounds.Left + 10, bounds.Bottom - 1), "the bottom edge was not asked for");
        }

        [Fact]
        public void A_pushed_button_does_not_look_like_a_normal_one ()
        {
            // ButtonState is not decoration: a pressed button has to read as pressed, which is the bevel
            // swapping over. Asserted as a difference rather than as two colours, so it does not encode
            // the palette.
            var (normal, normal_g, normal_c) = Surface ();
            using var _normal = normal;
            using var _normalC = normal_c;
            var (pushed, pushed_g, pushed_c) = Surface ();
            using var _pushed = pushed;
            using var _pushedC = pushed_c;
            var bounds = new Rectangle (4, 4, 20, 20);

            ControlPaint.DrawButton (normal_g, bounds, ButtonState.Normal);
            ControlPaint.DrawButton (pushed_g, bounds, ButtonState.Pushed);

            Assert.NotEqual (normal.GetPixel (bounds.Left, bounds.Top), pushed.GetPixel (bounds.Left, bounds.Top));
        }

        [Fact]
        public void An_unchecked_check_box_has_no_tick ()
        {
            // The tick is the state, so a checked box must carry ink the unchecked one does not.
            var (unchecked_, unchecked_g, unchecked_c) = Surface ();
            using var _unchecked = unchecked_;
            using var _uncheckedC = unchecked_c;
            var (checked_, checked_g, checked_c) = Surface ();
            using var _checked = checked_;
            using var _checkedC = checked_c;
            var bounds = new Rectangle (4, 4, 20, 20);

            ControlPaint.DrawCheckBox (unchecked_g, bounds, ButtonState.Normal);
            ControlPaint.DrawCheckBox (checked_g, bounds, ButtonState.Checked);

            // Counted in the GLYPH's colour, not as "not the background": a check box FILLS its well, so
            // every pixel in it is already non-background and the two states counted identically.
            Assert.Equal (0, CountOf (unchecked_, SystemColors.ControlText));
            Assert.True (CountOf (checked_, SystemColors.ControlText) > 0, "a checked box should carry a tick");
        }

        [Fact]
        public void A_checked_radio_button_carries_its_dot ()
        {
            var (empty, empty_g, empty_c) = Surface ();
            using var _empty = empty;
            using var _emptyC = empty_c;
            var (full, full_g, full_c) = Surface ();
            using var _full = full;
            using var _fullC = full_c;
            var bounds = new Rectangle (4, 4, 20, 20);

            ControlPaint.DrawRadioButton (empty_g, bounds, ButtonState.Normal);
            ControlPaint.DrawRadioButton (full_g, bounds, ButtonState.Checked);

            // In the glyph's colour: a radio button fills its circle, so the middle is non-background in
            // both states and "is there ink here" cannot tell them apart.
            Assert.Equal (0, CountOf (empty, SystemColors.ControlText));
            Assert.True (CountOf (full, SystemColors.ControlText) > 0, "a checked radio button should carry its dot");
        }

        [Fact]
        public void Each_menu_glyph_draws_a_different_shape ()
        {
            // Rendered together and compared pairwise, because "there is ink in the foreground colour"
            // cannot tell one glyph from another: a Checkmark that fell through to the arrow branch
            // would satisfy it. The three are genuinely different marks, and the test says so.
            var shapes = new Dictionary<MenuGlyph, string> ();

            foreach (var glyph in new[] { MenuGlyph.Checkmark, MenuGlyph.Bullet, MenuGlyph.Arrow }) {
                var (bitmap, graphics, canvas) = Surface ();
                using var _bitmap = bitmap;
                using var _canvas = canvas;
                var bounds = new Rectangle (4, 4, 20, 20);

                ControlPaint.DrawMenuGlyph (graphics, bounds, glyph, Color.Red, Color.White);

                // The glyph colour, not "not the background": DrawMenuGlyph fills its background first.
                var red = new SKColor (255, 0, 0);
                var shape = new System.Text.StringBuilder ();

                for (var y = bounds.Top; y < bounds.Bottom; y++)
                    for (var x = bounds.Left; x < bounds.Right; x++)
                        shape.Append (bitmap.GetPixel (x, y) == red ? '#' : '.');

                Assert.Contains ('#', shape.ToString ());
                shapes[glyph] = shape.ToString ();
            }

            Assert.NotEqual (shapes[MenuGlyph.Checkmark], shapes[MenuGlyph.Bullet]);
            Assert.NotEqual (shapes[MenuGlyph.Checkmark], shapes[MenuGlyph.Arrow]);
            Assert.NotEqual (shapes[MenuGlyph.Bullet], shapes[MenuGlyph.Arrow]);
        }

        [Fact]
        public void A_disabled_grab_handle_differs_from_an_enabled_one ()
        {
            var (on, on_g, on_c) = Surface ();
            using var _on = on;
            using var _onC = on_c;
            var (off, off_g, off_c) = Surface ();
            using var _off = off;
            using var _offC = off_c;
            var bounds = new Rectangle (4, 4, 8, 8);

            ControlPaint.DrawGrabHandle (on_g, bounds, primary: true, enabled: true);
            ControlPaint.DrawGrabHandle (off_g, bounds, primary: true, enabled: false);

            Assert.NotEqual (on.GetPixel (bounds.Left + 4, bounds.Top + 4), off.GetPixel (bounds.Left + 4, bounds.Top + 4));
        }

        [Fact]
        public void The_size_grip_is_in_the_bottom_right_corner ()
        {
            var (bitmap, graphics, canvas) = Surface ();
            using var _bitmap = bitmap;
            using var _canvas = canvas;
            var bounds = new Rectangle (0, 0, 40, 40);

            ControlPaint.DrawSizeGrip (graphics, SystemColors.Control, bounds);

            // A grip that drew in the top-left would satisfy "not uniform" and be wrong.
            var bottomRight = 0;
            var topLeft = 0;

            for (var x = 0; x < 40; x++)
                for (var y = 0; y < 40; y++)
                    if (IsInk (bitmap, x, y)) {
                        if (x > 20 && y > 20) bottomRight++;
                        if (x < 20 && y < 20) topLeft++;
                    }

            Assert.True (bottomRight > 0, "the grip should be in the bottom-right corner");
            Assert.Equal (0, topLeft);
        }

        [Fact]
        public void A_small_size_grip_stays_inside_its_rectangle ()
        {
            // A grip smaller than the 40x40 case. There is no bounds clip in the implementation -- the
            // loop condition keeps every tick inside -- so this asserts that invariant rather than a
            // guard, and is the test that would catch the loop condition being loosened.
            var (bitmap, graphics, canvas) = Surface ();
            using var _bitmap = bitmap;
            using var _canvas = canvas;
            var bounds = new Rectangle (30, 30, 6, 6);

            ControlPaint.DrawSizeGrip (graphics, SystemColors.Control, bounds);

            for (var x = 0; x < bitmap.Width; x++)
                for (var y = 0; y < bitmap.Height; y++)
                    if (IsInk (bitmap, x, y))
                        Assert.True (bounds.Contains (x, y), $"the grip drew at ({x},{y}), outside {bounds}");
        }

        // ---------------- GFX-03: the focus rectangle

        [Fact]
        public void The_focus_rectangle_uses_the_colours_it_is_given ()
        {
            // It hardcoded black, so the focus indicator disappeared on a dark theme -- an accessibility
            // regression rather than a cosmetic one.
            var (bitmap, graphics, canvas) = Surface ();
            using var _bitmap = bitmap;
            using var _canvas = canvas;
            canvas.Clear (SKColors.Black);

            ControlPaint.DrawFocusRectangle (graphics, new Rectangle (0, 0, 10, 10), Color.White, Color.Black);

            var lit = 0;

            for (var x = 0; x < 10; x++)
                for (var y = 0; y < 10; y++)
                    if (bitmap.GetPixel (x, y) != SKColors.Black)
                        lit++;

            Assert.True (lit > 0, "a white-on-black focus rectangle should be visible against black");
        }

        [Fact]
        public void The_focus_rectangle_stays_inside_its_rectangle ()
        {
            // It stroked the full rectangle, so a focus rect at ClientRectangle was clipped on the right
            // and bottom. Upstream decrements the width and height first.
            var (bitmap, graphics, canvas) = Surface ();
            using var _bitmap = bitmap;
            using var _canvas = canvas;

            ControlPaint.DrawFocusRectangle (graphics, new Rectangle (0, 0, 10, 10), Color.White, Color.Black);

            // The rightmost inked column, asserted as a NUMBER: "is (10,10) ink" was too blunt to see an
            // off-by-one, because a stroke centred on x=10 can round away from that exact pixel.
            var rightmost = -1;

            for (var x = 0; x < bitmap.Width; x++)
                for (var y = 0; y < bitmap.Height; y++)
                    if (IsInk (bitmap, x, y))
                        rightmost = Math.Max (rightmost, x);

            Assert.Equal (9, rightmost);
        }

        // ---------------- GFX-38: the themed-or-classic fork

        [Fact]
        public void RenderWithVisualStyles_agrees_with_VisualStyleRenderer ()
        {
            // The invariant, not the literals: upstream defines the two as the same answer. Asserting
            // the invariant means this keeps holding if a visual-style engine is implemented later.
            Assert.Equal (VisualStyleRenderer.IsSupported && Application.VisualStyleState != VisualStyleState.NoneEnabled,
                          Application.RenderWithVisualStyles);
        }

        [Fact]
        public void The_themed_or_classic_fork_produces_ink ()
        {
            // The shape every themed custom control is written as. It took the themed branch, whose
            // DrawBackground is empty, and never reached the fallback -- so the element rendered as a
            // blank rectangle. Both halves are exercised here; whichever the fork picks must draw.
            var (bitmap, graphics, canvas) = Surface ();
            using var _bitmap = bitmap;
            using var _canvas = canvas;
            var bounds = new Rectangle (4, 4, 20, 20);

            if (Application.RenderWithVisualStyles) {
                var renderer = new VisualStyleRenderer (VisualStyleElement.CreateElement ("BUTTON", 1, 1));
                renderer.DrawBackground (graphics, bounds);
            } else {
                ControlPaint.DrawButton (graphics, bounds, ButtonState.Normal);
            }

            Assert.True (InkCount (bitmap) > 0, "a themed-or-classic fork must draw through one branch or the other");
        }
    }
}
