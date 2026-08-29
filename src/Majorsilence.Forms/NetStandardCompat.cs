// Compat shims for BCL guard-clause / math helpers that were added well after netstandard2.0 was
// frozen. They live directly on the sealed BCL exception types (ArgumentNullException.ThrowIfNull and
// friends) or on System.Math, so they can't be polyfilled by adding members from here -- these
// wrappers forward to the real thing on the .NET TFMs and fall back to a manual throw / inline
// implementation on netstandard2.0 (.NET Framework 4.7.2+, Mono, older Unity).
//
// Same pattern, and mostly the same code, as Majorsilence.Forms.Drawing.Common's NetStandardCompat.cs.
// Placed in the root Majorsilence.Forms namespace: C# name lookup walks outward through enclosing
// namespaces, so the sub-namespaces (Majorsilence.Forms.Layout, .Printing, .Renderers, ...) see
// Guard/MathCompat unqualified without a using.

using System;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
#if NETSTANDARD2_0
using System.Threading;
using System.Threading.Tasks;
#endif

namespace Majorsilence.Forms
{
    internal static class Guard
    {
        public static void ThrowIfNull ([NotNull] object? argument, [CallerArgumentExpression (nameof (argument))] string? paramName = null)
        {
#if NETSTANDARD2_0
            if (argument is null)
                throw new ArgumentNullException (paramName);
#else
            ArgumentNullException.ThrowIfNull (argument, paramName);
#endif
        }

        public static void ThrowIfNullOrEmpty ([NotNull] string? argument, [CallerArgumentExpression (nameof (argument))] string? paramName = null)
        {
#if NETSTANDARD2_0
            // Checked separately (rather than via string.IsNullOrEmpty) so the compiler tracks that
            // argument is non-null past this point -- netstandard2.0's IsNullOrEmpty has no
            // [NotNullWhen(false)] annotation.
            if (argument is null)
                throw new ArgumentNullException (paramName);
            if (argument.Length == 0)
                throw new ArgumentException ("The value cannot be an empty string.", paramName);
#else
            ArgumentException.ThrowIfNullOrEmpty (argument, paramName);
#endif
        }

        public static void ThrowIfNegative (int value, [CallerArgumentExpression (nameof (value))] string? paramName = null)
        {
#if NETSTANDARD2_0
            if (value < 0)
                throw new ArgumentOutOfRangeException (paramName, value, "Value must be a non-negative value.");
#else
            ArgumentOutOfRangeException.ThrowIfNegative (value, paramName);
#endif
        }

        public static void ThrowIfNegativeOrZero (int value, [CallerArgumentExpression (nameof (value))] string? paramName = null)
        {
#if NETSTANDARD2_0
            if (value <= 0)
                throw new ArgumentOutOfRangeException (paramName, value, "Value must be a non-negative and non-zero value.");
#else
            ArgumentOutOfRangeException.ThrowIfNegativeOrZero (value, paramName);
#endif
        }

        // Non-int numeric overloads. The real ArgumentOutOfRangeException.ThrowIf* are generic over
        // INumber<T>; only the concrete types this codebase actually passes are mirrored here.
        public static void ThrowIfNegative (decimal value, [CallerArgumentExpression (nameof (value))] string? paramName = null)
        {
#if NETSTANDARD2_0
            if (value < 0m)
                throw new ArgumentOutOfRangeException (paramName, value, "Value must be a non-negative value.");
#else
            ArgumentOutOfRangeException.ThrowIfNegative (value, paramName);
#endif
        }

        public static void ThrowIfNegative (double value, [CallerArgumentExpression (nameof (value))] string? paramName = null)
        {
#if NETSTANDARD2_0
            if (value < 0d)
                throw new ArgumentOutOfRangeException (paramName, value, "Value must be a non-negative value.");
#else
            ArgumentOutOfRangeException.ThrowIfNegative (value, paramName);
#endif
        }

        public static void ThrowIfNegativeOrZero (double value, [CallerArgumentExpression (nameof (value))] string? paramName = null)
        {
#if NETSTANDARD2_0
            if (value <= 0d)
                throw new ArgumentOutOfRangeException (paramName, value, "Value must be a non-negative and non-zero value.");
#else
            ArgumentOutOfRangeException.ThrowIfNegativeOrZero (value, paramName);
#endif
        }

