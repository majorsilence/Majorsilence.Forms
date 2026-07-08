using System;
using System.ComponentModel;
using System.Linq;
using Xunit;

namespace Majorsilence.Forms.Tests
{
    public class RadSupportInitializeTests
    {
        [Fact]
        public void Every_Telerik_control_implements_ISupportInitialize ()
        {
            // Real Telerik's RadControl base implements ISupportInitialize, so WinForms/VB
            // designer-generated InitializeComponent code brackets EVERY Rad control with
            // ((ISupportInitialize)(this.radFoo)).BeginInit()/EndInit() -- an unconditional cast.
            // Found via a real migrated app (TownSuite frmMainLogin) crashing with
            // InvalidCastException on RadWaitingBar the moment its designer code ran. Sweep the
            // whole compat assembly so the next control added can't regress this.
            var telerikAssembly = typeof (Majorsilence.Forms.Telerik.RadWaitingBar).Assembly;

            var missing = telerikAssembly.GetExportedTypes ()
                .Where (t => t.IsClass && !t.IsAbstract)
                .Where (t => typeof (Control).IsAssignableFrom (t))
                .Where (t => !typeof (ISupportInitialize).IsAssignableFrom (t))
                .Select (t => t.FullName)
                .OrderBy (n => n)
                .ToList ();

            Assert.True (missing.Count == 0,
                "Telerik compat controls missing ISupportInitialize (designer code casts unconditionally):\n" +
                string.Join ("\n", missing));
        }

        [Fact]
        public void RadWaitingBar_BeginInit_EndInit_roundtrip ()
        {
            // The concrete crash site: frmMainLogin.Designer.vb line 53.
            using var control = new Majorsilence.Forms.Telerik.RadWaitingBar ();
            var isi = Assert.IsAssignableFrom<ISupportInitialize> (control);

            isi.BeginInit ();
            isi.EndInit ();
        }

        [Fact]
        public void RadWaitingBar_element_tree_walks_typed ()
        {
            // Designer code CTypes each level of the element tree (frmMainLogin.Designer.vb):
            //   CType(bar.GetChildAt(0), RadWaitingBarElement).WaitingSpeed = 50
            //   CType(bar.GetChildAt(0).GetChildAt(0), WaitingBarContentElement).WaitingStyle = ...
            //   CType(bar.GetChildAt(0).GetChildAt(0).GetChildAt(0), WaitingBarSeparatorElement).Dash = False
            // so each GetChildAt(0) must return the typed child, not the base's untyped stub.
            using var bar = new Majorsilence.Forms.Telerik.RadWaitingBar ();

            var root = Assert.IsType<Majorsilence.Forms.Telerik.RadWaitingBarElement> (bar.GetChildAt (0));
            var content = Assert.IsType<Majorsilence.Forms.Telerik.WaitingBarContentElement> (root.GetChildAt (0));
            var separator = Assert.IsType<Majorsilence.Forms.Telerik.WaitingBarSeparatorElement> (content.GetChildAt (0));

            root.WaitingSpeed = 50;
            content.WaitingStyle = Majorsilence.Forms.Telerik.WaitingBarStyles.DotsSpinner;
            separator.Dash = false;
        }
    }
}
