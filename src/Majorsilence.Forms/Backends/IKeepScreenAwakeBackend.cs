namespace Majorsilence.Forms.Backends
{
    /// <summary>
    /// Optional capability implemented by a platform backend that can keep the screen on (Android's
    /// <c>FLAG_KEEP_SCREEN_ON</c>, iOS's <c>IdleTimerDisabled</c>, or a desktop OS's own sleep-inhibit
    /// mechanism). Discovered via <c>Platform.Backend as IKeepScreenAwakeBackend</c>, the same
    /// optional-capability pattern <see cref="IAudioBackend"/> and <see cref="IHapticsBackend"/> use.
    /// Unlike those, this is a plain stateful property, not a fire-and-forget action -- the app turns it
    /// on and off itself (a bedside/status-display screen), there is nothing external to poll or be told
    /// about changing, so there is no companion changed event the way <see cref="IReducedMotionSource"/> has one.
    /// </summary>
    public interface IKeepScreenAwakeBackend
    {
        /// <summary>Gets or sets whether the screen is being kept on.</summary>
        bool KeepScreenAwake { get; set; }
    }
}
