using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

// Three small pieces of the WinForms surface that ask the operating system about itself: which optional
// window features it has (OSFeature), which keyboard layouts are installed (InputLanguage), and the
// standard alert sounds (System.Media.SystemSounds). Each is reachable on Windows only through Win32, so
// each answers here from what this library can actually see -- and where it can see nothing, it says so in
// the direction that makes callers pick their own fallback rather than trust an invented answer.

namespace Majorsilence.Forms
{
    /// <summary>Which system-wide metric to test for, for <see cref="OSFeature.IsPresent(SystemParameter)"/>.</summary>
    public enum SystemParameter
    {
        /// <summary>Whether drop shadows are drawn under menus.</summary>
        DropShadow = 0,
        /// <summary>Whether flat menus are in use.</summary>
        FlatMenu = 1,
        /// <summary>The configured font-smoothing contrast.</summary>
        FontSmoothingContrastMetric = 2,
        /// <summary>Which font-smoothing type is configured.</summary>
        FontSmoothingTypeMetric = 3,
        /// <summary>Whether menus fade out rather than vanish.</summary>
        MenuFadeEnabled = 4,
        /// <summary>Whether selection fades out rather than clearing.</summary>
        SelectionFade = 5,
        /// <summary>How tool tips animate into view.</summary>
        ToolTipAnimationMetric = 6,
        /// <summary>Whether UI effects are enabled at all.</summary>
        UIEffects = 7,
        /// <summary>The width of the text caret.</summary>
        CaretWidthMetric = 8,
        /// <summary>The thickness of the vertical part of a focus rectangle.</summary>
        VerticalFocusThicknessMetric = 9,
        /// <summary>The thickness of the horizontal part of a focus rectangle.</summary>
        HorizontalFocusThicknessMetric = 10,
    }

    /// <summary>Asks whether an optional platform feature is available, and at which version.</summary>
    /// <remarks>The base of <see cref="OSFeature"/>; declared separately because WinForms declares it
    /// separately and a caller may hold one as its base type.</remarks>
    public abstract class FeatureSupport
    {
        /// <summary>Gets the version of the feature that is present, or null when it is absent.</summary>
        public abstract Version? GetVersionPresent (object feature);

        /// <summary>Gets whether the feature is present at all.</summary>
        public virtual bool IsPresent (object feature) => GetVersionPresent (feature) is not null;

        /// <summary>Gets whether the feature is present at or above the given version.</summary>
        public virtual bool IsPresent (object feature, Version minimumVersion)
            => GetVersionPresent (feature) is { } version && version >= minimumVersion;

        /// <summary>Gets whether a feature named by a class-and-constant pair is present.</summary>
        /// <remarks>Always false. The pair names a type to load by reflection and a constant to read off
        /// it, which is a lookup this library has no table for.</remarks>
        public static bool IsPresent (string featureClassName, string featureConstName) => false;

        /// <inheritdoc cref="IsPresent(string, string)"/>
        public static bool IsPresent (string featureClassName, string featureConstName, Version minimumVersion) => false;

        /// <inheritdoc cref="IsPresent(string, string)"/>
        public static Version? GetVersionPresent (string featureClassName, string featureConstName) => null;
    }

    /// <summary>Reports which optional windowing features the operating system offers.</summary>
    /// <remarks>
    /// Every feature reports absent. The two that are asked for in practice are layered windows and visual
    /// styles, and absent is the true answer for both: per-pixel window alpha is not implemented here (see
    /// <c>Form.AllowTransparency</c>, which stores its value and does nothing with it) and there is no
    /// msstyles theme engine (see <see cref="VisualStyles.VisualStyleInformation.IsEnabledByUser"/>).
    ///
    /// This is the useful direction to be wrong in, and the reason the type is worth having rather than
    /// stubbing at the call site: code that tests for layered windows does so to choose between an
    /// alpha-blended effect and a plain one — Krypton picks rounded versus square drag feedback this way —
    /// so answering null routes it to the path that actually renders correctly here.
    /// </remarks>
    public class OSFeature : FeatureSupport
    {
        private OSFeature () { }

        /// <summary>Gets the single instance features are queried through.</summary>
        public static OSFeature Feature { get; } = new OSFeature ();

        /// <summary>Names the layered-window (per-pixel alpha) feature.</summary>
        public static readonly object LayeredWindows = new object ();

