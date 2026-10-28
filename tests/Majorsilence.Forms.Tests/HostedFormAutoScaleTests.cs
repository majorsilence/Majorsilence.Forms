using System;
using System.Drawing;
using Majorsilence.Forms.Headless;
using Xunit;

namespace Majorsilence.Forms.Tests
{
    // Upstream scales a form on its first layout whatever shows it: an MDI child takes the same legacy
    // AutoScaleBaseSize scale as a top-level window. Measured on WinForms (.NET 10) with a 12pt Microsoft
    // Sans Serif default font, a 400 x 200 child with a 120 x 30 button under AutoScaleBaseSize 6 x 16
    // opened at 565 x 254 with a 170 x 38 button, the same as the top-level form. Here the child opened
    // at its designed size. (AutoScaleMode.Font is deliberately not applied in this library; see
    // AutoScaleEngine.TryGetFactor.)
    [Collection ("Headless")]
    public class HostedFormAutoScaleTests
    {
        private static Form Designed (Action<Form> scaling)
        {
            var form = new Form {
                FormBorderStyle = FormBorderStyle.None,
                Font = new Majorsilence.Forms.Drawing.Font (Control.DefaultFont.Name, 12f),
            };
            form.SuspendLayout ();
            scaling (form);
            form.ClientSize = new Size (400, 200);
            form.Controls.Add (new Button { Name = "b", Location = new Point (100, 50), Size = new Size (120, 30) });
            form.ResumeLayout (false);
            return form;
        }

        private static (Size client, Rectangle button) TopLevel (Action<Form> scaling)
        {
            using var form = Designed (scaling);
            form.Show ();
            return (form.ClientSize, form.Controls["b"]!.Bounds);
        }

        private static (Size client, Rectangle button) MdiChild (Action<Form> scaling)
        {
            using var parent = new Form { IsMdiContainer = true, ClientSize = new Size (1400, 900) };
            parent.Show ();
            var child = Designed (scaling);
            child.MdiParent = parent;
            child.Show ();
            return (child.ClientSize, child.Controls["b"]!.Bounds);
        }

        [Fact]
        public void An_MDI_child_takes_its_legacy_AutoScaleBaseSize_scale ()
        {
            HeadlessRenderer.Use ();
            Action<Form> legacy = f => f.AutoScaleBaseSize = new Size (6, 16);

            var top = TopLevel (legacy);
            var child = MdiChild (legacy);

            Assert.True (top.button.Width > 120, $"the top-level form should scale, its button is {top.button}");
            Assert.Equal (top.button, child.button);
            Assert.Equal (top.client, child.client);
        }
    }
}
