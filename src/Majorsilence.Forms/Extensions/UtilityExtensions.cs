namespace Majorsilence.Forms
{
    static class UtilityExtensions
    {
        public static bool In<T> (this T enumeration, params T[] values) where T : Enum
        {
            foreach (var en in values)
                if (enumeration.Equals (en))
                    return true;

            return false;
        }

        public static bool HasValue (this string str) => !string.IsNullOrEmpty (str);

        public static IEnumerable<T> WhereNotNull<T> (this IEnumerable<T?> values) where T : class
        {
            return values.Where (p => p != null).Cast<T> ();
        }
    }
}
