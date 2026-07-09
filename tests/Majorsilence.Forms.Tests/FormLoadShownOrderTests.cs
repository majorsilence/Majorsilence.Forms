using System.Collections.Generic;
using Xunit;

namespace Majorsilence.Forms.Tests
{
    // WinForms parity: Form.Show() raises Load once, before the form is displayed, then Shown once after
    // first display -- Load and Shown are distinct lifecycle points in that order. (Previously the fork
    // raised Load coupled inside OnShown, i.e. after the window was shown.)
    public class FormLoadShownOrderTests
    {
        [Fact]
        public void Show_raises_Load_then_Shown_each_once ()
        {
            var order = new List<string> ();
            using var form = new Form { Width = 200, Height = 150 };
            form.Load += (s, e) => order.Add ("Load");
            form.Shown += (s, e) => order.Add ("Shown");

            form.Show ();

            Assert.Equal (new[] { "Load", "Shown" }, order);
        }

        [Fact]
        public void Load_fires_exactly_once ()
        {
            var loads = 0;
            using var form = new Form { Width = 200, Height = 150 };
            form.Load += (s, e) => loads++;

            form.Show ();

            Assert.Equal (1, loads);
        }
    }
}
