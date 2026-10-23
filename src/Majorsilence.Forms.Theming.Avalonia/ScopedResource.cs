using System.Collections.Concurrent;

namespace Majorsilence.Forms.Theming.Avalonia
{
    using global::Avalonia;
    using global::Avalonia.Controls;

    /// <summary>
    /// A Fluent theme resource scoped to the controls a class or id rule matches. A <c>Style</c>'s own <c>Resources</c> are visible to
    /// the whole application, not just to the controls the style matches, so a style cannot scope <c>ButtonBackground</c> to
    /// <c>Button.primary</c>. Instead a style <em>sets</em> an attached property named after the resource key, which puts the value in the
    /// matched control's own <c>Resources</c> (where the Fluent template's <c>DynamicResource</c> finds it first) and takes it out when the
    /// style stops matching. One property per key, because Avalonia resolves each property on its own: two rules that scope different keys
    /// to the same control both apply, and for the same key the style that wins is the one the cascade says.
    /// </summary>
    internal static class ScopedResource
    {
        private static readonly ConcurrentDictionary<string, AvaloniaProperty> properties = new (StringComparer.Ordinal);

        /// <summary>The attached property that holds the scoped value of the resource <paramref name="key"/>.</summary>
        public static AvaloniaProperty For (string key) => properties.GetOrAdd (key, Register);

        private static AvaloniaProperty Register (string key)
        {
            // Registered on first use, one per resource key (a few dozen at most, and never unregistered): the analyzer wants static
            // initializers, which cannot know the keys.
#pragma warning disable AVP1001
            var property = AvaloniaProperty.RegisterAttached<Marker, Control, object?> (key);
#pragma warning restore AVP1001

            property.Changed.AddClassHandler<Control> ((control, e) => {
                if (e.NewValue is { } value)
                    control.Resources[key] = value;
                else
                    control.Resources.Remove (key);
            });

            return property;
        }

        // The owner type the properties are registered under; nothing else.
        private sealed class Marker : AvaloniaObject
        {
        }
    }
}