        public static void ThrowIfNegativeOrZero (float value, [CallerArgumentExpression (nameof (value))] string? paramName = null)
        {
#if NETSTANDARD2_0
            if (value <= 0f)
                throw new ArgumentOutOfRangeException (paramName, value, "Value must be a non-negative and non-zero value.");
#else
            ArgumentOutOfRangeException.ThrowIfNegativeOrZero (value, paramName);
#endif
        }

        public static void ThrowIfLessThan<T> (T value, T other, [CallerArgumentExpression (nameof (value))] string? paramName = null)
            where T : IComparable<T>
        {
#if NETSTANDARD2_0
            if (value.CompareTo (other) < 0)
                throw new ArgumentOutOfRangeException (paramName, value, $"Value must be greater than or equal to {other}.");
#else
            ArgumentOutOfRangeException.ThrowIfLessThan (value, other, paramName);
#endif
        }

        public static void ThrowIfLessThanOrEqual<T> (T value, T other, [CallerArgumentExpression (nameof (value))] string? paramName = null)
            where T : IComparable<T>
        {
#if NETSTANDARD2_0
            if (value.CompareTo (other) <= 0)
                throw new ArgumentOutOfRangeException (paramName, value, $"Value must be greater than {other}.");
#else
            ArgumentOutOfRangeException.ThrowIfLessThanOrEqual (value, other, paramName);
#endif
        }

        public static void ThrowIfGreaterThan<T> (T value, T other, [CallerArgumentExpression (nameof (value))] string? paramName = null)
            where T : IComparable<T>
        {
#if NETSTANDARD2_0
            if (value.CompareTo (other) > 0)
                throw new ArgumentOutOfRangeException (paramName, value, $"Value must be less than or equal to {other}.");
#else
            ArgumentOutOfRangeException.ThrowIfGreaterThan (value, other, paramName);
#endif
        }

        public static void ThrowIfGreaterThanOrEqual<T> (T value, T other, [CallerArgumentExpression (nameof (value))] string? paramName = null)
            where T : IComparable<T>
        {
#if NETSTANDARD2_0
            if (value.CompareTo (other) >= 0)
                throw new ArgumentOutOfRangeException (paramName, value, $"Value must be less than {other}.");
#else
            ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual (value, other, paramName);
#endif
        }
    }

    internal static class MathCompat
    {
        public static int Clamp (int value, int min, int max)
#if NETSTANDARD2_0
            => value < min ? min : value > max ? max : value;
#else
            => Math.Clamp (value, min, max);
#endif

        public static double Clamp (double value, double min, double max)
#if NETSTANDARD2_0
            => value < min ? min : value > max ? max : value;
#else
            => Math.Clamp (value, min, max);
#endif
    }

#if NETSTANDARD2_0
    // The non-generic TaskCompletionSource arrived in .NET 5; netstandard2.0 has only the generic one.
    // This forwards to a TaskCompletionSource<bool> so the InvokeAsync overloads in ControlAndFormParity
    // compile unchanged. Only the members that class actually uses are provided.
    internal sealed class TaskCompletionSource
    {
        private readonly TaskCompletionSource<bool> _inner =
            new TaskCompletionSource<bool> (TaskCreationOptions.RunContinuationsAsynchronously);

        public Task Task => _inner.Task;

        public bool TrySetResult () => _inner.TrySetResult (true);
        public bool TrySetException (Exception exception) => _inner.TrySetException (exception);
        public bool TrySetCanceled () => _inner.TrySetCanceled ();
        public bool TrySetCanceled (CancellationToken cancellationToken) => _inner.TrySetCanceled (cancellationToken);

        public void SetCanceled (CancellationToken cancellationToken)
        {
            if (!_inner.TrySetCanceled (cancellationToken))
                throw new InvalidOperationException ("An attempt was made to transition a task to a final state when it had already completed.");
        }
    }

    // String helpers added to the BCL after netstandard2.0 was frozen. Extension methods bind only when
    // no matching instance method exists, so on the .NET TFMs the real instance methods still win.
    internal static class NetStandardStringExtensions
    {
        public static bool Contains (this string s, char value)
            => s.IndexOf (value) >= 0;

