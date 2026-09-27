# Animation

## Frames: `RequestAnimationFrame`

```csharp
void OnFrame (TimeSpan timestamp)
{
    Progress = Math.Min (1, (timestamp - start).TotalSeconds / 0.4);
    Invalidate ();

    if (Progress < 1)
        RequestAnimationFrame (OnFrame);   // ask for the next frame from inside this one
}

start = ... ; RequestAnimationFrame (first);
```

`control.RequestAnimationFrame (callback)` (and the same on a `Form`) calls `callback` **once**, at the start of the next display frame,
with a timestamp that increases from frame to frame. To keep animating, ask again from inside the callback.

- **One shot.** A request is served once. A request made inside a callback is served on the frame **after**, never the same one, so an
  animation cannot run away inside a single frame.
- **Only differences mean anything.** The timestamp has no fixed origin; subtract the first one you saw.
- **UI thread only**, like the controls it belongs to.
- **Several requests** waiting at the start of a frame all run in that frame, in the order they were made, with the same timestamp.

### Where the frames come from

| Backend | Source |
|---|---|
| Avalonia | The top level's own frame request (`TopLevel.RequestAnimationFrame`), so the callback is aligned to the display. A single-view host that is not yet in the visual tree has no top level, and uses the timer below until it does. |
| Headless | A **manual clock**: nothing runs until you step it (see below). |
| Any other backend | A timer at about 60 Hz on the UI thread, which is what animation was before there was a frame request. |

A window's own backend is asked first, then the platform backend, then the timer.

**Measured, Avalonia:** on a Linux desktop (GNOME, through Xwayland), 180 consecutive callbacks from a small window had a median gap of
16.00 ms and a maximum of 18.94 ms after the first ten frames, and the timestamps were strictly increasing and equal to wall-clock time.
The window's start-up is not steady: the second interval was about 1.08 s in both runs, which was not investigated. There is no automated
test of the Avalonia path, because it needs a running Avalonia application. **Not measured:** Android, iOS, the browser, another
compositor, and the timer fallback on a real backend.

### Testing an animation: the Headless clock

```csharp
HeadlessRenderer.Use ();
var clock = HeadlessRenderer.AnimationClock;
clock.Reset ();                      // so a previous test's animation cannot leak into this one

var timestamps = new List<TimeSpan> ();
void OnFrame (TimeSpan t)
{
    timestamps.Add (t);
    if (timestamps.Count < 10)
        control.RequestAnimationFrame (OnFrame);
}
control.RequestAnimationFrame (OnFrame);

clock.Step (10);                     // ten frames of 1/60 s each; nothing ran before this line
```

`Step (frames, frameInterval)` advances time by that many frames (default one sixtieth of a second each), and on each runs the requests
that were waiting when the frame began. `Now`, `PendingCount` and `Reset ()` are there too. The clock belongs to the Headless backend, so
a control that is not on a window is deterministic as well.

### For backend authors

A backend that can call back once per display frame implements `Majorsilence.Forms.Backends.IAnimationFrameSource` on its window backend
or on the platform backend. It is optional, so a backend that does not is unaffected and gets the timer.