        /// <summary>Names the visual-styles (msstyles theming) feature.</summary>
        public static readonly object Themes = new object ();

        /// <inheritdoc/>
        public override Version? GetVersionPresent (object feature) => null;

        /// <summary>Gets whether the given system metric is enabled. Always false.</summary>
        public static bool IsPresent (SystemParameter enumVal) => false;
    }

    /// <summary>One keyboard layout the user can type with.</summary>
    /// <remarks>
    /// Enumerating installed keyboard layouts is a Win32 call with no cross-platform equivalent, so this
    /// answers from the culture instead: the current input language is the current culture, and the
    /// installed set is the cultures .NET reports for this machine. That is genuinely useful for the thing
    /// callers do with it — naming the language the user is working in — and it is honest about the part it
    /// cannot know, which is the layout: <see cref="LayoutName"/> gives the culture's English name rather
    /// than inventing a layout identifier, and <see cref="Handle"/> is zero because there is no HKL.
    /// </remarks>
    public sealed class InputLanguage
    {
        private InputLanguage (CultureInfo culture) => Culture = culture;

        /// <summary>Gets the culture of this input language.</summary>
        public CultureInfo Culture { get; }

        /// <summary>Gets the keyboard-layout handle. Always zero: there is no HKL here.</summary>
        public IntPtr Handle => IntPtr.Zero;

        /// <summary>Gets a display name for the layout, which here is the culture's English name.</summary>
        public string LayoutName => Culture.EnglishName;

        /// <summary>Gets or sets the input language in use.</summary>
        /// <remarks>The setter is honoured as far as it can be: it stores the choice and later reads come
        /// back with it, but it does not switch the OS keyboard layout, which this layer cannot do.</remarks>
        public static InputLanguage? CurrentInputLanguage {
            get => current ??= new InputLanguage (CultureInfo.CurrentCulture);
            set => current = value;
        }

        private static InputLanguage? current;

        /// <summary>Gets the input language the machine starts in.</summary>
        public static InputLanguage DefaultInputLanguage { get; } =
            new InputLanguage (CultureInfo.InstalledUICulture);

        /// <summary>Gets the input languages available on this machine.</summary>
        /// <remarks>
        /// The current culture and the installed UI culture, de-duplicated — the two the runtime can name
        /// without asking Win32 for the layout list. A caller listing them for the user gets the languages
        /// actually in play rather than an empty list.
        /// </remarks>
        public static InputLanguageCollection InstalledInputLanguages { get; } = BuildInstalled ();

        /// <summary>Returns the input language for the given culture, or null when it is not installed.</summary>
        public static InputLanguage? FromCulture (CultureInfo culture)
        {
            ArgumentNullException.ThrowIfNull (culture);

            foreach (InputLanguage language in InstalledInputLanguages) {
                if (string.Equals (language.Culture.Name, culture.Name, StringComparison.OrdinalIgnoreCase))
                    return language;
            }

            return null;
        }

        private static InputLanguageCollection BuildInstalled ()
        {
            var cultures = new List<CultureInfo> { CultureInfo.CurrentCulture };

            if (!string.Equals (CultureInfo.InstalledUICulture.Name, CultureInfo.CurrentCulture.Name,
                                StringComparison.OrdinalIgnoreCase))
                cultures.Add (CultureInfo.InstalledUICulture);

            return new InputLanguageCollection (cultures.Select (c => new InputLanguage (c)).ToArray ());
        }
    }

    /// <summary>A read-only collection of <see cref="InputLanguage"/>.</summary>
    public class InputLanguageCollection : IEnumerable<InputLanguage>
    {
        private readonly InputLanguage[] languages;

        internal InputLanguageCollection (InputLanguage[] languages) => this.languages = languages;

        /// <summary>Gets the number of languages in the collection.</summary>
        public int Count => languages.Length;

        /// <summary>Gets the language at the given index.</summary>
        public InputLanguage this[int index] => languages[index];

        /// <summary>Returns whether the language is in the collection.</summary>
        public bool Contains (InputLanguage value) => IndexOf (value) >= 0;

        /// <summary>Returns the index of the language, or -1.</summary>
        public int IndexOf (InputLanguage value) => Array.IndexOf (languages, value);

        /// <summary>Copies the languages into the given array.</summary>
        public void CopyTo (InputLanguage[] array, int index) => languages.CopyTo (array, index);