        public static bool Contains (this string s, string value, StringComparison comparisonType)
            => s.IndexOf (value, comparisonType) >= 0;

        public static bool StartsWith (this string s, char value)
            => s.Length > 0 && s[0] == value;

        public static bool EndsWith (this string s, char value)
            => s.Length > 0 && s[s.Length - 1] == value;

        public static string Replace (this string s, string oldValue, string? newValue, StringComparison comparisonType)
        {
            if (oldValue is null || oldValue.Length == 0)
                return s;

            var result = new System.Text.StringBuilder ();
            int start = 0, index;
            while ((index = s.IndexOf (oldValue, start, comparisonType)) >= 0)
            {
                result.Append (s, start, index - start).Append (newValue);
                start = index + oldValue.Length;
            }
            result.Append (s, start, s.Length - start);
            return result.ToString ();
        }

        public static string[] Split (this string s, char separator, StringSplitOptions options)
            => s.Split (new[] { separator }, options);
    }

    internal static class NetStandardCollectionExtensions
    {
        public static void Deconstruct<TKey, TValue> (this System.Collections.Generic.KeyValuePair<TKey, TValue> pair,
            out TKey key, out TValue value)
        {
            key = pair.Key;
            value = pair.Value;
        }

        public static bool Remove<TKey, TValue> (this System.Collections.Generic.Dictionary<TKey, TValue> dictionary,
            TKey key, out TValue value)
        {
            if (dictionary.TryGetValue (key, out value!))
            {
                dictionary.Remove (key);
                return true;
            }
            value = default!;
            return false;
        }
    }
#endif

    internal static class BinaryReaderCompat
    {
        // BinaryReader.Read7BitEncodedInt() is protected until .NET 5.
        public static int Read7BitEncodedIntCompat (this System.IO.BinaryReader reader)
#if NETSTANDARD2_0
        {
            // Same LEB128 decode the BCL uses.
            int count = 0, shift = 0;
            byte b;
            do
            {
                if (shift == 5 * 7)
                    throw new FormatException ("Too many bytes in what should have been a 7-bit encoded Int32.");
                b = reader.ReadByte ();
                count |= (b & 0x7F) << shift;
                shift += 7;
            } while ((b & 0x80) != 0);
            return count;
        }
#else
            => reader.Read7BitEncodedInt ();
#endif
    }

    internal static class EnumCompat
    {
        public static T[] GetValues<T> () where T : struct, Enum
#if NETSTANDARD2_0
            => (T[]) Enum.GetValues (typeof (T));
#else
            => Enum.GetValues<T> ();
#endif

        public static bool IsDefined<T> (T value) where T : struct, Enum
#if NETSTANDARD2_0
            => Enum.IsDefined (typeof (T), value);
#else
            => Enum.IsDefined (value);
#endif
    }

    // System.Collections.Generic.ReferenceEqualityComparer is a .NET 5 addition.
    internal sealed class ReferenceEqualityComparer<T> : System.Collections.Generic.IEqualityComparer<T>
        where T : class
    {
        public static readonly ReferenceEqualityComparer<T> Instance = new ReferenceEqualityComparer<T> ();

        public bool Equals (T? x, T? y) => ReferenceEquals (x, y);

        public int GetHashCode (T obj) => RuntimeHelpers.GetHashCode (obj);
    }

    internal static class OperatingSystemCompat
    {
#if NETSTANDARD2_0
        public static bool IsWindows () => System.Runtime.InteropServices.RuntimeInformation.IsOSPlatform (System.Runtime.InteropServices.OSPlatform.Windows);
        public static bool IsLinux () => System.Runtime.InteropServices.RuntimeInformation.IsOSPlatform (System.Runtime.InteropServices.OSPlatform.Linux);
        public static bool IsMacOS () => System.Runtime.InteropServices.RuntimeInformation.IsOSPlatform (System.Runtime.InteropServices.OSPlatform.OSX);
#else
        public static bool IsWindows () => OperatingSystem.IsWindows ();
        public static bool IsLinux () => OperatingSystem.IsLinux ();
        public static bool IsMacOS () => OperatingSystem.IsMacOS ();
#endif
    }
}
