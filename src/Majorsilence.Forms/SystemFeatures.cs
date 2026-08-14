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
    /// Stands in for <c>System.Media.SystemSound</c>, which lives in a Windows-only assembly. Playing the
    /// OS alert sounds needs a per-platform audio path this library does not carry, so
    /// <see cref="Play"/> is silent — the stub policy's neutral outcome. A message box that plays a sound
    /// alongside its icon still shows the icon, which is the part that carries the meaning.
    /// </remarks>
    public class SystemSound
    {
        internal SystemSound (string name) => Name = name;

        /// <summary>Gets the name of the sound this instance stands for.</summary>
        /// <remarks>Not upstream. Present so that a caller wiring its own audio can tell the five sounds
        /// apart rather than having to compare against the <see cref="SystemSounds"/> properties.</remarks>
        public string Name { get; }

        /// <summary>Plays the sound. Silent here: there is no cross-platform system-sound path.</summary>
        public void Play () { }
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
