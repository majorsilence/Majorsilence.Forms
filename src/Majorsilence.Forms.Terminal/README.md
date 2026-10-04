# Majorsilence.Forms.Terminal

Runs a Majorsilence.Forms app in a terminal. The form is drawn by the usual SkiaSharp pipeline and shown one
of three ways:

| Mode | Resolution | When |
|---|---|---|
| **Kitty graphics** | the terminal's real pixels | kitty, WezTerm, Ghostty (detected) |
| **Sixel** | the terminal's real pixels, 256 colours | foot, mlterm, iTerm2, Contour (detected); xterm with `-ti vt340`, WezTerm (by override) |
| **Half-block** | 1 pixel per column, 2 per row, 24-bit/256/16 colours | everything else, and always inside tmux/screen |

**Which mode.** At startup the app asks the terminal what it supports instead of guessing: one write carrying a
Kitty graphics query, a Kitty keyboard query and a primary-device-attributes request (which also lists Sixel).
Terminals answer in order, so the attributes reply, asked last, marks the end of the answers and a missing answer
means "no" without a timeout per question. The first frame waits for it (up to 0.4 s for a terminal that answers
nothing, which then keeps the environment's guess). An answer replaces the guess: Kitty graphics if the terminal
accepts them, else Sixel if it advertises it, else half-blocks. The environment guess (`TERM`, `TERM_PROGRAM`,
`KITTY_WINDOW_ID`, with half-blocks forced inside tmux/screen) is only the starting point and the fallback when
there is no input to read replies from. Pin a mode with `MF_TERMINAL_GRAPHICS=halfblock|kitty|sixel` or
`TerminalOptions.GraphicsMode`; a pinned mode is never overridden by the terminal. In the pixel modes the app also
asks for the cell size (`ESC[16t`) and renders at the true pixel size, so scale 1 is right; in half-block mode a
form needs `TerminalOptions.Scaling` below 1 and a large terminal to be readable.

**What is re-sent.** Half-block repaints only changed cells. Sixel repaints the bounding box of changed cells.
Kitty splits the window into tiles of 16x8 cells, one image per tile, and re-sends only the tiles whose pixels
changed (deflated), so a caret blink is one tile. Tiles rather than the protocol's frame-edit command, which only
kitty itself implements fully; replacing an image by id is what every Kitty-protocol terminal does.

**Input.** The mouse (clicks, drags, hover, wheel) and keyboard (text, arrows, Home/End/Page, Insert/Delete,
F1-F12, Ctrl/Alt/Shift) are decoded from the terminal's byte stream. One cell is one pixel wide and two tall,
so pointer precision is a cell. With Kitty graphics the mouse is pixel-exact (xterm's 1016 mode); with Sixel a click lands on the centre of its cell. On a legacy terminal only presses are reported, so each key-down is followed by a key-up straight away. If the
terminal implements the Kitty keyboard protocol it is switched on (and switched off again on exit): then key
releases and repeats are real, modifiers are exact, the text a key types comes from the terminal (so non-US layouts
type correctly while shortcuts still use the key's US-layout identity), and Escape is unambiguous. **Ctrl+C always
exits** and is never delivered to the app: a legacy terminal raises SIGINT for it, and under the Kitty keyboard
protocol it arrives as a key, which the host catches. Pastes arrive as one bracketed paste. If stdin is redirected, or `stty` is missing on
Unix, the app still renders but takes no input. Windows input and output use console VT modes
(not yet exercised on a Windows machine).

**Not implemented yet:** asking the terminal what it supports (DA1/Kitty query) instead of reading the environment, dirty-rectangle updates for Kitty, the Kitty keyboard protocol (key releases, exact
modifiers), and a terminal-sized layout story. Native file pickers, `NativeControlHost` and web views
have no terminal equivalent.
