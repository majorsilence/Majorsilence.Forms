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

## Tweens and the `Animator` (`Majorsilence.Forms.Animation`)

Every animated control needs the same three things: a curve, a value that follows it, and something to drive it a frame at a time.

```csharp
using Majorsilence.Forms.Animation;

var tween = Tween.Of (0f, 1f, TimeSpan.FromMilliseconds (400), Easing.CubicOut);

var handle = card.Animate (tween,
    lift => { card.Lift = lift; card.Invalidate (); },   // called on every frame with the value for that moment
    () => card.OnLifted ());                              // called once at the end

// later, if it should stop where it is
handle.Cancel ();
```

### `Tween<T>`

A tween is a value moving from one value to another over a duration, shaped by an easing function. It holds **no clock and no state**: ask
it for the value at a time (`ValueAt (elapsed)`) or at a progress from 0 to 1 (`ValueAtProgress (t)`), so it is deterministic, and one
tween can drive any number of animations.

- `Tween.Of` makes one for a `float`, a `PointF` (each coordinate moves as a number) or a `Color`. Any other type takes your own
  interpolation: `new Tween<T> (from, to, duration, (a, b, t) => ..., easing)`.
- Before the start it is at `From`; at or after `Duration` it is at `To`; a duration of zero or less is at `To` from the first frame.
- **A colour moves per channel in sRGB**, alpha included. That is simple and predictable and it is not perceptually even, so a
  mid-point between two saturated colours can look muddier than you expect. Channels are clamped to 0 to 255.
- **A back easing overshoots**, so a `float` or `PointF` tween passes through values outside `From` to `To`, on purpose. A colour is
  clamped instead of wrapping.

### `Easing`

`Linear`; `QuadIn`, `QuadOut`, `QuadInOut`; `CubicIn`, `CubicOut`, `CubicInOut`; `BackIn`, `BackOut`, `BackInOut` (overshoot);
`BounceIn`, `BounceOut`, `BounceInOut`. They are the standard set at easings.net, each is `float -> float`, each is 0 at 0 and 1 at 1,
and any one converts to an `EasingFunction`, so you can also pass your own lambda.

### `Animate`

`control.Animate (tween, apply, completed)` runs the tween on `RequestAnimationFrame`.

- **The first frame is time zero**, so `apply` gets the start value first and the movement takes its full duration. The last frame gets
  the end value, and then `completed` runs **once**.
- `handle.Cancel ()` stops it where it is: no more values, and `completed` does not run. It is safe to call twice and from inside `apply`.
  `handle.IsRunning` is true until it completes or is cancelled (and is already false inside `completed`).
- It stops by itself if the control is disposed.
- An exception from `apply` or `completed` ends the animation and comes out of the frame callback.
- **It does not read the reduced-motion setting.** Whether to animate is the caller's decision; a platform reduced-motion setting is
  tracked separately (#269).

### Testing an animation

Step the Headless clock (see above), and the values are exact:

```csharp
HeadlessRenderer.Use ();
HeadlessRenderer.AnimationClock.Reset ();

var values = new List<float> ();
panel.Animate (Tween.Of (0f, 100f, TimeSpan.FromMilliseconds (400)), values.Add);

HeadlessRenderer.AnimationClock.Step (5, TimeSpan.FromMilliseconds (100));
// values is 0, 25, 50, 75, 100
```