        /// <inheritdoc/>
        public IEnumerator<InputLanguage> GetEnumerator () => ((IEnumerable<InputLanguage>)languages).GetEnumerator ();

        IEnumerator IEnumerable.GetEnumerator () => languages.GetEnumerator ();
    }
}

namespace Majorsilence.Forms.Media
{
    /// <summary>One of the operating system's standard alert sounds.</summary>
    /// <remarks>
    /// Stands in for <c>System.Media.SystemSound</c>, which lives in a Windows-only assembly.
    /// <see cref="Play"/> is real: it routes through the operating system's own alert sounds (see
    /// <see cref="NativeAudio"/> for the per-platform mapping and the silence fallback).
    /// </remarks>
    public class SystemSound
    {
        internal SystemSound (string name) => Name = name;

        /// <summary>Gets the name of the sound this instance stands for.</summary>
        /// <remarks>Not upstream. Present so that a caller wiring its own audio can tell the five sounds
        /// apart rather than having to compare against the <see cref="SystemSounds"/> properties.</remarks>
        public string Name { get; }

        /// <summary>Plays the sound through the operating system's alert-sound path.</summary>
        /// <remarks>Real as of 2026-08: routed through <see cref="NativeAudio"/> -- Windows' own
        /// SystemSounds, macOS's stock alert set, the freedesktop sound theme on Linux. Fire-and-forget
        /// and never throws; where no OS path exists it stays silent, as the stub did.</remarks>
        public void Play () => NativeAudio.Start (NativeAudio.SystemSoundCommands (Name));
    }

    /// <summary>Plays a .wav file or stream.</summary>
    /// <remarks>
    /// Stands in for <c>System.Media.SoundPlayer</c>, which lives in a Windows-only assembly. Playback
    /// is real: it routes through <see cref="NativeAudio"/>, so the sound plays through the operating
    /// system's own utility and the child process gives the API its semantics -- <see cref="Stop"/>
    /// kills it, <see cref="PlaySync"/> waits for it, <see cref="PlayLooping"/> respawns it until
    /// stopped. A stream is materialised to a temporary .wav the utility can open, once per stream, and
    /// deleted on dispose. The Load family still completes immediately: there is nothing to preload
    /// when the OS opens the file itself, and <see cref="IsLoadCompleted"/> stays true so a caller that
    /// waits for the load is never left waiting.
    /// </remarks>
    public class SoundPlayer : System.ComponentModel.Component
    {
        private System.IO.Stream? stream;
        private string sound_location = string.Empty;
        private string? temp_file;
        private IPlayingSound? playing;
        private System.Threading.CancellationTokenSource? loop;

        /// <summary>Initializes an empty player.</summary>
        public SoundPlayer () { }

        /// <summary>Initializes a player for the given .wav stream.</summary>
        public SoundPlayer (System.IO.Stream? stream) => Stream = stream;

        /// <summary>Initializes a player for the given .wav path.</summary>
        public SoundPlayer (string soundLocation) => SoundLocation = soundLocation ?? string.Empty;

        /// <summary>Gets or sets the .wav stream to play.</summary>
        public System.IO.Stream? Stream {
            get => stream;
            set {
                if (ReferenceEquals (stream, value))
                    return;

                stream = value;
                DeleteTempFile ();   // the materialised copy no longer matches
                StreamChanged?.Invoke (this, EventArgs.Empty);
            }
        }

        /// <summary>Gets or sets the path of the .wav to play.</summary>
        /// <remarks>A local file path. URLs are accepted for compatibility but play silently -- fetching
        /// remote audio is not something this layer will do implicitly.</remarks>
        public string SoundLocation {
            get => sound_location;
            set {
                value ??= string.Empty;
                if (sound_location == value)
                    return;

                sound_location = value;
                SoundLocationChanged?.Invoke (this, EventArgs.Empty);
            }
        }

        /// <summary>Gets whether loading has finished. Always true: the OS utility opens the file itself.</summary>
        public bool IsLoadCompleted => true;

        /// <summary>Gets or sets how long a load may take, in milliseconds.</summary>
        public int LoadTimeout { get; set; } = 10_000;

        /// <summary>Gets or sets arbitrary data associated with the player.</summary>
        public object? Tag { get; set; }

        /// <summary>Loads the sound synchronously. Completes immediately; see <see cref="IsLoadCompleted"/>.</summary>
        public void Load () { }

