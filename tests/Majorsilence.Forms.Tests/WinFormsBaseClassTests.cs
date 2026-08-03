using System.Linq;
using Xunit;

namespace Majorsilence.Forms.Tests
{
    /// <summary>
    /// Covers the intermediate base classes added for docs/winforms-gap-plan.md item 3.
    ///
    /// The point of these is the hierarchy, not the members: migrated code writes
    /// <c>class MyButton : ButtonBase</c>, <c>if (c is ListControl)</c>, and
    /// <c>Controls.OfType&lt;ButtonBase&gt;()</c>. A base class that exists but that nothing derives
    /// from would compile and then silently match nothing — so these assert the real relationships.
    /// </summary>
    public class WinFormsBaseClassTests
    {
        [Theory]
        [InlineData (typeof (Button))]
        [InlineData (typeof (CheckBox))]
        [InlineData (typeof (RadioButton))]
        public void The_button_controls_derive_from_ButtonBase (System.Type control)
            => Assert.True (typeof (ButtonBase).IsAssignableFrom (control), $"{control.Name} should derive from ButtonBase");

        [Fact]
        public void ListBox_derives_from_ListControl ()
            => Assert.True (typeof (ListControl).IsAssignableFrom (typeof (ListBox)));

        [Fact]
        public void The_bases_sit_under_Control_so_they_can_be_added_to_a_form ()
        {
            Assert.True (typeof (Control).IsAssignableFrom (typeof (ButtonBase)));
            Assert.True (typeof (Control).IsAssignableFrom (typeof (ListControl)));
            Assert.True (typeof (Control).IsAssignableFrom (typeof (UpDownBase)));
        }

        [Fact]
        public void A_button_is_reachable_through_its_base_type ()
        {
            // The pattern migrated code actually uses: collect by base type and set a shared property.
            using var form = new Form ();
            var button = new Button { Text = "ok" };
            var check = new CheckBox { Text = "on" };
            form.Controls.Add (button);
            form.Controls.Add (check);

            var buttons = form.Controls.OfType<ButtonBase> ().ToArray ();

            Assert.Equal (2, buttons.Length);
            foreach (var b in buttons)
                b.FlatStyle = FlatStyle.Flat;

            Assert.Equal (FlatStyle.Flat, button.FlatStyle);
            Assert.Equal (FlatStyle.Flat, check.FlatStyle);
        }

        [Fact]
        public void Derived_implementations_still_win_when_called_through_the_base ()
        {
            // Button.AutoEllipsis carries real layout/invalidate behaviour. Reparenting kept that
            // implementation as an override rather than replacing it with the base's auto-property,
            // so a call through the base type must still reach it.
            ButtonBase button = new Button ();
            button.AutoEllipsis = true;
            Assert.True (button.AutoEllipsis);

            button.TextAlign = ContentAlignment.BottomRight;
            Assert.Equal (ContentAlignment.BottomRight, ((Button)button).TextAlign);
        }

        [Fact]
        public void ListControl_resolves_item_text_through_DisplayMember ()
        {
            var list = new ListBox { DisplayMember = nameof (Person.Name) };

            Assert.Equal ("Ada", list.GetItemText (new Person { Name = "Ada" }));
            // With no DisplayMember set, the item's own ToString wins.
            Assert.Equal ("plain", new ListBox ().GetItemText ("plain"));
        }

        [Fact]
        public void A_custom_control_can_derive_from_ButtonBase ()
        {
            // The declaration that could not compile before this item.
            var custom = new CustomButton ();
            Assert.IsAssignableFrom<ButtonBase> (custom);
            Assert.IsAssignableFrom<Control> (custom);
        }

        private sealed class CustomButton : ButtonBase { }

        private sealed class Person
        {
            public string Name { get; set; } = string.Empty;
        }
    }
}
