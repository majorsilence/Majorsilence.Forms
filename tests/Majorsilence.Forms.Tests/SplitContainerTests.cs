using System.ComponentModel;
using Xunit;

namespace Majorsilence.Forms.Tests
{
    public class SplitContainerTests
    {
        [Fact]
        public void ImplementsISupportInitialize ()
        {
            // WinForms designer-generated InitializeComponent code always brackets a
            // SplitContainer's property assignments with
            // ((ISupportInitialize)(this.splitContainer1)).BeginInit()/EndInit() -- found via a
            // real migrated designer app (ReportDesigner.Forms) crashing with
            // InvalidCastException on that cast for every dialog containing a SplitContainer
            // (DialogDatabase, DataSetsCtl, DialogExprEditor, RdlUserControl, SQLCtl -- reachable
            // just from File > New, which constructs DialogDatabase).
            using var control = new SplitContainer ();
            var isi = Assert.IsAssignableFrom<ISupportInitialize> (control);

            isi.BeginInit ();
            isi.EndInit ();
        }
    }
}
