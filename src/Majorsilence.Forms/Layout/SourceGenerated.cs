// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.ComponentModel;
#if NET8_0_OR_GREATER
using System.Runtime.CompilerServices;
#endif

namespace Majorsilence.Forms.Layout;

internal sealed class SourceGenerated
{
    internal sealed class EnumValidator
    {
        /// <summary>
        /// Validates that an enum value is valid for its type, throwing
        /// <see cref="InvalidEnumArgumentException"/> otherwise — matching WinForms semantics.
        /// For a <see cref="FlagsAttribute"/> enum any combination of defined bits is accepted;
        /// for a normal enum the value must be one of the defined members.
        /// All call sites pass a parameter named <c>value</c>.
        /// </summary>
        /// <remarks>
        /// Upstream generates a switch per enum at compile time. This used to answer by reflection on every call
        /// (<c>Attribute.IsDefined</c> and <c>Enum.IsDefined</c> on a boxed value), and some callers validate on
        /// every read: <c>CommonProperties.xGetDock</c> does, and a layout pass reads every child's
        /// <c>Dock</c> several times, so reflection was 90 % of a docked layout pass (LayoutBenchmarks). The rules
        /// for each enum type are now worked out once, and checking a value neither reflects nor boxes.
        /// </remarks>
        public static void Validate<T> (T value) where T : struct, Enum
        {
            if (!Rules<T>.IsValid (value))
                throw new InvalidEnumArgumentException ("value", unchecked ((int)Bits (value)), typeof (T));
        }

        private static class Rules<T> where T : struct, Enum
        {
            private static readonly bool flags = typeof (T).IsDefined (typeof (FlagsAttribute), false);
            private static readonly ulong mask;
            private static readonly HashSet<T> defined = new ();

            static Rules ()
            {
#if NET8_0_OR_GREATER
                var values = Enum.GetValues<T> ();
#else
                var values = (T[])Enum.GetValues (typeof (T));
#endif
                foreach (var member in values) {
                    defined.Add (member);
                    mask |= Bits (member);
                }
            }

            // Flags: no bit outside the union of the defined members. Otherwise: one of the defined members.
            public static bool IsValid (T value) => flags ? (Bits (value) & ~mask) == 0 : defined.Contains (value);
        }

        // The value's bits, as stored (not sign-extended); used consistently for the mask and the value.
        private static ulong Bits<T> (T value) where T : struct, Enum
        {
#if NET8_0_OR_GREATER
            return Unsafe.SizeOf<T> () switch {
                1 => Unsafe.As<T, byte> (ref value),
                2 => Unsafe.As<T, ushort> (ref value),
                4 => Unsafe.As<T, uint> (ref value),
                _ => Unsafe.As<T, ulong> (ref value),
            };
#else
            return unchecked ((ulong)Convert.ToInt64 (value));
#endif
        }
    }
}
