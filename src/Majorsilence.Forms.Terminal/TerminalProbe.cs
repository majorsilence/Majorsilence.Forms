using System;

namespace Majorsilence.Forms.Terminal
{
    /// <summary>
    /// Asks the terminal what it supports instead of guessing from environment variables, and collects the
    /// answers. Three questions go out in one write, with the device attributes query last: a terminal
    /// answers in order, so once that reply is in, every other reply that is coming has arrived, and one
    /// that is missing means "not supported" without a timeout per question.
    /// </summary>
    internal sealed class TerminalProbe
    {
        // The id the Kitty graphics query uses; it only has to match the reply.
        internal const int GraphicsQueryId = 31;

        /// <summary>
        /// The bytes to send. <c>a=q</c> asks a Kitty graphics terminal whether it could display a 1x1 RGB
        /// image (it displays nothing); <c>ESC[?u</c> asks for the Kitty keyboard flags; <c>ESC[c</c> is the
        /// sentinel and also lists Sixel support.
        /// </summary>
        public static string Query => $"\u001b_Gi={GraphicsQueryId},s=1,v=1,a=q,t=d,f=24;AAAA\u001b\\\u001b[?u\u001b[c";

        /// <summary>Gets whether the device attributes reply has arrived, which ends the probe.</summary>
        public bool Complete { get; private set; }

        /// <summary>Gets whether the terminal answered the Kitty graphics query with OK.</summary>
        public bool KittyGraphics { get; private set; }

        /// <summary>Gets whether the terminal advertised Sixel in its device attributes.</summary>
        public bool Sixel { get; private set; }

        /// <summary>Gets whether the terminal answered the Kitty keyboard query, i.e. implements the protocol.</summary>
        public bool KittyKeyboard { get; private set; }

        /// <summary>Feeds one decoded input event; events that are not probe replies are ignored. Returns whether it was a probe reply.</summary>
        public bool Observe (TerminalInput e)
        {
            switch (e.Kind) {
                case TerminalInputKind.GraphicsReply:
                    if (e.Col == GraphicsQueryId && e.Text == "OK")
                        KittyGraphics = true;
                    return true;
                case TerminalInputKind.KeyboardFlags:
                    KittyKeyboard = true;
                    return true;
                case TerminalInputKind.DeviceAttributes:
                    // The attribute list is ';'-separated; 4 is Sixel graphics.
                    foreach (var attribute in (e.Text ?? string.Empty).Split (';'))
                        if (attribute == "4")
                            Sixel = true;
                    Complete = true;
                    return true;
                default:
                    return false;
            }
        }

        /// <summary>The best way to show frames given the answers: real Kitty graphics, then Sixel, else half-blocks.</summary>
        public TerminalGraphicsMode Decide ()
            => KittyGraphics ? TerminalGraphicsMode.Kitty
             : Sixel ? TerminalGraphicsMode.Sixel
             : TerminalGraphicsMode.HalfBlock;
    }
}
