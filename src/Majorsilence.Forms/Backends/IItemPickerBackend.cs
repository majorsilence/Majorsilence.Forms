namespace Majorsilence.Forms.Backends
{
    /// <summary>
    /// Optional capability implemented by a platform backend whose platform has its own way to pick one item
    /// from a list (on Android, a dialog listing the items), which a finger finds easier than the small
    /// drop-down a desktop <see cref="ComboBox"/> opens. Discovered via <c>Platform.Backend as IItemPickerBackend</c>,
    /// the same optional-capability pattern <see cref="IKeepScreenAwakeBackend"/> and <see cref="IHapticsBackend"/> use.
    /// A <see cref="ComboBox"/> asks for it before it opens its own popup, and keeps the popup when the backend
    /// does not implement this or <see cref="PrefersNativeItemPicker"/> is false.
    /// </summary>
    public interface IItemPickerBackend
    {
        /// <summary>
        /// Gets whether this platform should pick from a list with <see cref="ShowItemPicker"/> rather than a
        /// drop-down popup. False on desktop and the browser, where the pointer makes the popup fine.
        /// </summary>
        bool PrefersNativeItemPicker { get; }

        /// <summary>
        /// Shows the platform's item picker and returns at once; the choice arrives through <paramref name="completed"/>.
        /// </summary>
        /// <param name="title">A heading for the picker, or null for none.</param>
        /// <param name="items">The text of each item, in order.</param>
        /// <param name="selectedIndex">The item to mark as current, or -1 for none.</param>
        /// <param name="completed">
        /// Called once, on the UI thread, with the chosen index, or -1 when the picker was dismissed without a choice.
        /// </param>
        /// <returns>False when the picker could not be shown (the caller then uses its own popup); true when it is showing.</returns>
        bool ShowItemPicker (string? title, IReadOnlyList<string> items, int selectedIndex, Action<int> completed);
    }
}
