using System;
using System.Collections.Generic;
using System.Linq;
using Majorsilence.Forms.Drawing;
using SkiaSharp;
using Xunit;

namespace Majorsilence.Forms.Tests;

// GFX-36: an animated GIF in a PictureBox showed its first frame for ever. Nothing started the
// animator, and the animator had no timer of its own. Upstream PictureBox animates while it is
// visible, enabled and parented, and repaints on every frame change; the animator's timer moves the
// frames on at their own delays. Time here is a manual clock -- no test waits on the wall clock.
[Collection ("Headless")]
public sealed class PictureBoxAnimationTests : IDisposable
{
    private readonly Func<long> saved_clock = ImageAnimator.Clock;
    private readonly Func<Action, IDisposable> saved_factory = ImageAnimator.TimerFactory;
    private readonly Action<Action>? saved_dispatcher = ImageAnimator.UIThreadDispatcher;
    private readonly List<Action> posted = [];
    private readonly List<PictureBox> boxes = [];
    private Action? timer_callback;
    private long now;

    public PictureBoxAnimationTests ()
    {
        Majorsilence.Forms.Headless.HeadlessRenderer.Use ();
        ImageAnimator.Clock = () => now;
        ImageAnimator.TimerFactory = callback => {
            timer_callback = callback;
            return new Stop (() => timer_callback = null);
        };
        ImageAnimator.UIThreadDispatcher = null;
    }

    public void Dispose ()
    {
        // A test that fails part-way must not leave an image animating into the next one.
        foreach (var box in boxes)
            box.Dispose ();

        ImageAnimator.Clock = saved_clock;
        ImageAnimator.TimerFactory = saved_factory;
        ImageAnimator.UIThreadDispatcher = saved_dispatcher;
    }

    private PictureBox Track (PictureBox box)
    {
        boxes.Add (box);
        return box;
    }

    private sealed class Stop (Action stop) : IDisposable
    {
        public void Dispose () => stop ();
    }

    // The timer firing after a given time, and whatever it posted then running on the UI thread.
    private void Elapse (long milliseconds)
    {
        now += milliseconds;
        timer_callback?.Invoke ();
        var queued = posted.ToArray ();
        posted.Clear ();
        foreach (var action in queued)
            action ();
    }

    // A two-frame 1x1 GIF, red then blue, 100 ms each, looping for ever (built by hand: Skia cannot
    // write a multi-frame GIF). The box stretches it, so every pixel shows the frame.
    private static byte[] TwoFrameGif ()
    {
        var bytes = new List<byte> ();
        void Add (params int[] values) => bytes.AddRange (values.Select (v => (byte)v));

        Add (0x47, 0x49, 0x46, 0x38, 0x39, 0x61);   // GIF89a
        Add (1, 0, 1, 0, 0x80, 0, 0);                // 1x1, 2-entry global colour table
        Add (0xFF, 0x00, 0x00, 0x00, 0x00, 0xFF);   // red, blue
        Add (0x21, 0xFF, 0x0B);
        bytes.AddRange ("NETSCAPE2.0"u8.ToArray ());
        Add (0x03, 0x01, 0x00, 0x00, 0x00);

        foreach (var index in new[] { 0, 1 }) {
            Add (0x21, 0xF9, 0x04, 0x00, 0x0A, 0x00, 0x00, 0x00);   // 100 ms
            Add (0x2C, 0, 0, 0, 0, 1, 0, 1, 0, 0);                  // 1x1 frame
            Add (0x02, 0x02, index == 0 ? 0x44 : 0x4C, 0x01, 0x00); // clear, index, end
        }

        Add (0x3B);
        return [.. bytes];
    }

    private static SKColor Center (PictureBox box)
    {
        using var bitmap = PaintSurface.Render (box);
        return bitmap.GetPixel (bitmap.Width / 2, bitmap.Height / 2);
    }

    // Guard for the fixture: the hand-built GIF must decode as an animation for the tests below to mean anything.
    [Fact]
    public void The_test_gif_has_two_frames ()
    {
        using var image = Image.FromBytes (TwoFrameGif ());
        Assert.True (ImageAnimator.CanAnimate (image));
    }

    [Fact]
    public void An_animated_gif_moves_to_its_next_frame_after_the_frame_delay ()
    {
        using var image = Image.FromBytes (TwoFrameGif ());
        using var form = new Form ();
        var box = Track (new PictureBox { Image = image, Size = new System.Drawing.Size (8, 8), SizeMode = PictureBoxSizeMode.StretchImage });

        // Not animating until it is parented, as upstream.
        Assert.False (ImageAnimator.IsTimerRunning);
        form.Controls.Add (box);
        Assert.True (ImageAnimator.IsTimerRunning);

        // The animator now posts through the UI thread; capture what it posts.
        Assert.NotNull (ImageAnimator.UIThreadDispatcher);
        ImageAnimator.UIThreadDispatcher = posted.Add;

        var first = Center (box);
        Assert.Equal (SKColors.Red, first);

        Elapse (50);
        Assert.Equal (SKColors.Red, Center (box));   // frame 0 lasts 100 ms

        Elapse (60);
        Assert.Equal (SKColors.Blue, Center (box));

        Elapse (100);
        Assert.Equal (SKColors.Red, Center (box));   // and loops

        // Disposing the form disposes its controls, as upstream's Control.Dispose does, and a disposed
        // box stops animating at once -- not at its next frame change. With nothing left animating the
        // timer stops too.
        form.Dispose ();
        Assert.True (box.IsDisposed);
        Assert.False (ImageAnimator.IsTimerRunning);
    }

    [Fact]
    public void A_hidden_or_disabled_box_does_not_animate ()
    {
        using var image = Image.FromBytes (TwoFrameGif ());
        using var form = new Form ();
        var box = Track (new PictureBox { Image = image });
        form.Controls.Add (box);
        Assert.True (ImageAnimator.IsTimerRunning);

        box.Enabled = false;
        Assert.False (ImageAnimator.IsTimerRunning);
        box.Enabled = true;
        Assert.True (ImageAnimator.IsTimerRunning);

        box.Visible = false;
        Assert.False (ImageAnimator.IsTimerRunning);
        box.Visible = true;

        // Replacing the image stops the old one's animation.
        box.Image = null;
        Assert.False (ImageAnimator.IsTimerRunning);
        box.Dispose ();
    }
}
