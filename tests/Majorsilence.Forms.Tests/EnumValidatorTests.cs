using System;
using System.ComponentModel;
using Majorsilence.Forms.Layout;
using Xunit;

namespace Majorsilence.Forms.Tests
{
    // EnumValidator works out each enum type's rules once instead of reflecting on every call (performance plan F10);
    // it must still accept and reject exactly what it did.
    public class EnumValidatorTests
    {
        [Flags]
        private enum Bits : byte { None = 0, A = 1, B = 2, D = 8 }

        private enum Signed : short { Low = -3, Zero = 0, High = 7 }

        [Fact]
        public void Every_defined_member_of_a_plain_enum_passes ()
        {
            foreach (var value in Enum.GetValues<DockStyle> ())
                SourceGenerated.EnumValidator.Validate (value);
            foreach (var value in Enum.GetValues<Signed> ())
                SourceGenerated.EnumValidator.Validate (value);
        }

        [Fact]
        public void An_undefined_value_of_a_plain_enum_throws ()
        {
            var e = Assert.Throws<InvalidEnumArgumentException> (() => SourceGenerated.EnumValidator.Validate ((DockStyle)42));
            Assert.Equal ("value", e.ParamName);
            Assert.Throws<InvalidEnumArgumentException> (() => SourceGenerated.EnumValidator.Validate ((Signed)(-1)));
            Assert.Throws<InvalidEnumArgumentException> (() => SourceGenerated.EnumValidator.Validate ((Signed)1));
        }

        [Fact]
        public void Any_combination_of_defined_flags_passes ()
        {
            SourceGenerated.EnumValidator.Validate (Bits.None);
            SourceGenerated.EnumValidator.Validate (Bits.A | Bits.B | Bits.D);
            SourceGenerated.EnumValidator.Validate (AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right | AnchorStyles.Bottom);
        }

        [Fact]
        public void A_flag_outside_the_defined_bits_throws ()
        {
            Assert.Throws<InvalidEnumArgumentException> (() => SourceGenerated.EnumValidator.Validate ((Bits)4));
            Assert.Throws<InvalidEnumArgumentException> (() => SourceGenerated.EnumValidator.Validate (Bits.A | (Bits)128));
            Assert.Throws<InvalidEnumArgumentException> (() => SourceGenerated.EnumValidator.Validate ((AnchorStyles)16));
        }

        [Fact]
        public void An_invalid_Dock_set_on_a_control_still_throws ()
        {
            using var panel = new Panel ();

            Assert.Throws<InvalidEnumArgumentException> (() => panel.Dock = (DockStyle)99);
        }
    }
}
