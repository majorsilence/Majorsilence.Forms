using Majorsilence.Forms.Telerik;
using Xunit;

namespace Majorsilence.Forms.Tests
{
    // A RadToggleSwitch starts as Telerik's does. Measured (UI for WinForms 2026.2.520): Value True, OnText "ON",
    // OffText "OFF", ToggleStateMode ClickAndDrag, whose values are None 0, Click 1, Drag 2 and ClickAndDrag 3. A
    // designer writes only what differs from those, so a switch left on in the designer had no Value line and came
    // up off here -- an "Active Status" filter a form opens with was off, listing the inactive records too.
    public class RadToggleSwitchDefaultsTests
    {
        [Fact]
        public void A_new_switch_has_Telerik_s_defaults ()
        {
            using var toggle = new RadToggleSwitch ();

            Assert.True (toggle.Value);
            Assert.Equal ("ON", toggle.OnText);
            Assert.Equal ("OFF", toggle.OffText);
            Assert.Equal (ToggleStateMode.ClickAndDrag, toggle.ToggleStateMode);
        }

        [Fact]
        public void ToggleStateMode_has_Telerik_s_values ()
        {
            Assert.Equal (0, (int) ToggleStateMode.None);
            Assert.Equal (1, (int) ToggleStateMode.Click);
            Assert.Equal (2, (int) ToggleStateMode.Drag);
            Assert.Equal (3, (int) ToggleStateMode.ClickAndDrag);
        }

        [Fact]
        public void A_new_switch_raises_no_value_change ()
        {
            var changes = 0;
            using var toggle = new RadToggleSwitch ();
            toggle.ValueChanged += (_, _) => changes++;

            toggle.Value = true;

            Assert.Equal (0, changes);
        }
    }
}
