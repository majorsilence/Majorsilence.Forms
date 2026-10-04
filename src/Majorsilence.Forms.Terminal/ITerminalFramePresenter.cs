using System;
using System.Buffers;

namespace Majorsilence.Forms.Terminal
{
    /// <summary>Turns a rendered bitmap into the bytes that show it, remembering the previous frame so it can send only what changed.</summary>
    internal interface ITerminalFramePresenter
    {
        /// <summary>Forgets what is on screen, so the next <see cref="Encode"/> sends everything (after a resize or a screen clear).</summary>
        void Reset ();

        /// <summary>Appends the bytes that bring the screen to this frame (nothing when it did not change).</summary>
        /// <param name="bgra">Bgra8888 pixels, rows <paramref name="stride"/> bytes apart. Alpha is ignored: a window is opaque.</param>
        /// <param name="width">Pixel width.</param>
        /// <param name="height">Pixel height.</param>
        /// <param name="stride">Bytes per bitmap row.</param>
        /// <param name="output">Receives the bytes to write to the terminal.</param>
        void Encode (ReadOnlySpan<byte> bgra, int width, int height, int stride, IBufferWriter<byte> output);

        /// <summary>Gets what to write on the way out so nothing of the app is left on the user's screen (kitty images outlive the alternate screen).</summary>
        string ExitSequence { get; }
    }
}
