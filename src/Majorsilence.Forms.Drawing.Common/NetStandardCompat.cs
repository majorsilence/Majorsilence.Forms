// Every other namespace in this project (Majorsilence.Forms.Drawing, .Drawing2D, .Imaging, ...) needs
// unqualified access to Guard/MathCompat/etc. below.
global using Majorsilence.Forms.Drawing.Common;

using System.Runtime.CompilerServices;

namespace Majorsilence.Forms.Drawing.Common
{
    // BCL guard-clause helpers (ArgumentNullException.ThrowIfNull and friends) added well after
    // netstandard2.0 was frozen. They live directly on the sealed exception types, so they can't be
    // polyfilled by adding members to those types from here -- these wrappers forward to the real
    // thing on the .NET TFMs and fall back to a manual throw on netstandard2.0.
    internal static class Guard
    {
        public static void ThrowIfNull (object? argument, [CallerArgumentExpression (nameof (argument))] string? paramName = null)
        {
#if NETSTANDARD2_0
            if (argument is null)
                throw new ArgumentNullException (paramName);
#else
            ArgumentNullException.ThrowIfNull (argument, paramName);
#endif
        }

        public static void ThrowIfNegative (int value, [CallerArgumentExpression (nameof (value))] string? paramName = null)
        {
#if NETSTANDARD2_0
            if (value < 0)
                throw new ArgumentOutOfRangeException (paramName);
#else
            ArgumentOutOfRangeException.ThrowIfNegative (value, paramName);
#endif
        }

        public static void ThrowIfNegativeOrZero (int value, [CallerArgumentExpression (nameof (value))] string? paramName = null)
        {
#if NETSTANDARD2_0
            if (value <= 0)
                throw new ArgumentOutOfRangeException (paramName);
#else
            ArgumentOutOfRangeException.ThrowIfNegativeOrZero (value, paramName);
#endif
        }

        public static void ThrowIfDisposed (bool condition, object instance)
        {
#if NETSTANDARD2_0
            if (condition)
                throw new ObjectDisposedException (instance.GetType ().FullName);
#else
            ObjectDisposedException.ThrowIf (condition, instance);
#endif
        }
    }

    // Math.Clamp<T> and MathF are likewise later BCL additions absent from netstandard2.0.
    internal static class MathCompat
    {
        public static int Clamp (int value, int min, int max)
#if NETSTANDARD2_0
            => value < min ? min : value > max ? max : value;
#else
            => Math.Clamp (value, min, max);
#endif

        public static float Clamp (float value, float min, float max)
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

    internal static class MathFCompat
    {
        public static float Round (float x)
#if NETSTANDARD2_0
            => (float)Math.Round (x);
#else
            => MathF.Round (x);
#endif
    }

    // BitConverter.TryWriteBytes(Span<byte>, T) is a .NET Core 2.1+ addition.
    internal static class BitConverterCompat
    {
        public static bool TryWriteBytes (Span<byte> destination, float value)
        {
#if NETSTANDARD2_0
            var bytes = BitConverter.GetBytes (value);
            if (destination.Length < bytes.Length)
                return false;
            bytes.CopyTo (destination);
            return true;
#else
            return BitConverter.TryWriteBytes (destination, value);
#endif
        }
    }

    // Encoding.Latin1 is a .NET 5+ addition; ISO-8859-1 is the same encoding by codepage.
    internal static class EncodingCompat
    {
#if NETSTANDARD2_0
        public static System.Text.Encoding Latin1 { get; } = System.Text.Encoding.GetEncoding ("ISO-8859-1");
#else
        public static System.Text.Encoding Latin1 => System.Text.Encoding.Latin1;
#endif
    }
}
