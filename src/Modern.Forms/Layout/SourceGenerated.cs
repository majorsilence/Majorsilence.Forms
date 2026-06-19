// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.ComponentModel;

namespace Modern.Forms.Layout;

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
        public static void Validate (Enum e)
        {
            var type = e.GetType ();

            if (Attribute.IsDefined (type, typeof (FlagsAttribute))) {
                // Flags enum: the value must not contain any bit outside the union of defined members.
                long mask = 0;
                foreach (var member in Enum.GetValues (type))
                    mask |= Convert.ToInt64 (member);

                var value = Convert.ToInt64 (e);
                if ((value & ~mask) != 0)
                    throw new InvalidEnumArgumentException ("value", unchecked ((int) value), type);

                return;
            }

            if (!Enum.IsDefined (type, e))
                throw new InvalidEnumArgumentException ("value", Convert.ToInt32 (e), type);
        }
    }
}