        /// <summary>Loads the sound asynchronously. Completes immediately, raising <see cref="LoadCompleted"/>.</summary>
        public void LoadAsync () => LoadCompleted?.Invoke (this,
            new System.ComponentModel.AsyncCompletedEventArgs (null, false, null));

        /// <summary>Plays the sound without blocking. A play already in progress is stopped first, as upstream.</summary>
        public void Play ()
        {
            Stop ();
            playing = StartOnce ();
        }

        /// <summary>Plays the sound and blocks until it finishes.</summary>
        public void PlaySync ()
        {
            Stop ();
            playing = StartOnce ();
            playing?.Wait ();
        }

        /// <summary>Plays the sound repeatedly until <see cref="Stop"/> is called.</summary>
        /// <remarks>Looped by respawning the player as each pass ends -- the utilities have no loop flag
        /// in common -- so there is a brief seam between iterations. Alert-style cues loop cleanly;
        /// gapless music loops are out of scope for this API upstream too.</remarks>
        public void PlayLooping ()
        {
            Stop ();

            var cts = new System.Threading.CancellationTokenSource ();
            loop = cts;

            System.Threading.Tasks.Task.Run (() => {
                while (!cts.IsCancellationRequested) {
                    var pass = StartOnce ();
                    if (pass is null)
                        return;    // nothing can play; do not spin

                    playing = pass;
                    pass.Wait ();
                }
            }, cts.Token);
        }

        /// <summary>Stops playback, ending a loop if one is running.</summary>
        public void Stop ()
        {
            loop?.Cancel ();
            loop = null;
            playing?.Dispose ();
            playing = null;
        }

        private IPlayingSound? StartOnce ()
        {
            var path = ResolvePath ();
            return path is null ? null : NativeAudio.Start (NativeAudio.FileCommands (path));
        }

        // SoundLocation wins when both are set, matching the "whichever identifies a sound" contract;
        // a URL location plays silently rather than fetching.
        private string? ResolvePath ()
        {
            if (sound_location.Length > 0)
                return System.IO.File.Exists (sound_location) ? sound_location : null;

            if (stream is null)
                return null;

            if (temp_file is null) {
                try {
                    var path = System.IO.Path.Combine (System.IO.Path.GetTempPath (),
                        $"majorsilence-sound-{Guid.NewGuid ():N}.wav");

                    using (var file = System.IO.File.Create (path)) {
                        if (stream.CanSeek)
                            stream.Position = 0;
                        stream.CopyTo (file);
                    }

                    temp_file = path;
                } catch {
                    return null;   // unreadable stream or unwritable temp dir: silence, not an exception
                }
            }

            return temp_file;
        }

        private void DeleteTempFile ()
        {
            if (temp_file is null)
                return;

            try { System.IO.File.Delete (temp_file); } catch { }
            temp_file = null;
        }

        /// <inheritdoc/>
        protected override void Dispose (bool disposing)
        {
            if (disposing) {
                Stop ();
                DeleteTempFile ();
            }

            base.Dispose (disposing);
        }

        /// <summary>Raised when an asynchronous load finishes. Raised by <see cref="LoadAsync"/> immediately.</summary>
        public event System.ComponentModel.AsyncCompletedEventHandler? LoadCompleted;

        /// <summary>Raised when <see cref="SoundLocation"/> changes.</summary>
        public event EventHandler? SoundLocationChanged;

        /// <summary>Raised when <see cref="Stream"/> changes.</summary>
        public event EventHandler? StreamChanged;
    }

    /// <summary>The operating system's standard alert sounds.</summary>
    /// <inheritdoc cref="SystemSound"/>
    public static class SystemSounds
    {
        /// <summary>The sound for an informational message.</summary>
        public static SystemSound Asterisk { get; } = new SystemSound (nameof (Asterisk));

        /// <summary>The default beep.</summary>
        public static SystemSound Beep { get; } = new SystemSound (nameof (Beep));

        /// <summary>The sound for a warning.</summary>
        public static SystemSound Exclamation { get; } = new SystemSound (nameof (Exclamation));

        /// <summary>The sound for a critical error.</summary>
        public static SystemSound Hand { get; } = new SystemSound (nameof (Hand));

        /// <summary>The sound for a question.</summary>
        public static SystemSound Question { get; } = new SystemSound (nameof (Question));
    }
}
